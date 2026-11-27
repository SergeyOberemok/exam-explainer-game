using System;
using System.Threading;
using System.Threading.Tasks;
using ExamCards.Exam.Api;
using NUnit.Framework;
using FakeTransport = ExamCards.Exam.Tests.ApiQuestionServiceTests.FakeTransport;

namespace ExamCards.Exam.Tests
{
    /// <summary>Lost responses, retries, later batches and error codes against a fake backend.</summary>
    public class ApiExamRoundTests
    {
        const string BaseUrl = "http://api.test";
        const string RoundUrl = BaseUrl + "/v2/game/rounds/r1";

        static T Run<T>(Task<T> task) => task.GetAwaiter().GetResult();

        static ApiQuestionService MakeService(FakeTransport transport, int count = 10) =>
            new ApiQuestionService(BaseUrl, count, timeoutSeconds: 7, transport, retryDelaySeconds: 0f);

        static IExamRound StartRound(FakeTransport transport, int count = 10) =>
            Run(MakeService(transport.Reply(201, GameQuestionMapperTests.RoundJson), count).StartRoundAsync());

        const string AlreadyAnswered = "{\"detail\":\"question already answered\",\"code\":\"already_answered\"}";

        [Test]
        public void SubmitAnswer_AfterALostResponse_ReturnsTheRecordedAnswer()
        {
            var transport = new FakeTransport();
            var round = StartRound(transport);
            // Answer 0 reached the server earlier; now the player picks 2.
            transport.Reply(409, AlreadyAnswered).Reply(200, GameQuestionMapperTests.AnswerJson);

            var review = Run(round.SubmitAnswerAsync("q1", 2));

            Assert.AreEqual(RoundUrl + "/answers", transport.Requests[1].Url);
            Assert.AreEqual("GET", transport.Requests[2].Method);
            Assert.AreEqual(RoundUrl + "/answers/q1", transport.Requests[2].Url);
            Assert.AreEqual(0, review.AnsweredIndex);
            Assert.AreEqual(1, review.CorrectIndex);
        }

        [Test]
        public void SubmitAnswer_TransientFailure_IsRetriedOnce()
        {
            var transport = new FakeTransport();
            var round = StartRound(transport);
            transport.Fail("Request timeout").Reply(200, GameQuestionMapperTests.AnswerJson);

            var review = Run(round.SubmitAnswerAsync("q1", 0));

            Assert.AreEqual(3, transport.Requests.Count);
            Assert.AreEqual(transport.Requests[1], transport.Requests[2]);
            Assert.AreEqual(0, review.AnsweredIndex);
        }

        [Test]
        public void SubmitAnswer_TwoFailures_Throw()
        {
            var transport = new FakeTransport();
            var round = StartRound(transport);
            transport.Reply(503, "{\"detail\":\"down\",\"code\":\"http_503\"}").Fail("Cannot connect");

            var e = Assert.Throws<ApiException>(() => Run(round.SubmitAnswerAsync("q1", 0)));

            Assert.AreEqual(0, e.StatusCode);
            Assert.AreEqual(3, transport.Requests.Count);
        }

        [Test]
        public void ClientErrors_AreNotRetried_AndCarryTheCode()
        {
            var transport = new FakeTransport();
            var round = StartRound(transport);
            transport.Reply(409, "{\"detail\":\"no 50/50 charges left\",\"code\":\"no_fifty_fifty_charges\"}");

            var e = Assert.Throws<ApiException>(() => Run(round.EliminateAsync("q1")));

            Assert.AreEqual("no_fifty_fifty_charges", e.Code);
            Assert.AreEqual(2, transport.Requests.Count);
        }

        [Test]
        public void StartRound_IsNotRetried()
        {
            var transport = new FakeTransport().Fail("Cannot connect");

            Assert.Throws<ApiException>(() => Run(MakeService(transport).StartRoundAsync()));

            Assert.AreEqual(1, transport.Requests.Count);
        }

        [Test]
        public void StartRound_NoQuestions_ThrowsNoQuestionsException()
        {
            var transport = new FakeTransport()
                .Reply(404, "{\"detail\":\"no playable questions match\",\"code\":\"no_questions\"}");

            var e = Assert.Throws<NoQuestionsException>(() => Run(MakeService(transport).StartRoundAsync()));

            StringAssert.Contains("no playable questions", e.Message);
        }

        [Test]
        public void StartRound_MoreMatchesThanTheRoundHolds_HasMore()
        {
            Assert.IsTrue(StartRound(new FakeTransport()).HasMore);
            var transport = new FakeTransport()
                .Reply(201, GameQuestionMapperTests.RoundJson.Replace("\"total\": 60", "\"total\": 2"));
            Assert.IsFalse(Run(MakeService(transport).StartRoundAsync()).HasMore);
        }

        static string Batch(int index, int remaining, params string[] ids)
        {
            var items = string.Join(",", Array.ConvertAll(ids, id =>
                $"{{\"id\":\"{id}\",\"section\":\"S\",\"assessment_type\":\"t\",\"question\":\"Q {id}\"," +
                "\"tools\":[{\"index\":0,\"text\":\"A\"},{\"index\":1,\"text\":\"B\"}]}"));
            return $"{{\"batch_index\":{index},\"items\":[{items}],\"remaining\":{remaining}}}";
        }

        [Test]
        public void FetchMore_AsksForBatchesInOrder_AndRetriesTheSameIndex()
        {
            var transport = new FakeTransport();
            var round = StartRound(transport, count: 2);
            transport
                .Reply(200, Batch(1, 3, "b1", "b2"))
                .Reply(400, "{\"detail\":\"bad\",\"code\":\"http_400\"}")
                .Reply(200, Batch(2, 0, "b3"));

            var first = Run(round.FetchMoreAsync());
            Assert.AreEqual(new[] { "b1", "b2" }, new[] { first[0].Id, first[1].Id });
            Assert.AreEqual(RoundUrl + "/batches", transport.Requests[1].Url);
            Assert.AreEqual("{\"batch_index\":1,\"count\":2}", transport.Requests[1].Body);
            Assert.IsTrue(round.HasMore);

            Assert.Throws<ApiException>(() => Run(round.FetchMoreAsync()));
            var second = Run(round.FetchMoreAsync());

            // The failed call did not move on: both asked for batch 2.
            Assert.AreEqual("{\"batch_index\":2,\"count\":2}", transport.Requests[2].Body);
            Assert.AreEqual("{\"batch_index\":2,\"count\":2}", transport.Requests[3].Body);
            Assert.AreEqual("b3", second[0].Id);
            Assert.IsFalse(round.HasMore);
        }

        [Test]
        public void FetchMore_ForAnotherBatch_Throws()
        {
            var transport = new FakeTransport();
            var round = StartRound(transport);
            transport.Reply(200, Batch(5, 0, "b1"));

            Assert.Throws<FormatException>(() => Run(round.FetchMoreAsync()));
        }

        [Test]
        public void Cancellation_StopsTheRequestWithoutARetry()
        {
            var transport = new FakeTransport();
            var round = StartRound(transport);
            transport.Fail("timeout");
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            Assert.Throws<OperationCanceledException>(() => Run(round.SubmitAnswerAsync("q1", 0, cancellation.Token)));
            Assert.AreEqual(1, transport.Requests.Count);
        }
    }
}
