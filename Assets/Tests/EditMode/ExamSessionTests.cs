using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace ExamCards.Exam.Tests
{
    public class ExamSessionTests
    {
        internal static QuestionPrompt MakePrompt(string text) => new QuestionPrompt
        {
            Question = text,
            Answers = new[] { "a", "b", "c", "d" },
        };

        internal static PromptReview MakeReview(string text) => new PromptReview
        {
            Explanation = text + " explanation",
            Feedbacks = new[] { "fa", "fb", "fc", "fd" },
        };

        internal static KeyedQuestion MakeQuestion(int correctIndex, bool withSimplified = true) => new KeyedQuestion
        {
            Question = new Question
            {
                Id = Guid.NewGuid().ToString(),
                Original = MakePrompt("original"),
                Simplified = withSimplified ? MakePrompt("simplified") : null,
            },
            Review = new AnswerReview
            {
                CorrectIndex = correctIndex,
                Original = MakeReview("original"),
                Simplified = withSimplified ? MakeReview("simplified") : null,
            },
        };

        static ExamSession MakeSession(params KeyedQuestion[] questions) => new ExamSession(new LocalExamRound(questions));

        static ExamSession MakeSession(Random random, int charges, params KeyedQuestion[] questions) =>
            new ExamSession(new LocalExamRound(questions, random, charges));

        SynchronizationContext _savedContext;

        // Without Unity's context, a continuation runs inline when PendingRound completes its task, instead of
        // being queued to the main thread that Run is blocking.
        [SetUp]
        public void ClearSynchronizationContext()
        {
            _savedContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
        }

        [TearDown]
        public void RestoreSynchronizationContext() => SynchronizationContext.SetSynchronizationContext(_savedContext);

        // Every task here has completed by the time it is run, so this never blocks.
        static T Run<T>(Task<T> task) => task.GetAwaiter().GetResult();

        static AnswerResult Answer(ExamSession session, int index) => Run(session.AnswerAsync(index));
        static bool Eliminate(ExamSession session) => Run(session.EliminateAsync());

        [Test]
        public void Answer_Correct_IncrementsScore()
        {
            var session = MakeSession(MakeQuestion(2));

            var result = Answer(session, 2);

            Assert.IsTrue(result.IsCorrect);
            Assert.AreEqual(2, result.CorrectIndex);
            Assert.AreEqual(1, session.Score);
            Assert.AreEqual(ExamSession.MaxLives, session.Lives);
        }

        [Test]
        public void Answer_Wrong_CostsALifeAndReportsCorrectIndex()
        {
            var session = MakeSession(MakeQuestion(2));

            var result = Answer(session, 0);

            Assert.IsFalse(result.IsCorrect);
            Assert.AreEqual(2, result.CorrectIndex);
            Assert.AreEqual(0, session.Score);
            Assert.AreEqual(ExamSession.MaxLives - 1, session.Lives);
        }

        [Test]
        public void Answer_SetsReview_AndReviewFollowsReveal()
        {
            var question = MakeQuestion(1);
            var session = MakeSession(question);
            Assert.IsNull(session.CurrentReview);
            Assert.IsNull(session.CurrentPromptReview);

            Answer(session, 1);
            Assert.AreSame(question.Review, session.CurrentReview);
            Assert.AreSame(question.Review.Original, session.CurrentPromptReview);

            session.Reveal();
            Assert.AreSame(question.Review.Simplified, session.CurrentPromptReview);
        }

        [Test]
        public void Answer_Twice_Throws()
        {
            var session = MakeSession(MakeQuestion(0));
            Answer(session, 0);

            Assert.Throws<InvalidOperationException>(() => Answer(session, 1));
        }

        [TestCase(-1)]
        [TestCase(4)]
        public void Answer_OutOfRange_Throws(int index)
        {
            var session = MakeSession(MakeQuestion(0));

            Assert.Throws<ArgumentOutOfRangeException>(() => Answer(session, index));
        }

        [Test]
        public void Answer_WhileARequestRuns_Throws()
        {
            var round = new PendingRound(MakeQuestion(0));
            var session = new ExamSession(round);

            var first = session.AnswerAsync(0);
            Assert.IsTrue(session.IsBusy);
            Assert.IsFalse(session.CanEliminate);
            Assert.Throws<InvalidOperationException>(() => Run(session.AnswerAsync(1)));

            round.Complete();
            Run(first);
            Assert.IsFalse(session.IsBusy);
            Assert.AreEqual(0, session.AnsweredIndex);
        }

        [Test]
        public void Answer_WhenTheRoundFails_ChangesNothingAndCanBeRetried()
        {
            var round = new PendingRound(MakeQuestion(0));
            var session = new ExamSession(round);

            var failed = session.AnswerAsync(0);
            round.Fail(new InvalidOperationException("offline"));
            Assert.Throws<InvalidOperationException>(() => Run(failed));
            Assert.IsFalse(session.IsBusy);
            Assert.IsFalse(session.IsCurrentAnswered);
            Assert.AreEqual(ExamSession.MaxLives, session.Lives);

            var retry = session.AnswerAsync(0);
            round.Complete();
            Assert.IsTrue(Run(retry).IsCorrect);
        }

        [Test]
        public void Answer_ReviewThatDoesNotFit_ThrowsFormatException()
        {
            var question = MakeQuestion(0);
            var round = new PendingRound(question);
            var session = new ExamSession(round);

            var pending = session.AnswerAsync(0);
            round.Complete(new AnswerReview { CorrectIndex = 9, Original = MakeReview("x") });

            Assert.Throws<FormatException>(() => Run(pending));
            Assert.IsFalse(session.IsCurrentAnswered);
        }

        [Test]
        public void MoveNext_WalksAllQuestionsThenStops()
        {
            var session = MakeSession(MakeQuestion(0), MakeQuestion(1));

            Assert.AreEqual(0, session.CurrentIndex);
            Answer(session, 0);
            Assert.IsTrue(session.MoveNext());
            Assert.AreEqual(1, session.CurrentIndex);
            Assert.IsFalse(session.IsCurrentAnswered);
            Assert.IsNull(session.CurrentReview);
            Answer(session, 1);
            Assert.IsFalse(session.MoveNext());
            Assert.AreEqual(1, session.CurrentIndex);
            Assert.AreEqual(2, session.Score);
        }

        [Test]
        public void MoveNext_Unanswered_Throws()
        {
            var session = MakeSession(MakeQuestion(0), MakeQuestion(1));

            Assert.Throws<InvalidOperationException>(() => session.MoveNext());
            Assert.AreEqual(0, session.CurrentIndex);
        }

        [Test]
        public void MoveNext_PastLastQuestion_FinishesExam()
        {
            var session = MakeSession(MakeQuestion(0));
            Answer(session, 0);
            Assert.IsFalse(session.IsFinished);

            Assert.IsFalse(session.MoveNext());

            Assert.IsTrue(session.IsFinished);
        }

        [Test]
        public void MoveNext_OutOfLives_FinishesExam()
        {
            var session = MakeSession(Enumerable.Range(0, ExamSession.MaxLives + 1).Select(_ => MakeQuestion(0)).ToArray());
            for (var i = 0; i < ExamSession.MaxLives - 1; i++)
            {
                Answer(session, 1);
                Assert.IsTrue(session.MoveNext());
            }

            Answer(session, 1);

            Assert.AreEqual(0, session.Lives);
            Assert.IsFalse(session.MoveNext());
            Assert.IsTrue(session.IsFinished);
        }

        [Test]
        public void AnsweredIndex_TracksPickAndClearsOnNextQuestion()
        {
            var session = MakeSession(MakeQuestion(0), MakeQuestion(1));
            Assert.IsNull(session.AnsweredIndex);

            Answer(session, 3);
            Assert.AreEqual(3, session.AnsweredIndex);

            session.MoveNext();
            Assert.IsNull(session.AnsweredIndex);
        }

        [Test]
        public void AnsweredCount_CountsTheCurrentQuestionOnceAnswered()
        {
            var session = MakeSession(MakeQuestion(0), MakeQuestion(1));
            Assert.AreEqual(0, session.AnsweredCount);

            Answer(session, 0);
            Assert.AreEqual(1, session.AnsweredCount);

            session.MoveNext();
            Assert.AreEqual(1, session.AnsweredCount);

            Answer(session, 1);
            Assert.AreEqual(2, session.AnsweredCount);
        }

        [Test]
        public void Reveal_AfterFinished_ReturnsFalse()
        {
            var session = MakeSession(MakeQuestion(0));
            Answer(session, 0);
            session.MoveNext();

            Assert.IsFalse(session.Reveal());
        }

        [Test]
        public void Constructor_EmptyRound_Throws()
        {
            Assert.Throws<ArgumentException>(() => MakeSession());
        }

        [Test]
        public void CurrentPrompt_BeforeReveal_IsOriginal()
        {
            var question = MakeQuestion(0);
            var session = MakeSession(question);

            Assert.IsFalse(session.IsCurrentRevealed);
            Assert.AreSame(question.Question.Original, session.CurrentPrompt);
        }

        [Test]
        public void Reveal_SwapsCurrentPromptToSimplified()
        {
            var question = MakeQuestion(0);
            var session = MakeSession(question);

            Assert.IsTrue(session.Reveal());

            Assert.IsTrue(session.IsCurrentRevealed);
            Assert.AreSame(question.Question.Simplified, session.CurrentPrompt);
        }

        [Test]
        public void Reveal_Twice_ReturnsFalseAndStaysRevealed()
        {
            var question = MakeQuestion(0);
            var session = MakeSession(question);
            session.Reveal();

            Assert.IsFalse(session.Reveal());
            Assert.AreSame(question.Question.Simplified, session.CurrentPrompt);
        }

        [Test]
        public void Reveal_AfterAnswering_WorksAndKeepsScore()
        {
            var question = MakeQuestion(1);
            var session = MakeSession(question);
            Answer(session, 1);

            Assert.IsTrue(session.Reveal());

            Assert.AreSame(question.Question.Simplified, session.CurrentPrompt);
            Assert.IsTrue(session.IsCurrentAnswered);
            Assert.AreEqual(1, session.Score);
        }

        [Test]
        public void Reveal_BeforeAnswering_DoesNotChangeScoring()
        {
            var session = MakeSession(MakeQuestion(1));
            session.Reveal();

            var result = Answer(session, 1);

            Assert.IsTrue(result.IsCorrect);
            Assert.AreEqual(1, session.Score);
        }

        [Test]
        public void MoveNext_ResetsReveal()
        {
            var second = MakeQuestion(0);
            var session = MakeSession(MakeQuestion(0), second);
            session.Reveal();
            Answer(session, 0);

            session.MoveNext();

            Assert.IsFalse(session.IsCurrentRevealed);
            Assert.AreSame(second.Question.Original, session.CurrentPrompt);
        }

        [Test]
        public void Reveal_WithoutSimplifiedVersion_DoesNothing()
        {
            var question = MakeQuestion(0, withSimplified: false);
            var session = MakeSession(question);

            Assert.IsFalse(question.Question.CanReveal);
            Assert.IsFalse(session.Reveal());
            Assert.IsFalse(session.IsCurrentRevealed);
            Assert.AreSame(question.Question.Original, session.CurrentPrompt);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void Eliminate_RemovesTwoWrongAnswersAndKeepsTheCorrectOne(int correctIndex)
        {
            for (var seed = 0; seed < 20; seed++)
            {
                var session = MakeSession(new Random(seed), 2, MakeQuestion(correctIndex));

                Assert.IsTrue(Eliminate(session));

                var eliminated = 0;
                for (var i = 0; i < 4; i++)
                    eliminated += session.IsEliminated(i) ? 1 : 0;
                Assert.AreEqual(ExamSession.EliminatedPerUse, eliminated);
                Assert.IsFalse(session.IsEliminated(correctIndex));
            }
        }

        [Test]
        public void Eliminate_UsesACharge_AndWorksOncePerQuestion()
        {
            var session = MakeSession(MakeQuestion(0), MakeQuestion(0));
            Assert.AreEqual(2, session.FiftyFiftyRemaining);

            Assert.IsTrue(Eliminate(session));
            Assert.AreEqual(1, session.FiftyFiftyRemaining);
            Assert.IsFalse(Eliminate(session));
            Assert.AreEqual(1, session.FiftyFiftyRemaining);
        }

        [Test]
        public void Eliminate_StartsWithTheRoundsCharges_AndRunsOut()
        {
            var session = MakeSession(null, 1, MakeQuestion(0), MakeQuestion(0));
            Assert.AreEqual(1, session.FiftyFiftyRemaining);
            Eliminate(session);
            Answer(session, 0);
            session.MoveNext();

            Assert.IsFalse(session.CanEliminate);
            Assert.IsFalse(Eliminate(session));
        }

        [Test]
        public void Eliminate_AfterAnswering_ReturnsFalse()
        {
            var session = MakeSession(MakeQuestion(0));
            Answer(session, 0);

            Assert.IsFalse(Eliminate(session));
            Assert.AreEqual(2, session.FiftyFiftyRemaining);
        }

        [Test]
        public void Eliminate_InvalidIndicesFromTheRound_ThrowsAndKeepsTheCharge()
        {
            var round = new PendingRound(MakeQuestion(0));
            var session = new ExamSession(round);

            var pending = session.EliminateAsync();
            round.CompleteElimination(new[] { 1, 1 });

            Assert.Throws<FormatException>(() => Run(pending));
            Assert.IsFalse(session.IsCurrentEliminated);
            Assert.AreEqual(2, session.FiftyFiftyRemaining);
        }

        [Test]
        public void Answer_EliminatedOption_Throws()
        {
            var session = MakeSession(new Random(1), 2, MakeQuestion(0));
            Eliminate(session);
            var eliminated = Enumerable.Range(0, 4).First(session.IsEliminated);

            Assert.Throws<InvalidOperationException>(() => Answer(session, eliminated));
        }

        [Test]
        public void MoveNext_ClearsEliminatedAnswers()
        {
            var session = MakeSession(MakeQuestion(0), MakeQuestion(0));
            Eliminate(session);
            Answer(session, 0);

            session.MoveNext();

            Assert.IsFalse(session.IsCurrentEliminated);
            for (var i = 0; i < 4; i++)
                Assert.IsFalse(session.IsEliminated(i));
        }

        /// <summary>A round whose requests finish only when the test says so, like a slow server.</summary>
        sealed class PendingRound : IExamRound
        {
            readonly KeyedQuestion _question;
            TaskCompletionSource<AnswerReview> _answer;
            TaskCompletionSource<IReadOnlyList<int>> _elimination;

            public PendingRound(KeyedQuestion question)
            {
                _question = question;
                Questions = new[] { question.Question };
            }

            public IReadOnlyList<Question> Questions { get; }
            public int FiftyFiftyCharges => 2;

            public Task<AnswerReview> SubmitAnswerAsync(string questionId, int answerIndex, CancellationToken cancellationToken = default)
            {
                _answer = new TaskCompletionSource<AnswerReview>();
                return _answer.Task;
            }

            public Task<IReadOnlyList<int>> EliminateAsync(string questionId, CancellationToken cancellationToken = default)
            {
                _elimination = new TaskCompletionSource<IReadOnlyList<int>>();
                return _elimination.Task;
            }

            public void Complete(AnswerReview review = null) => _answer.SetResult(review ?? _question.Review);
            public void Fail(Exception e) => _answer.SetException(e);
            public void CompleteElimination(IReadOnlyList<int> indices) => _elimination.SetResult(indices);
        }
    }
}
