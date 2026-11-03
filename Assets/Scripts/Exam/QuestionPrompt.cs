using System;
using System.Collections.Generic;

namespace ExamCards.Exam
{
    /// <summary>
    /// The text a player sees before answering; one version (original or simplified) of a question.
    /// Feedback and explanation arrive with the answer, in <see cref="PromptReview"/>.
    /// </summary>
    [Serializable]
    public sealed class QuestionPrompt
    {
        public string Question { get; set; }
        public IReadOnlyList<string> Answers { get; set; } = Array.Empty<string>();
    }
}
