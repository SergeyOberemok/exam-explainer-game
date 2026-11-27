using System;
using System.Collections.Generic;
using System.Linq;

namespace ExamCards.Exam
{
    /// <summary>Which of a question's two maps to show.</summary>
    public enum FlowMapKind
    {
        /// <summary>The situation up to the question's decision, with the answers hanging off it.</summary>
        Question,

        /// <summary>After answering: the flow continued with the correct steps.</summary>
        Resolution,
    }

    public enum MapEdgeStyle
    {
        Normal,

        /// <summary>The backend worked it out rather than read it in the question.</summary>
        Inferred,

        /// <summary>A step of the correct mechanism the resolution added.</summary>
        Added,

        /// <summary>From the question's decision to one of its answers.</summary>
        Answer,
    }

    /// <summary>One box of a map: a node of the flow, or one of the answers hanging off the question's decision.</summary>
    public sealed class MapNode
    {
        public string Id { get; set; }
        public string Label { get; set; } = string.Empty;
        public FlowNodeType Type { get; set; }
        public bool IsQuestion { get; set; }
        public bool Added { get; set; }
        public bool Inferred { get; set; }

        /// <summary>The cast members the node shows. Before answering, only those the scene names.</summary>
        public IReadOnlyList<CastMember> Cast { get; set; } = Array.Empty<CastMember>();

        /// <summary>The answer's index for an answer box; null for a node of the flow.</summary>
        public int? AnswerIndex { get; set; }

        /// <summary>The icon key of an answer box or of a node of the flow; null when the backend sent none.</summary>
        public string Icon { get; set; }

        /// <summary>An answer removed by 50/50.</summary>
        public bool Eliminated { get; set; }

        public bool IsAnswer => AnswerIndex.HasValue;
    }

    public sealed class MapEdge
    {
        public string Source { get; set; }
        public string Target { get; set; }
        public string Label { get; set; } = string.Empty;
        public MapEdgeStyle Style { get; set; }
    }

    /// <summary>
    /// What the map modal shows for one question. It is built from the data the game already has:
    /// <see cref="Question.Flow"/> before answering and <see cref="Resolution.Flow"/> after. Before answering, it
    /// shows only cast members the scene names, so the map can't give the answer away even if the backend
    /// slips.
    /// </summary>
    public sealed class FlowMapModel
    {
        /// <summary>Prefix of the answer boxes' ids; the backend's node ids are plain words.</summary>
        public const string AnswerIdPrefix = "~answer-";

        public static FlowMapModel Empty(FlowMapKind kind) => new FlowMapModel { Kind = kind };

        public FlowMapKind Kind { get; private set; }

        /// <summary>A line under the title: what starts the flow, or the rule to remember.</summary>
        public string Caption { get; private set; } = string.Empty;

        public IReadOnlyList<MapNode> Nodes { get; private set; } = Array.Empty<MapNode>();
        public IReadOnlyList<MapEdge> Edges { get; private set; } = Array.Empty<MapEdge>();

        public bool IsEmpty => Nodes.Count == 0;

        /// <summary>The question's map: its flow, and an answer box per tool hanging off the question's decision.</summary>
        /// <param name="isEliminated">Whether 50/50 removed an answer; null when none is.</param>
        public static FlowMapModel ForQuestion(Question question, Func<int, bool> isEliminated = null)
        {
            var flow = question?.Flow;
            if (flow == null || flow.IsEmpty)
                return Empty(FlowMapKind.Question);

            var nodes = flow.Nodes.Select(node => ToMapNode(node, question, sceneOnly: true)).ToList();
            var edges = flow.Edges.Select(ToMapEdge).ToList();

            // Mermaid draws the answers off the decision (n6 -.-> opt0…); the flow itself doesn't list them.
            var decision = flow.Nodes.FirstOrDefault(node => node.IsQuestion);
            if (decision != null)
            {
                foreach (var tool in question.Tools)
                {
                    var id = AnswerIdPrefix + tool.Index;
                    nodes.Add(new MapNode
                    {
                        Id = id,
                        Label = string.IsNullOrWhiteSpace(tool.Label) ? tool.Text ?? string.Empty : tool.Label,
                        AnswerIndex = tool.Index,
                        Icon = question.IconForAnswer(tool.Index),
                        Eliminated = isEliminated?.Invoke(tool.Index) ?? false,
                    });
                    edges.Add(new MapEdge { Source = decision.Id, Target = id, Style = MapEdgeStyle.Answer });
                }
            }

            return new FlowMapModel
            {
                Kind = FlowMapKind.Question,
                Caption = flow.Trigger,
                Nodes = nodes,
                Edges = edges,
            };
        }

        /// <summary>
        /// The resolution's map. Its node ids aren't the question map's (the backend reuses an id for a different
        /// node), so the new steps are told apart by <see cref="FlowNode.Added"/> alone.
        /// </summary>
        public static FlowMapModel ForResolution(Question question, AnswerReview review)
        {
            var resolution = review?.Resolution;
            var flow = resolution?.Flow;
            if (question == null || flow == null || flow.IsEmpty)
                return Empty(FlowMapKind.Resolution);

            return new FlowMapModel
            {
                Kind = FlowMapKind.Resolution,
                Caption = string.IsNullOrEmpty(resolution.Rule) ? flow.Trigger : resolution.Rule,
                Nodes = flow.Nodes.Select(node => ToMapNode(node, question, sceneOnly: false)).ToList(),
                Edges = flow.Edges.Select(ToMapEdge).ToList(),
            };
        }

        static MapNode ToMapNode(FlowNode node, Question question, bool sceneOnly)
        {
            var cast = question.CastFor(node.EntityRefs);
            if (sceneOnly)
                cast = cast.Where(member => member.InScene).ToArray();
            return new MapNode
            {
                Id = node.Id,
                Label = node.Label ?? string.Empty,
                Type = node.Type,
                Icon = node.Icon,
                IsQuestion = node.IsQuestion,
                Added = node.Added,
                Inferred = node.Status == FlowStatus.Inferred,
                Cast = cast,
            };
        }

        static MapEdge ToMapEdge(FlowEdge edge) => new MapEdge
        {
            Source = edge.Source,
            Target = edge.Target,
            Label = EdgeLabel(edge),
            Style = edge.Added ? MapEdgeStyle.Added
                : edge.Status == FlowStatus.Inferred ? MapEdgeStyle.Inferred
                : MapEdgeStyle.Normal,
        };

        /// <summary>The edge's own label, or what a branch type says ("yes", "no"…) when it has none.</summary>
        static string EdgeLabel(FlowEdge edge)
        {
            if (!string.IsNullOrEmpty(edge.Label))
                return edge.Label;
            return edge.Type switch
            {
                FlowEdgeType.Yes => "yes",
                FlowEdgeType.No => "no",
                FlowEdgeType.Approved => "approved",
                FlowEdgeType.Rejected => "rejected",
                _ => string.Empty,
            };
        }

        /// <summary>Lays the map out with the sizes its boxes and edge labels take on screen.</summary>
        /// <param name="labelSize">The size of edge <c>i</c>'s label; without it a label counts as a dot.</param>
        public FlowLayoutResult Layout(Func<string, FlowSize> size, FlowLayoutOptions options = null,
            Func<int, FlowSize> labelSize = null) =>
            FlowLayout.Compute(
                Nodes.Select(node => node.Id).ToArray(),
                Edges.Select((edge, i) => new FlowLink(edge.Source, edge.Target, string.IsNullOrEmpty(edge.Label)
                    ? default
                    : labelSize?.Invoke(i) ?? new FlowSize(1f, 1f))).ToArray(),
                size,
                options);
    }
}
