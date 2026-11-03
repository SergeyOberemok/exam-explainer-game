using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ExamCards.Exam
{
    /// <summary>
    /// One round of questions, owned by whoever holds the answer key (the backend, or
    /// <see cref="LocalExamRound"/>). Each question can be answered once, and the answer reveals the review.
    /// </summary>
    public interface IExamRound
    {
        /// <summary>The round's questions, in play order.</summary>
        IReadOnlyList<Question> Questions { get; }

        /// <summary>How many 50/50 uses the round allows.</summary>
        int FiftyFiftyCharges { get; }

        Task<AnswerReview> SubmitAnswerAsync(string questionId, int answerIndex, CancellationToken cancellationToken = default);

        /// <summary>Removes wrong answers from an unanswered question and uses one 50/50 charge.</summary>
        /// <returns>The indices of the removed answers.</returns>
        Task<IReadOnlyList<int>> EliminateAsync(string questionId, CancellationToken cancellationToken = default);
    }
}
