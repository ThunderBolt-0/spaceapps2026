using UnityEngine;

namespace MissionTrainer
{
    /// <summary>Slow, low camera orbit around the base for the title screen and menus.</summary>
    public class MenuOrbit : MonoBehaviour
    {
        public Vector3 target = new Vector3(-4, 2.2f, 4);
        public float radius = 26;
        public float height = 4.5f;
        public float degreesPerSecond = 2.2f;
        public float startAngle = 200;

        float angle;

        void OnEnable() => angle = startAngle;

        void LateUpdate()
        {
            angle += degreesPerSecond * Time.unscaledDeltaTime;
            float a = angle * Mathf.Deg2Rad;
            var pos = target + new Vector3(Mathf.Cos(a) * radius, height, Mathf.Sin(a) * radius);
            transform.position = pos;
            // Look a little to the right of the base, so the title text on the left has open sky behind it.
            transform.rotation = Quaternion.LookRotation(target - pos) * Quaternion.Euler(-4, -14, 0);
        }
    }
}
