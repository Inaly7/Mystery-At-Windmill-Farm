using UnityEngine;
using UnityEngine.UI;

namespace EggRescue
{
    /// <summary>
    /// 主菜单动物的两帧循环。A/B 图来自 Assets/Res/Sprites/StartUi/animalWalk。
    /// </summary>
    public sealed class MenuAnimalFlip : MonoBehaviour
    {
        public const string Folder = "Assets/Res/Sprites/StartUi/animalWalk";

        [SerializeField] Sprite frameA;
        [SerializeField] Sprite frameB;
        [SerializeField] float framesPerSecond = 4f;
        [SerializeField] float phase;

        Image _image;

        void Awake()
        {
            _image = GetComponent<Image>();
            if (_image != null) _image.raycastTarget = false;
#if UNITY_EDITOR
            if (frameA == null || frameB == null)
                TryAssignFromFolder(transform.GetSiblingIndex());
#endif
        }

        void Update()
        {
            if (_image == null || frameA == null || frameB == null || framesPerSecond <= 0f) return;
            var step = Mathf.FloorToInt((Time.unscaledTime + phase) * framesPerSecond);
            var sprite = (step & 1) == 0 ? frameA : frameB;
            if (_image.sprite != sprite) _image.sprite = sprite;
        }

#if UNITY_EDITOR
        public void TryAssignFromFolder(int fallbackIndex)
        {
            var image = GetComponent<Image>();
            if (image == null) return;
            var groups = LoadGroups();
            if (groups.Count == 0) return;
            var current = image.sprite != null ? image.sprite.name : "";
            var key = GroupKey(current);
            FramePair pair;
            if (!groups.TryGetValue(key, out pair) || pair.A == null || pair.B == null)
            {
                var keys = new System.Collections.Generic.List<string>(groups.Keys);
                keys.Sort(System.StringComparer.Ordinal);
                if (fallbackIndex < 0 || fallbackIndex >= keys.Count) return;
                pair = groups[keys[fallbackIndex]];
            }
            if (pair.A == null || pair.B == null) return;
            frameA = pair.A;
            frameB = pair.B;
            UnityEditor.EditorUtility.SetDirty(this);
        }

        struct FramePair
        {
            public Sprite A;
            public Sprite B;
        }

        static System.Collections.Generic.Dictionary<string, FramePair> LoadGroups()
        {
            var groups = new System.Collections.Generic.Dictionary<string, FramePair>();
            var guids = UnityEditor.AssetDatabase.FindAssets("t:Sprite", new[] { Folder });
            for (var i = 0; i < guids.Length; i++)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
                var sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null) continue;
                var key = GroupKey(sprite.name);
                FramePair pair;
                groups.TryGetValue(key, out pair);
                if (sprite.name.Contains("_A_")) pair.A = sprite;
                else if (sprite.name.Contains("_B_")) pair.B = sprite;
                groups[key] = pair;
            }
            return groups;
        }

        public static string GroupKey(string spriteName)
        {
            if (string.IsNullOrEmpty(spriteName)) return "";
            var a = spriteName.IndexOf("_A_", System.StringComparison.Ordinal);
            var b = spriteName.IndexOf("_B_", System.StringComparison.Ordinal);
            var cut = a >= 0 && b >= 0 ? Mathf.Min(a, b) : Mathf.Max(a, b);
            return cut >= 0 ? spriteName.Substring(0, cut) : spriteName;
        }
#endif
    }
}
