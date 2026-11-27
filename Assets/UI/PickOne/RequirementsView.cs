using System.Collections.Generic;
using UnityEngine.UIElements;

namespace ExamCards.UI
{
    /// <summary>
    /// The answer notes of an answered question's debrief, built in code (every answer's feedback), plus the
    /// criterion row the criteria modal shares. The notes' section is hidden while it has no rows.
    /// </summary>
    public sealed class RequirementsView
    {
        const string HiddenClass = "is-hidden";

        readonly VisualElement _notesSection;
        readonly VisualElement _notes;

        public RequirementsView(VisualElement root)
        {
            _notesSection = root.Q("answerNotesSection");
            _notes = root.Q("answerNotes");
        }

        /// <summary>One criterion row for the criteria modal.</summary>
        /// <param name="met">True ticked, false missed, null not judged yet (an empty box).</param>
        internal static VisualElement BuildRow(string requirement, bool? met)
        {
            var row = new VisualElement();
            row.AddToClassList("requirement");

            var mark = new Label(met == null ? string.Empty : met.Value ? "✓" : "✗");
            mark.AddToClassList("requirement__mark");
            mark.AddToClassList(met == null ? "requirement__mark--empty" : met.Value ? "requirement__mark--met" : "requirement__mark--missed");
            row.Add(mark);

            var text = new Label(requirement) { enableRichText = false };
            text.AddToClassList("requirement__text");
            row.Add(text);
            return row;
        }

        /// <param name="feedbacks">One per answer; null or all empty hides the list. Empty ones get no row.</param>
        /// <param name="correctIndex">The correct answer, tinted as right.</param>
        /// <param name="answeredIndex">The answer that counts, tinted as wrong unless it is the correct one.</param>
        public void BindAnswerNotes(IReadOnlyList<string> feedbacks, int correctIndex, int? answeredIndex)
        {
            _notes.Clear();
            for (var i = 0; feedbacks != null && i < feedbacks.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(feedbacks[i]))
                    continue;
                var row = new VisualElement();
                row.AddToClassList("answer-note");

                var letter = new Label(((char)('A' + i)).ToString());
                letter.AddToClassList("answer-note__letter");
                letter.EnableInClassList("answer-note__letter--correct", i == correctIndex);
                letter.EnableInClassList("answer-note__letter--wrong", i == answeredIndex && i != correctIndex);
                row.Add(letter);

                var text = new Label(feedbacks[i]) { enableRichText = false };
                text.AddToClassList("answer-note__text");
                row.Add(text);
                _notes.Add(row);
            }
            _notesSection.EnableInClassList(HiddenClass, _notes.childCount == 0);
        }
    }
}
