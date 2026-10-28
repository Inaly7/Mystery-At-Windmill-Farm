using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace WindmillFarm.EditorTools
{
    // 导入 Assets/Res/Model/NewMesh_202610 下的 FBX + TGA，
    // 按贴图命名分组生成 FlatKit 风格化材质并回写到模型。
    internal static class GhibliMeshImport
    {
        const string ModelDir = "Assets/Res/Model/NewMesh_202610";
        const string TexDir = "Assets/Res/Model/NewMesh_202610/Textures";
        const string MatDir = "Assets/Res/Model/NewMesh_202610/Materials";
        const string ShaderName = "FlatKit/Stylized Surface";
        const string MarkerPath = "Library/GhibliMeshImport.marker";

        static readonly string[] Kinds = { "_Base_color", "_Normal_OpenGL", "_Mixed_AO", "_Roughness" };

        class TexSet
        {
            public Texture2D albedo;
            public Texture2D normal;
            public Texture2D ao;
            public Texture2D rough;
        }

        [MenuItem("Tools/Import/生成吉卜力模型材质", false, 30)]
        static void ManualRun()
        {
            Apply();
        }

        static void Apply()
        {
            if (!AssetDatabase.IsValidFolder(MatDir))
                AssetDatabase.CreateFolder(ModelDir, "Materials");

            // 1. 修正法线贴图导入类型，否则赋到 _BumpMap 也不会生效
            int normalFixed = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TexDir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith("_Normal_OpenGL.tga")) continue;
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null || ti.textureType == TextureImporterType.NormalMap) continue;
                ti.textureType = TextureImporterType.NormalMap;
                ti.SaveAndReimport();
                normalFixed++;
            }

            // 2. 按 <组名>_<通道>.tga 归档
            var sets = new Dictionary<string, TexSet>();
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TexDir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var name = Path.GetFileNameWithoutExtension(path);
                string group = null, kind = null;
                foreach (var k in Kinds)
                {
                    if (name.EndsWith(k)) { group = name.Substring(0, name.Length - k.Length); kind = k; break; }
                }
                if (group == null) continue;
                if (!sets.TryGetValue(group, out var set)) { set = new TexSet(); sets[group] = set; }
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                switch (kind)
                {
                    case "_Base_color": set.albedo = tex; break;
                    case "_Normal_OpenGL": set.normal = tex; break;
                    case "_Mixed_AO": set.ao = tex; break;
                    case "_Roughness": set.rough = tex; break;
                }
            }

            // 3. 让 Unity 把 FBX 材质抽成外部 .mat，才能改 shader
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { ModelDir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.ToLower().EndsWith(".fbx")) continue;
                var imp = AssetImporter.GetAtPath(path) as ModelImporter;
                if (imp == null) continue;
                if (imp.materialLocation == ModelImporterMaterialLocation.External) continue;
                imp.materialLocation = ModelImporterMaterialLocation.External;
                imp.materialName = ModelImporterMaterialName.BasedOnMaterialName;
                imp.materialSearch = ModelImporterMaterialSearch.Local;
                imp.SaveAndReimport();
            }
            AssetDatabase.Refresh();

            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogWarning("[GhibliImport] 找不到 shader " + ShaderName);
                return;
            }

            // 4. 逐个材质换成 FlatKit 并根据名字匹配贴图组
            var groups = new List<string>(sets.Keys);
            groups.Sort((a, b) => b.Length.CompareTo(a.Length)); // 长名优先，避免 M_B02 抢走 M_B02_01
            int converted = 0, textured = 0;
            var unmatched = new List<string>();

            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { ModelDir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) continue;

                mat.shader = shader;
                mat.SetColor("_BaseColor", Color.white);
                converted++;

                var group = MatchGroup(mat.name, groups);
                if (group == null)
                {
                    unmatched.Add(mat.name);
                    EditorUtility.SetDirty(mat);
                    continue;
                }

                var set = sets[group];
                if (set.albedo != null) mat.SetTexture("_BaseMap", set.albedo);
                if (set.normal != null) mat.SetTexture("_BumpMap", set.normal);
                if (set.ao != null)
                {
                    // FlatKit 没有独立 AO 槽，借用 Detail 槽做轻微压暗
                    mat.SetTexture("_DetailMap", set.ao);
                    mat.SetFloat("_DetailMapImpact", 0.35f);
                }
                textured++;
                EditorUtility.SetDirty(mat);
            }

            AssetDatabase.SaveAssets();

            Debug.Log(string.Format(
                "[GhibliImport] 法线修正 {0} 张, 贴图组 {1} 组, 材质转 FlatKit {2} 个, 已配贴图 {3} 个。未匹配: {4}",
                normalFixed, sets.Count, converted, textured,
                unmatched.Count == 0 ? "无" : string.Join(", ", unmatched.ToArray())));
        }

        // 先精确匹配，再退化为包含匹配（FBX 内材质名可能不带 M_ 前缀）
        static string MatchGroup(string matName, List<string> groups)
        {
            foreach (var g in groups)
                if (string.Equals(matName, g)) return g;

            foreach (var g in groups)
                if (matName.Contains(g) || g.Contains(matName)) return g;

            return null;
        }
    }
}
