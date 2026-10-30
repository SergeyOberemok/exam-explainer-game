using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ExamCards.Exam
{
    /// <summary>Source of exam questions. The real implementation will call the backend API.</summary>
    public interface IQuestionService
    {
        Task<IReadOnlyList<Question>> GetQuestionsAsync(CancellationToken cancellationToken = default);
    }
}
