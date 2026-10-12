using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EggRescue
{
    /// <summary>
    /// 主菜单：开始新游戏、继续存档、音量、灵敏度、分辨率与窗口/全屏、退出。进入 Mechanics_Code。
    /// </summary>
    public sealed class StartMenuController : MonoBehaviour
    {
        // 明亮的农场插画配色：奶油白、鼠尾草绿、暖橙色。
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
        static Sprite _panelSprite;
        static bool _panelSliced;

        public const string TitleChinesePath = "Assets/Res/Sprites/StartUi/title_final_ch.png";
        public const string TitleEnglishPath = "Assets/Res/Sprites/StartUi/title_final_en.png";

        Button _start;
        Button _load;
        Button _settings;
        Button _credits;
        Button _quit;
        Button _langZh;
        Button _langEn;
        Button _windowed;
        Button _fullscreen;
        Button _resolutionButton;
        TMP_Text _resolutionValue;
        GameObject _resolutionList;
        readonly List<Button> _resolutionOptions = new List<Button>();
        Image _titleImage;
        [SerializeField] Sprite _titleChinese;
        [SerializeField] Sprite _titleEnglish;
        GameObject _settingsRoot;
        GameObject _creditsRoot;
        GameObject _confirmRoot;
        bool _pressLock;
        Slider _volumeSlider;
        Slider _sensitivitySlider;
        TMP_Text _volumeValue;
        TMP_Text _sensitivityValue;
        bool _entering;
        MenuButtonColumn _column;
        readonly List<BoundLabel> _labels = new List<BoundLabel>();

        sealed class BoundLabel
        {
            public TMP_Text Label;
            public string Key;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureOnMenu()
        {
            if (SceneManager.GetActiveScene().name != GameSession.MenuSceneName) return;
            if (FindObjectOfType<StartMenuController>() != null) return;
            var canvas = GameObject.Find("Canvas");
            if (canvas == null) return;
            canvas.AddComponent<StartMenuController>();
        }

        void Awake()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            GameSettings.Load();
            CachePanelSprite();
            var font = ResolveFont();
            BindSceneButtons();
            FitMenuColumn();
            BuildSettings(font);
            BuildCredits(font);
            BuildConfirm(font);
            Wire();
            BindTitle();
            RefreshSaveState();
        }

        void OnEnable()
        {
            GameLocale.Changed += RefreshLocale;
        }

        void OnDisable()
        {
            GameLocale.Changed -= RefreshLocale;
        }

        void Start()
        {
            FitMenuColumn();
        }

        void Update()
        {
            if (_resolutionList != null && _resolutionList.activeSelf && Input.GetMouseButtonDown(0))
            {
                var overList = ContainsPointer(_resolutionList.transform as RectTransform);
                var overButton = ContainsPointer(_resolutionButton != null ? _resolutionButton.transform as RectTransform : null);
                if (!overList && !overButton) SetResolutionList(false);
            }
            if (!Input.GetKeyDown(KeyCode.Escape)) return;
            if (_resolutionList != null && _resolutionList.activeSelf)
            {
                SetResolutionList(false);
                return;
            }
            if (_confirmRoot != null && _confirmRoot.activeSelf) SetOpen(_confirmRoot, false);
            else if (_creditsRoot != null && _creditsRoot.activeSelf) SetOpen(_creditsRoot, false);
            else if (_settingsRoot != null && _settingsRoot.activeSelf) SetOpen(_settingsRoot, false);
        }

        void BindSceneButtons()
        {
            _column = FindObjectOfType<MenuButtonColumn>();
            _start = BindSceneButton("StartButton", "menu.start");
            _load = BindSceneButton("LoadButton", "menu.continue");
            _settings = BindSceneButton("SettingButton", "menu.settings");
            _credits = BindSceneButton("CreditsButton", "menu.credits");
            _quit = BindSceneButton("QuitButton", "menu.quit");
        }

        void FitMenuColumn()
        {
            if (_column == null) _column = FindObjectOfType<MenuButtonColumn>();
            if (_column == null) return;
            _column.BeginLocalizedFit();
            FitMenuButton(_start, "menu.start");
            FitMenuButton(_load, "menu.continue");
            FitMenuButton(_settings, "menu.settings");
            FitMenuButton(_credits, "menu.credits");
            FitMenuButton(_quit, "menu.quit");
            _column.EndLocalizedFit();
        }

        void FitMenuButton(Button button, string key)
        {
            if (button == null || _column == null) return;
            var reference = LocaleCatalog.Ui(key, false);
            if (string.IsNullOrEmpty(reference) || reference == key) return;
            _column.FitButton(button, reference);
        }

        Button BindSceneButton(string name, string key)
        {
            var go = GameObject.Find(name);
            if (go == null) return null;
            var label = go.GetComponentInChildren<TMP_Text>();
            if (label != null) Bind(label, key);
            return go.GetComponent<Button>();
        }

        void Wire()
        {
            Press(_start, OnStart);
            Press(_load, OnContinue);
            Press(_settings, OpenSettings);
            Press(_credits, OpenCredits);
            Press(_quit, QuitGame);
        }

        void Press(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null || action == null) return;
            var motion = button.GetComponent<MenuButtonMotion>();
            if (motion == null) motion = button.gameObject.AddComponent<MenuButtonMotion>();
            button.onClick.AddListener(() =>
            {
                if (_pressLock || _entering) return;
                _pressLock = true;
                motion.Play(() =>
                {
                    _pressLock = false;
                    action();
                });
            });
        }

        void RefreshSaveState()
        {
            var hasSave = SaveService.Exists();
            if (_load != null) _load.interactable = hasSave;
        }

        void OnStart()
        {
            if (SaveService.Exists())
            {
                SetOpen(_confirmRoot, true);
                return;
            }
            EnterGame(GameSession.Mode.NewGame);
        }

        void ConfirmNewGame()
        {
            if (!SaveService.WriteNewGame()) return;
            EnterGame(GameSession.Mode.NewGame);
        }

        void OnContinue()
        {
            if (!SaveService.Exists())
            {
                RefreshSaveState();
                return;
            }
            EnterGame(GameSession.Mode.Continue);
        }

        void EnterGame(GameSession.Mode mode)
        {
            if (_entering) return;
            if (!Application.CanStreamedLevelBeLoaded(GameSession.GameSceneName))
            {
                SetOpen(_confirmRoot, false);
                Debug.LogError("[StartMenu] Build Settings 缺少 " + GameSession.GameSceneName);
                return;
            }
            _entering = true;
            GameSession.Pending = mode;
            SceneManager.LoadScene(GameSession.GameSceneName);
        }

        void OpenSettings()
        {
            SetResolutionList(false);
            if (_volumeSlider != null) _volumeSlider.SetValueWithoutNotify(GameSettings.MasterVolume);
            if (_sensitivitySlider != null) _sensitivitySlider.SetValueWithoutNotify(GameSettings.MouseSensitivity);
            RefreshSettingLabels();
            RefreshDisplayButtons();
            SetOpen(_settingsRoot, true);
        }

        void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void BuildSettings(TMP_FontAsset font)
        {
            _settingsRoot = CreateOverlay("SettingsOverlay");
            var card = CreatePaper(_settingsRoot.transform, new Vector2(760f, 820f), true);
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
                RefreshSettingLabels();
            });
            _sensitivitySlider.onValueChanged.AddListener(value =>
            {
                GameSettings.SetMouseSensitivity(value);
                RefreshSettingLabels();
            });
            BuildResolutionMenu(card, font);
            _windowed = CreateButton(card, "menu.windowed", new Vector2(-110f, -144f), new Vector2(200f, 52f), font, true);
            _fullscreen = CreateButton(card, "menu.fullscreen", new Vector2(110f, -144f), new Vector2(200f, 52f), font, true);
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
            _langZh = CreateButton(card, "menu.lang_zh", new Vector2(-110f, -282f), new Vector2(200f, 52f), font, true);
            _langEn = CreateButton(card, "menu.lang_en", new Vector2(110f, -282f), new Vector2(200f, 52f), font, true);
            Press(_langZh, () => GameLocale.Set(GameLocale.Chinese));
            Press(_langEn, () => GameLocale.Set(GameLocale.English));
            var back = CreateButton(card, "menu.back", new Vector2(0f, -372f), new Vector2(260f, 56f), font, true);
            Press(back, () => SetOpen(_settingsRoot, false));
            RefreshSettingLabels();
            RefreshLanguageButtons();
            RefreshDisplayButtons();
        }

        void BuildResolutionMenu(RectTransform card, TMP_FontAsset font)
        {
            _resolutionButton = CreateButton(card, "ResolutionMenu", new Vector2(0f, -16f), new Vector2(420f, 48f), font, true);
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
                var option = CreateButton(listRt, "ResolutionOption", new Vector2(0f, y), new Vector2(396f, 42f), font, true);
                SetButtonText(option, GameSettings.FormatResolution(i), 22f);
                var index = i;
                option.onClick.AddListener(() =>
                {
                    GameSettings.SetResolution(index);
                    SetResolutionList(false);
                    RefreshSettingLabels();
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
                StyleLanguageButton(_resolutionOptions[i], i == GameSettings.ResolutionIndex);
        }

        static bool ContainsPointer(RectTransform rect)
        {
            if (rect == null || !rect.gameObject.activeInHierarchy) return false;
            return RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, null);
        }

        void BuildCredits(TMP_FontAsset font)
        {
            _creditsRoot = CreateOverlay("CreditsOverlay");
            var card = CreatePaper(_creditsRoot.transform, new Vector2(720f, 620f), true);
            AddLabel(card, "menu.credits", new Vector2(0f, 230f), new Vector2(560f, 72f), 44f, Ink, font);
            var body = AddLabel(card, "menu.credits_body", new Vector2(0f, 20f), new Vector2(560f, 320f), 30f, InkSoft, font);
            body.lineSpacing = 16f;
            var back = CreateButton(card, "menu.back", new Vector2(0f, -230f), new Vector2(260f, 60f), font, true);
            Press(back, () => SetOpen(_creditsRoot, false));
        }

        void OpenCredits()
        {
            SetOpen(_creditsRoot, true);
        }

        void BuildConfirm(TMP_FontAsset font)
        {
            _confirmRoot = CreateOverlay("ConfirmOverlay");
            var card = CreatePaper(_confirmRoot.transform, new Vector2(640f, 360f), true);
            AddLabel(card, "menu.confirm_title", new Vector2(0f, 90f), new Vector2(520f, 56f), 36f, Ink, font);
            AddLabel(card, "menu.confirm_body", new Vector2(0f, 20f), new Vector2(520f, 72f), 22f, InkSoft, font);
            var ok = CreateButton(card, "menu.confirm_ok", new Vector2(-120f, -100f), new Vector2(180f, 56f), font, true);
            var cancel = CreateButton(card, "menu.confirm_cancel", new Vector2(120f, -100f), new Vector2(180f, 56f), font, true);
            Press(ok, ConfirmNewGame);
            Press(cancel, () => SetOpen(_confirmRoot, false));
        }

        void RefreshSettingLabels()
        {
            if (_volumeValue != null)
                _volumeValue.text = Mathf.RoundToInt(GameSettings.MasterVolume * 100f) + "%";
            if (_sensitivityValue != null)
                _sensitivityValue.text = GameSettings.MouseSensitivity.ToString("0.0");
            if (_resolutionValue != null)
                _resolutionValue.text = GameSettings.ResolutionText;
            RefreshDisplayButtons();
        }

        void SetOpen(GameObject panel, bool open)
        {
            if (panel == null) return;
            panel.SetActive(open);
            if (open) panel.transform.SetAsLastSibling();
        }

        GameObject CreateOverlay(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(transform, false);
            Stretch(go.GetComponent<RectTransform>());
            var image = go.GetComponent<Image>();
            image.color = new Color(0.075f, 0.13f, 0.14f, 0.48f);
            image.sprite = WhiteSprite();
            image.type = Image.Type.Simple;
            go.SetActive(false);
            return go;
        }

        static RectTransform CreatePaper(Transform parent, Vector2 size, bool solid = false)
        {
            var go = new GameObject("Paper", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            var image = go.GetComponent<Image>();
            image.color = Paper;
            if (solid) ApplySolid(image);
            else ApplyPanelSprite(image);
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
            Bind(label, key);
            label.ForceMeshUpdate();
            return label;
        }

        Button CreateButton(RectTransform parent, string key, Vector2 pos, Vector2 size, TMP_FontAsset font, bool solid = false)
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
            if (solid) ApplySolid(image);
            else ApplyPanelSprite(image);
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
                Bind(label, key);
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

        static void StyleButton(Button button, TMP_Text label = null)
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

            if (label == null) label = button.GetComponentInChildren<TMP_Text>();
            if (label != null)
            {
                label.color = Cream;
            }
        }

        void Bind(TMP_Text label, string key)
        {
            if (label == null || string.IsNullOrEmpty(key)) return;
            label.text = GameLocale.T(key);
            label.ForceMeshUpdate();
            _labels.Add(new BoundLabel { Label = label, Key = key });
        }

        void RefreshLocale()
        {
            for (var i = 0; i < _labels.Count; i++)
            {
                var bound = _labels[i];
                if (bound.Label != null)
                {
                    bound.Label.text = GameLocale.T(bound.Key);
                    bound.Label.ForceMeshUpdate();
                }
            }
            RefreshSaveState();
            RefreshLanguageButtons();
            RefreshDisplayButtons();
            ApplyTitleSprite();
            FitMenuColumn();
        }

        void BindTitle()
        {
            var title = transform.Find("Title");
            if (title == null)
            {
                var go = GameObject.Find("Title");
                if (go != null) title = go.transform;
            }
            _titleImage = title != null ? title.GetComponent<Image>() : null;
#if UNITY_EDITOR
            if (_titleChinese == null)
                _titleChinese = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(TitleChinesePath);
            if (_titleEnglish == null)
                _titleEnglish = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(TitleEnglishPath);
#endif
            ApplyTitleSprite();
        }

        void ApplyTitleSprite()
        {
            if (_titleImage == null) return;
            var sprite = GameLocale.IsEnglish ? _titleEnglish : _titleChinese;
            if (sprite == null) return;
            if (_titleImage.sprite != sprite) _titleImage.sprite = sprite;
        }

        void RefreshLanguageButtons()
        {
            StyleLanguageButton(_langZh, !GameLocale.IsEnglish);
            StyleLanguageButton(_langEn, GameLocale.IsEnglish);
        }

        void RefreshDisplayButtons()
        {
            StyleLanguageButton(_windowed, !GameSettings.FullScreen);
            StyleLanguageButton(_fullscreen, GameSettings.FullScreen);
            RefreshResolutionOptions();
        }

        static void StyleLanguageButton(Button button, bool selected)
        {
            if (button == null) return;
            var colors = button.colors;
            colors.normalColor = selected ? ButtonHighlight : ButtonNormal;
            colors.selectedColor = colors.normalColor;
            button.colors = colors;
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

            var background = CreateImage(go.transform, "Background", new Color(0.76f, 0.80f, 0.69f, 1f), true);
            Stretch(background.rectTransform);

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(go.transform, false);
            var fillAreaRt = fillArea.GetComponent<RectTransform>();
            Stretch(fillAreaRt);
            fillAreaRt.offsetMin = new Vector2(6f, 5f);
            fillAreaRt.offsetMax = new Vector2(-6f, -5f);
            var fill = CreateImage(fillArea.transform, "Fill", ButtonNormal, true);
            Stretch(fill.rectTransform);

            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(go.transform, false);
            var handleAreaRt = handleArea.GetComponent<RectTransform>();
            Stretch(handleAreaRt);
            handleAreaRt.offsetMin = new Vector2(10f, 0f);
            handleAreaRt.offsetMax = new Vector2(-10f, 0f);
            var handle = CreateImage(handleArea.transform, "Handle", ButtonNormal, true);
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

        static Image CreateImage(Transform parent, string name, Color color, bool solid = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            if (solid) ApplySolid(image);
            else ApplyPanelSprite(image);
            return image;
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

        void CachePanelSprite()
        {
            var images = GetComponentsInChildren<Image>(true);
            for (var i = 0; i < images.Length; i++)
            {
                var sprite = images[i] != null ? images[i].sprite : null;
                if (sprite == null) continue;
                _panelSprite = sprite;
                _panelSliced = sprite.border.sqrMagnitude > 0.01f;
                return;
            }
            _panelSprite = WhiteSprite();
            _panelSliced = false;
        }

        static void ApplyPanelSprite(Image image)
        {
            if (image == null) return;
            image.sprite = _panelSprite != null ? _panelSprite : WhiteSprite();
            image.type = _panelSliced ? Image.Type.Sliced : Image.Type.Simple;
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

        static TMP_FontAsset ResolveFont()
        {
            return UiFontCatalog.Load();
        }
    }
}
