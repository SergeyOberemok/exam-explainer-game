using System;
using System.Linq;
using NUnit.Framework;

namespace ExamCards.Exam.Tests
{
    public class QuestionAssemblerTests
    {
        static QuestionPrompt MakePrompt(string text) => new QuestionPrompt
        {
            Question = text,
            Answers = new[] { "a", "b", "c", "d" },
            Feedbacks = new[] { "fa", "fb", "fc", "fd" },
            Explanation = "why",
        };

        static ExamItem Original(string id, string letter = "a") => new ExamItem
        {
            Id = id,
            Section = "section " + id,
            Source = "source",
            Prompt = MakePrompt("original " + id),
            CorrectResponse = new[] { letter },
        };

        static ExamItem Simplified(string questionId, string letter = "a") => new ExamItem
        {
            Id = "s-" + questionId,
            QuestionId = questionId,
            Prompt = MakePrompt("simplified " + questionId),
            CorrectResponse = new[] { letter },
        };

        [Test]
        public void Build_JoinsByQuestionId_RegardlessOfOrder()
        {
            var questions = QuestionAssembler.Build(
                new[] { Original("1"), Original("2") },
                new[] { Simplified("2"), Simplified("1") });

            Assert.AreEqual(new[] { "1", "2" }, questions.Select(q => q.Id).ToArray());
            Assert.AreEqual("simplified 1", questions[0].Simplified.Question);
            Assert.AreEqual("simplified 2", questions[1].Simplified.Question);
            Assert.AreEqual("original 1", questions[0].Original.Question);
            Assert.AreEqual("section 1", questions[0].Section);
        }

        [TestCase("a", 0)]
        [TestCase("b", 1)]
        [TestCase("c", 2)]
        [TestCase("d", 3)]
        [TestCase("C", 2)]
        public void Build_MapsCorrectLetterToIndex(string letter, int expected)
        {
            var questions = QuestionAssembler.Build(new[] { Original("1", letter) }, Array.Empty<ExamItem>());

            Assert.AreEqual(expected, questions[0].CorrectIndex);
        }

        [Test]
        public void Build_MissingSimplified_KeepsQuestionWithoutReveal()
        {
            var questions = QuestionAssembler.Build(new[] { Original("1") }, Array.Empty<ExamItem>());

            Assert.AreEqual(1, questions.Count);
            Assert.IsNull(questions[0].Simplified);
            Assert.IsFalse(questions[0].CanReveal);
        }

        [TestCase("e")]
        [TestCase("")]
        [TestCase("ab")]
        [TestCase(null)]
        public void Build_InvalidLetter_ThrowsNamingQuestion(string letter)
        {
            var error = Assert.Throws<FormatException>(() =>
                QuestionAssembler.Build(new[] { Original("bad-id", letter) }, Array.Empty<ExamItem>()));

            StringAssert.Contains("bad-id", error.Message);
        }

        [Test]
        public void Build_SeveralCorrectLetters_Throws()
        {
            var item = Original("bad-id");
            item.CorrectResponse = new[] { "a", "b" };

            var error = Assert.Throws<FormatException>(() => QuestionAssembler.Build(new[] { item }, Array.Empty<ExamItem>()));

            StringAssert.Contains("bad-id", error.Message);
        }

        [Test]
        public void Build_FeedbackCountMismatch_Throws()
        {
            var item = Original("bad-id");
            item.Prompt.Feedbacks = new[] { "only one" };

            var error = Assert.Throws<FormatException>(() => QuestionAssembler.Build(new[] { item }, Array.Empty<ExamItem>()));

            StringAssert.Contains("bad-id", error.Message);
        }

        [Test]
        public void Build_SimplifiedAnswerCountMismatch_Throws()
        {
            var simplified = Simplified("bad-id");
            simplified.Prompt.Answers = new[] { "a", "b" };
            simplified.Prompt.Feedbacks = new[] { "fa", "fb" };

            var error = Assert.Throws<FormatException>(() => QuestionAssembler.Build(new[] { Original("bad-id") }, new[] { simplified }));

            StringAssert.Contains("bad-id", error.Message);
        }

        [Test]
        public void Build_SimplifiedDisagreesOnCorrectAnswer_Throws()
        {
            var error = Assert.Throws<FormatException>(() =>
                QuestionAssembler.Build(new[] { Original("bad-id", "a") }, new[] { Simplified("bad-id", "b") }));

            StringAssert.Contains("bad-id", error.Message);
        }

        [TestCase(null)]
        [TestCase("")]
        public void Build_SimplifiedWithoutQuestionId_ThrowsNamingItem(string questionId)
        {
            var simplified = Simplified("1");
            simplified.Id = "bad-id";
            simplified.QuestionId = questionId;

            var error = Assert.Throws<FormatException>(() => QuestionAssembler.Build(new[] { Original("1") }, new[] { simplified }));

            StringAssert.Contains("bad-id", error.Message);
        }

        [Test]
        public void Build_SimplifiedWithNoOriginal_ThrowsNamingQuestion()
        {
            var error = Assert.Throws<FormatException>(() =>
                QuestionAssembler.Build(new[] { Original("1") }, new[] { Simplified("1"), Simplified("bad-id") }));

            StringAssert.Contains("bad-id", error.Message);
        }

        [Test]
        public void Build_MoreAnswersThanSupported_ThrowsNamingQuestion()
        {
            var item = Original("bad-id");
            item.Prompt.Answers = new[] { "a", "b", "c", "d", "e" };
            item.Prompt.Feedbacks = new[] { "fa", "fb", "fc", "fd", "fe" };

            var error = Assert.Throws<FormatException>(() => QuestionAssembler.Build(new[] { item }, Array.Empty<ExamItem>()));

            StringAssert.Contains("bad-id", error.Message);
        }

        [Test]
        public void Build_DuplicateSimplified_Throws()
        {
            var error = Assert.Throws<FormatException>(() =>
                QuestionAssembler.Build(new[] { Original("bad-id") }, new[] { Simplified("bad-id"), Simplified("bad-id") }));

            StringAssert.Contains("bad-id", error.Message);
        }
    }
}
