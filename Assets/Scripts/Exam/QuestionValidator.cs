using System;
using System.Collections.Generic;

namespace ExamCards.Exam
{
    /// <summary>
    /// Checks the data a question source hands the game, so a broken question fails loudly instead of
    /// misbehaving on screen. Every message names the question.
    /// </summary>
    public static class QuestionValidator
    {
        /// <exception cref="FormatException">The question can't be shown.</exception>
        public static void ValidateQuestion(Question question)
        {
            if (question == null || string.IsNullOrEmpty(question.Id))
                throw new FormatException("A question has no id.");

            ValidatePrompt(question.Id, question.Original);
            if (question.Simplified == null)
                return;

            ValidatePrompt(question.Id, question.Simplified);
            if (question.Simplified.Answers.Count != question.Original.Answers.Count)
                throw new FormatException($"Question {question.Id}: simplified version has a different number of answers.");
        }

        /// <exception cref="FormatException">The review doesn't fit the question.</exception>
        public static void ValidateReview(Question question, AnswerReview review)
        {
            if (review == null)
                throw new FormatException($"Question {question.Id}: the answer came back without a review.");

            var count = question.Original.Answers.Count;
            if (review.CorrectIndex < 0 || review.CorrectIndex >= count)
                throw new FormatException($"Question {question.Id}: invalid correct answer {review.CorrectIndex}.");
            if (review.AnsweredIndex is int answered && (answered < 0 || answered >= count))
                throw new FormatException($"Question {question.Id}: invalid recorded answer {answered}.");

            ValidatePromptReview(question.Id, review.Original, count);
        }

        /// <exception cref="FormatException">The indices aren't distinct answers, would remove every answer, or the
        /// charges left are negative.</exception>
        public static void ValidateFiftyFifty(Question question, FiftyFiftyResult result)
        {
            if (result == null)
                throw new FormatException($"Question {question.Id}: 50/50 returned nothing.");
            if (result.Remaining < 0)
                throw new FormatException($"Question {question.Id}: 50/50 left {result.Remaining} charges.");
            ValidateEliminated(question, result.Eliminated);
        }

        /// <exception cref="FormatException">The indices aren't distinct answers, or would remove every answer.</exception>
        public static void ValidateEliminated(Question question, IReadOnlyList<int> eliminated)
        {
            var count = question.Original.Answers.Count;
            if (eliminated == null || eliminated.Count == 0)
                throw new FormatException($"Question {question.Id}: 50/50 removed no answers.");
            if (eliminated.Count >= count)
                throw new FormatException($"Question {question.Id}: 50/50 would remove every answer.");

            var seen = new HashSet<int>();
            foreach (var index in eliminated)
            {
                if (index < 0 || index >= count || !seen.Add(index))
                    throw new FormatException($"Question {question.Id}: 50/50 returned invalid answer {index}.");
            }
        }

        static void ValidatePrompt(string questionId, QuestionPrompt prompt)
        {
            if (prompt?.Answers == null || prompt.Answers.Count == 0)
                throw new FormatException($"Question {questionId}: no answers.");
            if (prompt.Answers.Count > Question.MaxAnswers)
                throw new FormatException($"Question {questionId}: {prompt.Answers.Count} answers, but at most {Question.MaxAnswers} are supported.");
        }

        static void ValidatePromptReview(string questionId, PromptReview review, int answerCount)
        {
            if (review?.Feedbacks == null)
                throw new FormatException($"Question {questionId}: the review has no feedback.");
            if (review.Feedbacks.Count != answerCount)
                throw new FormatException($"Question {questionId}: {answerCount} answers but {review.Feedbacks.Count} feedbacks.");
        }
    }
}
