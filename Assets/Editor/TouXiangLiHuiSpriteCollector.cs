using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Legacy Douyin binding for dialogue portraits. Current portraits live on DialogueManager.
/// Auto-runs only when that old Sprites block is still present.
/// </summary>
[InitializeOnLoad]
public static class TouXiangLiHuiSpriteCollector
{
    private const string ScenePath = "Assets/Scenes/Mechanics_Code.unity";
    private const string PrefabPath = "Assets/Prefabs/DialogueManager.prefab";
    private const string PortraitRoot = "Assets/Res/Sprites/DialoguePortrait";
    private const int ExpectedSpriteCount = 21;

    static TouXiangLiHuiSpriteCollector()
    {
        EditorApplication.delayCall += TryAutoWireOnLoad;
    }

    [MenuItem("Tools/Egg Rescue/Collect Dialogue Portrait Sprites")]
    public static void CollectPortraitSprites()
    {
        WirePortraitSprites(showDialog: true);
    }

    public static void BatchWirePortraitSprites()
    {
        WirePortraitSprites(showDialog: false);
    }

    private static bool HasLegacySpriteBlock(string yaml)
    {
        return Regex.IsMatch(yaml, @"varName: Sprites\s+Data:", RegexOptions.Singleline);
    }

    private static bool HasValidPortraitGuids(string yaml)
    {
        var currentGuids = new HashSet<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { PortraitRoot }))
        {
            currentGuids.Add(guid);
        }

        Match m = Regex.Match(
            yaml,
            @"varName: Sprites\s+Data:\s+(.*?)\s+varType: UnityEngine\.Sprite\[\]",
            RegexOptions.Singleline);
        if (!m.Success)
        {
            return false;
        }

        foreach (Match gm in Regex.Matches(m.Groups[1].Value, @"guid: ([0-9a-f]+)"))
        {
            if (!currentGuids.Contains(gm.Groups[1].Value))
            {
                return false;
            }
        }

        return CountSpritesInSceneYaml(yaml) >= ExpectedSpriteCount;
    }

    private static void TryAutoWireOnLoad()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        string prefabFull = Path.Combine(Directory.GetCurrentDirectory(), PrefabPath);
        if (!File.Exists(prefabFull))
        {
            return;
        }

        string yaml = File.ReadAllText(prefabFull);
        if (!HasLegacySpriteBlock(yaml) || HasValidPortraitGuids(yaml))
        {
            return;
        }

        WirePortraitSprites(showDialog: false);
    }

    private static void WirePortraitSprites(bool showDialog)
    {
        List<(string name, string guid)> portraits = CollectPortraitGuids();
        if (portraits.Count == 0)
        {
            Debug.LogError("[TouXiangLiHuiSpriteCollector] 未在 " + PortraitRoot + " 找到立绘 PNG");
            return;
        }

        var wrote = false;
        string prefabFull = Path.Combine(Directory.GetCurrentDirectory(), PrefabPath);
        if (File.Exists(prefabFull))
        {
            string yaml = File.ReadAllText(prefabFull);
            string patched = PatchSpritesBlock(yaml, portraits);
            if (patched != null)
            {
                File.WriteAllText(prefabFull, patched);
                AssetDatabase.ImportAsset(PrefabPath);
                wrote = true;
            }
        }

        string sceneFull = Path.Combine(Directory.GetCurrentDirectory(), ScenePath);
        if (File.Exists(sceneFull))
        {
            string sceneYaml = File.ReadAllText(sceneFull);
            if (HasLegacySpriteBlock(sceneYaml) && !HasValidPortraitGuids(sceneYaml))
            {
                string patched = PatchSpritesBlock(sceneYaml, portraits);
                if (patched != null)
                {
                    File.WriteAllText(sceneFull, patched);
                    AssetDatabase.ImportAsset(ScenePath);
                    wrote = true;
                }
            }
        }

        if (!wrote)
        {
            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "Dialogue Portrait Sprites",
                    "立绘在 " + PortraitRoot + "。当前预制体没有旧的 Sprites 绑定，玩家与 NPC 立绘由 DialogueManager 引用。",
                    "OK");
            }
            return;
        }

        string msg = $"已写入 {portraits.Count} 张立绘（Prefab + 必要时 Scene）";
        Debug.Log("[TouXiangLiHuiSpriteCollector] " + msg + ": " +
                  string.Join(", ", portraits.Select(p => p.name)));
        if (showDialog)
        {
            EditorUtility.DisplayDialog("Dialogue Portrait Sprites", msg, "OK");
        }
    }

    private static List<(string name, string guid)> CollectPortraitGuids()
    {
        var list = new List<(string, string)>();
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { PortraitRoot });
        foreach (string guid in guids.OrderBy(AssetDatabase.GUIDToAssetPath))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var normalized = path.Replace('\\', '/');
            if (!normalized.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (normalized.IndexOf("/Player/", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                continue;
            }

            string name = Path.GetFileNameWithoutExtension(path);
            list.Add((name, guid));
        }

        return list;
    }

    private static int CountSpritesInSceneYaml(string yaml)
    {
        Match m = Regex.Match(
            yaml,
            @"varName: Sprites\s+Data:\s+(.*?)\s+varType: UnityEngine\.Sprite\[\]",
            RegexOptions.Singleline);
        if (!m.Success)
        {
            return 0;
        }

        return Regex.Matches(m.Groups[1].Value, @"guid: [0-9a-f]+").Count;
    }

    private static string PatchSpritesBlock(string yaml, List<(string name, string guid)> portraits)
    {
        string lines = string.Join(
            "\n",
            portraits.Select(p => $"        - {{fileID: 21300000, guid: {p.guid}, type: 3}}"));

        string pattern =
            @"(        varName: Sprites\n        Data:\n)" +
            @"(?:        - \{fileID: 21300000, guid: [0-9a-f]+, type: 3\}\n)+" +
            @"(        varType: UnityEngine\.Sprite\[\])";

        if (!Regex.IsMatch(yaml, pattern, RegexOptions.Multiline))
        {
            return null;
        }

        return Regex.Replace(yaml, pattern, "$1" + lines + "\n$2", RegexOptions.Multiline);
    }
}
