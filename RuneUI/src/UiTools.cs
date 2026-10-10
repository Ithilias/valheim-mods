using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace RuneUI
{
    internal static class UiTools
    {
        /// <summary>
        /// White nine-slice sprite for a rounded box, tinted by the Image colour. With
        /// <paramref name="ring"/> it is just the border of the given thickness; without, it is the
        /// area inside that border, so a translucent fill and border do not overlap. Sizes are in
        /// texture pixels, which the caller maps one to one onto UI units.
        /// </summary>
        public static Sprite CreateRoundedRectSprite(float radius, float thickness, bool ring)
        {
            int border = Mathf.CeilToInt(Mathf.Max(radius, thickness)) + 2;
            int size = border * 2 + 1;
            float half = size * 0.5f;
            float r = Mathf.Min(radius, border - 1f);

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = ring ? "RuneUI_Border" : "RuneUI_Fill",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Signed distance to the rounded box's edge: negative inside.
                    float qx = Mathf.Abs(x + 0.5f - half) - (half - r);
                    float qy = Mathf.Abs(y + 0.5f - half) - (half - r);
                    float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
                    float d = outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;

                    float inner = Mathf.Clamp01(0.5f - (d + thickness));
                    float alpha = ring ? Mathf.Clamp01(0.5f - d) - inner : inner;
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha) * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }

        public static string DumpHierarchy(Transform root)
        {
            var sb = new StringBuilder();
            sb.Append("RuneUI hierarchy dump of '").Append(root.name).Append("'\n");
            Dump(root, sb, 0);
            return sb.ToString();
        }

        private static void Dump(Transform t, StringBuilder sb, int depth)
        {
            sb.Append(' ', depth * 2).Append(t.name);
            if (!t.gameObject.activeSelf) sb.Append(" [inactive]");

            var rt = t as RectTransform;
            if (rt != null)
            {
                sb.Append("  rect=").Append(rt.rect.size.ToString("F1"))
                  .Append(" pos=").Append(rt.anchoredPosition.ToString("F1"))
                  .Append(" pivot=").Append(rt.pivot.ToString("F2"))
                  .Append(" anchors=").Append(rt.anchorMin.ToString("F2")).Append("..")
                  .Append(rt.anchorMax.ToString("F2"));
            }

            var components = t.GetComponents<Component>();
            sb.Append("  <");
            for (int i = 0; i < components.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(components[i] == null ? "null" : components[i].GetType().Name);
            }
            sb.Append('>');

            var img = t.GetComponent<Image>();
            if (img != null && img.sprite != null) sb.Append(" sprite=").Append(img.sprite.name);

            var raw = t.GetComponent<RawImage>();
            if (raw != null && raw.material != null)
                sb.Append(" material=").Append(raw.material.name)
                  .Append(" shader=").Append(raw.material.shader != null ? raw.material.shader.name : "null");

            sb.Append('\n');
            for (int i = 0; i < t.childCount; i++) Dump(t.GetChild(i), sb, depth + 1);
        }
    }
}
