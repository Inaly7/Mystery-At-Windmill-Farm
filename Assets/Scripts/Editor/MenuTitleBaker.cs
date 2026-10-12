using EggRescue;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EggRescue.Editor
{
    /// <summary>
    /// 把中英文标题图写进主菜单，正式包才能带着这两张贴图。
    /// </summary>
    [InitializeOnLoad]
    public static class MenuTitleBaker
    {
        const string MenuScenePath = "Assets/Scenes/StartMenu.unity";

        static MenuTitleBaker()
        {
            EditorApplication.delayCall += AutoBake;
        }

        [MenuItem("Tools/Egg Rescue/UI/Bake Menu Title Sprites")]
        public static void BakeMenu()
        {
            if (Bake(true))
                EditorUtility.DisplayDialog("Egg Rescue UI", "主菜单标题已写入中英文两张图。", "OK");
        }

        static void AutoBake()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var key = "EggRescue.MenuTitle.v2." + Application.dataPath;
            if (EditorPrefs.GetBool(key, false)) return;
            if (Bake(true)) EditorPrefs.SetBool(key, true);
        }

        static bool Bake(bool save)
        {
            var chinese = AssetDatabase.LoadAssetAtPath<Sprite>(StartMenuController.TitleChinesePath);
            var english = AssetDatabase.LoadAssetAtPath<Sprite>(StartMenuController.TitleEnglishPath);
            if (chinese == null || english == null)
            {
                Debug.LogError("[MenuTitle] 找不到标题图：" + StartMenuController.TitleChinesePath);
                return false;
            }

            var scene = SceneManager.GetSceneByPath(MenuScenePath);
            var openedHere = !scene.IsValid() || !scene.isLoaded;
            if (openedHere)
                scene = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Additive);

            StartMenuController menu = null;
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length && menu == null; i++)
                menu = roots[i].GetComponentInChildren<StartMenuController>(true);
            if (menu == null)
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
                Debug.LogError("[MenuTitle] StartMenu 上没有 StartMenuController。");
                return false;
            }

            var so = new SerializedObject(menu);
            var chineseProp = so.FindProperty("_titleChinese");
            var englishProp = so.FindProperty("_titleEnglish");
            if (chineseProp == null || englishProp == null)
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
                Debug.LogError("[MenuTitle] StartMenuController 还没有标题图字段。");
                return false;
            }
            chineseProp.objectReferenceValue = chinese;
            englishProp.objectReferenceValue = english;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(menu);

            var title = menu.transform.Find("Title");
            var image = title != null ? title.GetComponent<Image>() : null;
            if (image != null)
            {
                image.sprite = chinese;
                EditorUtility.SetDirty(image);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (save) EditorSceneManager.SaveScene(scene);
            if (openedHere) EditorSceneManager.CloseScene(scene, true);
            return true;
        }
    }
}
