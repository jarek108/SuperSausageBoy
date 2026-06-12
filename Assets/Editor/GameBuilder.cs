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

            // Generate retro SFX before wiring the AudioManager that references them.
            SfxGenerator.Generate();

            var hazardPrefabs = BuildHazardPrefabs();
            var greasePrefab = BuildGreasePrefab();
            var playerPrefab = BuildPlayerPrefab(greasePrefab);
            var goalPrefab = BuildGoalPrefab();
            var tilePrefab = BuildTilePrefab("Tile", false);
            var wallPrefab = BuildTilePrefab("WallTile", false);
            var audioPrefab = BuildAudioManagerPrefab();

            // Build levels
            for (int i = 0; i < 5; i++)
                BuildLevel(i + 1, playerPrefab, goalPrefab, tilePrefab, wallPrefab, hazardPrefabs, audioPrefab);

            BuildStoryScene();
            BuildWinScene();
            ConfigureBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[SSB] BUILD COMPLETE: 5 levels + win scene generated.");
        }

        // ---------- Sprites ----------
        static Sprite Spr(string name) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"{GEN}/{name}.png");

        // ---------- Audio ----------
        static AudioClip Clip(string path) =>
            AssetDatabase.LoadAssetAtPath<AudioClip>(path);

        // Builds the persistent AudioManager prefab, wiring generated SFX +
        // music clips. Returns null gracefully if clips aren't present yet.
        static GameObject BuildAudioManagerPrefab()
        {
            var go = new GameObject("AudioManager");
            var am = go.AddComponent<SuperSausageBoy.Core.AudioManager>();

            am.jumpClip      = Clip("Assets/Audio/SFX/jump.wav");
            am.landClip      = Clip("Assets/Audio/SFX/land.wav");
            am.deathClip     = Clip("Assets/Audio/SFX/death.wav");
            am.wallSlideClip = Clip("Assets/Audio/SFX/wallslide.wav");
            am.goalClip      = Clip("Assets/Audio/SFX/goal.wav");

            am.levelMusic = Clip("Assets/Audio/Music/level_theme.mp3");
            am.introMusic = Clip("Assets/Audio/Music/intro_theme.mp3");
            am.winMusic   = Clip("Assets/Audio/Music/win_theme.mp3");

            var path = $"{PREFABS}/AudioManager.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        // ---------- Prefabs ----------

        // Builds a self-destructing one-shot ParticleSystem prefab for "juice"
        // bursts (death splat chunks, landing dust). Plays on awake, then the
        // attached ParticleAutoDestroy cleans it up when finished.
        static GameObject BuildParticlePrefab(string name, Color color, int count,
            float speed, float lifetime, float gravity, float size, bool burst)
        {
            var go = new GameObject(name);
            var ps = go.AddComponent<ParticleSystem>();

            // Configure via modules (ParticleSystem must exist before editing).
            var main = ps.main;
            main.duration = 1f;
            main.loop = false;
            main.startLifetime = lifetime;
            main.startSpeed = speed;
            main.startSize = size;
            main.startColor = color;
            main.gravityModifier = gravity;
            main.playOnAwake = true;
            main.maxParticles = Mathf.Max(count, 32);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            if (burst)
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.15f;

            // Fade out over lifetime.
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;

            // Renderer: use a simple sprite material so it shows in 2D/URP.
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = 20;

            go.AddComponent<SuperSausageBoy.Core.ParticleAutoDestroy>();

            var path = $"{PREFABS}/{name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

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

            // Visual lives on a child so we can squash/stretch the sprite without
            // deforming the collider on the root.
            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform);
            visual.transform.localPosition = Vector3.zero;
            var sr = visual.AddComponent<SpriteRenderer>();
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
            health.deathBurstPrefab = BuildParticlePrefab(
                "DeathBurst", new Color(0.71f, 0.27f, 0.18f), count: 24,
                speed: 7f, lifetime: 0.6f, gravity: 1.2f, size: 0.12f, burst: true);

            // Juice: squash/stretch on the visual + feedback glue (audio/shake).
            var squash = visual.AddComponent<SquashStretch>();
            squash.movement = move;
            var feedback = go.AddComponent<PlayerFeedback>();
            feedback.landingDustPrefab = BuildParticlePrefab(
                "LandingDust", new Color(0.78f, 0.72f, 0.58f), count: 10,
                speed: 2.5f, lifetime: 0.35f, gravity: 0.1f, size: 0.1f, burst: true);

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
            GameObject tile, GameObject wall, Dictionary<string, GameObject> hz,
            GameObject audioPrefab)
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

            // Juice: screen shake on the camera (additive, runs after follow).
            camGO.AddComponent<SuperSausageBoy.Core.ScreenShake>();

            // Persistent AudioManager (singleton guards against duplicates).
            if (audioPrefab != null)
                PrefabUtility.InstantiatePrefab(audioPrefab);

            // Music starter on its OWN scene object so it isn't destroyed along
            // with a duplicate AudioManager on reload.
            var musicStarter = new GameObject("MusicStarter");
            musicStarter.AddComponent<SuperSausageBoy.Core.LevelAudioStarter>().track =
                SuperSausageBoy.Core.LevelAudioStarter.Track.Level;

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

        static void BuildStoryScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera (UI only; solid black backdrop behind letterboxed art).
            var camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            var cam = camGO.AddComponent<Camera>();
            cam.orthographic = true;
            cam.backgroundColor = Color.black;
            cam.clearFlags = CameraClearFlags.SolidColor;
            camGO.transform.position = new Vector3(0, 0, -10);

            // Canvas
            var canvasGO = new GameObject("Story Canvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            canvasGO.AddComponent<GraphicRaycaster>();

            // Full-screen story image.
            var imgGO = new GameObject("StoryImage");
            imgGO.transform.SetParent(canvasGO.transform);
            var img = imgGO.AddComponent<Image>();
            img.preserveAspect = true;
            var irt = img.rectTransform;
            irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
            irt.offsetMin = irt.offsetMax = Vector2.zero;

            // Caption (bottom).
            var capGO = new GameObject("Caption");
            capGO.transform.SetParent(canvasGO.transform);
            var cap = capGO.AddComponent<Text>();
            cap.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            cap.fontSize = 30;
            cap.color = Color.white;
            cap.alignment = TextAnchor.LowerCenter;
            cap.horizontalOverflow = HorizontalWrapMode.Wrap;
            cap.verticalOverflow = VerticalWrapMode.Overflow;
            var crt = cap.rectTransform;
            crt.anchorMin = new Vector2(0.1f, 0f);
            crt.anchorMax = new Vector2(0.9f, 0.22f);
            crt.offsetMin = crt.offsetMax = Vector2.zero;
            // simple readability backdrop (semi-transparent strip)
            var capBg = new GameObject("CaptionBG");
            capBg.transform.SetParent(canvasGO.transform);
            capBg.transform.SetSiblingIndex(capGO.transform.GetSiblingIndex());
            var capBgImg = capBg.AddComponent<Image>();
            capBgImg.color = new Color(0f, 0f, 0f, 0.45f);
            var cbrt = capBgImg.rectTransform;
            cbrt.anchorMin = new Vector2(0f, 0f);
            cbrt.anchorMax = new Vector2(1f, 0.24f);
            cbrt.offsetMin = cbrt.offsetMax = Vector2.zero;

            // Black fader overlay (on top of everything).
            var fadeGO = new GameObject("Fader");
            fadeGO.transform.SetParent(canvasGO.transform);
            var fadeImg = fadeGO.AddComponent<Image>();
            fadeImg.color = Color.black;
            var frt = fadeImg.rectTransform;
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
            frt.offsetMin = frt.offsetMax = Vector2.zero;
            var fadeGroup = fadeGO.AddComponent<CanvasGroup>();
            fadeGroup.blocksRaycasts = false;

            // Audio sources.
            var audioGO = new GameObject("StoryAudio");
            var voiceSrc = audioGO.AddComponent<AudioSource>();
            voiceSrc.playOnAwake = false;
            var musicSrc = audioGO.AddComponent<AudioSource>();
            musicSrc.playOnAwake = false;

            // Controller + panels.
            var ctrlGO = new GameObject("StoryController");
            var ctrl = ctrlGO.AddComponent<SuperSausageBoy.Story.StoryController>();
            ctrl.displayImage = img;
            ctrl.captionText = cap;
            ctrl.voiceSource = voiceSrc;
            ctrl.musicSource = musicSrc;
            ctrl.fader = fadeGroup;
            ctrl.introMusic = Clip("Assets/Audio/Music/intro_theme.mp3");
            ctrl.nextScene = "Level1";

            var captions = StoryCaptions();
            var panels = new SuperSausageBoy.Story.StoryController.Panel[6];
            for (int i = 0; i < 6; i++)
            {
                panels[i] = new SuperSausageBoy.Story.StoryController.Panel
                {
                    image = StorySprite($"panel{i + 1}"),
                    caption = i < captions.Length ? captions[i] : "",
                    narration = Clip($"Assets/Audio/Voice/narration_{i + 1}.wav"),
                };
            }
            ctrl.panels = panels;

            EditorSceneManager.SaveScene(scene, $"{SCENES}/Story.unity");
        }

        // Loads a story panel PNG as a Sprite (importer set up below if needed).
        static Sprite StorySprite(string name)
        {
            string path = $"Assets/Art/Story/{name}.png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // Narration captions (kept in sync with story_script.md).
        static string[] StoryCaptions() => new[]
        {
            "In a warm little kitchen at the edge of the world lived Super Sausage Boy.",
            "And beside him, glowing golden, was the one he loved most \u2014 Hot Bun Girl.",
            "But deep in the dark, something charred and cruel was watching. The Griller.",
            "In a flash of smoke and sparks, Hot Bun Girl was gone \u2014 carried off beyond the saws.",
            "Super Sausage Boy did not hesitate. He ran. For her, he would cross any danger.",
            "Run fast. Jump true. Leap from the walls. The gauntlet awaits.",
        };

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
            // Story intro plays first.
            list.Add(new EditorBuildSettingsScene($"{SCENES}/Story.unity", true));
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
