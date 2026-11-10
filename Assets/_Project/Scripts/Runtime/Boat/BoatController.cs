using System;
using UnityEngine;

namespace Offshore
{
    /// <summary>
    /// Arcade boat handling on the water plane. Not a buoyancy simulation: a Rigidbody locked to the surface
    /// (Y position, X/Z rotation frozen) whose forward speed chases the throttle lever, whose sideways slip is
    /// damped like a keel, and whose turn rate grows with speed (with some low-speed authority so it stays forgiving).
    /// W/S move a persistent throttle lever rather than acting as a gas pedal, so the boat cruises (and later
    /// trolls) without holding a key.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class BoatController : MonoBehaviour
    {
        public const float MetersPerSecondToKnots = 1.943844f;

        [Header("Input")]
        [SerializeField] HelmInput input;

        [Header("Engine")]
        [Tooltip("Top speed ahead, m/s (9 m/s ≈ 17.5 kn).")]
        [SerializeField] float maxForwardSpeed = 9f;
        [Tooltip("Top speed astern, m/s.")]
        [SerializeField] float maxReverseSpeed = 3f;
        [Tooltip("How fast speed builds towards the lever setting, m/s².")]
        [SerializeField] float acceleration = 2.2f;
        [Tooltip("Slowing down with the lever pulled back or reversed, m/s².")]
        [SerializeField] float braking = 3f;
        [Tooltip("Slowing down with the lever at STOP (water drag), m/s².")]
        [SerializeField] float coastDeceleration = 0.7f;

        [Header("Throttle lever")]
        [Tooltip("Lever travel per second while W/S is held (1 = full range).")]
        [SerializeField] float throttleRate = 0.8f;
        [Tooltip("Seconds the lever rests at STOP when swept through it, so stopping is easy.")]
        [SerializeField] float stopDetent = 0.35f;

        [Header("Steering")]
        [Tooltip("Turn rate at cruising speed, degrees per second.")]
        [SerializeField] float turnRate = 30f;
        [Tooltip("Fraction of the turn rate available when (nearly) stopped.")]
        [SerializeField, Range(0f, 1f)] float lowSpeedTurnFactor = 0.35f;
        [Tooltip("Speed (fraction of top speed) at which full steering is available.")]
        [SerializeField, Range(0.05f, 1f)] float fullSteeringAt = 0.4f;
        [Tooltip("How quickly the turn rate follows the helm. Lower = more weighty.")]
        [SerializeField] float rudderResponse = 2.5f;
        [Tooltip("How quickly sideways drift dies away, per second. Lower = more slide in turns.")]
        [SerializeField] float lateralGrip = 1.4f;

        [Header("Anchor")]
        [Tooltip("Deceleration while the anchor is down, m/s².")]
        [SerializeField] float anchorDeceleration = 2.5f;

        Rigidbody body;
        float yawRate;
        float detentTimer;
        float lastLeverInput;

        public float ThrottleInput { get; set; }
        public float SteerInput { get; set; }

        /// <summary>Off in tests: the boat then only follows <see cref="ThrottleInput"/>/<see cref="SteerInput"/> set by code.</summary>
        public bool UsePlayerInput { get; set; } = true;

        /// <summary>Lever position: -1 full astern, 0 stop, 1 full ahead.</summary>
        public float Throttle { get; private set; }
        public bool Anchored { get; private set; }

        /// <summary>True while the player pushes the throttle but the anchor is down.</summary>
        public bool ThrottleBlockedByAnchor { get; private set; }

        public float ForwardSpeed { get; private set; }
        public float Speed => body != null ? Flat(body.linearVelocity).magnitude : 0f;
        public float Knots => Speed * MetersPerSecondToKnots;
        public Vector3 Velocity => body != null ? Flat(body.linearVelocity) : Vector3.zero;
        public float MaxForwardSpeed => maxForwardSpeed;
        public float MaxReverseSpeed => maxReverseSpeed;

        /// <summary>Compass heading in degrees, 0 = north (+Z), 90 = east (+X).</summary>
        public float Heading => Mathf.Repeat(Mathf.Atan2(transform.forward.x, transform.forward.z) * Mathf.Rad2Deg, 360f);

        /// <summary>Speed as a fraction of top speed ahead, 0 to 1.</summary>
        public float SpeedFraction => Mathf.Clamp01(Speed / maxForwardSpeed);

        public event Action<bool> AnchorChanged;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        void Update()
        {
            if (UsePlayerInput && input != null)
            {
                ThrottleInput = input.Throttle;
                SteerInput = input.Steer;
                if (input.AnchorPressed) ToggleAnchor();
            }

            UpdateLever(Time.deltaTime);
        }

        void UpdateLever(float deltaTime)
        {
            float push = Mathf.Clamp(ThrottleInput, -1f, 1f);
            ThrottleBlockedByAnchor = Anchored && Mathf.Abs(push) > 0.1f;
            if (Anchored)
            {
                Throttle = 0f;
                lastLeverInput = push;
                return;
            }

            // A fresh press starting at STOP moves off immediately; sweeping through STOP pauses there briefly.
            bool freshPress = Mathf.Abs(lastLeverInput) < 0.1f;
            lastLeverInput = push;

            if (detentTimer > 0f)
            {
                detentTimer -= deltaTime;
                if (!freshPress && Mathf.Abs(push) > 0.1f) return;
                detentTimer = 0f;
            }

            if (Mathf.Abs(push) < 0.01f) return;

            float before = Throttle;
            float after = Mathf.Clamp(before + push * throttleRate * deltaTime, -1f, 1f);
            if (before != 0f && Mathf.Sign(after) != Mathf.Sign(before) || after == 0f)
            {
                after = 0f;
                detentTimer = stopDetent;
            }
            Throttle = after;
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            Vector3 forward = Flat(transform.forward).normalized;
            Vector3 velocity = Flat(body.linearVelocity);
            float forwardSpeed = Vector3.Dot(velocity, forward);
            Vector3 lateral = velocity - forward * forwardSpeed;

            float target = Anchored ? 0f : Throttle >= 0f ? Throttle * maxForwardSpeed : Throttle * maxReverseSpeed;
            float rate;
            if (Anchored) rate = anchorDeceleration;
            else if (Mathf.Abs(target) > Mathf.Abs(forwardSpeed) && (Mathf.Sign(target) == Mathf.Sign(forwardSpeed) || Mathf.Abs(forwardSpeed) < 0.05f)) rate = acceleration;
            else if (Throttle == 0f) rate = coastDeceleration;
            else rate = braking;

            forwardSpeed = Mathf.MoveTowards(forwardSpeed, target, rate * dt);
            lateral *= Mathf.Exp(-(Anchored ? lateralGrip * 3f : lateralGrip) * dt);

            float authority = Mathf.Lerp(lowSpeedTurnFactor, 1f, Mathf.Clamp01(Mathf.Abs(forwardSpeed) / (maxForwardSpeed * fullSteeringAt)));
            float direction = forwardSpeed < -0.2f ? -1f : 1f;
            float targetYaw = Anchored ? 0f : Mathf.Clamp(SteerInput, -1f, 1f) * turnRate * authority * direction;
            yawRate = Mathf.MoveTowards(yawRate, targetYaw, turnRate * rudderResponse * dt);

            ForwardSpeed = forwardSpeed;
            body.linearVelocity = forward * forwardSpeed + lateral;
            body.angularVelocity = new Vector3(0f, yawRate * Mathf.Deg2Rad, 0f);
        }

        public void SetAnchor(bool down)
        {
            if (Anchored == down) return;
            Anchored = down;
            Throttle = 0f;
            detentTimer = 0f;
            AnchorChanged?.Invoke(down);
        }

        public void ToggleAnchor() => SetAnchor(!Anchored);

        /// <summary>Set the lever directly (tests, future autopilot/trolling UI).</summary>
        public void SetThrottle(float lever)
        {
            if (Anchored) return;
            Throttle = Mathf.Clamp(lever, -1f, 1f);
            detentTimer = 0f;
        }

        /// <summary>Move the boat somewhere and stop it (spawning, later the rescue tow).</summary>
        public void Teleport(Vector3 position, float headingDegrees)
        {
            if (body == null) body = GetComponent<Rigidbody>();
            position.y = transform.position.y;
            var rotation = Quaternion.Euler(0f, headingDegrees, 0f);
            body.position = position;
            body.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            yawRate = 0f;
            Throttle = 0f;
            ForwardSpeed = 0f;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
