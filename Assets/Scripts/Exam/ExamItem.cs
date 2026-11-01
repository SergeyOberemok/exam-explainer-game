using System;
using System.Collections.Generic;

namespace ExamCards.Exam
{
    /// <summary>One raw document from the <c>examination</c> or <c>examination_simplified</c> collection.</summary>
    [Serializable]
    public sealed class ExamItem
    {
        public string Id { get; set; }

        /// <summary>On a simplified item, the id of the original question. Null on an original.</summary>
        public string QuestionId { get; set; }

        public string Section { get; set; }
        public string Source { get; set; }
        public QuestionPrompt Prompt { get; set; }

        /// <summary>Correct answer letters, e.g. <c>["c"]</c>.</summary>
        public IReadOnlyList<string> CorrectResponse { get; set; } = Array.Empty<string>();
    }
}
