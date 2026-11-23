using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Builds a demo scene: a floating island with a ruined low-poly city (Kenney Fantasy Town Kit),
// a few small floating islands and one-eyed drones (Quaternius Sci-Fi Essentials Kit).
// Menu: Tools > A3 > Build Demo City
public static class BuildDemoCity
{
    const string TownDir = "Assets/A3/Art/Town/";
    const string EnemyDir = "Assets/A3/Art/Enemies/";
    const string MatDir = "Assets/A3/Art/Materials";
    const string ScenePath = "Assets/A3/Scenes/A3_DemoCity.unity";
    const float CityScale = 3f;
    const int IslandRadius = 13;

    static Material townMat, enemyMat;
    static System.Random rng;
    static int missing;

    [MenuItem("Tools/A3/Build Demo City")]
    public static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        AssetDatabase.Refresh();
        EnsureFolder("Assets/A3/Art", "Materials");
        EnsureFolder("Assets/A3", "Scenes");

        townMat = MakeMat("Town_Mat", TownDir + "Textures/colormap.png", null);
        enemyMat = MakeMat("Enemy_Mat", EnemyDir + "Textures/T_Enemies_BaseColor.png", EnemyDir + "Textures/T_Enemies_Emissive.png");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        rng = new System.Random(7);
        missing = 0;

        SetupLighting();

        var rootGo = new GameObject("DemoCity");
        rootGo.transform.localScale = Vector3.one * CityScale;
        Transform root = rootGo.transform;

        // main island
        var main = new GameObject("MainIsland").transform;
        main.SetParent(root, false);
        var occ = new HashSet<Vector2Int>();
        BuildGround(main, IslandRadius, true);

        // plaza
        Place("fountain-round", main, Vector3.zero);
        for (int x = -3; x <= 3; x++) for (int z = -3; z <= 3; z++) if (x * x + z * z < 12) occ.Add(new Vector2Int(x, z));
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI * 2f / 8f;
            Place("pillar-stone", main, new Vector3(Mathf.Cos(a) * 3.2f, 0, Mathf.Sin(a) * 3.2f), 0, 1.6f);
        }
        Place("lantern", main, new Vector3(5, 0, 0));
        Place("lantern", main, new Vector3(-5, 0, 0));
        Place("lantern", main, new Vector3(0, 0, 5));
        Place("lantern", main, new Vector3(0, 0, -5));

        // ruined houses (x0, z0, width, depth)
        int[][] houses = {
            new[] { -9, -5, 4, 3 }, new[] { -9, 2, 3, 4 }, new[] { -3, -9, 4, 3 }, new[] { 4, -9, 3, 4 },
            new[] { 8, -3, 3, 4 }, new[] { 8, 4, 4, 3 }, new[] { 1, 9, 4, 3 }, new[] { -6, 8, 3, 3 }
        };
        foreach (var h in houses) BuildHouse(main, h[0], h[1], h[2], h[3], occ);

        // trees near the edge
        string[] trees = { "tree", "tree-crooked", "tree-high", "tree-high-crooked", "tree-high-round" };
        int placed = 0;
        for (int tries = 0; tries < 200 && placed < 12; tries++)
        {
            float a = (float)(rng.NextDouble() * Mathf.PI * 2);
            float r = 9.5f + (float)rng.NextDouble() * 3f;
            int x = Mathf.RoundToInt(Mathf.Cos(a) * r), z = Mathf.RoundToInt(Mathf.Sin(a) * r);
            if (x * x + z * z > (IslandRadius - 1) * (IslandRadius - 1)) continue;
            var c = new Vector2Int(x, z);
            if (occ.Contains(c)) continue;
            occ.Add(c);
            Place(trees[rng.Next(trees.Length)], main, new Vector3(x, 0, z), rng.Next(360));
            placed++;
        }

        // small floating islands
        var s1 = MakeIsland(root, new Vector3(-24, 3, -14), 4);
        Place("wall-arch", s1, Vector3.zero);
        Place("tree-high", s1, new Vector3(2, 0, 1), 20);
        var s2 = MakeIsland(root, new Vector3(22, -3, 9), 5);
        Place("pillar-stone", s2, new Vector3(1, 0, 1), 0, 1.6f);
        Place("pillar-stone", s2, new Vector3(-1, 0, 1), 0, 1.6f);
        Place("wall-broken", s2, new Vector3(0, 0, -2), 0);
        var s3 = MakeIsland(root, new Vector3(6, 5, -26), 3);
        Place("fountain-round", s3, Vector3.zero);

        // drones (world space, outside the scaled city root)
        Vector3[] dronePos = { new Vector3(0, 6, 0), new Vector3(14, 7, -8), new Vector3(-14, 8, 6) };
        foreach (var p in dronePos)
        {
            var d = Place("Enemy_EyeDrone", null, p, 0, 1f, EnemyDir);
            if (d != null) d.transform.LookAt(new Vector3(0, 3, -20));
        }

        // camera
        var cam = Camera.main;
        if (cam != null)
        {
            cam.transform.position = new Vector3(0, 16, -75);
            cam.transform.LookAt(new Vector3(0, 2, 0));
            cam.farClipPlane = 500;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log("BuildDemoCity: done. Scene saved to " + ScenePath + ". Missing models: " + missing);
    }

    static void SetupLighting()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.55f, 0.65f, 0.80f);
        RenderSettings.ambientEquatorColor = new Color(0.45f, 0.45f, 0.48f);
        RenderSettings.ambientGroundColor = new Color(0.25f, 0.22f, 0.20f);
        var sun = Object.FindFirstObjectByType<Light>();
        if (sun != null)
        {
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.93f, 0.82f);
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.None;
            sun.transform.rotation = Quaternion.Euler(50, -30, 0);
        }
    }

    // ---------- building blocks ----------
    static Transform MakeIsland(Transform parent, Vector3 center, int radius)
    {
        var isl = new GameObject("Island").transform;
        isl.SetParent(parent, false);
        isl.localPosition = center;
        BuildGround(isl, radius, true);
        return isl;
    }

    static void BuildGround(Transform isl, int radius, bool rocks)
    {
        for (int x = -radius; x <= radius; x++)
            for (int z = -radius; z <= radius; z++)
                if (x * x + z * z <= radius * radius + 0.5f)
                    Place("road", isl, new Vector3(x, 0, z));
        if (!rocks) return;

        int layers = Mathf.Max(3, radius / 2);
        for (int i = 0; i < layers; i++)
        {
            float t = (float)i / layers;
            float sc = Mathf.Lerp(2.4f, 1.3f, t);
            float y = -0.3f - i * 1.3f;
            for (int ringIdx = 0; ringIdx < 2; ringIdx++)
            {
                float ring = radius * (1f - t) * 0.85f * (ringIdx == 0 ? 1f : 0.5f);
                int n = ring < 0.6f ? 1 : Mathf.CeilToInt(2f * Mathf.PI * ring / (1.4f * sc));
                if (ringIdx == 1 && ring < 0.6f) continue;
                for (int k = 0; k < n; k++)
                {
                    float a = k * Mathf.PI * 2f / n + (float)rng.NextDouble() * 0.3f;
                    Vector3 pos = ring < 0.6f ? new Vector3(0, y, 0) : new Vector3(Mathf.Cos(a) * ring, y, Mathf.Sin(a) * ring);
                    PlaceRot(rng.Next(2) == 0 ? "rock-large" : "rock-wide", isl, pos,
                        Quaternion.Euler(180, rng.Next(360), 0), Vector3.one * sc, TownDir);
                }
            }
        }
    }

    static readonly Vector2Int[] Dirs = {
        new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1)
    };

    static void BuildHouse(Transform parent, int x0, int z0, int w, int d, HashSet<Vector2Int> occ)
    {
        var tiles = new HashSet<Vector2Int>();
        for (int x = x0; x < x0 + w; x++)
            for (int z = z0; z < z0 + d; z++)
            {
                tiles.Add(new Vector2Int(x, z));
                Place("planks", parent, new Vector3(x, 0.03f, z));
            }
        for (int x = x0 - 1; x <= x0 + w; x++)
            for (int z = z0 - 1; z <= z0 + d; z++)
                occ.Add(new Vector2Int(x, z));

        // upper floor: only some tiles
        var upper = new HashSet<Vector2Int>();
        foreach (var t in tiles) if (rng.NextDouble() < 0.7) upper.Add(t);
        foreach (var t in upper) Place("planks", parent, new Vector3(t.x, 1.0f, t.y));

        // choose the door edge: the one facing the island centre
        Vector2 center = new Vector2(x0 + (w - 1) / 2f, z0 + (d - 1) / 2f);
        Vector2 toCenter = (-center).normalized;
        Vector2Int doorTile = default, doorDir = default;
        float best = -999f;
        foreach (var t in tiles)
            foreach (var dir in Dirs)
            {
                if (tiles.Contains(t + dir)) continue;
                float s = Vector2.Dot(new Vector2(dir.x, dir.y), toCenter);
                if (s > best) { best = s; doorTile = t; doorDir = dir; }
            }

        foreach (var t in tiles)
            foreach (var dir in Dirs)
            {
                if (tiles.Contains(t + dir)) continue;
                float yaw = Mathf.Atan2(-dir.y, dir.x) * Mathf.Rad2Deg;
                string piece = (t == doorTile && dir == doorDir) ? "wall-doorway-square" : PickWall((float)rng.NextDouble(), false);
                if (piece != null) Place(piece, parent, new Vector3(t.x, 0, t.y), yaw);
            }
        foreach (var t in upper)
            foreach (var dir in Dirs)
            {
                if (upper.Contains(t + dir)) continue;
                float yaw = Mathf.Atan2(-dir.y, dir.x) * Mathf.Rad2Deg;
                string piece = PickWall((float)rng.NextDouble(), true);
                if (piece != null) Place(piece, parent, new Vector3(t.x, 1, t.y), yaw);
            }
    }

    static string PickWall(float r, bool upper)
    {
        if (!upper)
        {
            if (r < 0.50f) return "wall";
            if (r < 0.64f) return "wall-window-small";
            if (r < 0.78f) return "wall-broken";
            if (r < 0.90f) return "wall-half";
            return null;
        }
        if (r < 0.40f) return "wall";
        if (r < 0.60f) return "wall-broken";
        if (r < 0.75f) return "wall-half";
        if (r < 0.80f) return "wall-window-small";
        return null;
    }

    // ---------- helpers ----------
    static GameObject Place(string file, Transform parent, Vector3 pos, float yaw = 0f, float scale = 1f, string dir = null)
    {
        return PlaceRot(file, parent, pos, Quaternion.Euler(0, yaw, 0), Vector3.one * scale, dir ?? TownDir);
    }

    static GameObject PlaceRot(string file, Transform parent, Vector3 pos, Quaternion rot, Vector3 scale, string dir)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(dir + file + ".fbx");
        if (prefab == null) { missing++; Debug.LogWarning("BuildDemoCity: missing " + dir + file + ".fbx"); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot;
        go.transform.localScale = scale;
        var mat = dir == EnemyDir ? enemyMat : townMat;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            var ms = r.sharedMaterials;
            for (int i = 0; i < ms.Length; i++) ms[i] = mat;
            r.sharedMaterials = ms;
        }
        return go;
    }

    static Material MakeMat(string name, string baseTexPath, string emissiveTexPath)
    {
        string path = MatDir + "/" + name + ".mat";
        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool isNew = mat == null;
        if (isNew) mat = new Material(sh);
        mat.shader = sh;
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(baseTexPath);
        if (tex != null)
        {
            mat.SetTexture("_BaseMap", tex);
            mat.SetTexture("_MainTex", tex);
        }
        mat.SetColor("_BaseColor", Color.white);
        mat.SetColor("_Color", Color.white);
        mat.SetFloat("_Smoothness", 0.15f);
        mat.SetFloat("_Glossiness", 0.15f);
        mat.SetFloat("_Metallic", 0f);
        if (!string.IsNullOrEmpty(emissiveTexPath))
        {
            var em = AssetDatabase.LoadAssetAtPath<Texture2D>(emissiveTexPath);
            if (em != null)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetTexture("_EmissionMap", em);
                mat.SetColor("_EmissionColor", Color.white * 2f);
            }
        }
        if (isNew) AssetDatabase.CreateAsset(mat, path); else EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        return mat;
    }

    static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
    }
}
