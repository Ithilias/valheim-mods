using UnityEngine;
using UnityEngine.UI;

namespace RuneUI
{
    internal enum BackdropStyle
    {
        Ornate,
        Vanilla,
    }

    /// <summary>
    /// The panels behind the HUD stack and the inventory's extra slots. Ornate is drawn here: a dark
    /// grained fill in an embossed metal rim with corner pieces and gems. Vanilla copies the game's
    /// inventory panel. The sprites are greyscale and tinted by the theme colours, so they are built once.
    /// </summary>
    internal static class Frames
    {
        public const string Name = "RuneUI_Frame";

        /// <summary>How far in from the edge the rim reaches; keep content at least this far in.</summary>
        public const float RimDepth = 12f;

        private const int FrameBorder = 26;
        private const int ShadowBorder = 18;
        private const int CornerSize = 48;
        private const int CrestWidth = 72, CrestHeight = 26;
        private const int GemSize = 14;
        private const int GrainSize = 64;
        private const float CornerOverhang = 8f;
        private const float VanillaShadow = 40f;

        // Rim, from the outside in, in sprite pixels.
        private const float Outline = 1.4f, BandEnd = 8.4f, GrooveEnd = 9.4f, InlayEnd = 11.2f;

        // Corner piece layout, from the corner, in corner sprite pixels.
        private const float StudCentre = 15f, StudRadius = 15f, RivetAt = 33f, RivetInset = 5f, RivetRadius = 2.6f;

        private static Sprite _frame, _corner, _crest, _gem, _glint, _grain, _shadow;

        /// <summary>Puts the chosen backdrop behind everything else in <paramref name="target"/>, filling it.</summary>
        public static RectTransform Build(RectTransform target, BackdropStyle style, float opacity)
        {
            var root = Theme.NewRect(Name, target);
            Theme.Stretch(root);
            root.SetAsFirstSibling();
            if (style != BackdropStyle.Vanilla || !BuildVanilla(root, opacity)) BuildOrnate(root, opacity);
            return root;
        }

        private static void BuildOrnate(RectTransform root, float opacity)
        {
            EnsureSprites();

            var shadow = Theme.NewImage("Shadow", root, _shadow, new Color(0f, 0f, 0f, 0.8f * opacity));
            Theme.Stretch(shadow.rectTransform, -ShadowBorder);

            // A backdrop is meant to be solid, so its opacity replaces the slot panels' alpha.
            Color fillColor = Plugin.PanelColor.Value;
            fillColor.a = opacity;
            var fill = Theme.NewImage("Fill", root, _grain, fillColor);
            fill.type = Image.Type.Tiled;
            // Inset so the fill does not show past the rim's rounded corners.
            Theme.Stretch(fill.rectTransform, 2f);

            var frame = Theme.NewImage("Rim", root, _frame, Plugin.BorderColor.Value);
            Theme.Stretch(frame.rectTransform);

            for (int i = 0; i < 4; i++) AddCorner(root, i % 2 == 1, i >= 2);

            // A crest in the middle of the top edge, on the rim's band.
            var crest = Piece("Crest", root, _crest, new Vector2(CrestWidth, CrestHeight));
            crest.anchorMin = crest.anchorMax = new Vector2(0.5f, 1f);
            crest.pivot = new Vector2(0.5f, 0.5f);
            crest.anchoredPosition = new Vector2(0f, -(Outline + BandEnd) * 0.5f);
            AddGem(crest, new Vector2(CrestWidth * 0.5f, CrestHeight * 0.5f), Vector2.one);
        }

        private static void AddCorner(RectTransform root, bool right, bool top)
        {
            var rt = Piece("Corner", root, _corner, new Vector2(CornerSize, CornerSize));
            var flip = new Vector2(right ? -1f : 1f, top ? -1f : 1f);
            // The sprite is a bottom left corner; mirroring it gives the other three.
            rt.anchorMin = rt.anchorMax = new Vector2(right ? 1f : 0f, top ? 1f : 0f);
            rt.localScale = new Vector3(flip.x, flip.y, 1f);
            rt.anchoredPosition = -flip * CornerOverhang;
            AddGem(rt, new Vector2(StudCentre, StudCentre), flip);
        }

        /// <summary>A metal piece in the border colour, pivoted at its bottom left.</summary>
        private static RectTransform Piece(string name, Transform parent, Sprite sprite, Vector2 size)
        {
            var rt = Theme.NewRect(name, parent);
            rt.pivot = Vector2.zero;
            rt.sizeDelta = size;
            var img = rt.gameObject.AddComponent<Image>();
            img.raycastTarget = false;
            img.sprite = sprite;
            img.color = Plugin.BorderColor.Value;
            return rt;
        }

        /// <param name="flip">The parent's mirroring, undone so every gem catches the light from the same side.</param>
        private static void AddGem(RectTransform parent, Vector2 centre, Vector2 flip)
        {
            var gem = Theme.NewRect("Gem", parent);
            gem.anchorMin = gem.anchorMax = Vector2.zero;
            gem.pivot = new Vector2(0.5f, 0.5f);
            gem.sizeDelta = new Vector2(GemSize, GemSize);
            gem.anchoredPosition = centre;
            gem.localScale = new Vector3(flip.x, flip.y, 1f);
            var img = gem.gameObject.AddComponent<Image>();
            img.raycastTarget = false;
            img.sprite = _gem;
            img.color = Plugin.GemColor.Value;

            var glint = Theme.NewRect("Glint", gem);
            Theme.Stretch(glint);
            var glintImg = glint.gameObject.AddComponent<Image>();
            glintImg.raycastTarget = false;
            glintImg.sprite = _glint;
        }

        /// <summary>The background and soft shadow of the vanilla inventory panel. False if not found.</summary>
        private static bool BuildVanilla(RectTransform root, float opacity)
        {
            var inventory = InventoryGui.instance != null ? InventoryGui.instance.m_player : null;
            var panel = inventory != null ? inventory.Find("Bkg")?.GetComponent<Image>() : null;
            if (panel == null || panel.sprite == null)
            {
                Plugin.Log.LogWarning("Found no vanilla panel background for the backdrop; using the ornate one.");
                return false;
            }

            var shadow = inventory.Find("Darken")?.GetComponent<Image>();
            if (shadow != null && shadow.sprite != null)
            {
                var copy = Copy("Shadow", root, shadow, opacity);
                var source = shadow.rectTransform;
                bool stretched = source.anchorMin == Vector2.zero && source.anchorMax == Vector2.one;
                Theme.Stretch(copy.rectTransform);
                copy.rectTransform.offsetMin = stretched ? source.offsetMin : new Vector2(-VanillaShadow, -VanillaShadow);
                copy.rectTransform.offsetMax = stretched ? source.offsetMax : new Vector2(VanillaShadow, VanillaShadow);
            }
            Theme.Stretch(Copy("Panel", root, panel, opacity).rectTransform);
            return true;
        }

        private static Image Copy(string name, Transform parent, Image source, float opacity)
        {
            var img = Theme.NewRect(name, parent).gameObject.AddComponent<Image>();
            img.raycastTarget = false;
            img.sprite = source.sprite;
            img.type = source.type;
            img.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
            img.material = source.material;
            Color c = source.color;
            c.a *= opacity;
            img.color = c;
            return img;
        }

        // Sprite drawing. All in texture pixels, which Theme maps one to one onto UI units. Grey
        // values are multiplied by the tint, so 1 is the full theme colour and 0 is black.

        private static void EnsureSprites()
        {
            if (_frame != null) return;
            _frame = Sliced("RuneUI_FrameRim", FrameBorder, RimPixel);
            _shadow = Sliced("RuneUI_FrameShadow", ShadowBorder, ShadowPixel);
            _corner = Simple("RuneUI_FrameCorner", CornerSize, CornerSize, CornerPixel);
            _crest = Simple("RuneUI_FrameCrest", CrestWidth, CrestHeight, CrestPixel);
            _gem = Simple("RuneUI_FrameGem", GemSize, GemSize, GemPixel);
            _glint = Simple("RuneUI_FrameGlint", GemSize, GemSize, GlintPixel);
            _grain = Grain();
        }

        public static void DestroySprites()
        {
            foreach (var sprite in new[] { _frame, _corner, _crest, _gem, _glint, _grain, _shadow })
            {
                if (sprite == null) continue;
                Object.Destroy(sprite.texture);
                Object.Destroy(sprite);
            }
            _frame = _corner = _crest = _gem = _glint = _grain = _shadow = null;
        }

        private delegate Color PixelFn(float x, float y, int width, int height);

        /// <summary>Draws each pixel from 4 samples, which smooths the edges.</summary>
        private static Texture2D Draw(string name, int width, int height, PixelFn pixel)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            var px = new Color[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                Color sum = Color.clear;
                for (int s = 0; s < 4; s++)
                {
                    Color c = pixel(x + 0.25f + 0.5f * (s % 2), y + 0.25f + 0.5f * (s / 2), width, height);
                    // Premultiply so transparent samples do not darken the colour of the opaque ones.
                    sum += new Color(c.r * c.a, c.g * c.a, c.b * c.a, c.a);
                }
                sum /= 4f;
                px[y * width + x] = sum.a > 0f ? new Color(sum.r / sum.a, sum.g / sum.a, sum.b / sum.a, sum.a) : Color.clear;
            }
            tex.SetPixels(px);
            tex.Apply(false, false);
            return tex;
        }

        private static Sprite Sliced(string name, int border, PixelFn pixel)
        {
            int size = border * 2 + 1;
            var tex = Draw(name, size, size, pixel);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }

        private static Sprite Simple(string name, int width, int height, PixelFn pixel)
        {
            var tex = Draw(name, width, height, pixel);
            return Sprite.Create(tex, new Rect(0, 0, width, height), Vector2.zero, 100f, 0, SpriteMeshType.FullRect);
        }

        private static Color Grey(float v, float a) => new Color(v, v, v, a);

        /// <summary>Small stable noise in 0..1 for a pixel.</summary>
        private static float Hash(int x, int y)
        {
            uint h = (uint)(x * 374761393 + y * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return (h ^ (h >> 16)) / (float)uint.MaxValue;
        }

        /// <summary>Signed distance to a rounded box filling the texture: negative inside.</summary>
        private static float BoxDistance(float x, float y, int size, float radius)
        {
            float half = size * 0.5f;
            float qx = Mathf.Abs(x - half) - (half - radius);
            float qy = Mathf.Abs(y - half) - (half - radius);
            return new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
        }

        private static Vector2 BoxNormal(float x, float y, int size, float radius)
        {
            const float e = 0.5f;
            var n = new Vector2(BoxDistance(x + e, y, size, radius) - BoxDistance(x - e, y, size, radius),
                BoxDistance(x, y + e, size, radius) - BoxDistance(x, y - e, size, radius));
            return n.sqrMagnitude > 0f ? n.normalized : Vector2.zero;
        }

        private static readonly Vector2 Light = new Vector2(-0.45f, 0.89f).normalized;

        /// <summary>Hammered metal: a coarse, stable unevenness.</summary>
        private static float Worn(float x, float y) => 0.86f + 0.14f * Hash((int)(x * 0.5f) + 7, (int)(y * 0.5f));

        /// <summary>
        /// The rim, from the outside in: a black outline, a thick rounded metal band lit from above with
        /// a highlight along it, a groove, a thin sunken inlay, and a shadow falling onto the fill.
        /// </summary>
        private static Color RimPixel(float x, float y, int width, int height)
        {
            float depth = -BoxDistance(x, y, width, 5f);
            if (depth < 0f) return Color.clear;
            float light = Vector2.Dot(BoxNormal(x, y, width, 5f), Light);
            if (depth < Outline) return Grey(0.05f, 1f);
            if (depth < BandEnd)
            {
                // Across the band the surface turns from facing out to facing in, like a round bar.
                float h = 1f - 2f * (depth - Outline) / (BandEnd - Outline);
                float diffuse = 0.62f * light * h + 0.78f * Mathf.Sqrt(Mathf.Max(0f, 1f - h * h));
                float shine = Mathf.Pow(Mathf.Max(0f, diffuse), 10f);
                return Grey(Mathf.Clamp01((0.2f + 0.62f * diffuse) * Worn(x, y) + 0.35f * shine), 1f);
            }
            if (depth < GrooveEnd) return Grey(0.07f, 1f);
            if (depth < InlayEnd) return Grey(Mathf.Clamp01(0.42f - 0.2f * light) * Worn(x, y), 1f);
            if (depth < RimDepth) return Grey(0.04f, 0.95f);
            float fade = 1f - (depth - RimDepth) / (FrameBorder - RimDepth);
            return Grey(0f, 0.7f * fade * fade);
        }

        /// <summary>A soft shadow outside the panel; the centre stays clear so the fill sets the opacity.</summary>
        private static Color ShadowPixel(float x, float y, int width, int height)
        {
            float half = width * 0.5f;
            float inner = half - ShadowBorder;
            float dx = Mathf.Max(Mathf.Abs(x - half) - inner, 0f);
            float dy = Mathf.Max(Mathf.Abs(y - half) - inner, 0f);
            if (dx <= 0f && dy <= 0f) return Color.clear;
            // Distance from the panel's edge, which sits ShadowBorder in from the sprite's edge.
            float a = 1f - Mathf.Clamp01(new Vector2(dx, dy).magnitude / ShadowBorder);
            return Grey(0f, a * a);
        }

        /// <summary>
        /// Shading for a flat metal piece: an outline, a bevel lit from above along its edge, and a
        /// worn top. <paramref name="d"/> is the signed distance to its edge, <paramref name="n"/> the
        /// direction out of it.
        /// </summary>
        private static Color Metal(float d, Vector2 n, float x, float y)
        {
            if (d > 0f) return Color.clear;
            float depth = -d;
            if (depth < Outline) return Grey(0.05f, 1f);
            if (depth < 4.4f)
            {
                float t = (depth - Outline) / (4.4f - Outline);
                return Grey(Mathf.Clamp01(0.62f + 0.5f * Vector2.Dot(n, Light) * (1f - t)) * Worn(x, y), 1f);
            }
            return Grey(0.58f * Worn(x, y), 1f);
        }

        /// <summary>A pyramid shaped stud: four facets meeting at its centre, ringed by a groove.</summary>
        private static Color Stud(float d, Vector2 p, Vector2 centre)
        {
            if (d > 0f) return Color.clear;
            if (d > -Outline) return Grey(0.05f, 1f);
            var facet = new Vector2(Mathf.Sign(p.x - centre.x), Mathf.Sign(p.y - centre.y)).normalized;
            return Grey(Mathf.Clamp01(0.6f + 0.42f * Vector2.Dot(facet, Light)) * Worn(p.x, p.y), 1f);
        }

        /// <summary>A round rivet head, lit from above.</summary>
        private static Color Rivet(Vector2 p, Vector2 centre)
        {
            var q = (p - centre) / RivetRadius;
            float r = q.magnitude;
            if (r > 1f) return Color.clear;
            if (r > 0.75f) return Grey(0.05f, 1f);
            q /= 0.75f;
            float z = Mathf.Sqrt(Mathf.Max(0f, 1f - q.sqrMagnitude));
            return Grey(Mathf.Clamp01(0.3f + 0.75f * Vector3.Dot(new Vector3(q.x, q.y, z), new Vector3(Light.x * 0.6f, Light.y * 0.6f, 0.8f))), 1f);
        }

        private static float Diamond(Vector2 p, Vector2 centre, float radius) =>
            (Mathf.Abs(p.x - centre.x) + Mathf.Abs(p.y - centre.y) - radius) * 0.7071f;

        private static Vector2 Normal(System.Func<Vector2, float> sdf, Vector2 p)
        {
            const float e = 0.5f;
            var n = new Vector2(sdf(p + new Vector2(e, 0f)) - sdf(p - new Vector2(e, 0f)),
                sdf(p + new Vector2(0f, e)) - sdf(p - new Vector2(0f, e)));
            return n.sqrMagnitude > 0f ? n.normalized : Vector2.zero;
        }

        private static readonly Vector2[] BracketShape =
        {
            new Vector2(0.5f, 0.5f), new Vector2(47.5f, 0.5f), new Vector2(40f, 10.5f),
            new Vector2(10.5f, 10.5f), new Vector2(10.5f, 40f), new Vector2(0.5f, 47.5f),
        };

        /// <summary>
        /// A bottom left corner piece: an L shaped bracket with pointed ends along both edges, a
        /// rivet on each arm and a faceted stud where they meet, which holds a gem.
        /// </summary>
        private static Color CornerPixel(float x, float y, int width, int height)
        {
            var p = new Vector2(x, y);
            var centre = new Vector2(StudCentre, StudCentre);
            float stud = Diamond(p, centre, StudRadius);
            if (stud <= 0f) return Stud(stud, p, centre);
            Color rivet = Rivet(p, new Vector2(RivetAt, RivetInset));
            if (rivet.a > 0f) return rivet;
            rivet = Rivet(p, new Vector2(RivetInset, RivetAt));
            if (rivet.a > 0f) return rivet;
            float bracket = Polygon(p, BracketShape);
            // A dark seam where the stud sits on the bracket.
            if (bracket <= -Outline && stud < 1.2f) return Grey(0.06f, 1f);
            return Metal(bracket, Normal(q => Polygon(q, BracketShape), p), x, y);
        }

        private static readonly Vector2[] CrestWings =
        {
            new Vector2(1.5f, 13f), new Vector2(22f, 19.5f), new Vector2(50f, 19.5f),
            new Vector2(70.5f, 13f), new Vector2(50f, 6.5f), new Vector2(22f, 6.5f),
        };

        /// <summary>The top crest: a faceted stud for a gem between two tapering wings.</summary>
        private static Color CrestPixel(float x, float y, int width, int height)
        {
            var p = new Vector2(x, y);
            var centre = new Vector2(width * 0.5f, height * 0.5f);
            float stud = Diamond(p, centre, 12.5f);
            if (stud <= 0f) return Stud(stud, p, centre);
            float wings = Polygon(p, CrestWings);
            if (wings <= -Outline && stud < 1.2f) return Grey(0.06f, 1f);
            return Metal(wings, Normal(q => Polygon(q, CrestWings), p), x, y);
        }

        /// <summary>Signed distance to a simple polygon: negative inside.</summary>
        private static float Polygon(Vector2 p, Vector2[] v)
        {
            float d = Vector2.Dot(p - v[0], p - v[0]);
            float s = 1f;
            for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
            {
                Vector2 e = v[j] - v[i];
                Vector2 w = p - v[i];
                Vector2 b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Vector2.Dot(e, e));
                d = Mathf.Min(d, Vector2.Dot(b, b));
                bool c1 = p.y >= v[i].y, c2 = p.y < v[j].y, c3 = e.x * w.y > e.y * w.x;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
            }
            return s * Mathf.Sqrt(d);
        }

        /// <summary>A round cut gem: a dark rim and shading lit from the top left.</summary>
        private static Color GemPixel(float x, float y, int size, int height)
        {
            Vector2 q = GemPoint(x, y, size, out float r);
            if (r > 1f) return Color.clear;
            if (r > GemCut) return Grey(0.12f, 1f);
            float z = Mathf.Sqrt(Mathf.Max(0f, 1f - q.sqrMagnitude));
            float diffuse = Mathf.Max(0f, Vector3.Dot(new Vector3(q.x, q.y, z), new Vector3(-0.45f, 0.55f, 0.7f).normalized));
            return Grey(0.3f + 0.7f * diffuse, 1f);
        }

        /// <summary>
        /// The gem's glint, drawn white over the tinted gem: a tinted sprite can never be brighter
        /// than its tint.
        /// </summary>
        private static Color GlintPixel(float x, float y, int size, int height)
        {
            Vector2 q = GemPoint(x, y, size, out float r);
            if (r > GemCut) return Color.clear;
            float spot = 1f - Mathf.Clamp01((q - new Vector2(-0.35f, 0.4f)).magnitude / 0.3f);
            return Grey(1f, spot * spot * 0.9f);
        }

        private const float GemCut = 0.82f;

        /// <summary>A pixel as a point on the gem's face, where 1 is its edge; <paramref name="r"/> counts the rim.</summary>
        private static Vector2 GemPoint(float x, float y, int size, out float r)
        {
            float half = size * 0.5f;
            var p = new Vector2(x - half, y - half) / (half - 0.5f);
            r = p.magnitude;
            return p / GemCut;
        }

        /// <summary>A seamless greyscale grain, tiled over the fill so it looks like worn stone or leather.</summary>
        private static Sprite Grain()
        {
            var tex = new Texture2D(GrainSize, GrainSize, TextureFormat.RGBA32, false)
            {
                name = "RuneUI_FrameGrain",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
            };
            var px = new Color[GrainSize * GrainSize];
            for (int y = 0; y < GrainSize; y++)
            for (int x = 0; x < GrainSize; x++)
            {
                float v = 0.55f * TiledNoise(x, y, 8) + 0.3f * TiledNoise(x, y, 16) + 0.15f * Hash(x, y);
                px[y * GrainSize + x] = Grey(0.8f + 0.2f * v, 1f);
            }
            tex.SetPixels(px);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, GrainSize, GrainSize), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }

        /// <summary>Smooth value noise with <paramref name="cells"/> cells across the texture, wrapping at its edges.</summary>
        private static float TiledNoise(int x, int y, int cells)
        {
            float fx = x * cells / (float)GrainSize, fy = y * cells / (float)GrainSize;
            int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
            float tx = Mathf.SmoothStep(0f, 1f, fx - x0), ty = Mathf.SmoothStep(0f, 1f, fy - y0);
            float Corner(int cx, int cy) => Hash(((cx % cells) + cells) % cells + cells * 31, ((cy % cells) + cells) % cells + cells * 17);
            float a = Mathf.Lerp(Corner(x0, y0), Corner(x0 + 1, y0), tx);
            float b = Mathf.Lerp(Corner(x0, y0 + 1), Corner(x0 + 1, y0 + 1), tx);
            return Mathf.Lerp(a, b, ty);
        }
    }
}
