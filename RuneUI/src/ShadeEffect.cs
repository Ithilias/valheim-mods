using UnityEngine;
using UnityEngine.UI;

namespace RuneUI
{
    /// <summary>
    /// Shades a Graphic from top to bottom through its vertex colours, which gives flat bars a rounded
    /// 3D look without extra textures or materials. Values above 0 lighten towards white, below 0
    /// darken towards black. The shade is a function of height, so a sliced sprite's extra vertex rows
    /// still give one smooth gradient.
    /// </summary>
    [DisallowMultipleComponent]
    internal class ShadeEffect : BaseMeshEffect
    {
        private float _top;
        private float _bottom;

        public void Set(float top, float bottom)
        {
            _top = Mathf.Clamp(top, -1f, 1f);
            _bottom = Mathf.Clamp(bottom, -1f, 1f);
            if (graphic != null) graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;

            var vertex = new UIVertex();
            float min = float.MaxValue, max = float.MinValue;
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);
                min = Mathf.Min(min, vertex.position.y);
                max = Mathf.Max(max, vertex.position.y);
            }
            float height = Mathf.Max(0.001f, max - min);

            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);
                float t = (vertex.position.y - min) / height;
                vertex.color = Shade(vertex.color, Mathf.Lerp(_bottom, _top, t));
                vh.SetUIVertex(vertex, i);
            }
        }

        private static Color32 Shade(Color32 color, float amount)
        {
            Color c = color;
            Color shaded = amount >= 0f ? Color.Lerp(c, Color.white, amount) : Color.Lerp(c, Color.black, -amount);
            shaded.a = c.a;
            return shaded;
        }

        public static void Apply(Graphic graphic, float top, float bottom)
        {
            var effect = graphic.GetComponent<ShadeEffect>();
            if (effect == null) effect = graphic.gameObject.AddComponent<ShadeEffect>();
            effect.Set(top, bottom);
        }
    }
}
