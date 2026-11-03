using System;

namespace ExamCards.Exam.Api
{
    /// <summary>A backend request failed. The message has the method, URL, status and the server's reason.</summary>
    public sealed class ApiException : Exception
    {
        public ApiException(string method, string url, long statusCode, string detail)
            : base($"{method} {url} failed ({(statusCode > 0 ? $"HTTP {statusCode}" : "no response")}): {detail}")
        {
            Url = url;
            StatusCode = statusCode;
            Detail = detail;
        }

        public string Url { get; }

        /// <summary>The HTTP status, or 0 when no response arrived.</summary>
        public long StatusCode { get; }

        /// <summary>The server's <c>detail</c> message, the start of its body, or the transport error.</summary>
        public string Detail { get; }
    }
}
