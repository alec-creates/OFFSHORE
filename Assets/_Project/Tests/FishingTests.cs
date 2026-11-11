using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Offshore.Tests
{
    /// <summary>
    /// Milestone 2 in the real Sea scene: eligibility, the automatic cycle, cancellation, the hold limit and
    /// pause. Fishing is driven through AutoFisher's public methods with shortened timings; key bindings,
    /// visuals and HUD readability still need a manual play test.
    /// </summary>
    public class FishingTests
    {
        static readonly Vector3 OpenWater = new Vector3(0f, 0f, -100f);

        // Ground names as created by the scene builder (Data/Grounds).
        const string HarborFlats = "Harbor Flats";
        const string GullReef = "Gull Reef";
        const string DeepDrop = "Deep Drop";

        BoatController boat;
        AutoFisher fisher;
        CatchHold hold;
        FishingSettings fastSettings;
        FishingSettings sceneSettings;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            GamePause.Set(false);
            yield return SceneManager.LoadSceneAsync("Sea");
            boat = Object.FindAnyObjectByType<BoatController>();
            fisher = Object.FindAnyObjectByType<AutoFisher>();
            hold = fisher.Hold;
            boat.UsePlayerInput = false;
            fisher.UsePlayerInput = false;

            sceneSettings = fisher.Settings;
            fastSettings = Object.Instantiate(sceneSettings);
            fastSettings.DurationScale = 0.03f;
            fisher.Settings = fastSettings;
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            GamePause.Set(false);
            if (fastSettings != null) Object.Destroy(fastSettings);
        }

        IEnumerator AnchorAt(string groundName)
        {
            FishingGroundZone zone = FishingGroundZone.Named(groundName);
            Assert.IsNotNull(zone, $"Ground '{groundName}' should be in the scene");
            boat.Teleport(zone.Center, 0f);
            boat.SetAnchor(true);
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.AreEqual(zone.Ground, fisher.CurrentGround);
        }

        IEnumerator WaitFor(System.Func<bool> condition, float timeout, string what)
        {
            for (float t = 0f; t < timeout && !condition(); t += Time.unscaledDeltaTime) yield return null;
            Assert.IsTrue(condition(), $"Timed out waiting for: {what}. State {fisher.State}, last message: {fisher.LastMessage}");
        }

        // ------------------------------------------------------------ Eligibility

        [UnityTest]
        public IEnumerator CannotStart_InOpenWater()
        {
            boat.Teleport(OpenWater, 0f);
            boat.SetAnchor(true);
            yield return null;

            Assert.IsNull(fisher.CurrentGround);
            Assert.AreEqual(FishingBlock.NoGround, fisher.CanStart());
            Assert.IsFalse(fisher.TryStart());
            StringAssert.Contains("Nearest fishing ground", fisher.LastMessage);
        }

        [UnityTest]
        public IEnumerator CannotStart_WithoutAnchor()
        {
            yield return AnchorAt(HarborFlats);
            boat.SetAnchor(false);

            Assert.AreEqual(FishingBlock.NotAnchored, fisher.CanStart());
            Assert.IsFalse(fisher.TryStart());
            StringAssert.Contains("anchor", fisher.LastMessage.ToLowerInvariant());
            Assert.AreEqual(FishingState.Idle, fisher.State);
        }

        [UnityTest]
        public IEnumerator CannotStart_WhileStillMoving()
        {
            yield return AnchorAt(HarborFlats);
            boat.SetAnchor(false);
            boat.SetThrottle(1f);
            yield return new WaitForSeconds(2f);
            boat.SetAnchor(true);

            Assert.AreEqual(FishingBlock.StillMoving, fisher.CanStart());
            Assert.IsFalse(fisher.TryStart());
            StringAssert.Contains("stop", fisher.LastMessage);

            yield return WaitFor(() => fisher.CanStart() == FishingBlock.None, 8f, "anchored boat to come to rest");
        }

        [UnityTest]
        public IEnumerator CannotStart_WithFullHold()
        {
            yield return AnchorAt(HarborFlats);
            hold.SetCapacity(5f);
            FishSpecies anySpecies = fisher.CurrentGround.Catches[0].species;
            Assert.IsTrue(hold.TryStore(new CaughtFish(anySpecies, 4.8f, "test")));

            Assert.AreEqual(FishingBlock.HoldFull, fisher.CanStart());
            Assert.IsFalse(fisher.TryStart());
            StringAssert.Contains("hold is full", fisher.LastMessage);
        }

        // ------------------------------------------------------------ The automatic cycle

        [UnityTest]
        public IEnumerator Fishing_RepeatsOnItsOwn_AndStoresGroundFish()
        {
            yield return AnchorAt(HarborFlats);
            var seen = new List<FishingState>();
            Assert.IsTrue(fisher.TryStart());

            yield return WaitFor(() =>
            {
                if (seen.Count == 0 || seen[seen.Count - 1] != fisher.State) seen.Add(fisher.State);
                return hold.Count >= 3;
            }, 20f, "three catches without any input");

            Assert.IsTrue(fisher.IsFishing, "Still fishing after several catches");
            foreach (var state in new[] { FishingState.Casting, FishingState.Waiting, FishingState.Bite, FishingState.Reeling, FishingState.Landed })
            {
                CollectionAssert.Contains(seen, state, $"The cycle should pass through {state}");
            }

            var allowed = fisher.CurrentGround.Catches.Select(c => c.species).ToList();
            foreach (var fish in hold.Fish)
            {
                CollectionAssert.Contains(allowed, fish.Species, $"{fish.Name} doesn't live at Harbor Flats");
                Assert.AreEqual(HarborFlats, fish.Ground);
                Assert.That(fish.Kg, Is.InRange(fish.Species.MinKg, fish.Species.MaxKg));
            }
            Assert.AreEqual(hold.Fish.Sum(f => f.Kg), hold.TotalKg, 0.001f);
        }

        [UnityTest]
        public IEnumerator Grounds_HaveDistinctCatches_AndSizes()
        {
            yield return null;
            var rng = new System.Random(7);
            var results = new Dictionary<string, List<CaughtFish>>();
            foreach (var zone in FishingGroundZone.All)
            {
                results[zone.Ground.DisplayName] = Enumerable.Range(0, 600).Select(_ => zone.Ground.RollCatch(rng)).ToList();
            }

            Assert.AreEqual(3, results.Count, "Three fishing grounds");
            var species = results.ToDictionary(r => r.Key, r => new HashSet<string>(r.Value.Select(f => f.Name)));
            Assert.AreEqual(6, species.Values.SelectMany(s => s).Distinct().Count(), "Six species across the grounds");

            var names = results.Keys.ToList();
            for (int i = 0; i < names.Count; i++)
            {
                for (int j = i + 1; j < names.Count; j++)
                {
                    Assert.IsFalse(species[names[i]].SetEquals(species[names[j]]), $"{names[i]} and {names[j]} should have different fish");
                }
            }

            foreach (var fish in results.Values.SelectMany(f => f))
            {
                Assert.That(fish.Kg, Is.InRange(fish.Species.MinKg, fish.Species.MaxKg));
            }
            Assert.Greater(results[DeepDrop].Average(f => f.Kg), results[GullReef].Average(f => f.Kg));
            Assert.Greater(results[GullReef].Average(f => f.Kg), results[HarborFlats].Average(f => f.Kg));
            Assert.Greater(results[HarborFlats].Select(f => f.Kg).Distinct().Count(), 10, "Weights vary");
        }

        // ------------------------------------------------------------ Stopping

        [UnityTest]
        public IEnumerator ExplicitStop_EndsAtOnce_AndLosesTheHookedFish()
        {
            fastSettings.DurationScale = 0.2f;
            yield return AnchorAt(HarborFlats);
            Assert.IsTrue(fisher.TryStart());
            yield return WaitFor(() => fisher.State == FishingState.Reeling, 15f, "a fish being reeled in");

            int before = hold.Count;
            fisher.Stop("You stopped fishing. The anchor is still down.");

            Assert.AreEqual(FishingState.Idle, fisher.State);
            StringAssert.Contains("got away", fisher.LastStopReason);
            Assert.IsTrue(boat.Anchored, "Stopping fishing never raises the anchor");
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(before, hold.Count, "The fish on the line is not stored");
        }

        [UnityTest]
        public IEnumerator HelmInput_StopsFishing_AndKeepsTheAnchorDown()
        {
            fastSettings.DurationScale = 1f;
            yield return AnchorAt(HarborFlats);
            Assert.IsTrue(fisher.TryStart());
            yield return WaitFor(() => fisher.State == FishingState.Waiting, 5f, "waiting for a bite");

            boat.ThrottleInput = 1f;
            yield return null;

            Assert.AreEqual(FishingState.Idle, fisher.State);
            StringAssert.Contains("Helm input", fisher.LastStopReason);
            Assert.IsTrue(boat.Anchored, "Helm input doesn't raise the anchor");
            Assert.AreEqual(0f, boat.Throttle, "Anchor still locks the throttle");

            boat.ThrottleInput = 0f;
            Assert.IsTrue(fisher.TryStart());
            boat.SteerInput = -1f;
            yield return null;
            Assert.AreEqual(FishingState.Idle, fisher.State, "Steering also stops fishing");
        }

        [UnityTest]
        public IEnumerator RaisingTheAnchor_StopsFishing()
        {
            yield return AnchorAt(GullReef);
            Assert.IsTrue(fisher.TryStart());
            yield return null;

            boat.SetAnchor(false);
            yield return null;

            Assert.AreEqual(FishingState.Idle, fisher.State);
            StringAssert.Contains("Anchor raised", fisher.LastStopReason);
        }

        // ------------------------------------------------------------ Storage

        [UnityTest]
        public IEnumerator SmallHold_FillsUp_ThenFishingStopsWithAReason()
        {
            yield return AnchorAt(HarborFlats);
            hold.SetCapacity(3f);
            Assert.IsTrue(fisher.TryStart());

            yield return WaitFor(() => !fisher.IsFishing, 30f, "fishing to stop on a full hold");

            Assert.LessOrEqual(hold.TotalKg, hold.CapacityKg + 0.001f, "Never stores past capacity");
            StringAssert.Contains("hold", fisher.LastStopReason);
            Assert.IsTrue(boat.Anchored);
        }

        [UnityTest]
        public IEnumerator DevEmpty_ClearsTheHold()
        {
            yield return AnchorAt(HarborFlats);
            FishSpecies anySpecies = fisher.CurrentGround.Catches[0].species;
            hold.TryStore(new CaughtFish(anySpecies, 1f, "test"));
            hold.TryStore(new CaughtFish(anySpecies, 2f, "test"));

            hold.DevEmpty();

            Assert.AreEqual(0, hold.Count);
            Assert.AreEqual(0f, hold.TotalKg);
            Assert.AreEqual(hold.CapacityKg, hold.RemainingKg);
        }

        // ------------------------------------------------------------ Pause

        [UnityTest]
        public IEnumerator Pause_FreezesFishingTimersAndCatches()
        {
            fastSettings.DurationScale = 0.3f;
            yield return AnchorAt(HarborFlats);
            Assert.IsTrue(fisher.TryStart());
            yield return WaitFor(() => fisher.State == FishingState.Waiting && fisher.Progress > 0.1f, 5f, "partway through a wait");

            GamePause.Set(true);
            yield return null;
            FishingState state = fisher.State;
            float progress = fisher.Progress;
            int count = hold.Count;
            yield return new WaitForSecondsRealtime(2f);

            Assert.AreEqual(state, fisher.State, "No state change while paused");
            Assert.AreEqual(progress, fisher.Progress, "Timers frozen while paused");
            Assert.AreEqual(count, hold.Count, "No catches while paused");
            Assert.IsTrue(fisher.IsFishing, "Pausing doesn't cancel fishing");

            GamePause.Set(false);
            yield return WaitFor(() => fisher.State != state || fisher.Progress > progress, 3f, "fishing to resume");
        }
    }
}
