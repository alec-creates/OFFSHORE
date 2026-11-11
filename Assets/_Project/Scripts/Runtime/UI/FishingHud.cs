using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Offshore
{
    /// <summary>
    /// Fishing UI: the fishing ground card (or directions to the nearest one), fishing status with progress,
    /// notices and catch toasts, the hold gauge, and the hold contents panel (Tab).
    /// </summary>
    public class FishingHud : MonoBehaviour
    {
        const float NoticeSeconds = 4f;

        [SerializeField] AutoFisher fisher;
        [SerializeField] HelmInput input;

        [Header("Ground card")]
        [SerializeField] Text groundTitle;
        [SerializeField] Text groundInfo;
        [SerializeField] Image groundStripe;

        [Header("Status")]
        [SerializeField] GameObject statusGroup;
        [SerializeField] Text statusText;
        [SerializeField] RectTransform statusFill;
        [SerializeField] Image statusFillImage;
        [SerializeField] GameObject statusBar;
        [SerializeField] Text noticeText;

        [Header("Hold")]
        [SerializeField] Text holdText;
        [SerializeField] RectTransform holdFill;
        [SerializeField] Image holdFillImage;
        [SerializeField] GameObject holdPanel;
        [SerializeField] Text holdList;

        float noticeTimer;

        CatchHold Hold => fisher != null ? fisher.Hold : null;

        void OnEnable()
        {
            if (fisher != null)
            {
                fisher.Message += ShowNotice;
                fisher.Landed += OnLanded;
                fisher.GroundChanged += OnGroundChanged;
            }
            if (Hold != null) Hold.Changed += RefreshHoldList;
            if (holdPanel != null) holdPanel.SetActive(false);
            noticeText.text = "";
            RefreshHoldList();
        }

        void OnDisable()
        {
            if (fisher != null)
            {
                fisher.Message -= ShowNotice;
                fisher.Landed -= OnLanded;
                fisher.GroundChanged -= OnGroundChanged;
            }
            if (Hold != null) Hold.Changed -= RefreshHoldList;
        }

        public void ShowNotice(string message)
        {
            noticeText.text = message;
            noticeText.color = new Color(1f, 0.93f, 0.7f, 1f);
            noticeTimer = NoticeSeconds;
        }

        void OnLanded(CaughtFish fish)
        {
            noticeText.text = $"Caught a {fish.Kg:0.0} kg {fish.Name}!";
            noticeText.color = Color.Lerp(fish.Species.Color, Color.white, 0.55f);
            noticeTimer = NoticeSeconds;
        }

        void OnGroundChanged(FishingGround ground)
        {
            ShowNotice(ground != null ? $"Entering {ground.DisplayName}" : "Back in open water");
        }

        void Update()
        {
            if (fisher == null) return;

            if (input != null && input.HoldPanelPressed && holdPanel != null) holdPanel.SetActive(!holdPanel.activeSelf);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (input != null && input.DevEmptyHoldPressed && Hold != null)
            {
                Hold.DevEmpty();
                ShowNotice("[DEV] Hold emptied (development builds only).");
            }
#endif

            UpdateGroundCard();
            UpdateStatus();
            UpdateHoldGauge();

            if (noticeTimer > 0f)
            {
                noticeTimer -= Time.unscaledDeltaTime;
                Color c = noticeText.color;
                noticeText.color = new Color(c.r, c.g, c.b, Mathf.Clamp01(noticeTimer / 0.8f));
            }
        }

        void UpdateGroundCard()
        {
            FishingGround ground = fisher.CurrentGround;
            if (ground != null)
            {
                groundTitle.text = ground.DisplayName.ToUpperInvariant();
                groundInfo.text = $"Depth {ground.Depth} · {ground.Bottom}\nDanger: {ground.Danger}\nFish: {ground.SpeciesList()}";
                groundStripe.color = ground.MarkerColor;
                return;
            }

            groundTitle.text = "OPEN WATER";
            groundStripe.color = new Color(1f, 1f, 1f, 0.25f);
            Vector3 boat = fisher.Boat.transform.position;
            FishingGroundZone nearest = FishingGroundZone.Nearest(boat);
            if (nearest == null)
            {
                groundInfo.text = "No fishing grounds charted.";
                return;
            }

            Vector3 toward = nearest.Center - new Vector3(boat.x, 0f, boat.z);
            float distance = Mathf.Max(0f, toward.magnitude - nearest.Radius);
            float bearing = Mathf.Repeat(Mathf.Atan2(toward.x, toward.z) * Mathf.Rad2Deg, 360f);
            groundInfo.text = $"No fish here.\nNearest ground: {nearest.Ground.DisplayName}\n{distance:0} m {Compass.Point(bearing)} ({bearing:000}°)";
        }

        void UpdateStatus()
        {
            BoatController boat = fisher.Boat;
            string text;
            bool showBar = true;
            Color barColor = new Color(0.45f, 0.8f, 1f);

            switch (fisher.State)
            {
                case FishingState.Casting:
                    text = "Casting...";
                    break;
                case FishingState.Waiting:
                    text = "Waiting for a bite...";
                    barColor = new Color(0.6f, 0.7f, 0.8f);
                    break;
                case FishingState.Bite:
                    text = "<b>BITE!</b>  Fish on!";
                    barColor = new Color(1f, 0.8f, 0.25f);
                    break;
                case FishingState.Reeling:
                    text = "Reeling it in...";
                    barColor = new Color(0.4f, 0.85f, 0.5f);
                    break;
                case FishingState.Landed:
                    text = fisher.LastCatch.HasValue ? $"Landed: {fisher.LastCatch.Value}" : "Landed!";
                    barColor = new Color(0.4f, 0.85f, 0.5f);
                    break;
                default:
                    showBar = false;
                    if (fisher.CurrentGround == null) text = "";
                    else if (!boat.Anchored) text = "Drop anchor (Space), then press F to fish";
                    else if (boat.Speed > fisher.Settings.MaxStartSpeed) text = "Anchored. Waiting for the boat to stop...";
                    else if (Hold.RemainingKg < fisher.Settings.MinimumFreeKg) text = "Hold full: no room for more fish";
                    else text = "Press F to start fishing";
                    break;
            }

            if (fisher.IsFishing) text += "\n<size=18>F stop fishing · W/S/A/D or Space also stop</size>";

            statusGroup.SetActive(!string.IsNullOrEmpty(text));
            statusText.text = text;
            statusBar.SetActive(showBar);
            statusFill.anchorMax = new Vector2(fisher.Progress, 1f);
            statusFillImage.color = barColor;
        }

        void UpdateHoldGauge()
        {
            CatchHold hold = Hold;
            float fill = hold.TotalKg / hold.CapacityKg;
            holdText.text = $"HOLD  {hold.TotalKg:0.0} / {hold.CapacityKg:0} kg   ({hold.Count} fish)\n<size=16>Tab: inspect</size>";
            holdFill.anchorMax = new Vector2(Mathf.Clamp01(fill), 1f);
            holdFillImage.color = fill >= 0.95f ? new Color(0.95f, 0.35f, 0.25f) : fill >= 0.75f ? new Color(1f, 0.75f, 0.25f) : new Color(0.4f, 0.8f, 0.55f);
        }

        void RefreshHoldList()
        {
            CatchHold hold = Hold;
            if (holdList == null || hold == null) return;

            var text = new StringBuilder();
            text.Append($"<b>HOLD</b>   {hold.TotalKg:0.0} / {hold.CapacityKg:0} kg used, {hold.RemainingKg:0.0} kg free\n\n");
            if (hold.Count == 0)
            {
                text.Append("Empty.\n");
            }
            else
            {
                // Per species: count, total and biggest.
                var bySpecies = new Dictionary<string, (int count, float kg, float best)>();
                var order = new List<string>();
                foreach (var fish in hold.Fish)
                {
                    if (!bySpecies.TryGetValue(fish.Name, out var s))
                    {
                        s = (0, 0f, 0f);
                        order.Add(fish.Name);
                    }
                    bySpecies[fish.Name] = (s.count + 1, s.kg + fish.Kg, Mathf.Max(s.best, fish.Kg));
                }
                foreach (string name in order)
                {
                    var s = bySpecies[name];
                    text.Append($"{name,-16} x{s.count,-3} {s.kg,6:0.0} kg   (biggest {s.best:0.0} kg)\n");
                }

                text.Append("\n<b>Latest</b>\n");
                for (int i = hold.Count - 1; i >= Mathf.Max(0, hold.Count - 6); i--)
                {
                    text.Append($"  {hold.Fish[i]}  ·  {hold.Fish[i].Ground}\n");
                }
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            text.Append("\n<color=#ff9a6a>DEV ONLY: F9 empties the hold</color>");
#endif
            holdList.text = text.ToString();
        }
    }
}
