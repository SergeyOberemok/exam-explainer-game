using System;
using System.Collections.Generic;
using System.Linq;

namespace ExamCards.Exam
{
    /// <summary>A point of a laid-out map, in pixels from its top-left corner.</summary>
    public readonly struct FlowPoint
    {
        public FlowPoint(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float X { get; }
        public float Y { get; }

        public override string ToString() => $"({X}, {Y})";
    }

    public readonly struct FlowSize
    {
        public FlowSize(float width, float height)
        {
            Width = width;
            Height = height;
        }

        public float Width { get; }
        public float Height { get; }
    }

    /// <summary>Where a node of a laid-out map goes.</summary>
    public readonly struct FlowRect
    {
        public FlowRect(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public float X { get; }
        public float Y { get; }
        public float Width { get; }
        public float Height { get; }
        public float Right => X + Width;
        public float Bottom => Y + Height;
        public float CenterX => X + Width / 2f;

        public bool Overlaps(FlowRect other) =>
            X < other.Right && other.X < Right && Y < other.Bottom && other.Y < Bottom;
    }

    /// <summary>A link from one node to another, by id.</summary>
    public readonly struct FlowLink
    {
        /// <param name="labelSize">The size its label takes on screen; zero for a link without one.</param>
        public FlowLink(string source, string target, FlowSize labelSize = default)
        {
            Source = source;
            Target = target;
            LabelSize = labelSize;
        }

        public string Source { get; }
        public string Target { get; }
        public FlowSize LabelSize { get; }
        public bool HasLabel => LabelSize.Width > 0f && LabelSize.Height > 0f;
    }

    public sealed class FlowLayoutOptions
    {
        /// <summary>Vertical room between two ranks; edges and their labels run through it.</summary>
        public float RankGap { get; set; } = 56f;

        /// <summary>Horizontal room between two neighbours in a rank.</summary>
        public float NodeGap { get; set; } = 28f;

        /// <summary>The width an edge passing through a rank takes there.</summary>
        public float LaneWidth { get; set; } = 16f;

        /// <summary>Room around the whole map.</summary>
        public float Padding { get; set; } = 16f;
    }

    public sealed class FlowLayoutResult
    {
        public static FlowLayoutResult Empty => new FlowLayoutResult(
            new Dictionary<string, FlowRect>(), Array.Empty<IReadOnlyList<FlowPoint>>(), Array.Empty<FlowPoint?>(), 0f, 0f);

        public FlowLayoutResult(IReadOnlyDictionary<string, FlowRect> nodes, IReadOnlyList<IReadOnlyList<FlowPoint>> edges,
            IReadOnlyList<FlowPoint?> labels, float width, float height)
        {
            Nodes = nodes;
            Edges = edges;
            Labels = labels;
            Width = width;
            Height = height;
        }

        /// <summary>Every node by id.</summary>
        public IReadOnlyDictionary<string, FlowRect> Nodes { get; }

        /// <summary>
        /// One path per link, index-aligned with the links given: from the bottom centre of its source, through the
        /// lanes of the ranks it passes, to the top centre of its target. A link that closes a cycle goes up
        /// instead: from the top of its source, round the right of every node between, into the bottom of its
        /// target. Empty for a link that can't be drawn (an unknown node, or a node linked to itself).
        /// </summary>
        public IReadOnlyList<IReadOnlyList<FlowPoint>> Edges { get; }

        /// <summary>
        /// Where the centre of each link's label goes, index-aligned with the links; null for a link without a
        /// label or a path. It sits where no node is: in the gap below the source, or in the column of a link
        /// that goes up, which is made wide enough for it.
        /// </summary>
        public IReadOnlyList<FlowPoint?> Labels { get; }

        public float Width { get; }
        public float Height { get; }
    }

    /// <summary>
    /// Lays out a small flow chart top to bottom in ranks, like mermaid's <c>flowchart TD</c>.
    /// <list type="number">
    /// <item>Each node's rank is its longest path from a root. Links that close a cycle are left out of the
    /// ranking.</item>
    /// <item>A link that skips ranks gets a lane in each rank it passes, so it doesn't run through the nodes
    /// there.</item>
    /// <item>The order in a rank follows the parents (barycenter sweeps).</item>
    /// <item>Each node is placed under the mean of its parents, and a root over the mean of its children,
    /// pushed right as far as needed to keep its neighbours apart.</item>
    /// <item>A link that closes a cycle goes up through the rank gaps and a column right of the nodes it
    /// passes, so it doesn't cross them.</item>
    /// </list>
    /// The maps are a handful of nodes, so nothing here is tuned for size.
    /// </summary>
    public static class FlowLayout
    {
        const int Sweeps = 4;

        /// <summary>How far into a rank gap a link going up turns, from the node it leaves or enters.</summary>
        const float BackEdgeGapShare = 0.35f;

        /// <summary>A real node or a lane that a long link takes through a rank.</summary>
        sealed class Slot
        {
            public string Id;
            public bool IsLane;
            public int Rank;
            public float Width;
            public float Height;
            public int Order;
            public float X;
            public readonly List<Slot> Up = new();
            public readonly List<Slot> Down = new();
            public float CenterX => X + Width / 2f;
        }

        /// <param name="nodes">The node ids, in the source's order (it breaks ties). Duplicates count once.</param>
        /// <param name="size">The size each node takes on screen.</param>
        public static FlowLayoutResult Compute(IReadOnlyList<string> nodes, IReadOnlyList<FlowLink> links,
            Func<string, FlowSize> size, FlowLayoutOptions options = null)
        {
            options ??= new FlowLayoutOptions();
            if (nodes == null || nodes.Count == 0)
                return FlowLayoutResult.Empty;
            links ??= Array.Empty<FlowLink>();

            var slots = new Dictionary<string, Slot>();
            var ordered = new List<Slot>();
            foreach (var id in nodes)
            {
                if (id == null || slots.ContainsKey(id))
                    continue;
                var measured = size(id);
                var slot = new Slot { Id = id, Width = Math.Max(1f, measured.Width), Height = Math.Max(1f, measured.Height) };
                slots[id] = slot;
                ordered.Add(slot);
            }
            if (ordered.Count == 0)
                return FlowLayoutResult.Empty;

            var drawable = new bool[links.Count];
            for (var i = 0; i < links.Count; i++)
            {
                var link = links[i];
                drawable[i] = link.Source != null && link.Target != null && link.Source != link.Target
                    && slots.ContainsKey(link.Source) && slots.ContainsKey(link.Target);
            }

            var forward = FindForwardLinks(ordered, links, drawable);
            AssignRanks(ordered, links, forward, slots);

            // Lanes: a forward link from rank r to rank r + k gets one in each of the k - 1 ranks between.
            var lanes = new List<Slot>[links.Count];
            var all = new List<Slot>(ordered);
            for (var i = 0; i < links.Count; i++)
            {
                if (!forward[i])
                    continue;
                var source = slots[links[i].Source];
                var target = slots[links[i].Target];
                var previous = source;
                lanes[i] = new List<Slot>();
                for (var rank = source.Rank + 1; rank < target.Rank; rank++)
                {
                    var lane = new Slot { Id = null, IsLane = true, Rank = rank, Width = options.LaneWidth, Height = 0f };
                    Connect(previous, lane);
                    lanes[i].Add(lane);
                    all.Add(lane);
                    previous = lane;
                }
                Connect(previous, target);
            }

            var rankCount = all.Max(slot => slot.Rank) + 1;
            var ranks = new List<Slot>[rankCount];
            for (var r = 0; r < rankCount; r++)
                ranks[r] = new List<Slot>();
            foreach (var slot in all)
                ranks[slot.Rank].Add(slot);
            Renumber(ranks);

            OrderRanks(ranks);
            PlaceHorizontally(ranks, options.NodeGap);

            // Rank tops, from the tallest node of each rank.
            var rankTop = new float[rankCount];
            var rankHeight = new float[rankCount];
            var y = 0f;
            for (var r = 0; r < rankCount; r++)
            {
                rankTop[r] = y;
                rankHeight[r] = ranks[r].Where(slot => !slot.IsLane).Select(slot => slot.Height).DefaultIfEmpty(0f).Max();
                y += rankHeight[r] + options.RankGap;
            }
            var contentHeight = y - options.RankGap;

            var minX = all.Min(slot => slot.X);
            var maxX = all.Max(slot => slot.X + slot.Width);
            var shift = options.Padding - minX;

            var rects = new Dictionary<string, FlowRect>();
            foreach (var slot in ordered)
            {
                var top = rankTop[slot.Rank] + (rankHeight[slot.Rank] - slot.Height) / 2f;
                rects[slot.Id] = new FlowRect(slot.X + shift, top + options.Padding, slot.Width, slot.Height);
            }

            var edges = new IReadOnlyList<FlowPoint>[links.Count];
            var labels = new FlowPoint?[links.Count];
            var contentRight = maxX + shift;
            for (var i = 0; i < links.Count; i++)
            {
                if (!drawable[i])
                {
                    edges[i] = Array.Empty<FlowPoint>();
                    continue;
                }
                var source = rects[links[i].Source];
                var target = rects[links[i].Target];
                List<FlowPoint> points;
                FlowPoint label;
                if (forward[i])
                {
                    points = new List<FlowPoint> { new FlowPoint(source.CenterX, source.Bottom) };
                    if (lanes[i] != null)
                    {
                        // Straight down through each rank it passes, in its own lane.
                        foreach (var lane in lanes[i])
                        {
                            var x = lane.CenterX + shift;
                            points.Add(new FlowPoint(x, rankTop[lane.Rank] + options.Padding));
                            points.Add(new FlowPoint(x, rankTop[lane.Rank] + rankHeight[lane.Rank] + options.Padding));
                        }
                    }
                    points.Add(new FlowPoint(target.CenterX, target.Y));
                    // In the gap below the source, not in a lane beside the nodes of a rank it passes.
                    label = Midpoint(points[0], points[1]);
                }
                else
                {
                    // Closes a cycle, so its target is ranks above: up into the gap above the source, right to a
                    // column of its own past every slot between, up, and back left into the gap below the target.
                    var low = slots[links[i].Target].Rank;
                    var high = slots[links[i].Source].Rank;
                    var beside = all.Where(slot => slot.Rank >= low && slot.Rank <= high).Max(slot => slot.X + slot.Width) + shift;
                    var columnWidth = options.LaneWidth + (links[i].HasLabel ? links[i].LabelSize.Width : 0f);
                    var columnLeft = Math.Max(beside, contentRight) + options.NodeGap / 2f;
                    var columnX = columnLeft + columnWidth / 2f;
                    contentRight = columnLeft + columnWidth;

                    var exitY = rankTop[high] + options.Padding - options.RankGap * BackEdgeGapShare;
                    var entryY = rankTop[low] + rankHeight[low] + options.Padding + options.RankGap * BackEdgeGapShare;
                    points = new List<FlowPoint>
                    {
                        new FlowPoint(source.CenterX, source.Y),
                        new FlowPoint(source.CenterX, exitY),
                        new FlowPoint(columnX, exitY),
                        new FlowPoint(columnX, entryY),
                        new FlowPoint(target.CenterX, entryY),
                        new FlowPoint(target.CenterX, target.Bottom),
                    };
                    label = new FlowPoint(columnX, (exitY + entryY) / 2f);
                }
                edges[i] = points;
                if (links[i].HasLabel)
                    labels[i] = label;
            }

            return new FlowLayoutResult(rects, edges, labels, contentRight + options.Padding, contentHeight + 2f * options.Padding);
        }

        static FlowPoint Midpoint(FlowPoint a, FlowPoint b) => new((a.X + b.X) / 2f, (a.Y + b.Y) / 2f);

        static void Connect(Slot upper, Slot lower)
        {
            upper.Down.Add(lower);
            lower.Up.Add(upper);
        }

        /// <returns>For each link, whether it ranks its target below its source: drawable and not closing a cycle.</returns>
        static bool[] FindForwardLinks(List<Slot> ordered, IReadOnlyList<FlowLink> links, bool[] drawable)
        {
            var outgoing = ordered.ToDictionary(slot => slot.Id, _ => new List<int>());
            for (var i = 0; i < links.Count; i++)
            {
                if (drawable[i])
                    outgoing[links[i].Source].Add(i);
            }

            // Iterative DFS from the nodes in order; a link to a node still on the stack closes a cycle.
            var forward = new bool[links.Count];
            var state = ordered.ToDictionary(slot => slot.Id, _ => 0); // 0 new, 1 on the stack, 2 done
            foreach (var root in ordered)
            {
                if (state[root.Id] != 0)
                    continue;
                var stack = new Stack<(string Id, int Next)>();
                stack.Push((root.Id, 0));
                state[root.Id] = 1;
                while (stack.Count > 0)
                {
                    var (id, next) = stack.Pop();
                    var outs = outgoing[id];
                    if (next >= outs.Count)
                    {
                        state[id] = 2;
                        continue;
                    }
                    stack.Push((id, next + 1));
                    var link = outs[next];
                    var target = links[link].Target;
                    if (state[target] == 1)
                        continue; // Closes a cycle: drawn, but not ranked.
                    forward[link] = true;
                    if (state[target] == 0)
                    {
                        state[target] = 1;
                        stack.Push((target, 0));
                    }
                }
            }
            return forward;
        }

        /// <summary>Longest path from a root over the forward links, in topological order.</summary>
        static void AssignRanks(List<Slot> ordered, IReadOnlyList<FlowLink> links, bool[] forward, Dictionary<string, Slot> slots)
        {
            var incoming = ordered.ToDictionary(slot => slot.Id, _ => 0);
            var outgoing = ordered.ToDictionary(slot => slot.Id, _ => new List<string>());
            for (var i = 0; i < links.Count; i++)
            {
                if (!forward[i])
                    continue;
                incoming[links[i].Target]++;
                outgoing[links[i].Source].Add(links[i].Target);
            }

            var ready = new Queue<string>(ordered.Where(slot => incoming[slot.Id] == 0).Select(slot => slot.Id));
            while (ready.Count > 0)
            {
                var id = ready.Dequeue();
                foreach (var target in outgoing[id])
                {
                    slots[target].Rank = Math.Max(slots[target].Rank, slots[id].Rank + 1);
                    if (--incoming[target] == 0)
                        ready.Enqueue(target);
                }
            }
        }

        static void Renumber(List<Slot>[] ranks)
        {
            foreach (var rank in ranks)
            {
                for (var i = 0; i < rank.Count; i++)
                    rank[i].Order = i;
            }
        }

        /// <summary>Sorts each rank by the mean position of its neighbours, downwards then upwards.</summary>
        static void OrderRanks(List<Slot>[] ranks)
        {
            for (var sweep = 0; sweep < Sweeps; sweep++)
            {
                var down = sweep % 2 == 0;
                if (down)
                {
                    for (var r = 1; r < ranks.Length; r++)
                        SortByBarycenter(ranks[r], slot => slot.Up);
                }
                else
                {
                    for (var r = ranks.Length - 2; r >= 0; r--)
                        SortByBarycenter(ranks[r], slot => slot.Down);
                }
            }
        }

        static void SortByBarycenter(List<Slot> rank, Func<Slot, List<Slot>> neighbours)
        {
            // A slot without neighbours on that side keeps its place.
            var keyed = rank
                .Select(slot => (Slot: slot, Key: neighbours(slot).Count == 0 ? slot.Order : neighbours(slot).Average(n => n.Order)))
                .OrderBy(item => item.Key)
                .ThenBy(item => item.Slot.Order)
                .Select(item => item.Slot)
                .ToList();
            rank.Clear();
            rank.AddRange(keyed);
            for (var i = 0; i < rank.Count; i++)
                rank[i].Order = i;
        }

        static void PlaceHorizontally(List<Slot>[] ranks, float gap)
        {
            // The first rank packed from the left; below it, every slot under the mean of its parents.
            Pack(ranks[0], _ => null, gap);
            for (var r = 1; r < ranks.Length; r++)
                Pack(ranks[r], slot => slot.Up.Count == 0 ? (float?)null : slot.Up.Average(n => n.CenterX), gap);

            // Then, from the bottom up, a root over the mean of its children; the others stay where they are.
            for (var r = ranks.Length - 1; r >= 0; r--)
            {
                Pack(ranks[r], slot => slot.Up.Count == 0 && slot.Down.Count > 0
                    ? slot.Down.Average(n => n.CenterX)
                    : slot.CenterX, gap);
            }
        }

        /// <summary>A run of neighbours in a rank that would overlap apart, so they move together.</summary>
        sealed class Block
        {
            public readonly List<Slot> Slots = new();
            public readonly List<float> Offsets = new();
            public float Width;

            /// <summary>The sum of (where each slot wants its left edge - its offset in the block).</summary>
            public float WantedSum;

            public float X => WantedSum / Slots.Count;
            public float Right => X + Width;
        }

        /// <summary>
        /// Places a rank left to right, each slot centred on where it wants to be (right after its left neighbour
        /// when it has no preference). Neighbours that would overlap are joined into a block, and the block is
        /// centred on what its slots want: four answers that all want the decision's centre fan out under it.
        /// </summary>
        static void Pack(List<Slot> rank, Func<Slot, float?> wantedCenter, float gap)
        {
            var blocks = new List<Block>();
            foreach (var slot in rank)
            {
                var previous = blocks.Count > 0 ? blocks[blocks.Count - 1] : null;
                var wanted = wantedCenter(slot);
                var left = wanted.HasValue ? wanted.Value - slot.Width / 2f : previous != null ? previous.Right + gap : 0f;
                var block = new Block { Width = slot.Width, WantedSum = left };
                block.Slots.Add(slot);
                block.Offsets.Add(0f);

                // Join the blocks on the left for as long as they overlap.
                while (previous != null && previous.Right + gap > block.X)
                {
                    var shift = previous.Width + gap;
                    for (var i = 0; i < block.Slots.Count; i++)
                    {
                        previous.Slots.Add(block.Slots[i]);
                        previous.Offsets.Add(block.Offsets[i] + shift);
                    }
                    previous.WantedSum += block.WantedSum - block.Slots.Count * shift;
                    previous.Width += gap + block.Width;
                    blocks.RemoveAt(blocks.Count - 1);
                    block = previous;
                    previous = blocks.Count > 0 ? blocks[blocks.Count - 1] : null;
                }
                blocks.Add(block);
            }

            foreach (var block in blocks)
            {
                for (var i = 0; i < block.Slots.Count; i++)
                    block.Slots[i].X = block.X + block.Offsets[i];
            }
        }
    }
}
