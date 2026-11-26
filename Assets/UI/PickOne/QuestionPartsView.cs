using System.Collections.Generic;
using ExamCards.Exam;
using UnityEngine.UIElements;

namespace ExamCards.UI
{
    /// <summary>
    /// Fills a container with the scenario's facts of a question, one row each: a role chip and the text. The
    /// ask is left out, since the question text shows it. The container is hidden while it has nothing to show.
    /// </summary>
    public sealed class QuestionPartsView
    {
        const string HiddenClass = "is-hidden";

        readonly VisualElement _container;
        IReadOnlyList<QuestionPart> _shown;

        public QuestionPartsView(VisualElement container)
        {
            _container = container;
        }

        /// <param name="parts">The question's parts; null, empty or only an ask hides the container.</param>
        public void Bind(IReadOnlyList<QuestionPart> parts)
        {
            if (ReferenceEquals(parts, _shown))
                return;
            _shown = parts;

            _container.Clear();
            foreach (var part in parts ?? System.Array.Empty<QuestionPart>())
            {
                if (part.Role != PartRole.Ask)
                    _container.Add(CreateRow(part));
            }
            _container.EnableInClassList(HiddenClass, _container.childCount == 0);
        }

        static VisualElement CreateRow(QuestionPart part)
        {
            var row = new VisualElement();
            row.AddToClassList("question-part");

            var chip = new VisualElement();
            chip.AddToClassList("chip");
            chip.AddToClassList(RoleChipClass(part.Role));
            chip.AddToClassList("question-part__role");
            var role = new Label(RoleText(part.Role)) { enableRichText = false };
            role.AddToClassList("chip__text");
            chip.Add(role);
            row.Add(chip);

            var text = new Label(part.Text) { enableRichText = false };
            text.AddToClassList("question-part__text");
            row.Add(text);
            return row;
        }

        static string RoleText(PartRole role) => role switch
        {
            PartRole.Context => "CONTEXT",
            PartRole.CurrentState => "NOW",
            PartRole.Problem => "PROBLEM",
            PartRole.Consequence => "IMPACT",
            PartRole.Goal => "GOAL",
            PartRole.Constraint => "CONSTRAINT",
            _ => "QUESTION",
        };

        static string RoleChipClass(PartRole role) => role switch
        {
            PartRole.Problem or PartRole.Consequence => "chip--pink",
            PartRole.Goal => "chip--mint",
            PartRole.Constraint => "chip--gold",
            _ => "chip--sand",
        };
    }
}
