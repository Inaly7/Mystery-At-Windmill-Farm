// 视觉调参面板 · Inspector
// 目标：让美术在一个面板里改完所有 L1 参数，边看边调，不用碰 Volume / URP 资产。
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WindmillFarm.Visual;

[CustomEditor(typeof(VisualPolishSettings))]
[CanEditMultipleObjects]
public sealed class VisualPolishSettingsEditor : Editor
{
    static readonly Color Accent = new Color(0.90f, 0.65f, 0.36f);
    static readonly Color Sub = new Color(0.62f, 0.62f, 0.62f);

    static bool _foldQuality = true, _foldTone = true, _foldColor = true, _foldSmh = true;
    static bool _foldBloom = false, _foldVignette = false, _foldFog = true, _foldAmbient = false, _foldSun = false;

    public override void OnInspectorGUI()
    {
        var t = (VisualPolishSettings)target;

        // ---------- 顶栏 ----------
        var rect = GUILayoutUtility.GetRect(0, 42, GUILayout.ExpandWidth(true));
        var old = GUI.color;
        GUI.color = t.lookEnabled ? new Color(0.20f, 0.16f, 0.12f) : new Color(0.14f, 0.14f, 0.16f);
        GUI.DrawTexture(rect, EditorGUIUtility.whiteTexture);
        GUI.color = old;

        var title = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
        title.normal.textColor = t.lookEnabled ? Accent : Sub;
        GUI.Label(new Rect(rect.x + 10, rect.y + 5, rect.width - 20, 18),
            t.lookEnabled ? "视觉调优 · L1 定调  [ 生效中 ]" : "视觉调优 · L1 定调  [ 已关闭 · 原始画面 ]", title);
        var hint = new GUIStyle(EditorStyles.miniLabel);
        hint.normal.textColor = Sub;
        GUI.Label(new Rect(rect.x + 10, rect.y + 23, rect.width - 20, 16),
            "改任意参数 → 场景/游戏视图立即生效。面板关掉 lookEnabled 可做 A/B 对比。", hint);

        EditorGUILayout.Space(4);

        // ---------- 总控 ----------
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUI.BeginChangeCheck();
            t.lookEnabled = EditorGUILayout.ToggleLeft("  启用这套 look（关掉 = 对比原始画面）", t.lookEnabled);
            if (EditorGUI.EndChangeCheck()) { Apply(t); SceneDirty(t); }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("重新应用", GUILayout.Height(22))) { Apply(t); SceneDirty(t); }
                if (GUILayout.Button("保存到资产", GUILayout.Height(22))) { Apply(t); AssetDatabase.SaveAssets(); SceneDirty(t); }
                if (GUILayout.Button("重置为 L1 默认", GUILayout.Height(22))) { ResetToDefault(t); Apply(t); SceneDirty(t); }
            }
        }

        // ---------- 关联资源 ----------
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("关联资源", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            t.profile = (UnityEngine.Rendering.VolumeProfile)EditorGUILayout.ObjectField("后处理 Profile", t.profile, typeof(UnityEngine.Rendering.VolumeProfile), false);
            t.fogSettings = (FlatKit.FogSettings)EditorGUILayout.ObjectField("雾设置", t.fogSettings, typeof(FlatKit.FogSettings), false);
            t.urpAsset = (UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset)EditorGUILayout.ObjectField("管线资产", t.urpAsset, typeof(UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset), false);
            t.sun = (Light)EditorGUILayout.ObjectField("主光", t.sun, typeof(Light), true);
            if (EditorGUI.EndChangeCheck()) { Apply(t); SceneDirty(t); }
        }

        // ---------- 画质 ----------
        _foldQuality = Section("画质（仅编辑器生效 · 改完需保存）", _foldQuality, () =>
        {
            EditorGUI.BeginChangeCheck();
            t.msaa = EditorGUILayout.IntPopup("抗锯齿 MSAA", t.msaa,
                new[] { "关（锯齿明显）", "2x", "4x（推荐）", "8x" }, new[] { 1, 2, 4, 8 });
            t.shadowResolution = EditorGUILayout.IntPopup("阴影分辨率", t.shadowResolution,
                new[] { "1024（糊）", "2048（够用）", "4096（利落）" }, new[] { 1024, 2048, 4096 });
            t.shadowDistance = EditorGUILayout.Slider(new GUIContent("阴影距离", "低于 80 米时远处地标会没有投影"), t.shadowDistance, 20f, 250f);
            t.softShadows = EditorGUILayout.Toggle(new GUIContent("软阴影", "关掉会变成硬边阴影，风格立刻崩"), t.softShadows);
            if (EditorGUI.EndChangeCheck()) { Apply(t); SceneDirty(t); }
        });

        // ---------- 色调映射 ----------
        _foldTone = Section("色调映射", _foldTone, () =>
        {
            EditorGUI.BeginChangeCheck();
            t.tonemapping = (UnityEngine.Rendering.Universal.TonemappingMode)EditorGUILayout.EnumPopup(
                new GUIContent("模式", "ACES：高光滚降自然；None：直出，更硬更亮"), t.tonemapping);
            if (EditorGUI.EndChangeCheck()) { Apply(t); SceneDirty(t); }
        });

        // ---------- 色彩 ----------
        _foldColor = Section("色彩", _foldColor, () =>
        {
            EditorGUI.BeginChangeCheck();
            t.postExposure = EditorGUILayout.Slider(new GUIContent("曝光", "参考图的日光感偏亮，0.1~0.25"), t.postExposure, -1f, 1f);
            t.contrast = EditorGUILayout.Slider(new GUIContent("对比", "负值更柔（水粉感）；低于 -12 会发灰"), t.contrast, -30f, 30f);
            t.saturation = EditorGUILayout.Slider(new GUIContent("饱和", "建议 +4~+10"), t.saturation, -40f, 40f);
            t.whiteBalanceTemperature = EditorGUILayout.Slider(new GUIContent("色温", "+ 偏暖 / − 偏冷"), t.whiteBalanceTemperature, -40f, 40f);
            t.whiteBalanceTint = EditorGUILayout.Slider(new GUIContent("色调", "+ 偏品红 / − 偏绿"), t.whiteBalanceTint, -40f, 40f);
            t.colorFilter = EditorGUILayout.ColorField(new GUIContent("色滤镜", "轻微暖白即可，别做重度调色"), t.colorFilter);
            if (EditorGUI.EndChangeCheck()) { Apply(t); SceneDirty(t); }
        });

        // ---------- SMH ----------
        _foldSmh = Section("阴影 / 中调 / 高光 分离（风格的命门）", _foldSmh, () =>
        {
            EditorGUILayout.HelpBox("1 = 该通道不变。阴影 B>1 → 冷蓝；高光 R>1 且 B<1 → 暖阳。\n这是“暖高光 + 冷阴影”铁律的唯一实现处。", MessageType.Info);
            EditorGUI.BeginChangeCheck();
            t.shadows = EditorGUILayout.Vector3Field(new GUIContent("阴影", "参考图阴影偏冷：B 略大于 1"), t.shadows);
            t.midtones = EditorGUILayout.Vector3Field(new GUIContent("中间调", "接近中性，微暖"), t.midtones);
            t.highlights = EditorGUILayout.Vector3Field(new GUIContent("高光", "R 大 B 小 = 暖高光"), t.highlights);
            if (EditorGUI.EndChangeCheck()) { Apply(t); SceneDirty(t); }
        });

        // ---------- Bloom ----------
        _foldBloom = Section("泛光 Bloom", _foldBloom, () =>
        {
            EditorGUI.BeginChangeCheck();
            t.bloomThreshold = EditorGUILayout.Slider(new GUIContent("阈值", "1.0 左右：只有天空/水面/窗光发光"), t.bloomThreshold, 0f, 3f);
            t.bloomIntensity = EditorGUILayout.Slider(new GUIContent("强度", "0.2~0.4 最像手绘；>0.6 会糊"), t.bloomIntensity, 0f, 1.5f);
            t.bloomScatter = EditorGUILayout.Slider(new GUIContent("扩散", "越大光晕越宽越柔"), t.bloomScatter, 0f, 1f);
            t.bloomTint = EditorGUILayout.ColorField(new GUIContent("颜色", "暖白，别用纯白"), t.bloomTint);
            if (EditorGUI.EndChangeCheck()) { Apply(t); SceneDirty(t); }
        });

        // ---------- Vignette ----------
        _foldVignette = Section("压角 Vignette", _foldVignette, () =>
        {
            EditorGUI.BeginChangeCheck();
            t.vignetteIntensity = EditorGUILayout.Slider("强度", t.vignetteIntensity, 0f, 0.6f);
            t.vignetteSmoothness = EditorGUILayout.Slider("柔度", t.vignetteSmoothness, 0f, 1f);
            t.vignetteColor = EditorGUILayout.ColorField("颜色", t.vignetteColor);
            if (EditorGUI.EndChangeCheck()) { Apply(t); SceneDirty(t); }
        });

        // ---------- 雾 ----------
        _foldFog = Section("雾 · 空气透视", _foldFog, () =>
        {
            EditorGUI.BeginChangeCheck();
            t.fogEnabled = EditorGUILayout.Toggle(new GUIContent("启用距离雾", "关掉远处不会降饱和，画面会平"), t.fogEnabled);
            using (new EditorGUI.DisabledScope(!t.fogEnabled))
            {
                t.fogNear = EditorGUILayout.Slider(new GUIContent("起雾距离", "太近会糊掉中景地标"), t.fogNear, 0f, 120f);
                t.fogFar = EditorGUILayout.Slider(new GUIContent("满雾距离", "150~200 较自然"), t.fogFar, 60f, 400f);
                t.fogNearColor = EditorGUILayout.ColorField(new GUIContent("近处色", "极淡暖白"), t.fogNearColor);
                t.fogFarColor = EditorGUILayout.ColorField(new GUIContent("远处色", "偏青灰 = 空气透视"), t.fogFarColor);
                t.fogIntensity = EditorGUILayout.Slider(new GUIContent("浓度", "1.0 会盖住远景"), t.fogIntensity, 0f, 1f);
            }
            EditorGUILayout.Space(2);
            t.heightFogEnabled = EditorGUILayout.Toggle(new GUIContent("高度雾", "贴地薄雾，适合晨景"), t.heightFogEnabled);
            using (new EditorGUI.DisabledScope(!t.heightFogEnabled))
            {
                t.heightFogLow = EditorGUILayout.FloatField(new GUIContent("起始高度", "世界 Y。低于此高度雾最浓"), t.heightFogLow);
                t.heightFogHigh = EditorGUILayout.FloatField(new GUIContent("结束高度", "世界 Y。高于此高度雾消失"), t.heightFogHigh);
                t.heightFogIntensity = EditorGUILayout.Slider("浓度", t.heightFogIntensity, 0f, 1f);
                t.heightFogColor = EditorGUILayout.ColorField(new GUIContent("雾色", "取比雾远色更淡的冷灰"), t.heightFogColor);
            }
            if (EditorGUI.EndChangeCheck()) { Apply(t); SceneDirty(t); }
        });

        // ---------- 环境光 ----------
        _foldAmbient = Section("环境光", _foldAmbient, () =>
        {
            EditorGUILayout.HelpBox("冷天光 + 暖地面反光。地面色决定角色肚子和阴影下半部分偏暖还是偏脏。\n取色方式切换会整体改变画面基调，是调地面色调最快的开关。", MessageType.Info);
            EditorGUI.BeginChangeCheck();
            t.ambientMode = (UnityEngine.Rendering.AmbientMode)EditorGUILayout.EnumPopup(
                new GUIContent("取色方式", "Skybox=从天盒算（ArtTest 场景用这个，地面更暖更亮）／Trilight=手动三分量"),
                t.ambientMode);
            using (new EditorGUI.DisabledScope(t.ambientMode != UnityEngine.Rendering.AmbientMode.Trilight))
            {
                t.ambientSky = EditorGUILayout.ColorField(new GUIContent("天空（上）", "偏青蓝"), t.ambientSky);
                t.ambientEquator = EditorGUILayout.ColorField(new GUIContent("地平（中）", "中性微绿"), t.ambientEquator);
                t.ambientGround = EditorGUILayout.ColorField(new GUIContent("地面（下）", "暖褐"), t.ambientGround);
            }
            if (t.ambientMode != UnityEngine.Rendering.AmbientMode.Trilight)
                EditorGUILayout.HelpBox("非 Trilight 模式下，上面三个颜色不起作用（由天盒或光照设置资产决定）。", MessageType.None);
            t.ambientIntensity = EditorGUILayout.Slider("强度", t.ambientIntensity, 0f, 2f);
            if (EditorGUI.EndChangeCheck()) { Apply(t); SceneDirty(t); }
        });

        // ---------- 主光 ----------
        _foldSun = Section("主光", _foldSun, () =>
        {
            EditorGUI.BeginChangeCheck();
            t.sunColor = EditorGUILayout.ColorField(new GUIContent("颜色", "暖白；纯白会失去时间感"), t.sunColor);
            t.sunIntensity = EditorGUILayout.Slider(new GUIContent("强度", "1.3~1.8 是日光区间"), t.sunIntensity, 0f, 4f);
            t.shadowStrength = EditorGUILayout.Slider(new GUIContent("阴影浓度", "0.7 最软"), t.shadowStrength, 0f, 1f);
            if (EditorGUI.EndChangeCheck()) { Apply(t); SceneDirty(t); }
        });

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("判定标准：三族主色 / 冷阴影 / 暖高光 / 有空气透视 / 主体有边缘光 / 无锯齿", EditorStyles.miniLabel);
    }

    static bool Section(string title, bool open, System.Action body)
    {
        var style = new GUIStyle(EditorStyles.foldoutHeader) { fontStyle = FontStyle.Bold };
        open = EditorGUILayout.Foldout(open, title, true, style);
        if (open)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            using (new EditorGUI.IndentLevelScope(1))
            {
                body();
            }
        }
        return open;
    }

    static void Apply(VisualPolishSettings t)
    {
        Undo.RecordObject(t, "Visual Polish");
        t.Apply();
        EditorUtility.SetDirty(t);
    }

    static void SceneDirty(VisualPolishSettings t)
    {
        if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(t.gameObject.scene);
    }

    static void ResetToDefault(VisualPolishSettings t)
    {
        Undo.RecordObject(t, "Reset Polish");
        t.lookEnabled = true;
        t.msaa = 4; t.shadowResolution = 2048; t.shadowDistance = 120f; t.softShadows = true;
        t.tonemapping = UnityEngine.Rendering.Universal.TonemappingMode.ACES;
        t.postExposure = 0.15f; t.contrast = -6f; t.saturation = 6f;
        t.whiteBalanceTemperature = 8f; t.whiteBalanceTint = -3f;
        t.colorFilter = new Color(1f, 0.985f, 0.965f);
        t.shadows = new Vector3(0.97f, 1.00f, 1.045f);
        t.midtones = new Vector3(1.02f, 1.00f, 0.975f);
        t.highlights = new Vector3(1.055f, 1.015f, 0.955f);
        t.bloomThreshold = 1.05f; t.bloomIntensity = 0.32f; t.bloomScatter = 0.62f;
        t.bloomTint = new Color(1f, 0.97f, 0.92f);
        t.vignetteIntensity = 0.16f; t.vignetteSmoothness = 0.65f;
        t.vignetteColor = new Color(0.10f, 0.12f, 0.14f);
        t.fogEnabled = true; t.fogNear = 28f; t.fogFar = 165f;
        t.fogNearColor = new Color(0.937f, 0.976f, 0.980f);
        t.fogFarColor = new Color(0.784f, 0.878f, 0.902f);
        t.fogIntensity = 1f;
        t.heightFogEnabled = false; t.heightFogLow = 0f; t.heightFogHigh = 6f; t.heightFogIntensity = 0.4f;
        t.heightFogColor = new Color(0.86f, 0.92f, 0.94f);
        t.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        t.ambientSky = new Color(0.663f, 0.784f, 0.878f);
        t.ambientEquator = new Color(0.796f, 0.839f, 0.761f);
        t.ambientGround = new Color(0.549f, 0.451f, 0.333f);
        t.ambientIntensity = 1f;
        t.sunColor = new Color(1f, 0.945f, 0.855f);
        t.sunIntensity = 1.55f; t.shadowStrength = 0.72f;
        EditorUtility.SetDirty(t);
    }
}
