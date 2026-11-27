using System;
using System.Collections.Generic;
using System.Linq;

namespace ExamCards.Exam.Api
{
    /// <summary>
    /// Turns backend <c>/v2/game</c> responses into the game's models. What decides play (ids, answers, the key) is
    /// checked strictly and a bad response fails; what only describes a question (cast, briefing, icons, flows,
    /// outcomes) is cleaned leniently, dropping the bad pieces, so it never fails a round.
    /// </summary>
    public static class GameQuestionMapper
    {
        /// <summary>The <see cref="Question.Source"/> of every question that came from the API.</summary>
        public const string Source = "api";

        /// <returns>The round's questions in play order.</returns>
        /// <exception cref="FormatException">The round is inconsistent; the message names the question id.</exception>
        public static IReadOnlyList<Question> BuildQuestions(GameCardRoundDto round)
        {
            if (round == null)
                throw new FormatException("The round response is empty.");
            if (string.IsNullOrEmpty(round.round_id))
                throw new FormatException("The round has no id.");
            return ToQuestions(round.items, "returned");
        }

        /// <returns>The batch's questions in order.</returns>
        /// <exception cref="FormatException">The batch is for another index, or inconsistent.</exception>
        public static IReadOnlyList<Question> BuildBatch(GameCardBatchDto batch, int batchIndex)
        {
            if (batch == null)
                throw new FormatException($"Batch {batchIndex}: the response is empty.");
            if (batch.batch_index != batchIndex)
                throw new FormatException($"Batch {batchIndex}: the response is for batch {batch.batch_index}.");
            return ToQuestions(batch.items, "returned");
        }

        /// <returns>The search results in their order.</returns>
        /// <exception cref="FormatException">A result is inconsistent, or listed twice.</exception>
        public static IReadOnlyList<Question> ToSearchResults(GameCardListDto list)
        {
            if (list == null)
                throw new FormatException("The search response is empty.");
            return ToQuestions(list.items, "found");
        }

        /// <exception cref="FormatException">The response is for another question, or inconsistent.</exception>
        public static Question ToAddedQuestion(GameCardRoundQuestionDto result, string questionId)
        {
            if (result?.item == null)
                throw new FormatException($"Question {questionId}: the add-question response is empty.");
            if (result.item.id != questionId)
                throw new FormatException($"Question {questionId}: the add-question response is for question {result.item.id}.");
            return ToQuestion(result.item);
        }

        static IReadOnlyList<Question> ToQuestions(GameCardDto[] items, string verb)
        {
            var ids = new HashSet<string>();
            return (items ?? Array.Empty<GameCardDto>()).Select(item =>
            {
                var question = ToQuestion(item);
                if (!ids.Add(question.Id))
                    throw new FormatException($"Question {question.Id}: {verb} more than once.");
                return question;
            }).ToList();
        }

        static Question ToQuestion(GameCardDto card)
        {
            if (card == null || string.IsNullOrEmpty(card.id))
                throw new FormatException("A question has no id.");

            var cast = ToCast(card.cast);
            var refs = new HashSet<string>(cast.Select(member => member.Ref));
            var question = new Question
            {
                Id = card.id,
                Section = card.section,
                Source = Source,
                Text = card.question ?? string.Empty,
                Tools = ToTools(card, refs),
                Objectives = ToObjectives(card.objectives),
                Requirements = CleanList(card.requirements),
                Difficulty = card.difficulty >= 1 && card.difficulty <= 3 ? card.difficulty : 0,
                // Like objectives, the story only describes a question: a bad key is dropped, never fatal.
                Scenario = EntityIconResolver.CleanKey(card.scenario) ?? string.Empty,
                Stage = EntityIconResolver.CleanKey(card.stage) ?? string.Empty,
                Stem = card.stem?.Trim() ?? string.Empty,
                Beat = card.beat?.Trim() ?? string.Empty,
                Icon = EntityIconResolver.CleanKey(card.icon),
                Briefing = ToBriefing(card.briefing, refs),
                Ask = ToAsk(card.ask, refs),
                Cast = cast,
                Actions = ToActions(card.actions, refs),
                Flow = ToFlow(card.map, refs),
            };
            QuestionValidator.ValidateQuestion(question);
            return question;
        }

        /// <summary>The answers in index order; indices must be 0..n-1 (the validator checks the count).</summary>
        static IReadOnlyList<AnswerTool> ToTools(GameCardDto card, HashSet<string> refs)
        {
            var tools = (card.tools ?? Array.Empty<GameToolDto>())
                .Where(tool => tool != null)
                .OrderBy(tool => tool.index)
                .ToArray();
            for (var i = 0; i < tools.Length; i++)
            {
                if (tools[i].index != i)
                    throw new FormatException($"Question {card.id}: answer indices are not 0..{tools.Length - 1}.");
            }
            return tools.Select(tool => new AnswerTool
            {
                Index = tool.index,
                Text = tool.text ?? string.Empty,
                Label = tool.label?.Trim() ?? string.Empty,
                Icon = EntityIconResolver.CleanKey(tool.icon),
                ComponentRefs = KnownRefs(tool.components, refs),
            }).ToArray();
        }

        /// <summary>
        /// Objectives only describe a question, so anything that isn't a key (see
        /// <see cref="EntityIconResolver.CleanKey"/>) or repeats is dropped instead of failing the round.
        /// </summary>
        static IReadOnlyList<string> ToObjectives(string[] keys) =>
            (keys ?? Array.Empty<string>())
                .Select(EntityIconResolver.CleanKey)
                .Where(key => key != null)
                .Distinct()
                .ToArray();

        // Cast, briefing, actions, flows and outcomes only explain a question, so bad pieces are dropped instead
        // of failing the round: a member without a ref or a name (or with a repeated ref), a ref that names no
        // member, an unknown role or flow type, and anything that isn't an icon key.

        static IReadOnlyList<CastMember> ToCast(GameCastMemberDto[] dtos)
        {
            var cast = new List<CastMember>();
            var refs = new HashSet<string>();
            foreach (var dto in dtos ?? Array.Empty<GameCastMemberDto>())
            {
                var reference = dto?.@ref?.Trim();
                if (string.IsNullOrEmpty(reference) || string.IsNullOrWhiteSpace(dto.name) || !refs.Add(reference))
                    continue;
                cast.Add(new CastMember
                {
                    Ref = reference,
                    Name = dto.name.Trim(),
                    Type = ToCastType(dto.type),
                    Aliases = CleanList(dto.aliases),
                    Icon = EntityIconResolver.CleanKey(dto.icon),
                    InScene = dto.scene,
                });
            }
            return cast;
        }

        static IReadOnlyList<QuestionPart> ToBriefing(GamePartDto[] dtos, HashSet<string> refs)
        {
            var parts = new List<QuestionPart>();
            foreach (var dto in dtos ?? Array.Empty<GamePartDto>())
            {
                // The ask has a field of its own; one in the briefing would show twice.
                if (dto == null || string.IsNullOrWhiteSpace(dto.text) || !TryParsePartRole(dto.role, out var role)
                    || role == PartRole.Ask)
                    continue;
                parts.Add(new QuestionPart { Role = role, Text = dto.text.Trim(), EntityRefs = KnownRefs(dto.entity_ids, refs) });
            }
            return parts;
        }

        static QuestionPart ToAsk(GameAskDto dto, HashSet<string> refs) =>
            string.IsNullOrWhiteSpace(dto?.text)
                ? null
                : new QuestionPart { Role = PartRole.Ask, Text = dto.text.Trim(), EntityRefs = KnownRefs(dto.entity_ids, refs) };

        static IReadOnlyList<QuestionAction> ToActions(GameActionDto[] dtos, HashSet<string> refs)
        {
            var actions = new List<QuestionAction>();
            foreach (var dto in dtos ?? Array.Empty<GameActionDto>())
            {
                if (dto == null || string.IsNullOrWhiteSpace(dto.name))
                    continue;
                actions.Add(new QuestionAction
                {
                    Ref = dto.@ref?.Trim() ?? string.Empty,
                    Name = dto.name.Trim(),
                    Sequence = dto.sequence,
                    ActorRef = KnownRef(dto.actor, refs),
                    TargetRef = KnownRef(dto.target, refs),
                    InputRefs = KnownRefs(dto.inputs, refs),
                    OutputRefs = KnownRefs(dto.outputs, refs),
                    Icon = EntityIconResolver.CleanKey(dto.icon),
                });
            }
            // Ordered by sequence; an action without one (0) goes last, in the order it came.
            return actions.OrderBy(action => action.Sequence <= 0 ? int.MaxValue : action.Sequence).ToArray();
        }

        /// <summary>
        /// Drops nodes without a unique id or a known type, and edges that don't join two kept nodes or have an
        /// unknown type. An unknown status counts as <see cref="FlowStatus.Unknown"/>.
        /// </summary>
        static DecisionFlow ToFlow(GameFlowDto dto, HashSet<string> refs)
        {
            if (dto?.nodes == null || dto.nodes.Length == 0)
                return DecisionFlow.Empty;

            var nodes = new List<FlowNode>();
            var ids = new HashSet<string>();
            foreach (var node in dto.nodes)
            {
                var id = node?.id?.Trim();
                if (string.IsNullOrEmpty(id) || !TryParseNodeType(node.type, out var type) || !ids.Add(id))
                    continue;
                nodes.Add(new FlowNode
                {
                    Id = id,
                    Type = type,
                    Label = node.label?.Trim() ?? string.Empty,
                    EntityRefs = refs == null ? CleanList(node.entity_ids) : KnownRefs(node.entity_ids, refs),
                    Icon = EntityIconResolver.CleanKey(node.icon),
                    ConditionId = node.condition_id?.Trim() ?? string.Empty,
                    Status = ToStatus(node.source_status),
                    IsQuestion = node.is_question,
                    Added = node.added,
                });
            }
            if (nodes.Count == 0)
                return DecisionFlow.Empty;

            var edges = new List<FlowEdge>();
            foreach (var edge in dto.edges ?? Array.Empty<FlowEdgeDto>())
            {
                var source = edge?.source?.Trim();
                var target = edge?.target?.Trim();
                if (source == null || target == null || !ids.Contains(source) || !ids.Contains(target)
                    || !TryParseEdgeType(edge.type, out var type))
                    continue;
                edges.Add(new FlowEdge
                {
                    Source = source,
                    Target = target,
                    Type = type,
                    Label = edge.label?.Trim() ?? string.Empty,
                    Status = ToStatus(edge.source_status),
                    Added = edge.added,
                });
            }

            return new DecisionFlow
            {
                Primary = dto.primary?.Trim() ?? string.Empty,
                Secondary = CleanList(dto.secondary),
                Reason = dto.reason?.Trim() ?? string.Empty,
                Trigger = dto.trigger?.Trim() ?? string.Empty,
                Nodes = nodes,
                Edges = edges,
            };
        }

        /// <param name="answerIndex">The answer sent; null for a recorded answer, which may be any.</param>
        /// <exception cref="FormatException">The response is for another answer, incomplete, or contradicts itself.</exception>
        public static AnswerReview ToReview(GameCardAnswerResultDto result, string questionId, int? answerIndex)
        {
            if (result == null)
                throw new FormatException($"Question {questionId}: the answer response is empty.");
            if (result.question_id != questionId || (answerIndex.HasValue && result.answer_index != answerIndex.Value))
                throw new FormatException($"Question {questionId}: the answer response is for question {result.question_id}, answer {result.answer_index}.");
            if (result.is_correct != (result.correct_index == result.answer_index))
                throw new FormatException($"Question {questionId}: the answer response contradicts itself about the correct answer.");
            // JsonUtility fills a missing field with its default (correct_index 0, empty review) instead of failing,
            // and every playable question has at least two answers, so an empty feedback list means a missing review.
            if (result.review?.feedbacks == null || result.review.feedbacks.Length == 0)
                throw new FormatException($"Question {questionId}: the answer response has no review.");

            // QuestionValidator.ValidateReview checks the index range and feedback counts against the question.
            var resolution = new Resolution
            {
                AnswerRequirements = CleanList(result.answer_requirements),
                // The cast isn't in the answer; the question's own flow already had its refs cleaned.
                Flow = ToFlow(result.resolution_flow, null),
                Options = ToOutcomes(result),
                Rule = result.rule?.Trim() ?? string.Empty,
            };
            return new AnswerReview
            {
                AnsweredIndex = result.answer_index,
                CorrectIndex = result.correct_index,
                Review = new PromptReview
                {
                    Explanation = result.review.explanation ?? string.Empty,
                    Feedbacks = result.review.feedbacks.Select(feedback => feedback ?? string.Empty).ToArray(),
                },
                Resolution = resolution.IsEmpty ? Resolution.Empty : resolution,
                StoryLine = result.story_line?.Trim() ?? string.Empty,
            };
        }

        /// <summary>
        /// Every answer's outcome, or only the answer's own from a backend that sends no list. Drops outcomes with
        /// a negative or repeated index or an unknown verdict, and all of them if a verdict contradicts
        /// <c>correct_index</c>: reasons that disagree with the key would mislead. One for an answer the
        /// question doesn't have is harmless, since the screen asks by index.
        /// </summary>
        static IReadOnlyList<AnswerOutcome> ToOutcomes(GameCardAnswerResultDto result)
        {
            var dtos = result.outcomes != null && result.outcomes.Length > 0
                ? result.outcomes
                : result.outcome != null && !string.IsNullOrEmpty(result.outcome.verdict)
                    ? new[] { result.outcome }
                    : Array.Empty<GameOutcomeDto>();

            var outcomes = new List<AnswerOutcome>();
            var indices = new HashSet<int>();
            foreach (var dto in dtos)
            {
                if (dto == null || dto.index < 0 || !indices.Add(dto.index))
                    continue;
                bool isCorrect;
                if (dto.verdict == "correct")
                    isCorrect = true;
                else if (dto.verdict == "incorrect")
                    isCorrect = false;
                else
                    continue;
                if (isCorrect != (dto.index == result.correct_index))
                    return Array.Empty<AnswerOutcome>();
                outcomes.Add(new AnswerOutcome
                {
                    Index = dto.index,
                    IsCorrect = isCorrect,
                    Impact = ToImpact(dto.impact, isCorrect),
                    Consequence = dto.consequence?.Trim() ?? string.Empty,
                    // Never shown next to a correct answer.
                    Misconception = isCorrect ? string.Empty : dto.misconception?.Trim() ?? string.Empty,
                    Reason = dto.reason?.Trim() ?? string.Empty,
                    SupportedRequirements = CleanList(dto.supported_requirements),
                    Gaps = CleanList(dto.gaps),
                });
            }
            return outcomes;
        }

        /// <summary>The impact the verdict allows: a correct answer is a success, a wrong one partial or a failure.</summary>
        static AnswerImpact ToImpact(string value, bool isCorrect) =>
            isCorrect ? AnswerImpact.Success
            : value == "partial" ? AnswerImpact.Partial
            : AnswerImpact.Failure;

        /// <exception cref="FormatException">The response is for another question.</exception>
        public static FiftyFiftyResult ToFiftyFifty(GameFiftyFiftyResultDto result, string questionId)
        {
            if (result == null)
                throw new FormatException($"Question {questionId}: the 50/50 response is empty.");
            if (result.question_id != questionId)
                throw new FormatException($"Question {questionId}: the 50/50 response is for question {result.question_id}.");
            // QuestionValidator.ValidateFiftyFifty checks the indices and the count against the question.
            return new FiftyFiftyResult(result.eliminated_indices ?? Array.Empty<int>(), result.fifty_fifty_remaining);
        }

        static string KnownRef(string value, HashSet<string> refs)
        {
            var reference = value?.Trim();
            return !string.IsNullOrEmpty(reference) && refs.Contains(reference) ? reference : string.Empty;
        }

        static IReadOnlyList<string> KnownRefs(string[] values, HashSet<string> refs) =>
            CleanList(values).Where(refs.Contains).Distinct().ToArray();

        static IReadOnlyList<string> CleanList(string[] values) =>
            (values ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .ToArray();

        static CastType ToCastType(string value) => value switch
        {
            "actor" => CastType.Actor,
            "system" => CastType.System,
            "knowledge_source" => CastType.KnowledgeSource,
            "artifact" => CastType.Artifact,
            "external_service" => CastType.ExternalService,
            "concept" => CastType.Concept,
            _ => CastType.Object,
        };

        static bool TryParsePartRole(string value, out PartRole role)
        {
            switch (value)
            {
                case "context": role = PartRole.Context; return true;
                case "current_state": role = PartRole.CurrentState; return true;
                case "problem": role = PartRole.Problem; return true;
                case "consequence": role = PartRole.Consequence; return true;
                case "goal": role = PartRole.Goal; return true;
                case "constraint": role = PartRole.Constraint; return true;
                case "ask": role = PartRole.Ask; return true;
                default: role = default; return false;
            }
        }

        static bool TryParseNodeType(string value, out FlowNodeType type)
        {
            switch (value)
            {
                case "start": type = FlowNodeType.Start; return true;
                case "process": type = FlowNodeType.Process; return true;
                case "decision": type = FlowNodeType.Decision; return true;
                case "input": type = FlowNodeType.Input; return true;
                case "output": type = FlowNodeType.Output; return true;
                case "human_review": type = FlowNodeType.HumanReview; return true;
                case "end": type = FlowNodeType.End; return true;
                default: type = default; return false;
            }
        }

        static bool TryParseEdgeType(string value, out FlowEdgeType type)
        {
            switch (value)
            {
                case "next": type = FlowEdgeType.Next; return true;
                case "yes": type = FlowEdgeType.Yes; return true;
                case "no": type = FlowEdgeType.No; return true;
                case "unknown": type = FlowEdgeType.Unknown; return true;
                case "approved": type = FlowEdgeType.Approved; return true;
                case "rejected": type = FlowEdgeType.Rejected; return true;
                case "custom": type = FlowEdgeType.Custom; return true;
                default: type = default; return false;
            }
        }

        static FlowStatus ToStatus(string value) =>
            value == "explicit" ? FlowStatus.Explicit
            : value == "inferred" ? FlowStatus.Inferred
            : FlowStatus.Unknown;
    }
}
