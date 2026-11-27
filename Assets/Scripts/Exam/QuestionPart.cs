using System;
using System.Collections.Generic;

namespace ExamCards.Exam
{
    /// <summary>What a <see cref="QuestionPart"/> says about the situation.</summary>
    public enum PartRole
    {
        /// <summary>Who and where: the setting of the scenario.</summary>
        Context,

        /// <summary>How things stand now.</summary>
        CurrentState,

        /// <summary>What goes wrong: a symptom the scenario reports.</summary>
        Problem,

        /// <summary>What the problem leads to.</summary>
        Consequence,

        /// <summary>What the people in the scenario want.</summary>
        Goal,

        /// <summary>A condition the scenario states, which an answer has to respect.</summary>
        Constraint,

        /// <summary>The question itself, word for word (<see cref="Question.Ask"/>).</summary>
        Ask,
    }

    /// <summary>
    /// One piece of an original question, as the backend's decomposition splits it: a briefing item (one of the
    /// scenario's facts) or the ask. Never names an entity only an answer names, so it gives nothing away.
    /// </summary>
    [Serializable]
    public sealed class QuestionPart
    {
        public PartRole Role { get; set; }
        public string Text { get; set; }

        /// <summary>The <see cref="CastMember.Ref"/>s of the cast members the part names.</summary>
        public IReadOnlyList<string> EntityRefs { get; set; } = Array.Empty<string>();
    }
}
