using UnityEngine;

namespace ThaiMystery
{
    // Keeps the camera on the player, but holds still when the whole map fits on screen,
    // which is how small RPG Maker rooms usually look.
    public class CameraFollow : MonoBehaviour
    {
        private Camera cam;
        private Transform target;

        public void Init(Transform followTarget)
        {
            cam = GetComponent<Camera>();
            target = followTarget;
            Snap();
        }

        private void LateUpdate() => Snap();

        private void Snap()
        {
            if (target == null || Game.Map == null) return;
            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;
            var map = Game.Map;

            float x = map.Width <= halfW * 2 ? map.Width / 2f : Mathf.Clamp(target.position.x, halfW, map.Width - halfW);
            // When the room fits, sit it a little high so the dialogue box covers less of it.
            float y = map.Height <= halfH * 2 ? map.Height / 2f - Mathf.Min(1f, halfH - map.Height / 2f) : Mathf.Clamp(target.position.y + 0.5f, halfH, map.Height - halfH);
            transform.position = new Vector3(x, y, -10f);
        }
    }
}
