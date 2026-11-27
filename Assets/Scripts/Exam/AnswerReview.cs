using System;

namespace ExamCards.Exam
{
    /// <summary>What answering a question reveals: the correct answer, the review, and every answer's outcome.</summary>
    [Serializable]
    public sealed class AnswerReview
    {
        /// <summary>
        /// The answer the round recorded, or null for "the one just sent". It differs from the one sent when an
        /// earlier answer reached the round but its response was lost: that earlier answer is the one that counts.
        /// </summary>
        public int? AnsweredIndex { get; set; }

        public int CorrectIndex { get; set; }

        /// <summary>Feedback per answer and the explanation.</summary>
        public PromptReview Review { get; set; }

        /// <summary>Requirements, outcomes per answer, the resolved flow and the rule; <see cref="Exam.Resolution.Empty"/> when the backend has none.</summary>
        public Resolution Resolution { get; set; } = Resolution.Empty;

        /// <summary>
        /// The chapter's story line after the answer that counts (its win or fail line). Empty when the backend
        /// has no story for the question.
        /// </summary>
        public string StoryLine { get; set; } = string.Empty;

        /// <returns>
        /// What answer <paramref name="index"/> suggests the player got wrong; empty for the correct answer and
        /// when the backend has nothing to say about it.
        /// </returns>
        public string MisconceptionFor(int index) =>
            index == CorrectIndex ? string.Empty : Resolution.ForAnswer(index)?.Misconception ?? string.Empty;

        /// <summary>A copy that records <paramref name="answeredIndex"/> as the answer, with its story line.</summary>
        public AnswerReview WithAnsweredIndex(int answeredIndex, string storyLine) => new AnswerReview
        {
            AnsweredIndex = answeredIndex,
            CorrectIndex = CorrectIndex,
            Review = Review,
            Resolution = Resolution,
            StoryLine = storyLine ?? string.Empty,
        };
    }
}
