using System.Collections.Generic;
using ExamCards.Exam;
using UnityEngine.UIElements;

namespace ExamCards.UI
{
    /// <summary>
    /// Fills a container with one chip per <see cref="VisualEntity"/>: its icon, plus its name unless compact
    /// (then the name is the tooltip). The container is hidden while it has nothing to show.
    /// </summary>
    public sealed class EntityStripView
    {
        const string HiddenClass = "is-hidden";

        readonly VisualElement _container;
        readonly bool _compact;
        readonly int _maxShown;
        IReadOnlyList<VisualEntity> _shown;

        public EntityStripView(VisualElement container, bool compact, int maxShown)
        {
            _container = container;
            _compact = compact;
            _maxShown = maxShown;
        }

        /// <param name="entities">What to show; null or empty hides the strip.</param>
        public void Bind(IReadOnlyList<VisualEntity> entities)
        {
            if (entities != null && entities.Count == 0)
                entities = null;
            if (ReferenceEquals(entities, _shown))
                return;
            _shown = entities;

            _container.Clear();
            _container.EnableInClassList(HiddenClass, entities == null);
            if (entities == null)
                return;

            for (var i = 0; i < entities.Count && i < _maxShown; i++)
                _container.Add(CreateChip(entities[i]));
        }

        VisualElement CreateChip(VisualEntity entity)
        {
            var chip = new VisualElement { pickingMode = PickingMode.Position };
            chip.AddToClassList("entity");
            chip.AddToClassList(KindClass(entity.Kind));

            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.AddToClassList("entity__icon");
            // The icon depends on the data, like the progress width, so it's the one inline style here.
            icon.style.backgroundImage = new StyleBackground(EntityIcons.For(entity));
            chip.Add(icon);

            if (_compact)
            {
                chip.AddToClassList("entity--compact");
                chip.tooltip = entity.Name;
            }
            else
            {
                var name = new Label(entity.Name) { enableRichText = false, pickingMode = PickingMode.Ignore };
                name.AddToClassList("entity__name");
                chip.Add(name);
            }
            return chip;
        }

        static string KindClass(EntityKind kind) => kind switch
        {
            EntityKind.Actor => "entity--actor",
            EntityKind.Action => "entity--action",
            _ => "entity--object",
        };
    }
}
