using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ExamCards.Exam.Api
{
    /// <summary>Sends requests to the backend and parses the JSON reply, turning failures into readable exceptions.</summary>
    sealed class GameApiClient
    {
        const int MaxDetailLength = 300;

        readonly string _baseUrl;
        readonly int _timeoutSeconds;
        readonly IHttpTransport _transport;
        readonly float _retryDelaySeconds;

        /// <param name="retryDelaySeconds">Wait before the one retry of a transient failure; 0 retries at once.</param>
        public GameApiClient(string baseUrl, int timeoutSeconds, IHttpTransport transport, float retryDelaySeconds)
        {
            _baseUrl = baseUrl;
            _timeoutSeconds = timeoutSeconds;
            _transport = transport;
            _retryDelaySeconds = Mathf.Max(0f, retryDelaySeconds);
        }

        /// <param name="idempotent">
        /// True if sending the request twice has the same effect as once (every round request except starting a
        /// round): then a transient failure is retried once.
        /// </param>
        /// <exception cref="ApiException">No response, or a non-2xx status.</exception>
        /// <exception cref="FormatException">The reply is not the expected JSON.</exception>
        public Task<TResponse> PostAsync<TResponse>(string path, object body, bool idempotent,
            CancellationToken cancellationToken)
            where TResponse : class =>
            SendAsync<TResponse>("POST", path, JsonUtility.ToJson(body), idempotent, cancellationToken);

        /// <param name="pathAndQuery">The path with its query string, already escaped.</param>
        /// <exception cref="ApiException">No response, or a non-2xx status.</exception>
        /// <exception cref="FormatException">The reply is not the expected JSON.</exception>
        public Task<TResponse> GetAsync<TResponse>(string pathAndQuery, CancellationToken cancellationToken)
            where TResponse : class =>
            SendAsync<TResponse>("GET", pathAndQuery, null, true, cancellationToken);

        async Task<TResponse> SendAsync<TResponse>(string method, string path, string jsonBody, bool retry,
            CancellationToken cancellationToken)
            where TResponse : class
        {
            try
            {
                return await SendOnceAsync<TResponse>(method, path, jsonBody, cancellationToken);
            }
            catch (ApiException e) when (retry && e.IsTransient)
            {
                // Awaitable rather than Task.Delay: it runs on Unity's player loop, also on WebGL (no threads there).
                if (_retryDelaySeconds > 0f)
                    await Awaitable.WaitForSecondsAsync(_retryDelaySeconds, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                return await SendOnceAsync<TResponse>(method, path, jsonBody, cancellationToken);
            }
        }

        async Task<TResponse> SendOnceAsync<TResponse>(string method, string path, string jsonBody,
            CancellationToken cancellationToken)
            where TResponse : class
        {
            var url = _baseUrl + path;
            var response = await _transport.SendAsync(method, url, jsonBody, _timeoutSeconds, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (!response.IsSuccess)
            {
                if (response.TransportError != null)
                    throw new ApiException(method, url, response.StatusCode, response.TransportError);
                var (detail, code) = ErrorOf(response.Body);
                throw new ApiException(method, url, response.StatusCode, detail, code);
            }

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

        /// <summary>The backend's <c>detail</c> and <c>code</c>; for another body, its start and no code.</summary>
        static (string Detail, string Code) ErrorOf(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return ("no details", null);
            try
            {
                var error = JsonUtility.FromJson<ApiErrorDto>(body);
                if (!string.IsNullOrEmpty(error?.detail))
                    return (error.detail, string.IsNullOrEmpty(error.code) ? null : error.code);
            }
            catch (ArgumentException)
            {
                // Not JSON, or detail is a list (an older backend's validation errors): fall back to the raw body.
            }
            body = body.Trim();
            return (body.Length <= MaxDetailLength ? body : body.Substring(0, MaxDetailLength) + "…", null);
        }
    }
}
