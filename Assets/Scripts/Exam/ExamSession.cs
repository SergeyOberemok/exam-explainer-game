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
        bool _currentAnswered;

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
        public bool IsCurrentAnswered => _currentAnswered;

        public AnswerResult Answer(int optionIndex)
        {
            var question = Current;
            if (optionIndex < 0 || optionIndex >= question.Options.Count)
                throw new ArgumentOutOfRangeException(nameof(optionIndex));
            if (_currentAnswered)
                throw new InvalidOperationException("The current question has already been answered.");

            _currentAnswered = true;
            var isCorrect = optionIndex == question.CorrectIndex;
            if (isCorrect)
                Score++;
            return new AnswerResult(isCorrect, question.CorrectIndex);
        }

        /// <summary>Advances to the next question. Returns false when the exam is over.</summary>
        public bool MoveNext()
        {
            if (CurrentIndex + 1 >= _questions.Count)
                return false;
            CurrentIndex++;
            _currentAnswered = false;
            return true;
        }
    }
}
