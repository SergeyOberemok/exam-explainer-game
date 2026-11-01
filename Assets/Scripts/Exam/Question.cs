using System;

namespace ExamCards.Exam
{
    /// <summary>A pick-one question: original text, optional simplified text, and one correct answer.</summary>
    [Serializable]
    public sealed class Question
    {
        /// <summary>The most answers a question can have; the screen has one letter and one key for each.</summary>
        public const int MaxAnswers = 4;

        public string Id { get; set; }
        public string Section { get; set; }
        public string Source { get; set; }
        public QuestionPrompt Original { get; set; }

        /// <summary>Text shown after Reveal. Null when the question has no simplified version.</summary>
        public QuestionPrompt Simplified { get; set; }

        /// <summary>Index of the correct answer; the same for both versions of the text.</summary>
        public int CorrectIndex { get; set; }

        public bool CanReveal => Simplified != null;
    }
}
