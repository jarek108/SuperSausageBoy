#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.U2D;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using SuperSausageBoy.Player;
using SuperSausageBoy.Hazards;
using SuperSausageBoy.Level;
using SuperSausageBoy.UI;

namespace SuperSausageBoy.EditorTools
{
    /// <summary>
    /// One-shot project builder: creates URP 2D pipeline assets, sprite refs,
    /// the Player prefab, a HUD, the persistent GameManager, and 5 levels of
    /// increasing difficulty. Run via menu or -executeMethod in batch mode.
    /// </summary>
    public static class GameBuilder
    {
        const int PPU = 16;
        const string GEN = "Assets/Art/Generated";
        const string PREFABS = "Assets/Prefabs";
        const string SCENES = "Assets/Scenes";
        const string SETTINGS = "Assets/Settings";

        const int GroundLayer = 8;

        [MenuItem("SuperSausageBoy/Build Entire Game")]
        public static void BuildAll()
        {
            EnsureLayer(GroundLayer, "Ground");
            EnsureTag("Player");
            EnsureTag("Hazard");
            EnsureTag("OneWayPlatform");

            PlaceholderArtGenerator.Generate();

            Directory.CreateDirectory(PREFABS);
            Directory.CreateDirectory(SCENES);
            Directory.CreateDirectory(SETTINGS);

            var hazardPrefabs = BuildHazardPrefabs();
            var greasePrefab = BuildGreasePrefab();
            var playerPrefab = BuildPlayerPrefab(greasePrefab);
            var goalPrefab = BuildGoalPrefab();
            var tilePrefab = BuildTilePrefab("Tile", false);
            var wallPrefab = BuildTilePrefab("WallTile", false);

            // Build levels
            for (int i = 0; i < 5; i++)
                BuildLevel(i + 1, playerPrefab, goalPrefab, tilePrefab, wallPrefab, hazardPrefabs);

            BuildWinScene();
            ConfigureBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[SSB] BUILD COMPLETE: 5 levels + win scene generated.");
        }

        // ---------- Sprites ----------
        static Sprite Spr(string name) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"{GEN}/{name}.png");

        // ---------- Prefabs ----------

        static GameObject BuildGreasePrefab()
        {
            var go = new GameObject("GreaseSplat");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Spr("GreaseSplat");
            sr.sortingOrder = -1;
            var path = $"{PREFABS}/GreaseSplat.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        static GameObject BuildPlayerPrefab(GameObject grease)
        {
            var go = new GameObject("SuperSausageBoy");
            go.tag = "Player";

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Spr("SausageBoy");
            sr.sortingOrder = 10;

            // Box collider sized to sprite bounds.
            var box = go.AddComponent<BoxCollider2D>();
            var s = sr.sprite.bounds.size;
            box.size = new Vector2(s.x * 0.85f, s.y);

            var controller = go.AddComponent<Controller2D>();
            controller.collisionMask = 1 << GroundLayer;

            var move = go.AddComponent<PlayerMovement>();
            move.pixelsPerUnit = PPU;

            var health = go.AddComponent<PlayerHealth>();
            health.greaseSplatPrefab = grease;

            // Trigger collider child for hazard/goal detection (separate from solid box).
            var trig = go.AddComponent<CircleCollider2D>();
            trig.isTrigger = true;
            trig.radius = s.x * 0.4f;

            // Input
            var pi = go.AddComponent<PlayerInput>();
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                "Assets/Input/PlayerControls.inputactions");
            pi.actions = actions;
            pi.defaultActionMap = "Gameplay";
            pi.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;

            var path = $"{PREFABS}/SuperSausageBoy.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        static GameObject BuildGoalPrefab()
        {
            var go = new GameObject("HotBunGirl");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Spr("HotBunGirl");
            sr.sortingOrder = 5;
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = sr.sprite.bounds.size;
            go.AddComponent<Goal>();
            // glow child
            var glow = new GameObject("Glow");
            glow.transform.SetParent(go.transform);
            glow.transform.localPosition = Vector3.zero;
            var gsr = glow.AddComponent<SpriteRenderer>();
            gsr.sprite = Spr("HotBunGirl");
            gsr.color = new Color(1f, 0.82f, 0.48f, 0.3f);
            gsr.sortingOrder = 4;
            glow.transform.localScale = Vector3.one * 1.6f;

            var path = $"{PREFABS}/HotBunGirl.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        static GameObject BuildTilePrefab(string sprite, bool oneWay)
        {
            var go = new GameObject(sprite);
            if (!oneWay) go.layer = GroundLayer;
            else go.tag = "OneWayPlatform";
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Spr(sprite);
            sr.sortingOrder = 0;
            var col = go.AddComponent<BoxCollider2D>();
            col.size = sr.sprite.bounds.size;
            var path = $"{PREFABS}/{sprite}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        static Dictionary<string, GameObject> BuildHazardPrefabs()
        {
            var dict = new Dictionary<string, GameObject>();

            // Saw
            {
                var go = new GameObject("Saw");
                go.tag = "Hazard";
                var col = go.AddComponent<CircleCollider2D>();
                col.isTrigger = true;
                col.radius = 0.6f;
                var visual = new GameObject("Visual");
                visual.transform.SetParent(go.transform);
                var sr = visual.AddComponent<SpriteRenderer>();
                sr.sprite = Spr("Saw");
                sr.sortingOrder = 3;
                go.AddComponent<SawBlade>();
                var path = $"{PREFABS}/Saw.prefab";
                dict["Saw"] = PrefabUtility.SaveAsPrefabAsset(go, path);
                Object.DestroyImmediate(go);
            }
            // Spike
            {
                var go = new GameObject("Spike");
                go.tag = "Hazard";
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = Spr("Spike");
                sr.sortingOrder = 2;
                var col = go.AddComponent<BoxCollider2D>();
                col.isTrigger = true;
                col.size = new Vector2(sr.sprite.bounds.size.x * 0.6f, sr.sprite.bounds.size.y * 0.6f);
                go.AddComponent<Spike>();
                var path = $"{PREFABS}/Spike.prefab";
                dict["Spike"] = PrefabUtility.SaveAsPrefabAsset(go, path);
                Object.DestroyImmediate(go);
            }
            // Crumbling block
            {
                var go = new GameObject("Crumble");
                go.layer = GroundLayer;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = Spr("WallTile");
                sr.color = new Color(0.8f, 0.6f, 0.4f);
                sr.sortingOrder = 1;
                var col = go.AddComponent<BoxCollider2D>();
                col.size = sr.sprite.bounds.size;
                go.AddComponent<CrumblingBlock>();
                var path = $"{PREFABS}/Crumble.prefab";
                dict["Crumble"] = PrefabUtility.SaveAsPrefabAsset(go, path);
                Object.DestroyImmediate(go);
            }
            return dict;
        }

        // ---------- Levels ----------

        static void BuildLevel(int n, GameObject player, GameObject goal,
            GameObject tile, GameObject wall, Dictionary<string, GameObject> hz)
        {
            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera + Pixel Perfect
            var camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            var cam = camGO.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 180f / 2f / PPU; // 320x180 ref
            cam.backgroundColor = new Color(0.07f, 0.06f, 0.12f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            camGO.transform.position = new Vector3(0, 0, -10);
            var ppc = camGO.AddComponent<UnityEngine.U2D.PixelPerfectCamera>();
            ppc.assetsPPU = PPU;
            ppc.refResolutionX = 320;
            ppc.refResolutionY = 180;
            ppc.pixelSnapping = true;
            ppc.upscaleRT = false;

            // Light2D global (URP 2D needs light to see sprites with lit, but default is unlit -> fine)

            // GameManager (persistent)
            var gm = new GameObject("GameManager");
            gm.AddComponent<LevelManager>();

            // Player + spawn
            var spawn = new GameObject("Spawn");
            spawn.transform.position = LevelLayout.Spawn(n);
            var p = (GameObject)PrefabUtility.InstantiatePrefab(player);
            p.transform.position = spawn.transform.position;
            p.GetComponent<PlayerHealth>().spawnPoint = spawn.transform;

            // Camera follow (simple)
            camGO.AddComponent<SuperSausageBoy.Core.CameraFollow>().target = p.transform;

            // Layout geometry, hazards, goal
            LevelLayout.Build(n, tile, wall, goal, hz, p);

            // Bootstrap
            var boot = new GameObject("LevelBootstrap");
            boot.AddComponent<LevelBootstrap>().player = p.GetComponent<PlayerHealth>();

            // HUD
            BuildHUD();

            var path = $"{SCENES}/Level{n}.unity";
            EditorSceneManager.SaveScene(scene, path);
        }

        static void BuildHUD()
        {
            var canvasGO = new GameObject("HUD Canvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>().uiScaleMode =
                CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGO.AddComponent<GraphicRaycaster>();

            Text MakeText(string name, Vector2 anchor, Vector2 pivot, Vector2 pos, TextAnchor align)
            {
                var go = new GameObject(name);
                go.transform.SetParent(canvasGO.transform);
                var t = go.AddComponent<Text>();
                t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                t.fontSize = 22;
                t.color = Color.white;
                t.alignment = align;
                var rt = t.rectTransform;
                rt.anchorMin = rt.anchorMax = anchor;
                rt.pivot = pivot;
                rt.anchoredPosition = pos;
                rt.sizeDelta = new Vector2(300, 40);
                return t;
            }

            var level = MakeText("Level", new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -16), TextAnchor.UpperLeft);
            var time = MakeText("Time", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -16), TextAnchor.UpperCenter);
            var death = MakeText("Deaths", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-20, -16), TextAnchor.UpperRight);

            var hud = canvasGO.AddComponent<HUD>();
            hud.levelText = level; hud.timerText = time; hud.deathText = death;
        }

        static void BuildWinScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            var cam = camGO.AddComponent<Camera>();
            cam.orthographic = true;
            cam.backgroundColor = new Color(0.1f, 0.08f, 0.05f);
            camGO.transform.position = new Vector3(0, 0, -10);

            var canvasGO = new GameObject("Canvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
            var go = new GameObject("WinText");
            go.transform.SetParent(canvasGO.transform);
            var t = go.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = 48; t.color = Color.white; t.alignment = TextAnchor.MiddleCenter;
            t.text = "YOU SAVED HOT BUN GIRL!\n<size=24>Press R to play again</size>";
            t.rectTransform.anchorMin = Vector2.zero; t.rectTransform.anchorMax = Vector2.one;
            t.rectTransform.offsetMin = t.rectTransform.offsetMax = Vector2.zero;

            EditorSceneManager.SaveScene(scene, $"{SCENES}/Win.unity");
        }

        static void ConfigureBuildSettings()
        {
            var list = new List<EditorBuildSettingsScene>();
            for (int i = 1; i <= 5; i++)
                list.Add(new EditorBuildSettingsScene($"{SCENES}/Level{i}.unity", true));
            list.Add(new EditorBuildSettingsScene($"{SCENES}/Win.unity", true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        // ---------- helpers ----------

        static void EnsureLayer(int index, string name)
        {
            var tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            if (layers != null && index < layers.arraySize)
            {
                var sp = layers.GetArrayElementAtIndex(index);
                if (string.IsNullOrEmpty(sp.stringValue)) sp.stringValue = name;
                tagManager.ApplyModifiedProperties();
            }
        }

        static void EnsureTag(string tag)
        {
            var tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var tags = tagManager.FindProperty("tags");
            for (int i = 0; i < tags.arraySize; i++)
                if (tags.GetArrayElementAtIndex(i).stringValue == tag) return;
            tags.InsertArrayElementAtIndex(tags.arraySize);
            tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
            tagManager.ApplyModifiedProperties();
        }
    }
}
#endif
