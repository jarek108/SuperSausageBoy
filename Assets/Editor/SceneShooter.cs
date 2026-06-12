#if UNITY_EDITOR
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace SuperSausageBoy.EditorTools
{
    /// <summary>
    /// Renders each level scene's camera to a PNG so we can visually verify the
    /// look/layout headlessly. Frames the whole level (not just the start) by
    /// temporarily widening the camera to fit all geometry.
    ///
    /// Run: -executeMethod SuperSausageBoy.EditorTools.SceneShooter.ShootAll
    /// Output: <project>/Screenshots/LevelN.png
    /// </summary>
    public static class SceneShooter
    {
        const int W = 960;   // 3x of 320 for a crisp but readable shot
        const int H = 540;

        public static void ShootAll()
        {
            string dir = Path.Combine(Application.dataPath, "../Screenshots");
            Directory.CreateDirectory(dir);

            for (int n = 1; n <= 5; n++)
                ShootScene($"Assets/Scenes/Level{n}.unity", Path.Combine(dir, $"Level{n}.png"), true);

            Debug.Log("[SSB] Screenshots written to " + dir);
        }

        public static void ShootScene(string scenePath, string outPath, bool fitAll)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            var cam = Object.FindObjectOfType<Camera>();
            if (cam == null) { Debug.LogError("No camera in " + scenePath); return; }

            // Disable the runtime follow so it doesn't fight our framing.
            var follow = cam.GetComponent<SuperSausageBoy.Core.CameraFollow>();
            if (follow != null) follow.enabled = false;
            var ppc = cam.GetComponent<UnityEngine.U2D.PixelPerfectCamera>();
            if (ppc != null) ppc.enabled = false; // let us set custom ortho size

            if (fitAll) FrameAllRenderers(cam);

            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 1;
            cam.targetTexture = rt;
            cam.aspect = (float)W / H;

            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();

            File.WriteAllBytes(outPath, tex.EncodeToPNG());

            cam.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Debug.Log("[SSB] Wrote " + outPath);
        }

        static void FrameAllRenderers(Camera cam)
        {
            var renderers = Object.FindObjectsOfType<SpriteRenderer>();
            if (renderers.Length == 0) return;

            Bounds b = renderers[0].bounds;
            foreach (var r in renderers)
            {
                // skip giant glow/UI; include world sprites
                b.Encapsulate(r.bounds);
            }

            Vector3 center = b.center;
            center.z = -10f;
            cam.transform.position = center;

            float aspect = (float)W / H;
            float vert = b.size.y / 2f;
            float horiz = (b.size.x / 2f) / aspect;
            cam.orthographicSize = Mathf.Max(vert, horiz) * 1.15f + 1f; // padding
        }
    }
}
#endif
