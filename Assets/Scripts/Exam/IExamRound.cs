using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ExamCards.Exam
{
    /// <summary>
    /// One round of questions, owned by whoever holds the answer key (the backend, or
    /// <see cref="LocalExamRound"/>). Each question can be answered once, and the answer reveals the review.
    /// A round can grow: <see cref="FetchMoreAsync"/> brings questions it doesn't have yet, and
    /// <see cref="AddQuestionAsync"/> adds a chosen one, so no question repeats within a round.
    /// </summary>
    public interface IExamRound
    {
        /// <summary>The questions the round started with, in play order. Later questions are not listed here.</summary>
        IReadOnlyList<Question> Questions { get; }

        /// <summary>How many 50/50 uses the round allows.</summary>
        int FiftyFiftyCharges { get; }

        /// <summary>
        /// True while the round may still have questions it hasn't handed out. A hint: <see cref="FetchMoreAsync"/>
        /// returning nothing is the final word.
        /// </summary>
        bool HasMore { get; }

        /// <summary>
        /// Answers a question. If the round already recorded another answer for it (its response was lost), that
        /// recorded answer counts: the review says which in <see cref="AnswerReview.AnsweredIndex"/>.
        /// </summary>
        Task<AnswerReview> SubmitAnswerAsync(string questionId, int answerIndex, CancellationToken cancellationToken = default);

        /// <summary>Removes wrong answers from an unanswered question and uses one 50/50 charge.</summary>
        Task<FiftyFiftyResult> EliminateAsync(string questionId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Adds a question (for example a search result) to the round so it can be answered here. Adding one the
        /// round already has changes nothing and returns it again, so a retry is safe.
        /// </summary>
        /// <returns>The question as the player sees it, without its key.</returns>
        Task<Question> AddQuestionAsync(string questionId, CancellationToken cancellationToken = default);

        /// <summary>
        /// The round's next questions, none of which it has had before (search adds included). Empty once every
        /// question is used. A retry after a failure returns the same batch.
        /// </summary>
        Task<IReadOnlyList<Question>> FetchMoreAsync(CancellationToken cancellationToken = default);
    }
}
