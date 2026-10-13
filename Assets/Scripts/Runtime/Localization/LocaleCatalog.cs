using System.Collections.Generic;
using UnityEngine;

namespace EggRescue
{
    /// <summary>
    /// 同步文案表。编辑器会把同一份数据导入 Unity Localization 字符串表。
    /// </summary>
    public static class LocaleCatalog
    {
        public const string ResourcePath = "Localization/catalog";

        static readonly Dictionary<string, string> UiZh = new Dictionary<string, string>();
        static readonly Dictionary<string, string> UiEn = new Dictionary<string, string>();
        static readonly Dictionary<string, string> NamesEn = new Dictionary<string, string>();
        static readonly Dictionary<string, string> LinesEn = new Dictionary<string, string>();
        static bool _loaded;

        public static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null)
            {
                Debug.LogWarning("[Locale] missing Resources/" + ResourcePath);
                return;
            }
            var root = JsonValue.Parse(asset.text);
            ReadUi(root["ui"]);
            ReadPairs(root["names"], NamesEn);
            ReadPairs(root["lines"], LinesEn);
        }

        public static string Ui(string key, bool english)
        {
            EnsureLoaded();
            string value;
            var table = english ? UiEn : UiZh;
            if (key != null && table.TryGetValue(key, out value) && !string.IsNullOrEmpty(value))
                return value;
            if (!english && key != null && UiZh.TryGetValue(key, out value))
                return value;
            return key ?? "";
        }

        public static string Name(string source, bool english)
        {
            if (source == "玩家") source = "芦笛";
            if (!english || string.IsNullOrEmpty(source)) return source;
            EnsureLoaded();
            string value;
            if (NamesEn.TryGetValue(source, out value) && !string.IsNullOrEmpty(value))
                return value;
            return source;
        }

        public static string Line(string source, bool english)
        {
            if (!english || string.IsNullOrEmpty(source)) return source;
            EnsureLoaded();
            string value;
            if (LinesEn.TryGetValue(source, out value) && !string.IsNullOrEmpty(value))
                return value;
            return source;
        }

        public static IEnumerable<KeyValuePair<string, string>> UiEntries(bool english)
        {
            EnsureLoaded();
            var table = english ? UiEn : UiZh;
            return table;
        }

        public static Dictionary<string, string> NameEntries()
        {
            EnsureLoaded();
            return NamesEn;
        }

        public static Dictionary<string, string> LineEntries()
        {
            EnsureLoaded();
            return LinesEn;
        }

        public static Dictionary<string, string> UiZhEntries()
        {
            EnsureLoaded();
            return UiZh;
        }

        static void ReadUi(JsonValue array)
        {
            foreach (var item in array.AsArray())
            {
                var key = item["key"].AsString();
                if (string.IsNullOrEmpty(key)) continue;
                UiZh[key] = item["zh"].AsString();
                UiEn[key] = item["en"].AsString();
            }
        }

        static void ReadPairs(JsonValue array, Dictionary<string, string> into)
        {
            foreach (var item in array.AsArray())
            {
                var zh = item["zh"].AsString();
                if (string.IsNullOrEmpty(zh)) continue;
                into[zh] = item["en"].AsString();
            }
        }
    }
}
