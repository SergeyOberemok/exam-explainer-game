using System.Collections.Generic;
using ExamCards.Exam;
using UnityEngine;
using UnityEngine.UIElements;

namespace ExamCards.UI
{
    /// <summary>
    /// Loads the picture of an icon key (see <see cref="EntityIconResolver"/>) from
    /// <c>Resources/EntityIcons/&lt;key&gt;</c>. A key the game doesn't ship, as after a backend catalog
    /// change, falls back: a cast member to the icon of its type, anything else to the default icon.
    /// </summary>
    public static class EntityIcons
    {
        const string Folder = "EntityIcons/";

        static readonly Dictionary<string, VectorImage> Cache = new Dictionary<string, VectorImage>();

        public static VectorImage For(CastMember member)
        {
            var icon = Load(EntityIconResolver.Resolve(member));
            return icon != null ? icon : Load(EntityIconResolver.TypeIcon(member.Type));
        }

        /// <returns>The picture for a question's or an answer's key; the default one when the key is null or doesn't ship.</returns>
        public static VectorImage For(string iconKey)
        {
            var icon = iconKey != null ? Load(iconKey) : null;
            return icon != null ? icon : Load(EntityIconResolver.DefaultIcon);
        }

        /// <summary>Draws the key's picture as <paramref name="element"/>'s background, the data-driven inline style of an icon.</summary>
        public static void Show(VisualElement element, string iconKey) =>
            element.style.backgroundImage = new StyleBackground(For(iconKey));

        /// <returns>The icon, or null (with a warning, once) when it doesn't ship.</returns>
        public static VectorImage Load(string name)
        {
            if (Cache.TryGetValue(name, out var icon))
                return icon;

            icon = Resources.Load<VectorImage>(Folder + name);
            if (icon == null)
                Debug.LogWarning($"Entity icon \"{name}\" is missing from Resources/{Folder}.");
            Cache[name] = icon;
            return icon;
        }
    }
}
