using UnityEngine;

namespace EggRescue
{
    /// <summary>
    /// 主菜单里改的音量、鼠标灵敏度、分辨率和窗口/全屏。写入 PlayerPrefs。
    /// </summary>
    public static class GameSettings
    {
        const string VolumeKey = "egg.masterVolume";
        const string SensitivityKey = "egg.mouseSensitivity";
        const string WidthKey = "egg.screenWidth";
        const string HeightKey = "egg.screenHeight";
        const string FullscreenKey = "egg.fullScreen";
        public const float DefaultSensitivity = 2.2f;

        static readonly int[] ResolutionWidths = { 1280, 1600, 1920, 2560, 3840 };
        static readonly int[] ResolutionHeights = { 720, 900, 1080, 1440, 2160 };
        static int _resolutionIndex = 2;

        public static float MasterVolume { get; private set; } = 1f;
        public static float MouseSensitivity { get; private set; } = DefaultSensitivity;
        public static int ScreenWidth { get; private set; } = 1920;
        public static int ScreenHeight { get; private set; } = 1080;
        public static bool FullScreen { get; private set; } = true;
        public static int ResolutionCount { get { return ResolutionWidths.Length; } }
        public static int ResolutionIndex { get { return _resolutionIndex; } }
        public static string ResolutionText
        {
            get { return FormatResolution(_resolutionIndex); }
        }

        public static void Load()
        {
            MasterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 1f));
            MouseSensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(SensitivityKey, DefaultSensitivity), 0.4f, 6f);
            FullScreen = PlayerPrefs.HasKey(FullscreenKey)
                ? PlayerPrefs.GetInt(FullscreenKey, 1) == 1
                : Screen.fullScreen;
            SelectSavedResolution();
            Apply();
        }

        public static void SetMasterVolume(float value)
        {
            MasterVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(VolumeKey, MasterVolume);
            PlayerPrefs.Save();
            Apply();
        }

        public static void SetMouseSensitivity(float value)
        {
            MouseSensitivity = Mathf.Clamp(value, 0.4f, 6f);
            PlayerPrefs.SetFloat(SensitivityKey, MouseSensitivity);
            PlayerPrefs.Save();
            Apply();
        }

        public static string FormatResolution(int index)
        {
            if (index < 0 || index >= ResolutionWidths.Length) return "";
            return ResolutionWidths[index] + " × " + ResolutionHeights[index];
        }

        public static void SetResolution(int index)
        {
            if (index < 0 || index >= ResolutionWidths.Length || index == _resolutionIndex) return;
            _resolutionIndex = index;
            ScreenWidth = ResolutionWidths[index];
            ScreenHeight = ResolutionHeights[index];
            SaveDisplay();
        }

        public static void SetFullScreen(bool fullScreen)
        {
            if (FullScreen == fullScreen) return;
            FullScreen = fullScreen;
            SaveDisplay();
        }

        public static void Apply()
        {
            AudioListener.volume = MasterVolume;
            if (ThirdPersonCamera.Instance != null)
                ThirdPersonCamera.Instance.SetMouseSensitivity(MouseSensitivity);
            ApplyDisplay();
        }

        static void SaveDisplay()
        {
            PlayerPrefs.SetInt(WidthKey, ScreenWidth);
            PlayerPrefs.SetInt(HeightKey, ScreenHeight);
            PlayerPrefs.SetInt(FullscreenKey, FullScreen ? 1 : 0);
            PlayerPrefs.Save();
            ApplyDisplay();
        }

        static void ApplyDisplay()
        {
#if UNITY_EDITOR
            return;
#else
            if (ScreenWidth < 640 || ScreenHeight < 480) return;
            var mode = FullScreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            if (Screen.width == ScreenWidth && Screen.height == ScreenHeight && Screen.fullScreenMode == mode)
                return;
            Screen.SetResolution(ScreenWidth, ScreenHeight, mode);
#endif
        }

        static void SelectSavedResolution()
        {
            var savedWidth = PlayerPrefs.GetInt(WidthKey, Screen.width);
            var savedHeight = PlayerPrefs.GetInt(HeightKey, Screen.height);
            _resolutionIndex = FindResolution(savedWidth, savedHeight);
            ScreenWidth = ResolutionWidths[_resolutionIndex];
            ScreenHeight = ResolutionHeights[_resolutionIndex];
        }

        static int FindResolution(int width, int height)
        {
            var best = 0;
            var bestScore = int.MaxValue;
            for (var i = 0; i < ResolutionWidths.Length; i++)
            {
                var score = Mathf.Abs(ResolutionWidths[i] - width) + Mathf.Abs(ResolutionHeights[i] - height);
                if (score >= bestScore) continue;
                bestScore = score;
                best = i;
            }
            return best;
        }
    }
}
