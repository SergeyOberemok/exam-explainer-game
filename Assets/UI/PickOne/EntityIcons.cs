using System.Collections.Generic;
using ExamCards.Exam;
using UnityEngine;
using UnityEngine.UIElements;

namespace ExamCards.UI
{
    /// <summary>
    /// Loads the Lucide icon <see cref="EntityIconResolver"/> picks for an entity from
    /// <c>Resources/EntityIcons/</c>, falling back to the icon for its kind.
    /// </summary>
    public static class EntityIcons
    {
        const string Folder = "EntityIcons/";

        static readonly Dictionary<string, VectorImage> Cache = new Dictionary<string, VectorImage>();

        public static VectorImage For(VisualEntity entity)
        {
            var icon = Load(EntityIconResolver.Resolve(entity));
            return icon != null ? icon : Load(EntityIconResolver.KindIcon(entity.Kind));
        }

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
