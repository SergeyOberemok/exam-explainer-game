using System;
using System.Collections.Generic;

namespace ExamCards.Exam
{
    public enum FlowNodeType
    {
        Start,
        Process,
        Decision,
        Input,
        Output,
        HumanReview,
        End,
    }

    public enum FlowEdgeType
    {
        Next,
        Yes,
        No,
        Unknown,
        Approved,
        Rejected,
        Custom,
    }

    /// <summary>Whether the question states a node or edge (<see cref="Explicit"/>) or the backend worked it out.</summary>
    public enum FlowStatus
    {
        Explicit,
        Inferred,
        Unknown,
    }

    [Serializable]
    public sealed class FlowNode
    {
        public string Id { get; set; }
        public FlowNodeType Type { get; set; }
        public string Label { get; set; }

        /// <summary>The <see cref="CastMember.Ref"/>s of the cast members the node shows.</summary>
        public IReadOnlyList<string> EntityRefs { get; set; } = Array.Empty<string>();

        public string ConditionId { get; set; } = string.Empty;
        public FlowStatus Status { get; set; }

        /// <summary>The decision the question asks about; the answer cards hang on it. A pre-answer flow ends in one.</summary>
        public bool IsQuestion { get; set; }

        /// <summary>A step of the correct mechanism that a resolution added after the answer.</summary>
        public bool Added { get; set; }
    }

    [Serializable]
    public sealed class FlowEdge
    {
        public string Source { get; set; }
        public string Target { get; set; }
        public FlowEdgeType Type { get; set; }
        public string Label { get; set; } = string.Empty;
        public FlowStatus Status { get; set; }

        /// <summary>Added by a resolution, like <see cref="FlowNode.Added"/>.</summary>
        public bool Added { get; set; }
    }

    /// <summary>
    /// The situation of a question as a flow chart, from the backend's decomposition: the pre-answer flow
    /// ends in the question's decision node; a <see cref="Resolution"/> continues it with the correct steps.
    /// Nothing draws it yet; it is the data for the planned diagram.
    /// </summary>
    [Serializable]
    public sealed class DecisionFlow
    {
        /// <summary>No flow: the question hasn't been decomposed.</summary>
        public static DecisionFlow Empty => new DecisionFlow();

        /// <summary>How to draw it, e.g. "decision_flow", "process_flow", "cause_effect_diagram"; empty without a flow.</summary>
        public string Primary { get; set; } = string.Empty;

        public IReadOnlyList<string> Secondary { get; set; } = Array.Empty<string>();

        /// <summary>Why <see cref="Primary"/> fits the question.</summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>What starts the flow.</summary>
        public string Trigger { get; set; } = string.Empty;

        public IReadOnlyList<FlowNode> Nodes { get; set; } = Array.Empty<FlowNode>();

        /// <summary>Only between <see cref="Nodes"/>.</summary>
        public IReadOnlyList<FlowEdge> Edges { get; set; } = Array.Empty<FlowEdge>();

        public bool IsEmpty => Nodes.Count == 0;
    }
}
