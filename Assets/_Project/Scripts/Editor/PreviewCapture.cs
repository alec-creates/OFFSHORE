using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Offshore.EditorTools
{
    /// <summary>
    /// Renders the Sea scene's camera to PNGs in Previews/ (outside Assets), at a few zoom levels.
    /// Menu: OFFSHORE > Capture Preview Images. Batch: -executeMethod Offshore.EditorTools.PreviewCapture.CaptureFromCommandLine
    /// Edit-mode render: waves are at time 0 and wakes are off.
    /// </summary>
    public static class PreviewCapture
    {
        const string Folder = "Previews";

        [MenuItem("OFFSHORE/Capture Preview Images")]
        public static void CaptureFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Capture();
            EditorUtility.RevealInFinder(Folder);
        }

        public static void CaptureFromCommandLine()
        {
            try
            {
                Capture();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        static void Capture()
        {
            EditorSceneManager.OpenScene(WorldBuilder.ScenePath, OpenSceneMode.Single);
            var rig = UnityEngine.Object.FindAnyObjectByType<HighAngleCamera>();
            var camera = rig.GetComponentInChildren<Camera>();
            Directory.CreateDirectory(Folder);

            // The very first render after opening can come out with placeholder shading; throw one away.
            Shot(camera, rig.transform, WorldBuilder.BoatSpawn, 14f, null);

            Shot(camera, rig.transform, WorldBuilder.BoatSpawn, 14f, "boat_close.png");
            Shot(camera, rig.transform, WorldBuilder.BoatSpawn + new Vector3(0f, 0f, 20f), 38f, "harbor_far.png");
            Shot(camera, rig.transform, new Vector3(110f, 0f, 40f), 38f, "gull_island.png");

            foreach (var (name, center, _) in WorldBuilder.GroundLayout)
            {
                Shot(camera, rig.transform, new Vector3(center.x, 0f, center.y), 38f, $"ground_{name.Replace(' ', '_').ToLowerInvariant()}.png");
            }

            // Boat with the fishing rig shown mid-wait: move it to a ground and float the bobber out.
            var boat = UnityEngine.Object.FindAnyObjectByType<BoatController>();
            var bobber = GameObject.Find("Bobber");
            var line = UnityEngine.Object.FindAnyObjectByType<LineRenderer>();
            var (_, flats, _) = WorldBuilder.GroundLayout[0];
            boat.transform.SetPositionAndRotation(new Vector3(flats.x, 0f, flats.y), Quaternion.identity);
            Vector3 tip = GameObject.Find("Rod Tip").transform.position;
            Vector3 target = boat.transform.TransformPoint(new Vector3(5f, 0f, -13f));
            bobber.transform.position = new Vector3(target.x, 0.05f, target.z);
            line.positionCount = 2;
            line.SetPositions(new[] { tip, bobber.transform.position });
            line.enabled = true;
            Shot(camera, rig.transform, boat.transform.position + new Vector3(0f, 0f, -5f), 12f, "boat_fishing.png");

            CaptureHudLayout(camera, rig.transform, boat.transform.position + new Vector3(0f, 0f, -5f));
        }

        /// <summary>
        /// HUD layout check: the overlay canvas is drawn through the camera with sample fishing text, since
        /// HUD scripts don't run in edit mode. Shows sizes and overlaps only, not live behaviour.
        /// </summary>
        static void CaptureHudLayout(Camera camera, Transform rig, Vector3 focus)
        {
            var canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
            void Set(string path, string text)
            {
                var t = canvas.transform.Find(path);
                if (t != null) t.GetComponent<UnityEngine.UI.Text>().text = text;
            }

            Set("Helm/Speed", "0.0 kn");
            Set("Helm/Throttle", "STOP");
            Set("Helm/Anchor", "ANCHORED");
            Set("Heading", "N  000°");
            Set("Ground Card/Title", "HARBOR FLATS");
            Set("Ground Card/Info", "Depth 3–6 m · Sand and seagrass\nDanger: Safe\nFish: Mackerel, Flounder, Sea Bass");
            Set("Fishing Status/Text", "Reeling it in...\n<size=18>F stop fishing · W/S/A/D or Space also stop</size>");
            Set("Fishing Notice", "Caught a 3.4 kg Flounder!");
            Set("Hold Gauge/Text", "HOLD  23.5 / 80 kg   (9 fish)\n<size=16>Tab: inspect</size>");
            var statusFill = canvas.transform.Find("Fishing Status/Bar/Fill") as RectTransform;
            if (statusFill != null) statusFill.anchorMax = new Vector2(0.6f, 1f);
            var holdFill = canvas.transform.Find("Hold Gauge/Bar/Fill") as RectTransform;
            if (holdFill != null) holdFill.anchorMax = new Vector2(0.3f, 1f);
            var panel = canvas.transform.Find("Hold Panel");
            if (panel != null)
            {
                panel.gameObject.SetActive(true);
                Set("Hold Panel/List", "<b>HOLD</b>   23.5 / 80 kg used, 56.5 kg free\n\nMackerel          x5     4.1 kg   (biggest 1.2 kg)\nFlounder          x3    14.2 kg   (biggest 4.0 kg)\nSea Bass          x1     5.2 kg   (biggest 5.2 kg)\n\n<b>Latest</b>\n  3.4 kg Flounder  ·  Harbor Flats\n  0.8 kg Mackerel  ·  Harbor Flats\n\n<color=#ff9a6a>DEV ONLY: F9 empties the hold</color>");
            }

            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 2f;
            Shot(camera, rig, focus, 12f, "hud_layout.png");
        }

        static void Shot(Camera camera, Transform rig, Vector3 focus, float size, string file)
        {
            rig.position = focus;
            camera.orthographicSize = size;
            var target = new RenderTexture(1600, 900, 24);
            camera.targetTexture = target;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            if (file != null) File.WriteAllBytes(Path.Combine(Folder, file), image.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = null;
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(image);
        }
    }
}
