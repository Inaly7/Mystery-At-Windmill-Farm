using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EggRescue
{
    /// <summary>
    /// 按钮按下后先播一段缩放，再执行动作。竖排时的放大和推开由 MenuButtonColumn 负责。
    /// </summary>
    public sealed class MenuButtonMotion : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        const float SoloHoverScale = 1.04f;
        const float SoloClickPop = 1.34f;
        const float ClickDuration = 0.2f;

        Button _button;
        RectTransform _rt;
        Vector3 _baseScale;
        Vector2 _basePos;
        MenuButtonColumn _column;
        float _hover;
        bool _over;
        bool _busy;

        public bool WantsHover
        {
            get
            {
                if (_busy || !IsInteractable) return false;
                if (_over && !PointerInside()) _over = false;
                return _over;
            }
        }
        public bool Busy { get { return _busy; } }
        public float ClickPulse { get; private set; }
        public bool IsInteractable { get { return _button == null || _button.interactable; } }

        void Awake()
        {
            _button = GetComponent<Button>();
            _rt = transform as RectTransform;
            _baseScale = transform.localScale;
            if (_rt != null) _basePos = _rt.anchoredPosition;
        }

        public void SetColumn(MenuButtonColumn column)
        {
            _column = column;
        }

        void OnDisable()
        {
            _busy = false;
            _over = false;
            _hover = 0f;
            ClickPulse = 0f;
            if (_column != null) return;
            RestoreRest();
        }

        public void Play(UnityAction then)
        {
            if (_busy) return;
            if (_button != null && !_button.interactable) return;
            StartCoroutine(ClickRoutine(then));
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_button != null && !_button.interactable) return;
            _over = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _over = false;
        }

        void Update()
        {
            if (_column != null || _busy || _rt == null) return;
            var target = _over && IsInteractable ? 1f : 0f;
            _hover = Mathf.MoveTowards(_hover, target, Time.unscaledDeltaTime * 8f);
            var blend = Mathf.SmoothStep(0f, 1f, _hover);
            transform.localScale = _baseScale * Mathf.Lerp(1f, SoloHoverScale, blend);
            transform.localRotation = Quaternion.identity;
        }

        IEnumerator ClickRoutine(UnityAction then)
        {
            _busy = true;
            var elapsed = 0f;
            var from = transform.localScale;
            var columnOwnsScale = _column != null;
            while (elapsed < ClickDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                ClickPulse = Punch(Mathf.Clamp01(elapsed / ClickDuration));
                if (!columnOwnsScale)
                    transform.localScale = from * Mathf.Lerp(1f, SoloClickPop, ClickPulse);
                yield return null;
            }
            ClickPulse = 0f;
            if (!columnOwnsScale)
                transform.localScale = from;
            _busy = false;
            if (then != null) then();
        }

        static float Punch(float p)
        {
            const float peakAt = 0.32f;
            if (p <= peakAt)
            {
                var t = p / peakAt;
                return 1f - (1f - t) * (1f - t);
            }
            return 1f - Mathf.SmoothStep(0f, 1f, (p - peakAt) / (1f - peakAt));
        }

        bool PointerInside()
        {
            if (_rt == null) return false;
            var canvas = _rt.GetComponentInParent<Canvas>();
            var camera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            return RectTransformUtility.RectangleContainsScreenPoint(_rt, Input.mousePosition, camera);
        }

        void RestoreRest()
        {
            transform.localScale = _baseScale;
            transform.localRotation = Quaternion.identity;
            if (_rt != null) _rt.anchoredPosition = _basePos;
        }
    }
}
