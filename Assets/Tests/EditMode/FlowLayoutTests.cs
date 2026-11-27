using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace ExamCards.Exam.Tests
{
    public class FlowLayoutTests
    {
        static readonly FlowLayoutOptions Options = new FlowLayoutOptions();

        static FlowSize Box(string id) => new FlowSize(200f, 60f);

        static FlowLink[] Links(params (string Source, string Target)[] links) =>
            links.Select(link => new FlowLink(link.Source, link.Target)).ToArray();

        /// <summary>The question map of the headless-sessions question: a fan-in, and n4 → n5 skips n3's rank.</summary>
        static readonly string[] SampleNodes = { "n1", "n2", "n3", "n4", "n5", "n6" };

        static readonly FlowLink[] SampleLinks = Links(
            ("n1", "n2"), ("n1", "n4"), ("n2", "n3"), ("n3", "n5"), ("n4", "n5"), ("n5", "n6"));

        static void AssertNoOverlaps(FlowLayoutResult layout)
        {
            var rects = layout.Nodes.ToList();
            for (var i = 0; i < rects.Count; i++)
            {
                for (var j = i + 1; j < rects.Count; j++)
                    Assert.IsFalse(rects[i].Value.Overlaps(rects[j].Value), $"{rects[i].Key} overlaps {rects[j].Key}");
            }
        }

        [Test]
        public void Chain_GoesStraightDown()
        {
            var layout = FlowLayout.Compute(new[] { "a", "b", "c" }, Links(("a", "b"), ("b", "c")), Box, Options);

            Assert.Less(layout.Nodes["a"].Bottom, layout.Nodes["b"].Y);
            Assert.Less(layout.Nodes["b"].Bottom, layout.Nodes["c"].Y);
            Assert.AreEqual(layout.Nodes["a"].CenterX, layout.Nodes["c"].CenterX, 0.01f);
            // From the bottom centre of the source to the top centre of the target.
            var edge = layout.Edges[0];
            Assert.AreEqual(2, edge.Count);
            Assert.AreEqual(layout.Nodes["a"].Bottom, edge[0].Y, 0.01f);
            Assert.AreEqual(layout.Nodes["b"].Y, edge[1].Y, 0.01f);
            Assert.AreEqual(200f + 2f * Options.Padding, layout.Width, 0.01f);
        }

        [Test]
        public void Sample_RanksByLongestPath_AndKeepsNodesApart()
        {
            var layout = FlowLayout.Compute(SampleNodes, SampleLinks, Box, Options);

            // n2 and n4 share a rank; n5 waits for n3, the longer branch.
            Assert.AreEqual(layout.Nodes["n2"].Y, layout.Nodes["n4"].Y, 0.01f);
            Assert.Greater(layout.Nodes["n5"].Y, layout.Nodes["n3"].Bottom);
            Assert.Greater(layout.Nodes["n6"].Y, layout.Nodes["n5"].Bottom);
            AssertNoOverlaps(layout);
            // The root sits over its two children.
            var children = (layout.Nodes["n2"].CenterX + layout.Nodes["n4"].CenterX) / 2f;
            Assert.AreEqual(children, layout.Nodes["n1"].CenterX, 0.01f);
        }

        [Test]
        public void LinkSkippingARank_RunsInItsOwnLane_NotThroughTheNodeThere()
        {
            var layout = FlowLayout.Compute(SampleNodes, SampleLinks, Box, Options);
            var skip = layout.Edges[4]; // n4 → n5, past n3's rank

            Assert.AreEqual(4, skip.Count);
            var n3 = layout.Nodes["n3"];
            // The two lane points run straight down through n3's rank, beside n3.
            Assert.AreEqual(skip[1].X, skip[2].X, 0.01f);
            Assert.IsTrue(skip[1].X < n3.X || skip[1].X > n3.Right, "the lane runs through n3");
            Assert.LessOrEqual(skip[1].Y, n3.Y + 0.01f);
            Assert.GreaterOrEqual(skip[2].Y, n3.Bottom - 0.01f);
        }

        [Test]
        public void Cycle_DoesNotHang_AndIsStillDrawn()
        {
            var layout = FlowLayout.Compute(new[] { "a", "b", "c" }, Links(("a", "b"), ("b", "c"), ("c", "a")), Box, Options);

            Assert.AreEqual(3, layout.Nodes.Count);
            Assert.Less(layout.Nodes["a"].Y, layout.Nodes["c"].Y);
            Assert.AreEqual(6, layout.Edges[2].Count);
            AssertNoOverlaps(layout);
        }

        [Test]
        public void LinkClosingACycle_GoesUpRoundTheNodes_IntoTheBottomOfItsTarget()
        {
            // A retry loop: n3 goes back to n2.
            var nodes = new[] { "n1", "n2", "n3" };
            var links = new[]
            {
                new FlowLink("n1", "n2"), new FlowLink("n2", "n3"), new FlowLink("n3", "n2", new FlowSize(80f, 16f)),
            };

            var layout = FlowLayout.Compute(nodes, links, Box, Options);
            var back = layout.Edges[2];
            var n2 = layout.Nodes["n2"];
            var n3 = layout.Nodes["n3"];

            // Leaves the top of n3 and enters the bottom of n2, going up, so the arrowhead points into n2.
            Assert.AreEqual(n3.Y, back[0].Y, 0.01f);
            Assert.AreEqual(n2.Bottom, back[back.Count - 1].Y, 0.01f);
            Assert.Less(back[back.Count - 1].Y, back[back.Count - 2].Y);
            // Every point is outside every box, apart from the two ends on their edges.
            for (var i = 1; i < back.Count - 1; i++)
            {
                foreach (var rect in layout.Nodes.Values)
                    Assert.IsFalse(Inside(rect, back[i]), $"point {i} {back[i]} is inside a box");
            }
            // Its column is past every box, with room for the label, and the map is widened to hold it.
            var column = back[2].X;
            Assert.Greater(column - 40f, layout.Nodes.Values.Max(rect => rect.Right));
            Assert.LessOrEqual(column + 40f, layout.Width - Options.Padding + 0.01f);
            var label = layout.Labels[2].Value;
            Assert.AreEqual(column, label.X, 0.01f);
            Assert.IsFalse(layout.Nodes.Values.Any(rect => LabelRect(label, 80f, 16f).Overlaps(rect)));
        }

        [Test]
        public void LabelOfALinkSkippingARank_SitsInTheGapBelowItsSource()
        {
            var links = SampleLinks.Select(link => new FlowLink(link.Source, link.Target, new FlowSize(150f, 30f))).ToArray();

            var layout = FlowLayout.Compute(SampleNodes, links, Box, Options);

            for (var i = 0; i < links.Length; i++)
            {
                var label = LabelRect(layout.Labels[i].Value, 150f, 30f);
                foreach (var (id, rect) in layout.Nodes)
                    Assert.IsFalse(label.Overlaps(rect), $"the label of link {i} covers {id}");
            }
            // n4 → n5: between n4's bottom and n3's rank, not beside n3.
            var skip = layout.Labels[4].Value;
            Assert.Greater(skip.Y, layout.Nodes["n4"].Bottom);
            Assert.Less(skip.Y, layout.Nodes["n3"].Y);
        }

        [Test]
        public void LinksWithoutLabels_HaveNoLabelPoint()
        {
            var layout = FlowLayout.Compute(SampleNodes, SampleLinks, Box, Options);

            Assert.AreEqual(SampleLinks.Length, layout.Labels.Count);
            Assert.IsTrue(layout.Labels.All(label => label == null));
        }

        [Test]
        public void OnlyNullIds_HasNothing()
        {
            var layout = FlowLayout.Compute(new string[] { null, null }, Links(("a", "b")), Box, Options);

            Assert.AreEqual(0, layout.Nodes.Count);
            Assert.AreEqual(0f, layout.Width);
        }

        static bool Inside(FlowRect rect, FlowPoint point) =>
            point.X > rect.X && point.X < rect.Right && point.Y > rect.Y && point.Y < rect.Bottom;

        static FlowRect LabelRect(FlowPoint centre, float width, float height) =>
            new(centre.X - width / 2f, centre.Y - height / 2f, width, height);

        [Test]
        public void BadLinks_AreKeptAsEmptyPaths()
        {
            var layout = FlowLayout.Compute(new[] { "a", "b", "a" }, Links(("a", "ghost"), ("a", "a"), ("a", "b")), Box, Options);

            Assert.AreEqual(2, layout.Nodes.Count);
            Assert.AreEqual(3, layout.Edges.Count);
            Assert.IsEmpty(layout.Edges[0]);
            Assert.IsEmpty(layout.Edges[1]);
            Assert.AreEqual(2, layout.Edges[2].Count);
        }

        [Test]
        public void Empty_HasNothing()
        {
            var layout = FlowLayout.Compute(new string[0], null, Box, Options);

            Assert.AreEqual(0, layout.Nodes.Count);
            Assert.AreEqual(0f, layout.Width);
        }

        [Test]
        public void Answers_FanOutCentredUnderTheDecision_SideBySide()
        {
            // The decision has a parent, so it stays put and its answers centre under it as one block.
            var nodes = new List<string> { "p", "q" };
            var links = new List<FlowLink> { new FlowLink("p", "q") };
            for (var i = 0; i < 4; i++)
            {
                nodes.Add("a" + i);
                links.Add(new FlowLink("q", "a" + i));
            }

            var layout = FlowLayout.Compute(nodes, links, id => id.StartsWith("a") ? new FlowSize(150f, 50f) : new FlowSize(200f, 80f), Options);

            AssertNoOverlaps(layout);
            for (var i = 1; i < 4; i++)
                Assert.Greater(layout.Nodes["a" + i].X, layout.Nodes["a" + (i - 1)].X);
            var middle = (layout.Nodes["a0"].CenterX + layout.Nodes["a3"].CenterX) / 2f;
            Assert.AreEqual(middle, layout.Nodes["q"].CenterX, 0.01f);
            Assert.AreEqual(layout.Nodes["p"].CenterX, layout.Nodes["q"].CenterX, 0.01f);
            // Answers 150 wide with 28 between: 4 * 150 + 3 * 28.
            Assert.AreEqual(684f + 2f * Options.Padding, layout.Width, 0.01f);
        }

        [Test]
        public void Sample_IsBalanced_UnderItsRoot()
        {
            var layout = FlowLayout.Compute(SampleNodes, SampleLinks, Box, Options);

            // n2 and n4 both want n1's centre, so they straddle it; n5 then sits under it again.
            var n1 = layout.Nodes["n1"].CenterX;
            Assert.AreEqual(n1 - layout.Nodes["n2"].CenterX, layout.Nodes["n4"].CenterX - n1, 0.01f);
            Assert.AreEqual(n1, layout.Nodes["n5"].CenterX, 0.01f);
        }
    }
}
