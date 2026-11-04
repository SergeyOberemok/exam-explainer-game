using System;

namespace ExamCards.Exam.Api
{
    /// <summary>A backend request failed. The message has the method, URL, status and the server's reason.</summary>
    public sealed class ApiException : Exception
    {
        public ApiException(string method, string url, long statusCode, string detail, string code = null)
            : base($"{method} {url} failed ({(statusCode > 0 ? $"HTTP {statusCode}" : "no response")}): {detail}")
        {
            Url = url;
            StatusCode = statusCode;
            Detail = detail;
            Code = code;
        }

        public string Url { get; }

        /// <summary>The HTTP status, or 0 when no response arrived.</summary>
        public long StatusCode { get; }

        /// <summary>The server's <c>detail</c> message, the start of its body, or the transport error.</summary>
        public string Detail { get; }

        /// <summary>The server's machine-readable reason (e.g. <c>already_answered</c>); null if it sent none.</summary>
        public string Code { get; }

        /// <summary>
        /// True for a failure that may pass on its own: no response, a server error, or a round that changed
        /// under the request. Repeating such a request is safe only when the request is idempotent.
        /// </summary>
        public bool IsTransient => StatusCode == 0 || StatusCode >= 500 || Code == ApiErrorCodes.RoundChanged;
    }

    /// <summary>The backend's error codes the game reacts to (see the backend's <c>api/game/errors.py</c>).</summary>
    public static class ApiErrorCodes
    {
        public const string AlreadyAnswered = "already_answered";
        public const string NoQuestions = "no_questions";
        public const string RoundChanged = "round_changed";
    }
}
