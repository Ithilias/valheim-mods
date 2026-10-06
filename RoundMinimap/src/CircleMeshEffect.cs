using UnityEngine;
using UnityEngine.UI;

namespace RoundMinimap
{
    /// <summary>
    /// Replaces a Graphic's quad with a disc, optionally sampling the texture rotated about the
    /// disc's centre.
    ///
    /// Rotating here rather than on the RectTransform matters for two reasons: the graphic keeps
    /// rendering with its own material (Valheim writes _zoom / _mapCenter / _pixelSize to the
    /// minimap material every frame, so a cloned material renders stale nonsense), and nothing in
    /// the UI hierarchy turns, so child markers stay where they were put.
    ///
    /// A disc is rotation-invariant, so rotated sampling never reaches outside the visible map
    /// region: the circle inscribed in the uv rect maps onto itself.
    /// </summary>
    [DisallowMultipleComponent]
    internal class CircleMeshEffect : BaseMeshEffect
    {
        private float _radiusScale = 1f;
        private float _angle;
        private int _segments = 96;

        /// <summary>Disc diameter as a fraction of the graphic's shortest side.</summary>
        public float RadiusScale
        {
            get => _radiusScale;
            set => Set(ref _radiusScale, Mathf.Clamp(value, 0.05f, 1f));
        }

        /// <summary>Degrees the sampled content is turned counter-clockwise.</summary>
        public float Angle
        {
            get => _angle;
            set => Set(ref _angle, value);
        }

        public int Segments
        {
            get => _segments;
            set
            {
                int clamped = Mathf.Clamp(value, 12, 512);
                if (_segments == clamped) return;
                _segments = clamped;
                if (graphic != null) graphic.SetVerticesDirty();
            }
        }

        private void Set(ref float field, float value)
        {
            if (Mathf.Approximately(field, value)) return;
            field = value;
            if (graphic != null) graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount < 4) return;

            // The source quad is axis-aligned, so its bounds plus the uv at two opposite corners
            // fully describe the position-to-uv mapping.
            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            var vertex = default(UIVertex);
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);
                minX = Mathf.Min(minX, vertex.position.x);
                maxX = Mathf.Max(maxX, vertex.position.x);
                minY = Mathf.Min(minY, vertex.position.y);
                maxY = Mathf.Max(maxY, vertex.position.y);
            }

            float width = maxX - minX;
            float height = maxY - minY;
            if (width <= 0f || height <= 0f) return;

            Vector4 uvAtMin = Vector4.zero, uvAtMax = Vector4.one;
            Color32 color = Color.white;
            float z = 0f;
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);
                color = vertex.color;
                z = vertex.position.z;
                bool atMinX = Mathf.Approximately(vertex.position.x, minX);
                bool atMinY = Mathf.Approximately(vertex.position.y, minY);
                if (atMinX && atMinY) uvAtMin = vertex.uv0;
                else if (!atMinX && !atMinY) uvAtMax = vertex.uv0;
            }

            Vector2 centre = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
            Vector2 uvCentre = new Vector2((uvAtMin.x + uvAtMax.x) * 0.5f, (uvAtMin.y + uvAtMax.y) * 0.5f);
            Vector2 uvPerPixel = new Vector2((uvAtMax.x - uvAtMin.x) / width, (uvAtMax.y - uvAtMin.y) / height);
            float radius = Mathf.Min(width, height) * 0.5f * _radiusScale;

            // Content turned counter-clockwise by Angle means sampling at the clockwise offset.
            float rad = -_angle * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);

            vh.Clear();
            vh.AddVert(MakeVertex(centre, centre, uvCentre, uvPerPixel, cos, sin, color, z));
            for (int i = 0; i < _segments; i++)
            {
                float theta = i * 2f * Mathf.PI / _segments;
                Vector2 point = centre + new Vector2(Mathf.Cos(theta), Mathf.Sin(theta)) * radius;
                vh.AddVert(MakeVertex(point, centre, uvCentre, uvPerPixel, cos, sin, color, z));
            }
            for (int i = 0; i < _segments; i++)
            {
                vh.AddTriangle(0, 1 + i, 1 + (i + 1) % _segments);
            }
        }

        private static UIVertex MakeVertex(Vector2 point, Vector2 centre, Vector2 uvCentre,
            Vector2 uvPerPixel, float cos, float sin, Color32 color, float z)
        {
            Vector2 offset = point - centre;
            Vector2 sampled = new Vector2(
                offset.x * cos - offset.y * sin,
                offset.x * sin + offset.y * cos);

            var vertex = UIVertex.simpleVert;
            vertex.position = new Vector3(point.x, point.y, z);
            vertex.color = color;
            vertex.uv0 = new Vector4(
                uvCentre.x + sampled.x * uvPerPixel.x,
                uvCentre.y + sampled.y * uvPerPixel.y,
                0f, 0f);
            return vertex;
        }
    }
}
