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
                Assert.IsNotEmpty(question.Objectives, question.Id);
                Assert.That(question.Difficulty, Is.InRange(1, 3), question.Id);
                Assert.AreEqual(MockQuestionService.SampleScenario, question.Scenario, question.Id);
                Assert.IsNotEmpty(question.Stage, question.Id);
                Assert.IsNotEmpty(question.Beat, question.Id);

                var review = round.SubmitAnswerAsync(question.Id, 0).GetAwaiter().GetResult();
                Assert.That(review.CorrectIndex, Is.InRange(0, 3), question.Id);
                Assert.IsFalse(string.IsNullOrWhiteSpace(review.Original.Explanation), question.Id);
                // Like the backend: both texts carry the same entities.
                Assert.AreSame(question.Original.Entities, question.Simplified.Entities, question.Id);
            }
        }

        [Test]
        public void SomeQuestions_HavePartsAndAResolutionThatAgreesWithTheKey()
        {
            var round = new MockQuestionService(latencyMs: 0).StartRoundAsync().GetAwaiter().GetResult();
            var decomposed = round.Questions.Where(q => q.Parts.Count > 0).ToList();

            Assert.GreaterOrEqual(decomposed.Count, 2);
            foreach (var question in decomposed)
            {
                Assert.AreEqual(PartRole.Ask, question.Parts.Last().Role, question.Id);
                Assert.IsNotNull(question.Ask, question.Id);

                var review = round.SubmitAnswerAsync(question.Id, 0).GetAwaiter().GetResult();
                Assert.IsNotEmpty(review.Resolution.Rule, question.Id);
                for (var i = 0; i < question.Original.Answers.Count; i++)
                    Assert.AreEqual(i == review.CorrectIndex, review.Resolution.ForAnswer(i).IsCorrect, $"{question.Id} answer {i}");
            }
        }

        [Test]
        public void Requirements_TheCorrectAnswerMeetsAll_AndAWrongOneOnlySome()
        {
            var round = new MockQuestionService(latencyMs: 0).StartRoundAsync().GetAwaiter().GetResult();
            var withRequirements = round.Questions
                .Select(q => (q.Id, Review: round.SubmitAnswerAsync(q.Id, 0).GetAwaiter().GetResult()))
                .Where(r => r.Review.Resolution.AnswerRequirements.Count > 0)
                .ToList();

            Assert.IsNotEmpty(withRequirements);
            foreach (var (id, review) in withRequirements)
            {
                var resolution = review.Resolution;
                var total = resolution.AnswerRequirements.Count;
                Assert.AreEqual(total, resolution.MetCount(review.CorrectIndex), id);
                Assert.IsTrue(Enumerable.Range(0, 4).Any(i => i != review.CorrectIndex && resolution.MetCount(i) > 0 && resolution.MetCount(i) < total), id);
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
