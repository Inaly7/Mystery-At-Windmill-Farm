// 生成风气流用到的材质和 Prefab，省得手动拖。
//
// Prefab 结构特意预置了一条样例线（Gust_00，previewInEditMode = true）：
// 编辑器里不运行也能看到一条完整气流，方便对参数。运行时 WindGustField 建池时
// 会把它一并收进池子复用，不会多出一条不回收的线。
//
// 幂等：重复执行只会更新材质参数、覆盖 Prefab，不会生成 m_windGust 1.mat 这种东西。
using System.IO;
using UnityEditor;
using UnityEngine;
using WindmillFarm.Visual;

namespace WindmillFarm.EditorTools
{
    public static class WindGustBuilder
    {
        const string ShaderName = "WindmillFarm/VFX/WindGust";
        const string MatDir = "Assets/Res/vfx/mats";
        const string MatPath = MatDir + "/m_windGust.mat";
        const string PrefabDir = "Assets/Res/vfx/prefabs";
        const string PrefabPath = PrefabDir + "/windGust.prefab";
        const string MarkerPath = "Library/WindGustBuild.marker";

        [MenuItem("Tools/VFX/生成风气流材质与 Prefab", false, 10)]
        public static void BuildMenu()
        {
            Build();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }

        [MenuItem("Tools/VFX/在场景中放置风气流", false, 11)]
        public static void PlaceInScene()
        {
            Build();

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError("[WindGust] Prefab 生成失败，无法放置。");
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (SceneView.lastActiveSceneView != null)
            {
                var cam = SceneView.lastActiveSceneView.camera;
                if (cam != null)
                {
                    // 放在相机前方 6 米、略高一点的位置，落地就能看见
                    instance.transform.position = cam.transform.position
                                                + cam.transform.forward * 6f
                                                + Vector3.up * 0.8f;
                }
            }

            Undo.RegisterCreatedObjectUndo(instance, "Place Wind Gust");
            Selection.activeObject = instance;
        }

        public static void Build()
        {
            EnsureFolder(MatDir);
            EnsureFolder(PrefabDir);

            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError($"[WindGust] 找不到 Shader '{ShaderName}'，请确认 WindGust.shader 编译通过。");
                return;
            }

            var mat = BuildMaterial(shader);
            BuildPrefab(mat);

            AssetDatabase.SaveAssets();
            Debug.Log($"[WindGust] 已生成材质 {MatPath} 与 Prefab {PrefabPath}");
        }

        static Material BuildMaterial(Shader shader)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, MatPath);
            }
            else
            {
                mat.shader = shader;
            }

            // 吉卜力风格用偏暖的米白，不要纯白（纯白在黑背景上太扎眼）
            mat.SetColor("_Color", new Color(1f, 0.97f, 0.92f, 1f));
            mat.SetFloat("_Opacity", 0.6f);
            mat.SetFloat("_CoreSoftness", 2.2f);
            mat.SetFloat("_WobbleAmp", 0.35f);
            mat.SetFloat("_WobbleFreq", 4.5f);
            mat.SetFloat("_HeadFade", 0.05f);
            mat.SetFloat("_TailFade", 0.14f);
            mat.SetFloat("_DrawProgress", 1.2f);
            mat.SetFloat("_DrawSoftness", 0.05f);
            mat.SetFloat("_FlowTiling", 2f);
            mat.SetFloat("_FlowSpeed", 0.3f);
            mat.SetFloat("_FlowContrast", 0.35f);
            mat.SetFloat("_Phase", 0f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void BuildPrefab(Material mat)
        {
            var root = new GameObject("windGust");
            try
            {
                var field = root.AddComponent<WindGustField>();
                field.gustMaterial = mat;
                field.baseDirection = Vector3.right;

                // 预览线：编辑器里能直接看到一条样例
                var sample = new GameObject("Gust_00");
                sample.transform.SetParent(root.transform, false);

                var lr = sample.AddComponent<LineRenderer>();
                lr.sharedMaterial = mat;
                lr.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
                lr.widthMultiplier = 0.22f;
                lr.positionCount = 0;

                var ribbon = sample.AddComponent<WindGustRibbon>();
                ribbon.previewInEditMode = true;
                ribbon.previewSeed = 20261028;

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            var parts = path.Split('/');
            var cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }

        // 无头构建：放一个 marker 文件（Library/ 不会被 Unity 导入），
        // 编辑器下次加载脚本时自动跑一次然后删掉 marker。
        // 用同步而不是 delayCall —— delayCall 会被随后的域重载清空。
        [InitializeOnLoadMethod]
        static void AutoBuildOnce()
        {
            if (!File.Exists(MarkerPath)) return;

            try
            {
                Debug.Log("[WindGust] 检测到自动构建标记，开始生成材质与 Prefab…");
                Build();
                Debug.Log("[WindGust] 自动构建完成。");
            }
            catch (System.Exception e)
            {
                Debug.LogError("[WindGust] 自动构建失败：" + e);
            }
            finally
            {
                if (File.Exists(MarkerPath)) File.Delete(MarkerPath);
            }
        }
    }
}
