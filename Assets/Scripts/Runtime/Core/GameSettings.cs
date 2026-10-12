using System.Collections.Generic;
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

        struct ScreenSize
        {
            public int Width;
            public int Height;
        }

        static readonly List<ScreenSize> Resolutions = new List<ScreenSize>();
        static int _resolutionIndex;

        public static float MasterVolume { get; private set; } = 1f;
        public static float MouseSensitivity { get; private set; } = DefaultSensitivity;
        public static int ScreenWidth { get; private set; } = 1920;
        public static int ScreenHeight { get; private set; } = 1080;
        public static bool FullScreen { get; private set; } = true;
        public static bool CanDecreaseResolution { get { return _resolutionIndex > 0; } }
        public static bool CanIncreaseResolution { get { return _resolutionIndex < Resolutions.Count - 1; } }
        public static string ResolutionText
        {
            get { return ScreenWidth + " × " + ScreenHeight; }
        }

        public static void Load()
        {
            MasterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 1f));
            MouseSensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(SensitivityKey, DefaultSensitivity), 0.4f, 6f);
            FullScreen = PlayerPrefs.HasKey(FullscreenKey)
                ? PlayerPrefs.GetInt(FullscreenKey, 1) == 1
                : Screen.fullScreen;
            BuildResolutions();
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

        public static void StepResolution(int direction)
        {
            if (Resolutions.Count == 0) return;
            var next = Mathf.Clamp(_resolutionIndex + direction, 0, Resolutions.Count - 1);
            if (next == _resolutionIndex) return;
            _resolutionIndex = next;
            ScreenWidth = Resolutions[_resolutionIndex].Width;
            ScreenHeight = Resolutions[_resolutionIndex].Height;
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

        static void BuildResolutions()
        {
            Resolutions.Clear();
            var seen = new HashSet<long>();
            var available = Screen.resolutions;
            for (var i = 0; i < available.Length; i++)
            {
                var width = available[i].width;
                var height = available[i].height;
                if (width < 1280 || height < 720) continue;
                AddResolution(seen, width, height);
            }
            AddResolution(seen, Screen.width, Screen.height);
            if (Resolutions.Count == 0)
            {
                AddResolution(seen, 1280, 720);
                AddResolution(seen, 1600, 900);
                AddResolution(seen, 1920, 1080);
                AddResolution(seen, 2560, 1440);
            }
            Resolutions.Sort((a, b) =>
            {
                var width = a.Width.CompareTo(b.Width);
                return width != 0 ? width : a.Height.CompareTo(b.Height);
            });
            var savedWidth = PlayerPrefs.GetInt(WidthKey, Screen.width);
            var savedHeight = PlayerPrefs.GetInt(HeightKey, Screen.height);
            _resolutionIndex = FindResolution(savedWidth, savedHeight);
            ScreenWidth = Resolutions[_resolutionIndex].Width;
            ScreenHeight = Resolutions[_resolutionIndex].Height;
        }

        static void AddResolution(HashSet<long> seen, int width, int height)
        {
            if (width < 640 || height < 480) return;
            var key = ((long)width << 32) | (uint)height;
            if (!seen.Add(key)) return;
            Resolutions.Add(new ScreenSize { Width = width, Height = height });
        }

        static int FindResolution(int width, int height)
        {
            var best = 0;
            var bestScore = int.MaxValue;
            for (var i = 0; i < Resolutions.Count; i++)
            {
                var score = Mathf.Abs(Resolutions[i].Width - width) + Mathf.Abs(Resolutions[i].Height - height);
                if (score >= bestScore) continue;
                bestScore = score;
                best = i;
            }
            return best;
        }
    }
}
