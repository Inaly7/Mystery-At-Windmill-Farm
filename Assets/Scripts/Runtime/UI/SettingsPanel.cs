using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace EggRescue
{
    /// <summary>
    /// 主菜单和暂停菜单共用的设置：音量、灵敏度、分辨率、窗口/全屏、语言。
    /// </summary>
    public sealed class SettingsPanel : MonoBehaviour
    {
        static readonly Color Paper = new Color(1f, 0.974f, 0.91f, 1f);
        static readonly Color PaperEdge = new Color(0.84f, 0.75f, 0.57f, 1f);
        static readonly Color Plate = new Color(0.89f, 0.93f, 0.82f, 1f);
        static readonly Color Ink = new Color(0.18f, 0.29f, 0.22f, 1f);
        static readonly Color InkSoft = new Color(0.38f, 0.46f, 0.36f, 1f);
        static readonly Color Cream = Color.white;
        static readonly Color ButtonNormal = new Color(0.69803923f, 0.427451f, 0.15686275f, 1f);
        static readonly Color ButtonHighlight = new Color(0.86f, 0.52f, 0.25f, 1f);
        static readonly Color ButtonSelected = new Color(0.78f, 0.48f, 0.20f, 1f);
        static readonly Color ButtonPressed = new Color(0.67f, 0.40f, 0.31f, 1f);
        static readonly Color ButtonDisabled = new Color(0.75f, 0.72f, 0.67f, 0.68f);

        static Sprite _white;

        GameObject _root;
        Button _langZh;
        Button _langEn;
        Button _windowed;
        Button _fullscreen;
        Button _resolutionButton;
        TMP_Text _resolutionValue;
        GameObject _resolutionList;
        Slider _volumeSlider;
        Slider _sensitivitySlider;
        TMP_Text _volumeValue;
        TMP_Text _sensitivityValue;
        readonly List<Button> _resolutionOptions = new List<Button>();
        readonly List<BoundLabel> _labels = new List<BoundLabel>();

        sealed class BoundLabel
        {
            public TMP_Text Label;
            public string Key;
        }

        public bool IsOpen
        {
            get { return _root != null && _root.activeSelf; }
        }

        void OnEnable()
        {
            GameLocale.Changed += RefreshLocale;
        }

        void OnDisable()
        {
            GameLocale.Changed -= RefreshLocale;
        }

        void Update()
        {
            if (_resolutionList == null || !_resolutionList.activeSelf || !Input.GetMouseButtonDown(0)) return;
            var overList = ContainsPointer(_resolutionList.transform as RectTransform);
            var overButton = ContainsPointer(_resolutionButton != null ? _resolutionButton.transform as RectTransform : null);
            if (!overList && !overButton) SetResolutionList(false);
        }

        public void Build(Transform parent)
        {
            if (_root != null || parent == null) return;
            var font = UiFontCatalog.Load();
            _root = CreateOverlay(parent, "SettingsOverlay");
            var card = CreatePaper(_root.transform, new Vector2(760f, 820f));
            AddLabel(card, "menu.settings_title", new Vector2(0f, 360f), new Vector2(640f, 64f), 44f, Ink, font);
            AddPlate(card, new Vector2(0f, 250f), new Vector2(660f, 108f));
            AddPlate(card, new Vector2(0f, 132f), new Vector2(660f, 108f));
            AddPlate(card, new Vector2(0f, 14f), new Vector2(660f, 108f));
            AddPlate(card, new Vector2(0f, -112f), new Vector2(660f, 118f));
            AddPlate(card, new Vector2(0f, -250f), new Vector2(660f, 118f));
            AddLabel(card, "menu.volume", new Vector2(0f, 278f), new Vector2(360f, 36f), 26f, Ink, font);
            AddLabel(card, "menu.sensitivity", new Vector2(0f, 160f), new Vector2(360f, 36f), 26f, Ink, font);
            AddLabel(card, "menu.resolution", new Vector2(0f, 42f), new Vector2(360f, 36f), 26f, Ink, font);
            AddLabel(card, "menu.display", new Vector2(0f, -84f), new Vector2(360f, 36f), 26f, Ink, font);
            AddLabel(card, "menu.language", new Vector2(0f, -222f), new Vector2(360f, 36f), 26f, Ink, font);
            _volumeValue = AddLabel(card, null, new Vector2(250f, 278f), new Vector2(120f, 36f), 26f, InkSoft, font, TextAlignmentOptions.Right);
            _sensitivityValue = AddLabel(card, null, new Vector2(250f, 160f), new Vector2(120f, 36f), 26f, InkSoft, font, TextAlignmentOptions.Right);
            _volumeSlider = CreateSlider(card, new Vector2(0f, 222f), 0f, 1f, GameSettings.MasterVolume);
            _sensitivitySlider = CreateSlider(card, new Vector2(0f, 104f), 0.6f, 5f, GameSettings.MouseSensitivity);
            _volumeSlider.onValueChanged.AddListener(value =>
            {
                GameSettings.SetMasterVolume(value);
                RefreshValues();
            });
            _sensitivitySlider.onValueChanged.AddListener(value =>
            {
                GameSettings.SetMouseSensitivity(value);
                RefreshValues();
            });
            BuildResolutionMenu(card, font);
            _windowed = CreateButton(card, "menu.windowed", new Vector2(-110f, -144f), new Vector2(200f, 52f), font);
            _fullscreen = CreateButton(card, "menu.fullscreen", new Vector2(110f, -144f), new Vector2(200f, 52f), font);
            Press(_windowed, () =>
            {
                GameSettings.SetFullScreen(false);
                RefreshDisplayButtons();
            });
            Press(_fullscreen, () =>
            {
                GameSettings.SetFullScreen(true);
                RefreshDisplayButtons();
            });
            _langZh = CreateButton(card, "menu.lang_zh", new Vector2(-110f, -282f), new Vector2(200f, 52f), font);
            _langEn = CreateButton(card, "menu.lang_en", new Vector2(110f, -282f), new Vector2(200f, 52f), font);
            Press(_langZh, () => GameLocale.Set(GameLocale.Chinese));
            Press(_langEn, () => GameLocale.Set(GameLocale.English));
            var back = CreateButton(card, "menu.back", new Vector2(0f, -372f), new Vector2(260f, 56f), font);
            Press(back, Close);
            RefreshValues();
            RefreshLanguageButtons();
            RefreshDisplayButtons();
        }

        public void Open()
        {
            SetResolutionList(false);
            if (_volumeSlider != null) _volumeSlider.SetValueWithoutNotify(GameSettings.MasterVolume);
            if (_sensitivitySlider != null) _sensitivitySlider.SetValueWithoutNotify(GameSettings.MouseSensitivity);
            RefreshValues();
            RefreshDisplayButtons();
            RefreshLanguageButtons();
            if (_root == null) return;
            _root.SetActive(true);
            _root.transform.SetAsLastSibling();
        }

        public void Close()
        {
            SetResolutionList(false);
            if (_root != null) _root.SetActive(false);
        }

        public bool HandleEscape()
        {
            if (!IsOpen) return false;
            if (_resolutionList != null && _resolutionList.activeSelf)
            {
                SetResolutionList(false);
                return true;
            }
            Close();
            return true;
        }

        void BuildResolutionMenu(RectTransform card, TMP_FontAsset font)
        {
            _resolutionButton = CreateButton(card, "ResolutionMenu", new Vector2(0f, -16f), new Vector2(420f, 48f), font);
            _resolutionButton.onClick.AddListener(() => SetResolutionList(_resolutionList == null || !_resolutionList.activeSelf));
            _resolutionValue = _resolutionButton.GetComponentInChildren<TMP_Text>();
            if (_resolutionValue != null)
            {
                _resolutionValue.fontSize = 24f;
                var caption = _resolutionValue.rectTransform;
                caption.offsetMin = new Vector2(18f, 0f);
                caption.offsetMax = new Vector2(-42f, 0f);
            }
            var chevron = AddLabel(_resolutionButton.transform as RectTransform, null, new Vector2(176f, 0f), new Vector2(36f, 36f), 18f, Cream, font);
            chevron.text = "\u25bc";

            var count = GameSettings.ResolutionCount;
            var row = 46f;
            var listHeight = 12f + count * row;
            var list = new GameObject("ResolutionList", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            list.transform.SetParent(card, false);
            var listRt = list.GetComponent<RectTransform>();
            listRt.anchorMin = listRt.anchorMax = new Vector2(0.5f, 0.5f);
            listRt.pivot = new Vector2(0.5f, 0.5f);
            listRt.sizeDelta = new Vector2(420f, listHeight);
            listRt.anchoredPosition = new Vector2(0f, -46f - listHeight * 0.5f);
            var listImage = list.GetComponent<Image>();
            listImage.color = Paper;
            ApplySolid(listImage);
            var outline = list.AddComponent<Outline>();
            outline.effectColor = PaperEdge;
            outline.effectDistance = new Vector2(2f, -2f);
            _resolutionList = list;
            _resolutionOptions.Clear();
            for (var i = 0; i < count; i++)
            {
                var y = listHeight * 0.5f - 6f - row * 0.5f - i * row;
                var option = CreateButton(listRt, "ResolutionOption", new Vector2(0f, y), new Vector2(396f, 42f), font);
                SetButtonText(option, GameSettings.FormatResolution(i), 22f);
                var index = i;
                option.onClick.AddListener(() =>
                {
                    GameSettings.SetResolution(index);
                    SetResolutionList(false);
                    RefreshValues();
                });
                _resolutionOptions.Add(option);
            }
            list.SetActive(false);
        }

        void SetResolutionList(bool open)
        {
            if (_resolutionList == null) return;
            _resolutionList.SetActive(open);
            if (!open) return;
            _resolutionList.transform.SetAsLastSibling();
            RefreshResolutionOptions();
        }

        void RefreshResolutionOptions()
        {
            for (var i = 0; i < _resolutionOptions.Count; i++)
                StyleChoice(_resolutionOptions[i], i == GameSettings.ResolutionIndex);
        }

        void RefreshLocale()
        {
            for (var i = 0; i < _labels.Count; i++)
            {
                var bound = _labels[i];
                if (bound.Label == null) continue;
                bound.Label.text = GameLocale.T(bound.Key);
                bound.Label.ForceMeshUpdate();
            }
            RefreshLanguageButtons();
            RefreshDisplayButtons();
        }

        void RefreshValues()
        {
            if (_volumeValue != null)
                _volumeValue.text = Mathf.RoundToInt(GameSettings.MasterVolume * 100f) + "%";
            if (_sensitivityValue != null)
                _sensitivityValue.text = GameSettings.MouseSensitivity.ToString("0.0");
            if (_resolutionValue != null)
                _resolutionValue.text = GameSettings.ResolutionText;
            RefreshDisplayButtons();
        }

        void RefreshLanguageButtons()
        {
            StyleChoice(_langZh, !GameLocale.IsEnglish);
            StyleChoice(_langEn, GameLocale.IsEnglish);
        }

        void RefreshDisplayButtons()
        {
            StyleChoice(_windowed, !GameSettings.FullScreen);
            StyleChoice(_fullscreen, GameSettings.FullScreen);
            RefreshResolutionOptions();
        }

        static void StyleChoice(Button button, bool selected)
        {
            if (button == null) return;
            var colors = button.colors;
            colors.normalColor = selected ? ButtonHighlight : ButtonNormal;
            colors.selectedColor = colors.normalColor;
            button.colors = colors;
        }

        void Press(Button button, UnityAction action)
        {
            if (button == null || action == null) return;
            var motion = button.GetComponent<MenuButtonMotion>();
            if (motion == null) motion = button.gameObject.AddComponent<MenuButtonMotion>();
            button.onClick.AddListener(() => motion.Play(action));
        }

        GameObject CreateOverlay(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Stretch(go.GetComponent<RectTransform>());
            var image = go.GetComponent<Image>();
            image.color = new Color(0.075f, 0.13f, 0.14f, 0.48f);
            image.sprite = WhiteSprite();
            image.type = Image.Type.Simple;
            image.raycastTarget = true;
            go.SetActive(false);
            return go;
        }

        static RectTransform CreatePaper(Transform parent, Vector2 size)
        {
            var go = new GameObject("Paper", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            var image = go.GetComponent<Image>();
            image.color = Paper;
            ApplySolid(image);
            var outline = go.AddComponent<Outline>();
            outline.effectColor = PaperEdge;
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = true;
            return rt;
        }

        static RectTransform AddPlate(RectTransform parent, Vector2 pos, Vector2 size)
        {
            var go = new GameObject("Row", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var image = go.GetComponent<Image>();
            image.color = Plate;
            image.raycastTarget = false;
            ApplySolid(image);
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(PaperEdge.r, PaperEdge.g, PaperEdge.b, 0.34f);
            outline.effectDistance = new Vector2(2f, -2f);
            return rt;
        }

        TMP_Text AddLabel(RectTransform parent, string key, Vector2 pos, Vector2 size, float fontSize, Color color, TMP_FontAsset font, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var label = go.GetComponent<TextMeshProUGUI>();
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = alignment;
            label.raycastTarget = false;
            label.enableWordWrapping = true;
            if (font != null) label.font = font;
            if (!string.IsNullOrEmpty(key))
            {
                label.text = GameLocale.T(key);
                _labels.Add(new BoundLabel { Label = label, Key = key });
            }
            label.ForceMeshUpdate();
            return label;
        }

        Button CreateButton(RectTransform parent, string key, Vector2 pos, Vector2 size, TMP_FontAsset font)
        {
            var go = new GameObject(key, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var image = go.GetComponent<Image>();
            image.color = Color.white;
            ApplySolid(image);
            var button = go.GetComponent<Button>();
            button.gameObject.AddComponent<MenuButtonMotion>();
            var labelGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(go.transform, false);
            Stretch(labelGo.GetComponent<RectTransform>());
            var label = labelGo.GetComponent<TextMeshProUGUI>();
            label.fontSize = 26f;
            label.color = Cream;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            if (font != null) label.font = font;
            if (!string.IsNullOrEmpty(key) && key.StartsWith("menu."))
            {
                label.text = GameLocale.T(key);
                _labels.Add(new BoundLabel { Label = label, Key = key });
            }
            label.ForceMeshUpdate();
            StyleButton(button, label);
            return button;
        }

        static void SetButtonText(Button button, string text, float fontSize)
        {
            if (button == null) return;
            var label = button.GetComponentInChildren<TMP_Text>();
            if (label == null) return;
            label.text = text;
            label.fontSize = fontSize;
            label.ForceMeshUpdate();
        }

        static void StyleButton(Button button, TMP_Text label)
        {
            if (button == null) return;
            var image = button.targetGraphic as Image;
            if (image == null) image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = Color.white;
                var buttonSprite = UiFontCatalog.LoadButtonSprite();
                if (buttonSprite != null)
                {
                    image.sprite = buttonSprite;
                    image.type = Image.Type.Sliced;
                }
                button.targetGraphic = image;
            }
            var colors = button.colors;
            colors.normalColor = ButtonNormal;
            colors.highlightedColor = ButtonHighlight;
            colors.pressedColor = ButtonPressed;
            colors.selectedColor = ButtonSelected;
            colors.disabledColor = ButtonDisabled;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.1f;
            button.colors = colors;
            if (label != null) label.color = Cream;
        }

        static Slider CreateSlider(RectTransform parent, Vector2 pos, float min, float max, float value)
        {
            var go = new GameObject("Slider", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(600f, 28f);

            var background = CreateImage(go.transform, "Background", new Color(0.76f, 0.80f, 0.69f, 1f));
            Stretch(background.rectTransform);

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(go.transform, false);
            var fillAreaRt = fillArea.GetComponent<RectTransform>();
            Stretch(fillAreaRt);
            fillAreaRt.offsetMin = new Vector2(6f, 5f);
            fillAreaRt.offsetMax = new Vector2(-6f, -5f);
            var fill = CreateImage(fillArea.transform, "Fill", ButtonNormal);
            Stretch(fill.rectTransform);

            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(go.transform, false);
            var handleAreaRt = handleArea.GetComponent<RectTransform>();
            Stretch(handleAreaRt);
            handleAreaRt.offsetMin = new Vector2(10f, 0f);
            handleAreaRt.offsetMax = new Vector2(-10f, 0f);
            var handle = CreateImage(handleArea.transform, "Handle", ButtonNormal);
            handle.rectTransform.sizeDelta = new Vector2(22f, 36f);

            var slider = go.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;
            return slider;
        }

        static Image CreateImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            ApplySolid(image);
            return image;
        }

        static bool ContainsPointer(RectTransform rect)
        {
            if (rect == null || !rect.gameObject.activeInHierarchy) return false;
            return RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, null);
        }

        static void Stretch(RectTransform rt)
        {
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        static void ApplySolid(Image image)
        {
            if (image == null) return;
            image.sprite = WhiteSprite();
            image.type = Image.Type.Simple;
        }

        static Sprite WhiteSprite()
        {
            if (_white != null) return _white;
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            tex.hideFlags = HideFlags.HideAndDontSave;
            _white = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            _white.hideFlags = HideFlags.HideAndDontSave;
            return _white;
        }
    }
}
