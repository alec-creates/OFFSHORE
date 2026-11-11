using System;
using UnityEngine;

namespace Offshore
{
    public enum FishingState
    {
        Idle,
        Casting,
        Waiting,
        Bite,
        Reeling,
        Landed,
    }

    public enum FishingBlock
    {
        None,
        AlreadyFishing,
        NotAnchored,
        StillMoving,
        NoGround,
        HoldFull,
    }

    /// <summary>
    /// Semi-idle rod fishing. Once started (F) on an anchored, stopped boat over a fishing ground, it repeats
    /// cast → wait → bite → reel → land on its own, storing each fish in the hold, until the hold is full or
    /// the player stops it. No reflex input is needed.
    /// It stops at once on F, on any helm input (throttle or steering) or when the anchor is raised. Stopping
    /// never touches the anchor. All timers run on scaled game time, so pausing freezes fishing.
    /// </summary>
    [DefaultExecutionOrder(10)] // after BoatController has read this frame's helm input
    public class AutoFisher : MonoBehaviour
    {
        [SerializeField] BoatController boat;
        [SerializeField] CatchHold hold;
        [SerializeField] HelmInput input;
        [SerializeField] FishingSettings settings;

        readonly System.Random rng = new System.Random();
        float stateTime;
        float stateDuration;
        FishingGroundZone currentZone;

        /// <summary>Off in tests: fishing then only starts and stops through the public methods.</summary>
        public bool UsePlayerInput { get; set; } = true;

        public FishingSettings Settings
        {
            get => settings;
            set => settings = value;
        }

        public BoatController Boat => boat;
        public CatchHold Hold => hold;
        public FishingState State { get; private set; }
        public bool IsFishing => State != FishingState.Idle;

        /// <summary>0–1 through the current timed step.</summary>
        public float Progress => stateDuration > 0f ? Mathf.Clamp01(stateTime / stateDuration) : 0f;

        public FishingGround CurrentGround => currentZone != null ? currentZone.Ground : null;
        public FishingGroundZone CurrentZone => currentZone;

        /// <summary>The fish on the line from the bite until it is landed or lost.</summary>
        public CaughtFish? Hooked { get; private set; }
        public CaughtFish? LastCatch { get; private set; }
        public string LastMessage { get; private set; } = "";
        public string LastStopReason { get; private set; } = "";

        /// <summary>Player-facing explanation: why fishing could not start, started, or stopped.</summary>
        public event Action<string> Message;
        public event Action<CaughtFish> Landed;
        public event Action<FishingGround> GroundChanged;

        void Update()
        {
            UpdateGround();

            if (UsePlayerInput && input != null && input.FishTogglePressed)
            {
                if (IsFishing) Stop("You stopped fishing. The anchor is still down.");
                else TryStart();
            }

            if (!IsFishing) return;
            if (CheckInterruptions()) return;

            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f) return;

            stateTime += deltaTime;
            if (stateTime >= stateDuration) Advance();
        }

        void UpdateGround()
        {
            FishingGroundZone zone = boat != null ? FishingGroundZone.At(boat.transform.position) : null;
            if (zone == currentZone) return;
            currentZone = zone;
            GroundChanged?.Invoke(CurrentGround);
        }

        bool CheckInterruptions()
        {
            if (!boat.Anchored)
            {
                Stop("Anchor raised, so fishing stopped.");
                return true;
            }
            if (Mathf.Abs(boat.ThrottleInput) > 0.1f || Mathf.Abs(boat.SteerInput) > 0.1f)
            {
                Stop("Helm input stopped fishing. The anchor is still down (Space raises it).");
                return true;
            }
            if (CurrentGround == null)
            {
                Stop("You left the fishing ground, so fishing stopped.");
                return true;
            }
            return false;
        }

        public FishingBlock CanStart()
        {
            if (IsFishing) return FishingBlock.AlreadyFishing;
            UpdateGround();
            if (CurrentGround == null) return FishingBlock.NoGround;
            if (!boat.Anchored) return FishingBlock.NotAnchored;
            if (boat.Speed > settings.MaxStartSpeed) return FishingBlock.StillMoving;
            if (hold.RemainingKg < settings.MinimumFreeKg) return FishingBlock.HoldFull;
            return FishingBlock.None;
        }

        public string Explain(FishingBlock block) => block switch
        {
            FishingBlock.AlreadyFishing => "Already fishing. Press F to stop.",
            FishingBlock.NoGround => NoGroundText(),
            FishingBlock.NotAnchored => "Drop anchor first (Space), then press F to fish.",
            FishingBlock.StillMoving => "Wait for the boat to stop before fishing.",
            FishingBlock.HoldFull => $"The hold is full ({hold.TotalKg:0.0} / {hold.CapacityKg:0} kg). No room for more fish.",
            _ => "",
        };

        string NoGroundText()
        {
            FishingGroundZone nearest = boat != null ? FishingGroundZone.Nearest(boat.transform.position) : null;
            return nearest == null
                ? "There are no fish here. Find a fishing ground."
                : $"There are no fish here. Nearest fishing ground: {nearest.Ground.DisplayName}.";
        }

        /// <summary>Start the automatic cycle, or explain why it can't start.</summary>
        public bool TryStart()
        {
            FishingBlock block = CanStart();
            if (block != FishingBlock.None)
            {
                Say(Explain(block));
                return false;
            }

            Hooked = null;
            Enter(FishingState.Casting, settings.CastSeconds);
            Say($"Fishing at {CurrentGround.DisplayName}. Press F to stop.");
            return true;
        }

        /// <summary>Stop immediately. A fish on the line is lost; the anchor is left as it is.</summary>
        public void Stop(string reason)
        {
            if (!IsFishing) return;

            if (Hooked.HasValue && (State == FishingState.Bite || State == FishingState.Reeling))
            {
                reason += $" The {Hooked.Value.Name} got away.";
            }

            Hooked = null;
            State = FishingState.Idle;
            stateTime = stateDuration = 0f;
            LastStopReason = reason;
            Say(reason);
        }

        void Advance()
        {
            switch (State)
            {
                case FishingState.Casting:
                    Enter(FishingState.Waiting, CurrentGround.RollBiteDelay(rng) * settings.DurationScale);
                    break;

                case FishingState.Waiting:
                    Hooked = CurrentGround.RollCatch(rng);
                    Enter(FishingState.Bite, settings.BiteSeconds);
                    break;

                case FishingState.Bite:
                    Enter(FishingState.Reeling, Hooked.Value.Species.ReelSecondsFor(Hooked.Value.Kg) * settings.DurationScale);
                    break;

                case FishingState.Reeling:
                    Land();
                    break;

                case FishingState.Landed:
                    if (hold.RemainingKg < settings.MinimumFreeKg)
                    {
                        Stop($"The hold is full ({hold.TotalKg:0.0} / {hold.CapacityKg:0} kg). Fishing stopped.");
                    }
                    else
                    {
                        Hooked = null;
                        Enter(FishingState.Casting, settings.CastSeconds);
                    }
                    break;
            }
        }

        void Land()
        {
            CaughtFish fish = Hooked.Value;
            if (!hold.TryStore(fish))
            {
                Hooked = null;
                Stop($"No room in the hold for a {fish} ({hold.RemainingKg:0.0} kg free). You released it and stopped fishing.");
                return;
            }

            LastCatch = fish;
            Enter(FishingState.Landed, settings.LandedSeconds);
            Debug.Log($"[OFFSHORE] Caught {fish} at {fish.Ground}. Hold {hold.TotalKg:0.0}/{hold.CapacityKg:0} kg.");
            Landed?.Invoke(fish);
        }

        void Enter(FishingState state, float duration)
        {
            State = state;
            stateTime = 0f;
            stateDuration = Mathf.Max(0.01f, duration);
        }

        void Say(string message)
        {
            LastMessage = message;
            Message?.Invoke(message);
        }
    }
}
