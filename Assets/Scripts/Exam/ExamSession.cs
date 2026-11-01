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
        readonly IReadOnlyList<Question> _questions;

        public ExamSession(IReadOnlyList<Question> questions)
        {
            _questions = questions ?? throw new ArgumentNullException(nameof(questions));
            if (_questions.Count == 0)
                throw new ArgumentException("An exam needs at least one question.", nameof(questions));
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
            return true;
        }
    }
}
