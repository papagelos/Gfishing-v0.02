using UnityEngine;

namespace GalacticFishing.Minigames.HexWorld
{
    public sealed class BillboardToCamera : MonoBehaviour
    {
        [Tooltip("If empty, uses Camera.main")]
        public Camera targetCamera;

        [Tooltip("If true, only rotates around Y so it stays upright.")]
        public bool yAxisOnly = true;

        private void LateUpdate()
        {
            var cam = targetCamera ? targetCamera : Camera.main;
            if (!cam) return;

            Vector3 toCam = cam.transform.position - transform.position;

            if (yAxisOnly)
                toCam.y = 0f;

            if (toCam.sqrMagnitude < 0.0001f) return;

            transform.rotation = Quaternion.LookRotation(toCam.normalized, Vector3.up);
        }
    }
}
