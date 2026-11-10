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
        }

        static void Shot(Camera camera, Transform rig, Vector3 focus, float size, string file)
        {
            rig.position = focus;
            camera.orthographicSize = size;
            var target = new RenderTexture(1600, 900, 24);
            camera.targetTexture = target;
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
