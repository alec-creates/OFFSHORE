using UnityEngine;

namespace Offshore
{
    /// <summary>
    /// Cosmetic motion for the hull model: bobs and tilts with the swell under it, lifts its bow with speed,
    /// banks into turns, and switches the wake trails on when moving. Physics is untouched.
    /// </summary>
    public class BoatVisuals : MonoBehaviour
    {
        [SerializeField] BoatController boat;
        [SerializeField] SeaSurface sea;
        [Tooltip("The hull model; its local position/rotation are driven here.")]
        [SerializeField] Transform model;
        [SerializeField] TrailRenderer[] wakes = new TrailRenderer[0];

        [Header("Motion")]
        [SerializeField] float hullLength = 9f;
        [SerializeField] float hullBeam = 3.4f;
        [Tooltip("How much of the swell's height and tilt the hull follows.")]
        [SerializeField, Range(0f, 1.5f)] float waveFollow = 0.8f;
        [SerializeField] float bowLiftDegrees = 4f;
        [SerializeField] float bankDegrees = 5f;
        [SerializeField] float smoothing = 6f;
        [Tooltip("Wakes appear above this speed, m/s.")]
        [SerializeField] float wakeSpeed = 0.8f;

        float heave;
        float pitch;
        float roll;

        void LateUpdate()
        {
            if (boat == null || model == null) return;

            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f) return;

            float targetHeave = 0f, targetPitch = 0f, targetRoll = 0f;
            if (sea != null)
            {
                Transform t = boat.transform;
                Vector3 c = t.position;
                Vector3 f = t.forward * (hullLength * 0.5f);
                Vector3 r = t.right * (hullBeam * 0.5f);
                float bow = sea.HeightAt(c + f), stern = sea.HeightAt(c - f);
                float starboard = sea.HeightAt(c + r), port = sea.HeightAt(c - r);
                float baseLevel = sea.transform.position.y;

                targetHeave = ((bow + stern + starboard + port) * 0.25f - baseLevel) * waveFollow;
                targetPitch = -Mathf.Atan2(bow - stern, hullLength) * Mathf.Rad2Deg * waveFollow;
                targetRoll = Mathf.Atan2(starboard - port, hullBeam) * Mathf.Rad2Deg * waveFollow;
            }

            float speed01 = boat.SpeedFraction;
            targetPitch -= bowLiftDegrees * Mathf.Clamp01(boat.ForwardSpeed / boat.MaxForwardSpeed);
            targetRoll -= boat.SteerInput * bankDegrees * speed01;

            float blend = 1f - Mathf.Exp(-smoothing * deltaTime);
            heave = Mathf.Lerp(heave, targetHeave, blend);
            pitch = Mathf.Lerp(pitch, targetPitch, blend);
            roll = Mathf.Lerp(roll, targetRoll, blend);

            model.localPosition = new Vector3(0f, heave, 0f);
            model.localRotation = Quaternion.Euler(pitch, 0f, roll);

            bool wake = boat.Speed > wakeSpeed;
            foreach (var trail in wakes)
            {
                if (trail != null) trail.emitting = wake;
            }
        }
    }
}
