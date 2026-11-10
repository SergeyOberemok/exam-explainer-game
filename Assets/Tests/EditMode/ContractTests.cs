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
        const string Q2 = "65f0000000000000000000a2";
        const string Q3 = "65f0000000000000000000a3";

        static string Contract(string name) =>
            File.ReadAllText(Path.Combine(Application.dataPath, "Tests", "EditMode", "Contract", name));

        static T Parse<T>(string name) => JsonUtility.FromJson<T>(Contract(name));

        static T Run<T>(Task<T> task) => task.GetAwaiter().GetResult();

        [Test]
        public void Round_BuildsQuestionsWithTheirSimplifiedText()
        {
            var questions = GameQuestionMapper.BuildQuestions(Parse<GameRoundDto>("round.json"));

            CollectionAssert.AreEqual(new[] { Q1, Q2 }, questions.Select(q => q.Id));
            Assert.IsTrue(questions[0].CanReveal);
            Assert.AreEqual("Where does the key go?", questions[0].Simplified.Question);
            CollectionAssert.AreEqual(new[] { "Authorization", "x-api-key", "X-Key" }, questions[0].Original.Answers);
            Assert.IsFalse(questions[1].CanReveal);
        }

        [Test]
        public void Entities_ComeWithTheSimplifiedText()
        {
            var round = GameQuestionMapper.BuildQuestions(Parse<GameRoundDto>("round.json"));
            var added = GameQuestionMapper.ToAddedQuestion(Parse<GameRoundQuestionDto>("add_question.json"), Q1);

            foreach (var question in new[] { round[0], added })
            {
                var entities = question.Simplified.Entities;
                CollectionAssert.AreEqual(new[] { "API Key", "Send Request" }, entities.Question.Select(e => e.Name));
                CollectionAssert.AreEqual(new[] { EntityKind.Object, EntityKind.Action }, entities.Question.Select(e => e.Kind));
                CollectionAssert.AreEqual(new[] { "key" }, entities.Question[0].Aliases);
                Assert.AreEqual(3, entities.Answers.Count);
                Assert.AreEqual("x-api-key", entities.ForAnswer(1).Single().Name);
                Assert.AreEqual(0, entities.ForAnswer(2).Count);
                Assert.IsTrue(question.Original.Entities.IsEmpty);

                // Icons: null where the backend has none (or no longer has the stored key).
                Assert.AreEqual("credential", entities.QuestionIcon);
                CollectionAssert.AreEqual(new[] { "credential", "send" }, entities.Question.Select(e => e.Icon));
                CollectionAssert.AreEqual(new[] { "permission", null, "tags" }, entities.AnswerIcons);
                Assert.IsNull(entities.ForAnswer(0).Single().Icon);
                Assert.IsNull(entities.ForAnswer(1).Single().Icon);
            }
            Assert.IsTrue(round[1].Original.Entities.IsEmpty);
        }

        [Test]
        public void Answers_MapToReviews()
        {
            var questions = GameQuestionMapper.BuildQuestions(Parse<GameRoundDto>("round.json"));

            var review = GameQuestionMapper.ToReview(Parse<GameAnswerResultDto>("answer.json"), Q1, 1);
            QuestionValidator.ValidateReview(questions[0], review);
            Assert.AreEqual(1, review.CorrectIndex);
            Assert.AreEqual("Simply put: x-api-key.", review.Simplified.Explanation);

            var plain = GameQuestionMapper.ToReview(Parse<GameAnswerResultDto>("answer_no_simplified.json"), Q2, 0);
            QuestionValidator.ValidateReview(questions[1], plain);
            Assert.IsNull(plain.Simplified);
        }

        [Test]
        public void FiftyFifty_MapsIndicesAndTheRoundsCount()
        {
            var questions = GameQuestionMapper.BuildQuestions(Parse<GameRoundDto>("round.json"));
            var result = GameQuestionMapper.ToFiftyFifty(Parse<GameFiftyFiftyResultDto>("fifty_fifty.json"), Q1);

            QuestionValidator.ValidateFiftyFifty(questions[0], result);
            Assert.AreEqual(1, result.Remaining);
            Assert.IsFalse(result.Eliminated.Contains(1));
        }

        [Test]
        public void Batches_MapToQuestions_AndAnEmptyBatchToNone()
        {
            var batch = GameQuestionMapper.BuildBatch(Parse<GameRoundBatchDto>("batch.json"), 1);
            Assert.AreEqual(Q3, batch.Single().Id);
            // Exam text keeps literal tags; the UI shows it without rich text.
            Assert.AreEqual("<example>", batch[0].Original.Answers[0]);

            Assert.AreEqual(0, GameQuestionMapper.BuildBatch(Parse<GameRoundBatchDto>("batch_empty.json"), 2).Count);
        }

        [Test]
        public void AddQuestion_And_Search_Map()
        {
            var added = GameQuestionMapper.ToAddedQuestion(Parse<GameRoundQuestionDto>("add_question.json"), Q1);
            Assert.IsTrue(added.CanReveal);

            var results = GameQuestionMapper.ToSearchResults(Parse<GameQuestionListDto>("search.json"));
            Assert.Greater(results.Count, 0);
            Assert.IsTrue(results.All(q => !q.CanReveal));
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
                .Reply(201, Contract("round.json"))
                .Reply(409, Contract("error_already_answered.json"))
                .Reply(200, Contract("answer.json"));
            var round = Run(new ApiQuestionService("http://api.test", 10, 5, transport, retryDelaySeconds: 0f)
                .StartRoundAsync());
            var session = new ExamSession(round);

            var result = Run(session.AnswerAsync(0));

            // The server recorded answer 1 before the player picked 0; answer 1 counts.
            Assert.AreEqual(1, result.AnsweredIndex);
            Assert.IsTrue(result.IsCorrect);
            Assert.AreEqual(1, session.Score);
        }
    }
}
