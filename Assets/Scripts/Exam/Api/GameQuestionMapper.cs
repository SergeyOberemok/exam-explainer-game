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

            var originals = round.items ?? Array.Empty<GameQuestionDto>();
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
            foreach (var simplified in round.simplified ?? Array.Empty<GameQuestionDto>())
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
            };
            QuestionValidator.ValidateQuestion(question);
            return question;
        }

        /// <exception cref="FormatException">The response is for another answer, or contradicts itself.</exception>
        public static AnswerReview ToReview(GameAnswerResultDto result, string questionId, int answerIndex)
        {
            if (result == null)
                throw new FormatException($"Question {questionId}: the answer response is empty.");
            if (result.question_id != questionId || result.answer_index != answerIndex)
                throw new FormatException($"Question {questionId}: the answer response is for question {result.question_id}, answer {result.answer_index}.");
            if (result.is_correct != (result.correct_index == answerIndex))
                throw new FormatException($"Question {questionId}: the answer response contradicts itself about the correct answer.");

            // QuestionValidator.ValidateReview checks the index range and feedback counts against the question.
            return new AnswerReview
            {
                CorrectIndex = result.correct_index,
                Original = ToPromptReview(result.original),
                Simplified = result.has_simplified ? ToPromptReview(result.simplified) : null,
            };
        }

        /// <exception cref="FormatException">The response is for another question.</exception>
        public static IReadOnlyList<int> ToEliminated(GameFiftyFiftyResultDto result, string questionId)
        {
            if (result == null)
                throw new FormatException($"Question {questionId}: the 50/50 response is empty.");
            if (result.question_id != questionId)
                throw new FormatException($"Question {questionId}: the 50/50 response is for question {result.question_id}.");
            return result.eliminated_indices ?? Array.Empty<int>();
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
            };
        }

        static PromptReview ToPromptReview(GameReviewDto dto) => new PromptReview
        {
            Explanation = dto?.explanation ?? string.Empty,
            Feedbacks = (dto?.feedbacks ?? Array.Empty<string>()).Select(feedback => feedback ?? string.Empty).ToArray(),
        };
    }
}
