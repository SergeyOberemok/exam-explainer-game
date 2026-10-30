using System.Linq;
using NUnit.Framework;

namespace ExamCards.Exam.Tests
{
    public class MockQuestionServiceTests
    {
        [Test]
        public void GetQuestions_ReturnsValidPickOneQuestions()
        {
            var questions = new MockQuestionService(latencyMs: 0).GetQuestionsAsync().GetAwaiter().GetResult();

            Assert.IsNotEmpty(questions);
            Assert.AreEqual(questions.Count, questions.Select(q => q.Id).Distinct().Count(), "Question ids must be unique.");
            foreach (var question in questions)
            {
                Assert.AreEqual(4, question.Options.Count, question.Id);
                Assert.That(question.CorrectIndex, Is.InRange(0, question.Options.Count - 1), question.Id);
                Assert.IsFalse(string.IsNullOrWhiteSpace(question.Prompt), question.Id);
                Assert.IsTrue(question.Options.All(o => !string.IsNullOrEmpty(o.Image)), question.Id);
            }
        }
    }
}
