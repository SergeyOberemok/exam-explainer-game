using System;

namespace ExamCards.Exam
{
    /// <summary>Speed settings for the typing effect.</summary>
    public readonly struct TypewriterOptions
    {
        public static readonly TypewriterOptions Default = new(90f, 3.5f, 0.12f);

        /// <summary>Typing speed for plain characters.</summary>
        public float CharsPerSecond { get; }
        /// <summary>The whole text never takes longer than this; a long text types faster instead.</summary>
        public float MaxDurationSeconds { get; }
        /// <summary>Extra pause after sentence punctuation and line breaks.</summary>
        public float PauseSeconds { get; }

        public TypewriterOptions(float charsPerSecond, float maxDurationSeconds, float pauseSeconds)
        {
            CharsPerSecond = charsPerSecond;
            MaxDurationSeconds = maxDurationSeconds;
            PauseSeconds = pauseSeconds;
        }
    }

    /// <summary>
    /// Works out how much of a text is visible a given time after it started typing, like a streamed chat message.
    /// Plain C# so it can be unit-tested; the view only asks it for a length.
    /// </summary>
    public static class TypewriterTimeline
    {
        /// <summary>Seconds the whole text takes, after the speed-up for a long text.</summary>
        public static float TotalDuration(string text, TypewriterOptions options)
        {
            if (string.IsNullOrEmpty(text) || options.CharsPerSecond <= 0f)
                return 0f;
            return Math.Min(RawDuration(text, options), options.MaxDurationSeconds);
        }

        /// <summary>How many characters (never half a surrogate pair) are visible after <paramref name="elapsedSeconds"/>.</summary>
        public static int VisibleLength(string text, float elapsedSeconds, TypewriterOptions options)
        {
            if (string.IsNullOrEmpty(text))
                return 0;
            if (options.CharsPerSecond <= 0f || elapsedSeconds >= TotalDuration(text, options))
                return text.Length;
            if (elapsedSeconds <= 0f)
                return 0;

            // Time on the unscaled timeline; a long text is squeezed into MaxDurationSeconds.
            var raw = RawDuration(text, options);
            var scaled = raw > options.MaxDurationSeconds ? raw / options.MaxDurationSeconds : 1f;
            var clock = elapsedSeconds * scaled;

            var perChar = 1f / options.CharsPerSecond;
            var time = 0f;
            var length = 0;
            while (length < text.Length)
            {
                time += perChar;
                if (time > clock)
                    break;
                length++;
                if (IsPause(text[length - 1]))
                {
                    time += options.PauseSeconds;
                    if (time > clock)
                        break;
                }
            }

            if (length > 0 && length < text.Length && char.IsHighSurrogate(text[length - 1]))
                length--;
            return length;
        }

        static float RawDuration(string text, TypewriterOptions options)
        {
            var pauses = 0;
            foreach (var c in text)
            {
                if (IsPause(c))
                    pauses++;
            }
            return text.Length / options.CharsPerSecond + pauses * options.PauseSeconds;
        }

        static bool IsPause(char c) => c is '.' or '?' or '!' or ':' or ',' or '\n';
    }
}
