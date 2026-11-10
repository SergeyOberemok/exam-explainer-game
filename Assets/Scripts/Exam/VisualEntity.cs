using System;
using System.Collections.Generic;

namespace ExamCards.Exam
{
    /// <summary>What a <see cref="VisualEntity"/> is in the scenario.</summary>
    public enum EntityKind
    {
        /// <summary>Performs or takes part in something: a person, a role, an agent, a model.</summary>
        Actor,

        /// <summary>Is processed, exchanged or accessed: software, a tool, a file, a setting, a business object.</summary>
        Object,

        /// <summary>An operation that can be one workflow step.</summary>
        Action,
    }

    /// <summary>
    /// A thing a question mentions that can be drawn as an icon. The backend extracts it from the
    /// question's texts; the same entity is repeated wherever it is mentioned.
    /// </summary>
    [Serializable]
    public sealed class VisualEntity
    {
        public string Name { get; set; }
        public EntityKind Kind { get; set; }

        /// <summary>Other wordings of the same thing in the text (never the name itself).</summary>
        public IReadOnlyList<string> Aliases { get; set; } = Array.Empty<string>();

        /// <summary>The icon key the backend picked; null when it hasn't (see <see cref="EntityIconResolver"/>).</summary>
        public string Icon { get; set; }
    }

    /// <summary>
    /// The visual entities of one <see cref="QuestionPrompt"/>: the question's, and each answer's. It also
    /// carries one icon key for the question and one per answer, for what each is about as a whole.
    /// </summary>
    [Serializable]
    public sealed class PromptEntities
    {
        /// <summary>No entities: the original text, and questions extracted before entities existed.</summary>
        public static PromptEntities Empty => new PromptEntities();

        public IReadOnlyList<VisualEntity> Question { get; set; } = Array.Empty<VisualEntity>();

        /// <summary>One list per answer, index-aligned with <see cref="QuestionPrompt.Answers"/>; empty when there are none.</summary>
        public IReadOnlyList<IReadOnlyList<VisualEntity>> Answers { get; set; } = Array.Empty<IReadOnlyList<VisualEntity>>();

        /// <summary>Icon key for the question as a whole; null when the backend picked none.</summary>
        public string QuestionIcon { get; set; }

        /// <summary>One icon key per answer (null where none was picked), index-aligned like <see cref="Answers"/>; may be empty.</summary>
        public IReadOnlyList<string> AnswerIcons { get; set; } = Array.Empty<string>();

        public bool IsEmpty => Question.Count == 0 && Answers.Count == 0 && QuestionIcon == null && AnswerIcons.Count == 0;

        /// <returns>The entities of answer <paramref name="index"/>; empty when that answer has none.</returns>
        public IReadOnlyList<VisualEntity> ForAnswer(int index) =>
            index >= 0 && index < Answers.Count ? Answers[index] : Array.Empty<VisualEntity>();

        /// <returns>The icon key of answer <paramref name="index"/>; null when it has none.</returns>
        public string IconForAnswer(int index) =>
            index >= 0 && index < AnswerIcons.Count ? AnswerIcons[index] : null;
    }
}
