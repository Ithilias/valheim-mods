using System.Collections.Generic;
using UnityEngine;

namespace RuneUI
{
    /// <summary>
    /// Hides vanilla HUD parts by scale, never by deactivating them, so the game's own show and hide
    /// logic keeps working. Remembers each original scale to restore exactly that.
    /// </summary>
    internal sealed class ScaleHider
    {
        private readonly Dictionary<Transform, Vector3> _hidden = new Dictionary<Transform, Vector3>();

        public void Hide(Transform t)
        {
            if (t == null) return;
            if (!_hidden.ContainsKey(t)) _hidden[t] = t.localScale;
            if (t.localScale != Vector3.zero) t.localScale = Vector3.zero;
        }

        public void Restore()
        {
            foreach (var pair in _hidden)
                if (pair.Key != null) pair.Key.localScale = pair.Value;
            _hidden.Clear();
        }

        /// <summary>For when the hidden objects were destroyed with the HUD.</summary>
        public void Forget() => _hidden.Clear();
    }
}
