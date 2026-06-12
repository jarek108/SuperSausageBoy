#if UNITY_EDITOR
using System.IO;
using UnityEngine;
using UnityEditor;

namespace SuperSausageBoy.EditorTools
{
    /// <summary>
    /// Generates ORIGINAL placeholder pixel-art textures procedurally so we can
    /// build & test the game before final art exists. Everything here is drawn by
    /// code (no copyrighted assets). Outputs PNGs into Assets/Art/Generated with
    /// pixel-perfect import settings (Point filter, no compression, PPU 16).
    /// </summary>
    public static class PlaceholderArtGenerator
    {
        const int PPU = 16;
        const string OutDir = "Assets/Art/Generated";

        [MenuItem("SuperSausageBoy/Generate Placeholder Art")]
        public static void Generate()
        {
            Directory.CreateDirectory(OutDir);

            // Super Sausage Boy: 14x18 browned sausage with outline, grill marks, eyes.
            SaveSprite("SausageBoy", BuildSausageBoy());
            // Hot Bun Girl: 16x16 golden bun with sesame + glow rim.
            SaveSprite("HotBunGirl", BuildHotBunGirl());
            // Ground tile: 16x16 dark slab with top edge.
            SaveSprite("Tile", BuildTile());
            // Wall-grippable tile (slightly different hue for readability).
            SaveSprite("WallTile", BuildWallTile());
            // Saw blade: 24x24 spinning circular blade.
            SaveSprite("Saw", BuildSaw());
            // Spike: 16x16 triangle spike.
            SaveSprite("Spike", BuildSpike());
            // Grease splat decal: 16x16 translucent brown blob.
            SaveSprite("GreaseSplat", BuildGreaseSplat());
            // 1x1 white pixel (for HUD backgrounds etc.)
            SaveSprite("White", BuildSolid(1, 1, Color.white));

            AssetDatabase.Refresh();
            Debug.Log("[SSB] Placeholder art generated in " + OutDir);
        }

        // ---- builders (return Color32[] row-major, bottom-left origin) ----

        static readonly Color32 Clear = new Color32(0, 0, 0, 0);

        static Texture2D BuildSausageBoy()
        {
            int w = 14, h = 18;
            var px = Fill(w, h, Clear);
            Color32 outline = C("2A1410");
            Color32 body = C("B5462E");
            Color32 hi = C("E0784B");
            Color32 grill = C("5A2A1A");
            Color32 white = C("FFFFFF");
            Color32 black = C("1A1A1A");

            // rounded capsule body
            for (int y = 1; y < h - 1; y++)
            for (int x = 2; x < w - 2; x++)
            {
                bool topCap = y > h - 4 && (x < 3 || x > w - 4);
                bool botCap = y < 3 && (x < 3 || x > w - 4);
                if (topCap || botCap) continue;
                Set(px, w, x, y, body);
            }
            // highlight column
            for (int y = 3; y < h - 3; y++) Set(px, w, 4, y, hi);
            // grill marks (diagonal seared stripes)
            for (int i = 0; i < 3; i++)
            {
                int gy = 5 + i * 4;
                for (int x = 3; x < w - 3; x++)
                    if (((x + gy) % 6) == 0) Set(px, w, x, gy, grill);
            }
            // eyes
            Set(px, w, 6, h - 5, white); Set(px, w, 6, h - 6, black);
            Set(px, w, 9, h - 5, white); Set(px, w, 9, h - 6, black);
            // tiny legs
            Set(px, w, 4, 0, outline); Set(px, w, 9, 0, outline);
            // outline pass
            Outline(px, w, h, outline);
            return ToTex(px, w, h);
        }

        static Texture2D BuildHotBunGirl()
        {
            int w = 16, h = 16;
            var px = Fill(w, h, Clear);
            Color32 outline = C("3A2A18");
            Color32 basec = C("E8B564");
            Color32 hi = C("FFE3A3");
            Color32 shadow = C("C68A3C");
            Color32 seed = C("FFF6DC");

            // dome bun
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = (x - 7.5f) / 7.5f;
                float dy = (y - 6f) / 8f;
                if (dx * dx + dy * dy <= 1f)
                    Set(px, w, x, y, y > 9 ? hi : (y < 4 ? shadow : basec));
            }
            // sesame seeds
            Set(px, w, 5, 10, seed); Set(px, w, 9, 11, seed);
            Set(px, w, 7, 8, seed); Set(px, w, 11, 9, seed);
            Outline(px, w, h, outline);
            return ToTex(px, w, h);
        }

        static Texture2D BuildTile()
        {
            int w = 16, h = 16;
            var px = Fill(w, h, C("3A3550"));
            Color32 top = C("5A557A");
            Color32 dark = C("2A2640");
            for (int x = 0; x < w; x++) { Set(px, w, x, h - 1, top); Set(px, w, x, h - 2, top); }
            for (int x = 0; x < w; x++) Set(px, w, x, 0, dark);
            // subtle speckle
            for (int i = 0; i < 10; i++)
                Set(px, w, (i * 7) % w, (i * 5 + 3) % (h - 3), dark);
            return ToTex(px, w, h);
        }

        static Texture2D BuildWallTile()
        {
            int w = 16, h = 16;
            var px = Fill(w, h, C("403A30"));
            Color32 edge = C("6A5C44");
            for (int y = 0; y < h; y++) { Set(px, w, 0, y, edge); Set(px, w, w - 1, y, edge); }
            for (int i = 0; i < 8; i++) Set(px, w, (i * 5) % w, (i * 6) % h, edge);
            return ToTex(px, w, h);
        }

        static Texture2D BuildSaw()
        {
            int s = 24;
            var px = Fill(s, s, Clear);
            Color32 metal = C("C0C8D0");
            Color32 dark = C("707880");
            Color32 hub = C("404850");
            float cx = (s - 1) / 2f, cy = (s - 1) / 2f, r = 11f;
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                float ang = Mathf.Atan2(y - cy, x - cx);
                float toothR = r + Mathf.Sin(ang * 12f) * 1.8f; // saw teeth
                if (d <= toothR) Set(px, s, x, y, d > r - 2 ? dark : metal);
                if (d <= 3f) Set(px, s, x, y, hub);
            }
            return ToTex(px, s, s);
        }

        static Texture2D BuildSpike()
        {
            int w = 16, h = 16;
            var px = Fill(w, h, Clear);
            Color32 metal = C("D0D4DC");
            Color32 dark = C("8088A0");
            // upward triangle
            for (int y = 0; y < h; y++)
            {
                int half = Mathf.RoundToInt((h - 1 - y) * 0.5f);
                int cxi = w / 2;
                for (int x = cxi - half; x <= cxi + half; x++)
                    if (x >= 0 && x < w) Set(px, w, x, y, x < cxi ? metal : dark);
            }
            return ToTex(px, w, h);
        }

        static Texture2D BuildGreaseSplat()
        {
            int w = 16, h = 16;
            var px = Fill(w, h, Clear);
            Color32 grease = new Color32(0x3A, 0x24, 0x18, 0xB0);
            int[] xs = { 5, 8, 11, 4, 9, 12, 6, 10, 7 };
            int[] ys = { 4, 3, 5, 7, 8, 6, 10, 9, 6 };
            for (int i = 0; i < xs.Length; i++)
            {
                Set(px, w, xs[i], ys[i], grease);
                Set(px, w, xs[i] + 1, ys[i], grease);
                Set(px, w, xs[i], ys[i] + 1, grease);
            }
            return ToTex(px, w, h);
        }

        static Texture2D BuildSolid(int w, int h, Color32 c) => ToTex(Fill(w, h, c), w, h);

        // ---- helpers ----

        static Color32[] Fill(int w, int h, Color32 c)
        {
            var a = new Color32[w * h];
            for (int i = 0; i < a.Length; i++) a[i] = c;
            return a;
        }

        static void Set(Color32[] px, int w, int x, int y, Color32 c)
        {
            if (x < 0 || y < 0 || x >= w || y >= px.Length / w) return;
            px[y * w + x] = c;
        }

        static Color32 Get(Color32[] px, int w, int x, int y) => px[y * w + x];

        static void Outline(Color32[] px, int w, int h, Color32 outline)
        {
            var copy = (Color32[])px.Clone();
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (copy[y * w + x].a != 0) continue;
                bool adj =
                    (x > 0 && copy[y * w + x - 1].a != 0) ||
                    (x < w - 1 && copy[y * w + x + 1].a != 0) ||
                    (y > 0 && copy[(y - 1) * w + x].a != 0) ||
                    (y < h - 1 && copy[(y + 1) * w + x].a != 0);
                if (adj) px[y * w + x] = outline;
            }
        }

        static Texture2D ToTex(Color32[] px, int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.SetPixels32(px);
            t.Apply();
            return t;
        }

        static Color32 C(string hex)
        {
            byte r = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
            byte g = byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
            byte b = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
            return new Color32(r, g, b, 255);
        }

        static void SaveSprite(string name, Texture2D tex)
        {
            string path = $"{OutDir}/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(path);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = PPU;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.spriteImportMode = SpriteImportMode.Single;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }
    }
}
#endif
