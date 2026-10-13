using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EggRescue
{
    public sealed class GameBootstrap : MonoBehaviour
    {
        public static GameBootstrap Instance { get; private set; }
        public Transform playerSpawn;
        public GameObject playerPrefab;
        bool _booted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoBoot()
        {
            if (SceneManager.GetActiveScene().name == GameSession.MenuSceneName) return;
            if (FindObjectOfType<GameBootstrap>() != null) return;
            var go = new GameObject("EggRescue_Runtime");
            go.AddComponent<GameBootstrap>();
        }

        void Awake()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Boot();
        }

        public void Boot()
        {
            if (_booted) return;
            _booted = true;
            if (IsArtWalkScene())
            {
                EnsureArtWalk();
                return;
            }
            GameSettings.Load();
            GameState.LoadDefaults();
            NpcRegistry.Load();
            DialogueDatabase.LoadAll();
            CheeseRegistry.ClearPicked();
            InteractionPointVfx.ClearDiscovered();
            ClimbPathPoint.ResetPaths();
            SaveService.ClearPose();
            if (GameSession.ShouldLoadSave())
                SaveService.Load();
            GameSession.Consume();
            EnsureAudio();
            EnsureDialogue();
            EnsurePlayer();
            EnsureSystems();
            AutoWireWorld();
            DisableDouyinRuntime();
        }

        void Update()
        {
            if (IsArtWalkScene())
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                    GameEvents.SetPaused(!GameEvents.Paused);
                return;
            }
            if (Input.GetKeyDown(KeyCode.F5)) SaveService.Save();
            if (Input.GetKeyDown(KeyCode.F9)) SaveService.Load();
        }

        /// <summary>
        /// 美术预览场景只保留走路和第三人称镜头，方便录视频。
        /// </summary>
        static bool IsArtWalkScene()
        {
            var name = SceneManager.GetActiveScene().name;
            return name.StartsWith("ArtTest");
        }

        void EnsureArtWalk()
        {
            GameObject player = null;
            if (PlayerController.Instance != null)
                player = PlayerController.Instance.gameObject;
            else
            {
                var existing = FindObjectOfType<PlayerController>();
                if (existing != null) player = existing.gameObject;
            }
            if (player == null)
                player = SpawnPlayer();
            player.tag = "Player";
            player.layer = 2;
            var interaction = player.GetComponent<PlayerInteraction>();
            if (interaction != null) interaction.enabled = false;
            BindCameraToPlayer(player);
        }

        void EnsureAudio()
        {
            if (AudioDirector.Instance != null) return;
            gameObject.AddComponent<AudioDirector>();
        }

        void EnsureDialogue()
        {
            if (DialogueManager.Instance != null) return;
            var existing = GameObject.Find("DialogueManager");
            if (existing != null)
            {
                var dm = existing.GetComponent<DialogueManager>();
                if (dm == null) existing.AddComponent<DialogueManager>();
                return;
            }
            var go = new GameObject("DialogueManager");
            go.AddComponent<DialogueManager>();
        }

        void EnsurePlayer()
        {
            if (PlayerController.Instance != null) return;
            var player = SpawnPlayer();
            player.tag = "Player";
            player.layer = 2;
            BindCameraToPlayer(player);
            ApplySavedPose(player);
            CreatePrompt(player.GetComponent<PlayerInteraction>());
        }

        GameObject SpawnPlayer()
        {
            Vector3 pos;
            Quaternion rot;
            ResolveSpawn(out pos, out rot);
            GameObject player;
            if (playerPrefab != null)
            {
                player = Instantiate(playerPrefab, pos, rot);
            }
            else
            {
                player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                player.name = "Player";
                var col = player.GetComponent<Collider>();
                if (col != null) col.enabled = false;
                var cc = player.AddComponent<CharacterController>();
                cc.height = 1.8f;
                cc.radius = 0.4f;
                cc.center = new Vector3(0f, 0.9f, 0f);
                player.transform.SetPositionAndRotation(pos, rot);
            }
            if (player.GetComponent<PlayerController>() == null)
                player.AddComponent<PlayerController>();
            if (player.GetComponent<PlayerInteraction>() == null)
                player.AddComponent<PlayerInteraction>();
            return player;
        }

        static void ApplySavedPose(GameObject player)
        {
            if (!SaveService.HasPose || player == null) return;
            var controller = player.GetComponent<PlayerController>();
            if (controller != null)
                controller.Teleport(SaveService.PosePosition, SaveService.PoseYaw);
            else
                player.transform.SetPositionAndRotation(SaveService.PosePosition, Quaternion.Euler(0f, SaveService.PoseYaw, 0f));
            if (ThirdPersonCamera.Instance != null)
                ThirdPersonCamera.Instance.SetView(SaveService.PoseViewYaw, SaveService.PosePitch);
        }

        static void BindCameraToPlayer(GameObject player)
        {
            var cam = ResolveMainCamera();
            if (cam == null) return;
            DisableConflictingCameraDrivers(cam.gameObject);
            var tps = cam.GetComponent<ThirdPersonCamera>();
            if (tps == null) tps = cam.gameObject.AddComponent<ThirdPersonCamera>();
            var pc = player.GetComponent<PlayerController>();
            var follow = pc != null && pc.CameraPivot != null ? pc.CameraPivot : player.transform;
            tps.SetTarget(follow);
            tps.SnapToTarget();
        }

        /// <summary>
        /// 场景相机上若残留 demo 脚本(OrbitMotion / TMP CameraController 等)，
        /// 它们会和 ThirdPersonCamera 争抢同一个 transform，表现为画面抖动。
        /// </summary>
        static void DisableConflictingCameraDrivers(GameObject camGo)
        {
            var behaviours = camGo.GetComponents<MonoBehaviour>();
            for (var i = 0; i < behaviours.Length; i++)
            {
                var mb = behaviours[i];
                if (mb == null) continue;
                var n = mb.GetType().Name;
                if (n == "ThirdPersonCamera") continue;
                if (n == "OrbitMotion" || n == "CameraController" || n == "SmoothFollow"
                    || n == "CameraFollow" || n == "CinemachineBrain")
                {
                    mb.enabled = false;
                    Debug.Log("[GameBootstrap] disabled conflicting camera driver: " + n);
                }
            }
        }

        static Camera ResolveMainCamera()
        {
            var cam = Camera.main;
            if (cam != null) return cam;
            var named = GameObject.Find("Main Camera");
            if (named != null)
            {
                cam = named.GetComponent<Camera>();
                if (cam != null)
                {
                    named.tag = "MainCamera";
                    return cam;
                }
            }
            var all = Object.FindObjectsOfType<Camera>(true);
            for (var i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].enabled)
                    return all[i];
            }
            return all != null && all.Length > 0 ? all[0] : null;
        }

        void ResolveSpawn(out Vector3 pos, out Quaternion rot)
        {
            if (playerSpawn == null)
            {
                var named = GameObject.Find("PlayerSpawn");
                if (named != null) playerSpawn = named.transform;
            }
            if (playerSpawn != null)
            {
                pos = playerSpawn.position;
                rot = FlattenYaw(playerSpawn.rotation);
                return;
            }
            var shufen = InteractionUtil.FindByName("淑芬1") ?? InteractionUtil.FindByName("淑芬");
            if (shufen != null)
            {
                var away = shufen.transform.forward;
                away.y = 0f;
                if (away.sqrMagnitude < 0.01f) away = Vector3.forward;
                else away.Normalize();
                pos = shufen.transform.position + away * 2.2f + Vector3.up * 0.05f;
                rot = FlattenYaw(Quaternion.LookRotation(-away, Vector3.up));
                return;
            }
            var cam = Camera.main;
            if (cam != null)
            {
                pos = cam.transform.position;
                rot = FlattenYaw(cam.transform.rotation);
                return;
            }
            pos = Vector3.up * 0.05f;
            rot = Quaternion.identity;
        }

        static Quaternion FlattenYaw(Quaternion rotation)
        {
            var forward = rotation * Vector3.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) return Quaternion.identity;
            return Quaternion.LookRotation(forward.normalized, Vector3.up);
        }

        void CreatePrompt(PlayerInteraction interaction)
        {
            var canvasGo = new GameObject("InteractionPromptCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();
            var textGo = new GameObject("Prompt");
            textGo.transform.SetParent(canvasGo.transform, false);
            var text = textGo.AddComponent<TextMeshProUGUI>();
            var font = UiFontCatalog.Load();
            if (font != null) text.font = font;
            text.fontSize = 28;
            text.alignment = TextAlignmentOptions.Bottom;
            text.color = Color.white;
            text.raycastTarget = false;
            var rt = text.rectTransform;
            rt.anchorMin = new Vector2(0.3f, 0.08f);
            rt.anchorMax = new Vector2(0.7f, 0.16f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            text.gameObject.SetActive(false);
            if (interaction != null) interaction.SetPromptLabel(text);
        }

        void EnsureSystems()
        {
            if (GetComponent<PauseMenu>() == null)
                gameObject.AddComponent<PauseMenu>();
            if (FindObjectOfType<MouseBrotherShop>() == null)
                gameObject.AddComponent<MouseBrotherShop>();
            if (FindObjectOfType<CheeseRefreshManager>() == null)
                gameObject.AddComponent<CheeseRefreshManager>();
            var notebook = GameObject.Find("Notebook");
            if (notebook != null && notebook.GetComponent<BookController>() == null)
                notebook.AddComponent<BookController>();
            if (notebook != null)
            {
                var hud = notebook.GetComponent<CheeseHud>();
                if (hud == null) hud = notebook.AddComponent<CheeseHud>();
                if (hud.countText == null)
                    hud.countText = CheeseHud.FindCountText(notebook.transform);
            }
            var ending = notebook != null ? notebook.transform.Find("Ending") : null;
            if (ending != null && ending.GetComponent<EndingController>() == null)
                ending.gameObject.AddComponent<EndingController>();
            WireCheeseSpawner();
        }

        void AutoWireWorld()
        {
            WireNpc("淑芬1", "淑芬", 0);
            WireNpc("淑芬2", "淑芬", 0);
            WireNpc("淑芬3", "淑芬", 0);
            WireNpc("淑芬", "淑芬", 0);
            WireNpc("大黄", "大黄", 0);
            WireNpc("大黄 2", "大黄", 0);
            WireNpc("黑猫", "黑猫", 0);
            WireNpc("悲伤蛙", "悲伤蛙", 0);
            WireNpc("悲伤蛙2", "悲伤蛙", 0);
            WireNpc("乌鸦", "乌鸦", 0);
            WireNpc("小鸡侦探团", "小鸡侦探团", 0);
            WireNpc("闪电蜗牛", "闪电蜗牛", 0);
            WireNpc("老鼠兄弟", "老鼠兄弟", 0);
            WireNpc("大树", "大树", 0);
            AddIfMissing<DaHuangController>(GameObject.Find("大黄"));
            AddIfMissing<DaHuangController>(GameObject.Find("大黄 2"));
            var shufenRoot = FindShuFenRoot();
            AddIfMissing<ShuFenController>(shufenRoot);
            AddIfMissing<BeiShangWaController>(FindFrogRoot());
            AddIfMissing<BlackCatInteractionController>(GameObject.Find("黑猫"));
            AddIfMissing<CrowInteractionController>(GameObject.Find("乌鸦"));
            AddIfMissing<E03EavesdropController>(FindNamedStartsWith("E03"));
            AddIfMissing<E05GrainSoakController>(FindNamedStartsWith("E05"));
            AddIfMissing<E06LadderController>(FindNamedStartsWith("E06 ·"));
            AddIfMissing<ComicGateTrigger>(FindNamedStartsWith("E20"));
            var interactionRoot = GameObject.Find("InteractionPoint");
            if (interactionRoot != null)
                AddIfMissing<SecondFloorWindowController>(interactionRoot);
            WireClues();
            WireAreaTriggers();
            var tree = FindNamedContains("TreeDialogue");
            AddIfMissing<TreeInteractionController>(tree);
            if (FindObjectOfType<LevelTeleport>() == null)
            {
                var spawn = GameObject.Find("PlayerSpawn");
                var lt = gameObject.AddComponent<LevelTeleport>();
                if (spawn != null) lt.targetObject = spawn.transform;
            }
        }

        static void WireClues()
        {
            WireClueAndDesc("E01", "E01_ViewCharcoal", 1);
            WireClueAndDesc("E02", "E02_ViewFeather", 2);
            WireClueAndDesc("E05", "E05_GrainSoakGet", 90);
            WireClueAndDesc("E07", "E07_ViewNapSpot", 14);
            WireClueAndDesc("E08", "E08_ViewBurnMark", 12);
            WireClueAndDesc("E09", "E09_AnimalPawPrints", 16);
            WireClueAndDesc("E10", "E10_ViewWhiteStone", 19);
            WireClueAndDesc("E12", "E12_ViewGreenPad", 3);
            WireClueAndDesc("E13", "E13_ViewDoorBlocked", 30);
            WireClueAndDesc("E14", "E14_ViewCatDoor", 24);
            WireClueAndDesc("E15", "E15_ViewFoodBowl", 27);
            WireClueAndDesc("E16", "E16_ViewFur", 29);
            WireClueAndDesc("E17", "E17_ViewEmptyBucket", 21);
            WireClueAndDesc("E18", "E18_ViewBootprints", 23);
            WireClueAndDesc("E23", "E23_dabble", 7);
            WireClueAndDesc("E25", "E25_ChickenFootprints", 5);
            WireClueAndDesc("E27", "E27_ColorReflective", 17);
            WireClueAndDesc("E28", "E28_ViewTreeScratch", 26);
            WireClueAndDesc("E34", "E34_Glass", 38);
            WireDesc("E11", 36);
            WireDesc("E21", 55);
            WireDesc("E22", 320);
            WireDesc("E24", 47);
            WireDesc("E26", 48);
            WireDesc("E29", 58);
            WireDesc("E31", 49);
            WireDesc("E32", 51);
            WireDesc("E33", 52);
            var e03 = FindNamedStartsWith("E03");
            if (e03 != null)
            {
                WireNpc(e03.name, "E03_Eavesdrop", 0);
                AddIfMissing<E03EavesdropController>(e03);
            }
        }

        static void WireDesc(string token, int miaosuId)
        {
            var go = FindNamedStartsWith(token);
            if (go == null) return;
            var dt = go.GetComponent<DialogueTrigger>();
            if (dt == null) dt = go.AddComponent<DialogueTrigger>();
            if (string.IsNullOrEmpty(dt.npcName))
            {
                dt.npcName = "描述";
                dt.startId = miaosuId;
            }
            if (go.GetComponent<Interactable>() == null)
                go.AddComponent<Interactable>();
        }

        static void WireAreaTriggers()
        {
            WireArea(FindNamedContains("TreeForceZone"), "大树", 1, null, null);
            WireArea(FindNamedStartsWith("E39"), "老鼠兄弟", 1, null, "Mouse_AreaCalloutShown");
            WireArea(FindNamedStartsWith("E35"), "乌鸦", 5, "E06_LadderPlaced", "E35_CrowTauntShown");
            WireArea(FindNamedStartsWith("E36"), "乌鸦", 7, "E06_LadderPlaced", "E36_CrowTauntShown");
            WireArea(FindNamedStartsWith("E37"), "黑猫", 170, "BlackCat_Entered", "E37_BlackCatTauntShown");
            WireArea(FindNamedStartsWith("E38"), "黑猫", 180, "BlackCat_Entered", "E38_BlackCatTauntShown");
        }

        static void WireArea(GameObject go, string npc, int startId, string requireVar, string blockVar)
        {
            if (go == null) return;
            var area = go.GetComponent<DialogueAreaTrigger>();
            if (area == null) area = go.AddComponent<DialogueAreaTrigger>();
            if (string.IsNullOrEmpty(area.npcName))
            {
                area.npcName = npc;
                area.startNodeId = startId;
                if (!string.IsNullOrEmpty(requireVar))
                {
                    area.requireVarName = requireVar;
                    area.requireVarMustBe = true;
                }
                if (!string.IsNullOrEmpty(blockVar))
                {
                    area.blockVarName = blockVar;
                    area.blockWhenTrue = true;
                }
            }
            var col = go.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        static void WireCheeseSpawner()
        {
            var root = GameObject.Find("奶酪散点");
            if (root == null) return;
            var spawner = root.GetComponent<CheeseSpawner>();
            if (spawner == null) spawner = root.AddComponent<CheeseSpawner>();
            if (spawner.cheesePrefab == null)
                spawner.cheesePrefab = Resources.Load<GameObject>("CheesePickup");
        }

        static GameObject FindShuFenRoot()
        {
            var existing = FindObjectOfType<ShuFenController>(true);
            if (existing != null) return existing.gameObject;

            var commission = InteractionUtil.FindByName("淑芬1") ?? InteractionUtil.FindByName("淑芬");
            var hub = InteractionUtil.FindByName("淑芬2") ?? InteractionUtil.FindByName("淑芬 2");
            var ngPlus = InteractionUtil.FindByName("淑芬3") ?? InteractionUtil.FindByName("淑芬 3");
            if (commission == null) return null;

            var parent = commission.transform.parent;
            if (IsDedicatedShuFenParent(parent, commission, hub, ngPlus))
                return parent.gameObject;

            // 场景里三只淑芬目前是 characters 的平级子物体；运行时收到专用 parent 上，对齐 Lua。
            var root = new GameObject("淑芬");
            if (parent != null)
                root.transform.SetParent(parent, false);
            ReparentKeepWorld(commission, root.transform);
            ReparentKeepWorld(hub, root.transform);
            ReparentKeepWorld(ngPlus, root.transform);
            return root;
        }

        static bool IsDedicatedShuFenParent(Transform parent, GameObject commission, GameObject hub, GameObject ngPlus)
        {
            if (parent == null) return false;
            if (parent.name == "characters") return false;
            if (parent.name != "淑芬" && parent.name != "ShuFen" && !parent.name.Contains("淑芬"))
                return false;
            if (hub != null && hub.transform.parent != parent) return false;
            if (ngPlus != null && ngPlus.transform.parent != parent) return false;
            return true;
        }

        static void ReparentKeepWorld(GameObject go, Transform parent)
        {
            if (go == null || parent == null) return;
            go.transform.SetParent(parent, true);
        }

        static GameObject FindFrogRoot()
        {
            var a = GameObject.Find("悲伤蛙");
            if (a != null && a.transform.parent != null) return a.transform.parent.gameObject;
            return a;
        }

        static void WireClueAndDesc(string token, string varName, int miaosuId)
        {
            var go = FindNamedContains(token);
            if (go == null) return;
            var clue = go.GetComponent<ClueTrigger>();
            if (clue == null) clue = go.AddComponent<ClueTrigger>();
            if (string.IsNullOrEmpty(clue.varName1))
            {
                clue.varName1 = varName;
                clue.varType1 = "bool";
                clue.varValue1 = true;
            }
            var dt = go.GetComponent<DialogueTrigger>();
            if (dt == null) dt = go.AddComponent<DialogueTrigger>();
            if (string.IsNullOrEmpty(dt.npcName))
            {
                dt.npcName = "描述";
                dt.startId = miaosuId;
            }
            if (go.GetComponent<Interactable>() == null)
                go.AddComponent<Interactable>();
            if (go.GetComponent<InteractionPointVfx>() == null)
                go.AddComponent<InteractionPointVfx>();
        }

        static void WireNpc(string goName, string npcName, int startId)
        {
            var go = InteractionUtil.FindByName(goName);
            if (go == null) return;
            if (go.GetComponent<ShuFenController>() != null) return;
            if (goName == "淑芬" && InteractionUtil.FindChildOrWorld(go.transform, "淑芬1") != null)
                return;
            var trigger = go.GetComponent<DialogueTrigger>();
            if (trigger == null) trigger = go.AddComponent<DialogueTrigger>();
            if (string.IsNullOrEmpty(trigger.npcName))
            {
                trigger.npcName = npcName;
                trigger.startId = startId;
            }
            if (go.GetComponent<Interactable>() == null)
                go.AddComponent<Interactable>();
        }

        static void AddIfMissing<T>(GameObject go) where T : Component
        {
            if (go == null) return;
            if (go.GetComponent<T>() == null) go.AddComponent<T>();
        }

        static GameObject FindNamedStartsWith(string token)
        {
            GameObject starts = null;
            GameObject contains = null;
            var all = FindObjectsOfType<Transform>(true);
            for (var i = 0; i < all.Length; i++)
            {
                var n = all[i].name;
                if (n == token) return all[i].gameObject;
                if (starts == null && n.StartsWith(token)) starts = all[i].gameObject;
                if (contains == null && n.Contains(token)) contains = all[i].gameObject;
            }
            return starts != null ? starts : contains;
        }

        static GameObject FindNamedContains(string token)
        {
            return FindNamedStartsWith(token);
        }

        static void DisableDouyinRuntime()
        {
            var behaviours = FindObjectsOfType<MonoBehaviour>(true);
            var n = 0;
            for (var i = 0; i < behaviours.Length; i++)
            {
                var mb = behaviours[i];
                if (mb == null) continue;
                if (mb.GetType().Name != "DouyinScript") continue;
                mb.enabled = false;
                n++;
            }
            if (n > 0)
                Debug.Log("[GameBootstrap] disabled DouyinScript x" + n);
        }
    }
}
