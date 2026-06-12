#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace SuperSausageBoy.EditorTools
{
    /// <summary>
    /// Defines the geometry of the 5 levels. Each level is built from tile
    /// instances laid out on a grid, plus hazards and a goal. Difficulty ramps:
    ///   L1: gentle intro (run + simple jumps)
    ///   L2: introduces gaps + a saw
    ///   L3: wall jumping required
    ///   L4: saw gauntlet + crumbling blocks
    ///   L5: everything - precision wall jumps over saws + spikes
    /// Tiles are 1 world unit (16px at PPU 16).
    /// </summary>
    public static class LevelLayout
    {
        public static Vector3 Spawn(int level)
        {
            return new Vector3(-1f, 1.5f, 0f);
        }

        public static void Build(int level, GameObject tile, GameObject wall,
            GameObject goalPrefab, Dictionary<string, GameObject> hz, GameObject player)
        {
            var parent = new GameObject("Level").transform;

            switch (level)
            {
                case 1: BuildL1(parent, tile, goalPrefab, hz); break;
                case 2: BuildL2(parent, tile, goalPrefab, hz); break;
                case 3: BuildL3(parent, tile, wall, goalPrefab, hz); break;
                case 4: BuildL4(parent, tile, wall, goalPrefab, hz); break;
                case 5: BuildL5(parent, tile, wall, goalPrefab, hz); break;
            }
        }

        // ---- placement helpers ----

        static GameObject Place(GameObject prefab, Transform parent, float x, float y)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetParent(parent);
            go.transform.position = new Vector3(x, y, 0f);
            return go;
        }

        // a horizontal run of ground tiles, y is the tile's center row
        static void Row(GameObject tile, Transform parent, float xStart, float xEnd, float y)
        {
            for (float x = xStart; x <= xEnd; x += 1f)
                Place(tile, parent, x, y);
        }

        // a vertical column (wall)
        static void Col(GameObject tile, Transform parent, float x, float yStart, float yEnd)
        {
            for (float y = yStart; y <= yEnd; y += 1f)
                Place(tile, parent, x, y);
        }

        static void Goal(GameObject goalPrefab, Transform parent, float x, float y)
            => Place(goalPrefab, parent, x, y);

        // ====================================================================
        // LEVEL 1 — intro: flat run with two small step-ups, goal at the end.
        // ====================================================================
        static void BuildL1(Transform p, GameObject tile, GameObject goal, Dictionary<string, GameObject> hz)
        {
            Row(tile, p, -3, 6, 0);
            Row(tile, p, 7, 12, 1);   // step up
            Row(tile, p, 13, 20, 2);  // step up
            // ceiling-free; goal at the end
            Goal(goal, p, 19, 3f);
        }

        // ====================================================================
        // LEVEL 2 — gaps to jump + one floor saw to avoid.
        // ====================================================================
        static void BuildL2(Transform p, GameObject tile, GameObject goal, Dictionary<string, GameObject> hz)
        {
            Row(tile, p, -3, 3, 0);
            // gap
            Row(tile, p, 6, 10, 0);
            // saw sitting on this platform
            Place(hz["Saw"], p, 8, 1.1f);
            // gap
            Row(tile, p, 13, 16, 1);
            // gap (bigger)
            Row(tile, p, 20, 26, 1);
            Goal(goal, p, 25, 2.2f);
        }

        // ====================================================================
        // LEVEL 3 — wall jumping required to scale a shaft.
        // ====================================================================
        static void BuildL3(Transform p, GameObject tile, GameObject wall, GameObject goal, Dictionary<string, GameObject> hz)
        {
            Row(tile, p, -3, 4, 0);
            // a vertical shaft: two walls the player must wall-jump between
            Col(wall, p, 5, 0, 9);    // left wall of shaft
            Col(wall, p, 9, 1, 10);   // right wall (offset so it's a zig-zag climb)
            // a couple of spikes at the bottom of the shaft to punish falling
            Place(hz["Spike"], p, 6, 0.6f);
            Place(hz["Spike"], p, 7, 0.6f);
            Place(hz["Spike"], p, 8, 0.6f);
            // top exit ledge
            Row(tile, p, 9, 16, 10);
            Goal(goal, p, 15, 11.2f);
        }

        // ====================================================================
        // LEVEL 4 — saw gauntlet over gaps + crumbling blocks.
        // ====================================================================
        static void BuildL4(Transform p, GameObject tile, GameObject wall, GameObject goal, Dictionary<string, GameObject> hz)
        {
            Row(tile, p, -3, 2, 0);
            // patrolling saw on the start platform
            var saw1 = Place(hz["Saw"], p, 0, 1.2f);
            var sb1 = saw1.GetComponent<Hazards.SawBlade>();
            if (sb1 != null) { sb1.patrol = true; sb1.patrolOffset = new Vector2(3, 0); }

            // crumbling stepping stones over a spike pit
            Place(hz["Spike"], p, 4, -1.4f);
            Place(hz["Spike"], p, 5, -1.4f);
            Place(hz["Spike"], p, 6, -1.4f);
            Place(hz["Spike"], p, 7, -1.4f);
            Place(hz["Crumble"], p, 4, 0);
            Place(hz["Crumble"], p, 6, 0);
            Place(hz["Crumble"], p, 8, 0);

            Row(tile, p, 10, 14, 0);
            // ceiling saws
            Place(hz["Saw"], p, 11, 3f);
            Place(hz["Saw"], p, 13, 3f);

            Row(tile, p, 17, 24, 1);
            Goal(goal, p, 23, 2.2f);
        }

        // ====================================================================
        // LEVEL 5 — the gauntlet: precision wall jumps past saws over spikes.
        // ====================================================================
        static void BuildL5(Transform p, GameObject tile, GameObject wall, GameObject goal, Dictionary<string, GameObject> hz)
        {
            Row(tile, p, -3, 2, 0);

            // spike floor for the whole middle section
            for (float x = 3; x <= 22; x++) Place(hz["Spike"], p, x, -2.4f);

            // wall-jump shaft 1
            Col(wall, p, 4, -2, 6);
            Col(wall, p, 8, -1, 7);
            Place(hz["Saw"], p, 6, 2f);   // saw in the middle of the shaft

            // mid ledge (crumbling)
            Place(hz["Crumble"], p, 9, 3);
            Place(hz["Crumble"], p, 10, 3);

            // wall-jump shaft 2 (higher)
            Col(wall, p, 12, 1, 10);
            Col(wall, p, 16, 2, 11);
            var pSaw = Place(hz["Saw"], p, 14, 6f);
            var psb = pSaw.GetComponent<Hazards.SawBlade>();
            if (psb != null) { psb.patrol = true; psb.patrolOffset = new Vector2(0, 3); psb.patrolSpeed = 2f; }

            // final approach
            Row(tile, p, 16, 24, 11);
            Place(hz["Saw"], p, 19, 12.1f);
            Goal(goal, p, 23, 12.2f);
        }
    }
}
#endif
