namespace ExamCards.Exam
{
    /// <summary>Short chip texts for a question's tags.</summary>
    public static class QuestionLabels
    {
        /// <returns>"EASY", "MEDIUM" or "HARD" for <see cref="Question.Difficulty"/> 1–3; empty otherwise.</returns>
        public static string Difficulty(int difficulty) => difficulty switch
        {
            1 => "EASY",
            2 => "MEDIUM",
            3 => "HARD",
            _ => string.Empty,
        };

        /// <returns>
        /// An objective key as chip text: "agents-sessions" as "AGENTS · SESSIONS" (its area, then the rest).
        /// Empty for a null or blank key. The backend's catalog has real titles; the game doesn't fetch it yet.
        /// </returns>
        public static string Objective(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return string.Empty;
            var words = key.Trim().ToUpperInvariant();
            var split = words.IndexOf('-');
            return split < 0
                ? words
                : words.Substring(0, split) + " · " + words.Substring(split + 1).Replace('-', ' ');
        }
    }
}
