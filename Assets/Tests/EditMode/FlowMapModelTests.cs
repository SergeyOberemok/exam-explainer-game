using System.Linq;
using NUnit.Framework;

namespace ExamCards.Exam.Tests
{
    public class FlowMapModelTests
    {
        static FlowNode Node(string id, FlowNodeType type, params string[] refs) =>
            new FlowNode { Id = id, Type = type, Label = id, EntityRefs = refs, Status = FlowStatus.Explicit };

        static FlowEdge Edge(string source, string target, FlowStatus status = FlowStatus.Explicit, bool added = false,
            FlowEdgeType type = FlowEdgeType.Next) =>
            new FlowEdge { Source = source, Target = target, Status = status, Added = added, Type = type };

        /// <summary>The headless-sessions question in short: Session ID (e7) is only named by an answer.</summary>
        static Question MakeQuestion()
        {
            var decision = Node("n6", FlowNodeType.Decision, "e1", "e7");
            decision.IsQuestion = true;
            return new Question
            {
                Id = "q",
                Cast = new[]
                {
                    new CastMember { Ref = "e1", Name = "First Invocation", InScene = true },
                    new CastMember { Ref = "e3", Name = "Shared Runner", InScene = true },
                    new CastMember { Ref = "e7", Name = "Session ID", InScene = false },
                },
                Tools = new[]
                {
                    new AnswerTool { Index = 0, Text = "Write findings into CLAUDE.md", Label = "Write into CLAUDE.md", Icon = "file" },
                    new AnswerTool { Index = 1, Text = "Use --continue", Label = "", Icon = "cli-flag" },
                    new AnswerTool { Index = 2, Text = "Resume by session ID", Label = "Resume by ID", Icon = "session", ComponentRefs = new[] { "e7" } },
                },
                Flow = new DecisionFlow
                {
                    Trigger = "A pull request review job starts.",
                    Nodes = new[] { Node("n1", FlowNodeType.Start, "e1"), Node("n4", FlowNodeType.Input, "e3"), decision },
                    Edges = new[] { Edge("n1", "n4", FlowStatus.Inferred), Edge("n4", "n6", type: FlowEdgeType.Yes) },
                },
            };
        }

        static AnswerReview MakeReview()
        {
            var capture = Node("n6", FlowNodeType.Process, "e1", "e7");
            capture.Added = true;
            return new AnswerReview
            {
                CorrectIndex = 2,
                Resolution = new Resolution
                {
                    Rule = "Capture the session ID and resume it explicitly.",
                    Flow = new DecisionFlow
                    {
                        Nodes = new[] { Node("n1", FlowNodeType.Start, "e1"), capture },
                        Edges = new[] { Edge("n1", "n6", added: true) },
                    },
                },
            };
        }

        [Test]
        public void ForQuestion_HangsAnAnswerBoxPerToolOffTheDecision()
        {
            var model = FlowMapModel.ForQuestion(MakeQuestion(), index => index == 0);

            Assert.AreEqual(FlowMapKind.Question, model.Kind);
            Assert.AreEqual("A pull request review job starts.", model.Caption);
            var answers = model.Nodes.Where(node => node.IsAnswer).ToList();
            CollectionAssert.AreEqual(new int?[] { 0, 1, 2 }, answers.Select(node => node.AnswerIndex));
            // The label, or the text when the tool has none.
            CollectionAssert.AreEqual(new[] { "Write into CLAUDE.md", "Use --continue", "Resume by ID" }, answers.Select(node => node.Label));
            CollectionAssert.AreEqual(new[] { "file", "cli-flag", "session" }, answers.Select(node => node.Icon));
            CollectionAssert.AreEqual(new[] { true, false, false }, answers.Select(node => node.Eliminated));
            var answerEdges = model.Edges.Where(edge => edge.Style == MapEdgeStyle.Answer).ToList();
            Assert.AreEqual(3, answerEdges.Count);
            Assert.IsTrue(answerEdges.All(edge => edge.Source == "n6"));
        }

        [Test]
        public void ForQuestion_ShowsOnlyTheSceneCast_SoTheMapCantGiveTheAnswerAway()
        {
            var model = FlowMapModel.ForQuestion(MakeQuestion());

            var decision = model.Nodes.Single(node => node.IsQuestion);
            CollectionAssert.AreEqual(new[] { "e1" }, decision.Cast.Select(member => member.Ref));
        }

        [Test]
        public void ForQuestion_StylesInferredEdges_AndNamesBranches()
        {
            var model = FlowMapModel.ForQuestion(MakeQuestion());

            Assert.AreEqual(MapEdgeStyle.Inferred, model.Edges[0].Style);
            Assert.AreEqual(MapEdgeStyle.Normal, model.Edges[1].Style);
            Assert.AreEqual("yes", model.Edges[1].Label);
        }

        [Test]
        public void ForQuestion_WithoutAFlow_IsEmpty()
        {
            var question = MakeQuestion();
            question.Flow = DecisionFlow.Empty;

            Assert.IsTrue(FlowMapModel.ForQuestion(question).IsEmpty);
            Assert.IsTrue(FlowMapModel.ForQuestion(null).IsEmpty);
        }

        [Test]
        public void ForResolution_MarksTheAddedSteps_ShowsTheWholeCast_AndTheRule()
        {
            var model = FlowMapModel.ForResolution(MakeQuestion(), MakeReview());

            Assert.AreEqual(FlowMapKind.Resolution, model.Kind);
            Assert.AreEqual("Capture the session ID and resume it explicitly.", model.Caption);
            Assert.IsFalse(model.Nodes.Any(node => node.IsAnswer));
            // Same id as the question's decision, but a new step: Added says so.
            var capture = model.Nodes.Single(node => node.Id == "n6");
            Assert.IsTrue(capture.Added);
            Assert.IsFalse(capture.IsQuestion);
            CollectionAssert.AreEqual(new[] { "e1", "e7" }, capture.Cast.Select(member => member.Ref));
            Assert.AreEqual(MapEdgeStyle.Added, model.Edges.Single().Style);
        }

        [Test]
        public void ForResolution_WithoutAResolutionFlow_IsEmpty()
        {
            Assert.IsTrue(FlowMapModel.ForResolution(MakeQuestion(), new AnswerReview()).IsEmpty);
            Assert.IsTrue(FlowMapModel.ForResolution(MakeQuestion(), null).IsEmpty);
        }

        [Test]
        public void MockMaps_LayOutWithoutOverlaps_AndHideTheAnswerOnlyCast()
        {
            var round = new MockQuestionService(latencyMs: 0).StartRoundAsync().GetAwaiter().GetResult();
            var question = round.Questions.Single(q => q.Id == "q1");
            var review = round.SubmitAnswerAsync(question.Id, 1).GetAwaiter().GetResult();

            foreach (var model in new[] { FlowMapModel.ForQuestion(question), FlowMapModel.ForResolution(question, review) })
            {
                Assert.IsFalse(model.IsEmpty, model.Kind.ToString());
                var layout = model.Layout(
                    id => id.StartsWith(FlowMapModel.AnswerIdPrefix) ? new FlowSize(160f, 60f) : new FlowSize(210f, 80f),
                    labelSize: _ => new FlowSize(150f, 30f));
                var rects = layout.Nodes.Values.ToList();
                for (var i = 0; i < rects.Count; i++)
                {
                    for (var j = i + 1; j < rects.Count; j++)
                        Assert.IsFalse(rects[i].Overlaps(rects[j]), model.Kind.ToString());
                }
                // No edge label covers a box (the question's n3 → n5 passes n4's rank in a lane).
                for (var i = 0; i < model.Edges.Count; i++)
                {
                    Assert.AreEqual(!string.IsNullOrEmpty(model.Edges[i].Label), layout.Labels[i].HasValue, $"{model.Kind} edge {i}");
                    if (layout.Labels[i] is not FlowPoint centre)
                        continue;
                    var label = new FlowRect(centre.X - 75f, centre.Y - 15f, 150f, 30f);
                    Assert.IsFalse(rects.Any(rect => rect.Overlaps(label)), $"{model.Kind} edge {i} label covers a box");
                }
                Assert.IsTrue(layout.Edges.All(edge => edge.Count >= 2), model.Kind.ToString());
            }
            // "Log In" (e5) is named only by the correct answer: on the resolution's map, never on the question's.
            Assert.IsFalse(FlowMapModel.ForQuestion(question).Nodes.SelectMany(n => n.Cast).Any(m => m.Ref == "e5"));
            Assert.IsTrue(FlowMapModel.ForResolution(question, review).Nodes.SelectMany(n => n.Cast).Any(m => m.Ref == "e5"));
        }

        [Test]
        public void Layout_PlacesEveryBox()
        {
            var model = FlowMapModel.ForQuestion(MakeQuestion());

            var layout = model.Layout(_ => new FlowSize(150f, 50f));

            Assert.AreEqual(model.Nodes.Count, layout.Nodes.Count);
            Assert.AreEqual(model.Edges.Count, layout.Edges.Count);
            Assert.IsTrue(layout.Edges.All(edge => edge.Count >= 2));
        }
    }
}
