using System;
using System.Collections.Generic;

namespace ExamCards.Exam
{
    /// <summary>One selectable answer card of a question.</summary>
    [Serializable]
    public sealed class AnswerOption
    {
        public string Title { get; set; }

        /// <summary>Secondary caption next to the title (e.g. the Latin name).</summary>
        public string Subtitle { get; set; }

        public string Description { get; set; }
        public string PrimaryTag { get; set; }
        public string SecondaryTag { get; set; }
        public IReadOnlyList<string> Traits { get; set; } = Array.Empty<string>();

        /// <summary>Image key (mock) or URL (real API).</summary>
        public string Image { get; set; }

        public string Tier { get; set; }
    }
}
