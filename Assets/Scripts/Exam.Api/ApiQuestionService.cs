using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ExamCards.Exam.Api
{
    /// <summary>
    /// Starts rounds on the backend's <c>/v1/game</c> routes. One <c>POST /v1/game/rounds</c> brings the questions
    /// and their simplified versions, without the answer key; answers, 50/50 and later batches then go to that
    /// round. Search uses <c>GET /v1/search/game-questions</c>, which also leaves the key out.
    /// With the default transport, call it from the main thread (see <see cref="UnityWebRequestTransport"/>).
    /// </summary>
    public sealed class ApiQuestionService : IQuestionService, IQuestionSearch
    {
        /// <summary>The API version the game speaks; a breaking backend change moves to a new prefix.</summary>
        public const string Prefix = "/v1";

        /// <summary>Seconds before the one retry of a transient failure (no response, 5xx).</summary>
        public const float DefaultRetryDelaySeconds = 0.5f;

        readonly GameApiClient _client;
        readonly int _questionCount;
        readonly int _searchLimit;

        /// <exception cref="ArgumentException">The base URL is not an absolute http(s) URL.</exception>
        public ApiQuestionService(string baseUrl, int questionCount, int timeoutSeconds, IHttpTransport transport = null,
            int searchLimit = ExamApiSettings.DefaultSearchLimit, float retryDelaySeconds = DefaultRetryDelaySeconds)
        {
            if (!ExamApiSettings.TryValidateBaseUrl(baseUrl, out var error))
                throw new ArgumentException(error, nameof(baseUrl));

            _questionCount = Mathf.Clamp(questionCount, 1, ExamApiSettings.MaxQuestionCount);
            _searchLimit = Mathf.Clamp(searchLimit, 1, ExamApiSettings.MaxSearchLimit);
            _client = new GameApiClient(baseUrl.Trim().TrimEnd('/'), Mathf.Max(1, timeoutSeconds),
                transport ?? new UnityWebRequestTransport(), retryDelaySeconds);
        }

        public ApiQuestionService(ExamApiSettings settings, IHttpTransport transport = null)
            : this(settings.BaseUrl, settings.QuestionCount, settings.TimeoutSeconds, transport, settings.SearchLimit)
        {
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            var settings = ExamApiSettings.Load();
            QuestionServices.Factory = () => new ApiQuestionService(settings);
        }

        /// <exception cref="NoQuestionsException">No playable question matches.</exception>
        /// <exception cref="ApiException">The request failed.</exception>
        /// <exception cref="FormatException">The round is inconsistent.</exception>
        public async Task<IExamRound> StartRoundAsync(CancellationToken cancellationToken = default)
        {
            var request = new GameRoundRequestDto { count = _questionCount };
            GameRoundDto round;
            try
            {
                // Not idempotent: a retry would start a second round, so a failure goes to the player's Retry.
                round = await _client.PostAsync<GameRoundDto>(Prefix + "/game/rounds", request, false, cancellationToken);
            }
            catch (ApiException e) when (e.Code == ApiErrorCodes.NoQuestions)
            {
                throw new NoQuestionsException(e.Detail, e);
            }
            var questions = GameQuestionMapper.BuildQuestions(round);
            // `total` counts every playable match, so more than the round holds means later batches can follow.
            return new ApiExamRound(_client, round.round_id, questions, round.fifty_fifty_charges, _questionCount,
                hasMore: round.total > questions.Count);
        }

        /// <exception cref="ArgumentException">The text is too short.</exception>
        /// <exception cref="ApiException">The request failed.</exception>
        /// <exception cref="FormatException">The response is inconsistent.</exception>
        public async Task<IReadOnlyList<Question>> SearchAsync(string text, CancellationToken cancellationToken = default)
        {
            text = QuestionSearchText.Normalize(text)
                ?? throw new ArgumentException($"Search for at least {IQuestionSearch.MinTextLength} characters.", nameof(text));
            var path = Prefix + "/search/game-questions?q=" + Uri.EscapeDataString(text) + "&limit=" + _searchLimit;
            var list = await _client.GetAsync<GameQuestionListDto>(path, cancellationToken);
            return GameQuestionMapper.ToSearchResults(list);
        }
    }
}
