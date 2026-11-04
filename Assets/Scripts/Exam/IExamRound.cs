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
        /// <summary>The questions the round started with, in play order. Added questions are not listed here.</summary>
        IReadOnlyList<Question> Questions { get; }

        /// <summary>How many 50/50 uses the round allows.</summary>
        int FiftyFiftyCharges { get; }

        Task<AnswerReview> SubmitAnswerAsync(string questionId, int answerIndex, CancellationToken cancellationToken = default);

        /// <summary>Removes wrong answers from an unanswered question and uses one 50/50 charge.</summary>
        /// <returns>The indices of the removed answers.</returns>
        Task<IReadOnlyList<int>> EliminateAsync(string questionId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Adds a question (for example a search result) to the round so it can be answered here. Adding one the
        /// round already has changes nothing and returns it again, so a retry is safe.
        /// </summary>
        /// <returns>The question as the player sees it, with its simplified version if it has one.</returns>
        Task<Question> AddQuestionAsync(string questionId, CancellationToken cancellationToken = default);
    }
}
