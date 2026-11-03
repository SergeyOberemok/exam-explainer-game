using System;
using System.Collections.Generic;

namespace ExamCards.Exam
{
    /// <summary>What one version of a question shows once it is answered.</summary>
    [Serializable]
    public sealed class PromptReview
    {
        public string Explanation { get; set; }

        /// <summary>Per-answer feedback, index-aligned with <see cref="QuestionPrompt.Answers"/>.</summary>
        public IReadOnlyList<string> Feedbacks { get; set; } = Array.Empty<string>();
    }
}
