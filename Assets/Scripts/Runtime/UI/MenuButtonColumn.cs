using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EggRescue
{
    /// <summary>
    /// 竖排按钮的悬停放大、摇晃和推开。按钮列表和参数在 Inspector 里调。
    /// 编辑器里把 Preview 拖到 0 以上，可以不进播放直接看效果；调完请拖回 0 再保存。
    /// </summary>
    [ExecuteAlways]
    public sealed class MenuButtonColumn : MonoBehaviour
    {
        [SerializeField] Button[] buttons;
        [SerializeField] bool alignLeft = true;
        [SerializeField] float hoverScale = 1.72f;
        [SerializeField, Tooltip("点击时在悬停大小上再乘的倍数，播完缩回悬停大小。")]
        float clickPop = 1.3f;
        [SerializeField] float extraGap = 14f;
        [SerializeField] float wobbleDegrees = 1.6f;
        [SerializeField] float wobblePixels = 2.2f;
        [SerializeField] float hoverSpeed = 7f;
        [SerializeField] float labelPad = 28f;
        [SerializeField, Range(0f, 1f)] float preview;
        [SerializeField] int previewIndex;

        struct Slot
        {
            public MenuButtonMotion Motion;
            public RectTransform Rect;
            public Vector2 Rest;
            public Vector3 BaseScale;
            public float Height;
            public float Hover;
        }

        readonly List<Slot> _slots = new List<Slot>();
        readonly Dictionary<int, AuthoredButton> _authored = new Dictionary<int, AuthoredButton>();
        readonly Dictionary<int, Vector3> _authoredScale = new Dictionary<int, Vector3>();
        bool _previewing;

        sealed class AuthoredButton
        {
            public float Width;
            public float ReferenceWidth;
        }

        public bool AlignLeft { get { return alignLeft; } }

        void OnEnable()
        {
            if (preview > 0.001f) preview = 0f;
            Rebuild();
        }

        void OnDisable()
        {
            Restore();
            _previewing = false;
        }

        void Update()
        {
            if (!Application.isPlaying)
            {
                EditPreview();
                return;
            }
            if (_slots.Count == 0) Rebuild();
            PlayUpdate();
        }

        void EditPreview()
        {
            if (preview > 0.001f)
            {
                if (!_previewing)
                {
                    Rebuild();
                    _previewing = true;
                }
                Apply(Mathf.Clamp(previewIndex, 0, Mathf.Max(0, _slots.Count - 1)), preview, true);
                return;
            }
            if (!_previewing) return;
            Restore();
            _previewing = false;
        }

        void PlayUpdate()
        {
            var dt = Time.unscaledDeltaTime;
            var count = _slots.Count;
            var hot = -1;
            for (var i = 0; i < count; i++)
            {
                var slot = _slots[i];
                var busy = slot.Motion != null && slot.Motion.Busy;
                var wants = !busy && slot.Motion != null && slot.Motion.WantsHover && slot.Motion.IsInteractable;
                if (wants) hot = i;
                slot.Hover = busy ? 1f : Mathf.MoveTowards(slot.Hover, wants ? 1f : 0f, dt * hoverSpeed);
                _slots[i] = slot;
            }
            Apply(hot, hot >= 0 ? _slots[hot].Hover : 0f, false);
        }

        void Apply(int hot, float hotWeight, bool previewPose)
        {
            var count = _slots.Count;
            if (count == 0) return;
            if (previewPose)
            {
                for (var i = 0; i < count; i++)
                {
                    var slot = _slots[i];
                    slot.Hover = i == hot ? hotWeight : 0f;
                    _slots[i] = slot;
                }
            }

            var shift = new float[count];
            for (var k = 0; k < count; k++)
            {
                var weight = _slots[k].Hover;
                var pulse = PulseOf(k, previewPose);
                if (weight <= 0.001f && pulse <= 0.001f) continue;
                var mul = Mathf.Lerp(1f, hoverScale, weight) * Mathf.Lerp(1f, clickPop, pulse);
                var push = _slots[k].Height * (mul - 1f) * 0.5f + extraGap * weight;
                for (var i = 0; i < count; i++)
                {
                    if (i < k) shift[i] += push;
                    else if (i > k) shift[i] -= push;
                }
            }

            var front = 0;
            var frontHover = 0f;
            for (var i = 0; i < count; i++)
            {
                var slot = _slots[i];
                if (slot.Rect == null) continue;
                var pulse = PulseOf(i, previewPose);
                var wave = Mathf.Sin(Time.unscaledTime * 5.5f + i) * slot.Hover * (1f - pulse);
                slot.Rect.anchoredPosition = slot.Rest + new Vector2(wave * wobblePixels, shift[i]);
                var scale = Mathf.Lerp(1f, hoverScale, slot.Hover) * Mathf.Lerp(1f, clickPop, pulse);
                slot.Rect.localScale = slot.BaseScale * scale;
                slot.Rect.localRotation = Quaternion.Euler(0f, 0f, wave * wobbleDegrees);
                if (slot.Hover > frontHover)
                {
                    frontHover = slot.Hover;
                    front = i;
                }
            }
            if (frontHover > 0.2f && _slots[front].Rect != null)
                RaiseAmongColumn(_slots[front].Rect);
        }

        float PulseOf(int index, bool previewPose)
        {
            if (previewPose) return 0f;
            var motion = _slots[index].Motion;
            return motion != null ? motion.ClickPulse : 0f;
        }

        void Restore()
        {
            for (var i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (slot.Rect == null) continue;
                slot.Rect.anchoredPosition = slot.Rest;
                slot.Rect.localScale = slot.BaseScale;
                slot.Rect.localRotation = Quaternion.identity;
                slot.Hover = 0f;
                _slots[i] = slot;
            }
        }

        void Rebuild()
        {
            _slots.Clear();
            if (buttons == null) return;
            for (var i = 0; i < buttons.Length; i++)
            {
                var button = buttons[i];
                if (button == null) continue;
                var motion = button.GetComponent<MenuButtonMotion>();
                if (motion == null) motion = button.gameObject.AddComponent<MenuButtonMotion>();
                motion.SetColumn(this);
                var label = button.GetComponentInChildren<TMP_Text>();
                if (label != null)
                    label.alignment = alignLeft ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.Center;
                var rt = button.transform as RectTransform;
                var height = rt != null ? rt.sizeDelta.y : 64f;
                if (height < 1f && rt != null) height = rt.rect.height;
                var baseScale = AuthoredScale(button);
                button.transform.localScale = baseScale;
                _slots.Add(new Slot
                {
                    Motion = motion,
                    Rect = rt,
                    Rest = rt != null ? rt.anchoredPosition : Vector2.zero,
                    BaseScale = baseScale,
                    Height = height > 1f ? height : 64f
                });
            }
            _slots.Sort((a, b) => b.Rest.y.CompareTo(a.Rest.y));
        }

        void RaiseAmongColumn(RectTransform hovered)
        {
            var highest = hovered.GetSiblingIndex();
            for (var i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].Rect == null) continue;
                var index = _slots[i].Rect.GetSiblingIndex();
                if (index > highest) highest = index;
            }
            hovered.SetSiblingIndex(highest);
        }

        [ContextMenu("按文字收紧背景")]
        public void FitWidths()
        {
            if (preview > 0.001f) return;
            if (buttons == null) return;
            for (var i = 0; i < buttons.Length; i++)
                FitToLabel(buttons[i], alignLeft, labelPad);
            Rebuild();
        }

        /// <summary>
        /// 场景里的宽度是按中文留白调好的。换成英文时只补上字宽差，左右留白保持不变。
        /// </summary>
        public void BeginLocalizedFit()
        {
            Restore();
        }

        public void FitButton(Button button, string referenceText)
        {
            if (button == null || string.IsNullOrEmpty(referenceText)) return;
            var rt = button.transform as RectTransform;
            var label = button.GetComponentInChildren<TMP_Text>();
            if (rt == null || label == null || label.font == null) return;
            var id = button.GetInstanceID();
            if (!_authored.TryGetValue(id, out var authored))
            {
                var referenceWidth = Measure(label, referenceText);
                if (referenceWidth < 1f)
                {
                    label.ForceMeshUpdate();
                    return;
                }
                authored = new AuthoredButton { Width = rt.sizeDelta.x, ReferenceWidth = referenceWidth };
                _authored[id] = authored;
            }
            var currentWidth = Measure(label, label.text);
            label.ForceMeshUpdate();
            if (currentWidth < 1f) return;
            if (alignLeft)
            {
                ApplyWidth(rt, Mathf.Ceil(currentWidth) + labelPad * 2f, true);
                return;
            }
            var width = Mathf.Abs(currentWidth - authored.ReferenceWidth) < 0.75f
                ? authored.Width
                : authored.Width + (currentWidth - authored.ReferenceWidth);
            if (width < 64f) width = 64f;
            ApplyWidth(rt, width, alignLeft);
        }

        public void EndLocalizedFit()
        {
            Rebuild();
        }

        public void Include(Button button)
        {
            if (button == null) return;
            if (buttons != null)
            {
                for (var i = 0; i < buttons.Length; i++)
                {
                    if (buttons[i] == button) return;
                }
            }
            var count = buttons != null ? buttons.Length : 0;
            var next = new Button[count + 1];
            for (var i = 0; i < count; i++) next[i] = buttons[i];
            next[count] = button;
            buttons = next;
        }

        public void Refresh()
        {
            Rebuild();
        }

        Vector3 AuthoredScale(Button button)
        {
            var id = button.GetInstanceID();
            Vector3 scale;
            if (_authoredScale.TryGetValue(id, out scale)) return scale;
            scale = button.transform.localScale;
            if (scale.sqrMagnitude < 0.01f) scale = Vector3.one;
            _authoredScale[id] = scale;
            return scale;
        }

        static float Measure(TMP_Text label, string text)
        {
            if (label == null || label.font == null || string.IsNullOrEmpty(text)) return 0f;
            label.enableWordWrapping = false;
            return label.GetPreferredValues(text).x;
        }

        static void ApplyWidth(RectTransform rt, float width, bool alignLeft)
        {
            var pos = rt.anchoredPosition;
            var pivot = rt.pivot;
            var height = rt.sizeDelta.y;
            if (alignLeft)
            {
                var left = pos.x - rt.sizeDelta.x * pivot.x;
                rt.sizeDelta = new Vector2(width, height);
                rt.anchoredPosition = new Vector2(left + width * pivot.x, pos.y);
                return;
            }
            var center = pos.x + (0.5f - pivot.x) * rt.sizeDelta.x;
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = new Vector2(center - (0.5f - pivot.x) * width, pos.y);
        }

        public static void FitToLabel(Button button, bool alignLeft, float pad)
        {
            if (button == null) return;
            var rt = button.transform as RectTransform;
            var label = button.GetComponentInChildren<TMP_Text>();
            if (rt == null || label == null) return;
            label.enableWordWrapping = false;
            label.alignment = alignLeft ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.Center;
            var labelRt = label.rectTransform;
            labelRt.offsetMin = new Vector2(pad, labelRt.offsetMin.y);
            labelRt.offsetMax = new Vector2(-pad, labelRt.offsetMax.y);
            label.ForceMeshUpdate();
            var textWidth = label.GetPreferredValues(label.text).x;
            if (textWidth < 8f) return;
            var width = Mathf.Ceil(textWidth) + pad * 2f;
            ApplyWidth(rt, width, alignLeft);
        }
    }
}
