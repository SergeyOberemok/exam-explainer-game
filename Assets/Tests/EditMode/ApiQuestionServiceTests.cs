using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ExamCards.Exam.Api;
using NUnit.Framework;

namespace ExamCards.Exam.Tests
{
    public class ApiQuestionServiceTests
    {
        const string BaseUrl = "http://api.test/";

        // The fake transport completes immediately, so these never block.
        static T Run<T>(Task<T> task) => task.GetAwaiter().GetResult();

        static ApiQuestionService MakeService(FakeTransport transport, int count = 10) =>
            new ApiQuestionService(BaseUrl, count, timeoutSeconds: 7, transport);

        [Test]
        public void StartRound_PostsTheCount_AndBuildsTheRound()
        {
            var transport = new FakeTransport().Reply(201, GameQuestionMapperTests.RoundJson);

            var round = Run(MakeService(transport, count: 3).StartRoundAsync());

            var request = transport.Requests[0];
            Assert.AreEqual("POST", request.Method);
            Assert.AreEqual("http://api.test/game/rounds", request.Url);
            Assert.AreEqual("{\"count\":3}", request.Body);
            Assert.AreEqual(7, request.TimeoutSeconds);
            Assert.AreEqual(2, round.Questions.Count);
            Assert.AreEqual(2, round.FiftyFiftyCharges);
            Assert.IsTrue(round.Questions[0].CanReveal);
        }

        [Test]
        public void StartRound_ClampsTheCount()
        {
            var transport = new FakeTransport().Reply(201, GameQuestionMapperTests.RoundJson);

            Run(MakeService(transport, count: 500).StartRoundAsync());

            Assert.AreEqual($"{{\"count\":{ExamApiSettings.MaxQuestionCount}}}", transport.Requests[0].Body);
        }

        [Test]
        public void SubmitAnswer_PostsToTheRound_AndReturnsTheReview()
        {
            var transport = new FakeTransport()
                .Reply(201, GameQuestionMapperTests.RoundJson.Replace("\"r1\"", "\"r 1/x\""))
                .Reply(200, GameQuestionMapperTests.AnswerJson);
            var round = Run(MakeService(transport).StartRoundAsync());

            var review = Run(round.SubmitAnswerAsync("q1", 0));

            var request = transport.Requests[1];
            // The round id is escaped as a path segment: a space is %20, not +.
            Assert.AreEqual("http://api.test/game/rounds/r%201%2Fx/answers", request.Url);
            Assert.AreEqual("{\"question_id\":\"q1\",\"answer_index\":0}", request.Body);
            Assert.AreEqual(1, review.CorrectIndex);
            Assert.AreEqual("Because.", review.Original.Explanation);
        }

        [Test]
        public void Eliminate_PostsToTheRound_AndReturnsTheIndices()
        {
            var transport = new FakeTransport()
                .Reply(201, GameQuestionMapperTests.RoundJson)
                .Reply(200, "{\"question_id\":\"q1\",\"eliminated_indices\":[0,3],\"fifty_fifty_remaining\":1}");
            var round = Run(MakeService(transport).StartRoundAsync());

            var eliminated = Run(round.EliminateAsync("q1"));

            Assert.AreEqual("http://api.test/game/rounds/r1/fifty-fifty", transport.Requests[1].Url);
            Assert.AreEqual("{\"question_id\":\"q1\"}", transport.Requests[1].Body);
            CollectionAssert.AreEqual(new[] { 0, 3 }, eliminated);
        }

        [Test]
        public void HttpError_ThrowsWithStatusAndTheServersDetail()
        {
            var transport = new FakeTransport()
                .Reply(201, GameQuestionMapperTests.RoundJson)
                .Reply(409, "{\"detail\":\"question already answered\"}");
            var round = Run(MakeService(transport).StartRoundAsync());

            var e = Assert.Throws<ApiException>(() => Run(round.SubmitAnswerAsync("q1", 0)));

            Assert.AreEqual(409, e.StatusCode);
            Assert.AreEqual("question already answered", e.Detail);
            StringAssert.Contains("http://api.test/game/rounds/r1/answers", e.Message);
            StringAssert.Contains("HTTP 409", e.Message);
        }

        [Test]
        public void HttpError_WithAListDetail_FallsBackToTheBody()
        {
            const string body = "{\"detail\":[{\"loc\":[\"body\",\"count\"],\"msg\":\"too big\"}]}";
            var transport = new FakeTransport().Reply(422, body);

            var e = Assert.Throws<ApiException>(() => Run(MakeService(transport).StartRoundAsync()));

            StringAssert.Contains("too big", e.Detail);
        }

        [Test]
        public void NoResponse_ThrowsWithTheTransportError()
        {
            var transport = new FakeTransport().Fail("Cannot connect to destination host");

            var e = Assert.Throws<ApiException>(() => Run(MakeService(transport).StartRoundAsync()));

            Assert.AreEqual(0, e.StatusCode);
            StringAssert.Contains("no response", e.Message);
            StringAssert.Contains("Cannot connect", e.Message);
        }

        [TestCase("")]
        [TestCase("not json")]
        public void BadBody_ThrowsFormatException(string body)
        {
            var transport = new FakeTransport().Reply(201, body);

            Assert.Throws<FormatException>(() => Run(MakeService(transport).StartRoundAsync()));
        }

        [Test]
        public void Cancellation_ThrowsOperationCanceled()
        {
            var transport = new FakeTransport().Reply(201, GameQuestionMapperTests.RoundJson);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            Assert.Throws<OperationCanceledException>(() => Run(MakeService(transport).StartRoundAsync(cancellation.Token)));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("127.0.0.1:8000")]
        [TestCase("ftp://example.com")]
        [TestCase("https://example.com/?x=1")]
        public void InvalidBaseUrl_IsRejected(string baseUrl)
        {
            Assert.IsFalse(ExamApiSettings.TryValidateBaseUrl(baseUrl, out var error));
            Assert.IsNotEmpty(error);
            Assert.Throws<ArgumentException>(() => new ApiQuestionService(baseUrl, 10, 15, new FakeTransport()));
        }

        [TestCase("http://127.0.0.1:8000")]
        [TestCase("https://exam.example.com/api/")]
        public void ValidBaseUrl_IsAccepted(string baseUrl)
        {
            Assert.IsTrue(ExamApiSettings.TryValidateBaseUrl(baseUrl, out var error), error);
        }

        sealed class FakeTransport : IHttpTransport
        {
            readonly Queue<HttpResponse> _responses = new Queue<HttpResponse>();

            public readonly List<(string Method, string Url, string Body, int TimeoutSeconds)> Requests =
                new List<(string, string, string, int)>();

            public FakeTransport Reply(long status, string body)
            {
                _responses.Enqueue(new HttpResponse(status, body));
                return this;
            }

            public FakeTransport Fail(string error)
            {
                _responses.Enqueue(new HttpResponse(0, null, error));
                return this;
            }

            public Task<HttpResponse> SendAsync(string method, string url, string jsonBody, int timeoutSeconds,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Requests.Add((method, url, jsonBody, timeoutSeconds));
                return Task.FromResult(_responses.Dequeue());
            }
        }
    }
}
