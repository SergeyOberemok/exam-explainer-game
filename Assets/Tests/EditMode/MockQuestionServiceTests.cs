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
    }
}
