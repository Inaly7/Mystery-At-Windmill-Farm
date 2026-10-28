// 《风车农场悬案》视觉调参面板（L1 定调）
// 挂在场景根节点的「视觉调参」物体上，Inspector 内实时微调，改完即所见。
// 所有值在运行时同样生效（打包后保持同一套 look）。
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace WindmillFarm.Visual
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class VisualPolishSettings : MonoBehaviour
    {
        [Header("—— 关联资源（不要改）——")]
        public VolumeProfile profile;
        public Light sun;
        public FlatKit.FogSettings fogSettings;
        public UniversalRenderPipelineAsset urpAsset;

        [Header("—— 总控 ——")]
        [Tooltip("关掉 = 回到调优前的原始画面，用于 A/B 对比。改完记得开回来。")]
        public bool lookEnabled = true;

        [Header("—— 画质（仅编辑器生效）——")]
        [Tooltip("抗锯齿。1=关（会看到明显锯齿），4=推荐，8=更干净但吃性能")]
        public int msaa = 4;
        [Tooltip("主光阴影贴图分辨率。1024 会糊，2048 够用，4096 远景更利落")]
        public int shadowResolution = 2048;
        [Tooltip("阴影可见距离（米）。小于 80 时远处地标会失去投影")]
        public float shadowDistance = 120f;
        public bool softShadows = true;

        [Header("—— 色调映射 ——")]
        [Tooltip("ACES：高光滚降自然，云和水面不易过曝。None = 直出（会更硬更亮）")]
        public TonemappingMode tonemapping = TonemappingMode.ACES;

        [Header("—— 色彩 ——")]
        [Tooltip("整体曝光。+ 更亮，参考图的日光感偏亮 0.1~0.25")]
        [Range(-2f, 2f)] public float postExposure = 0.15f;
        [Tooltip("对比。负值让画面更柔（水粉感），低于 -12 会发灰")]
        [Range(-40f, 40f)] public float contrast = -6f;
        [Tooltip("饱和。参考图是中高饱和，建议 +4~+10")]
        [Range(-60f, 60f)] public float saturation = 6f;
        [Tooltip("白平衡色温。+ 偏暖（黄昏/暖阳），- 偏冷")]
        [Range(-60f, 60f)] public float whiteBalanceTemperature = 8f;
        [Tooltip("白平衡色调。+ 偏品红，- 偏绿")]
        [Range(-60f, 60f)] public float whiteBalanceTint = -3f;
        [Tooltip("整体色滤镜。轻微暖白即可，别用来做大调色")]
        public Color colorFilter = new Color(1f, 0.985f, 0.965f);

        [Header("—— 阴影 / 中调 / 高光 分离（风格的命门）——")]
        [Tooltip("RGB 增益。1=不变。B>1 = 阴影偏冷蓝；R>1 = 阴影偏暖。参考图阴影偏冷")]
        public Vector3 shadows = new Vector3(0.97f, 1.00f, 1.045f);
        [Tooltip("中间调增益。参考图几乎中性，微暖即可")]
        public Vector3 midtones = new Vector3(1.02f, 1.00f, 0.975f);
        [Tooltip("高光增益。R>1 / B<1 = 高光偏暖（暖阳光的核心）")]
        public Vector3 highlights = new Vector3(1.055f, 1.015f, 0.955f);

        [Header("—— 泛光 Bloom ——")]
        [Tooltip("起辉阈值。1.0 左右：只有天空、水面、窗光这类高亮才会发光")]
        [Range(0f, 4f)] public float bloomThreshold = 1.05f;
        [Tooltip("强度。0.2~0.4 之间最像手绘，超过 0.6 会糊成一片")]
        [Range(0f, 2f)] public float bloomIntensity = 0.32f;
        [Tooltip("扩散。越大光晕越宽越柔")]
        [Range(0f, 1f)] public float bloomScatter = 0.62f;
        [Tooltip("泛光颜色。暖白，别用纯白（会发灰）")]
        public Color bloomTint = new Color(1f, 0.97f, 0.92f);

        [Header("—— 压角 Vignette ——")]
        [Range(0f, 1f)] public float vignetteIntensity = 0.16f;
        [Range(0f, 1f)] public float vignetteSmoothness = 0.65f;
        public Color vignetteColor = new Color(0.10f, 0.12f, 0.14f);

        [Header("—— 雾（空气透视）——")]
        public bool fogEnabled = true;
        [Tooltip("起雾距离。太近会糊掉中景地标")]
        public float fogNear = 28f;
        [Tooltip("雾满距离。150~200 之间比较自然")]
        public float fogFar = 165f;
        [Tooltip("正视颜色（近处）。极淡的暖白")]
        public Color fogNearColor = new Color(0.937f, 0.976f, 0.980f);
        [Tooltip("远处颜色。偏青灰 = 空气透视成立")]
        public Color fogFarColor = new Color(0.784f, 0.878f, 0.902f);
        [Tooltip("雾浓度。0.4~0.7 之间；1.0 会盖住远景")]
        [Range(0f, 1f)] public float fogIntensity = 1f;
        [Tooltip("高度雾：贴地面的一层薄雾，适合晨景")]
        public bool heightFogEnabled = false;
        public float heightFogLow = 0f;
        public float heightFogHigh = 6f;
        [Range(0f, 1f)] public float heightFogIntensity = 0.4f;
        [Tooltip("低层雾颜色。贴地最浓，往上淡出；建议取比雾远色更淡的冷灰")]
        public Color heightFogColor = new Color(0.86f, 0.92f, 0.94f);

        [Header("—— 环境光（冷天光 + 暖地面反光）——")]
        [Tooltip("取色方式。Skybox=从天盒实时算（更暖更亮，ArtTest 场景用的是这个）；Trilight=手动三分量；Flat=单一色；Custom=用光照设置资产")]
        public AmbientMode ambientMode = AmbientMode.Trilight;
        [Tooltip("天空方向的环境光。偏青蓝")]
        public Color ambientSky = new Color(0.663f, 0.784f, 0.878f);
        [Tooltip("地平线方向。中性微绿")]
        public Color ambientEquator = new Color(0.796f, 0.839f, 0.761f);
        [Tooltip("地面反光。暖褐，负责把角色肚子打暖")]
        public Color ambientGround = new Color(0.549f, 0.451f, 0.333f);
        [Range(0f, 3f)] public float ambientIntensity = 1f;

        [Header("—— 主光 ——")]
        [Tooltip("暖白日光。别用纯白，会失去时间感")]
        public Color sunColor = new Color(1f, 0.945f, 0.855f);
        [Range(0f, 5f)] public float sunIntensity = 1.55f;
        [Tooltip("阴影浓度。0.7 左右最软，1.0 会显得硬")]
        [Range(0f, 1f)] public float shadowStrength = 0.72f;

        // ---------------- 应用 ----------------
        void OnEnable() => Apply();
        void OnValidate() => Apply();

        [ContextMenu("应用（Apply）")]
        public void Apply()
        {
            if (profile == null) return;

            // 1) 后处理
            var tone = Get<Tonemapping>(profile);
            if (tone != null) { tone.active = lookEnabled; if (lookEnabled) { tone.mode.overrideState = true; tone.mode.value = tonemapping; } }

            var wb = Get<WhiteBalance>(profile);
            if (wb != null) { wb.active = lookEnabled; if (lookEnabled) { Set(wb.temperature, whiteBalanceTemperature); Set(wb.tint, whiteBalanceTint); } }

            var ca = Get<ColorAdjustments>(profile);
            if (ca != null)
            {
                ca.active = lookEnabled;
                if (lookEnabled)
                {
                    Set(ca.postExposure, postExposure);
                    Set(ca.contrast, contrast);
                    Set(ca.saturation, saturation);
                    Set(ca.colorFilter, colorFilter);
                }
            }

            var smh = Get<ShadowsMidtonesHighlights>(profile);
            if (smh != null)
            {
                smh.active = lookEnabled;
                if (lookEnabled)
                {
                    Set(smh.shadows, ToV4(shadows));
                    Set(smh.midtones, ToV4(midtones));
                    Set(smh.highlights, ToV4(highlights));
                }
            }

            var bloom = Get<Bloom>(profile);
            if (bloom != null)
            {
                bloom.active = lookEnabled;
                if (lookEnabled)
                {
                    Set(bloom.threshold, bloomThreshold);
                    Set(bloom.intensity, bloomIntensity);
                    Set(bloom.scatter, bloomScatter);
                    Set(bloom.tint, bloomTint);
                }
            }

            var vig = Get<Vignette>(profile);
            if (vig != null)
            {
                vig.active = lookEnabled;
                if (lookEnabled)
                {
                    Set(vig.intensity, vignetteIntensity);
                    Set(vig.smoothness, vignetteSmoothness);
                    Set(vig.color, vignetteColor);
                }
            }

            // 与本风格冲突的遗留项，永远关掉
            var chroma = Get<ChromaticAberration>(profile);
            if (chroma != null) chroma.active = false;

            // 2) 雾
            if (fogSettings != null)
            {
                fogSettings.useDistance = fogEnabled && lookEnabled;
                fogSettings.near = fogNear;
                fogSettings.far = fogFar;
                fogSettings.distanceFogIntensity = fogIntensity;
                fogSettings.distanceGradient = BuildGradient(fogNearColor, fogFarColor);
                fogSettings.useHeight = heightFogEnabled && lookEnabled;
                fogSettings.low = heightFogLow;
                fogSettings.high = heightFogHigh;
                fogSettings.heightFogIntensity = heightFogIntensity;
                fogSettings.heightGradient = BuildHeightGradient(heightFogColor);
                // FlatKit 靠内部回调刷新雾的 LUT 与材质属性；
                // 该成员是 internal 且 FlatKit 在自己的程序集里，这里用反射触发（拿不到就静默跳过）
                InvokeFogRefresh(fogSettings);
            }

            // 3) 环境光与内置雾
            RenderSettings.ambientMode = ambientMode;
            RenderSettings.ambientSkyColor = ambientSky;
            RenderSettings.ambientEquatorColor = ambientEquator;
            RenderSettings.ambientGroundColor = ambientGround;
            RenderSettings.ambientIntensity = ambientIntensity;
            RenderSettings.fog = false;   // 内置雾关掉，避免与 FlatKit 雾叠加

            // 4) 主光
            if (sun != null)
            {
                sun.color = sunColor;
                sun.intensity = sunIntensity;
                sun.shadowStrength = shadowStrength;
                sun.shadows = LightShadows.Soft;
                sun.shadowBias = 0.05f;
                sun.shadowNormalBias = 0.4f;
            }

#if UNITY_EDITOR
            // 5) 画质（只能在编辑器改，改动会重建渲染目标）
            if (urpAsset != null)
            {
                var so = new UnityEditor.SerializedObject(urpAsset);
                SetSerialized(so, "m_MSAA", msaa);
                SetSerialized(so, "m_MainLightShadowmapResolution", shadowResolution);
                SetSerialized(so, "m_ShadowDistance", shadowDistance);
                SetSerialized(so, "m_SoftShadowsSupported", softShadows ? 1 : 0);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
#endif
        }

        static Gradient BuildGradient(Color a, Color b)
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(a, 0f), new GradientColorKey(b, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.55f, 0.45f), new GradientAlphaKey(1f, 1f) });
            return g;
        }

        /// <summary>
        /// 高度雾用：颜色不变，透明度从低处 1 递减到高处 0（FlatKit 的 heightGradient
        /// 左边对应 low、右边对应 high，alpha 即该高度的雾浓度）。
        /// </summary>
        static Gradient BuildHeightGradient(Color c)
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        static System.Reflection.FieldInfo _fogChangedField;
        static bool _fogFieldSearched;

        static void InvokeFogRefresh(FlatKit.FogSettings settings)
        {
            if (!_fogFieldSearched)
            {
                _fogFieldSearched = true;
                _fogChangedField = typeof(FlatKit.FogSettings).GetField(
                    "onSettingsChanged",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Public);
            }
            if (_fogChangedField == null) return;   // 该版本没有回调字段，忽略即可
            var action = _fogChangedField.GetValue(settings) as System.Action;
            action?.Invoke();
        }

        static void Set(ClampedFloatParameter p, float v) { p.overrideState = true; p.value = v; }
        static void Set(FloatParameter p, float v) { p.overrideState = true; p.value = v; }
        static void Set(ColorParameter p, Color v) { p.overrideState = true; p.value = v; }
        static void Set(Vector4Parameter p, Vector4 v) { p.overrideState = true; p.value = v; }
        static void Set(BoolParameter p, bool v) { p.overrideState = true; p.value = v; }
        static Vector4 ToV4(Vector3 v) => new Vector4(v.x, v.y, v.z, 0f);

        static T Get<T>(VolumeProfile p) where T : VolumeComponent => p.TryGet(out T c) ? c : null;

#if UNITY_EDITOR
        static void SetSerialized(UnityEditor.SerializedObject so, string path, int v)
        {
            var prop = so.FindProperty(path);
            if (prop != null) prop.intValue = v;
        }
        static void SetSerialized(UnityEditor.SerializedObject so, string path, float v)
        {
            var prop = so.FindProperty(path);
            if (prop != null) prop.floatValue = v;
        }
#endif
    }
}
