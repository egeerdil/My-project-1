using UnityEngine;
using UnityEngine.UI;

namespace LingoRun
{
    /// <summary>
    /// Minimal flat-shaded filled polygon for uGUI: a triangle fan from the
    /// shape's own center (0,0) out to each boundary point. Each triangle can
    /// carry its own solid color (no per-vertex blending across facet edges),
    /// which is what gives the low-poly "faceted terrain" look used by the
    /// world map's islands. No texture/sprite — just flat color triangles.
    /// Build with UIFactory.CreatePolygon.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class PolyImage : MaskableGraphic
    {
        Vector2[] _boundary;
        Color[] _facetColors;

        /// <summary>boundaryPoints are local to this shape's own center (0,0),
        /// in the order they connect (a closed loop back to point 0). Must be
        /// "star-shaped" from the center — true for anything built by radiating
        /// points out from an origin, which is all this file does.</summary>
        public void SetPoints(Vector2[] boundaryPoints, Color[] facetColors = null)
        {
            _boundary = boundaryPoints;
            _facetColors = facetColors;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_boundary == null || _boundary.Length < 3) return;

            int n = _boundary.Length;
            int idx = 0;
            for (int i = 0; i < n; i++)
            {
                Color c = (_facetColors != null && _facetColors.Length == n) ? _facetColors[i] : color;

                var v0 = UIVertex.simpleVert;
                v0.color = c;
                v0.position = Vector3.zero;

                var v1 = UIVertex.simpleVert;
                v1.color = c;
                v1.position = _boundary[i];

                var v2 = UIVertex.simpleVert;
                v2.color = c;
                v2.position = _boundary[(i + 1) % n];

                vh.AddVert(v0);
                vh.AddVert(v1);
                vh.AddVert(v2);
                vh.AddTriangle(idx, idx + 1, idx + 2);
                idx += 3;
            }
        }
    }
}
