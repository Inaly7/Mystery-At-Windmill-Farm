using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EggRescue.Editor
{
    /// <summary>
    /// 给主菜单 Animals 下的每个动物写入两帧，方便在编辑器里直接看，也让正式包带上贴图引用。
    /// </summary>
    [InitializeOnLoad]
    public static class MenuAnimalFlipBaker
    {
        const string MenuScenePath = "Assets/Scenes/StartMenu.unity";

        static MenuAnimalFlipBaker()
        {
            EditorApplication.delayCall += BakeOpenScene;
        }

        [MenuItem("Tools/Egg Rescue/UI/Bake Animal Walk Frames")]
        public static void BakeMenu()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != MenuScenePath)
                scene = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
            var count = Bake(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorUtility.DisplayDialog("Egg Rescue UI", "已给 " + count + " 个动物写入两帧循环。", "OK");
        }

        static void BakeOpenScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = SceneManager.GetActiveScene();
            if (scene.path != MenuScenePath) return;
            var count = Bake(scene);
            if (count > 0) EditorSceneManager.MarkSceneDirty(scene);
        }

        static int Bake(Scene scene)
        {
            var count = 0;
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                var animals = FindDeep(roots[i].transform, "Animals");
                if (animals == null) continue;
                for (var c = 0; c < animals.childCount; c++)
                {
                    var child = animals.GetChild(c);
                    var flip = child.GetComponent<MenuAnimalFlip>();
                    if (flip == null) flip = Undo.AddComponent<MenuAnimalFlip>(child.gameObject);
                    var so = new SerializedObject(flip);
                    var frameA = so.FindProperty("frameA");
                    var frameB = so.FindProperty("frameB");
                    if (frameA.objectReferenceValue != null && frameB.objectReferenceValue != null) continue;
                    flip.TryAssignFromFolder(c);
                    so.Update();
                    so.FindProperty("phase").floatValue = c * 0.12f;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(flip);
                    count++;
                }
            }
            return count;
        }

        static Transform FindDeep(Transform root, string target)
        {
            if (root.name == target) return root;
            for (var i = 0; i < root.childCount; i++)
            {
                var found = FindDeep(root.GetChild(i), target);
                if (found != null) return found;
            }
            return null;
        }
    }
}
