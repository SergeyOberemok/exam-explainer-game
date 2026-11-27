using System;

namespace ExamCards.Exam
{
    /// <summary>A question together with its answer key, for rounds played without a server.</summary>
    [Serializable]
    public sealed class KeyedQuestion
    {
        public Question Question { get; set; }

        /// <summary>The key; its <see cref="AnswerReview.StoryLine"/> is replaced by <see cref="Win"/> or <see cref="Fail"/>.</summary>
        public AnswerReview Review { get; set; }

        /// <summary>The chapter's story line after a correct answer; empty without one.</summary>
        public string Win { get; set; } = string.Empty;

        /// <summary>The chapter's story line after a wrong answer; empty without one.</summary>
        public string Fail { get; set; } = string.Empty;

        /// <returns>The review for <paramref name="answerIndex"/>, as the backend's answer result would carry it.</returns>
        public AnswerReview ReviewFor(int answerIndex) =>
            Review.WithAnsweredIndex(answerIndex, answerIndex == Review.CorrectIndex ? Win : Fail);
    }
}
