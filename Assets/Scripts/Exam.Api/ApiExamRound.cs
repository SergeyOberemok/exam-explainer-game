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

        public ApiExamRound(GameApiClient client, string roundId, IReadOnlyList<Question> questions, int fiftyFiftyCharges)
        {
            _client = client;
            _roundPath = "/game/rounds/" + Uri.EscapeDataString(roundId);
            Questions = questions;
            FiftyFiftyCharges = fiftyFiftyCharges;
        }

        public IReadOnlyList<Question> Questions { get; }
        public int FiftyFiftyCharges { get; }

        public async Task<AnswerReview> SubmitAnswerAsync(string questionId, int answerIndex,
            CancellationToken cancellationToken = default)
        {
            var request = new GameAnswerRequestDto { question_id = questionId, answer_index = answerIndex };
            var result = await _client.PostAsync<GameAnswerResultDto>(_roundPath + "/answers", request, cancellationToken);
            return GameQuestionMapper.ToReview(result, questionId, answerIndex);
        }

        public async Task<IReadOnlyList<int>> EliminateAsync(string questionId, CancellationToken cancellationToken = default)
        {
            var request = new GameFiftyFiftyRequestDto { question_id = questionId };
            var result = await _client.PostAsync<GameFiftyFiftyResultDto>(_roundPath + "/fifty-fifty", request, cancellationToken);
            return GameQuestionMapper.ToEliminated(result, questionId);
        }

        public async Task<Question> AddQuestionAsync(string questionId, CancellationToken cancellationToken = default)
        {
            var request = new GameRoundQuestionRequestDto { question_id = questionId };
            var result = await _client.PostAsync<GameRoundQuestionDto>(_roundPath + "/questions", request, cancellationToken);
            return GameQuestionMapper.ToAddedQuestion(result, questionId);
        }
    }
}
