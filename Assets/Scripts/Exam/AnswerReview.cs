using System;

namespace ExamCards.Exam
{
    /// <summary>The outcome of answering a question: the correct answer and both versions' review.</summary>
    [Serializable]
    public sealed class AnswerReview
    {
        /// <summary>
        /// The answer the round recorded, or null for "the one just sent". It differs from the one sent when an
        /// earlier answer reached the round but its response was lost: that earlier answer is the one that counts.
        /// </summary>
        public int? AnsweredIndex { get; set; }

        /// <summary>Index of the correct answer; the same for both versions of the text.</summary>
        public int CorrectIndex { get; set; }

        public PromptReview Original { get; set; }

        /// <summary>Review of the simplified text. Null when the question has no simplified version.</summary>
        public PromptReview Simplified { get; set; }

        /// <summary>
        /// What the answer that counts suggests the player got wrong, in one sentence. Empty for the correct
        /// answer and when the backend has nothing to say about that answer.
        /// </summary>
        public string Misconception { get; set; } = string.Empty;

        /// <summary>
        /// One story line on what the answer that counts leads to in the question's chapter. Empty when the
        /// backend has no story for the question.
        /// </summary>
        public string Outcome { get; set; } = string.Empty;

        /// <summary>A copy that records <paramref name="answeredIndex"/> as the answer.</summary>
        public AnswerReview WithAnsweredIndex(int answeredIndex) => new AnswerReview
        {
            AnsweredIndex = answeredIndex,
            CorrectIndex = CorrectIndex,
            Original = Original,
            Simplified = Simplified,
            Misconception = Misconception,
            Outcome = Outcome,
        };
    }
}
