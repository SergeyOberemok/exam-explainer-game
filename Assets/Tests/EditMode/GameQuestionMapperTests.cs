using System;
using System.Linq;
using ExamCards.Exam.Api;
using NUnit.Framework;
using UnityEngine;

namespace ExamCards.Exam.Tests
{
    public class GameQuestionMapperTests
    {
        // Same shape as a real POST /v2/game/rounds response; tools deliberately out of order.
        internal const string RoundJson = @"{
            ""round_id"": ""r1"",
            ""items"": [{
                ""id"": ""q1"",
                ""section"": ""Domain 5"",
                ""assessment_type"": ""multiple-choice"",
                ""scenario"": """", ""stage"": """", ""objectives"": [], ""difficulty"": 0,
                ""icon"": ""workflow"",
                ""beat"": """",
                ""question"": ""Original <example> text"",
                ""stem"": """",
                ""briefing"": [
                    { ""role"": ""context"", ""text"": "" A team ships an app. "", ""entity_ids"": [""e1"", ""ghost"", "" ""] },
                    { ""role"": ""constraint"", ""text"": ""It must work offline."", ""entity_ids"": [] }
                ],
                ""ask"": { ""text"": "" What now? "", ""entity_ids"": [""e1""] },
                ""cast"": [
                    { ""ref"": ""e1"", ""name"": "" Team "", ""type"": ""actor"", ""aliases"": [""devs"", "" ""], ""icon"": ""user"", ""scene"": true },
                    { ""ref"": ""e2"", ""name"": ""Cache"", ""type"": ""concept"", ""aliases"": [], ""icon"": """", ""scene"": false }
                ],
                ""actions"": [
                    { ""ref"": ""ev1"", ""name"": ""Ship the app"", ""sequence"": 1, ""actor"": ""e1"", ""target"": ""ghost"",
                      ""inputs"": [], ""outputs"": [""e2""], ""icon"": ""send"" }
                ],
                ""tools"": [
                    { ""index"": 2, ""text"": ""C"", ""label"": ""Third"", ""icon"": """", ""components"": [] },
                    { ""index"": 0, ""text"": ""A"", ""label"": "" First "", ""icon"": ""Not A Key"", ""components"": [""e1""] },
                    { ""index"": 1, ""text"": ""B"", ""label"": ""Second"", ""icon"": ""cache"", ""components"": [""e2"", ""ghost""] },
                    { ""index"": 3, ""text"": ""D"", ""label"": """", ""icon"": """", ""components"": [] }
                ],
                ""map"": { ""primary"": """", ""secondary"": [], ""reason"": """", ""trigger"": """", ""nodes"": [], ""edges"": [] }
            }, {
                ""id"": ""q0"",
                ""section"": ""Domain 1"",
                ""assessment_type"": ""multiple-choice"",
                ""question"": ""Second"",
                ""tools"": [ { ""index"": 0, ""text"": ""x"" }, { ""index"": 1, ""text"": ""y"" } ]
            }],
            ""total"": 60,
            ""fifty_fifty_charges"": 2
        }";

        internal const string AnswerJson = @"{
            ""question_id"": ""q1"",
            ""answer_index"": 0,
            ""is_correct"": false,
            ""correct_index"": 1,
            ""outcome"": { ""index"": 0, ""verdict"": ""incorrect"", ""impact"": ""partial"", ""consequence"": "" It half works. "",
                ""misconception"": "" Believes X. "", ""reason"": "" Needs a network. "", ""gaps"": [""Works offline""], ""supported_requirements"": [] },
            ""outcomes"": [
                { ""index"": 0, ""verdict"": ""incorrect"", ""impact"": ""partial"", ""consequence"": "" It half works. "",
                  ""misconception"": "" Believes X. "", ""reason"": "" Needs a network. "", ""gaps"": [""Works offline""], ""supported_requirements"": [] },
                { ""index"": 1, ""verdict"": ""correct"", ""impact"": ""success"", ""consequence"": ""It works."",
                  ""misconception"": ""Never shown."", ""reason"": ""Caches."", ""gaps"": [], ""supported_requirements"": [""Works offline""] },
                { ""index"": 2, ""verdict"": ""incorrect"", ""impact"": ""failure"", ""consequence"": ""It breaks."",
                  ""misconception"": ""Believes Y."", ""reason"": ""No."", ""gaps"": [], ""supported_requirements"": [] }
            ],
            ""story_line"": "" The app ships. "",
            ""review"": { ""explanation"": ""Because."", ""feedbacks"": [""fa"", ""fb"", ""fc"", ""fd""] },
            ""rule"": "" Cache what must work offline. "",
            ""answer_requirements"": [ "" Works offline "", """" ],
            ""resolution_flow"": { ""primary"": ""process_flow"", ""secondary"": [], ""reason"": """", ""trigger"": """",
                ""nodes"": [
                    { ""id"": ""n1"", ""type"": ""start"", ""label"": ""Start"", ""entity_ids"": [], ""icon"": ""workflow"", ""condition_id"": """", ""source_status"": ""explicit"", ""is_question"": false, ""added"": false },
                    { ""id"": ""n2"", ""type"": ""process"", ""label"": ""Cache it"", ""entity_ids"": [""e1""], ""icon"": ""Not A Key"", ""condition_id"": """", ""source_status"": ""inferred"", ""is_question"": false, ""added"": true }
                ],
                ""edges"": [ { ""source"": ""n1"", ""target"": ""n2"", ""type"": ""next"", ""label"": """", ""source_status"": ""inferred"", ""added"": true } ] }
        }";

        static GameCardRoundDto ParseRound() => JsonUtility.FromJson<GameCardRoundDto>(RoundJson);
        static GameCardAnswerResultDto ParseAnswer() => JsonUtility.FromJson<GameCardAnswerResultDto>(AnswerJson);
        static Question First() => GameQuestionMapper.BuildQuestions(ParseRound())[0];

        [Test]
        public void ToReview_KeepsANodesIconKey_AndDropsOneThatIsntAKey()
        {
            var flow = GameQuestionMapper.ToReview(ParseAnswer(), "q1", 0).Resolution.Flow;

            Assert.AreEqual("workflow", flow.Nodes[0].Icon);
            Assert.IsNull(flow.Nodes[1].Icon);
        }

        [Test]
        public void BuildQuestions_MapsParsedJson_InPlayOrder_WithToolsByIndex()
        {
            var questions = GameQuestionMapper.BuildQuestions(ParseRound());

            Assert.AreEqual(2, questions.Count);
            var question = questions[0];
            Assert.AreEqual("q1", question.Id);
            Assert.AreEqual("Domain 5", question.Section);
            Assert.AreEqual(GameQuestionMapper.Source, question.Source);
            Assert.AreEqual("Original <example> text", question.Text);
            CollectionAssert.AreEqual(new[] { "A", "B", "C", "D" }, question.Answers);
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, question.Tools.Select(t => t.Index));
            Assert.AreEqual("First", question.Tools[0].Label);
            Assert.AreEqual("q0", questions[1].Id);
        }

        [Test]
        public void BuildQuestions_NoRoundId_Throws()
        {
            var round = ParseRound();
            round.round_id = "";

            Assert.Throws<FormatException>(() => GameQuestionMapper.BuildQuestions(round));
        }

        [Test]
        public void BuildQuestions_GapInToolIndices_Throws()
        {
            var round = ParseRound();
            round.items[0].tools[3].index = 5;

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
        public void BuildQuestions_NoTools_Or_TooMany_Throws()
        {
            var round = ParseRound();
            round.items[1].tools = Array.Empty<GameToolDto>();
            Assert.Throws<FormatException>(() => GameQuestionMapper.BuildQuestions(round));

            round = ParseRound();
            round.items[1].tools = Enumerable.Range(0, Question.MaxAnswers + 1)
                .Select(i => new GameToolDto { index = i, text = i.ToString() }).ToArray();
            Assert.Throws<FormatException>(() => GameQuestionMapper.BuildQuestions(round));
        }

        [Test]
        public void Cast_MapsTrimmed_WithTypesAndScene()
        {
            var question = First();

            Assert.AreEqual(2, question.Cast.Count);
            var team = question.Cast[0];
            Assert.AreEqual("e1", team.Ref);
            Assert.AreEqual("Team", team.Name);
            Assert.AreEqual(CastType.Actor, team.Type);
            CollectionAssert.AreEqual(new[] { "devs" }, team.Aliases);
            Assert.AreEqual("user", team.Icon);
            Assert.IsTrue(team.InScene);

            var cache = question.Cast[1];
            Assert.AreEqual(CastType.Concept, cache.Type);
            Assert.IsNull(cache.Icon, "an empty icon means the client's default");
            Assert.IsFalse(cache.InScene);
            CollectionAssert.AreEqual(new[] { team }, question.SceneCast);
        }

        [Test]
        public void Cast_BadMembers_AreDropped_AndUnknownTypesReadAsObjects()
        {
            var round = ParseRound();
            round.items[0].cast = new[]
            {
                new GameCastMemberDto { @ref = "a", name = "Planet", type = "planet", scene = true },
                new GameCastMemberDto { @ref = "", name = "No ref" },
                new GameCastMemberDto { @ref = "b", name = " " },
                new GameCastMemberDto { @ref = "a", name = "Repeated ref" },
                null,
            };

            var cast = GameQuestionMapper.BuildQuestions(round)[0].Cast;

            Assert.AreEqual("Planet", cast.Single().Name);
            Assert.AreEqual(CastType.Object, cast[0].Type);
        }

        [Test]
        public void Briefing_DropsUnknownRolesBlankTextAndAsks_AndRefsNamingNoMember()
        {
            var round = ParseRound();
            var briefing = round.items[0].briefing.ToList();
            briefing.Add(new GamePartDto { role = "mood", text = "Unknown role" });
            briefing.Add(new GamePartDto { role = "problem", text = "  " });
            briefing.Add(new GamePartDto { role = "ask", text = "Shown by the ask field" });
            briefing.Add(null);
            round.items[0].briefing = briefing.ToArray();

            var question = GameQuestionMapper.BuildQuestions(round)[0];

            CollectionAssert.AreEqual(new[] { PartRole.Context, PartRole.Constraint }, question.Briefing.Select(p => p.Role));
            Assert.AreEqual("A team ships an app.", question.Briefing[0].Text);
            CollectionAssert.AreEqual(new[] { "e1" }, question.Briefing[0].EntityRefs);
            Assert.AreEqual(PartRole.Ask, question.Ask.Role);
            Assert.AreEqual("What now?", question.Ask.Text);
            CollectionAssert.AreEqual(new[] { "e1" }, question.Ask.EntityRefs);
        }

        [Test]
        public void Tools_KeepKnownComponents_AndCleanIcons()
        {
            var question = First();

            CollectionAssert.AreEqual(new[] { "e2" }, question.Tools[1].ComponentRefs);
            Assert.IsNull(question.IconForAnswer(0), "not a key");
            Assert.AreEqual("cache", question.IconForAnswer(1));
            Assert.IsNull(question.IconForAnswer(7));
            Assert.AreEqual("workflow", question.Icon);

            // A card shows what its answer alone names: the scene's members are on the question.
            Assert.IsEmpty(question.ToolCast(0));
            Assert.AreEqual("Cache", question.ToolCast(1).Single().Name);
            Assert.IsEmpty(question.ToolCast(9));
        }

        [Test]
        public void Actions_KeepKnownRefsOnly()
        {
            var action = First().Actions.Single();

            Assert.AreEqual("ev1", action.Ref);
            Assert.AreEqual("Ship the app", action.Name);
            Assert.AreEqual(1, action.Sequence);
            Assert.AreEqual("e1", action.ActorRef);
            Assert.IsEmpty(action.TargetRef);
            CollectionAssert.AreEqual(new[] { "e2" }, action.OutputRefs);
            Assert.AreEqual("send", action.Icon);
        }

        [Test]
        public void SecondCard_WithoutDecomposition_HasEmptyBriefingCastAndFlow()
        {
            var question = GameQuestionMapper.BuildQuestions(ParseRound())[1];

            Assert.IsEmpty(question.Briefing);
            Assert.IsNull(question.Ask);
            Assert.IsEmpty(question.Cast);
            Assert.IsEmpty(question.Actions);
            Assert.IsTrue(question.Flow.IsEmpty);
            Assert.IsNull(question.Icon);
        }

        [Test]
        public void Map_DropsUnknownNodesAndDanglingEdges()
        {
            var round = ParseRound();
            round.items[0].map = new GameFlowDto
            {
                nodes = new[]
                {
                    new FlowNodeDto { id = "a", type = "start", label = "Go", source_status = "explicit", entity_ids = new[] { "e1", "ghost" } },
                    new FlowNodeDto { id = "b", type = "blob" },
                    new FlowNodeDto { id = "a", type = "end" },
                    new FlowNodeDto { id = "q", type = "decision", is_question = true, source_status = "weird" },
                },
                edges = new[]
                {
                    new FlowEdgeDto { source = "a", target = "q", type = "yes", label = "ok" },
                    new FlowEdgeDto { source = "a", target = "b", type = "next" },
                    new FlowEdgeDto { source = "q", target = "a", type = "sideways" },
                },
            };

            var flow = GameQuestionMapper.BuildQuestions(round)[0].Flow;

            CollectionAssert.AreEqual(new[] { "a", "q" }, flow.Nodes.Select(n => n.Id));
            Assert.AreEqual(FlowNodeType.Start, flow.Nodes[0].Type);
            CollectionAssert.AreEqual(new[] { "e1" }, flow.Nodes[0].EntityRefs);
            Assert.IsTrue(flow.Nodes[1].IsQuestion);
            Assert.AreEqual(FlowStatus.Unknown, flow.Nodes[1].Status);
            Assert.AreEqual(FlowEdgeType.Yes, flow.Edges.Single().Type);
        }

        [Test]
        public void Objectives_KeepKeysInOrder_AndDropWhatIsNotAKey()
        {
            var round = ParseRound();
            round.items[0].objectives = new[] { "tools-errors", " context-facts ", "Not A Key", null, "tools-errors" };
            round.items[0].difficulty = 3;
            round.items[1].difficulty = 7;

            var questions = GameQuestionMapper.BuildQuestions(round);

            CollectionAssert.AreEqual(new[] { "tools-errors", "context-facts" }, questions[0].Objectives);
            Assert.AreEqual(3, questions[0].Difficulty);
            Assert.AreEqual(0, questions[1].Difficulty);
            Assert.IsEmpty(questions[1].Objectives, "missing in the JSON");
        }

        [Test]
        public void Story_Missing_IsEmpty_AndTheWholeTextIsShown()
        {
            var question = First();

            Assert.IsEmpty(question.Scenario);
            Assert.IsEmpty(question.Stage);
            Assert.IsEmpty(question.Stem);
            Assert.IsEmpty(question.Beat);
            Assert.AreEqual(question.Text, question.OriginalText);
        }

        [Test]
        public void Story_KeepsKeysAndTrimmedLines_AndDropsWhatIsNotAKey()
        {
            var round = ParseRound();
            round.items[0].scenario = " support ";
            round.items[0].stage = "incident";
            round.items[0].stem = " Which fix? ";
            round.items[0].beat = " Logs show a failure. ";
            round.items[1].scenario = "Not A Key";
            round.items[1].stage = "Also Not";

            var questions = GameQuestionMapper.BuildQuestions(round);

            Assert.AreEqual("support", questions[0].Scenario);
            Assert.AreEqual("incident", questions[0].Stage);
            Assert.AreEqual("Which fix?", questions[0].OriginalText);
            Assert.AreEqual("Logs show a failure.", questions[0].Beat);
            Assert.IsEmpty(questions[1].Scenario);
            Assert.IsEmpty(questions[1].Stage);
        }

        [Test]
        public void ToSearchResults_MapsWholeCards_AndRejectsRepeats()
        {
            var list = JsonUtility.FromJson<GameCardListDto>(RoundJson);

            var results = GameQuestionMapper.ToSearchResults(list);
            Assert.AreEqual("Team", results[0].Cast[0].Name);

            list.items[1].id = "q1";
            Assert.Throws<FormatException>(() => GameQuestionMapper.ToSearchResults(list));
        }

        [Test]
        public void ToAddedQuestion_ForAnotherQuestion_Throws()
        {
            var round = ParseRound();
            var added = new GameCardRoundQuestionDto { item = round.items[0] };

            Assert.AreEqual("q1", GameQuestionMapper.ToAddedQuestion(added, "q1").Id);
            Assert.Throws<FormatException>(() => GameQuestionMapper.ToAddedQuestion(added, "q2"));
            Assert.Throws<FormatException>(() => GameQuestionMapper.ToAddedQuestion(new GameCardRoundQuestionDto(), "q1"));
        }

        [Test]
        public void BuildBatch_ForAnotherIndex_Throws()
        {
            var batch = new GameCardBatchDto { batch_index = 2, items = ParseRound().items, remaining = 0 };

            Assert.AreEqual(2, GameQuestionMapper.BuildBatch(batch, 2).Count);
            Assert.Throws<FormatException>(() => GameQuestionMapper.BuildBatch(batch, 1));
        }

        [Test]
        public void ToReview_MapsTheReviewAndStoryLine()
        {
            var review = GameQuestionMapper.ToReview(ParseAnswer(), "q1", 0);

            Assert.AreEqual(1, review.CorrectIndex);
            Assert.AreEqual(0, review.AnsweredIndex);
            Assert.AreEqual("Because.", review.Review.Explanation);
            CollectionAssert.AreEqual(new[] { "fa", "fb", "fc", "fd" }, review.Review.Feedbacks);
            Assert.AreEqual("The app ships.", review.StoryLine);
        }

        [Test]
        public void ToReview_MapsEveryOutcome_Trimmed()
        {
            var resolution = GameQuestionMapper.ToReview(ParseAnswer(), "q1", 0).Resolution;

            Assert.AreEqual("Cache what must work offline.", resolution.Rule);
            CollectionAssert.AreEqual(new[] { "Works offline" }, resolution.AnswerRequirements);

            var picked = resolution.ForAnswer(0);
            Assert.IsFalse(picked.IsCorrect);
            Assert.AreEqual(AnswerImpact.Partial, picked.Impact);
            Assert.AreEqual("It half works.", picked.Consequence);
            Assert.AreEqual("Believes X.", picked.Misconception);
            Assert.AreEqual("Needs a network.", picked.Reason);
            CollectionAssert.AreEqual(new[] { "Works offline" }, picked.Gaps);

            var correct = resolution.ForAnswer(1);
            Assert.IsTrue(correct.IsCorrect);
            Assert.AreEqual(AnswerImpact.Success, correct.Impact);
            Assert.IsEmpty(correct.Misconception, "never next to a correct answer");
            Assert.AreEqual(AnswerImpact.Failure, resolution.ForAnswer(2).Impact);
            Assert.IsNull(resolution.ForAnswer(3));

            Assert.IsTrue(resolution.HasRequirementMatches);
            Assert.AreEqual(1, resolution.MetCount(1));
            Assert.AreEqual(0, resolution.MetCount(0));
        }

        [Test]
        public void ToReview_MisconceptionFor_FollowsTheAnswer()
        {
            var review = GameQuestionMapper.ToReview(ParseAnswer(), "q1", 0);

            Assert.AreEqual("Believes X.", review.MisconceptionFor(0));
            Assert.AreEqual("Believes Y.", review.MisconceptionFor(2));
            Assert.IsEmpty(review.MisconceptionFor(1));
            Assert.IsEmpty(review.MisconceptionFor(3));
        }

        [Test]
        public void ToReview_WithoutAnOutcomeList_UsesTheAnswersOwnOutcome()
        {
            var result = ParseAnswer();
            result.outcomes = Array.Empty<GameOutcomeDto>();

            var options = GameQuestionMapper.ToReview(result, "q1", 0).Resolution.Options;

            Assert.AreEqual(0, options.Single().Index);
            Assert.AreEqual("It half works.", options[0].Consequence);
        }

        [Test]
        public void ToReview_ResolutionFlow_MapsTheAddedSteps()
        {
            var flow = GameQuestionMapper.ToReview(ParseAnswer(), "q1", 0).Resolution.Flow;

            Assert.AreEqual("process_flow", flow.Primary);
            CollectionAssert.AreEqual(new[] { FlowNodeType.Start, FlowNodeType.Process }, flow.Nodes.Select(n => n.Type));
            Assert.IsTrue(flow.Nodes[1].Added);
            Assert.AreEqual(FlowStatus.Inferred, flow.Nodes[1].Status);
            CollectionAssert.AreEqual(new[] { "e1" }, flow.Nodes[1].EntityRefs);
            Assert.AreEqual("n2", flow.Edges.Single().Target);
        }

        [Test]
        public void ToReview_NothingResolved_IsEmpty()
        {
            // What the backend sends for a question it hasn't resolved.
            var result = JsonUtility.FromJson<GameCardAnswerResultDto>(@"{
                ""question_id"": ""q1"", ""answer_index"": 0, ""is_correct"": false, ""correct_index"": 1,
                ""outcome"": { ""index"": 0, ""verdict"": """" }, ""outcomes"": [], ""story_line"": """",
                ""review"": { ""explanation"": """", ""feedbacks"": [""a"", ""b""] },
                ""rule"": """", ""answer_requirements"": [], ""resolution_flow"": { ""nodes"": [], ""edges"": [] } }");

            var review = GameQuestionMapper.ToReview(result, "q1", 0);

            Assert.IsTrue(review.Resolution.IsEmpty);
            Assert.IsEmpty(review.StoryLine);
        }

        [Test]
        public void ToReview_Outcomes_DropBadOnes_AndAllWhenAVerdictContradictsTheKey()
        {
            var result = ParseAnswer();
            result.outcomes = new[]
            {
                new GameOutcomeDto { index = 0, verdict = "incorrect", reason = "kept", impact = "sideways" },
                new GameOutcomeDto { index = 0, verdict = "incorrect", reason = "repeated" },
                new GameOutcomeDto { index = -1, verdict = "incorrect" },
                new GameOutcomeDto { index = 2, verdict = "maybe" },
                null,
            };
            var options = GameQuestionMapper.ToReview(result, "q1", 0).Resolution.Options;
            Assert.AreEqual("kept", options.Single().Reason);
            Assert.AreEqual(AnswerImpact.Failure, options[0].Impact, "an unknown impact on a wrong answer");

            // Answer 0 is wrong (correct_index 1), so a "correct" verdict for it contradicts the key.
            result.outcomes = new[]
            {
                new GameOutcomeDto { index = 1, verdict = "correct" },
                new GameOutcomeDto { index = 0, verdict = "correct" },
            };
            var review = GameQuestionMapper.ToReview(result, "q1", 0);
            Assert.IsEmpty(review.Resolution.Options);
            Assert.AreEqual("Cache what must work offline.", review.Resolution.Rule);
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
            var result = JsonUtility.FromJson<GameCardAnswerResultDto>(
                "{\"question_id\":\"q1\",\"answer_index\":0,\"is_correct\":false,\"correct_index\":1}");

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
    }
}
