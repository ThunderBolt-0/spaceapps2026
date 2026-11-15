using UnityEngine;
using UnityEngine.UI;

namespace MissionTrainer
{
    /// <summary>
    /// Keeps the 1280x720 UI fully on screen for any window shape:
    /// wide windows scale by height, narrow/tall windows (small laptops, portrait tablets) scale by width.
    /// </summary>
    [RequireComponent(typeof(CanvasScaler))]
    public class AdaptiveScaler : MonoBehaviour
    {
        CanvasScaler scaler;
        int lastW, lastH;

        void Awake() => scaler = GetComponent<CanvasScaler>();

        void Update()
        {
            if (Screen.width == lastW && Screen.height == lastH) return;
            lastW = Screen.width;
            lastH = Screen.height;
            float aspect = Screen.width / (float)Mathf.Max(1, Screen.height);
            float reference = scaler.referenceResolution.x / scaler.referenceResolution.y;
            scaler.matchWidthOrHeight = aspect >= reference ? 1f : 0f;
        }
    }
}
