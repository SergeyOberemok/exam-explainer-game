using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ExamCards.Exam.Api
{
    /// <summary>Posts JSON to the backend and parses the JSON reply, turning failures into readable exceptions.</summary>
    sealed class GameApiClient
    {
        const int MaxDetailLength = 300;

        readonly string _baseUrl;
        readonly int _timeoutSeconds;
        readonly IHttpTransport _transport;

        public GameApiClient(string baseUrl, int timeoutSeconds, IHttpTransport transport)
        {
            _baseUrl = baseUrl;
            _timeoutSeconds = timeoutSeconds;
            _transport = transport;
        }

        /// <exception cref="ApiException">No response, or a non-2xx status.</exception>
        /// <exception cref="FormatException">The reply is not the expected JSON.</exception>
        public async Task<TResponse> PostAsync<TResponse>(string path, object body, CancellationToken cancellationToken)
            where TResponse : class
        {
            const string method = "POST";
            var url = _baseUrl + path;
            var response = await _transport.SendAsync(method, url, JsonUtility.ToJson(body), _timeoutSeconds, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (!response.IsSuccess)
                throw new ApiException(method, url, response.StatusCode, response.TransportError ?? DetailOf(response.Body));

            TResponse parsed;
            try
            {
                parsed = string.IsNullOrWhiteSpace(response.Body) ? null : JsonUtility.FromJson<TResponse>(response.Body);
            }
            catch (ArgumentException e)
            {
                throw new FormatException($"{method} {url}: the response is not valid JSON.", e);
            }
            return parsed ?? throw new FormatException($"{method} {url}: the response is empty.");
        }

        /// <summary>FastAPI's <c>detail</c> message when it is a string, otherwise the start of the body.</summary>
        static string DetailOf(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return "no details";
            try
            {
                var detail = JsonUtility.FromJson<ApiErrorDto>(body)?.detail;
                if (!string.IsNullOrEmpty(detail))
                    return detail;
            }
            catch (ArgumentException)
            {
                // Not JSON, or detail is a list (validation errors): fall back to the raw body.
            }
            body = body.Trim();
            return body.Length <= MaxDetailLength ? body : body.Substring(0, MaxDetailLength) + "…";
        }
    }
}
