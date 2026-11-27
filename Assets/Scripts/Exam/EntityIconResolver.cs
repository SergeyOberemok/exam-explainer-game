namespace ExamCards.Exam
{
    /// <summary>
    /// Icon keys. The backend picks an icon for every entity, question and answer from its icon catalog and
    /// sends only the key ("subagent", "config-file"); the game ships one picture per key as
    /// <c>Assets/Resources/EntityIcons/&lt;key&gt;.svg</c>. What a key looks like is decided there, so the
    /// icon set can change without touching the questions.
    /// </summary>
    public static class EntityIconResolver
    {
        /// <summary>The catalog's keys for "nothing more specific": a cast member's by its type, and an action's.</summary>
        public const string ActorIcon = "actor";
        public const string ObjectIcon = "object";
        public const string ActionIcon = "action";

        /// <summary>Shown where a question or an answer has no icon of its own.</summary>
        public const string DefaultIcon = ObjectIcon;

        /// <returns>The member's icon key; the key of its type when it has none.</returns>
        public static string Resolve(CastMember member)
        {
            if (member == null)
                return ObjectIcon;
            return CleanKey(member.Icon) ?? TypeIcon(member.Type);
        }

        /// <returns>The action's icon key; <see cref="ActionIcon"/> when it has none.</returns>
        public static string Resolve(QuestionAction action) => CleanKey(action?.Icon) ?? ActionIcon;

        /// <summary>
        /// The catalog has fallbacks for actors and objects only, so every type that isn't a person or an agent
        /// (systems, artifacts, concepts…) is drawn as an object until it has a key of its own.
        /// </summary>
        public static string TypeIcon(CastType type) => type == CastType.Actor ? ActorIcon : ObjectIcon;

        /// <returns>
        /// <paramref name="key"/> trimmed, or null when it isn't a key: lower-case letters and digits in words
        /// joined by single hyphens. A key is used as a resource name, so nothing else gets through.
        /// </returns>
        public static string CleanKey(string key)
        {
            key = key?.Trim();
            if (string.IsNullOrEmpty(key))
                return null;

            var previous = '-';
            foreach (var c in key)
            {
                var isWordChar = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9');
                if (!isWordChar && (c != '-' || previous == '-'))
                    return null;
                previous = c;
            }
            return previous == '-' ? null : key;
        }
    }
}
