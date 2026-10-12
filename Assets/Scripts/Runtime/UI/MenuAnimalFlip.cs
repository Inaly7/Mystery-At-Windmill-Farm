using UnityEngine;
using UnityEngine.UI;

namespace EggRescue
{
    /// <summary>
    /// 主菜单动物的四帧循环。图来自 Assets/Res/Sprites/StartUi/animalWalkFinal。
    /// </summary>
    public sealed class MenuAnimalFlip : MonoBehaviour
    {
        public const string Folder = "Assets/Res/Sprites/StartUi/animalWalkFinal";

        [SerializeField] Sprite[] frames;
        [SerializeField] float framesPerSecond = 8f;
        [SerializeField] float phase;

        Image _image;

        void Awake()
        {
            _image = GetComponent<Image>();
            if (_image != null) _image.raycastTarget = false;
#if UNITY_EDITOR
            if (!HasFourFrames())
                TryAssignFromFolder(transform.GetSiblingIndex());
#endif
        }

        void Update()
        {
            if (_image == null || !HasFourFrames() || framesPerSecond <= 0f) return;
            var step = Mathf.FloorToInt((Time.unscaledTime + phase) * framesPerSecond);
            var index = step % frames.Length;
            if (index < 0) index += frames.Length;
            var sprite = frames[index];
            if (sprite != null && _image.sprite != sprite) _image.sprite = sprite;
        }

        bool HasFourFrames()
        {
            if (frames == null || frames.Length < 4) return false;
            for (var i = 0; i < 4; i++)
            {
                if (frames[i] == null) return false;
            }
            return true;
        }

#if UNITY_EDITOR
        public void TryAssignFromFolder(int fallbackIndex)
        {
            var groups = LoadGroups();
            if (groups.Count == 0) return;
            var image = GetComponent<Image>();
            var current = image != null && image.sprite != null ? image.sprite.name : "";
            var key = GroupKey(current);
            Sprite[] set;
            if (!groups.TryGetValue(key, out set) || !Complete(set))
            {
                var keys = new System.Collections.Generic.List<string>(groups.Keys);
                keys.Sort(System.StringComparer.Ordinal);
                if (fallbackIndex < 0 || fallbackIndex >= keys.Count) return;
                set = groups[keys[fallbackIndex]];
            }
            if (!Complete(set)) return;
            frames = set;
            UnityEditor.EditorUtility.SetDirty(this);
        }

        static bool Complete(Sprite[] set)
        {
            if (set == null || set.Length < 4) return false;
            for (var i = 0; i < 4; i++)
            {
                if (set[i] == null) return false;
            }
            return true;
        }

        static System.Collections.Generic.Dictionary<string, Sprite[]> LoadGroups()
        {
            var groups = new System.Collections.Generic.Dictionary<string, Sprite[]>();
            var guids = UnityEditor.AssetDatabase.FindAssets("t:Sprite", new[] { Folder });
            for (var i = 0; i < guids.Length; i++)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
                var sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null) continue;
                var key = GroupKey(sprite.name);
                var frame = FrameIndex(sprite.name);
                if (frame < 0) continue;
                Sprite[] set;
                if (!groups.TryGetValue(key, out set))
                {
                    set = new Sprite[4];
                    groups[key] = set;
                }
                set[frame] = sprite;
            }
            return groups;
        }

        public static string GroupKey(string spriteName)
        {
            if (string.IsNullOrEmpty(spriteName)) return "";
            var walk = spriteName.IndexOf("_walk_", System.StringComparison.Ordinal);
            if (walk >= 0) return spriteName.Substring(0, walk);
            var a = spriteName.IndexOf("_A_", System.StringComparison.Ordinal);
            var b = spriteName.IndexOf("_B_", System.StringComparison.Ordinal);
            var cut = a >= 0 && b >= 0 ? Mathf.Min(a, b) : Mathf.Max(a, b);
            return cut >= 0 ? spriteName.Substring(0, cut) : spriteName;
        }

        static int FrameIndex(string spriteName)
        {
            var walk = spriteName.IndexOf("_walk_", System.StringComparison.Ordinal);
            if (walk < 0) return -1;
            int number;
            if (!int.TryParse(spriteName.Substring(walk + 6), out number)) return -1;
            if (number < 1 || number > 4) return -1;
            return number - 1;
        }
#endif
    }
}
