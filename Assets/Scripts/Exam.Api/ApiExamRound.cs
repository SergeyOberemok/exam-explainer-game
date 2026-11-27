using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ExamCards.Exam.Api
{
    /// <summary>A round held by the backend: answers and 50/50 are decided there, since only it has the key.</summary>
    sealed class ApiExamRound : IExamRound
    {
        readonly GameApiClient _client;
        readonly string _roundPath;
        readonly int _batchSize;

        // Batch 0 is the start. A failed request leaves the index alone, so a retry asks for the same batch again
        // and the backend returns it, rather than handing out (and so skipping) another one.
        int _nextBatch = 1;

        public ApiExamRound(GameApiClient client, string roundId, IReadOnlyList<Question> questions,
            int fiftyFiftyCharges, int batchSize, bool hasMore)
        {
            _client = client;
            _roundPath = ApiQuestionService.Prefix + "/game/rounds/" + Uri.EscapeDataString(roundId);
            Questions = questions;
            FiftyFiftyCharges = fiftyFiftyCharges;
            _batchSize = batchSize;
            HasMore = hasMore;
        }

        public IReadOnlyList<Question> Questions { get; }
        public int FiftyFiftyCharges { get; }
        public bool HasMore { get; private set; }

        public async Task<AnswerReview> SubmitAnswerAsync(string questionId, int answerIndex,
            CancellationToken cancellationToken = default)
        {
            var request = new GameAnswerRequestDto { question_id = questionId, answer_index = answerIndex };
            GameCardAnswerResultDto result;
            try
            {
                result = await _client.PostAsync<GameCardAnswerResultDto>(_roundPath + "/answers", request, true,
                    cancellationToken);
            }
            catch (ApiException e) when (e.Code == ApiErrorCodes.AlreadyAnswered)
            {
                // An earlier answer reached the server but its response was lost: that answer counts. Read it, so the
                // question shows its result instead of refusing every other card.
                var recorded = await _client.GetAsync<GameCardAnswerResultDto>(
                    _roundPath + "/answers/" + Uri.EscapeDataString(questionId), cancellationToken);
                return GameQuestionMapper.ToReview(recorded, questionId, null);
            }
            return GameQuestionMapper.ToReview(result, questionId, answerIndex);
        }

        public async Task<FiftyFiftyResult> EliminateAsync(string questionId, CancellationToken cancellationToken = default)
        {
            var request = new GameFiftyFiftyRequestDto { question_id = questionId };
            var result = await _client.PostAsync<GameFiftyFiftyResultDto>(_roundPath + "/fifty-fifty", request, true,
                cancellationToken);
            return GameQuestionMapper.ToFiftyFifty(result, questionId);
        }

        public async Task<Question> AddQuestionAsync(string questionId, CancellationToken cancellationToken = default)
        {
            var request = new GameRoundQuestionRequestDto { question_id = questionId };
            var result = await _client.PostAsync<GameCardRoundQuestionDto>(_roundPath + "/questions", request, true,
                cancellationToken);
            return GameQuestionMapper.ToAddedQuestion(result, questionId);
        }

        public async Task<IReadOnlyList<Question>> FetchMoreAsync(CancellationToken cancellationToken = default)
        {
            var batchIndex = _nextBatch;
            var request = new GameRoundBatchRequestDto { batch_index = batchIndex, count = _batchSize };
            var result = await _client.PostAsync<GameCardBatchDto>(_roundPath + "/batches", request, true,
                cancellationToken);
            var questions = GameQuestionMapper.BuildBatch(result, batchIndex);
            // An empty batch isn't stored on the server, so only a full one moves to the next index.
            if (questions.Count > 0)
                _nextBatch++;
            HasMore = questions.Count > 0 && result.remaining > 0;
            return questions;
        }
    }
}
