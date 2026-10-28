using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WindmillFarm.EditorTools
{
    // 在 WaterTestPlane 上调试冲击流水：自动测量该斜面的真实落差与下坡方向，
    // 把参数配成"上游平静、下游湍急"，并用一份独立材质副本，避免污染池塘。
    [InitializeOnLoad]
    internal static class WaterRapidsDebug
    {
        const string SrcMat = "Assets/AZURE Nature/Materials/Nature/AN_Water.mat";
        const string DstMat = "Assets/Res/vfx/mats/AN_Water_Rapids.mat";
        const string MarkerPath = "Library/WaterRapidsDebug.marker";
        const string TargetName = "WaterTestPlane";
        const string SceneName = "Mechanics_Code";

        static WaterRapidsDebug()
        {
            EditorSceneManager.sceneOpened += OnSceneOpened;
            TryApply(SceneManager.GetActiveScene());
        }

        static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            TryApply(scene);
        }

        static void TryApply(Scene scene)
        {
            if (!System.IO.File.Exists(MarkerPath)) return;
            if (scene.name != SceneName) return;
            System.IO.File.Delete(MarkerPath);
            Apply();
        }

        [MenuItem("Tools/Water/在测试平面上调试激流", false, 21)]
        static void ManualApply()
        {
            Apply();
        }

        static void Apply()
        {
            var go = GameObject.Find(TargetName);
            if (go == null)
            {
                Debug.LogWarning("[WaterRapids] 场景里找不到 " + TargetName);
                return;
            }

            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer == null)
            {
                Debug.LogWarning("[WaterRapids] " + TargetName + " 上没有 MeshRenderer");
                return;
            }

            var src = AssetDatabase.LoadAssetAtPath<Material>(SrcMat);
            if (src == null)
            {
                Debug.LogWarning("[WaterRapids] 找不到源材质 " + SrcMat);
                return;
            }

            var mat = AssetDatabase.LoadAssetAtPath<Material>(DstMat);
            if (mat == null)
            {
                if (!AssetDatabase.CopyAsset(SrcMat, DstMat))
                {
                    Debug.LogWarning("[WaterRapids] 复制材质失败 -> " + DstMat);
                    return;
                }
                AssetDatabase.ImportAsset(DstMat);
                mat = AssetDatabase.LoadAssetAtPath<Material>(DstMat);
            }
            if (mat == null)
            {
                Debug.LogWarning("[WaterRapids] 无法取得材质副本 " + DstMat);
                return;
            }

            renderer.sharedMaterial = mat;
            EditorUtility.SetDirty(renderer);
            EditorSceneManager.MarkSceneDirty(go.scene);

            // Unity 内置 Plane 为 10x10，四角局部坐标 +-5
            var t = go.transform;
            var local = new[]
            {
                new Vector3(-5f, 0f, -5f), new Vector3(5f, 0f, -5f),
                new Vector3(-5f, 0f, 5f),  new Vector3(5f, 0f, 5f)
            };
            var world = new Vector3[4];
            float minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                world[i] = t.TransformPoint(local[i]);
                minY = Mathf.Min(minY, world[i].y);
                maxY = Mathf.Max(maxY, world[i].y);
            }

            int hi = 0, lo = 0;
            for (int i = 0; i < 4; i++)
            {
                if (world[i].y >= maxY - 1e-4f) hi = i;
                if (world[i].y <= minY + 1e-4f) lo = i;
            }

            Vector3 down = world[lo] - world[hi];
            down.y = 0f;
            float angle = down.sqrMagnitude < 1e-6f ? 0f : Mathf.Atan2(down.x, down.z) * Mathf.Rad2Deg;
            if (angle < 0f) angle += 360f;

            float drop = Mathf.Max(maxY - minY, 0.001f);
            float planeSize = Vector3.Distance(world[0], world[3]);

            // 只写入手算不出来的量：下坡方向、实际落差区间、冲击中心。
            // 速度 / 拉伸 / 色阶 / 泡沫色等手感参数一律保留你当前在 Inspector 上调的值。

            // 冲击落在下游低端：平静端在高处，湍急端在低处（参考图瀑布底部泡沫最密）。
            // 利用 shader 里 heightSpan 可为负的特性实现反向渐变。
            mat.SetFloat("_FlowHeightLow", maxY);
            mat.SetFloat("_FlowHeightHigh", minY);

            // 岸线/障碍泡沫：宽度按米计，决定石头周围多远的浅水会起沫。
            // 开阔水面压低，让泡沫集中在物体周围而不是铺满整片。
            mat.SetFloat("_FlowShoreWidth", 1.2f);
            mat.SetFloat("_FlowOpenWater", 0.06f);
            mat.SetFloat("_FlowAccent", 0.14f);

            mat.SetFloat("_FlowAngle", angle);

            mat.SetVector("_FlowImpactCenter", new Vector4(world[lo].x, world[lo].z, 0f, 0f));

            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();

            Debug.Log(string.Format(
                "[WaterRapids] {0}: 落差 {1:F2} m, 下坡角 {2:F1}°, 高端 y={3:F2} -> 低端 y={4:F2}, 尺寸 {5:F1} m。材质副本 {6}。场景已标脏，请 Ctrl+S 保存。",
                TargetName, drop, angle, maxY, minY, planeSize, DstMat));
        }
    }
}
