using System;
using System.Collections.Generic;

namespace ExamCards.Exam
{
    /// <summary>How one answer measures up against what the question requires.</summary>
    [Serializable]
    public sealed class OptionEvaluation
    {
        /// <summary>The answer's index.</summary>
        public int Index { get; set; }

        public bool IsCorrect { get; set; }

        /// <summary>The <see cref="Resolution.AnswerRequirements"/> the answer meets.</summary>
        public IReadOnlyList<string> SupportedRequirements { get; set; } = Array.Empty<string>();

        /// <summary>What the answer misses; empty for the correct one.</summary>
        public IReadOnlyList<string> Gaps { get; set; } = Array.Empty<string>();

        /// <summary>One sentence on why it is right or wrong.</summary>
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>
    /// Why the correct answer is correct, worked out by the backend with the key: what an answer has to do,
    /// the question's flow continued with the correct steps, each answer compared, and the rule to remember.
    /// Part of the key, so it only arrives with the <see cref="AnswerReview"/>. Empty for a question the
    /// backend hasn't resolved.
    /// </summary>
    [Serializable]
    public sealed class Resolution
    {
        public static Resolution Empty => new Resolution();

        public IReadOnlyList<string> AnswerRequirements { get; set; } = Array.Empty<string>();

        /// <summary>The question's pre-answer flow without its question node, continued with steps marked <see cref="FlowNode.Added"/>.</summary>
        public DecisionFlow Flow { get; set; } = DecisionFlow.Empty;

        /// <summary>At most one per answer, by <see cref="OptionEvaluation.Index"/>; empty when the backend has none.</summary>
        public IReadOnlyList<OptionEvaluation> Options { get; set; } = Array.Empty<OptionEvaluation>();

        /// <summary>The one-line lesson of the question; empty when there is none.</summary>
        public string Rule { get; set; } = string.Empty;

        public bool IsEmpty => AnswerRequirements.Count == 0 && Flow.IsEmpty && Options.Count == 0 && string.IsNullOrEmpty(Rule);

        /// <returns>The evaluation of answer <paramref name="index"/>; null when there is none.</returns>
        public OptionEvaluation ForAnswer(int index)
        {
            foreach (var option in Options)
            {
                if (option.Index == index)
                    return option;
            }
            return null;
        }

        /// <returns>
        /// For each of the <see cref="AnswerRequirements"/>, whether answer <paramref name="index"/> meets it;
        /// empty when the answer has no evaluation. The backend names the requirements an option supports by
        /// their text, so they are matched by trimmed text and any it names that isn't one is ignored.
        /// </returns>
        public IReadOnlyList<bool> RequirementsMetBy(int index)
        {
            var option = ForAnswer(index);
            if (option == null)
                return Array.Empty<bool>();

            var supported = new HashSet<string>(StringComparer.Ordinal);
            foreach (var requirement in option.SupportedRequirements)
            {
                if (requirement != null)
                    supported.Add(requirement.Trim());
            }
            var met = new bool[AnswerRequirements.Count];
            for (var i = 0; i < met.Length; i++)
                met[i] = AnswerRequirements[i] != null && supported.Contains(AnswerRequirements[i].Trim());
            return met;
        }

        /// <returns>How many of the <see cref="AnswerRequirements"/> answer <paramref name="index"/> meets.</returns>
        public int MetCount(int index)
        {
            var count = 0;
            foreach (var met in RequirementsMetBy(index))
            {
                if (met)
                    count++;
            }
            return count;
        }
    }
}
