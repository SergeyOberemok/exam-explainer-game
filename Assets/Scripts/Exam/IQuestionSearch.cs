using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ExamCards.Exam
{
    /// <summary>
    /// Finds playable questions by text. Results carry no answer key; to play one, add it to the round with
    /// <see cref="IExamRound.AddQuestionAsync"/> (see <see cref="ExamSession.PlayNowAsync"/>).
    /// </summary>
    public interface IQuestionSearch
    {
        /// <summary>Shortest text worth searching for.</summary>
        public const int MinTextLength = 2;

        /// <summary>Longest text the backend accepts.</summary>
        public const int MaxTextLength = 200;

        /// <summary>Best matches first. Results have no <see cref="Question.Simplified"/> text.</summary>
        /// <exception cref="System.ArgumentException">The text is shorter than <see cref="MinTextLength"/> once trimmed.</exception>
        Task<IReadOnlyList<Question>> SearchAsync(string text, CancellationToken cancellationToken = default);
    }

    public static class QuestionSearchText
    {
        /// <summary>The text trimmed and cut to <see cref="IQuestionSearch.MaxTextLength"/>, or null if too short.</summary>
        public static string Normalize(string text)
        {
            text = text?.Trim() ?? string.Empty;
            if (text.Length < IQuestionSearch.MinTextLength)
                return null;
            return text.Length <= IQuestionSearch.MaxTextLength ? text : text.Substring(0, IQuestionSearch.MaxTextLength).TrimEnd();
        }
    }
}
