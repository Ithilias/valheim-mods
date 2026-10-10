using UnityEngine;

namespace RuneUI
{
    /// <summary>
    /// The gear slots laid out over a body outline: head at the top, chest in the middle, legs below,
    /// with cape, trinket and utility items beside them. The outline is drawn in code.
    /// </summary>
    internal static class Paperdoll
    {
        public const int Columns = 3;
        public const int Rows = 3;

        // Column and row of each gear cell: head, chest, legs, cape, utility, trinket, then the extra
        // utility cells.
        private static readonly Vector2Int[] Cells =
        {
            new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(1, 2),
            new Vector2Int(0, 1), new Vector2Int(0, 2), new Vector2Int(2, 1),
            new Vector2Int(2, 2), new Vector2Int(0, 0),
        };

        private static Sprite _outline;

        public static bool On => Plugin.ShowPaperdoll.Value;

        public static Vector2Int CellOf(int index) => index >= 0 && index < Cells.Length ? Cells[index] : new Vector2Int(2, 0);

        /// <summary>A body outline in white, tinted by the image showing it.</summary>
        public static Sprite Outline
        {
            get
            {
                if (_outline == null) _outline = Draw(128);
                return _outline;
            }
        }

        private static Sprite Draw(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "RuneUI_Paperdoll",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
                // Distance to the body in pixels: negative inside.
                float d = Body(p) * size;
                px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(0.5f - d) * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>Signed distance to a simple standing figure, in texture units (0 to 1, y up).</summary>
        private static float Body(Vector2 p)
        {
            // Mirror across the middle: one arm and leg cover both sides.
            var m = new Vector2(0.5f + Mathf.Abs(p.x - 0.5f), p.y);
            float head = (p - new Vector2(0.5f, 0.86f)).magnitude - 0.085f;
            float neck = Capsule(p, new Vector2(0.5f, 0.76f), new Vector2(0.5f, 0.8f), 0.04f);
            float torso = Capsule(p, new Vector2(0.5f, 0.66f), new Vector2(0.5f, 0.46f), 0.14f);
            float arm = Capsule(m, new Vector2(0.68f, 0.7f), new Vector2(0.8f, 0.4f), 0.05f);
            float leg = Capsule(m, new Vector2(0.56f, 0.4f), new Vector2(0.58f, 0.06f), 0.06f);
            return Mathf.Min(Mathf.Min(head, neck), Mathf.Min(torso, Mathf.Min(arm, leg)));
        }

        private static float Capsule(Vector2 p, Vector2 a, Vector2 b, float radius)
        {
            Vector2 pa = p - a, ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * h).magnitude - radius;
        }

        public static void Destroy()
        {
            if (_outline == null) return;
            Object.Destroy(_outline.texture);
            Object.Destroy(_outline);
            _outline = null;
        }
    }
}
