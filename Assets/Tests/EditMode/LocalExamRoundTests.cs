using System;
using System.Linq;
using NUnit.Framework;

namespace ExamCards.Exam.Tests
{
    public class LocalExamRoundTests
    {
        static KeyedQuestion MakeQuestion(int correctIndex = 0) => ExamSessionTests.MakeQuestion(correctIndex);

        [Test]
        public void SubmitAnswer_SameAnswerAgain_RepeatsTheResult_OtherAnswerReturnsTheRecordedOne()
        {
            var question = MakeQuestion();
            var round = new LocalExamRound(new[] { question });
            var id = question.Question.Id;

            Assert.AreEqual(2, round.SubmitAnswerAsync(id, 2).Result.AnsweredIndex);
            Assert.AreEqual(2, round.SubmitAnswerAsync(id, 2).Result.AnsweredIndex);
            var other = round.SubmitAnswerAsync(id, 1).Result;
            Assert.AreEqual(2, other.AnsweredIndex);
            Assert.AreEqual(question.Review.CorrectIndex, other.CorrectIndex);
        }

        [Test]
        public void FetchMore_HandsOutBatchesWithoutQuestionsTheRoundHas()
        {
            var start = MakeQuestion();
            var added = MakeQuestion();
            var more = new[] { MakeQuestion(), added, MakeQuestion(), MakeQuestion() };
            var round = new LocalExamRound(new[] { start }, catalog: new[] { added }, more: more, batchSize: 2);
            round.AddQuestionAsync(added.Question.Id).Wait();

            Assert.IsTrue(round.HasMore);
            CollectionAssert.AreEqual(new[] { more[0].Question.Id, more[2].Question.Id },
                round.FetchMoreAsync().Result.Select(q => q.Id));
            CollectionAssert.AreEqual(new[] { more[3].Question.Id }, round.FetchMoreAsync().Result.Select(q => q.Id));
            Assert.IsFalse(round.HasMore);
            Assert.AreEqual(0, round.FetchMoreAsync().Result.Count);
            // Fetched questions can be answered in the round.
            var review = round.SubmitAnswerAsync(more[3].Question.Id, 0).Result;
            Assert.AreSame(more[3].Review.Review, review.Review);
            Assert.AreEqual(0, review.AnsweredIndex);
        }

        [Test]
        public void AddQuestion_FromTheCatalog_MakesItAnswerable_AndRepeats()
        {
            var inRound = MakeQuestion();
            var extra = MakeQuestion(3);
            var round = new LocalExamRound(new[] { inRound }, catalog: new[] { extra });
            var id = extra.Question.Id;

            Assert.Throws<ArgumentException>(() => round.SubmitAnswerAsync(id, 0));
            Assert.AreSame(extra.Question, round.AddQuestionAsync(id).Result);
            Assert.AreSame(extra.Question, round.AddQuestionAsync(id).Result);
            Assert.AreSame(inRound.Question, round.AddQuestionAsync(inRound.Question.Id).Result);
            Assert.AreEqual(3, round.SubmitAnswerAsync(id, 0).Result.CorrectIndex);
            Assert.AreEqual(1, round.Questions.Count, "Questions lists only the round's starting questions.");
            Assert.Throws<ArgumentException>(() => round.AddQuestionAsync("nope"));
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

            Assert.AreEqual(0, eliminated.Remaining);
            var again = round.EliminateAsync(first.Question.Id).Result;
            CollectionAssert.AreEqual(eliminated.Eliminated, again.Eliminated);
            Assert.AreEqual(0, again.Remaining);
            Assert.Throws<InvalidOperationException>(() => round.EliminateAsync(second.Question.Id));
        }

        [Test]
        public void Constructor_InconsistentReview_Throws()
        {
            var question = MakeQuestion();
            question.Review.Review.Feedbacks = new[] { "only one" };

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
