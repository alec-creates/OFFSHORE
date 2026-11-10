using UnityEngine;

namespace Offshore
{
    /// <summary>
    /// High-angle orthographic camera with a fixed yaw. Follows the boat smoothly, leads it in the direction
    /// of travel so more water ahead is visible, and zooms within limits. It never rotates with the boat.
    /// The rig sits on the focus point; the camera is a child pulled back along the view direction.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class HighAngleCamera : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] BoatController targetBoat;
        [SerializeField] Camera view;
        [SerializeField] HelmInput input;

        [Header("Angle")]
        [SerializeField, Range(20f, 89f)] float pitch = 50f;
        [SerializeField] float yaw = 45f;
        [Tooltip("How far the camera sits back from the focus point; only affects clipping in orthographic view.")]
        [SerializeField] float distance = 150f;

        [Header("Follow")]
        [SerializeField] float followSmoothTime = 0.35f;
        [Tooltip("Seconds of travel to lead the boat by.")]
        [SerializeField] float lookAheadTime = 1.2f;
        [SerializeField] float maxLookAhead = 10f;
        [SerializeField] float lookAheadSmoothTime = 0.8f;

        [Header("Zoom (orthographic size)")]
        [SerializeField] float minZoom = 10f;
        [SerializeField] float maxZoom = 38f;
        [SerializeField] float zoom = 20f;
        [SerializeField] float zoomStep = 3f;
        [SerializeField] float zoomKeySpeed = 18f;
        [SerializeField] float zoomSmoothTime = 0.15f;

        Vector3 focus;
        Vector3 focusVelocity;
        Vector3 lookAhead;
        Vector3 lookAheadVelocity;
        float targetZoom;
        float zoomVelocity;

        public float MinZoom => minZoom;
        public float MaxZoom => maxZoom;
        public float TargetZoom => targetZoom;
        public Vector3 Focus => focus;
        public Quaternion ViewRotation => Quaternion.Euler(pitch, yaw, 0f);

        void Start()
        {
            targetZoom = Mathf.Clamp(zoom, minZoom, maxZoom);
            if (view != null)
            {
                view.orthographic = true;
                view.orthographicSize = targetZoom;
            }
            SnapToTarget();
        }

        public void SetZoom(float size) => targetZoom = Mathf.Clamp(size, minZoom, maxZoom);

        public void SnapToTarget()
        {
            if (target == null) return;
            focus = Flat(target.position);
            focusVelocity = Vector3.zero;
            lookAhead = Vector3.zero;
            lookAheadVelocity = Vector3.zero;
            Apply();
        }

        void LateUpdate()
        {
            if (target == null) return;
            float deltaTime = Time.deltaTime;

            if (input != null)
            {
                targetZoom -= input.ZoomSteps * zoomStep;
                targetZoom -= input.ZoomHeld * zoomKeySpeed * Time.unscaledDeltaTime;
                targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
            }

            if (deltaTime > 0f)
            {
                Vector3 velocity = targetBoat != null ? targetBoat.Velocity : Vector3.zero;
                Vector3 desiredLead = Vector3.ClampMagnitude(velocity * lookAheadTime, maxLookAhead);
                lookAhead = Vector3.SmoothDamp(lookAhead, desiredLead, ref lookAheadVelocity, lookAheadSmoothTime, Mathf.Infinity, deltaTime);
                focus = Vector3.SmoothDamp(focus, Flat(target.position) + lookAhead, ref focusVelocity, followSmoothTime, Mathf.Infinity, deltaTime);
            }

            if (view != null)
            {
                // Zoom stays responsive while paused, so the player can look around a frozen scene.
                float zoomDelta = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
                view.orthographicSize = Mathf.SmoothDamp(view.orthographicSize, targetZoom, ref zoomVelocity, zoomSmoothTime, Mathf.Infinity, zoomDelta);
            }

            Apply();
        }

        void Apply()
        {
            transform.SetPositionAndRotation(focus, ViewRotation);
            if (view != null)
            {
                view.transform.localPosition = new Vector3(0f, 0f, -distance);
                view.transform.localRotation = Quaternion.identity;
            }
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
