using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ExamCards.Exam
{
    public readonly struct AnswerResult
    {
        public AnswerResult(bool isCorrect, int correctIndex, int answeredIndex)
        {
            IsCorrect = isCorrect;
            CorrectIndex = correctIndex;
            AnsweredIndex = answeredIndex;
        }

        public bool IsCorrect { get; }
        public int CorrectIndex { get; }

        /// <summary>The answer that counted: the one picked, or an earlier one the round had already recorded.</summary>
        public int AnsweredIndex { get; }
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
    /// When the loaded questions run out, <see cref="LoadMoreAsync"/> continues the same round with questions it
    /// hasn't had, so nothing repeats, until the round has no more or the lives are gone.
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

        // Set once the round returned no new questions.
        bool _exhausted;

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

        /// <summary>True while the round may have questions this exam hasn't loaded yet.</summary>
        public bool HasMoreQuestions => !_exhausted && _round.HasMore;

        /// <summary>
        /// True when the current question is answered, it is the last one loaded, a life is left and the round
        /// may have more: call <see cref="LoadMoreAsync"/> before <see cref="MoveNext"/>.
        /// </summary>
        public bool NeedsMore =>
            !IsFinished && IsCurrentAnswered && Lives > 0 && CurrentIndex + 1 >= _questions.Count && HasMoreQuestions;

        /// <summary>True if the current question is the exam's last: no lives left, or nothing more to load.</summary>
        public bool IsLastQuestion => Lives <= 0 || (CurrentIndex + 1 >= _questions.Count && !HasMoreQuestions);

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

            // Normally the pick; after a lost response, the answer the round recorded first.
            var answered = review.AnsweredIndex ?? optionIndex;
            AnsweredIndex = answered;
            CurrentReview = review;
            var isCorrect = answered == review.CorrectIndex;
            if (isCorrect)
                Score++;
            else
                Lives--;
            return new AnswerResult(isCorrect, review.CorrectIndex, answered);
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
            FiftyFiftyResult result;
            IsBusy = true;
            try
            {
                result = await _round.EliminateAsync(question.Id, cancellationToken);
            }
            finally
            {
                IsBusy = false;
            }
            QuestionValidator.ValidateFiftyFifty(question, result);

            _eliminated[question.Id] = new HashSet<int>(result.Eliminated);
            // The round counts the charges; its number wins, so the two can't drift apart.
            FiftyFiftyRemaining = result.Remaining;
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

        /// <summary>Switches the current question back to its original text. Returns false if nothing changed.</summary>
        public bool ShowOriginal()
        {
            if (IsFinished || !IsCurrentRevealed)
                return false;
            IsCurrentRevealed = false;
            return true;
        }

        /// <summary>Toggles between the original and simplified text. Returns false if nothing changed.</summary>
        public bool ToggleSimplified() => IsCurrentRevealed ? ShowOriginal() : Reveal();

        /// <summary>Advances to the next question. Returns false when the exam is over.</summary>
        /// <exception cref="InvalidOperationException">Unanswered, busy, or <see cref="NeedsMore"/> (load first).</exception>
        public bool MoveNext()
        {
            if (!IsCurrentAnswered)
                throw new InvalidOperationException("The current question has not been answered.");
            if (IsBusy)
                throw new InvalidOperationException("A request is still running.");
            if (NeedsMore)
                throw new InvalidOperationException("The loaded questions are used up; call LoadMoreAsync first.");
            if (IsLastQuestion)
            {
                IsFinished = true;
                return false;
            }

            CurrentIndex++;
            ResetCurrent();
            return true;
        }

        /// <summary>
        /// Loads the round's next questions after the loaded ones. Returns how many were added; 0 means the round
        /// has no more, and <see cref="MoveNext"/> then finishes the exam. If the request fails, nothing changes
        /// and it can be called again (the round returns the same batch).
        /// </summary>
        /// <exception cref="InvalidOperationException">Busy, finished, or out of lives.</exception>
        /// <exception cref="FormatException">The round returned a question that doesn't fit.</exception>
        public async Task<int> LoadMoreAsync(CancellationToken cancellationToken = default)
        {
            if (IsBusy)
                throw new InvalidOperationException("A request is still running.");
            if (IsFinished || Lives <= 0)
                throw new InvalidOperationException("The exam is over.");
            if (_exhausted)
                return 0;

            IReadOnlyList<Question> fetched;
            IsBusy = true;
            try
            {
                fetched = await _round.FetchMoreAsync(cancellationToken);
            }
            finally
            {
                IsBusy = false;
            }

            var added = new List<Question>();
            foreach (var question in fetched ?? Array.Empty<Question>())
            {
                QuestionValidator.ValidateQuestion(question);
                // The round never repeats a question; skip one anyway rather than show it twice.
                if (IndexOf(question.Id) < 0 && added.TrueForAll(q => q.Id != question.Id))
                    added.Add(question);
            }
            _questions.AddRange(added);
            if (added.Count == 0)
                _exhausted = true;
            return added.Count;
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
        /// <param name="reveal">
        /// Show the question's simplified text right away, if it has one. Ignored for the current question
        /// (<see cref="PlayNowOutcome.AlreadyCurrent"/>), which keeps the text the player chose.
        /// </param>
        /// <exception cref="InvalidOperationException">Finished, out of lives, or busy (see <see cref="CanPlayNow"/>).</exception>
        /// <exception cref="FormatException">The round returned a question that doesn't fit.</exception>
        public async Task<PlayNowOutcome> PlayNowAsync(string questionId, bool reveal = false,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(questionId))
                throw new ArgumentException("No question id.", nameof(questionId));
            if (!CanPlayNow)
                throw new InvalidOperationException(IsBusy ? "A request is still running." : "The exam is over.");

            var index = IndexOf(questionId);
            // Already on screen: the player keeps whichever text they chose for it.
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
            IsCurrentRevealed = reveal && question.CanReveal;
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
