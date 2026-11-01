using System;
using System.Collections.Generic;

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

    /// <summary>Walks through a list of questions one by one and keeps the score.</summary>
    public sealed class ExamSession
    {
        /// <summary>How many wrong answers a 50/50 removes.</summary>
        public const int EliminatedPerUse = 2;

        /// <summary>How many 50/50 uses an exam starts with.</summary>
        public const int DefaultFiftyFiftyCharges = 2;

        readonly IReadOnlyList<Question> _questions;
        readonly Random _random;
        readonly HashSet<int> _eliminated = new HashSet<int>();

        public ExamSession(IReadOnlyList<Question> questions, Random random = null,
            int fiftyFiftyCharges = DefaultFiftyFiftyCharges)
        {
            _questions = questions ?? throw new ArgumentNullException(nameof(questions));
            if (_questions.Count == 0)
                throw new ArgumentException("An exam needs at least one question.", nameof(questions));
            if (fiftyFiftyCharges < 0)
                throw new ArgumentOutOfRangeException(nameof(fiftyFiftyCharges));
            _random = random ?? new Random();
            FiftyFiftyRemaining = fiftyFiftyCharges;
        }

        /// <summary>How many 50/50 uses are left in this exam.</summary>
        public int FiftyFiftyRemaining { get; private set; }

        /// <summary>True if the current question has had its wrong answers removed.</summary>
        public bool IsCurrentEliminated => _eliminated.Count > 0;

        public bool IsEliminated(int optionIndex) => _eliminated.Contains(optionIndex);

        /// <summary>True if 50/50 can be used on the current question right now.</summary>
        public bool CanEliminate =>
            !IsFinished && !IsCurrentAnswered && !IsCurrentEliminated && FiftyFiftyRemaining > 0
            && Current.Original.Answers.Count - 1 >= EliminatedPerUse;

        /// <summary>
        /// Removes two random wrong answers from the current question and uses up one charge.
        /// Returns false if nothing changed. The correct answer is never removed.
        /// </summary>
        public bool Eliminate()
        {
            if (!CanEliminate)
                return false;

            var wrong = new List<int>();
            for (var i = 0; i < Current.Original.Answers.Count; i++)
            {
                if (i != Current.CorrectIndex)
                    wrong.Add(i);
            }

            for (var n = 0; n < EliminatedPerUse; n++)
            {
                var pick = _random.Next(wrong.Count);
                _eliminated.Add(wrong[pick]);
                wrong.RemoveAt(pick);
            }

            FiftyFiftyRemaining--;
            return true;
        }

        public int CurrentIndex { get; private set; }
        public int Total => _questions.Count;
        public int Score { get; private set; }
        public Question Current => _questions[CurrentIndex];

        /// <summary>The option picked for the current question, or null if it hasn't been answered.</summary>
        public int? AnsweredIndex { get; private set; }

        public bool IsCurrentAnswered => AnsweredIndex.HasValue;
        public bool IsCurrentRevealed { get; private set; }

        /// <summary>True once <see cref="MoveNext"/> has been called on the last question.</summary>
        public bool IsFinished { get; private set; }

        /// <summary>How many questions have been answered so far, including the current one.</summary>
        public int AnsweredCount => CurrentIndex + (IsCurrentAnswered ? 1 : 0);

        /// <summary>The text to display: the simplified version once revealed, otherwise the original.</summary>
        public QuestionPrompt CurrentPrompt => IsCurrentRevealed ? Current.Simplified : Current.Original;

        public AnswerResult Answer(int optionIndex)
        {
            var question = Current;
            if (optionIndex < 0 || optionIndex >= question.Original.Answers.Count)
                throw new ArgumentOutOfRangeException(nameof(optionIndex));
            if (IsCurrentAnswered)
                throw new InvalidOperationException("The current question has already been answered.");
            if (IsEliminated(optionIndex))
                throw new InvalidOperationException("That answer was eliminated by 50/50.");

            AnsweredIndex = optionIndex;
            var isCorrect = optionIndex == question.CorrectIndex;
            if (isCorrect)
                Score++;
            return new AnswerResult(isCorrect, question.CorrectIndex);
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
            if (CurrentIndex + 1 >= _questions.Count)
            {
                IsFinished = true;
                return false;
            }

            CurrentIndex++;
            AnsweredIndex = null;
            IsCurrentRevealed = false;
            _eliminated.Clear();
            return true;
        }
    }
}
