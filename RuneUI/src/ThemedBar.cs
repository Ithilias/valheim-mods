using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RuneUI
{
    /// <summary>A themed horizontal bar: panel background, fill that shrinks from the right, optional label.</summary>
    internal sealed class ThemedBar
    {
        public readonly RectTransform Root;
        private readonly RectTransform _fillArea;
        private readonly RectTransform _fill;
        private readonly TMP_Text _label;
        private float _shown = -1f;
        private string _text;

        public ThemedBar(string name, Transform parent, Color color, float textSize)
        {
            Root = Theme.NewPanel(name, parent).rectTransform;
            Root.anchorMin = Root.anchorMax = new Vector2(0f, 1f);
            Root.pivot = new Vector2(0f, 1f);

            // The fill sits inside the border so the border stays visible at any value.
            _fillArea = Theme.NewRect("FillArea", Root);
            Theme.Stretch(_fillArea, Mathf.Max(1f, Plugin.BorderWidth.Value));
            _fillArea.SetAsFirstSibling();
            _fill = Theme.NewImage("Fill", _fillArea, Theme.FillSprite, color).rectTransform;
            _fill.anchorMin = Vector2.zero;
            _fill.anchorMax = Vector2.one;
            _fill.offsetMin = _fill.offsetMax = Vector2.zero;

            if (textSize > 0f)
            {
                _label = Theme.NewText("Label", Root, textSize, TextAlignmentOptions.Center);
                Theme.Stretch((RectTransform)_label.transform);
            }
        }

        public void Layout(float x, float y, float width, float height)
        {
            Root.anchoredPosition = new Vector2(x, -y);
            Root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            Root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        }

        public void SetActive(bool active)
        {
            if (Root.gameObject.activeSelf != active) Root.gameObject.SetActive(active);
        }

        public void SetValue(float fraction, string text)
        {
            fraction = Mathf.Clamp01(fraction);
            if (Mathf.Abs(fraction - _shown) > 0.0005f)
            {
                _shown = fraction;
                _fill.anchorMax = new Vector2(fraction, 1f);
                // A rounded fill narrower than its corners looks broken, so drop it near zero.
                _fill.gameObject.SetActive(fraction > 0.001f);
            }
            if (_label != null && text != _text)
            {
                _text = text;
                _label.text = text;
            }
        }
    }
}
