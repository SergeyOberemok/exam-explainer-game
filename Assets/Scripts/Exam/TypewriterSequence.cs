using System.Collections.Generic;

namespace ExamCards.Exam
{
    /// <summary>
    /// Types several texts one after another, like someone writing instructions line by line: each text types
    /// on its own <see cref="TypewriterTimeline"/>, with a short gap before the next one starts. Empty texts take
    /// no time. Plain C# so it can be unit-tested; the view only asks it for a position.
    /// </summary>
    public static class TypewriterSequence
    {
        /// <summary>Where a sequence is: the text typing (or just typed, during the gap after it) and how much of it shows.</summary>
        public readonly struct Position
        {
            public Position(int index, int length)
            {
                Index = index;
                Length = length;
            }

            /// <summary>The text being typed; the count of texts once all are typed.</summary>
            public int Index { get; }

            /// <summary>Visible characters of <see cref="Index"/>; texts before it show whole, texts after it not at all.</summary>
            public int Length { get; }
        }

        /// <summary>Seconds the whole sequence takes, gaps included.</summary>
        public static float TotalDuration(IReadOnlyList<string> texts, TypewriterOptions options, float gapSeconds)
        {
            var total = 0f;
            var typed = false;
            foreach (var text in texts ?? System.Array.Empty<string>())
            {
                if (string.IsNullOrEmpty(text))
                    continue;
                if (typed)
                    total += gapSeconds;
                total += TypewriterTimeline.TotalDuration(text, options);
                typed = true;
            }
            return total;
        }

        public static Position At(IReadOnlyList<string> texts, float elapsedSeconds, TypewriterOptions options, float gapSeconds)
        {
            var count = texts?.Count ?? 0;
            var clock = elapsedSeconds;
            for (var i = 0; i < count; i++)
            {
                var text = texts[i];
                if (string.IsNullOrEmpty(text))
                    continue;

                var duration = TypewriterTimeline.TotalDuration(text, options);
                if (clock < duration)
                    return new Position(i, TypewriterTimeline.VisibleLength(text, clock, options));
                clock -= duration;

                // The gap only comes before another text; during it this one shows whole.
                if (!HasTextAfter(texts, i))
                    break;
                if (clock < gapSeconds)
                    return new Position(i, text.Length);
                clock -= gapSeconds;
            }
            return new Position(count, 0);
        }

        static bool HasTextAfter(IReadOnlyList<string> texts, int index)
        {
            for (var i = index + 1; i < texts.Count; i++)
            {
                if (!string.IsNullOrEmpty(texts[i]))
                    return true;
            }
            return false;
        }
    }
}
