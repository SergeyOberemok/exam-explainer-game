using System;
using NUnit.Framework;

namespace ExamCards.Exam.Tests
{
    public class ExamSessionTests
    {
        static QuestionPrompt MakePrompt(string text) => new QuestionPrompt
        {
            Question = text,
            Answers = new[] { "a", "b", "c", "d" },
            Feedbacks = new[] { "fa", "fb", "fc", "fd" },
            Explanation = text + " explanation",
        };

        static Question MakeQuestion(int correctIndex, bool withSimplified = true) => new Question
        {
            Id = Guid.NewGuid().ToString(),
            Original = MakePrompt("original"),
            Simplified = withSimplified ? MakePrompt("simplified") : null,
            CorrectIndex = correctIndex,
        };

        [Test]
        public void Answer_Correct_IncrementsScore()
        {
            var session = new ExamSession(new[] { MakeQuestion(2) });

            var result = session.Answer(2);

            Assert.IsTrue(result.IsCorrect);
            Assert.AreEqual(2, result.CorrectIndex);
            Assert.AreEqual(1, session.Score);
        }

        [Test]
        public void Answer_Wrong_KeepsScoreAndReportsCorrectIndex()
        {
            var session = new ExamSession(new[] { MakeQuestion(2) });

            var result = session.Answer(0);

            Assert.IsFalse(result.IsCorrect);
            Assert.AreEqual(2, result.CorrectIndex);
            Assert.AreEqual(0, session.Score);
        }

        [Test]
        public void Answer_Twice_Throws()
        {
            var session = new ExamSession(new[] { MakeQuestion(0) });
            session.Answer(0);

            Assert.Throws<InvalidOperationException>(() => session.Answer(1));
        }

        [TestCase(-1)]
        [TestCase(4)]
        public void Answer_OutOfRange_Throws(int index)
        {
            var session = new ExamSession(new[] { MakeQuestion(0) });

            Assert.Throws<ArgumentOutOfRangeException>(() => session.Answer(index));
        }

        [Test]
        public void MoveNext_WalksAllQuestionsThenStops()
        {
            var session = new ExamSession(new[] { MakeQuestion(0), MakeQuestion(1) });

            Assert.AreEqual(0, session.CurrentIndex);
            session.Answer(0);
            Assert.IsTrue(session.MoveNext());
            Assert.AreEqual(1, session.CurrentIndex);
            Assert.IsFalse(session.IsCurrentAnswered);
            session.Answer(1);
            Assert.IsFalse(session.MoveNext());
            Assert.AreEqual(1, session.CurrentIndex);
            Assert.AreEqual(2, session.Score);
        }

        [Test]
        public void MoveNext_Unanswered_Throws()
        {
            var session = new ExamSession(new[] { MakeQuestion(0), MakeQuestion(1) });

            Assert.Throws<InvalidOperationException>(() => session.MoveNext());
            Assert.AreEqual(0, session.CurrentIndex);
        }

        [Test]
        public void MoveNext_PastLastQuestion_FinishesExam()
        {
            var session = new ExamSession(new[] { MakeQuestion(0) });
            session.Answer(0);
            Assert.IsFalse(session.IsFinished);

            Assert.IsFalse(session.MoveNext());

            Assert.IsTrue(session.IsFinished);
        }

        [Test]
        public void AnsweredIndex_TracksPickAndClearsOnNextQuestion()
        {
            var session = new ExamSession(new[] { MakeQuestion(0), MakeQuestion(1) });
            Assert.IsNull(session.AnsweredIndex);

            session.Answer(3);
            Assert.AreEqual(3, session.AnsweredIndex);

            session.MoveNext();
            Assert.IsNull(session.AnsweredIndex);
        }

        [Test]
        public void AnsweredCount_CountsTheCurrentQuestionOnceAnswered()
        {
            var session = new ExamSession(new[] { MakeQuestion(0), MakeQuestion(1) });
            Assert.AreEqual(0, session.AnsweredCount);

            session.Answer(0);
            Assert.AreEqual(1, session.AnsweredCount);

            session.MoveNext();
            Assert.AreEqual(1, session.AnsweredCount);

            session.Answer(1);
            Assert.AreEqual(2, session.AnsweredCount);
        }

        [Test]
        public void Reveal_AfterFinished_ReturnsFalse()
        {
            var session = new ExamSession(new[] { MakeQuestion(0) });
            session.Answer(0);
            session.MoveNext();

            Assert.IsFalse(session.Reveal());
        }

        [Test]
        public void Constructor_EmptyList_Throws()
        {
            Assert.Throws<ArgumentException>(() => new ExamSession(Array.Empty<Question>()));
        }

        [Test]
        public void CurrentPrompt_BeforeReveal_IsOriginal()
        {
            var question = MakeQuestion(0);
            var session = new ExamSession(new[] { question });

            Assert.IsFalse(session.IsCurrentRevealed);
            Assert.AreSame(question.Original, session.CurrentPrompt);
        }

        [Test]
        public void Reveal_SwapsCurrentPromptToSimplified()
        {
            var question = MakeQuestion(0);
            var session = new ExamSession(new[] { question });

            Assert.IsTrue(session.Reveal());

            Assert.IsTrue(session.IsCurrentRevealed);
            Assert.AreSame(question.Simplified, session.CurrentPrompt);
        }

        [Test]
        public void Reveal_Twice_ReturnsFalseAndStaysRevealed()
        {
            var question = MakeQuestion(0);
            var session = new ExamSession(new[] { question });
            session.Reveal();

            Assert.IsFalse(session.Reveal());
            Assert.AreSame(question.Simplified, session.CurrentPrompt);
        }

        [Test]
        public void Reveal_AfterAnswering_WorksAndKeepsScore()
        {
            var question = MakeQuestion(1);
            var session = new ExamSession(new[] { question });
            session.Answer(1);

            Assert.IsTrue(session.Reveal());

            Assert.AreSame(question.Simplified, session.CurrentPrompt);
            Assert.IsTrue(session.IsCurrentAnswered);
            Assert.AreEqual(1, session.Score);
        }

        [Test]
        public void Reveal_BeforeAnswering_DoesNotChangeScoring()
        {
            var session = new ExamSession(new[] { MakeQuestion(1) });
            session.Reveal();

            var result = session.Answer(1);

            Assert.IsTrue(result.IsCorrect);
            Assert.AreEqual(1, session.Score);
        }

        [Test]
        public void MoveNext_ResetsReveal()
        {
            var second = MakeQuestion(0);
            var session = new ExamSession(new[] { MakeQuestion(0), second });
            session.Reveal();
            session.Answer(0);

            session.MoveNext();

            Assert.IsFalse(session.IsCurrentRevealed);
            Assert.AreSame(second.Original, session.CurrentPrompt);
        }

        [Test]
        public void Reveal_WithoutSimplifiedVersion_DoesNothing()
        {
            var question = MakeQuestion(0, withSimplified: false);
            var session = new ExamSession(new[] { question });

            Assert.IsFalse(question.CanReveal);
            Assert.IsFalse(session.Reveal());
            Assert.IsFalse(session.IsCurrentRevealed);
            Assert.AreSame(question.Original, session.CurrentPrompt);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void Eliminate_RemovesTwoWrongAnswersAndKeepsTheCorrectOne(int correctIndex)
        {
            for (var seed = 0; seed < 20; seed++)
            {
                var session = new ExamSession(new[] { MakeQuestion(correctIndex) }, new Random(seed));

                Assert.IsTrue(session.Eliminate());

                var eliminated = 0;
                for (var i = 0; i < 4; i++)
                    eliminated += session.IsEliminated(i) ? 1 : 0;
                Assert.AreEqual(2, eliminated);
                Assert.IsFalse(session.IsEliminated(correctIndex));
            }
        }

        [Test]
        public void Eliminate_UsesACharge_AndWorksOncePerQuestion()
        {
            var session = new ExamSession(new[] { MakeQuestion(0), MakeQuestion(0) });
            Assert.AreEqual(2, session.FiftyFiftyRemaining);

            Assert.IsTrue(session.Eliminate());
            Assert.AreEqual(1, session.FiftyFiftyRemaining);
            Assert.IsFalse(session.Eliminate());
            Assert.AreEqual(1, session.FiftyFiftyRemaining);
        }

        [Test]
        public void Eliminate_RunsOutOfCharges()
        {
            var session = new ExamSession(new[] { MakeQuestion(0), MakeQuestion(0) }, fiftyFiftyCharges: 1);
            session.Eliminate();
            session.Answer(0);
            session.MoveNext();

            Assert.IsFalse(session.CanEliminate);
            Assert.IsFalse(session.Eliminate());
        }

        [Test]
        public void Eliminate_AfterAnswering_ReturnsFalse()
        {
            var session = new ExamSession(new[] { MakeQuestion(0) });
            session.Answer(0);

            Assert.IsFalse(session.Eliminate());
            Assert.AreEqual(2, session.FiftyFiftyRemaining);
        }

        [Test]
        public void Answer_EliminatedOption_Throws()
        {
            var session = new ExamSession(new[] { MakeQuestion(0) }, new Random(1));
            session.Eliminate();
            var eliminated = -1;
            for (var i = 0; i < 4; i++)
            {
                if (session.IsEliminated(i))
                    eliminated = i;
            }

            Assert.Throws<InvalidOperationException>(() => session.Answer(eliminated));
        }

        [Test]
        public void MoveNext_ClearsEliminatedAnswers()
        {
            var session = new ExamSession(new[] { MakeQuestion(0), MakeQuestion(0) });
            session.Eliminate();
            session.Answer(0);

            session.MoveNext();

            Assert.IsFalse(session.IsCurrentEliminated);
            for (var i = 0; i < 4; i++)
                Assert.IsFalse(session.IsEliminated(i));
        }
    }
}
