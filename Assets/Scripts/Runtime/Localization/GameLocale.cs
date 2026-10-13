using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace EggRescue
{
    /// <summary>
    /// 中英文切换。优先读 Unity Localization 当前语言的字符串表，表还没载入时用 catalog。
    /// 语言来源：游戏内选择，然后是启动参数 -language，然后是 Steam 当前游戏语言，最后是简体中文。
    /// 游戏内选择写入 PlayerPrefs 后，下次启动不再被 Steam 覆盖。
    /// </summary>
    public static class GameLocale
        {
            public const string Chinese = "zh-Hans";
            public const string English = "en";
            const string PrefKey = "egg.locale";

        public static event Action Changed;

        public static string Code { get; private set; }
        public static bool IsEnglish { get { return Code != null && Code.StartsWith("en"); } }

        static StringTable _ui;
        static StringTable _lines;
        static bool _applyQueued;
        static Runner _runner;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            Code = ResolveInitial();
            LocaleCatalog.EnsureLoaded();
            SteamLanguage.Ready += OnSteamReady;
            var go = new GameObject("GameLocale");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _runner = go.AddComponent<Runner>();
        }

        /// <summary>玩家在游戏里选定语言。这次选择会记住，并压过 Steam。</summary>
        public static void Set(string code)
        {
            code = Normalize(code);
            PlayerPrefs.SetString(PrefKey, code);
            PlayerPrefs.Save();
            Apply(code);
        }

        public static string Normalize(string code)
        {
            if (string.IsNullOrEmpty(code)) return Chinese;
            if (code.StartsWith("en", StringComparison.OrdinalIgnoreCase)) return English;
            return Chinese;
        }

        static string ResolveInitial()
        {
            if (PlayerPrefs.HasKey(PrefKey))
                return Normalize(PlayerPrefs.GetString(PrefKey, Chinese));
            return ResolvePlatform() ?? Chinese;
        }

        static string ResolvePlatform()
        {
            var launch = SteamLocaleMap.FromLaunchOption();
            if (!string.IsNullOrEmpty(launch)) return launch;
            string steam;
            if (SteamLanguage.TryGetLocale(out steam)) return steam;
            return null;
        }

        static void OnSteamReady()
        {
            PullPlatformIfUnset();
        }

        static void PullPlatformIfUnset()
        {
            if (PlayerPrefs.HasKey(PrefKey)) return;
            var platform = ResolvePlatform();
            if (string.IsNullOrEmpty(platform)) return;
            Apply(platform);
        }

        static void Apply(string code)
        {
            code = Normalize(code);
            if (code == Code) return;
            Code = code;
            _ui = null;
            _lines = null;
            ApplySelectedLocale();
            if (Changed != null) Changed();
            if (_runner != null && LocalizationSettings.HasSettings && LocalizationSettings.InitializationOperation.IsDone)
                _runner.StartCoroutine(ReloadTables());
        }

        public static string T(string key)
        {
            var fromTable = FromTable(_ui, key);
            if (!string.IsNullOrEmpty(fromTable)) return fromTable;
            return LocaleCatalog.Ui(key, IsEnglish);
        }

        public static string Line(string source)
        {
            if (string.IsNullOrEmpty(source) || !IsEnglish) return source;
            var fromTable = FromTable(_lines, source);
            if (!string.IsNullOrEmpty(fromTable)) return fromTable;
            return LocaleCatalog.Line(source, true);
        }

        public static string Name(string source)
        {
            // Keep the dialogue/portrait actor key stable; localize its display name only.
            if (source == "玩家") source = "芦笛";
            if (string.IsNullOrEmpty(source) || !IsEnglish) return source;
            var fromTable = FromTable(_ui, "name." + source);
            if (!string.IsNullOrEmpty(fromTable)) return fromTable;
            return LocaleCatalog.Name(source, true);
        }

        static string FromTable(StringTable table, string key)
        {
            if (table == null || string.IsNullOrEmpty(key)) return null;
            try
            {
                var entry = table.GetEntry(key);
                if (entry == null || string.IsNullOrEmpty(entry.Value)) return null;
                return entry.Value;
            }
            catch (Exception)
            {
                return null;
            }
        }

        static void ApplySelectedLocale()
        {
            if (!LocalizationSettings.HasSettings) return;
            if (!LocalizationSettings.InitializationOperation.IsDone)
            {
                _applyQueued = true;
                return;
            }
            var locale = LocalizationSettings.AvailableLocales.GetLocale(new LocaleIdentifier(Code));
            if (locale != null) LocalizationSettings.SelectedLocale = locale;
        }

        static IEnumerator Initialize()
        {
            yield return LocalizationSettings.InitializationOperation;
            ApplySelectedLocale();
            _applyQueued = false;
            yield return ReloadTables();
        }

        static IEnumerator ReloadTables()
        {
            if (!LocalizationSettings.HasSettings) yield break;
            var ui = LocalizationSettings.StringDatabase.GetTableAsync("UI");
            var lines = LocalizationSettings.StringDatabase.GetTableAsync("Dialogue");
            yield return ui;
            yield return lines;
            if (ui.IsDone && ui.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
                _ui = ui.Result;
            if (lines.IsDone && lines.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
                _lines = lines.Result;
            if (Changed != null) Changed();
        }

        sealed class Runner : MonoBehaviour
        {
            void OnEnable()
            {
                LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
            }

            void OnDisable()
            {
                LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
            }

            void Start()
            {
                PullPlatformIfUnset();
                StartCoroutine(Initialize());
            }

            void OnLocaleChanged(Locale locale)
            {
                if (locale == null || _applyQueued) return;
                if (locale.Identifier.Code == Code) return;
                ApplySelectedLocale();
            }
        }
    }
}
