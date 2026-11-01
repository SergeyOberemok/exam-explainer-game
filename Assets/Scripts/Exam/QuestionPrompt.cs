using System;
using System.Collections.Generic;

namespace ExamCards.Exam
{
    /// <summary>The displayable text of a question; mirrors the <c>prompt</c> sub-document in MongoDB.</summary>
    [Serializable]
    public sealed class QuestionPrompt
    {
        public string Question { get; set; }
        public IReadOnlyList<string> Answers { get; set; } = Array.Empty<string>();

        /// <summary>Per-answer feedback, index-aligned with <see cref="Answers"/>.</summary>
        public IReadOnlyList<string> Feedbacks { get; set; } = Array.Empty<string>();

        public string Explanation { get; set; }
    }
}
