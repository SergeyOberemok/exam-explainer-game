using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace ExamCards.Exam.Api
{
    /// <summary>
    /// <see cref="IHttpTransport"/> over UnityWebRequest. UnityWebRequest is main-thread only: call
    /// <see cref="SendAsync"/> from the main thread and cancel its token from the main thread, because
    /// cancelling aborts the request. Its awaits resume there through Unity's synchronization context.
    /// </summary>
    public sealed class UnityWebRequestTransport : IHttpTransport
    {
        public async Task<HttpResponse> SendAsync(string method, string url, string jsonBody, int timeoutSeconds,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var request = new UnityWebRequest(url, method) { downloadHandler = new DownloadHandlerBuffer() };
            if (jsonBody != null)
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
                request.SetRequestHeader("Content-Type", "application/json");
            }
            request.timeout = timeoutSeconds;
            request.SetRequestHeader("Accept", "application/json");

            using (cancellationToken.Register(request.Abort))
                await Awaitable.FromAsyncOperation(request.SendWebRequest());

            cancellationToken.ThrowIfCancellationRequested();

            // A ProtocolError still has a status and body worth reporting; these two have no usable response.
            var noResponse = request.result == UnityWebRequest.Result.ConnectionError
                || request.result == UnityWebRequest.Result.DataProcessingError;
            return new HttpResponse(
                request.responseCode,
                request.downloadHandler?.text,
                noResponse ? request.error ?? request.result.ToString() : null);
        }
    }
}
