using System;
using NUnit.Framework;

namespace ExamCards.Exam.Tests
{
    public class LocalExamRoundTests
    {
        static KeyedQuestion MakeQuestion(int correctIndex = 0) => ExamSessionTests.MakeQuestion(correctIndex);

        [Test]
        public void SubmitAnswer_SameAnswerAgain_RepeatsTheResult_OtherAnswerThrows()
        {
            var question = MakeQuestion();
            var round = new LocalExamRound(new[] { question });
            var id = question.Question.Id;

            Assert.AreSame(question.Review, round.SubmitAnswerAsync(id, 2).Result);
            Assert.AreSame(question.Review, round.SubmitAnswerAsync(id, 2).Result);
            Assert.Throws<InvalidOperationException>(() => round.SubmitAnswerAsync(id, 1));
        }

        [Test]
        public void SubmitAnswer_UnknownQuestion_Throws()
        {
            var round = new LocalExamRound(new[] { MakeQuestion() });

            Assert.Throws<ArgumentException>(() => round.SubmitAnswerAsync("nope", 0));
        }

        [Test]
        public void Eliminate_Again_RepeatsTheResultWithoutACharge()
        {
            var first = MakeQuestion();
            var second = MakeQuestion();
            var round = new LocalExamRound(new[] { first, second }, new Random(3), fiftyFiftyCharges: 1);

            var eliminated = round.EliminateAsync(first.Question.Id).Result;

            CollectionAssert.AreEqual(eliminated, round.EliminateAsync(first.Question.Id).Result);
            Assert.Throws<InvalidOperationException>(() => round.EliminateAsync(second.Question.Id));
        }

        [Test]
        public void Constructor_InconsistentReview_Throws()
        {
            var question = MakeQuestion();
            question.Review.Original.Feedbacks = new[] { "only one" };

            var e = Assert.Throws<FormatException>(() => new LocalExamRound(new[] { question }));
            StringAssert.Contains(question.Question.Id, e.Message);
        }

        [Test]
        public void Constructor_DuplicateId_Throws()
        {
            var question = MakeQuestion();

            Assert.Throws<FormatException>(() => new LocalExamRound(new[] { question, question }));
        }
    }
}
