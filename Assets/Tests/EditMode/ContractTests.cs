using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ExamCards.Exam.Api;
using NUnit.Framework;
using UnityEngine;
using FakeTransport = ExamCards.Exam.Tests.ApiQuestionServiceTests.FakeTransport;

namespace ExamCards.Exam.Tests
{
    /// <summary>
    /// Parses real backend responses from <c>Contract/*.json</c>. The backend's <c>tests/test_contract.py</c> writes
    /// these files from the live code (and fails when its copy differs), so a wire change the game can't read fails
    /// here. Regenerate them with <c>UPDATE_CONTRACT=1 uv run --with pytest pytest tests/test_contract.py</c> in
    /// <c>../exam-explainer-api</c>.
    /// </summary>
    public class ContractTests
    {
        const string Q1 = "65f0000000000000000000a1";

        static string Contract(string name) =>
            File.ReadAllText(Path.Combine(Application.dataPath, "Tests", "EditMode", "Contract", name));

        static T Parse<T>(string name) => JsonUtility.FromJson<T>(Contract(name));

        static T Run<T>(Task<T> task) => task.GetAwaiter().GetResult();

        static Question RoundQuestion() => GameQuestionMapper.BuildQuestions(Parse<GameCardRoundDto>("v2_round.json")).Single();

        static Question AddedQuestion() =>
            GameQuestionMapper.ToAddedQuestion(Parse<GameCardRoundQuestionDto>("v2_add_question.json"), Q1);

        static Question SearchedQuestion() =>
            GameQuestionMapper.ToSearchResults(Parse<GameCardListDto>("v2_search.json")).Single();

        [Test]
        public void Round_Add_And_Search_AllBringTheSameCard()
        {
            foreach (var question in new[] { RoundQuestion(), AddedQuestion(), SearchedQuestion() })
            {
                Assert.AreEqual(Q1, question.Id);
                CollectionAssert.AreEqual(new[] { "Authorization", "x-api-key", "X-Key" }, question.Answers);
                Assert.AreEqual("Send the key in its own header", question.Tools[1].Label);
                Assert.AreEqual("api", question.Icon);
                CollectionAssert.AreEqual(new[] { "credential", "parameter", "tags" }, question.Tools.Select(t => t.Icon));
            }
        }

        [Test]
        public void Card_SendsTheCriteriaBeforeAnswering()
        {
            var question = RoundQuestion();

            Assert.IsNotEmpty(question.Requirements);
        }

        [Test]
        public void Card_HasTheStory_Objectives_AndTheStem()
        {
            var question = RoundQuestion();

            CollectionAssert.AreEqual(new[] { "tools-mcp-config", "tools-errors" }, question.Objectives);
            Assert.AreEqual(2, question.Difficulty);
            Assert.AreEqual("support", question.Scenario);
            Assert.AreEqual("build", question.Stage);
            Assert.AreEqual("The first API call of the support agent is refused.", question.Beat);
            // The scenario paragraph stays in the whole text; the stem is what the screen shows.
            StringAssert.StartsWith("SCENARIO:", question.Text);
            Assert.AreEqual("Which header carries the API key?", question.OriginalText);
        }

        [Test]
        public void Card_HasTheBriefingAskCastAndActions()
        {
            var question = RoundQuestion();

            Assert.AreEqual(PartRole.Context, question.Briefing.Single().Role);
            Assert.AreEqual("A support agent calls the API.", question.Briefing[0].Text);
            CollectionAssert.AreEqual(new[] { "e1" }, question.Briefing[0].EntityRefs);
            Assert.AreEqual("Which header carries the API key?", question.Ask.Text);

            CollectionAssert.AreEqual(new[] { "e1", "e2", "e3" }, question.Cast.Select(c => c.Ref));
            Assert.AreEqual(CastType.Artifact, question.Cast[0].Type);
            CollectionAssert.AreEqual(new[] { "key" }, question.Cast[0].Aliases);
            Assert.AreEqual("API Key", question.SceneCast.Single().Name);
            // Only answer B names x-api-key, so only its card shows it.
            Assert.AreEqual("x-api-key", question.ToolCast(1).Single().Name);
            Assert.IsEmpty(question.ToolCast(0));

            var action = question.Actions.Single();
            Assert.AreEqual("e1", action.TargetRef);
            Assert.IsEmpty(action.ActorRef);
            Assert.AreEqual(3, question.Flow.Nodes.Count);
        }

        [Test]
        public void Answer_HasEveryOutcome_TheRequirementsAndTheStoryLine()
        {
            var question = RoundQuestion();

            var review = GameQuestionMapper.ToReview(Parse<GameCardAnswerResultDto>("v2_answer.json"), Q1, 1);
            QuestionValidator.ValidateReview(question, review);
            Assert.AreEqual(1, review.CorrectIndex);
            Assert.IsFalse(string.IsNullOrEmpty(review.Review.Explanation));
            Assert.AreEqual("The agent reaches the API.", review.StoryLine);
            Assert.IsEmpty(review.MisconceptionFor(1));

            var resolution = review.Resolution;
            CollectionAssert.AreEqual(new[] { AnswerImpact.Failure, AnswerImpact.Success, AnswerImpact.Partial },
                resolution.Options.Select(o => o.Impact));
            Assert.AreEqual("The review goes well.", resolution.ForAnswer(1).Consequence);
            Assert.IsTrue(resolution.HasRequirementMatches);
            Assert.AreEqual(1, resolution.MetCount(1));
            Assert.AreEqual(0, resolution.MetCount(2));
            Assert.AreEqual("Plan first when edits need review.", resolution.Rule);
            Assert.IsTrue(resolution.Flow.Nodes.Any(n => n.Added));
        }

        [Test]
        public void WrongAnswer_HasItsMisconceptionAndTheFailLine()
        {
            var question = RoundQuestion();

            var wrong = GameQuestionMapper.ToReview(Parse<GameCardAnswerResultDto>("v2_answer_wrong.json"), Q1, 2);
            QuestionValidator.ValidateReview(question, wrong);
            Assert.AreEqual("Header names are free to choose.", wrong.MisconceptionFor(2));
            Assert.AreEqual("Any HTTP auth header carries the key.", wrong.MisconceptionFor(0));
            Assert.AreEqual("Every call still comes back unauthorized.", wrong.StoryLine);
            Assert.AreEqual(AnswerImpact.Partial, wrong.Resolution.ForAnswer(2).Impact);
            CollectionAssert.AreEqual(new[] { "Edits before review" }, wrong.Resolution.ForAnswer(2).Gaps);
        }

        [Test]
        public void FiftyFifty_MapsIndicesAndTheRoundsCount()
        {
            var result = GameQuestionMapper.ToFiftyFifty(Parse<GameFiftyFiftyResultDto>("fifty_fifty.json"), Q1);

            Assert.AreEqual(1, result.Remaining);
            Assert.IsFalse(result.Eliminated.Contains(1));
        }

        [Test]
        public void EmptyBatch_MapsToNone()
        {
            Assert.AreEqual(0, GameQuestionMapper.BuildBatch(Parse<GameCardBatchDto>("v2_batch_empty.json"), 1).Count);
        }

        [Test]
        public void Errors_CarryDetailAndCode()
        {
            var transport = new FakeTransport()
                .Reply(422, Contract("error_validation.json"));
            var service = new ApiQuestionService("http://api.test", 10, 5, transport, retryDelaySeconds: 0f);

            var e = Assert.Throws<ApiException>(() => Run(service.StartRoundAsync()));

            Assert.AreEqual("validation_error", e.Code);
            StringAssert.Contains("count", e.Detail);

            var conflict = JsonUtility.FromJson<ApiErrorDto>(Contract("error_already_answered.json"));
            Assert.AreEqual(ApiErrorCodes.AlreadyAnswered, conflict.code);
        }

        [Test]
        public void LostAnswerResponse_RecoversFromTheRealBodies()
        {
            var transport = new FakeTransport()
                .Reply(201, Contract("v2_round.json"))
                .Reply(409, Contract("error_already_answered.json"))
                .Reply(200, Contract("v2_answer.json"));
            var round = Run(new ApiQuestionService("http://api.test", 10, 5, transport, retryDelaySeconds: 0f)
                .StartRoundAsync());
            var session = new ExamSession(round);

            var result = Run(session.AnswerAsync(0));

            // The server recorded answer 1 before the player picked 0; answer 1 counts.
            Assert.AreEqual(1, result.AnsweredIndex);
            Assert.IsTrue(result.IsCorrect);
            Assert.AreEqual(1, session.Score);
            Assert.AreEqual("The agent reaches the API.", session.CurrentReview.StoryLine);
        }
    }
}
