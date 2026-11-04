using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ExamCards.Exam
{
    /// <summary>
    /// A round that holds its own answer key, for the mock and tests. It follows the backend's round rules:
    /// one answer per question (the same answer again repeats the result), and 50/50 once per unanswered
    /// question within the round's charges. Questions from <c>catalog</c> can be added to the round.
    /// </summary>
    public sealed class LocalExamRound : IExamRound
    {
        readonly Dictionary<string, KeyedQuestion> _byId = new Dictionary<string, KeyedQuestion>();
        readonly Dictionary<string, int> _answers = new Dictionary<string, int>();
        readonly Dictionary<string, IReadOnlyList<int>> _eliminated = new Dictionary<string, IReadOnlyList<int>>();
        readonly Dictionary<string, KeyedQuestion> _catalog = new Dictionary<string, KeyedQuestion>();
        readonly Random _random;
        int _chargesLeft;

        /// <param name="catalog">Questions that <see cref="AddQuestionAsync"/> may add; none if null.</param>
        /// <exception cref="FormatException">A question or its review is inconsistent, or an id repeats.</exception>
        public LocalExamRound(IReadOnlyList<KeyedQuestion> questions, Random random = null,
            int fiftyFiftyCharges = ExamSession.DefaultFiftyFiftyCharges, IEnumerable<KeyedQuestion> catalog = null)
        {
            if (questions == null)
                throw new ArgumentNullException(nameof(questions));
            if (fiftyFiftyCharges < 0)
                throw new ArgumentOutOfRangeException(nameof(fiftyFiftyCharges));

            foreach (var keyed in questions)
            {
                QuestionValidator.ValidateQuestion(keyed?.Question);
                QuestionValidator.ValidateReview(keyed.Question, keyed.Review);
                if (!_byId.TryAdd(keyed.Question.Id, keyed))
                    throw new FormatException($"Question {keyed.Question.Id}: listed more than once.");
            }

            foreach (var keyed in catalog ?? Enumerable.Empty<KeyedQuestion>())
            {
                QuestionValidator.ValidateQuestion(keyed?.Question);
                QuestionValidator.ValidateReview(keyed.Question, keyed.Review);
                _catalog[keyed.Question.Id] = keyed;
            }

            Questions = questions.Select(keyed => keyed.Question).ToList();
            FiftyFiftyCharges = fiftyFiftyCharges;
            _chargesLeft = fiftyFiftyCharges;
            _random = random ?? new Random();
        }

        public IReadOnlyList<Question> Questions { get; }
        public int FiftyFiftyCharges { get; }

        public Task<AnswerReview> SubmitAnswerAsync(string questionId, int answerIndex, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var keyed = Find(questionId);
            if (_answers.TryGetValue(questionId, out var previous))
            {
                if (previous == answerIndex)
                    return Task.FromResult(keyed.Review);
                throw new InvalidOperationException($"Question {questionId} has already been answered.");
            }
            if (answerIndex < 0 || answerIndex >= keyed.Question.Original.Answers.Count)
                throw new ArgumentOutOfRangeException(nameof(answerIndex));
            if (_eliminated.TryGetValue(questionId, out var eliminated) && eliminated.Contains(answerIndex))
                throw new InvalidOperationException("That answer was eliminated by 50/50.");

            _answers[questionId] = answerIndex;
            return Task.FromResult(keyed.Review);
        }

        public Task<IReadOnlyList<int>> EliminateAsync(string questionId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var keyed = Find(questionId);
            if (_eliminated.TryGetValue(questionId, out var already))
                return Task.FromResult(already);
            if (_answers.ContainsKey(questionId))
                throw new InvalidOperationException($"Question {questionId} has already been answered.");
            if (_chargesLeft <= 0)
                throw new InvalidOperationException("No 50/50 charges left.");

            var wrong = Enumerable.Range(0, keyed.Question.Original.Answers.Count)
                .Where(i => i != keyed.Review.CorrectIndex)
                .ToList();
            if (wrong.Count < ExamSession.EliminatedPerUse)
                throw new InvalidOperationException($"Question {questionId} has too few answers for 50/50.");

            var picked = new List<int>();
            for (var n = 0; n < ExamSession.EliminatedPerUse; n++)
            {
                var pick = _random.Next(wrong.Count);
                picked.Add(wrong[pick]);
                wrong.RemoveAt(pick);
            }
            picked.Sort();

            _eliminated[questionId] = picked;
            _chargesLeft--;
            return Task.FromResult<IReadOnlyList<int>>(picked);
        }

        public Task<Question> AddQuestionAsync(string questionId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (questionId != null && _byId.TryGetValue(questionId, out var existing))
                return Task.FromResult(existing.Question);
            if (questionId == null || !_catalog.TryGetValue(questionId, out var keyed))
                throw new ArgumentException($"Question {questionId} was not found.", nameof(questionId));

            _byId.Add(questionId, keyed);
            return Task.FromResult(keyed.Question);
        }

        KeyedQuestion Find(string questionId) =>
            questionId != null && _byId.TryGetValue(questionId, out var keyed)
                ? keyed
                : throw new ArgumentException($"Question {questionId} is not in this round.", nameof(questionId));
    }
}
