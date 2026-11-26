using System.Collections.Generic;
using UnityEngine.UIElements;

namespace ExamCards.UI
{
    /// <summary>
    /// The lists of an answered question's debrief, built in code: what the answer had to do, with a mark for
    /// whether the answer that counts did, and every answer's feedback. Each list's section is hidden while
    /// it has no rows.
    /// </summary>
    public sealed class RequirementsView
    {
        const string HiddenClass = "is-hidden";

        readonly VisualElement _requirementsSection;
        readonly VisualElement _requirements;
        readonly VisualElement _notesSection;
        readonly VisualElement _notes;

        public RequirementsView(VisualElement root)
        {
            _requirementsSection = root.Q("requirementsSection");
            _requirements = root.Q("requirementsList");
            _notesSection = root.Q("answerNotesSection");
            _notes = root.Q("answerNotes");
        }

        /// <param name="requirements">What an answer had to do; null or empty hides the list.</param>
        /// <param name="met">Whether the answer that counts meets each one, aligned with <paramref name="requirements"/>; shorter counts as not met.</param>
        public void BindRequirements(IReadOnlyList<string> requirements, IReadOnlyList<bool> met)
        {
            _requirements.Clear();
            for (var i = 0; requirements != null && i < requirements.Count; i++)
            {
                var isMet = met != null && i < met.Count && met[i];
                var row = new VisualElement();
                row.AddToClassList("requirement");

                var mark = new Label(isMet ? "✓" : "✗");
                mark.AddToClassList("requirement__mark");
                mark.AddToClassList(isMet ? "requirement__mark--met" : "requirement__mark--missed");
                row.Add(mark);

                var text = new Label(requirements[i]) { enableRichText = false };
                text.AddToClassList("requirement__text");
                row.Add(text);
                _requirements.Add(row);
            }
            _requirementsSection.EnableInClassList(HiddenClass, _requirements.childCount == 0);
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
