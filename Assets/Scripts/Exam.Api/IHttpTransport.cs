using System.Threading;
using System.Threading.Tasks;

namespace ExamCards.Exam.Api
{
    /// <summary>What came back from one HTTP request.</summary>
    public readonly struct HttpResponse
    {
        public HttpResponse(long statusCode, string body, string transportError = null)
        {
            StatusCode = statusCode;
            Body = body;
            TransportError = transportError;
        }

        /// <summary>The HTTP status, or 0 when no response arrived.</summary>
        public long StatusCode { get; }

        public string Body { get; }

        /// <summary>Why no usable response arrived (connection failed, timeout); null when one did.</summary>
        public string TransportError { get; }

        public bool IsSuccess => TransportError == null && StatusCode >= 200 && StatusCode < 300;
    }

    /// <summary>Sends JSON requests. <see cref="UnityWebRequestTransport"/> in the game, a fake in tests.</summary>
    public interface IHttpTransport
    {
        /// <param name="jsonBody">The request body, or null for none.</param>
        /// <exception cref="System.OperationCanceledException">The token was cancelled.</exception>
        Task<HttpResponse> SendAsync(string method, string url, string jsonBody, int timeoutSeconds,
            CancellationToken cancellationToken);
    }
}
