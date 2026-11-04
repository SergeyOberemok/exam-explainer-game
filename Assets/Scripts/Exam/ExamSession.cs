using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ExamCards.Exam
{
    public readonly struct AnswerResult
    {
        public AnswerResult(bool isCorrect, int correctIndex)
        {
            IsCorrect = isCorrect;
            CorrectIndex = correctIndex;
        }

        public bool IsCorrect { get; }
        public int CorrectIndex { get; }
    }

    /// <summary>What <see cref="ExamSession.PlayNowAsync"/> did.</summary>
    public enum PlayNowOutcome
    {
        /// <summary>The question is now the current one.</summary>
        Moved,

        /// <summary>The question was already the current one; nothing changed.</summary>
        AlreadyCurrent,

        /// <summary>The question was already answered in this exam; nothing changed.</summary>
        AlreadyAnswered,
    }

    /// <summary>
    /// Walks through a round's questions one by one and keeps the score. Answers and 50/50 go through
    /// the <see cref="IExamRound"/>, which holds the answer key; only one such request runs at a time.
    /// </summary>
    public sealed class ExamSession
    {
        /// <summary>How many wrong answers a 50/50 removes.</summary>
        public const int EliminatedPerUse = 2;

        /// <summary>How many 50/50 uses a round starts with.</summary>
        public const int DefaultFiftyFiftyCharges = 2;

        /// <summary>How many lives an exam starts with. Each wrong answer costs one.</summary>
        public const int MaxLives = 3;

        readonly IExamRound _round;
        readonly List<Question> _questions;

        // By question id, because a question skipped by PlayNowAsync keeps what 50/50 removed from it.
        readonly Dictionary<string, HashSet<int>> _eliminated = new Dictionary<string, HashSet<int>>();

        public ExamSession(IExamRound round)
        {
            _round = round ?? throw new ArgumentNullException(nameof(round));
            _questions = new List<Question>(
                round.Questions ?? throw new ArgumentException("The round has no question list.", nameof(round)));
            if (_questions.Count == 0)
                throw new ArgumentException("An exam needs at least one question.", nameof(round));
            FiftyFiftyRemaining = Math.Max(0, round.FiftyFiftyCharges);
        }

        /// <summary>True while an answer, 50/50 or play-now request is running.</summary>
        public bool IsBusy { get; private set; }

        /// <summary>How many 50/50 uses are left in this exam.</summary>
        public int FiftyFiftyRemaining { get; private set; }

        /// <summary>True if the current question has had its wrong answers removed.</summary>
        public bool IsCurrentEliminated => _eliminated.ContainsKey(Current.Id);

        public bool IsEliminated(int optionIndex) =>
            _eliminated.TryGetValue(Current.Id, out var eliminated) && eliminated.Contains(optionIndex);

        /// <summary>True if 50/50 can be used on the current question right now.</summary>
        public bool CanEliminate =>
            !IsBusy && !IsFinished && !IsCurrentAnswered && !IsCurrentEliminated && FiftyFiftyRemaining > 0
            && Current.Original.Answers.Count - 1 >= EliminatedPerUse;

        /// <summary>Lives left. The exam ends after the current question when this reaches 0.</summary>
        public int Lives { get; private set; } = MaxLives;

        public int CurrentIndex { get; private set; }
        public int Total => _questions.Count;
        public int Score { get; private set; }
        public Question Current => _questions[CurrentIndex];

        /// <summary>The option picked for the current question, or null if it hasn't been answered.</summary>
        public int? AnsweredIndex { get; private set; }

        /// <summary>The correct answer and feedback for the current question; null until it is answered.</summary>
        public AnswerReview CurrentReview { get; private set; }

        public bool IsCurrentAnswered => AnsweredIndex.HasValue;
        public bool IsCurrentRevealed { get; private set; }

        /// <summary>True once <see cref="MoveNext"/> has been called on the last question.</summary>
        public bool IsFinished { get; private set; }

        /// <summary>How many questions have been answered so far, including the current one.</summary>
        public int AnsweredCount => CurrentIndex + (IsCurrentAnswered ? 1 : 0);

        /// <summary>The text to display: the simplified version once revealed, otherwise the original.</summary>
        public QuestionPrompt CurrentPrompt => IsCurrentRevealed ? Current.Simplified : Current.Original;

        /// <summary>
        /// Feedback and explanation for <see cref="CurrentPrompt"/>; null until answered. Falls back to the
        /// original's review if the simplified one is missing.
        /// </summary>
        public PromptReview CurrentPromptReview =>
            CurrentReview == null ? null
            : IsCurrentRevealed && CurrentReview.Simplified != null ? CurrentReview.Simplified
            : CurrentReview.Original;

        /// <summary>
        /// Answers the current question. If the request fails, nothing changes and the player can answer again.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">No such option.</exception>
        /// <exception cref="InvalidOperationException">Already answered, eliminated, finished, or busy.</exception>
        /// <exception cref="FormatException">The round returned a review that doesn't fit the question.</exception>
        public async Task<AnswerResult> AnswerAsync(int optionIndex, CancellationToken cancellationToken = default)
        {
            var question = Current;
            if (optionIndex < 0 || optionIndex >= question.Original.Answers.Count)
                throw new ArgumentOutOfRangeException(nameof(optionIndex));
            if (IsFinished)
                throw new InvalidOperationException("The exam is finished.");
            if (IsCurrentAnswered)
                throw new InvalidOperationException("The current question has already been answered.");
            if (IsEliminated(optionIndex))
                throw new InvalidOperationException("That answer was eliminated by 50/50.");
            if (IsBusy)
                throw new InvalidOperationException("A request for the current question is still running.");

            AnswerReview review;
            IsBusy = true;
            try
            {
                review = await _round.SubmitAnswerAsync(question.Id, optionIndex, cancellationToken);
            }
            finally
            {
                IsBusy = false;
            }
            QuestionValidator.ValidateReview(question, review);

            AnsweredIndex = optionIndex;
            CurrentReview = review;
            var isCorrect = optionIndex == review.CorrectIndex;
            if (isCorrect)
                Score++;
            else
                Lives--;
            return new AnswerResult(isCorrect, review.CorrectIndex);
        }

        /// <summary>
        /// Removes wrong answers from the current question and uses up one charge. Returns false if 50/50
        /// can't be used now. The correct answer is never removed.
        /// </summary>
        /// <exception cref="FormatException">The round returned answers that don't fit the question.</exception>
        public async Task<bool> EliminateAsync(CancellationToken cancellationToken = default)
        {
            if (!CanEliminate)
                return false;

            var question = Current;
            IReadOnlyList<int> eliminated;
            IsBusy = true;
            try
            {
                eliminated = await _round.EliminateAsync(question.Id, cancellationToken);
            }
            finally
            {
                IsBusy = false;
            }
            QuestionValidator.ValidateEliminated(question, eliminated);

            _eliminated[question.Id] = new HashSet<int>(eliminated);
            FiftyFiftyRemaining--;
            return true;
        }

        /// <summary>Switches the current question to its simplified text. Returns false if nothing changed.</summary>
        public bool Reveal()
        {
            if (IsFinished || IsCurrentRevealed || !Current.CanReveal)
                return false;
            IsCurrentRevealed = true;
            return true;
        }

        /// <summary>Advances to the next question. Returns false when the exam is over.</summary>
        public bool MoveNext()
        {
            if (!IsCurrentAnswered)
                throw new InvalidOperationException("The current question has not been answered.");
            if (IsBusy)
                throw new InvalidOperationException("A request is still running.");
            if (Lives <= 0 || CurrentIndex + 1 >= _questions.Count)
            {
                IsFinished = true;
                return false;
            }

            CurrentIndex++;
            ResetCurrent();
            return true;
        }

        /// <summary>
        /// Where a question is in play order, or -1 if it isn't in this exam. Questions before
        /// <see cref="CurrentIndex"/> have been answered.
        /// </summary>
        public int IndexOf(string questionId) => _questions.FindIndex(q => q.Id == questionId);

        /// <summary>True if <see cref="PlayNowAsync"/> can run now.</summary>
        public bool CanPlayNow => !IsBusy && !IsFinished && Lives > 0;

        /// <summary>
        /// Makes a question, such as a search result, the current one. A question not yet in the exam is first
        /// added to the round and then counts towards the total. If the current question is unanswered, it is
        /// shown again right after; nothing about it is lost. If the request fails, nothing changes.
        /// </summary>
        /// <exception cref="InvalidOperationException">Finished, out of lives, or busy (see <see cref="CanPlayNow"/>).</exception>
        /// <exception cref="FormatException">The round returned a question that doesn't fit.</exception>
        public async Task<PlayNowOutcome> PlayNowAsync(string questionId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(questionId))
                throw new ArgumentException("No question id.", nameof(questionId));
            if (!CanPlayNow)
                throw new InvalidOperationException(IsBusy ? "A request is still running." : "The exam is over.");

            var index = IndexOf(questionId);
            if (index == CurrentIndex)
                return PlayNowOutcome.AlreadyCurrent;
            // Everything before the current question has been answered.
            if (index >= 0 && index < CurrentIndex)
                return PlayNowOutcome.AlreadyAnswered;

            Question question;
            if (index >= 0)
            {
                question = _questions[index];
                _questions.RemoveAt(index);
            }
            else
            {
                IsBusy = true;
                try
                {
                    question = await _round.AddQuestionAsync(questionId, cancellationToken);
                }
                finally
                {
                    IsBusy = false;
                }
                QuestionValidator.ValidateQuestion(question);
                if (question.Id != questionId)
                    throw new FormatException($"Question {questionId}: the round added question {question.Id} instead.");
            }

            // An answered current question stays where it is; an unanswered one moves up behind the new one.
            if (IsCurrentAnswered)
                CurrentIndex++;
            _questions.Insert(CurrentIndex, question);
            ResetCurrent();
            return PlayNowOutcome.Moved;
        }

        void ResetCurrent()
        {
            AnsweredIndex = null;
            CurrentReview = null;
            IsCurrentRevealed = false;
        }
    }
}
