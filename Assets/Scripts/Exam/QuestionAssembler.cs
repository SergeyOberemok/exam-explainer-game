using System;
using System.Collections.Generic;
using System.Linq;

namespace ExamCards.Exam
{
    /// <summary>Joins original exam items with their simplified versions into playable questions.</summary>
    public static class QuestionAssembler
    {
        /// <exception cref="FormatException">An item is inconsistent; the message names the question id.</exception>
        public static IReadOnlyList<Question> Build(IEnumerable<ExamItem> originals, IEnumerable<ExamItem> simplifieds)
        {
            if (originals == null)
                throw new ArgumentNullException(nameof(originals));
            if (simplifieds == null)
                throw new ArgumentNullException(nameof(simplifieds));

            var simplifiedByQuestion = new Dictionary<string, ExamItem>();
            foreach (var simplified in simplifieds)
            {
                if (string.IsNullOrEmpty(simplified.QuestionId))
                    throw new FormatException($"Simplified item {simplified.Id}: missing question id.");
                if (!simplifiedByQuestion.TryAdd(simplified.QuestionId, simplified))
                    throw new FormatException($"Question {simplified.QuestionId}: more than one simplified version.");
            }

            var questions = originals.Select(original =>
            {
                simplifiedByQuestion.TryGetValue(original.Id, out var simplified);
                return BuildQuestion(original, simplified);
            }).ToList();

            var questionIds = new HashSet<string>(questions.Select(question => question.Id));
            var unmatchedId = simplifiedByQuestion.Keys.FirstOrDefault(id => !questionIds.Contains(id));
            if (unmatchedId != null)
                throw new FormatException($"Question {unmatchedId}: simplified version has no original.");

            return questions;
        }

        static Question BuildQuestion(ExamItem original, ExamItem simplified)
        {
            ValidatePrompt(original.Id, original.Prompt);
            var correctIndex = ToCorrectIndex(original);

            if (simplified != null)
            {
                ValidatePrompt(original.Id, simplified.Prompt);
                if (simplified.Prompt.Answers.Count != original.Prompt.Answers.Count)
                    throw new FormatException($"Question {original.Id}: simplified version has a different number of answers.");
                if (ToCorrectIndex(simplified, original.Id) != correctIndex)
                    throw new FormatException($"Question {original.Id}: simplified version has a different correct answer.");
            }

            return new Question
            {
                Id = original.Id,
                Section = original.Section,
                Source = original.Source,
                Original = original.Prompt,
                Simplified = simplified?.Prompt,
                CorrectIndex = correctIndex,
            };
        }

        static void ValidatePrompt(string questionId, QuestionPrompt prompt)
        {
            if (prompt == null || prompt.Answers.Count == 0)
                throw new FormatException($"Question {questionId}: no answers.");
            if (prompt.Answers.Count > Question.MaxAnswers)
                throw new FormatException($"Question {questionId}: {prompt.Answers.Count} answers, but at most {Question.MaxAnswers} are supported.");
            if (prompt.Feedbacks.Count != prompt.Answers.Count)
                throw new FormatException($"Question {questionId}: {prompt.Answers.Count} answers but {prompt.Feedbacks.Count} feedbacks.");
        }

        static int ToCorrectIndex(ExamItem item, string questionId = null)
        {
            questionId ??= item.Id;
            if (item.CorrectResponse.Count != 1)
                throw new FormatException($"Question {questionId}: expected one correct answer, found {item.CorrectResponse.Count}.");

            var letter = item.CorrectResponse[0];
            var index = letter != null && letter.Length == 1 ? char.ToLowerInvariant(letter[0]) - 'a' : -1;
            if (index < 0 || index >= item.Prompt.Answers.Count)
                throw new FormatException($"Question {questionId}: invalid correct answer '{letter}'.");
            return index;
        }
    }
}
