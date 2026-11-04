using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ExamCards.Exam.Api
{
    /// <summary>
    /// Starts rounds on the backend's <c>/game</c> routes. One <c>POST /game/rounds</c> brings the questions
    /// and their simplified versions, without the answer key; answers and 50/50 then go to that round.
    /// Search uses <c>GET /search/game-questions</c>, which also leaves the key out.
    /// With the default transport, call it from the main thread (see <see cref="UnityWebRequestTransport"/>).
    /// </summary>
    public sealed class ApiQuestionService : IQuestionService, IQuestionSearch
    {
        readonly GameApiClient _client;
        readonly int _questionCount;
        readonly int _searchLimit;

        /// <exception cref="ArgumentException">The base URL is not an absolute http(s) URL.</exception>
        public ApiQuestionService(string baseUrl, int questionCount, int timeoutSeconds, IHttpTransport transport = null,
            int searchLimit = ExamApiSettings.DefaultSearchLimit)
        {
            if (!ExamApiSettings.TryValidateBaseUrl(baseUrl, out var error))
                throw new ArgumentException(error, nameof(baseUrl));

            _questionCount = Mathf.Clamp(questionCount, 1, ExamApiSettings.MaxQuestionCount);
            _searchLimit = Mathf.Clamp(searchLimit, 1, ExamApiSettings.MaxSearchLimit);
            _client = new GameApiClient(baseUrl.Trim().TrimEnd('/'), Mathf.Max(1, timeoutSeconds),
                transport ?? new UnityWebRequestTransport());
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

        /// <exception cref="ApiException">The request failed.</exception>
        /// <exception cref="FormatException">The round is inconsistent.</exception>
        public async Task<IExamRound> StartRoundAsync(CancellationToken cancellationToken = default)
        {
            var request = new GameRoundRequestDto { count = _questionCount };
            var round = await _client.PostAsync<GameRoundDto>("/game/rounds", request, cancellationToken);
            var questions = GameQuestionMapper.BuildQuestions(round);
            return new ApiExamRound(_client, round.round_id, questions, round.fifty_fifty_charges);
        }

        /// <exception cref="ArgumentException">The text is too short.</exception>
        /// <exception cref="ApiException">The request failed.</exception>
        /// <exception cref="FormatException">The response is inconsistent.</exception>
        public async Task<IReadOnlyList<Question>> SearchAsync(string text, CancellationToken cancellationToken = default)
        {
            text = QuestionSearchText.Normalize(text)
                ?? throw new ArgumentException($"Search for at least {IQuestionSearch.MinTextLength} characters.", nameof(text));
            var path = "/search/game-questions?q=" + Uri.EscapeDataString(text) + "&limit=" + _searchLimit;
            var list = await _client.GetAsync<GameQuestionListDto>(path, cancellationToken);
            return GameQuestionMapper.ToSearchResults(list);
        }
    }
}
