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
                Assert.IsTrue(question.CanReveal, question.Id);
                Assert.That(question.CorrectIndex, Is.InRange(0, question.Original.Answers.Count - 1), question.Id);
                AssertValid(question.Original, question.Id);
                AssertValid(question.Simplified, question.Id);
            }
        }

        static void AssertValid(QuestionPrompt prompt, string id)
        {
            Assert.AreEqual(4, prompt.Answers.Count, id);
            Assert.AreEqual(4, prompt.Feedbacks.Count, id);
            Assert.IsFalse(string.IsNullOrWhiteSpace(prompt.Question), id);
            Assert.IsFalse(string.IsNullOrWhiteSpace(prompt.Explanation), id);
        }
    }
}
