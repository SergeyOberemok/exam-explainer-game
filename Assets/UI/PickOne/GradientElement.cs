using UnityEngine;
using UnityEngine.UIElements;

namespace ExamCards.UI
{
    /// <summary>
    /// Three-stop linear gradient (start → mid → end) drawn with vertex colors,
    /// since USS has no gradient backgrounds.
    /// </summary>
    [UxmlElement]
    public partial class GradientElement : VisualElement
    {
        Color _startColor = Color.white;
        Color _midColor = Color.gray;
        Color _endColor = Color.black;
        bool _vertical;

        [UxmlAttribute]
        public Color StartColor
        {
            get => _startColor;
            set { _startColor = value; MarkDirtyRepaint(); }
        }

        [UxmlAttribute]
        public Color MidColor
        {
            get => _midColor;
            set { _midColor = value; MarkDirtyRepaint(); }
        }

        [UxmlAttribute]
        public Color EndColor
        {
            get => _endColor;
            set { _endColor = value; MarkDirtyRepaint(); }
        }

        /// <summary>False: left → right. True: top → bottom.</summary>
        [UxmlAttribute]
        public bool Vertical
        {
            get => _vertical;
            set { _vertical = value; MarkDirtyRepaint(); }
        }

        public GradientElement()
        {
            generateVisualContent += OnGenerateVisualContent;
        }

        void OnGenerateVisualContent(MeshGenerationContext context)
        {
            var rect = contentRect;
            if (rect.width <= 0f || rect.height <= 0f)
                return;

            // Two quads: start→mid and mid→end, 3 vertex pairs along the gradient axis.
            var mesh = context.Allocate(6, 12);
            var colors = new[] { _startColor, _midColor, _endColor };
            for (var i = 0; i < 3; i++)
            {
                var t = i * 0.5f;
                Vector3 a, b;
                if (_vertical)
                {
                    var y = Mathf.Lerp(rect.yMin, rect.yMax, t);
                    a = new Vector3(rect.xMin, y, Vertex.nearZ);
                    b = new Vector3(rect.xMax, y, Vertex.nearZ);
                }
                else
                {
                    var x = Mathf.Lerp(rect.xMin, rect.xMax, t);
                    a = new Vector3(x, rect.yMin, Vertex.nearZ);
                    b = new Vector3(x, rect.yMax, Vertex.nearZ);
                }

                Color32 tint = colors[i];
                mesh.SetNextVertex(new Vertex { position = a, tint = tint });
                mesh.SetNextVertex(new Vertex { position = b, tint = tint });
            }

            for (ushort q = 0; q < 2; q++)
            {
                var o = (ushort)(q * 2);
                // Clockwise winding for both axis orientations.
                if (_vertical)
                {
                    mesh.SetNextIndex(o);
                    mesh.SetNextIndex((ushort)(o + 1));
                    mesh.SetNextIndex((ushort)(o + 3));
                    mesh.SetNextIndex(o);
                    mesh.SetNextIndex((ushort)(o + 3));
                    mesh.SetNextIndex((ushort)(o + 2));
                }
                else
                {
                    mesh.SetNextIndex(o);
                    mesh.SetNextIndex((ushort)(o + 2));
                    mesh.SetNextIndex((ushort)(o + 3));
                    mesh.SetNextIndex(o);
                    mesh.SetNextIndex((ushort)(o + 3));
                    mesh.SetNextIndex((ushort)(o + 1));
                }
            }
        }
    }
}
