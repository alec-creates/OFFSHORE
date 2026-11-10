using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Offshore.Tests
{
    /// <summary>
    /// Milestone 1 in the real Sea scene, driving the boat through code instead of the keyboard.
    /// Handling feel, visuals and readability still need a manual play test.
    /// </summary>
    public class BoatAndCameraTests
    {
        static readonly Vector3 OpenWater = new Vector3(0f, 0f, -100f);

        BoatController boat;
        HighAngleCamera rig;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            GamePause.Set(false);
            yield return SceneManager.LoadSceneAsync("Sea");
            boat = Object.FindAnyObjectByType<BoatController>();
            rig = Object.FindAnyObjectByType<HighAngleCamera>();
            boat.UsePlayerInput = false;
            boat.Teleport(OpenWater, 0f);
            rig.SnapToTarget();
            yield return new WaitForFixedUpdate();
        }

        [TearDown]
        public void TearDown() => GamePause.Set(false);

        [UnityTest]
        public IEnumerator ThrottleLever_StaysWhereItWasLeft()
        {
            boat.ThrottleInput = 1f;
            yield return new WaitForSeconds(0.8f);
            boat.ThrottleInput = 0f;
            float lever = boat.Throttle;
            Assert.Greater(lever, 0.4f, "Holding W pushes the lever up");

            yield return new WaitForSeconds(1f);
            Assert.AreEqual(lever, boat.Throttle, 0.0001f, "Releasing W keeps the lever where it is (cruise)");
        }

        [UnityTest]
        public IEnumerator ThrottleLever_PausesAtStop_WhenSweptThrough()
        {
            boat.SetThrottle(0.1f);
            boat.ThrottleInput = -1f;
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(0f, boat.Throttle, "Sweeping down through STOP rests there first");
            yield return new WaitForSeconds(1f);
            Assert.Less(boat.Throttle, -0.1f, "Keep holding S to go astern");
        }

        [UnityTest]
        public IEnumerator FullAhead_BuildsSpeedAlongTheHeading()
        {
            Vector3 start = boat.transform.position;
            boat.SetThrottle(1f);
            yield return new WaitForSeconds(5f);

            Assert.Greater(boat.ForwardSpeed, boat.MaxForwardSpeed * 0.8f);
            Assert.LessOrEqual(boat.Speed, boat.MaxForwardSpeed + 0.1f);
            Vector3 moved = boat.transform.position - start;
            Assert.Greater(moved.z, 20f, "Heading north, so it moves north");
            Assert.Less(Mathf.Abs(moved.x), 1f);
            Assert.AreEqual(0f, boat.transform.position.y, 0.001f, "Boat stays on the water plane");
        }

        [UnityTest]
        public IEnumerator Reverse_IsSlowerThanAhead()
        {
            boat.SetThrottle(-1f);
            yield return new WaitForSeconds(5f);
            Assert.Less(boat.ForwardSpeed, -1f, "Moves astern");
            Assert.GreaterOrEqual(boat.ForwardSpeed, -boat.MaxReverseSpeed - 0.1f);
        }

        [UnityTest]
        public IEnumerator Steering_TurnsMoreAtSpeedThanAtRest()
        {
            boat.SteerInput = 1f;
            yield return new WaitForSeconds(2f);
            float turnedAtRest = Mathf.DeltaAngle(0f, boat.Heading);

            boat.Teleport(OpenWater, 0f);
            boat.SteerInput = 0f;
            boat.SetThrottle(0.7f);
            yield return new WaitForSeconds(3f);
            float before = boat.Heading;
            boat.SteerInput = 1f;
            yield return new WaitForSeconds(2f);
            float turnedAtSpeed = Mathf.DeltaAngle(before, boat.Heading);

            Assert.Greater(turnedAtRest, 3f, "Some steering when stopped keeps it forgiving");
            Assert.Greater(turnedAtSpeed, turnedAtRest * 1.8f, "Steering needs way on the boat");
            Assert.Less(turnedAtSpeed, 90f, "Turning radius stays boat-like");
        }

        [UnityTest]
        public IEnumerator Anchor_StopsAndHoldsTheBoat_AndLocksThrottle()
        {
            boat.SetThrottle(1f);
            yield return new WaitForSeconds(4f);
            boat.SetAnchor(true);
            Assert.AreEqual(0f, boat.Throttle);

            yield return new WaitForSeconds(5f);
            Assert.Less(boat.Speed, 0.05f, "Anchored boat comes to a stop");

            Vector3 held = boat.transform.position;
            boat.ThrottleInput = 1f;
            boat.SteerInput = 1f;
            yield return new WaitForSeconds(1.5f);
            Assert.IsTrue(boat.ThrottleBlockedByAnchor);
            Assert.AreEqual(0f, boat.Throttle);
            Assert.Less(Vector3.Distance(held, boat.transform.position), 0.05f, "Holds position while anchored");

            boat.ThrottleInput = 0f;
            boat.SetAnchor(false);
            boat.SetThrottle(0.5f);
            yield return new WaitForSeconds(1.5f);
            Assert.Greater(boat.Speed, 1f, "Raising the anchor frees the boat");
        }

        [UnityTest]
        public IEnumerator MapEdge_KeepsTheBoatInside()
        {
            boat.Teleport(new Vector3(-100f, 0f, 260f), 0f);
            boat.SetThrottle(1f);
            yield return new WaitForSeconds(10f);
            Assert.Less(boat.transform.position.z, 300f);
        }

        [UnityTest]
        public IEnumerator Camera_FollowsWithFixedAngle_AndLeadsTheBoat()
        {
            Quaternion angle = rig.transform.rotation;
            Camera view = rig.GetComponentInChildren<Camera>();
            Assert.IsTrue(view.orthographic);

            boat.SetThrottle(1f);
            boat.SteerInput = 0.6f;
            yield return new WaitForSeconds(6f);

            Assert.Less(Quaternion.Angle(angle, rig.transform.rotation), 0.01f, "Camera never rotates with the boat");
            Vector3 toFocus = rig.Focus - new Vector3(boat.transform.position.x, 0f, boat.transform.position.z);
            Assert.Less(toFocus.magnitude, 14f, "Camera keeps the boat near the centre");
            Assert.Greater(Vector3.Dot(toFocus, boat.Velocity.normalized), 2f, "Camera leads in the direction of travel");
        }

        [UnityTest]
        public IEnumerator Camera_ZoomStaysWithinLimits()
        {
            Camera view = rig.GetComponentInChildren<Camera>();
            rig.SetZoom(1000f);
            yield return new WaitForSeconds(1f);
            Assert.LessOrEqual(view.orthographicSize, rig.MaxZoom + 0.01f);

            rig.SetZoom(-5f);
            yield return new WaitForSeconds(1f);
            Assert.GreaterOrEqual(view.orthographicSize, rig.MinZoom - 0.01f);
        }

        [UnityTest]
        public IEnumerator Pause_FreezesBoatAndSea()
        {
            SeaSurface sea = Object.FindAnyObjectByType<SeaSurface>();
            boat.SetThrottle(1f);
            yield return new WaitForSeconds(2f);

            GamePause.Set(true);
            yield return null;
            Vector3 position = boat.transform.position;
            float seaTime = sea.SeaTime;
            yield return new WaitForSecondsRealtime(1f);

            Assert.AreEqual(position, boat.transform.position, "Boat frozen while paused");
            Assert.AreEqual(seaTime, sea.SeaTime, "Waves frozen while paused");

            GamePause.Set(false);
            yield return new WaitForSeconds(0.5f);
            Assert.Greater(Vector3.Distance(position, boat.transform.position), 0.5f, "Resumes after unpausing");
        }

        [UnityTest]
        public IEnumerator Sea_HeightStaysWithinTheSwell_AndHarborIsShallow()
        {
            SeaSurface sea = Object.FindAnyObjectByType<SeaSurface>();
            yield return new WaitForSeconds(0.5f);
            for (int i = 0; i < 50; i++)
            {
                float h = sea.HeightAt(i * 7.3f - 150f, i * 3.1f - 80f);
                Assert.LessOrEqual(Mathf.Abs(h - sea.transform.position.y), sea.MaxWaveHeight + 0.001f);
            }

            Assert.Less(sea.DepthFractionAt(0f, -252f), 0.5f, "Harbor water reads as shallow");
            Assert.AreEqual(1f, sea.DepthFractionAt(0f, 0f), 0.001f, "Middle of the map is open deep water");
        }
    }
}
