using System.Collections.Generic;
using System.Text.RegularExpressions;
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

        /// <summary>Longest text the backend accepts: enough for a whole question with its scenario paragraph.</summary>
        public const int MaxTextLength = 2000;

        /// <summary>Best matches first. Results have no <see cref="Question.Simplified"/> text.</summary>
        /// <exception cref="System.ArgumentException">The text is shorter than <see cref="MinTextLength"/> once trimmed.</exception>
        Task<IReadOnlyList<Question>> SearchAsync(string text, CancellationToken cancellationToken = default);
    }

    public static class QuestionSearchText
    {
        static readonly Regex Whitespace = new Regex(@"\s+");

        /// <summary>
        /// The text with runs of whitespace (including line breaks from pasted text) as single spaces, trimmed and
        /// cut to <see cref="IQuestionSearch.MaxTextLength"/>, or null if too short.
        /// </summary>
        public static string Normalize(string text)
        {
            text = Whitespace.Replace(text ?? string.Empty, " ").Trim();
            if (text.Length < IQuestionSearch.MinTextLength)
                return null;
            return text.Length <= IQuestionSearch.MaxTextLength ? text : text.Substring(0, IQuestionSearch.MaxTextLength).TrimEnd();
        }
    }
}
