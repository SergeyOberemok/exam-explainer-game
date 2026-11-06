using System;
using System.Linq;
using ExamCards.Exam.Api;
using NUnit.Framework;
using UnityEngine;

namespace ExamCards.Exam.Tests
{
    public class GameQuestionMapperTests
    {
        // Same shape as a real POST /v1/game/rounds response; answers deliberately out of order.
        internal const string RoundJson = @"{
            ""round_id"": ""r1"",
            ""items"": [{
                ""id"": ""q1"",
                ""simplified"": false,
                ""section"": ""Domain 5"",
                ""assessment_type"": ""multiple-choice"",
                ""question"": ""Original <example> text"",
                ""answers"": [
                    { ""index"": 2, ""text"": ""C"" },
                    { ""index"": 0, ""text"": ""A"" },
                    { ""index"": 1, ""text"": ""B"" },
                    { ""index"": 3, ""text"": ""D"" }
                ]
            }, {
                ""id"": ""q0"",
                ""simplified"": false,
                ""section"": ""Domain 1"",
                ""assessment_type"": ""multiple-choice"",
                ""question"": ""Second"",
                ""answers"": [ { ""index"": 0, ""text"": ""x"" }, { ""index"": 1, ""text"": ""y"" } ]
            }],
            ""simplified"": [{
                ""id"": ""q1"",
                ""simplified"": true,
                ""section"": ""Domain 5"",
                ""assessment_type"": ""multiple-choice"",
                ""question"": ""Simple text"",
                ""answers"": [
                    { ""index"": 0, ""text"": ""a"" }, { ""index"": 1, ""text"": ""b"" },
                    { ""index"": 2, ""text"": ""c"" }, { ""index"": 3, ""text"": ""d"" }
                ]
            }],
            ""total"": 60,
            ""fifty_fifty_charges"": 2
        }";

        internal const string AnswerJson = @"{
            ""question_id"": ""q1"",
            ""answer_index"": 0,
            ""is_correct"": false,
            ""correct_index"": 1,
            ""original"": { ""explanation"": ""Because."", ""feedbacks"": [""fa"", ""fb"", ""fc"", ""fd""] },
            ""has_simplified"": true,
            ""simplified"": { ""explanation"": ""Simply."", ""feedbacks"": [""sa"", ""sb"", ""sc"", ""sd""] }
        }";

        static GameRoundDto ParseRound() => JsonUtility.FromJson<GameRoundDto>(RoundJson);
        static GameAnswerResultDto ParseAnswer() => JsonUtility.FromJson<GameAnswerResultDto>(AnswerJson);

        [Test]
        public void BuildQuestions_MapsParsedJson_InPlayOrder()
        {
            var questions = GameQuestionMapper.BuildQuestions(ParseRound());

            Assert.AreEqual(2, questions.Count);
            var question = questions[0];
            Assert.AreEqual("q1", question.Id);
            Assert.AreEqual("Domain 5", question.Section);
            Assert.AreEqual(GameQuestionMapper.Source, question.Source);
            Assert.AreEqual("Original <example> text", question.Original.Question);
            CollectionAssert.AreEqual(new[] { "A", "B", "C", "D" }, question.Original.Answers);
            Assert.AreEqual("q0", questions[1].Id);
        }

        [Test]
        public void BuildQuestions_JoinsSimplifiedById()
        {
            var questions = GameQuestionMapper.BuildQuestions(ParseRound());

            Assert.IsTrue(questions[0].CanReveal);
            Assert.AreEqual("Simple text", questions[0].Simplified.Question);
            Assert.IsFalse(questions[1].CanReveal);
        }

        [Test]
        public void BuildQuestions_NoRoundId_Throws()
        {
            var round = ParseRound();
            round.round_id = "";

            Assert.Throws<FormatException>(() => GameQuestionMapper.BuildQuestions(round));
        }

        [Test]
        public void BuildQuestions_SimplifiedNotMarkedSimplified_Throws()
        {
            var round = ParseRound();
            round.simplified[0].simplified = false;

            var e = Assert.Throws<FormatException>(() => GameQuestionMapper.BuildQuestions(round));
            StringAssert.Contains("q1", e.Message);
        }

        [Test]
        public void BuildQuestions_OriginalMarkedSimplified_Throws()
        {
            var round = ParseRound();
            round.items[1].simplified = true;

            var e = Assert.Throws<FormatException>(() => GameQuestionMapper.BuildQuestions(round));
            StringAssert.Contains("q0", e.Message);
        }

        [Test]
        public void BuildQuestions_SimplifiedForAnotherQuestion_Throws()
        {
            var round = ParseRound();
            round.simplified[0].id = "elsewhere";

            Assert.Throws<FormatException>(() => GameQuestionMapper.BuildQuestions(round));
        }

        [Test]
        public void BuildQuestions_SimplifiedWithDifferentAnswerCount_Throws()
        {
            var round = ParseRound();
            round.simplified[0].answers = new[] { new GameAnswerDto { index = 0, text = "a" } };

            var e = Assert.Throws<FormatException>(() => GameQuestionMapper.BuildQuestions(round));
            StringAssert.Contains("q1", e.Message);
        }

        [Test]
        public void BuildQuestions_GapInAnswerIndices_Throws()
        {
            var round = ParseRound();
            round.items[0].answers[3].index = 5;

            var e = Assert.Throws<FormatException>(() => GameQuestionMapper.BuildQuestions(round));
            StringAssert.Contains("q1", e.Message);
        }

        [Test]
        public void BuildQuestions_DuplicateQuestion_Throws()
        {
            var round = ParseRound();
            round.items[1].id = "q1";

            Assert.Throws<FormatException>(() => GameQuestionMapper.BuildQuestions(round));
        }

        [Test]
        public void BuildQuestions_TooManyAnswers_Throws()
        {
            var round = ParseRound();
            var answers = new GameAnswerDto[Question.MaxAnswers + 1];
            for (var i = 0; i < answers.Length; i++)
                answers[i] = new GameAnswerDto { index = i, text = i.ToString() };
            round.items[1].answers = answers;

            Assert.Throws<FormatException>(() => GameQuestionMapper.BuildQuestions(round));
        }

        [Test]
        public void ToReview_MapsBothVersions()
        {
            var review = GameQuestionMapper.ToReview(ParseAnswer(), "q1", 0);

            Assert.AreEqual(1, review.CorrectIndex);
            Assert.AreEqual("Because.", review.Original.Explanation);
            CollectionAssert.AreEqual(new[] { "fa", "fb", "fc", "fd" }, review.Original.Feedbacks);
            Assert.AreEqual("Simply.", review.Simplified.Explanation);
        }

        [Test]
        public void ToReview_WithoutSimplified_HasNoSimplifiedReview()
        {
            var result = ParseAnswer();
            result.has_simplified = false;

            Assert.IsNull(GameQuestionMapper.ToReview(result, "q1", 0).Simplified);
        }

        [Test]
        public void ToReview_ForAnotherAnswer_Throws()
        {
            Assert.Throws<FormatException>(() => GameQuestionMapper.ToReview(ParseAnswer(), "q2", 0));
            Assert.Throws<FormatException>(() => GameQuestionMapper.ToReview(ParseAnswer(), "q1", 3));
        }

        [Test]
        public void ToReview_IsCorrectContradictingCorrectIndex_Throws()
        {
            var result = ParseAnswer();
            result.is_correct = true;

            Assert.Throws<FormatException>(() => GameQuestionMapper.ToReview(result, "q1", 0));
        }

        [Test]
        public void ToFiftyFifty_ForAnotherQuestion_Throws_AndKeepsTheServerCount()
        {
            var result = new GameFiftyFiftyResultDto
            {
                question_id = "q2", eliminated_indices = new[] { 0, 2 }, fifty_fifty_remaining = 1,
            };

            Assert.Throws<FormatException>(() => GameQuestionMapper.ToFiftyFifty(result, "q1"));
            var mapped = GameQuestionMapper.ToFiftyFifty(result, "q2");
            CollectionAssert.AreEqual(new[] { 0, 2 }, mapped.Eliminated);
            Assert.AreEqual(1, mapped.Remaining);
        }

        [Test]
        public void ToReview_RecordedAnswer_AcceptsAnyIndex_AndSaysWhichCounted()
        {
            var result = ParseAnswer();

            var review = GameQuestionMapper.ToReview(result, "q1", null);

            Assert.AreEqual(result.answer_index, review.AnsweredIndex);
        }

        [Test]
        public void ToReview_WithoutAReview_Throws()
        {
            // JsonUtility leaves a missing object with default fields instead of failing.
            var result = JsonUtility.FromJson<GameAnswerResultDto>(
                "{\"question_id\":\"q1\",\"answer_index\":0,\"is_correct\":false,\"correct_index\":1}");

            Assert.Throws<FormatException>(() => GameQuestionMapper.ToReview(result, "q1", 0));
        }

        // A round whose only simplified version carries ENTITIES (two answers).
        const string EntitiesRoundJson = @"{
            ""round_id"": ""r1"",
            ""items"": [{
                ""id"": ""q1"", ""simplified"": false, ""section"": ""S"", ""assessment_type"": ""t"",
                ""question"": ""Original"",
                ""answers"": [ { ""index"": 0, ""text"": ""x"" }, { ""index"": 1, ""text"": ""y"" } ],
                ""entities"": { ""question"": [], ""answers"": [] }
            }],
            ""simplified"": [{
                ""id"": ""q1"", ""simplified"": true, ""section"": ""S"", ""assessment_type"": ""t"",
                ""question"": ""Simple"",
                ""answers"": [ { ""index"": 0, ""text"": ""a"" }, { ""index"": 1, ""text"": ""b"" } ]
                ENTITIES
            }],
            ""total"": 1,
            ""fifty_fifty_charges"": 2
        }";

        static Question ParseEntities(string entitiesJson) =>
            GameQuestionMapper.BuildQuestions(JsonUtility.FromJson<GameRoundDto>(
                EntitiesRoundJson.Replace("ENTITIES", entitiesJson == null ? "" : "," + entitiesJson)))[0];

        [Test]
        public void Entities_MapToTheSimplifiedPrompt()
        {
            var question = ParseEntities(@"""entities"": {
                ""question"": [ { ""name"": "" Subagent "", ""kind"": ""actor"", ""aliases"": [""helper"", "" ""] } ],
                ""answers"": [
                    { ""entities"": [ { ""name"": ""process_refund"", ""kind"": ""action"", ""aliases"": [] } ] },
                    { ""entities"": [] }
                ] }");

            var entities = question.Simplified.Entities;
            Assert.AreEqual("Subagent", entities.Question.Single().Name);
            Assert.AreEqual(EntityKind.Actor, entities.Question[0].Kind);
            CollectionAssert.AreEqual(new[] { "helper" }, entities.Question[0].Aliases);
            Assert.AreEqual(EntityKind.Action, entities.ForAnswer(0).Single().Kind);
            Assert.AreEqual(0, entities.ForAnswer(1).Count);
            Assert.AreEqual(0, entities.ForAnswer(7).Count);
            Assert.IsTrue(question.Original.Entities.IsEmpty);
        }

        [Test]
        public void Entities_Missing_AreEmpty()
        {
            Assert.IsTrue(ParseEntities(null).Simplified.Entities.IsEmpty);
            Assert.IsTrue(ParseEntities(@"""entities"": {}").Simplified.Entities.IsEmpty);
        }

        [Test]
        public void Entities_BadOnes_AreDropped_NotFatal()
        {
            var question = ParseEntities(@"""entities"": {
                ""question"": [
                    { ""name"": ""Planet"", ""kind"": ""planet"" },
                    { ""name"": "" "", ""kind"": ""object"" },
                    { ""name"": ""Server"", ""kind"": ""object"" } ],
                ""answers"": [ { ""entities"": [ { ""name"": ""A"", ""kind"": ""object"" } ] } ] }");

            var entities = question.Simplified.Entities;
            Assert.AreEqual("Server", entities.Question.Single().Name);
            Assert.AreEqual(0, entities.Answers.Count, "one list for two answers doesn't line up");
            Assert.AreEqual(0, entities.Question[0].Aliases.Count);
        }
    }
}
