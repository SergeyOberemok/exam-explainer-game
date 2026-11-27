using UnityEngine.UIElements;

namespace ExamCards.UI
{
    /// <summary>The criterion row the criteria modal builds.</summary>
    public static class RequirementsView
    {
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
    }
}
