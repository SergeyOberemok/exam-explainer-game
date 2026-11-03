using System;

namespace ExamCards.Exam
{
    /// <summary>A question together with its answer key, for rounds played without a server.</summary>
    [Serializable]
    public sealed class KeyedQuestion
    {
        public Question Question { get; set; }
        public AnswerReview Review { get; set; }
    }
}
