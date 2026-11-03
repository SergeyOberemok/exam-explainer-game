using System;

namespace ExamCards.Exam
{
    /// <summary>The outcome of answering a question: the correct answer and both versions' review.</summary>
    [Serializable]
    public sealed class AnswerReview
    {
        /// <summary>Index of the correct answer; the same for both versions of the text.</summary>
        public int CorrectIndex { get; set; }

        public PromptReview Original { get; set; }

        /// <summary>Review of the simplified text. Null when the question has no simplified version.</summary>
        public PromptReview Simplified { get; set; }
    }
}
