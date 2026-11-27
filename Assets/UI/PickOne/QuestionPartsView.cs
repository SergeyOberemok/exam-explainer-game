using System;
using System.Collections.Generic;
using ExamCards.Exam;
using UnityEngine.UIElements;

namespace ExamCards.UI
{
    /// <summary>
    /// Fills a container with a question's briefing, one row per fact: a role chip, the text, and compact icons
    /// for the cast members the fact names. For a new question the rows are built empty and handed to a
    /// <see cref="TypingSequence"/> as <see cref="Steps"/>, so the player reads the context, the current state and
    /// the constraints typed out item by item before the ask. The container is hidden while it has nothing to show.
    /// </summary>
    public sealed class QuestionPartsView
    {
        const string HiddenClass = "is-hidden";

        /// <summary>Icons per row; the rest would crowd out the text.</summary>
        const int MaxRowCast = 4;

        readonly VisualElement _container;
        readonly List<TypingStep> _steps = new();
        IReadOnlyList<QuestionPart> _shown;

        public QuestionPartsView(VisualElement container)
        {
            _container = container;
        }

        /// <summary>One step per row, in order, with each row's full text.</summary>
        public IReadOnlyList<TypingStep> Steps => _steps;

        /// <param name="parts">The briefing; null or empty hides the container.</param>
        /// <param name="castFor">The cast members a row's refs name; null shows no icons.</param>
        /// <param name="forTyping">Build the rows empty and hidden, for a <see cref="TypingSequence"/> to type
        /// from <see cref="Steps"/>. Otherwise the same briefing as last time is left as it is.</param>
        public void Bind(IReadOnlyList<QuestionPart> parts, Func<IReadOnlyList<string>, IReadOnlyList<CastMember>> castFor = null,
            bool forTyping = false)
        {
            if (parts != null && parts.Count == 0)
                parts = null;
            if (!forTyping && ReferenceEquals(parts, _shown))
                return;

            _shown = parts;
            _container.Clear();
            _steps.Clear();
            foreach (var part in parts ?? Array.Empty<QuestionPart>())
            {
                if (part.Role == PartRole.Ask)
                    continue;
                var (row, text) = CreateRow(part, castFor?.Invoke(part.EntityRefs));
                if (forTyping)
                {
                    text.text = string.Empty;
                    row.AddToClassList(TypingSequence.PendingClass);
                }
                _container.Add(row);
                _steps.Add(new TypingStep(text, part.Text, row));
            }
            _container.EnableInClassList(HiddenClass, _steps.Count == 0);
        }

        static (VisualElement Row, Label Text) CreateRow(QuestionPart part, IReadOnlyList<CastMember> cast)
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

            if (cast != null && cast.Count > 0)
            {
                var strip = new VisualElement();
                strip.AddToClassList("question-part__cast");
                new EntityStripView(strip, compact: true, maxShown: MaxRowCast).Bind(cast);
                row.Add(strip);
            }
            return (row, text);
        }

        static string RoleText(PartRole role) => role switch
        {
            PartRole.Context => "CONTEXT",
            PartRole.CurrentState => "NOW",
            PartRole.Problem => "PROBLEM",
            PartRole.Consequence => "RISK",
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
