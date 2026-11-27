using System;
using System.Collections.Generic;

namespace ExamCards.Exam
{
    /// <summary>The source's feedback and explanation, shown once a question is answered.</summary>
    [Serializable]
    public sealed class PromptReview
    {
        public string Explanation { get; set; }

        /// <summary>Per-answer feedback, index-aligned with <see cref="Question.Answers"/>.</summary>
        public IReadOnlyList<string> Feedbacks { get; set; } = Array.Empty<string>();
    }
}
