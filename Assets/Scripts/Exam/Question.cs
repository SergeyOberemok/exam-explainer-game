using System;
using System.Collections.Generic;

namespace ExamCards.Exam
{
    /// <summary>
    /// A pick-one question as the player sees it: original text and optional simplified text.
    /// Which answer is correct is only known after answering it in an <see cref="IExamRound"/>.
    /// </summary>
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

        /// <summary>
        /// Keys of the learning objectives the question tests, the main one first. Empty when the backend
        /// hasn't tagged it. The backend's objective catalog says what a key means.
        /// </summary>
        public IReadOnlyList<string> Objectives { get; set; } = Array.Empty<string>();

        /// <summary>1 (easy) to 3 (hard); 0 when the backend hasn't tagged the question.</summary>
        public int Difficulty { get; set; }

        /// <summary>
        /// Key of the story chapter the question belongs to (the backend's scenario catalog); empty when it
        /// has none.
        /// </summary>
        public string Scenario { get; set; } = string.Empty;

        /// <summary>Key of the chapter's stage the question is in; empty until the backend has placed it.</summary>
        public string Stage { get; set; } = string.Empty;

        /// <summary>
        /// The original question without the scenario paragraph the source repeats in every question of a
        /// chapter. Empty when the question has no chapter; the original text is then all there is.
        /// </summary>
        public string Stem { get; set; } = string.Empty;

        /// <summary>One story line to show before the question; empty when the backend has none.</summary>
        public string Beat { get; set; } = string.Empty;

        /// <summary>
        /// What to show for the original text: the <see cref="Stem"/> when the question has a chapter (its intro
        /// stands for the scenario paragraph), otherwise the whole text.
        /// </summary>
        public string OriginalText => string.IsNullOrEmpty(Stem) ? Original?.Question : Stem;

        public bool CanReveal => Simplified != null;
    }
}
