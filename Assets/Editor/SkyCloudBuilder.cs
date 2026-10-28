// 一键安装一批动态云（球面卡片），之后每朵云自己调参数。
//
// 结构（扁平，不再分层）：
//   天空云            ← SkyCloudField：出生点 + 跟随相机 + 下发球心
//   ├─ 云朵_01        ← CloudCard：距离/方位/高度/大小/速度/淡出 全在这一朵上
//   ├─ 云朵_02
//   └─ ...
//
// 为什么不分层：分层适合批量产内容，但逐朵手调时（"这朵大一点、那朵贴地飘"）
// 按朵给参数最直观——想批量就在 Hierarchy 里多选一起改。
//
// 卡片用 20×20 细分的网格（不是两三角面），顶点在 shader 里被投到球面上做弧度。
// 只用 4 个顶点的话，再怎么调都只能是一块平板，云一大就露馅。
//
// 用法：
//   Tools/Sky/安装天空云   —— 全量重建（先删旧的；同时清掉旧的 SkyClouds.prefab）
//   Tools/Sky/添加一朵云   —— 往现有云场里随机加一朵，参数自己在 Inspector 改
//   Tools/Sky/删除天空云   —— 从场景移除
//
// 换贴图：把新 PNG 丢进 Assets/Res/Sky/CloudCards/ 并改下面的 TexPaths，
// 材质会在下次安装时按文件名自动建（已有的不覆盖，_Bend / _Tint 不会被动）。
//
// 注意：shader 的 Properties 块里不要写中文 [Tooltip]——本机 Unity 的 ShaderLab
// 解析器会在中文 tooltip 上报 "unexpected $undefined"，整个 shader 编译失败。
// 参数说明写在注释里即可。
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WindmillFarm.Visual;

public static class SkyCloudBuilder
{
    const string RootName = "天空云";
    const string LegacyPrefabPath = "Assets/Res/Sky/SkyClouds.prefab";
    const string LegacyQuadMeshPath = "Assets/Res/Sky/CloudQuad.mesh";
    const string ShaderPath = "Assets/Res/Sky/CloudCard.shader";
    const string MatDir = "Assets/Res/Sky/Materials";
    const int Subdivisions = 20;

    // 旧版根物体可能叫的名字。新版按 SkyCloudField 组件找；旧版的
    // SkyCloudLayer 脚本已删、组件失效，只能按名字兜底。
    // 注意：旧版是 SaveAsPrefabAsset + InstantiatePrefab，场景里的实例
    // 叫的是资产名 "SkyClouds"，不是代码里的 RootName。
    static readonly string[] LegacyRootNames = { RootName, "SkyClouds", "动态云" };

    static string GridMeshPath => "Assets/Res/Sky/CloudGrid" + Subdivisions + ".mesh";

    static readonly string[] TexPaths =
    {
        "Assets/Res/Sky/CloudCards/cloud_cumulus_mid.png",
        "Assets/Res/Sky/CloudCards/cloud_cumulus_big.png",
        "Assets/Res/Sky/CloudCards/cloud_cirrus_high.png",
        "Assets/Res/Sky/CloudCards/cloud_stratus_low.png",
    };

    /// <summary>一张卡的初始参数。azimuth 度、distance/height/size 米、speed 度/秒。</summary>
    class CardCfg
    {
        public float distance, azimuth, height, size, speed, roll;
        public float fadeStart, fadeEnd, horizonOpacity;
        public int tex;

        public CardCfg(float distance, float azimuth, float height, float size, float speed, int tex,
                       float roll = 0f, float fadeStart = 8f, float fadeEnd = 26f, float horizonOpacity = 0.3f)
        {
            this.distance = distance; this.azimuth = azimuth; this.height = height;
            this.size = size; this.speed = speed; this.tex = tex; this.roll = roll;
            this.fadeStart = fadeStart; this.fadeEnd = fadeEnd; this.horizonOpacity = horizonOpacity;
        }
    }

    // 起步的 12 朵：按高度分四档，对应四张贴图。都是建议值，装完随便改。
    // 层云那三朵贴着地平线，正好用来看淡出效果（horizonOpacity 给得高一些）。
    static readonly CardCfg[] Starter =
    {
        // 卷云：最高、最慢、最大张
        new CardCfg(340f,  30f, 300f, 240f, 0.25f, 2),
        new CardCfg(300f, 150f, 260f, 200f, 0.35f, 2, roll: 6f),
        new CardCfg(360f, 260f, 320f, 260f, 0.20f, 2, roll: -5f),
        // 大积云
        new CardCfg(300f,  80f, 210f, 170f, 0.60f, 1, roll: 4f),
        new CardCfg(340f, 200f, 180f, 150f, 0.70f, 1, roll: -8f),
        new CardCfg(280f, 320f, 200f, 180f, 0.55f, 1),
        // 中积云
        new CardCfg(280f, 120f, 120f, 150f, 1.00f, 0, roll: -6f),
        new CardCfg(320f, 240f, 100f, 130f, 1.10f, 0),
        new CardCfg(260f,  10f, 140f, 160f, 0.90f, 0, roll: 9f),
        // 层云：贴地平线，演示淡出
        new CardCfg(300f, 200f,  45f, 220f, 1.60f, 3, roll: 3f, fadeStart: 4f, fadeEnd: 20f, horizonOpacity: 0.5f),
        new CardCfg(340f,  60f,  30f, 200f, 1.80f, 3, roll: -4f, fadeStart: 4f, fadeEnd: 20f, horizonOpacity: 0.5f),
        new CardCfg(280f, 300f,  55f, 240f, 1.50f, 3, roll: 7f, fadeStart: 4f, fadeEnd: 20f, horizonOpacity: 0.5f),
    };

    // ---------------- 安装 ----------------
    [MenuItem("Tools/Sky/安装天空云")]
    public static void Install()
    {
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        if (shader == null)
        {
            Debug.LogError("[SkyCloud] 找不到 shader：" + ShaderPath);
            return;
        }

        var textures = LoadTextures();
        if (textures == null) return;

        EnsureGridMesh();
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(GridMeshPath);
        if (mesh == null)
        {
            Debug.LogError("[SkyCloud] 创建网格失败：" + GridMeshPath);
            return;
        }
        var materials = EnsureMaterials(shader, textures);

        DestroyRoot();

        var root = new GameObject(RootName);
        root.transform.position = Vector3.zero;
        root.transform.rotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;
        root.AddComponent<SkyCloudField>();

        for (var i = 0; i < Starter.Length; i++)
        {
            CreateCard(root.transform, Starter[i], textures, materials, mesh, i + 1);
        }

        // 先存盘再删遗留资产：存盘后场景不再引用旧预制体，删除才安全
        var scene = root.scene;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        DeleteLegacyAssets();

        AssetDatabase.SaveAssets();
        Debug.Log(string.Format(
            "[SkyCloud] 已安装天空云：根物体「{0}」+ {1} 张卡片（扁平结构，没有分层）。" +
            "每朵云的参数都在 Inspector 的 CloudCard 上：距离/方位/高度/大小/公转/淡出。" +
            "弧度在材质 _Bend 上调（0 平 / 1 贴球面 / >1 夸张）。按 Play 看云动。",
            RootName, Starter.Length));
    }

    // ---------------- 添加一朵 ----------------
    [MenuItem("Tools/Sky/添加一朵云")]
    public static void AddOne()
    {
        var root = FindRoot();
        if (root == null)
        {
            Debug.LogError("[SkyCloud] 场景里没有「" + RootName + "」，先跑 Tools/Sky/安装天空云。");
            return;
        }

        var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        if (shader == null)
        {
            Debug.LogError("[SkyCloud] 找不到 shader：" + ShaderPath);
            return;
        }

        var textures = LoadTextures();
        if (textures == null) return;

        EnsureGridMesh();
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(GridMeshPath);
        if (mesh == null)
        {
            Debug.LogError("[SkyCloud] 创建网格失败：" + GridMeshPath);
            return;
        }
        var materials = EnsureMaterials(shader, textures);

        // 当前时间做种子：每按一次位置都不一样，不满意删掉再按
        Random.InitState(unchecked((int)System.DateTime.Now.Ticks));

        var cfg = RandomCardCfg(root.transform);
        var index = root.transform.childCount + 1;
        var card = CreateCard(root.transform, cfg, textures, materials, mesh, index);

        EditorSceneManager.MarkSceneDirty(root.scene);
        EditorSceneManager.SaveScene(root.scene);
        Debug.Log(string.Format(
            "[SkyCloud] 已添加「{0}」：距离 {1:F0}m / 高度 {2:F0}m / 大小 {3:F0}m / 速度 {4:F2}°/s。参数在 Inspector 里改。",
            card.name, cfg.distance, cfg.height, cfg.size, cfg.speed));
    }

    /// <summary>随机一朵：高度决定用哪张贴图，方位角尽量避开现有卡片。</summary>
    static CardCfg RandomCardCfg(Transform root)
    {
        var height = Random.Range(25f, 340f);
        int tex;
        if (height > 230f) tex = 2;
        else if (height > 140f) tex = 1;
        else if (height > 70f) tex = 0;
        else tex = 3;

        // 层云宽高比 4:1，给大一点才不显得小气；卷云 2.4:1 同理
        var size = Random.Range(120f, 240f);
        if (tex == 3) size *= 1.5f;
        else if (tex == 2) size *= 1.2f;

        var cfg = new CardCfg(
            Random.Range(260f, 560f),
            PickAzimuth(root),
            height,
            size,
            Mathf.Lerp(1.8f, 0.2f, Mathf.InverseLerp(25f, 340f, height)) * Random.Range(0.8f, 1.2f),
            tex,
            roll: Random.Range(-15f, 15f));

        // 贴地的那几朵把淡出阈值压低、最低透明度抬高，否则会淡到几乎看不见
        if (tex == 3)
        {
            cfg.fadeStart = 4f;
            cfg.fadeEnd = 20f;
            cfg.horizonOpacity = 0.5f;
        }
        return cfg;
    }

    /// <summary>抽 6 个候选方位角，取离现有卡片最远的那个。</summary>
    static float PickAzimuth(Transform root)
    {
        var best = 0f;
        var bestScore = -1f;
        for (var i = 0; i < 6; i++)
        {
            var cand = Random.Range(0f, 360f);
            var score = float.MaxValue;
            foreach (Transform child in root)
            {
                var card = child.GetComponent<CloudCard>();
                if (card == null) continue;
                var d = Mathf.Abs(Mathf.DeltaAngle(cand, card.azimuth));
                if (d < score) score = d;
            }
            if (score <= bestScore) continue;
            bestScore = score;
            best = cand;
        }
        return best;
    }

    // ---------------- 删除 ----------------
    [MenuItem("Tools/Sky/删除天空云")]
    public static void Remove()
    {
        if (DestroyRoot())
        {
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("[SkyCloud] 已从场景移除天空云。");
        }
        else
        {
            Debug.Log("[SkyCloud] 场景里没有天空云。");
        }
    }

    // ---------------- 卡片 ----------------
    static GameObject CreateCard(Transform parent, CardCfg cfg, Texture2D[] textures,
                                 Material[] materials, Mesh mesh, int index)
    {
        var card = new GameObject("云朵_" + index.ToString("00"));
        var mf = card.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;
        var mr = card.AddComponent<MeshRenderer>();
        mr.sharedMaterial = materials[cfg.tex];
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        card.transform.SetParent(parent, false);
        card.transform.localScale = Vector3.one; // Apply 会按贴图宽高比重算

        var cloud = card.AddComponent<CloudCard>();
        cloud.distance = cfg.distance;
        cloud.azimuth = cfg.azimuth;
        cloud.height = cfg.height;
        cloud.size = cfg.size;
        cloud.angularSpeed = cfg.speed;
        cloud.roll = cfg.roll;
        cloud.fadeStartAngle = cfg.fadeStart;
        cloud.fadeEndAngle = cfg.fadeEnd;
        cloud.horizonOpacity = cfg.horizonOpacity;
        cloud.Apply(); // AddComponent 时 OnEnable 已按默认值跑过一次，这里按真实参数再跑
        return card;
    }

    // ---------------- 资源 ----------------
    static Texture2D[] LoadTextures()
    {
        var textures = new Texture2D[TexPaths.Length];
        for (var i = 0; i < TexPaths.Length; i++)
        {
            textures[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(TexPaths[i]);
            if (textures[i] == null)
            {
                Debug.LogError("[SkyCloud] 找不到云贴图：" + TexPaths[i]
                               + "（如果刚换过图，先等编辑器刷新完再运行）");
                return null;
            }
        }
        return textures;
    }

    /// <summary>
    /// 生成细分网格。20×20 = 441 顶点 / 800 三角面，一张卡片不到 1k 面，
    /// 十几张卡片总共一万多面，可以忽略。
    /// </summary>
    static void EnsureGridMesh()
    {
        var path = GridMeshPath;
        if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null) return;

        var n = Subdivisions;
        var side = n + 1;
        var verts = new Vector3[side * side];
        var uvs = new Vector2[side * side];

        for (var y = 0; y < side; y++)
        {
            for (var x = 0; x < side; x++)
            {
                var i = y * side + x;
                var u = (float)x / n;
                var v = (float)y / n;
                verts[i] = new Vector3(u - 0.5f, v - 0.5f, 0f);
                uvs[i] = new Vector2(u, v);
            }
        }

        var tris = new int[n * n * 6];
        var t = 0;
        for (var y = 0; y < n; y++)
        {
            for (var x = 0; x < n; x++)
            {
                var i = y * side + x;
                tris[t++] = i;
                tris[t++] = i + side;
                tris[t++] = i + 1;
                tris[t++] = i + 1;
                tris[t++] = i + side;
                tris[t++] = i + side + 1;
            }
        }

        var mesh = new Mesh { name = Path.GetFileNameWithoutExtension(path) };
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateBounds();
        // 关键：顶点在 shader 里被推到球面上，包围盒必须放大，
        // 否则卡片靠近画面边缘时会被视锥剔除突然消失。
        mesh.bounds = new Bounds(Vector3.zero, new Vector3(3f, 3f, 3f));

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        AssetDatabase.CreateAsset(mesh, path);
    }

    static Material[] EnsureMaterials(Shader shader, Texture2D[] textures)
    {
        Directory.CreateDirectory(MatDir);
        var result = new Material[textures.Length];
        for (var i = 0; i < textures.Length; i++)
        {
            var path = MatDir + "/CloudCard_" + textures[i].name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                // 只在新建时写默认值，避免每次安装把手工调过的 _Bend / _Tint 覆盖掉。
                // 地平线淡出三个值不走材质默认值——每朵云用 MaterialPropertyBlock 单独下发。
                mat = new Material(shader) { name = "CloudCard_" + textures[i].name };
                AssetDatabase.CreateAsset(mat, path);
                mat.SetTexture("_BaseMap", textures[i]);
                mat.SetColor("_Tint", Color.white);
                mat.SetFloat("_AlphaScale", 1f);
                mat.SetFloat("_Bend", 1f);
            }
            mat.shader = shader;
            EditorUtility.SetDirty(mat);
            result[i] = mat;
        }
        return result;
    }

    // ---------------- 查找 / 清理 ----------------
    static GameObject FindRoot()
    {
        // 新结构：按组件找
        var fields = Object.FindObjectsByType<SkyCloudField>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var f in fields)
        {
            if (f != null) return f.gameObject;
        }

        // 旧结构：SkyCloudLayer 脚本已删、组件失效，只能按根物体名字兜底。
        // 旧实例是 InstantiatePrefab 出来的，叫的是资产名 SkyClouds。
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t == null || t.parent != null) continue;
            foreach (var name in LegacyRootNames)
            {
                if (t.name == name) return t.gameObject;
            }
        }
        return null;
    }

    static bool DestroyRoot()
    {
        var root = FindRoot();
        if (root == null) return false;
        Object.DestroyImmediate(root);
        return true;
    }

    /// <summary>
    /// 清掉旧版遗留：分层结构的 SkyClouds.prefab（引用了已删除的 SkyCloudLayer）、
    /// 以及早期两三角面时代的 CloudQuad.mesh。删除前逐个确认没有场景再引用它们。
    /// </summary>
    static void DeleteLegacyAssets()
    {
        DeleteIfUnreferenced(LegacyPrefabPath);
        DeleteIfUnreferenced(LegacyQuadMeshPath);
    }

    static void DeleteIfUnreferenced(string assetPath)
    {
        if (AssetDatabase.LoadAssetAtPath<Object>(assetPath) == null) return;

        var guid = ReadGuid(assetPath + ".meta");
        if (guid != null && AnyOpenSceneReferences(guid))
        {
            Debug.LogWarning("[SkyCloud] " + assetPath + " 仍被场景引用，跳过删除，可手动清理。");
            return;
        }
        Debug.Log("[SkyCloud] 清理旧资产：" + assetPath);
        AssetDatabase.DeleteAsset(assetPath);
    }

    static string ReadGuid(string metaPath)
    {
        if (!File.Exists(metaPath)) return null;
        foreach (var line in File.ReadLines(metaPath))
        {
            if (line.StartsWith("guid:")) return line.Substring("guid:".Length).Trim();
        }
        return null;
    }

    /// <summary>读场景文件原文，看是否还引用某个资产的 GUID。读不了就当仍被引用（保守）。</summary>
    static bool AnyOpenSceneReferences(string guid)
    {
        var count = UnityEngine.SceneManagement.SceneManager.sceneCount;
        for (var i = 0; i < count; i++)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.path)) continue;
            try
            {
                if (File.ReadAllText(scene.path).Contains(guid)) return true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[SkyCloud] 读场景失败：" + scene.path + " - " + e.Message);
                return true;
            }
        }
        return false;
    }
}
