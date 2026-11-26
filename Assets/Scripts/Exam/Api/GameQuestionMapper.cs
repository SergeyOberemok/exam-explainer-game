using System;
using System.Collections.Generic;
using System.Linq;

namespace ExamCards.Exam.Api
{
    /// <summary>Turns backend <c>/game</c> responses into the game's models, rejecting inconsistent data.</summary>
    public static class GameQuestionMapper
    {
        /// <summary>The <see cref="Question.Source"/> of every question that came from the API.</summary>
        public const string Source = "api";

        /// <returns>The round's questions in play order, with their simplified versions joined by id.</returns>
        /// <exception cref="FormatException">The round is inconsistent; the message names the question id.</exception>
        public static IReadOnlyList<Question> BuildQuestions(GameRoundDto round)
        {
            if (round == null)
                throw new FormatException("The round response is empty.");
            if (string.IsNullOrEmpty(round.round_id))
                throw new FormatException("The round has no id.");

            return JoinQuestions(round.items, round.simplified);
        }

        /// <returns>The batch's questions in order, with their simplified versions joined by id.</returns>
        /// <exception cref="FormatException">The batch is for another index, or inconsistent.</exception>
        public static IReadOnlyList<Question> BuildBatch(GameRoundBatchDto batch, int batchIndex)
        {
            if (batch == null)
                throw new FormatException($"Batch {batchIndex}: the response is empty.");
            if (batch.batch_index != batchIndex)
                throw new FormatException($"Batch {batchIndex}: the response is for batch {batch.batch_index}.");
            return JoinQuestions(batch.items, batch.simplified);
        }

        static IReadOnlyList<Question> JoinQuestions(GameQuestionDto[] items, GameQuestionDto[] simplifiedItems)
        {
            var originals = items ?? Array.Empty<GameQuestionDto>();
            var originalIds = new HashSet<string>();
            foreach (var original in originals)
            {
                if (original == null || string.IsNullOrEmpty(original.id))
                    throw new FormatException("A question has no id.");
                if (original.simplified)
                    throw new FormatException($"Question {original.id}: listed as an original but marked simplified.");
                if (!originalIds.Add(original.id))
                    throw new FormatException($"Question {original.id}: returned more than once.");
            }

            var simplifiedById = new Dictionary<string, GameQuestionDto>();
            foreach (var simplified in simplifiedItems ?? Array.Empty<GameQuestionDto>())
            {
                if (simplified == null || string.IsNullOrEmpty(simplified.id))
                    throw new FormatException("A simplified question has no id.");
                if (!simplified.simplified)
                    throw new FormatException($"Question {simplified.id}: simplified version is not marked simplified.");
                if (!originalIds.Contains(simplified.id))
                    throw new FormatException($"Question {simplified.id}: simplified version has no original in this round.");
                if (!simplifiedById.TryAdd(simplified.id, simplified))
                    throw new FormatException($"Question {simplified.id}: more than one simplified version.");
            }

            return originals.Select(original =>
            {
                simplifiedById.TryGetValue(original.id, out var simplified);
                return ToQuestion(original, simplified);
            }).ToList();
        }

        /// <returns>The search results in their order; they never have simplified text.</returns>
        /// <exception cref="FormatException">A result is inconsistent, or listed twice.</exception>
        public static IReadOnlyList<Question> ToSearchResults(GameQuestionListDto list)
        {
            if (list == null)
                throw new FormatException("The search response is empty.");

            var ids = new HashSet<string>();
            return (list.items ?? Array.Empty<GameQuestionDto>()).Select(item =>
            {
                CheckOriginal(item);
                if (!ids.Add(item.id))
                    throw new FormatException($"Question {item.id}: found more than once.");
                return ToQuestion(item, null);
            }).ToList();
        }

        /// <exception cref="FormatException">The response is for another question, or inconsistent.</exception>
        public static Question ToAddedQuestion(GameRoundQuestionDto result, string questionId)
        {
            if (result?.item == null)
                throw new FormatException($"Question {questionId}: the add-question response is empty.");
            CheckOriginal(result.item);
            if (result.item.id != questionId)
                throw new FormatException($"Question {questionId}: the add-question response is for question {result.item.id}.");

            var simplified = result.simplified ?? Array.Empty<GameQuestionDto>();
            if (simplified.Length > 1)
                throw new FormatException($"Question {questionId}: more than one simplified version.");
            var version = simplified.Length == 1 ? simplified[0] : null;
            if (version != null && (version.id != questionId || !version.simplified))
                throw new FormatException($"Question {questionId}: the simplified version doesn't match the question.");
            return ToQuestion(result.item, version);
        }

        static void CheckOriginal(GameQuestionDto dto)
        {
            if (dto == null || string.IsNullOrEmpty(dto.id))
                throw new FormatException("A question has no id.");
            if (dto.simplified)
                throw new FormatException($"Question {dto.id}: listed as an original but marked simplified.");
        }

        static Question ToQuestion(GameQuestionDto original, GameQuestionDto simplified)
        {
            var question = new Question
            {
                Id = original.id,
                Section = original.section,
                Source = Source,
                Original = ToPrompt(original),
                Simplified = simplified == null ? null : ToPrompt(simplified),
                Objectives = ToObjectives(original.objectives),
                Difficulty = original.difficulty >= 1 && original.difficulty <= 3 ? original.difficulty : 0,
                // Like objectives, the story only describes a question: a bad key is dropped, never fatal.
                Scenario = EntityIconResolver.CleanKey(original.scenario) ?? string.Empty,
                Stage = EntityIconResolver.CleanKey(original.stage) ?? string.Empty,
                Stem = original.stem?.Trim() ?? string.Empty,
                Beat = original.beat?.Trim() ?? string.Empty,
                // Only originals have parts; the rephrase gets the same flow, so it stands in for a missing one.
                Parts = ToParts(original.parts),
                Flow = ToFlow(original.flow) is { IsEmpty: false } flow ? flow : ToFlow(simplified?.flow),
            };
            QuestionValidator.ValidateQuestion(question);
            return question;
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

        /// <param name="answerIndex">The answer sent; null for a recorded answer, which may be any.</param>
        /// <exception cref="FormatException">The response is for another answer, incomplete, or contradicts itself.</exception>
        public static AnswerReview ToReview(GameAnswerResultDto result, string questionId, int? answerIndex)
        {
            if (result == null)
                throw new FormatException($"Question {questionId}: the answer response is empty.");
            if (result.question_id != questionId || (answerIndex.HasValue && result.answer_index != answerIndex.Value))
                throw new FormatException($"Question {questionId}: the answer response is for question {result.question_id}, answer {result.answer_index}.");
            if (result.is_correct != (result.correct_index == result.answer_index))
                throw new FormatException($"Question {questionId}: the answer response contradicts itself about the correct answer.");
            // JsonUtility fills a missing field with its default (correct_index 0, empty review) instead of failing,
            // and every playable question has at least two answers, so an empty feedback list means a missing review.
            if (result.original?.feedbacks == null || result.original.feedbacks.Length == 0)
                throw new FormatException($"Question {questionId}: the answer response has no review.");

            // QuestionValidator.ValidateReview checks the index range and feedback counts against the question.
            return new AnswerReview
            {
                AnsweredIndex = result.answer_index,
                CorrectIndex = result.correct_index,
                Original = ToPromptReview(result.original),
                Resolution = ToResolution(result.resolution, result.correct_index),
                // The backend sends it only for a wrong answer; never show one next to a correct answer.
                Misconception = result.is_correct ? string.Empty : result.misconception?.Trim() ?? string.Empty,
                Outcome = result.outcome?.Trim() ?? string.Empty,
            };
        }

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

        static QuestionPrompt ToPrompt(GameQuestionDto dto)
        {
            var answers = (dto.answers ?? Array.Empty<GameAnswerDto>())
                .Where(answer => answer != null)
                .OrderBy(answer => answer.index)
                .ToArray();
            for (var i = 0; i < answers.Length; i++)
            {
                if (answers[i].index != i)
                    throw new FormatException($"Question {dto.id}: answer indices are not 0..{answers.Length - 1}.");
            }

            return new QuestionPrompt
            {
                Question = dto.question,
                Answers = answers.Select(answer => answer.text ?? string.Empty).ToArray(),
                Entities = ToEntities(dto.entities, answers.Length),
            };
        }

        /// <summary>
        /// Entities only decorate a question, so bad ones are dropped instead of failing the round: an entity
        /// without a name or with an unknown kind, the answers' entities and icons when they don't line up with the
        /// answers, and anything that isn't an icon key.
        /// </summary>
        static PromptEntities ToEntities(GameEntitiesDto dto, int answerCount)
        {
            if (dto == null)
                return PromptEntities.Empty;

            var question = ToEntityList(dto.question);
            var questionIcon = EntityIconResolver.CleanKey(dto.question_icon);
            var aligned = dto.answers != null && dto.answers.Length == answerCount;
            var answers = aligned
                ? dto.answers.Select(answer => ToEntityList(answer?.entities)).ToArray()
                : Array.Empty<IReadOnlyList<VisualEntity>>();
            var answerIcons = aligned
                ? dto.answers.Select(answer => EntityIconResolver.CleanKey(answer?.icon)).ToArray()
                : Array.Empty<string>();
            if (answerIcons.All(icon => icon == null))
                answerIcons = Array.Empty<string>();
            if (question.Count == 0 && answers.All(list => list.Count == 0) && questionIcon == null && answerIcons.Length == 0)
                return PromptEntities.Empty;
            return new PromptEntities { QuestionIcon = questionIcon, Question = question, Answers = answers, AnswerIcons = answerIcons };
        }

        static IReadOnlyList<VisualEntity> ToEntityList(GameEntityDto[] dtos)
        {
            if (dtos == null || dtos.Length == 0)
                return Array.Empty<VisualEntity>();

            var entities = new List<VisualEntity>(dtos.Length);
            foreach (var dto in dtos)
            {
                if (dto == null || string.IsNullOrWhiteSpace(dto.name) || !TryParseKind(dto.kind, out var kind))
                    continue;
                entities.Add(new VisualEntity
                {
                    Name = dto.name.Trim(),
                    Kind = kind,
                    Aliases = (dto.aliases ?? Array.Empty<string>())
                        .Where(alias => !string.IsNullOrWhiteSpace(alias))
                        .Select(alias => alias.Trim())
                        .ToArray(),
                    Icon = EntityIconResolver.CleanKey(dto.icon),
                    Ref = dto.@ref?.Trim() ?? string.Empty,
                });
            }
            return entities;
        }

        static bool TryParseKind(string value, out EntityKind kind)
        {
            switch (value)
            {
                case "actor": kind = EntityKind.Actor; return true;
                case "object": kind = EntityKind.Object; return true;
                case "action": kind = EntityKind.Action; return true;
                default: kind = default; return false;
            }
        }

        // Parts, flows and resolutions only explain a question, so, like entities, bad pieces are dropped
        // instead of failing the round.

        static IReadOnlyList<QuestionPart> ToParts(GamePartDto[] dtos)
        {
            var parts = new List<QuestionPart>();
            foreach (var dto in dtos ?? Array.Empty<GamePartDto>())
            {
                if (dto == null || string.IsNullOrWhiteSpace(dto.text) || !TryParsePartRole(dto.role, out var role))
                    continue;
                parts.Add(new QuestionPart { Role = role, Text = dto.text.Trim(), EntityRefs = CleanList(dto.entity_ids) });
            }
            return parts;
        }

        /// <summary>
        /// Drops nodes without a unique id or a known type, and edges that don't join two kept nodes or have an
        /// unknown type. An unknown status counts as <see cref="FlowStatus.Unknown"/>.
        /// </summary>
        static DecisionFlow ToFlow(GameFlowDto dto)
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
                    EntityRefs = CleanList(node.entity_ids),
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

        /// <summary>
        /// Drops options with a negative or repeated index or an unknown verdict, and all of them if a verdict
        /// contradicts <paramref name="correctIndex"/>: reasons that disagree with the key would mislead.
        /// An option for an answer the question doesn't have is harmless, since the screen asks by index.
        /// </summary>
        static Resolution ToResolution(GameResolutionDto dto, int correctIndex)
        {
            if (dto == null)
                return Resolution.Empty;

            var options = new List<OptionEvaluation>();
            var indices = new HashSet<int>();
            foreach (var option in dto.options ?? Array.Empty<OptionEvaluationDto>())
            {
                if (option == null || option.index < 0 || !indices.Add(option.index))
                    continue;
                bool isCorrect;
                if (option.verdict == "correct")
                    isCorrect = true;
                else if (option.verdict == "incorrect")
                    isCorrect = false;
                else
                    continue;
                if (isCorrect != (option.index == correctIndex))
                {
                    options.Clear();
                    break;
                }
                options.Add(new OptionEvaluation
                {
                    Index = option.index,
                    IsCorrect = isCorrect,
                    SupportedRequirements = CleanList(option.supported_requirements),
                    Gaps = CleanList(option.gaps),
                    Reason = option.reason?.Trim() ?? string.Empty,
                });
            }

            var resolution = new Resolution
            {
                AnswerRequirements = CleanList(dto.answer_requirements),
                Flow = ToFlow(dto.flow),
                Options = options,
                Rule = dto.rule?.Trim() ?? string.Empty,
            };
            return resolution.IsEmpty ? Resolution.Empty : resolution;
        }

        static IReadOnlyList<string> CleanList(string[] values) =>
            (values ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .ToArray();

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

        static PromptReview ToPromptReview(GameReviewDto dto) => new PromptReview
        {
            Explanation = dto?.explanation ?? string.Empty,
            Feedbacks = (dto?.feedbacks ?? Array.Empty<string>()).Select(feedback => feedback ?? string.Empty).ToArray(),
        };
    }
}
