using System.Linq;
using NUnit.Framework;

namespace ExamCards.Exam.Tests
{
    public class MockQuestionServiceTests
    {
        [Test]
        public void StartRound_ReturnsValidPickOneQuestions()
        {
            var round = new MockQuestionService(latencyMs: 0).StartRoundAsync().GetAwaiter().GetResult();
            var questions = round.Questions;

            Assert.IsNotEmpty(questions);
            Assert.AreEqual(questions.Count, questions.Select(q => q.Id).Distinct().Count(), "Question ids must be unique.");
            foreach (var question in questions)
            {
                Assert.IsTrue(question.CanReveal, question.Id);
                Assert.AreEqual(4, question.Original.Answers.Count, question.Id);
                Assert.IsFalse(string.IsNullOrWhiteSpace(question.Original.Question), question.Id);

                var review = round.SubmitAnswerAsync(question.Id, 0).GetAwaiter().GetResult();
                Assert.That(review.CorrectIndex, Is.InRange(0, 3), question.Id);
                Assert.IsFalse(string.IsNullOrWhiteSpace(review.Original.Explanation), question.Id);
                Assert.IsFalse(string.IsNullOrWhiteSpace(review.Simplified.Explanation), question.Id);
            }
        }

        [Test]
        public void Search_FindsQuestionsOutsideTheRound_ThatTheRoundCanAdd()
        {
            var service = new MockQuestionService(latencyMs: 0);
            var round = service.StartRoundAsync().GetAwaiter().GetResult();

            var results = service.SearchAsync("cors").GetAwaiter().GetResult();

            Assert.AreEqual(1, results.Count);
            var found = results[0];
            Assert.IsFalse(found.CanReveal, "Search results have no simplified text.");
            Assert.IsFalse(round.Questions.Any(q => q.Id == found.Id));
            var added = round.AddQuestionAsync(found.Id).GetAwaiter().GetResult();
            Assert.AreEqual(found.Id, added.Id);
            Assert.IsTrue(added.CanReveal);
            Assert.AreEqual(3, round.SubmitAnswerAsync(found.Id, 3).GetAwaiter().GetResult().CorrectIndex);
        }

        [Test]
        public void Search_IsCaseInsensitive_AndMatchesSectionsAndAnswers()
        {
            var service = new MockQuestionService(latencyMs: 0);

            Assert.AreEqual(2, service.SearchAsync("  DATABASES ").GetAwaiter().GetResult().Count);
            Assert.AreEqual(1, service.SearchAsync("git revert").GetAwaiter().GetResult().Count);
            Assert.IsEmpty(service.SearchAsync("no such text").GetAwaiter().GetResult());
        }
    }
}
