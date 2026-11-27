using System.Linq;
using NUnit.Framework;

namespace ExamCards.Exam.Tests
{
    /// <summary>The domain helpers behind the debrief and the cards: requirements met, card cast and chip texts.</summary>
    public class DebriefTests
    {
        static Resolution MakeResolution() => new Resolution
        {
            AnswerRequirements = new[] { "Gets the full history", "Targets the right session", "Passes the ID explicitly" },
            Options = new[]
            {
                new AnswerOutcome { Index = 0, IsCorrect = false },
                new AnswerOutcome
                {
                    Index = 1,
                    IsCorrect = false,
                    // Text with stray whitespace still matches; one that isn't a requirement is ignored.
                    SupportedRequirements = new[] { "  Gets the full history ", "Passes the ID explicitly", "Something else" },
                },
                new AnswerOutcome
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

        [Test]
        public void HasRequirementMatches_IsFalseWithoutAnySupportedRequirement()
        {
            Assert.IsTrue(MakeResolution().HasRequirementMatches);

            var stored = new Resolution
            {
                AnswerRequirements = new[] { "Gets the full history" },
                Options = new[] { new AnswerOutcome { Index = 0, IsCorrect = true } },
            };
            Assert.IsFalse(stored.HasRequirementMatches);
            Assert.IsFalse(Resolution.Empty.HasRequirementMatches);
        }

        static CastMember Member(string reference, bool scene) => new CastMember { Ref = reference, Name = reference, InScene = scene };

        static Question MakeQuestion() => new Question
        {
            Id = "q",
            Cast = new[] { Member("e1", true), Member("e6", false), Member("e7", false) },
            Tools = new[]
            {
                new AnswerTool { Index = 0, Text = "a", ComponentRefs = new[] { "e1", "e7" } },
                new AnswerTool { Index = 1, Text = "b", ComponentRefs = new[] { "e1", "ghost" } },
            },
        };

        [Test]
        public void ToolCast_LeavesOutTheScenesMembers_AndUnknownRefs()
        {
            var question = MakeQuestion();

            CollectionAssert.AreEqual(new[] { "e7" }, question.ToolCast(0).Select(m => m.Ref));
            Assert.IsEmpty(question.ToolCast(1));
            Assert.IsEmpty(question.ToolCast(2));
            CollectionAssert.AreEqual(new[] { "e1" }, question.SceneCast.Select(m => m.Ref));
        }

        [Test]
        public void ToolComponents_PutsTheAnswersOwnMembersFirst_AndDropsUnknownRefs()
        {
            var question = MakeQuestion();

            CollectionAssert.AreEqual(new[] { "e7", "e1" }, question.ToolComponents(0).Select(m => m.Ref));
            CollectionAssert.AreEqual(new[] { "e1" }, question.ToolComponents(1).Select(m => m.Ref));
            Assert.IsEmpty(question.ToolComponents(2));
        }

        [Test]
        public void CastFor_KeepsTheOrder_AndDropsRepeatsAndUnknownRefs()
        {
            var question = MakeQuestion();

            CollectionAssert.AreEqual(new[] { "e7", "e1" },
                question.CastFor(new[] { "e7", "ghost", "e1", "e7", null }).Select(m => m.Ref));
            Assert.IsNull(question.FindCast("ghost"));
        }

        [Test]
        public void MisconceptionFor_FollowsTheAnswer_AndIsEmptyForTheCorrectOne()
        {
            var review = new AnswerReview
            {
                CorrectIndex = 1,
                Resolution = new Resolution
                {
                    Options = new[]
                    {
                        new AnswerOutcome { Index = 0, Misconception = "Believes X." },
                        new AnswerOutcome { Index = 1, IsCorrect = true, Misconception = "Never shown." },
                    },
                },
            };

            Assert.AreEqual("Believes X.", review.MisconceptionFor(0));
            Assert.IsEmpty(review.MisconceptionFor(1));
            Assert.IsEmpty(review.MisconceptionFor(2));
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
