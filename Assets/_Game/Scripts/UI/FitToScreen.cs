using UnityEngine;

namespace MissionTrainer
{
    /// <summary>
    /// Keeps a pop-up panel fully on screen: a panel that grows with its text can become taller than the
    /// window, so it is scaled down just enough to fit inside its parent (with a small margin).
    /// </summary>
    public class FitToScreen : MonoBehaviour
    {
        public float margin = 14f;

        void LateUpdate()
        {
            var r = (RectTransform)transform;
            var parent = r.parent as RectTransform;
            if (!parent) return;
            float availH = parent.rect.height - margin * 2f;
            float availW = parent.rect.width - margin * 2f;
            float h = r.rect.height, w = r.rect.width;
            float s = 1f;
            if (h > 1f && h > availH) s = Mathf.Min(s, availH / h);
            if (w > 1f && w > availW) s = Mathf.Min(s, availW / w);
            if (!Mathf.Approximately(r.localScale.x, s)) r.localScale = new Vector3(s, s, 1f);
        }
    }
}
