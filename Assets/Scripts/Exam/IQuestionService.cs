using System.Threading;
using System.Threading.Tasks;

namespace ExamCards.Exam
{
    /// <summary>Starts rounds of questions. The game uses the backend API; tests use <see cref="MockQuestionService"/>.</summary>
    public interface IQuestionService
    {
        Task<IExamRound> StartRoundAsync(CancellationToken cancellationToken = default);
    }
}
