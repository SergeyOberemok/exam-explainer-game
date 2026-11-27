using System;
using System.Collections.Generic;
using System.Linq;

namespace ExamCards.Exam
{
    /// <summary>
    /// A pick-one question as the player meets it: the story beat, the briefing item by item, the ask, and the
    /// answers as tools, with the cast they name. Which answer is correct is only known after answering it in
    /// an <see cref="IExamRound"/>.
    /// </summary>
    [Serializable]
    public sealed class Question
    {
        /// <summary>The most answers a question can have; the screen has one letter and one key for each.</summary>
        public const int MaxAnswers = 4;

        IReadOnlyList<AnswerTool> _tools = Array.Empty<AnswerTool>();
        IReadOnlyList<string> _answers = Array.Empty<string>();

        public string Id { get; set; }
        public string Section { get; set; }
        public string Source { get; set; }

        /// <summary>The full original text, scenario paragraph included.</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// The answers in index order. Each tool's <see cref="AnswerTool.Index"/> is its position here.
        /// </summary>
        public IReadOnlyList<AnswerTool> Tools
        {
            get => _tools;
            set
            {
                _tools = value ?? Array.Empty<AnswerTool>();
                _answers = _tools.Select(tool => tool.Text ?? string.Empty).ToArray();
            }
        }

        /// <summary>The answers' texts, index-aligned with <see cref="Tools"/>.</summary>
        public IReadOnlyList<string> Answers => _answers;

        /// <summary>
        /// Keys of the learning objectives the question tests, the main one first. Empty when the backend
        /// hasn't tagged it. The backend's objective catalog says what a key means.
        /// </summary>
        public IReadOnlyList<string> Objectives { get; set; } = Array.Empty<string>();

        /// <summary>1 (easy) to 3 (hard); 0 when the backend hasn't tagged the question.</summary>
        public int Difficulty { get; set; }

        /// <summary>Key of the story chapter the question belongs to; empty when it has none.</summary>
        public string Scenario { get; set; } = string.Empty;

        /// <summary>Key of the chapter's stage the question is in; empty until the backend has placed it.</summary>
        public string Stage { get; set; } = string.Empty;

        /// <summary>
        /// The original text without the scenario paragraph the source repeats in every question of a chapter.
        /// Empty when the question has no chapter; <see cref="Text"/> is then all there is.
        /// </summary>
        public string Stem { get; set; } = string.Empty;

        /// <summary>One story line to show before the question; empty when the backend has none.</summary>
        public string Beat { get; set; } = string.Empty;

        /// <summary>Icon key for the question as a whole; null when the backend picked none.</summary>
        public string Icon { get; set; }

        /// <summary>
        /// The scenario's facts in the order to show them, item by item (context, current state, constraint…),
        /// without the ask. Empty for a question the backend hasn't decomposed.
        /// </summary>
        public IReadOnlyList<QuestionPart> Briefing { get; set; } = Array.Empty<QuestionPart>();

        /// <summary>The question itself, word for word (<see cref="PartRole.Ask"/>); null without a decomposition.</summary>
        public QuestionPart Ask { get; set; }

        /// <summary>Every entity the briefing, the ask, the tools, the actions and the flow name.</summary>
        public IReadOnlyList<CastMember> Cast { get; set; } = Array.Empty<CastMember>();

        /// <summary>What happens in the scenario, step by step. Data for the planned interactive icons.</summary>
        public IReadOnlyList<QuestionAction> Actions { get; set; } = Array.Empty<QuestionAction>();

        /// <summary>The situation as a flow chart ending in the question's decision; empty without one.</summary>
        public DecisionFlow Flow { get; set; } = DecisionFlow.Empty;

        /// <summary>
        /// What to show for the original text: the <see cref="Stem"/> when the question has a chapter (its intro
        /// stands for the scenario paragraph), otherwise the whole text.
        /// </summary>
        public string OriginalText => string.IsNullOrEmpty(Stem) ? Text : Stem;

        /// <summary>The cast members the scenario names, for the question's own chips.</summary>
        public IReadOnlyList<CastMember> SceneCast => Cast.Where(member => member.InScene).ToArray();

        /// <returns>The cast member with <paramref name="reference"/>; null when there is none.</returns>
        public CastMember FindCast(string reference)
        {
            if (string.IsNullOrEmpty(reference))
                return null;
            foreach (var member in Cast)
            {
                if (member.Ref == reference)
                    return member;
            }
            return null;
        }

        /// <returns>The cast members <paramref name="references"/> name, in that order, without unknown refs.</returns>
        public IReadOnlyList<CastMember> CastFor(IEnumerable<string> references) =>
            (references ?? Enumerable.Empty<string>()).Select(FindCast).Where(member => member != null).Distinct().ToArray();

        /// <returns>
        /// The cast members answer <paramref name="index"/> is made of that the scene doesn't name, so a card
        /// shows what is specific to its answer; empty for an index without a tool.
        /// </returns>
        public IReadOnlyList<CastMember> ToolCast(int index) =>
            index >= 0 && index < Tools.Count
                ? CastFor(Tools[index].ComponentRefs).Where(member => !member.InScene).ToArray()
                : Array.Empty<CastMember>();

        /// <returns>The icon key of answer <paramref name="index"/>; null when it has none.</returns>
        public string IconForAnswer(int index) => index >= 0 && index < Tools.Count ? Tools[index].Icon : null;
    }
}
