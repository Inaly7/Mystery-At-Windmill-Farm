using EggRescue;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EggRescue.Editor
{
    /// <summary>
    /// 用 AssetDatabase 解析团结引擎的资源 ID，并把 btnImage 真正写入场景和预制体。
    /// 首次导入自动执行一次；之后也可从 Tools 菜单手动重做。
    /// </summary>
    [InitializeOnLoad]
    public static class UiButtonSpriteBaker
    {
        const string SpritePath = "Assets/Res/Sprites/UiButtonSprite/btnImage2.png";
        const string MenuScenePath = "Assets/Scenes/StartMenu.unity";
        const string PausePrefabPath = "Assets/Resources/UI/PauseMenuColumn.prefab";
        const string CatalogPath = "Assets/Resources/UiFont.asset";
        const string BakeVersion = "v6";

        static readonly string[] MenuButtonNames =
        {
            "StartButton", "LoadButton", "SettingButton", "CreditsButton", "QuitButton"
        };

        static readonly string[] PauseButtonNames =
        {
            "ResumeButton", "SaveButton", "MainMenuButton"
        };

        static UiButtonSpriteBaker()
        {
            EditorApplication.delayCall += AutoBake;
        }

        [MenuItem("Tools/Egg Rescue/UI/Bake btnImage Into Buttons")]
        public static void BakeMenu()
        {
            if (Bake(true))
                EditorUtility.DisplayDialog("Egg Rescue UI", "btnImage 已写入主菜单和暂停按钮。", "OK");
        }

        static void AutoBake()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var key = "EggRescue.UiButtonSpriteBake." + BakeVersion + "." + Application.dataPath;
            if (EditorPrefs.GetBool(key, false)) return;
            if (Bake(false)) EditorPrefs.SetBool(key, true);
        }

        static bool Bake(bool showErrors)
        {
            var importer = AssetImporter.GetAtPath(SpritePath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
            else
            {
                AssetDatabase.ImportAsset(SpritePath, ImportAssetOptions.ForceUpdate);
            }
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
            if (sprite == null)
            {
                if (showErrors)
                    EditorUtility.DisplayDialog("Egg Rescue UI", "无法加载 " + SpritePath, "OK");
                Debug.LogError("[UI] Unable to load btnImage sprite.");
                return false;
            }

            BakeCatalog(sprite);
            BakePausePrefab(sprite);
            BakeMenuScene(sprite);
            AssetDatabase.SaveAssets();
            return true;
        }

        static void BakeCatalog(Sprite sprite)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<UiFontCatalog>(CatalogPath);
            if (catalog == null) return;
            var so = new SerializedObject(catalog);
            var property = so.FindProperty("ButtonSprite");
            if (property == null) return;
            property.objectReferenceValue = sprite;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        static void BakePausePrefab(Sprite sprite)
        {
            var root = PrefabUtility.LoadPrefabContents(PausePrefabPath);
            try
            {
                for (var i = 0; i < PauseButtonNames.Length; i++)
                    Apply(root.transform.Find(PauseButtonNames[i]), sprite, false);
                PrefabUtility.SaveAsPrefabAsset(root, PausePrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void BakeMenuScene(Sprite sprite)
        {
            var scene = SceneManager.GetSceneByPath(MenuScenePath);
            var openedHere = !scene.IsValid() || !scene.isLoaded;
            if (openedHere)
                scene = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Additive);

            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < MenuButtonNames.Length; i++)
            {
                Transform found = null;
                for (var r = 0; r < roots.Length && found == null; r++)
                    found = FindDeep(roots[r].transform, MenuButtonNames[i]);
                Apply(found, sprite, true);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (openedHere)
            {
                EditorSceneManager.SaveScene(scene);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            for (var i = 0; i < root.childCount; i++)
            {
                var found = FindDeep(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        static void Apply(Transform target, Sprite sprite, bool alignLeft)
        {
            if (target == null) return;
            var image = target.GetComponent<Image>();
            var button = target.GetComponent<Button>();
            if (image != null)
            {
                Undo.RecordObject(image, "Use btnImage");
                image.sprite = sprite;
                image.type = Image.Type.Sliced;
                image.preserveAspect = false;
                EditorUtility.SetDirty(image);
            }
            if (button != null)
            {
                Undo.RecordObject(button, "Light button palette");
                var colors = button.colors;
                colors.normalColor = new Color(0.69803923f, 0.427451f, 0.15686275f, 1f);
                colors.highlightedColor = new Color(0.86f, 0.52f, 0.25f, 1f);
                colors.pressedColor = new Color(0.67f, 0.40f, 0.31f, 1f);
                colors.selectedColor = new Color(0.78f, 0.48f, 0.20f, 1f);
                colors.disabledColor = new Color(0.75f, 0.72f, 0.67f, 0.68f);
                colors.fadeDuration = 0.1f;
                button.colors = colors;
                EditorUtility.SetDirty(button);
            }
            var label = target.GetComponentInChildren<TMP_Text>(true);
            if (label == null) return;
            Undo.RecordObject(label, "White button label");
            label.color = Color.white;
            label.alignment = alignLeft ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.Center;
            if (alignLeft)
            {
                label.enableWordWrapping = false;
                label.ForceMeshUpdate();
                var rt = target as RectTransform;
                var width = label.GetPreferredValues(label.text).x;
                if (rt != null && width > 1f)
                {
                    Undo.RecordObject(rt, "Fit button to label");
                    var left = rt.anchoredPosition.x - rt.sizeDelta.x * rt.pivot.x;
                    var fitted = Mathf.Ceil(width) + 56f;
                    rt.sizeDelta = new Vector2(fitted, rt.sizeDelta.y);
                    rt.anchoredPosition = new Vector2(left + fitted * rt.pivot.x, rt.anchoredPosition.y);
                    EditorUtility.SetDirty(rt);
                }
            }
            EditorUtility.SetDirty(label);
        }
    }
}
