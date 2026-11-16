#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Меню Unity: A2 → Build Superhot Scene
// 1) Сохраняет копию открытой сцены (с XR Origin) как Assets/A2/A2_Superhot.unity
// 2) Выключает всё старое (дрель, дверь), оставляет только XR Origin, свет и XR-менеджеры
// 3) Строит комнату, стол, 3 чёрных предмета для броска и красную фигуру
public static class A2SceneBuilder
{
    const string Dir = "Assets/A2";
    const string ScenePath = Dir + "/A2_Superhot.unity";

    [MenuItem("A2/Build Superhot Scene")]
    public static void Build()
    {
        Directory.CreateDirectory(Dir + "/Materials");
        AssetDatabase.Refresh();

        // --- 1. Копия текущей сцены ---
        Scene current = SceneManager.GetActiveScene();
        Scene scene = current;
        if (current.path != ScenePath)   // первый раз: копируем сцену дрели; повторно: работаем в A2-сцене
        {
            EditorSceneManager.SaveScene(current, ScenePath, true);
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        // --- 2. Выключаем всё лишнее ---
        Transform origin = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            string n = root.name.ToLower();
            if (root.name == "A2_Environment") { Object.DestroyImmediate(root); continue; } // пересборка: старое удаляем
            bool keep = n.Contains("xr") || n.Contains("light") || n.Contains("eventsystem") || n.Contains("interaction");
            root.SetActive(keep);
            // XR Origin может быть вложен (например внутри "XR Interaction Setup" в VR-шаблоне)
            if (keep && origin == null)
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    string tn = t.name.ToLower();
                    if (tn.Contains("xr origin") || tn.Contains("xr rig")) { origin = t; break; }
                }
        }
        if (origin == null) { Debug.LogError("A2: не нашёл XR Origin в сцене. Открой сцену CS1_Drill и запусти снова."); return; }

        Vector3 o = new Vector3(origin.position.x, 0f, origin.position.z);
        Quaternion rot = Quaternion.Euler(0f, origin.eulerAngles.y, 0f);
        Vector3 P(float x, float y, float z) => o + rot * new Vector3(x, y, z);

        // --- Материалы в стиле Superhot: белый мир, чёрные предметы, красные враги ---
        Material white = Mat("SH_White", new Color(0.92f, 0.92f, 0.92f));
        Material black = Mat("SH_Black", new Color(0.05f, 0.05f, 0.05f));
        Material red   = Mat("SH_Red",   new Color(0.9f, 0.08f, 0.05f));

        GameObject env = new GameObject("A2_Environment");

        // --- 3. Комната 6x8 м ---
        Box("Floor",     P(0, -0.05f, 2), new Vector3(6, 0.1f, 8), white, env);
        Box("Wall_Back", P(0, 1.5f, 6),   new Vector3(6, 3, 0.1f), white, env);
        Box("Wall_Left", P(-3, 1.5f, 2),  new Vector3(0.1f, 3, 8), white, env);
        Box("Wall_Right",P(3, 1.5f, 2),   new Vector3(0.1f, 3, 8), white, env);

        // Стол на расстоянии вытянутой руки (comfort/reach): 0.55 м впереди, высота 0.8 м
        Box("Table", P(0, 0.4f, 0.55f), new Vector3(1.0f, 0.8f, 0.45f), white, env);

        // --- Предметы для броска ---
        Throwable(PrimitiveType.Cylinder, "Bottle", P(-0.35f, 0.92f, 0.55f), new Vector3(0.07f, 0.11f, 0.07f), black, env);
        Throwable(PrimitiveType.Cube,     "Brick",  P(-0.15f, 0.86f, 0.55f), new Vector3(0.10f, 0.10f, 0.10f), black, env);
        Throwable(PrimitiveType.Sphere,   "Ball",   P(0.05f, 0.87f, 0.55f), new Vector3(0.11f, 0.11f, 0.11f), black, env);

        // --- Ракетка (как пинг-понг в First Steps): лежит справа на столе ---
        GameObject racket = new GameObject("Racket");
        racket.transform.SetParent(env.transform);
        racket.transform.position = P(0.33f, 0.815f, 0.36f);
        racket.transform.rotation = rot * Quaternion.Euler(90, 0, 0);   // лежит плашмя, ручкой к игроку
        Part(PrimitiveType.Cube,     "Head",   racket, new Vector3(0, 0.18f, 0), new Vector3(0.16f, 0.17f, 0.015f), black);
        Part(PrimitiveType.Cube,     "Handle", racket, new Vector3(0, 0.04f, 0), new Vector3(0.03f, 0.11f, 0.02f), black);
        Rigidbody rrb = racket.AddComponent<Rigidbody>();
        rrb.mass = 0.3f;
        rrb.interpolation = RigidbodyInterpolation.Interpolate;
        rrb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        XRGrabInteractable rg = racket.AddComponent<XRGrabInteractable>();
        rg.movementType = XRBaseInteractable.MovementType.VelocityTracking; // в руке остаётся физическим → отбивает мяч и бьёт фигуру
        rg.useDynamicAttach = false;
        GameObject attach = new GameObject("Attach");                       // хват за ручку
        attach.transform.SetParent(racket.transform, false);
        attach.transform.localPosition = new Vector3(0, 0.03f, 0);
        attach.transform.localRotation = Quaternion.Euler(-90, 0, 0);
        rg.attachTransform = attach.transform;
        racket.AddComponent<Throwable>();

        // --- Кулаки: невидимые сферы на обоих контроллерах ---
        foreach (Transform t in origin.GetComponentsInChildren<Transform>(true))
        {
            string tn = t.name.ToLower();
            if (!tn.Contains("controller") || !(tn.Contains("left") || tn.Contains("right"))) continue;
            if (tn.Contains("stabil") || tn.Contains("teleport") || tn.Contains("origin")) continue;
            if (t.GetComponentInParent<HandHitter>() != null || t.Find("Fist") != null) continue;
            if (t.parent != null && t.parent.name.ToLower().Contains("controller")) continue; // только верхний объект контроллера
            GameObject fist = new GameObject("Fist");
            fist.transform.SetParent(t, false);
            fist.transform.localPosition = new Vector3(0, -0.02f, 0.03f);
            fist.AddComponent<SphereCollider>().radius = 0.06f;
            fist.AddComponent<Rigidbody>().isKinematic = true;
            fist.AddComponent<HandHitter>();
            Debug.Log("A2: кулак добавлен на " + t.name);
        }

        // --- Красная фигура в 4.5 м ---
        GameObject fig = new GameObject("RedFigure");
        fig.transform.SetParent(env.transform);
        fig.transform.position = P(0, 0, 4.5f);
        fig.transform.rotation = rot * Quaternion.Euler(0, 180, 0);
        // Фигура в стиле Superhot из примитивов: туловище, голова, руки, ноги
        Part(PrimitiveType.Capsule, "Torso", fig, new Vector3(0, 1.15f, 0),  new Vector3(0.42f, 0.33f, 0.25f), red);
        Part(PrimitiveType.Sphere,  "Head",  fig, new Vector3(0, 1.68f, 0),  new Vector3(0.24f, 0.28f, 0.24f), red);
        Part(PrimitiveType.Capsule, "Arm_L", fig, new Vector3(-0.3f, 1.12f, 0.05f), new Vector3(0.11f, 0.3f, 0.11f), red);
        Part(PrimitiveType.Capsule, "Arm_R", fig, new Vector3( 0.3f, 1.12f, 0.05f), new Vector3(0.11f, 0.3f, 0.11f), red);
        Part(PrimitiveType.Capsule, "Leg_L", fig, new Vector3(-0.11f, 0.42f, 0), new Vector3(0.15f, 0.42f, 0.15f), red);
        Part(PrimitiveType.Capsule, "Leg_R", fig, new Vector3( 0.11f, 0.42f, 0), new Vector3(0.15f, 0.42f, 0.15f), red);
        Rigidbody frb = fig.AddComponent<Rigidbody>();
        frb.isKinematic = true;
        fig.AddComponent<AudioSource>().playOnAwake = false;
        RedFigure rf = fig.AddComponent<RedFigure>();
        rf.shatterClip = AssetDatabase.LoadAssetAtPath<AudioClip>(Dir + "/Audio/glass_shatter.wav");

        // --- Сохраняем и ставим сцену первой в билд ---
        EditorSceneManager.SaveScene(scene);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        Debug.Log("A2: сцена готова → " + ScenePath + ". Жми Play.");
    }

    static Material Mat(string name, Color c)
    {
        string path = $"{Dir}/Materials/{name}.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");
        m = new Material(sh) { color = c };
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static GameObject Box(string name, Vector3 pos, Vector3 scale, Material m, GameObject parent)
    {
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name;
        g.transform.SetParent(parent.transform);
        g.transform.position = pos;
        g.transform.localScale = scale;
        g.GetComponent<Renderer>().sharedMaterial = m;
        return g;
    }

    static void Part(PrimitiveType t, string name, GameObject parent, Vector3 localPos, Vector3 scale, Material m)
    {
        GameObject g = GameObject.CreatePrimitive(t);
        g.name = name;
        g.transform.SetParent(parent.transform, false);
        g.transform.localPosition = localPos;
        g.transform.localScale = scale;
        g.GetComponent<Renderer>().sharedMaterial = m;
    }

    static void Throwable(PrimitiveType t, string name, Vector3 pos, Vector3 scale, Material m, GameObject parent)
    {
        GameObject g = GameObject.CreatePrimitive(t);
        g.name = name;
        g.transform.SetParent(parent.transform);
        g.transform.position = pos;
        g.transform.localScale = scale;
        g.GetComponent<Renderer>().sharedMaterial = m;

        Rigidbody rb = g.AddComponent<Rigidbody>();
        rb.mass = 0.5f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // быстрые броски не пролетают сквозь фигуру

        XRGrabInteractable grab = g.AddComponent<XRGrabInteractable>();
        grab.throwOnDetach = true;
        grab.useDynamicAttach = true;     // берётся там, где схватил, а не "прыгает" в центр руки

        g.AddComponent<Throwable>();
    }
}
#endif
