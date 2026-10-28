using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WindmillFarm.EditorTools
{
    // 一次性初始化：给 AN_Water 材质写入吉卜力激流参数的初始值，
    // 并以池塘的实际世界高度校准 "平静 -> 湍急" 的高度渐变区间。
    [InitializeOnLoad]
    internal static class WaterRapidsSetup
    {
        const string MatPath = "Assets/AZURE Nature/Materials/Nature/AN_Water.mat";
        const string MarkerPath = "Library/WaterRapidsSetup.marker";
        const string PondName = "SM_Env_Pond_Water_01";
        const string SceneName = "Mechanics_Code";

        static WaterRapidsSetup()
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

        static void Apply()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            if (mat == null)
            {
                Debug.LogWarning("[WaterRapids] 找不到材质 " + MatPath);
                return;
            }

            float pondY = 0f;
            Vector2 pondXZ = Vector2.zero;
            bool found = false;
            var pond = GameObject.Find(PondName);
            if (pond != null)
            {
                var p = pond.transform.position;
                pondY = p.y;
                pondXZ = new Vector2(p.x, p.z);
                found = true;
            }

            // 高度渐变：池塘所在高度 = 平静基线，往上 6 米进入完全湍急。
            // 河流/瀑布的水面只要摆得比池塘高，就会自动获得更强的激流。
            mat.SetFloat("_FlowHeightLow", pondY);
            mat.SetFloat("_FlowHeightHigh", pondY + 6f);
            mat.SetFloat("_FlowHeightCurve", 1.5f);

            // 流向与流速。流速同时是总开关：设为 0 即完全回到原来的平静外观。
            mat.SetFloat("_FlowAngle", 0f);
            mat.SetFloat("_FlowSpeed", 0.8f);
            mat.SetFloat("_FlowIntensity", 1f);

            // 条纹造型：沿流向拉伸成长条，吉卜力式的两层色阶。
            mat.SetFloat("_FlowStretch", 8f);
            mat.SetFloat("_FlowFoamScale", 2.5f);
            mat.SetFloat("_FlowFoamSteps", 2f);

            // 岸线冲击保留开启，池塘边缘能看到白色泡沫，中央保持平静。
            mat.SetFloat("_FlowShoreBoost", 1f);

            // 手动冲击区默认关闭，需要时在瀑布落点处开启。
            mat.SetFloat("_FlowImpactRadius", 3f);
            mat.SetFloat("_FlowImpactStrength", 0f);
            mat.SetVector("_FlowImpactCenter", new Vector4(pondXZ.x, pondXZ.y, 0f, 0f));

            mat.SetColor("_FlowFoamColor", new Color(1f, 0.96f, 0.9f, 1f));

            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();

            Debug.Log(found
                ? "[WaterRapids] 已初始化 AN_Water 激流参数，池塘世界 Y = " + pondY.ToString("F2")
                : "[WaterRapids] 已初始化 AN_Water 激流参数（未找到 " + PondName + "，高度基线按 0 处理）");
        }

        [MenuItem("Tools/Water/初始化激流参数", false, 20)]
        static void ManualApply()
        {
            Apply();
        }
    }
}
