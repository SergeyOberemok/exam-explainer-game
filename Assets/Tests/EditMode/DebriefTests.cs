using System.Linq;
using NUnit.Framework;

namespace ExamCards.Exam.Tests
{
    /// <summary>The domain helpers behind the debrief and the cards: requirements met, card entities and chip texts.</summary>
    public class DebriefTests
    {
        static Resolution MakeResolution() => new Resolution
        {
            AnswerRequirements = new[] { "Gets the full history", "Targets the right session", "Passes the ID explicitly" },
            Options = new[]
            {
                new OptionEvaluation { Index = 0, IsCorrect = false },
                new OptionEvaluation
                {
                    Index = 1,
                    IsCorrect = false,
                    // Text with stray whitespace still matches; one that isn't a requirement is ignored.
                    SupportedRequirements = new[] { "  Gets the full history ", "Passes the ID explicitly", "Something else" },
                },
                new OptionEvaluation
                {
                    Index = 2,
                    IsCorrect = true,
                    SupportedRequirements = new[] { "Gets the full history", "Targets the right session", "Passes the ID explicitly" },
                },
            },
        };

        [Test]
        public void RequirementsMetBy_MatchesTrimmedText_AndIgnoresUnknownOnes()
        {
            var resolution = MakeResolution();

            CollectionAssert.AreEqual(new[] { true, false, true }, resolution.RequirementsMetBy(1));
            Assert.AreEqual(2, resolution.MetCount(1));
            Assert.AreEqual(3, resolution.MetCount(2));
            CollectionAssert.AreEqual(new[] { false, false, false }, resolution.RequirementsMetBy(0));
        }

        [Test]
        public void RequirementsMetBy_IsEmptyForAnAnswerWithoutAnEvaluation()
        {
            Assert.IsEmpty(MakeResolution().RequirementsMetBy(3));
            Assert.AreEqual(0, MakeResolution().MetCount(3));
            Assert.IsEmpty(Resolution.Empty.RequirementsMetBy(0));
        }

        [Test]
        public void RequirementsMetBy_CaseMatters()
        {
            var resolution = MakeResolution();
            resolution.Options[0].SupportedRequirements = new[] { "gets the full history" };

            Assert.AreEqual(0, resolution.MetCount(0));
        }

        static VisualEntity Entity(string name, string @ref = "") => new VisualEntity { Name = name, Ref = @ref };

        [Test]
        public void ForAnswerOnly_LeavesOutTheQuestionsEntities_ByRefOrElseByName()
        {
            var entities = new PromptEntities
            {
                Question = new[] { Entity("First Invocation", "e1"), Entity("Runner") },
                Answers = new[]
                {
                    new[] { Entity("First Invocation", "e1"), Entity("CLAUDE.md", "e7") },
                    // Same name, other ref: a different entity. No ref on one side: matched by name, ignoring case.
                    new[] { Entity("First Invocation", "e9"), Entity("runner", "e3") },
                },
            };

            CollectionAssert.AreEqual(new[] { "CLAUDE.md" }, entities.ForAnswerOnly(0).Select(e => e.Name));
            CollectionAssert.AreEqual(new[] { "First Invocation" }, entities.ForAnswerOnly(1).Select(e => e.Name));
            Assert.IsEmpty(entities.ForAnswerOnly(2));
            Assert.IsEmpty(PromptEntities.Empty.ForAnswerOnly(0));
        }

        [Test]
        public void ForAnswerOnly_KeepsEverythingWhenTheQuestionHasNoEntities()
        {
            var entities = new PromptEntities { Answers = new[] { new[] { Entity("Session ID", "e6"), Entity("Hook") } } };

            Assert.AreEqual(2, entities.ForAnswerOnly(0).Count);
        }

        [TestCase(0, "")]
        [TestCase(1, "EASY")]
        [TestCase(2, "MEDIUM")]
        [TestCase(3, "HARD")]
        [TestCase(4, "")]
        public void Difficulty_HasAWordForEachLevel(int difficulty, string text) =>
            Assert.AreEqual(text, QuestionLabels.Difficulty(difficulty));

        [TestCase("agents-sessions", "AGENTS · SESSIONS")]
        [TestCase("tools-mcp-config", "TOOLS · MCP CONFIG")]
        [TestCase(" prompting ", "PROMPTING")]
        [TestCase("", "")]
        [TestCase(null, "")]
        public void Objective_SplitsTheAreaFromTheRest(string key, string text) =>
            Assert.AreEqual(text, QuestionLabels.Objective(key));
    }
}
