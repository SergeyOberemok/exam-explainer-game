using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ExamCards.Exam
{
    /// <summary>
    /// A round that holds its own answer key, for the mock and tests. It follows the backend's round rules:
    /// one answer per question (asking again returns the recorded answer), and 50/50 once per unanswered
    /// question within the round's charges. Questions from <c>catalog</c> can be added to the round, and
    /// <c>more</c> supplies later batches, without anything the round already has.
    /// </summary>
    public sealed class LocalExamRound : IExamRound
    {
        readonly Dictionary<string, KeyedQuestion> _byId = new Dictionary<string, KeyedQuestion>();
        readonly Dictionary<string, int> _answers = new Dictionary<string, int>();
        readonly Dictionary<string, IReadOnlyList<int>> _eliminated = new Dictionary<string, IReadOnlyList<int>>();
        readonly Dictionary<string, KeyedQuestion> _catalog = new Dictionary<string, KeyedQuestion>();
        readonly List<KeyedQuestion> _more;
        readonly int _batchSize;
        readonly Random _random;
        int _chargesLeft;

        /// <param name="catalog">Questions that <see cref="AddQuestionAsync"/> may add; none if null.</param>
        /// <param name="more">Questions <see cref="FetchMoreAsync"/> hands out, in order; none if null.</param>
        /// <param name="batchSize">How many questions one <see cref="FetchMoreAsync"/> returns at most.</param>
        /// <exception cref="FormatException">A question or its review is inconsistent, or an id repeats.</exception>
        public LocalExamRound(IReadOnlyList<KeyedQuestion> questions, Random random = null,
            int fiftyFiftyCharges = ExamSession.DefaultFiftyFiftyCharges, IEnumerable<KeyedQuestion> catalog = null,
            IEnumerable<KeyedQuestion> more = null, int batchSize = 10)
        {
            if (questions == null)
                throw new ArgumentNullException(nameof(questions));
            if (fiftyFiftyCharges < 0)
                throw new ArgumentOutOfRangeException(nameof(fiftyFiftyCharges));
            if (batchSize < 1)
                throw new ArgumentOutOfRangeException(nameof(batchSize));

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

            _more = new List<KeyedQuestion>();
            foreach (var keyed in more ?? Enumerable.Empty<KeyedQuestion>())
            {
                QuestionValidator.ValidateQuestion(keyed?.Question);
                QuestionValidator.ValidateReview(keyed.Question, keyed.Review);
                _more.Add(keyed);
            }
            _batchSize = batchSize;

            Questions = questions.Select(keyed => keyed.Question).ToList();
            FiftyFiftyCharges = fiftyFiftyCharges;
            _chargesLeft = fiftyFiftyCharges;
            _random = random ?? new Random();
        }

        public IReadOnlyList<Question> Questions { get; }
        public int FiftyFiftyCharges { get; }
        public bool HasMore => _more.Any(keyed => !_byId.ContainsKey(keyed.Question.Id));

        public Task<AnswerReview> SubmitAnswerAsync(string questionId, int answerIndex, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var keyed = Find(questionId);
            if (_answers.TryGetValue(questionId, out var previous))
            {
                // Like the backend: the recorded answer counts, also when another one is sent later.
                return Task.FromResult(keyed.ReviewFor(previous));
            }
            if (answerIndex < 0 || answerIndex >= keyed.Question.Answers.Count)
                throw new ArgumentOutOfRangeException(nameof(answerIndex));
            if (_eliminated.TryGetValue(questionId, out var eliminated) && eliminated.Contains(answerIndex))
                throw new InvalidOperationException("That answer was eliminated by 50/50.");

            _answers[questionId] = answerIndex;
            return Task.FromResult(keyed.ReviewFor(answerIndex));
        }

        public Task<FiftyFiftyResult> EliminateAsync(string questionId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var keyed = Find(questionId);
            if (_eliminated.TryGetValue(questionId, out var already))
                return Task.FromResult(new FiftyFiftyResult(already, _chargesLeft));
            if (_answers.ContainsKey(questionId))
                throw new InvalidOperationException($"Question {questionId} has already been answered.");
            if (_chargesLeft <= 0)
                throw new InvalidOperationException("No 50/50 charges left.");

            var wrong = Enumerable.Range(0, keyed.Question.Answers.Count)
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
            return Task.FromResult(new FiftyFiftyResult(picked, _chargesLeft));
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

        public Task<IReadOnlyList<Question>> FetchMoreAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var batch = _more.Where(keyed => !_byId.ContainsKey(keyed.Question.Id)).Take(_batchSize).ToList();
            foreach (var keyed in batch)
                _byId.Add(keyed.Question.Id, keyed);
            return Task.FromResult<IReadOnlyList<Question>>(batch.Select(keyed => keyed.Question).ToList());
        }

        KeyedQuestion Find(string questionId) =>
            questionId != null && _byId.TryGetValue(questionId, out var keyed)
                ? keyed
                : throw new ArgumentException($"Question {questionId} is not in this round.", nameof(questionId));
    }
}
