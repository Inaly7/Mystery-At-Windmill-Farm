// L1 视觉定调：后处理栈 + 雾 + 光照 + 画质。
// 设计原则：只改参数与开关，不重制资源、不动材质、不换贴图。
// 所有数值集中在本文件顶部，便于逐轮对比调参。
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using WindmillFarm.Visual;

public static class PolishL1
{
    // ---------- 可调常量 ----------
    const string ScenePath      = "Assets/Scenes/Mechanics_Code.unity";
    const string ProfilePath    = "Assets/Settings/SampleSceneProfile.asset";
    const string UrpAssetPath   = "Assets/FlatKit/Demos/Common/URP Configs/[FlatKit] Example URP Asset.asset";
    const string RendererPath   = "Assets/FlatKit/Demos/Common/URP Configs/[FlatKit] Example Renderer.asset";
    const string FogSettingsPath= "Assets/FlatKit/Demos/Common/URP Configs/[FlatKit] Example Fog Settings.asset";

    // 画质
    const int   MSAA = 4;
    const int   ShadowRes = 2048;
    const float ShadowDistance = 120f;

    // 调色（Unit → 龙猫口径：暖高光 / 冷青暗部 / 中段微降对比）
    static readonly Color SunColor      = new Color(1.000f, 0.945f, 0.855f); // #FFF1DA
    const float SunIntensity = 1.55f;

    static readonly Color AmbientSky    = new Color(0.663f, 0.784f, 0.878f); // #A9C8E0
    static readonly Color AmbientEquator= new Color(0.796f, 0.839f, 0.761f); // #CBD6C2
    static readonly Color AmbientGround = new Color(0.549f, 0.451f, 0.333f); // #8C7355

    static readonly Color FogNear = new Color(0.937f, 0.976f, 0.980f);        // #EFF9FA 近处几乎无色
    static readonly Color FogFar  = new Color(0.784f, 0.878f, 0.902f);        // #C8E0E6 远处偏青
    const float FogNearDist = 28f;
    const float FogFarDist  = 165f;

    // ---------- 入口 ----------
    [MenuItem("Tools/Polish/L1 · 视觉定调")]
    public static void Apply()
    {
        int changed = 0;
        changed += ApplyQuality();
        changed += ApplyVolume();
        changed += ApplyFog();
        changed += ApplyLighting();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[PolishL1] 完成，共 {changed} 处变更。");
    }

    // 1) 画质：MSAA / 阴影
    static int ApplyQuality()
    {
        var so = new SerializedObject(AssetDatabase.LoadAssetAtPath<Object>(UrpAssetPath));
        int n = 0;
        n += SetInt(so, "m_MSAA", MSAA);
        n += SetInt(so, "m_MainLightShadowmapResolution", ShadowRes);
        n += SetFloat(so, "m_ShadowDistance", ShadowDistance);
        n += SetBool(so, "m_SoftShadowsSupported", true);
        so.ApplyModifiedPropertiesWithoutUndo();
        Debug.Log($"[PolishL1] 画质：MSAA={MSAA}, 阴影 {ShadowRes}px / {ShadowDistance}m, 软阴影开");
        return n;
    }

    // 2) 后处理栈
    // 注意：VolumeProfile.Add<T>() 只写入内存列表，不会作为子资源落盘；
    // 必须配合 AssetDatabase.AddObjectToAsset，否则重开工程就丢失。
    static T Ensure<T>(VolumeProfile p) where T : VolumeComponent
    {
        if (p.TryGet(out T exist)) return exist;
        var c = ScriptableObject.CreateInstance<T>();
        c.name = typeof(T).Name;
        c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
        p.components.Add(c);
        AssetDatabase.AddObjectToAsset(c, p);
        EditorUtility.SetDirty(p);
        Debug.Log($"[PolishL1] 新增 override：{typeof(T).Name}");
        return c;
    }

    static int ApplyVolume()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (profile == null) { Debug.LogError("[PolishL1] 找不到 VolumeProfile"); return 0; }
        int n = 0;
        Debug.Log("[PolishL1] 现有 override：" + string.Join(", ", profile.components.ConvertAll(x => x.GetType().Name)));

        // 色调映射
        var tone = Ensure<Tonemapping>(profile);
        tone.mode.overrideState = true; tone.mode.value = TonemappingMode.ACES; n++;

        // 白平衡（略暖）
        var wb = Ensure<WhiteBalance>(profile);
        wb.temperature.overrideState = true; wb.temperature.value = 8f;
        wb.tint.overrideState = true;        wb.tint.value = -3f; n++;

        // 基础色彩
        var ca = Ensure<ColorAdjustments>(profile);
        ca.postExposure.overrideState = true; ca.postExposure.value = 0.15f;
        ca.contrast.overrideState = true;     ca.contrast.value = -6f;
        ca.saturation.overrideState = true;   ca.saturation.value = 6f;
        ca.colorFilter.overrideState = true;  ca.colorFilter.value = new Color(1.0f, 0.985f, 0.965f);
        n++;

        // 阴影/中间调/高光分离（冷暗部 + 暖高光 = 手绘光的核心）
        var smh = Ensure<ShadowsMidtonesHighlights>(profile);
        smh.shadows.overrideState = true;    smh.shadows.value    = new Vector4(0.97f, 1.00f, 1.045f, 0f);
        smh.midtones.overrideState = true;   smh.midtones.value   = new Vector4(1.02f, 1.00f, 0.975f, 0f);
        smh.highlights.overrideState = true; smh.highlights.value = new Vector4(1.055f, 1.015f, 0.955f, 0f);
        n++;

        // 泛光：只让天空/水面高光溢出，不打糊整体
        var bloom = Ensure<Bloom>(profile);
        bloom.threshold.overrideState = true; bloom.threshold.value = 1.05f;
        bloom.intensity.overrideState = true; bloom.intensity.value = 0.32f;
        bloom.scatter.overrideState = true;   bloom.scatter.value = 0.62f;
        bloom.highQualityFiltering.overrideState = true; bloom.highQualityFiltering.value = true;
        bloom.tint.overrideState = true; bloom.tint.value = new Color(1f, 0.97f, 0.92f);
        n++;

        // 轻微压角
        var vig = Ensure<Vignette>(profile);
        vig.color.overrideState = true; vig.color.value = new Color(0.10f, 0.12f, 0.14f);
        vig.intensity.overrideState = true; vig.intensity.value = 0.16f;
        vig.smoothness.overrideState = true; vig.smoothness.value = 0.65f;
        n++;

        // 清掉与本风格冲突的遗留 override（色差会让手绘色块边缘发彩边）
        if (profile.TryGet(out ChromaticAberration chroma)) { chroma.active = false; EditorUtility.SetDirty(chroma); }
        Debug.Log("[PolishL1] 现有 override：" + string.Join(", ", profile.components.ConvertAll(x => x.GetType().Name)));

        EditorUtility.SetDirty(profile);
        Debug.Log("[PolishL1] 后处理：ACES / 白平衡 / 色彩 / SMH 分离 / Bloom / Vignette");
        return n;
    }

    // 3) 雾：FlatKit 屏幕空间深度雾接管，关掉内置雾避免双雾打架
    static int ApplyFog()
    {
        int n = 0;
        // 内置雾关闭
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        RenderSettings.fog = false;

        // 启用 FlatKitFog 特性
        var rendererData = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(RendererPath);
        if (rendererData != null)
        {
            var so = new SerializedObject(rendererData);
            var arr = so.FindProperty("m_RendererFeatures");
            for (int i = 0; i < arr.arraySize; i++)
            {
                var feat = arr.GetArrayElementAtIndex(i).objectReferenceValue;
                if (feat == null) continue;
                string fname = feat.name;
                var fso = new SerializedObject(feat);
                var active = fso.FindProperty("m_Active");
                if (active == null) continue;
                bool want = fname.Contains("Fog");          // 本轮只开雾，描边留到 L4
                if (active.boolValue != want) { active.boolValue = want; fso.ApplyModifiedPropertiesWithoutUndo(); n++; }
                Debug.Log($"[PolishL1] RendererFeature「{fname}」→ {(want ? "开" : "关")}");
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // 雾色与距离
        var fogSettings = AssetDatabase.LoadAssetAtPath<Object>(FogSettingsPath);
        if (fogSettings != null)
        {
            var fso = new SerializedObject(fogSettings);
            var grad = fso.FindProperty("distanceGradient");
            if (grad != null)
            {
                var g = new Gradient();
                g.SetKeys(
                    new[] { new GradientColorKey(FogNear, 0f), new GradientColorKey(FogFar, 1f) },
                    new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.55f, 0.45f), new GradientAlphaKey(1f, 1f) });
                grad.gradientValue = g;
            }
            var near = fso.FindProperty("near"); if (near != null) near.floatValue = FogNearDist;
            var far  = fso.FindProperty("far");  if (far  != null) far.floatValue  = FogFarDist;
            var useD = fso.FindProperty("useDistance"); if (useD != null) useD.boolValue = true;
            fso.ApplyModifiedPropertiesWithoutUndo();
            n++;
            Debug.Log($"[PolishL1] 雾：{FogNearDist}–{FogFarDist}m，近暖白 → 远青灰");
        }

        // 雾同样影响环境光读取的天空色，顺手把环境光设为三分量
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = AmbientSky;
        RenderSettings.ambientEquatorColor = AmbientEquator;
        RenderSettings.ambientGroundColor = AmbientGround;
        RenderSettings.ambientIntensity = 1f;

        EditorSceneManager.SaveScene(scene);
        return n;
    }

    // 4) 主光：色调与强度（场景无烘焙贴图，可安全调整）
    static int ApplyLighting()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        int n = 0;
        foreach (var l in Object.FindObjectsOfType<Light>())
        {
            if (l.type != LightType.Directional) continue;
            l.color = SunColor;
            l.intensity = SunIntensity;
            l.shadows = LightShadows.Soft;
            l.shadowStrength = 0.72f;
            l.shadowBias = 0.05f;
            l.shadowNormalBias = 0.4f;
            n++;
            Debug.Log($"[PolishL1] 主光「{l.name}」→ 暖白 {SunIntensity}，软阴影 0.72");
        }
        EditorSceneManager.SaveScene(scene);
        return n;
    }

    // ---------- 安装可视化调参面板 ----------
    [MenuItem("Tools/Polish/安装调参面板（挂到场景）")]
    public static void InstallPanel()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var go = GameObject.Find("视觉调参");
        if (go == null) go = new GameObject("视觉调参");

        var comp = go.GetComponent<VisualPolishSettings>();
        if (comp == null) comp = go.AddComponent<VisualPolishSettings>();

        comp.profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        comp.fogSettings = AssetDatabase.LoadAssetAtPath<FlatKit.FogSettings>(FogSettingsPath);
        comp.urpAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(UrpAssetPath);

        foreach (var l in Object.FindObjectsOfType<Light>())
            if (l.type == LightType.Directional) { comp.sun = l; break; }

        comp.Apply();
        EditorUtility.SetDirty(comp);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[PolishL1] 调参面板已挂到场景根节点「视觉调参」，选中它即可在 Inspector 微调。");
    }

    // ---------- 工具 ----------
    static int SetInt(SerializedObject so, string path, int v)
    { var p = so.FindProperty(path); if (p == null) { Debug.LogWarning("[PolishL1] 缺字段 " + path); return 0; } if (p.intValue == v) return 0; p.intValue = v; return 1; }
    static int SetFloat(SerializedObject so, string path, float v)
    { var p = so.FindProperty(path); if (p == null) { Debug.LogWarning("[PolishL1] 缺字段 " + path); return 0; } if (Mathf.Approximately(p.floatValue, v)) return 0; p.floatValue = v; return 1; }
    static int SetBool(SerializedObject so, string path, bool v)
    { var p = so.FindProperty(path); if (p == null) { Debug.LogWarning("[PolishL1] 缺字段 " + path); return 0; } if (p.boolValue == v) return 0; p.boolValue = v; return 1; }
}
