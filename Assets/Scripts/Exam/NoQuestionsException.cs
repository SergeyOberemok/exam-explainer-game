using System;

namespace ExamCards.Exam
{
    /// <summary>A round can't start because no playable question matches.</summary>
    public sealed class NoQuestionsException : Exception
    {
        public NoQuestionsException(string message, Exception innerException = null)
            : base(message, innerException)
        {
        }
    }
}
