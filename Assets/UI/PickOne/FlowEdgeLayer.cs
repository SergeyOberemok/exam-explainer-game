using System;
using System.Collections.Generic;
using ExamCards.Exam;
using UnityEngine;
using UnityEngine.UIElements;

namespace ExamCards.UI
{
    /// <summary>
    /// Draws a map's edges with Painter2D, under its boxes: vertical curves through the points
    /// <see cref="FlowLayout"/> worked out, with an arrowhead at the target.
    /// <list type="bullet">
    /// <item>Added steps are thicker, in the success colour.</item>
    /// <item>Inferred edges are muted.</item>
    /// <item>Answer edges are dashed.</item>
    /// </list>
    /// The colours come from USS custom properties, so the palette stays in <c>PickOne.uss</c>.
    /// </summary>
    sealed class FlowEdgeLayer : VisualElement
    {
        const float LineWidth = 2f;
        const float AddedLineWidth = 3f;
        const float ArrowLength = 9f;
        const float ArrowHalfWidth = 5f;

        static readonly CustomStyleProperty<Color> EdgeColorProperty = new("--flow-edge-color");
        static readonly CustomStyleProperty<Color> AddedColorProperty = new("--flow-edge-added-color");
        static readonly CustomStyleProperty<Color> MutedColorProperty = new("--flow-edge-muted-color");

        readonly List<(IReadOnlyList<FlowPoint> Points, MapEdgeStyle Style)> _edges = new();
        Color _edgeColor = new(0.165f, 0.09f, 0.059f);
        Color _addedColor = new(0.196f, 0.396f, 0.337f);
        Color _mutedColor = new(0.345f, 0.255f, 0.239f, 0.6f);

        public FlowEdgeLayer()
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("flow-edges");
            generateVisualContent += OnGenerateVisualContent;
            RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolved);
        }

        public void SetEdges(IEnumerable<(IReadOnlyList<FlowPoint> Points, MapEdgeStyle Style)> edges)
        {
            _edges.Clear();
            if (edges != null)
                _edges.AddRange(edges);
            MarkDirtyRepaint();
        }

        void OnCustomStyleResolved(CustomStyleResolvedEvent evt)
        {
            if (evt.customStyle.TryGetValue(EdgeColorProperty, out var edge))
                _edgeColor = edge;
            if (evt.customStyle.TryGetValue(AddedColorProperty, out var added))
                _addedColor = added;
            if (evt.customStyle.TryGetValue(MutedColorProperty, out var muted))
                _mutedColor = muted;
            MarkDirtyRepaint();
        }

        void OnGenerateVisualContent(MeshGenerationContext context)
        {
            var painter = context.painter2D;
            painter.lineJoin = LineJoin.Round;
            painter.lineCap = LineCap.Butt;
            foreach (var (points, style) in _edges)
            {
                if (points == null || points.Count < 2)
                    continue;

                var color = style switch
                {
                    MapEdgeStyle.Added => _addedColor,
                    MapEdgeStyle.Inferred or MapEdgeStyle.Answer => _mutedColor,
                    _ => _edgeColor,
                };
                painter.strokeColor = color;
                painter.fillColor = color;
                painter.lineWidth = style == MapEdgeStyle.Added ? AddedLineWidth : LineWidth;
                // The pattern stays on the painter between paths, so every path sets its own.
                if (style == MapEdgeStyle.Answer)
                    painter.SetDashPattern(5f, 4f);
                else
                    painter.SetDashPattern(ReadOnlySpan<float>.Empty);

                // The last stretch stops short of the box, so the line doesn't poke out past the arrowhead.
                var end = ToVector(points[points.Count - 1]);
                var before = ToVector(points[points.Count - 2]);
                var direction = LastDirection(before, end);
                var shortEnd = end - direction * ArrowLength * 0.8f;

                painter.BeginPath();
                painter.MoveTo(ToVector(points[0]));
                for (var i = 1; i < points.Count; i++)
                {
                    var from = ToVector(points[i - 1]);
                    var to = i == points.Count - 1 ? shortEnd : ToVector(points[i]);
                    var dy = to.y - from.y;
                    if (dy > 1f && Mathf.Abs(to.x - from.x) > 0.5f)
                        painter.BezierCurveTo(new Vector2(from.x, from.y + dy / 2f), new Vector2(to.x, to.y - dy / 2f), to);
                    else
                        painter.LineTo(to);
                }
                painter.Stroke();

                painter.SetDashPattern(ReadOnlySpan<float>.Empty);
                var normal = new Vector2(-direction.y, direction.x);
                var baseCenter = end - direction * ArrowLength;
                painter.BeginPath();
                painter.MoveTo(end);
                painter.LineTo(baseCenter + normal * ArrowHalfWidth);
                painter.LineTo(baseCenter - normal * ArrowHalfWidth);
                painter.ClosePath();
                painter.Fill();
            }
        }

        /// <summary>
        /// Which way the edge arrives. A curve going down arrives vertically, whatever its start, because its
        /// last control point is straight above the end.
        /// </summary>
        static Vector2 LastDirection(Vector2 from, Vector2 to)
        {
            // Panel coordinates: y grows downwards.
            var down = new Vector2(0f, 1f);
            if (to.y - from.y > 1f)
                return down;
            var direction = to - from;
            return direction.sqrMagnitude < 0.01f ? down : direction.normalized;
        }

        static Vector2 ToVector(FlowPoint point) => new(point.X, point.Y);
    }
}
