using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using static ExamCards.Exam.Tests.ExamSessionTests;

namespace ExamCards.Exam.Tests
{
    /// <summary>A round that goes on in batches, an answer recorded before its response was lost, and the 50/50 count.</summary>
    public class ExamSessionBatchTests
    {
        SynchronizationContext _savedContext;

        [SetUp]
        public void ClearSynchronizationContext()
        {
            _savedContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
        }

        [TearDown]
        public void RestoreSynchronizationContext() => SynchronizationContext.SetSynchronizationContext(_savedContext);

        // LocalExamRound completes every task at once, so this never blocks.
        static T Run<T>(Task<T> task) => task.GetAwaiter().GetResult();

        static KeyedQuestion[] Questions(int count) => Enumerable.Range(0, count).Select(_ => MakeQuestion(0)).ToArray();

        [Test]
        public void LoadMore_ContinuesTheRound_ThenFinishesWhenTheRoundHasNoMore()
        {
            var start = Questions(2);
            var more = Questions(3);
            var session = new ExamSession(new LocalExamRound(start, more: more, batchSize: 2));

            Run(session.AnswerAsync(0));
            Assert.IsFalse(session.NeedsMore);
            Assert.IsTrue(session.MoveNext());
            Run(session.AnswerAsync(0));

            // The loaded questions are used up, but the round has more.
            Assert.IsTrue(session.NeedsMore);
            Assert.IsFalse(session.IsLastQuestion);
            Assert.Throws<InvalidOperationException>(() => session.MoveNext());
            Assert.AreEqual(2, Run(session.LoadMoreAsync()));
            Assert.AreEqual(4, session.Total);
            Assert.IsTrue(session.MoveNext());
            Assert.AreEqual(more[0].Question.Id, session.Current.Id);

            Run(session.AnswerAsync(0));
            Assert.IsTrue(session.MoveNext());
            Run(session.AnswerAsync(0));
            Assert.AreEqual(1, Run(session.LoadMoreAsync()));
            Assert.IsTrue(session.MoveNext());
            Run(session.AnswerAsync(0));

            // The round has handed out everything: this is the last question.
            Assert.IsFalse(session.NeedsMore);
            Assert.IsTrue(session.IsLastQuestion);
            Assert.IsFalse(session.MoveNext());
            Assert.IsTrue(session.IsFinished);
            Assert.AreEqual(5, session.Score);
            Assert.AreEqual(5, session.AnsweredCount);
        }

        [Test]
        public void LoadMore_NeverBringsBackAQuestionAlreadyPlayed()
        {
            var start = Questions(1);
            var searched = MakeQuestion(0);
            var more = new[] { searched, MakeQuestion(0) };
            var session = new ExamSession(new LocalExamRound(start, catalog: new[] { searched }, more: more));

            Run(session.PlayNowAsync(searched.Question.Id));
            Run(session.AnswerAsync(0));
            Assert.IsTrue(session.MoveNext());
            Run(session.AnswerAsync(0));

            Assert.AreEqual(1, Run(session.LoadMoreAsync()));
            Assert.IsTrue(session.MoveNext());
            Assert.AreEqual(more[1].Question.Id, session.Current.Id);
        }

        [Test]
        public void LoadMore_EmptyBatch_MarksTheRoundExhausted()
        {
            var session = new ExamSession(new EmptyMoreRound(Questions(1)));
            Run(session.AnswerAsync(0));
            Assert.IsTrue(session.NeedsMore);

            Assert.AreEqual(0, Run(session.LoadMoreAsync()));

            Assert.IsFalse(session.NeedsMore);
            Assert.IsFalse(session.MoveNext());
            Assert.IsTrue(session.IsFinished);
        }

        [Test]
        public void OutOfLives_DoesNotLoadMore()
        {
            var session = new ExamSession(new LocalExamRound(Questions(3), more: Questions(5)));
            for (var i = 0; i < ExamSession.MaxLives; i++)
            {
                Run(session.AnswerAsync(1));
                if (i < ExamSession.MaxLives - 1)
                    Assert.IsTrue(session.MoveNext());
            }

            Assert.IsFalse(session.NeedsMore);
            Assert.IsTrue(session.IsLastQuestion);
            Assert.Throws<InvalidOperationException>(() => Run(session.LoadMoreAsync()));
            Assert.IsFalse(session.MoveNext());
        }

        [Test]
        public void Answer_WhenTheRoundRecordedAnEarlierPick_ThatPickCounts()
        {
            var question = MakeQuestion(2);
            var round = new LocalExamRound(new[] { question });
            var session = new ExamSession(round);
            // Card 2 reached the round, but its response was lost; the player then picks card 0.
            Run(round.SubmitAnswerAsync(question.Question.Id, 2));

            var result = Run(session.AnswerAsync(0));

            Assert.AreEqual(2, result.AnsweredIndex);
            Assert.IsTrue(result.IsCorrect);
            Assert.AreEqual(2, session.AnsweredIndex);
            Assert.AreEqual(1, session.Score);
            Assert.AreEqual(ExamSession.MaxLives, session.Lives);
        }

        [Test]
        public void Eliminate_TakesTheRoundsCount()
        {
            var session = new ExamSession(new LocalExamRound(Questions(2), new Random(4), fiftyFiftyCharges: 2));

            Assert.IsTrue(Run(session.EliminateAsync()));

            Assert.AreEqual(1, session.FiftyFiftyRemaining);
        }

        /// <summary>Claims to have more, then hands out nothing, like a pool used up by other means.</summary>
        sealed class EmptyMoreRound : IExamRound
        {
            readonly LocalExamRound _inner;

            public EmptyMoreRound(KeyedQuestion[] questions) => _inner = new LocalExamRound(questions);

            public System.Collections.Generic.IReadOnlyList<Question> Questions => _inner.Questions;
            public int FiftyFiftyCharges => _inner.FiftyFiftyCharges;
            public bool HasMore => true;

            public Task<AnswerReview> SubmitAnswerAsync(string questionId, int answerIndex,
                CancellationToken cancellationToken = default) =>
                _inner.SubmitAnswerAsync(questionId, answerIndex, cancellationToken);

            public Task<FiftyFiftyResult> EliminateAsync(string questionId, CancellationToken cancellationToken = default) =>
                _inner.EliminateAsync(questionId, cancellationToken);

            public Task<Question> AddQuestionAsync(string questionId, CancellationToken cancellationToken = default) =>
                _inner.AddQuestionAsync(questionId, cancellationToken);

            public Task<System.Collections.Generic.IReadOnlyList<Question>> FetchMoreAsync(
                CancellationToken cancellationToken = default) =>
                Task.FromResult<System.Collections.Generic.IReadOnlyList<Question>>(Array.Empty<Question>());
        }
    }
}
