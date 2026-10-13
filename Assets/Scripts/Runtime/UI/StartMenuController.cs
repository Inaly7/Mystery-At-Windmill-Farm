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
        Image _titleImage;
        [SerializeField] Sprite _titleChinese;
        [SerializeField] Sprite _titleEnglish;
        SettingsPanel _settingsPanel;
        GameObject _creditsRoot;
        GameObject _confirmRoot;
        bool _pressLock;
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
            _settingsPanel = gameObject.AddComponent<SettingsPanel>();
            _settingsPanel.Build(transform);
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
            if (!Input.GetKeyDown(KeyCode.Escape)) return;
            if (_settingsPanel != null && _settingsPanel.HandleEscape()) return;
            if (_confirmRoot != null && _confirmRoot.activeSelf) SetOpen(_confirmRoot, false);
            else if (_creditsRoot != null && _creditsRoot.activeSelf) SetOpen(_creditsRoot, false);
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
            Press(_settings, () => { if (_settingsPanel != null) _settingsPanel.Open(); });
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

        void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
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
