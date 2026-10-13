using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace EggRescue.Editor
{
    public static class LocalizationSetup
    {
        const string LocaleDir = "Assets/Localization/Locales";
        const string TableDir = "Assets/Localization/Tables";

        public static void ImportBatch()
        {
            Import();
        }

        [MenuItem("Tools/Egg Rescue/PC/Import Localization Tables")]
        public static void ImportMenu()
        {
            var count = Import();
            EditorUtility.DisplayDialog("Egg Rescue PC", "已导入 Unity Localization 字符串表，条目 " + count + "。", "OK");
        }

        public static int Import()
        {
            Directory.CreateDirectory(LocaleDir);
            Directory.CreateDirectory(TableDir);
            var zh = EnsureLocale("zh-Hans", SystemLanguage.ChineseSimplified);
            var en = EnsureLocale("en", SystemLanguage.English);
            LocaleCatalog.EnsureLoaded();
            var ui = EnsureCollection("UI");
            var dialogue = EnsureCollection("Dialogue");
            EnsureTable(ui, zh);
            EnsureTable(ui, en);
            EnsureTable(dialogue, zh);
            EnsureTable(dialogue, en);

            var count = 0;
            foreach (var key in LocaleCatalog.UiZhEntries().Keys)
            {
                Write(ui, key, LocaleCatalog.Ui(key, false), LocaleCatalog.Ui(key, true));
                count++;
            }
            foreach (var pair in LocaleCatalog.NameEntries())
            {
                Write(ui, "name." + pair.Key, LocaleCatalog.Name(pair.Key, false), pair.Value);
                count++;
            }
            foreach (var pair in LocaleCatalog.LineEntries())
            {
                Write(dialogue, pair.Key, pair.Key, pair.Value);
                count++;
            }

            EditorUtility.SetDirty(ui.SharedData);
            EditorUtility.SetDirty(dialogue.SharedData);
            foreach (var table in ui.StringTables) EditorUtility.SetDirty(table);
            foreach (var table in dialogue.StringTables) EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Locale] imported " + count + " entries");
            return count;
        }

        [InitializeOnLoadMethod]
        static void AutoImport()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                var existing = LocalizationEditorSettings.GetStringTableCollection("UI");
                if (existing != null && existing.SharedData != null && existing.SharedData.GetEntry("menu.title") != null)
                    return;
                try
                {
                    Import();
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[Locale] auto import skipped: " + e.Message);
                }
            };
        }

        static Locale EnsureLocale(string code, SystemLanguage language)
        {
            foreach (var locale in LocalizationEditorSettings.GetLocales())
            {
                if (locale != null && locale.Identifier.Code == code) return locale;
            }
            var created = Locale.CreateLocale(language);
            var path = LocaleDir + "/" + code + ".asset";
            AssetDatabase.CreateAsset(created, path);
            LocalizationEditorSettings.AddLocale(created);
            return created;
        }

        static StringTableCollection EnsureCollection(string name)
        {
            var existing = LocalizationEditorSettings.GetStringTableCollection(name);
            if (existing != null) return existing;
            return LocalizationEditorSettings.CreateStringTableCollection(name, TableDir);
        }

        static void EnsureTable(StringTableCollection collection, Locale locale)
        {
            if (collection.GetTable(locale.Identifier) != null) return;
            collection.AddNewTable(locale.Identifier);
        }

        static void Write(StringTableCollection collection, string key, string zh, string en)
        {
            if (string.IsNullOrEmpty(key)) return;
            Set(collection, "zh-Hans", key, zh);
            Set(collection, "en", key, en);
        }

        static void Set(StringTableCollection collection, string code, string key, string value)
        {
            var table = collection.GetTable(new LocaleIdentifier(code)) as StringTable;
            if (table == null) return;
            var entry = table.GetEntry(key);
            if (entry == null) table.AddEntry(key, value ?? "");
            else entry.Value = value ?? "";
        }
    }
}
