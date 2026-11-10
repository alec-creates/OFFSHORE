using UnityEngine;
using UnityEngine.UI;

namespace Offshore
{
    /// <summary>Minimal helm readout: speed, throttle lever, heading, anchor state, hints and the pause overlay.</summary>
    public class HelmHud : MonoBehaviour
    {
        static readonly string[] Compass = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };

        [SerializeField] BoatController boat;
        [SerializeField] HelmInput input;
        [SerializeField] Text speedText;
        [SerializeField] Text throttleText;
        [SerializeField] RectTransform throttleAheadFill;
        [SerializeField] RectTransform throttleAsternFill;
        [SerializeField] Text headingText;
        [SerializeField] Text anchorText;
        [SerializeField] Text hintText;
        [SerializeField] GameObject pauseOverlay;

        float hintTimer;

        void OnEnable()
        {
            if (boat != null) boat.AnchorChanged += OnAnchorChanged;
            GamePause.Changed += OnPauseChanged;
            OnPauseChanged(GamePause.IsPaused);
        }

        void OnDisable()
        {
            if (boat != null) boat.AnchorChanged -= OnAnchorChanged;
            GamePause.Changed -= OnPauseChanged;
        }

        void OnAnchorChanged(bool down) => ShowHint(down ? "Anchor down. Holding position." : "Anchor up.");

        void OnPauseChanged(bool paused)
        {
            if (pauseOverlay != null) pauseOverlay.SetActive(paused);
        }

        public void ShowHint(string message, float seconds = 2.5f)
        {
            hintText.text = message;
            hintTimer = seconds;
        }

        void Update()
        {
            if (input != null && input.PausePressed) GamePause.Toggle();
            if (boat == null) return;

            speedText.text = $"{boat.Knots:0.0} kn";

            float lever = boat.Throttle;
            throttleText.text = Mathf.Abs(lever) < 0.01f ? "STOP"
                : lever > 0f ? $"AHEAD {lever * 100f:0}%"
                : $"ASTERN {-lever * 100f:0}%";
            throttleAheadFill.anchorMax = new Vector2(1f, Mathf.Lerp(0.5f, 1f, Mathf.Max(0f, lever)));
            throttleAsternFill.anchorMin = new Vector2(0f, Mathf.Lerp(0.5f, 0f, Mathf.Max(0f, -lever)));

            float heading = boat.Heading;
            headingText.text = $"{Compass[Mathf.RoundToInt(heading / 45f) % 8]}  {heading:000}°";

            anchorText.text = boat.Anchored ? "ANCHORED" : "";

            if (boat.ThrottleBlockedByAnchor) ShowHint("Anchor is down. Press Space to raise it.", 0.5f);

            if (hintTimer > 0f)
            {
                hintTimer -= Time.unscaledDeltaTime;
                if (hintTimer <= 0f) hintText.text = "";
            }
        }
    }
}
