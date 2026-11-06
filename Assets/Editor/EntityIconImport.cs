using Unity.VectorGraphics.Editor;
using UnityEditor;

namespace ExamCards.Editor
{
    /// <summary>
    /// Imports the Lucide icons in <c>Assets/Resources/EntityIcons/</c> as UI Toolkit <c>VectorImage</c>s,
    /// so a newly added SVG works without setting its importer by hand.
    /// </summary>
    sealed class EntityIconImport : AssetPostprocessor
    {
        public const string Folder = "Assets/Resources/EntityIcons/";

        void OnPreprocessAsset()
        {
            if (!assetPath.StartsWith(Folder) || !assetPath.EndsWith(".svg"))
                return;
            if (assetImporter is SVGImporter importer && importer.SvgType != SVGType.VectorImage)
                importer.SvgType = SVGType.VectorImage;
        }
    }
}
