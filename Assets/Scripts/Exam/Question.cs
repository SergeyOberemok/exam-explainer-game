using System;
using System.Collections.Generic;

namespace ExamCards.Exam
{
    /// <summary>A pick-one question: several options, exactly one of them correct.</summary>
    [Serializable]
    public sealed class Question
    {
        public string Id { get; set; }
        public string Category { get; set; }
        public int XpReward { get; set; }
        public string ChallengeLabel { get; set; }
        public string Prompt { get; set; }
        public string Hint { get; set; }

        /// <summary>Image key (mock) or URL (real API) shown next to the prompt.</summary>
        public string ClueImage { get; set; }

        public string ClueText { get; set; }
        public IReadOnlyList<AnswerOption> Options { get; set; } = Array.Empty<AnswerOption>();
        public int CorrectIndex { get; set; }
    }
}
