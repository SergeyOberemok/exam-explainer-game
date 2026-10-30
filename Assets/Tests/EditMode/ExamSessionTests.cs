using System;
using NUnit.Framework;

namespace ExamCards.Exam.Tests
{
    public class ExamSessionTests
    {
        static Question MakeQuestion(int correctIndex) => new Question
        {
            Id = Guid.NewGuid().ToString(),
            Prompt = "?",
            Options = new[] { new AnswerOption(), new AnswerOption(), new AnswerOption(), new AnswerOption() },
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
        public void Constructor_EmptyList_Throws()
        {
            Assert.Throws<ArgumentException>(() => new ExamSession(Array.Empty<Question>()));
        }
    }
}
