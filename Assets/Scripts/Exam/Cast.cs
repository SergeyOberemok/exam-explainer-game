using System;
using System.Collections.Generic;

namespace ExamCards.Exam
{
    /// <summary>What a <see cref="CastMember"/> is, as the backend's decomposition types it.</summary>
    public enum CastType
    {
        /// <summary>A person, a role or an agent that does things.</summary>
        Actor,

        /// <summary>Software that runs: an app, a service, a pipeline, a model.</summary>
        System,

        /// <summary>Something processed, exchanged or accessed; also what an unknown type reads as.</summary>
        Object,

        /// <summary>Where knowledge comes from: docs, a codebase, a knowledge base.</summary>
        KnowledgeSource,

        /// <summary>Something made or kept: a file, a report, an id.</summary>
        Artifact,

        /// <summary>A third-party service the scenario calls.</summary>
        ExternalService,

        /// <summary>An idea or a mechanism rather than a thing: a flag, a state, a pattern.</summary>
        Concept,
    }

    /// <summary>
    /// One entity of a question: someone or something the scenario or an answer names, drawn as an icon. The
    /// briefing, the ask, the tools, the actions and the flow refer to it by <see cref="Ref"/>.
    /// </summary>
    [Serializable]
    public sealed class CastMember
    {
        public string Ref { get; set; } = string.Empty;
        public string Name { get; set; }
        public CastType Type { get; set; }

        /// <summary>Other wordings of the same thing in the text.</summary>
        public IReadOnlyList<string> Aliases { get; set; } = Array.Empty<string>();

        /// <summary>The icon key the backend picked; null when it hasn't (see <see cref="EntityIconResolver"/>).</summary>
        public string Icon { get; set; }

        /// <summary>
        /// True when the scenario names it. False when only an answer does: then it shows only on that answer's
        /// card, so the question doesn't give the answer away.
        /// </summary>
        public bool InScene { get; set; } = true;
    }

    /// <summary>One event of the scenario, in order: who (<see cref="ActorRef"/>) does what to what.</summary>
    [Serializable]
    public sealed class QuestionAction
    {
        public string Ref { get; set; } = string.Empty;
        public string Name { get; set; }
        public int Sequence { get; set; }

        /// <summary>The <see cref="CastMember.Ref"/> that does it; empty when none.</summary>
        public string ActorRef { get; set; } = string.Empty;

        /// <summary>The <see cref="CastMember.Ref"/> it is done to; empty when none.</summary>
        public string TargetRef { get; set; } = string.Empty;

        public IReadOnlyList<string> InputRefs { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> OutputRefs { get; set; } = Array.Empty<string>();

        /// <summary>The icon key the backend picked; null falls back to <see cref="EntityIconResolver.ActionIcon"/>.</summary>
        public string Icon { get; set; }
    }

    /// <summary>One answer as a tool the player can use: its text and the cast members it is made of.</summary>
    [Serializable]
    public sealed class AnswerTool
    {
        public int Index { get; set; }
        public string Text { get; set; } = string.Empty;

        /// <summary>What the answer proposes in a few words, without judging it; empty when the backend has none.</summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>Icon key for what the answer proposes; null when none was picked.</summary>
        public string Icon { get; set; }

        /// <summary>The <see cref="CastMember.Ref"/>s the answer is made of.</summary>
        public IReadOnlyList<string> ComponentRefs { get; set; } = Array.Empty<string>();
    }
}
