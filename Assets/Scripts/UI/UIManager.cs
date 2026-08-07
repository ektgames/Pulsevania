using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Pulsevania.Core
{
    // Helper component to detect touch hold for mobile movement
    public class MobileHoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public bool IsPressed { get; private set; }

        public void OnPointerDown(PointerEventData eventData)
        {
            IsPressed = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            IsPressed = false;
        }

        private void OnDisable()
        {
            IsPressed = false;
        }
    }

    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [Header("Tooltip Settings")]
        public GameObject tooltipPanel;
        public Text tooltipText;

        [Header("UI Panels")]
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private GameObject gameplayHUD;
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private GameObject levelCompletePanel;

        [Header("Shop Settings")]
        [SerializeField] private GameObject shopPanel;
        [SerializeField] private Text shopGoldText;
        [SerializeField] private Text hpUpgradeText;
        [SerializeField] private Text atkUpgradeText;
        [SerializeField] private Button btnOpenShop;
        [SerializeField] private Button btnCloseShop;
        [SerializeField] private Button btnUpgradeHP;
        [SerializeField] private Button btnUpgradeATK;

        [Header("HUD Elements")]
        [SerializeField] private Text hpText;
        [SerializeField] private Slider healthSlider;
        [SerializeField] private Text healthPercentText;
        [SerializeField] private HorizontalLayoutGroup extraHeartsContainer;
        [SerializeField] private Text goldText;
        [SerializeField] private Text keysText;
        [SerializeField] private Text potionsText;

        [Header("Mini-Map System")]
        [SerializeField] private Image minimapCellLeft;
        [SerializeField] private Image minimapCellMid;
        [SerializeField] private Image minimapCellRight;
        private Image[] minimapCells = new Image[50];

        [Header("Mobile Overlay Buttons")]
        [SerializeField] private MobileHoldButton btnLeft;
        [SerializeField] private MobileHoldButton btnRight;
        [SerializeField] private MobileHoldButton btnUp;
        [SerializeField] private MobileHoldButton btnDown;
        private Pulsevania.UI.VirtualJoystick virtualJoystick;
        [SerializeField] private Button btnJump;
        [SerializeField] private Button btnAttack;
        [SerializeField] private Button btnBlock; // Hold to block
        [SerializeField] private Button btnShoot;
        [SerializeField] private Button btnUsePotion;
        [SerializeField] private Button btnKnife;

        private System.Collections.Generic.Dictionary<ItemData, int> shopCart = new System.Collections.Generic.Dictionary<ItemData, int>();
        private bool isCartMode = false;
        private GameObject shopCartGridGo;
        private Text btnCartText;
        private Text btnCheckoutText;
        private GameObject btnCartGo;
        private GameObject btnCheckoutGo;

        private PlayerController activePlayer;
        private MobileHoldButton blockHoldButton;
        private GameObject settingsPanelGo;
        private GameObject saveLoadPanelGo;
        private bool isSavingMode = false;
        private GameObject loadingPanelGo;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.Log("UIManager Awake: Duplicate UIManager detected. Destroying duplicate.");
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Load saved language
            string savedLang = PlayerPrefs.GetString("GameLanguage", "Turkish");
            if (savedLang == "English") currentLanguage = GameLanguage.English;
            else currentLanguage = GameLanguage.Turkish;
        }

        private void OnEnable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            Debug.Log($"OnSceneLoaded: Scene '{scene.name}' loaded (buildIndex: {scene.buildIndex})");
            RebindSceneReferences();
        }

        private void Start()
        {
            // Subscribe to GameManager state changes
            GameManager.OnStateChanged += HandleStateChanged;
            GameManager.OnGoldChanged += UpdateGoldUI;
            GameManager.OnKeysChanged += UpdateKeysUI;
            GameManager.OnPotionsChanged += UpdatePotionsUI;
            GameManager.OnPlayerSpawned += LinkPlayerReferences;

            RebindSceneReferences();
            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.SyncHUDPotions();
            }

            UpdateLocalizedTexts();

            // Start EKT REKLAM intro splash sequence on startup
            StartIntroSequence();
        }

        private void SetupPanelButtonCallbacks()
        {
            // Main Menu
            BindButton(mainMenuPanel, "PlayButton", PlayButtonAction);
            BindButton(mainMenuPanel, "QuitButton", QuitButtonAction);

            // HUD Pause
            BindButton(gameplayHUD, "PauseButton", PauseButtonAction);

            // Pause Menu
            BindButton(pausePanel, "ResumeButton", ResumeButtonAction);
            BindButton(pausePanel, "RestartButton", RestartButtonAction);
            BindButton(pausePanel, "MainMenuButton", MainMenuButtonAction);

            // Game Over
            EnsureGameOverPanelButtons();

            // Level Complete
            BindButton(levelCompletePanel, "NextLevelButton", () => GameManager.Instance.LoadNextLevel());
            BindButton(levelCompletePanel, "MainMenuButton", MainMenuButtonAction);

            // Shop Buttons
            BindButton(mainMenuPanel, "ShopButton", OpenShopAction);
            BindButton(shopPanel, "CloseShopButton", CloseShopAction);
            BindButton(shopPanel, "UpgradeHPButton", UpgradeHPAction);
            BindButton(shopPanel, "UpgradeATKButton", UpgradeATKAction);
        }

        public void CloseGameOverPanel()
        {
            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(false);
            }
        }

        public void SavepointButtonAction()
        {
            if (GameManager.Instance != null)
            {
                int nearestSavepoint = PlayerPrefs.GetInt("ActiveSavepointRoomId", 10);

                // Kural 2: Savepoint'te canlanmak için ödüllü reklam izlemeyi zorunlu kıl
                if (Pulsevania.Core.AdManager.Instance != null)
                {
                    Pulsevania.Core.AdManager.Instance.ShowRewardedAd(() =>
                    {
                        // Reklam başarıyla tamamlandı, savepoint'te doğ
                        GameManager.Instance.LoadNearestSavepoint(nearestSavepoint);
                    }, () =>
                    {
                        // Reklam izlenmedi veya kapandı
                        Debug.Log("[UIManager] Savepoint rewarded ad not completed.");

                        // Show warning that the ad is not ready or failed
                        bool isTR = PlayerPrefs.GetString("GameLanguage", "Turkish") == "Turkish";
                        GameObject player = GameObject.FindWithTag("Player");
                        Vector3 spawnPos = player != null ? player.transform.position + Vector3.up * 1.5f : Vector3.zero;
                        if (DamageTextPool.Instance != null)
                        {
                            DamageTextPool.Instance.SpawnText(spawnPos, isTR ? "Reklam Hazır Değil, Lütfen Tekrar Deneyin!" : "Ad Not Ready, Please Try Again!", Color.red);
                        }
                    });
                }
                else
                {
                    // Fallback
                    GameManager.Instance.LoadNearestSavepoint(nearestSavepoint);
                }
            }
        }

        public void GameOverRestartButtonAction()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.NewGame();
            }
        }

        private void EnsureGameOverPanelButtons()
        {
            if (gameOverPanel == null) return;

            // Find or reposition title
            Transform title = gameOverPanel.transform.Find("GameOverTitle");
            if (title != null)
            {
                title.localPosition = new Vector3(0f, 150f, 0f);
            }

            // Find or create SavepointButton by duplicating RestartButton
            Transform savepointBtn = gameOverPanel.transform.Find("SavepointButton");
            if (savepointBtn == null)
            {
                Transform restartBtn = gameOverPanel.transform.Find("RestartButton");
                if (restartBtn != null)
                {
                    GameObject newBtn = Instantiate(restartBtn.gameObject, gameOverPanel.transform);
                    newBtn.name = "SavepointButton";
                    savepointBtn = newBtn.transform;
                }
            }

            // Find or create QuitButton by duplicating MainMenuButton
            Transform quitBtn = gameOverPanel.transform.Find("QuitButton");
            if (quitBtn == null)
            {
                Transform menuBtn = gameOverPanel.transform.Find("MainMenuButton");
                if (menuBtn != null)
                {
                    GameObject newBtn = Instantiate(menuBtn.gameObject, gameOverPanel.transform);
                    newBtn.name = "QuitButton";
                    quitBtn = newBtn.transform;
                }
            }

            bool isTR = PlayerPrefs.GetString("GameLanguage", "Turkish") == "Turkish";
            int activeSavepoint = PlayerPrefs.GetInt("ActiveSavepointRoomId", 0);
            bool hasSavepoint = activeSavepoint >= 10;

            Transform restart = gameOverPanel.transform.Find("RestartButton");
            Transform oldMenu = gameOverPanel.transform.Find("MainMenuButton");

            // Show/Hide and Position based on savepoint status
            if (hasSavepoint)
            {
                if (savepointBtn != null)
                {
                    savepointBtn.gameObject.SetActive(true);
                    savepointBtn.localPosition = new Vector3(0f, 60f, 0f);
                }
                if (restart != null)
                {
                    restart.gameObject.SetActive(false);
                }
                if (oldMenu != null)
                {
                    oldMenu.gameObject.SetActive(true);
                    oldMenu.localPosition = new Vector3(0f, -20f, 0f);
                }
                if (quitBtn != null)
                {
                    quitBtn.gameObject.SetActive(true);
                    quitBtn.localPosition = new Vector3(0f, -100f, 0f);
                }
            }
            else
            {
                if (savepointBtn != null)
                {
                    savepointBtn.gameObject.SetActive(false);
                }
                if (restart != null)
                {
                    restart.gameObject.SetActive(true);
                    restart.localPosition = new Vector3(0f, 10f, 0f);
                }
                if (oldMenu != null)
                {
                    oldMenu.gameObject.SetActive(false);
                }
                if (quitBtn != null)
                {
                    quitBtn.gameObject.SetActive(true);
                    quitBtn.localPosition = new Vector3(0f, -70f, 0f);
                }
            }

            // Set default localized text
            if (savepointBtn != null)
            {
                Text t = savepointBtn.GetComponentInChildren<Text>();
                if (t != null)
                {
                    t.text = isTR ? "REKLAM İZLE VE SAVEPOINT'TE DOĞ" : "WATCH AD & SPAWN AT SAVEPOINT";
                }
            }
            if (restart != null)
            {
                Text t = restart.GetComponentInChildren<Text>();
                if (t != null) t.text = isTR ? "YENİDEN BAŞLA (MAP 1)" : "RESTART RUN (MAP 1)";
            }
            if (oldMenu != null)
            {
                Text t = oldMenu.GetComponentInChildren<Text>();
                if (t != null) t.text = isTR ? "ANA MENÜ" : "MAIN MENU";
            }
            if (quitBtn != null)
            {
                Text t = quitBtn.GetComponentInChildren<Text>();
                if (t != null) t.text = isTR ? "ÇIKIŞ" : "QUIT GAME";
            }

            // Bind button actions
            BindButton(gameOverPanel, "SavepointButton", SavepointButtonAction);
            BindButton(gameOverPanel, "RestartButton", GameOverRestartButtonAction);
            BindButton(gameOverPanel, "MainMenuButton", MainMenuButtonAction);
            BindButton(gameOverPanel, "QuitButton", QuitButtonAction);
        }

        private void BindButton(GameObject panel, string buttonName, UnityEngine.Events.UnityAction action)
        {
            if (panel == null) return;
            Transform btnTrans = panel.transform.Find(buttonName);
            if (btnTrans != null)
            {
                Button btn = btnTrans.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(action);
                }
            }
        }

        private void OnDestroy()
        {
            GameManager.OnStateChanged -= HandleStateChanged;
            GameManager.OnGoldChanged -= UpdateGoldUI;
            GameManager.OnKeysChanged -= UpdateKeysUI;
            GameManager.OnPotionsChanged -= UpdatePotionsUI;
            GameManager.OnPlayerSpawned -= LinkPlayerReferences;
        }

        private void Update()
        {
            if (merchantShopPanelGo != null && merchantShopPanelGo.activeSelf)
            {
                UpdateGoldKazanButtonText();
            }

            if (isInRescueDialogue)
            {
                bool nextPressed = false;
                if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.eKey.wasPressedThisFrame)
                {
                    nextPressed = true;
                }
                if (UnityEngine.InputSystem.Pointer.current != null && UnityEngine.InputSystem.Pointer.current.press.wasPressedThisFrame)
                {
                    nextPressed = true;
                }

                if (nextPressed)
                {
                    ShowNextRescueDialogueLine();
                }
                return;
            }

            if (fullWorldMapPanelGo == null || !fullWorldMapPanelGo.activeSelf)
            {
                var mask1 = GameObject.Find("WorldMapPanel");
                if (mask1 != null)
                {
                    var img = mask1.GetComponent<UnityEngine.UI.Image>();
                    if (img != null) { img.enabled = false; img.raycastTarget = false; }
                    mask1.SetActive(false);
                }
                var mask2 = GameObject.Find("BlackMask");
                if (mask2 != null)
                {
                    var img = mask2.GetComponent<UnityEngine.UI.Image>();
                    if (img != null) { img.enabled = false; img.raycastTarget = false; }
                    mask2.SetActive(false);
                }
                var mask3 = GameObject.Find("BlockerPanel");
                if (mask3 != null)
                {
                    var img = mask3.GetComponent<UnityEngine.UI.Image>();
                    if (img != null) { img.enabled = false; img.raycastTarget = false; }
                    mask3.SetActive(false);
                }
            }

            if (activePlayer == null) return;

            // Handle hold-button input or Joystick processing in Update
            float hInput = 0f;
            float vInput = 0f;

            if (virtualJoystick != null && virtualJoystick.gameObject.activeInHierarchy)
            {
                hInput = virtualJoystick.Horizontal;
                vInput = virtualJoystick.Vertical;
            }
            else
            {
                if (btnLeft != null && btnLeft.IsPressed) hInput -= 1f;
                if (btnRight != null && btnRight.IsPressed) hInput += 1f;
                if (btnUp != null && btnUp.IsPressed) vInput += 1f;
                if (btnDown != null && btnDown.IsPressed) vInput -= 1f;
            }

            activePlayer.SetHorizontalInput(hInput);
            activePlayer.SetVerticalInput(vInput);

        }

        private void UpdateMinimap()
        {
            RefreshMapUI();
            if (fullWorldMapPanelGo != null && fullWorldMapPanelGo.activeSelf)
            {
                RefreshFullWorldMapUI();
            }
        }

        public void RefreshMapUI()
        {
            if (MapManager.Instance == null) return;

            int activeRoomId = MapManager.Instance.GetCurrentRoomId();

            for (int i = 0; i < 50; i++)
            {
                if (minimapCells[i] == null) continue;

                var room = MapManager.Instance.rooms[i];
                if (i == activeRoomId - 1)
                {
                    minimapCells[i].color = new Color(1f, 0.5f, 0f, 1f);
                }
                else if (room.state == RoomState.Discovered || room.state == RoomState.Cleared)
                {
                    minimapCells[i].color = new Color(0f, 1f, 0f, 1f);
                }
                else
                {
                    minimapCells[i].color = new Color(0f, 0f, 0f, 0f);
                }
            }
        }

        private void EnsureMinimapUI(GameObject gameplayHUDGo)
        {
            Transform minimapT = gameplayHUDGo.transform.Find("Minimap");
            if (minimapT == null) return;

            // Destroy legacy cell components
            var layout = minimapT.GetComponent<HorizontalLayoutGroup>();
            if (layout != null) Destroy(layout);

            foreach (Transform child in minimapT)
            {
                if (child.name.StartsWith("Cell") || child.name.Contains("Layout") || child.name.Contains("RoomCell"))
                {
                    Destroy(child.gameObject);
                }
            }

            Image mmBg = minimapT.GetComponent<Image>();
            if (mmBg == null) mmBg = minimapT.gameObject.AddComponent<Image>();
            mmBg.color = new Color(0.05f, 0.05f, 0.08f, 0.75f);

            Outline mmOutline = minimapT.GetComponent<Outline>();
            if (mmOutline == null) mmOutline = minimapT.gameObject.AddComponent<Outline>();
            mmOutline.effectColor = new Color(1f, 1f, 1f, 0.35f);
            mmOutline.effectDistance = new Vector2(1.5f, 1.5f);

            GridLayoutGroup grid = minimapT.GetComponent<GridLayoutGroup>();
            if (grid == null) grid = minimapT.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(16f, 12f);
            grid.spacing = new Vector2(2f, 2f);
            grid.padding = new RectOffset(6, 6, 6, 6);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 10;
            grid.childAlignment = TextAnchor.MiddleCenter;

            RectTransform rect = minimapT.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(192f, 82f);

            // Anchored to top-center
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -15f);

            // Spawn 50 cells
            for (int i = 0; i < 50; i++)
            {
                GameObject cellGo = new GameObject($"RoomCell_{i}");
                cellGo.transform.SetParent(minimapT, false);
                Image img = cellGo.AddComponent<Image>();
                img.color = new Color(0.12f, 0.12f, 0.15f, 0.6f);
                minimapCells[i] = img;
            }

            Button mapBtn = minimapT.GetComponent<Button>();
            if (mapBtn == null) mapBtn = minimapT.gameObject.AddComponent<Button>();
            mapBtn.onClick.RemoveAllListeners();
            mapBtn.onClick.AddListener(() => {
                ToggleFullWorldMapPanel();
            });
        }

        public void RebindSceneReferences()
        {
            Debug.Log($"RebindSceneReferences: Rebinding UI references. GameManager State: {(GameManager.Instance != null ? GameManager.Instance.CurrentState.ToString() : "NULL")}");
            GameObject canvas = GameObject.Find("Canvas");
            if (canvas != null)
            {
                mainMenuPanel = canvas.transform.Find("MainMenuPanel")?.gameObject;
                gameplayHUD = canvas.transform.Find("GameplayHUD")?.gameObject;
                pausePanel = canvas.transform.Find("PausePanel")?.gameObject;
                gameOverPanel = canvas.transform.Find("GameOverPanel")?.gameObject;
                EnsureGameOverPanelButtons();
                levelCompletePanel = canvas.transform.Find("LevelCompletePanel")?.gameObject;

                // Texts
                hpText = canvas.transform.Find("GameplayHUD/HPText")?.GetComponent<Text>();
                if (gameplayHUD != null)
                {
                    EnsureHealthBarUI(gameplayHUD);
                    EnsureHUDStatsContainer(gameplayHUD);
                }
                else
                {
                    healthSlider = canvas.transform.Find("GameplayHUD/HealthBar")?.GetComponent<Slider>();
                    healthPercentText = canvas.transform.Find("GameplayHUD/HealthBar/PercentText")?.GetComponent<Text>();
                    extraHeartsContainer = canvas.transform.Find("GameplayHUD/ExtraHeartsContainer")?.GetComponent<HorizontalLayoutGroup>();
                    goldText = canvas.transform.Find("GameplayHUD/GoldText")?.GetComponent<Text>();
                    keysText = canvas.transform.Find("GameplayHUD/KeysText")?.GetComponent<Text>();
                    potionsText = canvas.transform.Find("GameplayHUD/PotionsText")?.GetComponent<Text>();
                }

                // Minimap Cells
                GameObject hud = canvas.transform.Find("GameplayHUD")?.gameObject;
                if (hud != null)
                {
                    EnsureMinimapUI(hud);
                }

                // Hold Buttons
                btnLeft = canvas.transform.Find("GameplayHUD/BtnLeft")?.GetComponent<MobileHoldButton>();
                btnRight = canvas.transform.Find("GameplayHUD/BtnRight")?.GetComponent<MobileHoldButton>();
                btnUp = canvas.transform.Find("GameplayHUD/BtnUp")?.GetComponent<MobileHoldButton>();
                btnDown = canvas.transform.Find("GameplayHUD/BtnDown")?.GetComponent<MobileHoldButton>();

                // Normal Buttons
                btnJump = canvas.transform.Find("GameplayHUD/BtnJump")?.GetComponent<Button>();
                btnAttack = canvas.transform.Find("GameplayHUD/BtnAttack")?.GetComponent<Button>();
                btnBlock = canvas.transform.Find("GameplayHUD/BtnBlock")?.GetComponent<Button>();
                btnShoot = canvas.transform.Find("GameplayHUD/BtnShoot")?.GetComponent<Button>();
                if (btnShoot != null) btnShoot.gameObject.SetActive(false); // Disable archery arrows
                btnUsePotion = canvas.transform.Find("GameplayHUD/BtnPotion")?.GetComponent<Button>();

                Transform btnKnifeTrans = canvas.transform.Find("GameplayHUD/BtnKnife");
                if (btnKnifeTrans != null)
                {
                    btnKnife = btnKnifeTrans.GetComponent<Button>();
                }
                else
                {
                    // Create BtnKnife programmatically
                    Transform hudTrans = canvas.transform.Find("GameplayHUD");
                    if (hudTrans != null)
                    {
                        GameObject knifeGo = new GameObject("BtnKnife");
                        knifeGo.transform.SetParent(hudTrans, false);
                        
                        Image jumpImg = btnJump != null ? btnJump.GetComponent<Image>() : null;
                        Image kImg = knifeGo.AddComponent<Image>();
                        if (jumpImg != null)
                        {
                            kImg.sprite = jumpImg.sprite;
                            kImg.type = jumpImg.type;
                            kImg.color = jumpImg.color;
                        }
                        else
                        {
                            kImg.color = new Color(1f, 1f, 1f, 0.4f);
                        }

                        btnKnife = knifeGo.AddComponent<Button>();

                        GameObject textGo = new GameObject("Text");
                        textGo.transform.SetParent(knifeGo.transform, false);
                        RectTransform rtText = textGo.AddComponent<RectTransform>();
                        rtText.anchorMin = Vector2.zero;
                        rtText.anchorMax = Vector2.one;
                        rtText.sizeDelta = Vector2.zero;

                        Text txt = textGo.AddComponent<Text>();
                        txt.text = "KNIFE";
                        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                        txt.fontSize = 24;
                        txt.fontStyle = FontStyle.Bold;
                        txt.alignment = TextAnchor.MiddleCenter;
                        txt.color = Color.white;
                    }
                }

                // Disable global shop button from MainMenu
                Transform oldShopBtn = canvas.transform.Find("MainMenuPanel/ShopButton");
                if (oldShopBtn != null) oldShopBtn.gameObject.SetActive(false);

                // Shop Buttons & Texts
                btnOpenShop = canvas.transform.Find("MainMenuPanel/ShopButton")?.GetComponent<Button>();
                btnCloseShop = canvas.transform.Find("ShopPanel/CloseShopButton")?.GetComponent<Button>();
                btnUpgradeHP = canvas.transform.Find("ShopPanel/UpgradeHPButton")?.GetComponent<Button>();
                btnUpgradeATK = canvas.transform.Find("ShopPanel/UpgradeATKButton")?.GetComponent<Button>();
                shopGoldText = canvas.transform.Find("ShopPanel/ShopGoldText")?.GetComponent<Text>();
                hpUpgradeText = canvas.transform.Find("ShopPanel/HPUpgradeText")?.GetComponent<Text>();
                atkUpgradeText = canvas.transform.Find("ShopPanel/ATKUpgradeText")?.GetComponent<Text>();
                
                CreateInventoryUI();
            }

            // Link player references
            LinkPlayerReferences();

            // Re-setup callbacks
            SetupMobileButtonCallbacks();
            SetupPanelButtonCallbacks();

            // Setup programmatically aligned menu, pause, and settings panels
            EnsureMainMenuButtons();
            EnsurePausePanelButtons();
            EnsureSettingsAndSaveLoadUI();
            EnsureErgonomicMobileHUD();

            // Sync UI state
            HandleStateChanged(GameManager.Instance.CurrentState);
            if (GameManager.Instance.CurrentState == GameState.MainMenu)
            {
                ApplyMainMenuBackground();
            }
            UpdateGoldUI(GameManager.Instance.CurrentGold);
            UpdateKeysUI(GameManager.Instance.CurrentKeys);
            UpdatePotionsUI(GameManager.Instance.CurrentPotions);
        }

        private GameObject FindObjectInChild(string name)
        {
            Transform t = transform.Find(name);
            return t != null ? t.gameObject : null;
        }

        private Text GetComponentInChildrenText(string name)
        {
            Text[] texts = GetComponentsInChildren<Text>(true);
            foreach (var t in texts)
            {
                if (t.gameObject.name == name) return t;
            }
            return null;
        }

        private void SetupMobileButtonCallbacks()
        {
            if (btnJump != null)
            {
                btnJump.onClick.RemoveAllListeners();
                btnJump.onClick.AddListener(() => activePlayer?.TriggerJump());
            }
            if (btnAttack != null)
            {
                btnAttack.onClick.RemoveAllListeners();
                btnAttack.onClick.AddListener(() => activePlayer?.TriggerAttack());
            }
            if (btnShoot != null)
            {
                btnShoot.onClick.RemoveAllListeners();
                btnShoot.onClick.AddListener(() => activePlayer?.TriggerRanged());
            }
            if (btnUsePotion != null)
            {
                btnUsePotion.onClick.RemoveAllListeners();
                btnUsePotion.onClick.AddListener(UsePotionFromHUD);
            }
            if (btnKnife != null)
            {
                btnKnife.onClick.RemoveAllListeners();
                btnKnife.onClick.AddListener(() => activePlayer?.TriggerRanged());
            }

            if (btnBlock != null)
            {
                blockHoldButton = btnBlock.gameObject.GetComponent<MobileHoldButton>();
                if (blockHoldButton == null)
                {
                    blockHoldButton = btnBlock.gameObject.AddComponent<MobileHoldButton>();
                }
            }
        }

        private void LinkPlayerReferences()
        {
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.MainMenu)
            {
                activePlayer = null;
                return;
            }
            activePlayer = FindFirstObjectByType<PlayerController>();
            if (activePlayer == null)
            {
                activePlayer = SpawnPlayerCharacter();
            }

            if (activePlayer != null)
            {
                if (GameManager.pendingLoadData != null)
                {
                    GameManager.ApplySaveData(GameManager.pendingLoadData);
                    GameManager.pendingLoadData = null;
                }
                else if (GameManager.isNewGameSpawning)
                {
                    GameManager.isNewGameSpawning = false;
                    if (MapManager.Instance != null)
                    {
                        MapManager.Instance.InitializeRooms();
                        MapManager.Instance.SetActiveRoom(1);
                    }
                    UpdateHealthUI(activePlayer.currentHP, activePlayer.maxHP);
                    UpdateExtraHeartsUI(activePlayer.extraHearts);
                }
                else
                {
                    UpdateHealthUI(activePlayer.currentHP, activePlayer.maxHP);
                    UpdateExtraHeartsUI(activePlayer.extraHearts);
                }

                if (InventoryManager.Instance != null)
                {
                    InventoryManager.Instance.LocatePlayerVisuals();
                    InventoryManager.Instance.SyncHUDPotions();
                }

                HideLoadingScreen();
                RefreshMapUI();
            }
        }

        private PlayerController SpawnPlayerCharacter()
        {
            GameObject playerGo = new GameObject("Player");
            playerGo.tag = "Player";
            playerGo.transform.position = new Vector3(-78f, 16f, 0f);

            var rb = playerGo.AddComponent<Rigidbody2D>();
            rb.gravityScale = 2f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var hs = playerGo.AddComponent<HealthSystem>();
            hs.SetMaxHealth(100);

            var dmg = playerGo.AddComponent<Damageable>();
            dmg.Team = Team.Player;

            var col = playerGo.AddComponent<BoxCollider2D>();
            col.size = new Vector2(0.8f, 1.6f);

            GameObject sensorGo = new GameObject("GroundCheckSensor");
            sensorGo.transform.SetParent(playerGo.transform, false);
            sensorGo.transform.localPosition = new Vector3(0f, -0.9f, 0f);
            
            var sensorCol = sensorGo.AddComponent<BoxCollider2D>();
            sensorCol.size = new Vector2(0.5f, 0.1f);
            sensorCol.isTrigger = true;
            sensorGo.AddComponent<GroundSensorComponent>();

            var sr = playerGo.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 50;

            Texture2D tex = new Texture2D(16, 16);
            for (int x = 0; x < 16; x++)
            {
                for (int y = 0; y < 16; y++)
                {
                    bool isBody = (x > 3 && x < 12 && y > 1 && y < 15);
                    tex.SetPixel(x, y, isBody ? Color.blue : Color.clear);
                }
            }
            tex.filterMode = FilterMode.Point;
            tex.Apply();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16f);

            GameObject gc = new GameObject("GroundCheck");
            gc.transform.SetParent(playerGo.transform, false);
            gc.transform.localPosition = new Vector3(0f, -0.8f, 0f);

            GameObject wp = new GameObject("Visual_Weapon");
            wp.transform.SetParent(playerGo.transform, false);
            wp.transform.localPosition = new Vector3(0.5f, 0f, 0f);
            var wpSr = wp.AddComponent<SpriteRenderer>();
            wpSr.sortingOrder = 52;

            return playerGo.AddComponent<PlayerController>();
        }

        private void UsePotionFromHUD()
        {
            if (activePlayer != null && activePlayer.currentHP < activePlayer.maxHP)
            {
                if (InventoryManager.Instance != null)
                {
                    int potIndex = InventoryManager.Instance.inventoryItems.FindIndex(x => x.itemName == "Health Potion (Can Potu)");
                    if (potIndex >= 0)
                    {
                        InventoryManager.Instance.UseConsumable(potIndex);
                    }
                    else
                    {
                        if (DamageTextPool.Instance != null)
                        {
                            DamageTextPool.Instance.SpawnText(activePlayer.transform.position, "Pot Yok!", Color.red);
                        }
                    }
                }
            }
        }

        // --- UI UPDATERS ---

        public void UpdateHealthUI(float current, float max)
        {
            if (healthSlider != null)
            {
                healthSlider.maxValue = max;
                healthSlider.value = current;
            }
            if (healthPercentText != null)
            {
                healthPercentText.text = Mathf.CeilToInt(current) + "%";
            }
        }

        private void UpdateHealthUI(int current, int max)
        {
            UpdateHealthUI((float)current, (float)max);
        }

        private void UpdateGoldUI(int amount)
        {
            if (goldText != null)
            {
                goldText.text = amount.ToString();
            }
        }

        private void UpdateKeysUI(int amount)
        {
            if (keysText != null)
            {
                keysText.text = amount.ToString();
            }
        }

        public void UpdatePotionsUI(int amount)
        {
            if (potionsText != null)
            {
                potionsText.text = amount.ToString();
            }
        }

        private void HandleStateChanged(GameState state)
        {
            // Panel states toggles
            if (mainMenuPanel != null) mainMenuPanel.SetActive(state == GameState.MainMenu && isIntroPlayed);
            if (gameplayHUD != null) gameplayHUD.SetActive(state == GameState.Gameplay);
            if (pausePanel != null) pausePanel.SetActive(state == GameState.Paused);
            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(state == GameState.GameOver);
                if (state == GameState.GameOver)
                {
                    EnsureGameOverPanelButtons();
                    StartCoroutine(GameOverFadeRoutine());
                }
            }
            if (levelCompletePanel != null) levelCompletePanel.SetActive(state == GameState.LevelComplete);
            if (shopPanel != null) shopPanel.SetActive(false);
        }

        private IEnumerator GameOverFadeRoutine()
        {
            if (gameOverPanel == null) yield break;
            CanvasGroup cg = gameOverPanel.GetComponent<CanvasGroup>();
            if (cg == null)
            {
                cg = gameOverPanel.AddComponent<CanvasGroup>();
            }

            cg.alpha = 0f;
            float elapsed = 0f;
            float duration = 1.5f;

            while (elapsed < duration)
            {
                if (cg == null) yield break;
                elapsed += Time.unscaledDeltaTime;
                cg.alpha = Mathf.Clamp01(elapsed / duration);
                yield return null;
            }
            if (cg != null) cg.alpha = 1f;
        }

        // --- BUTTON HANDLERS FOR SCREEN PANELS ---

        public void PlayButtonAction()
        {
            int nextIndex = 1;
            if (nextIndex < UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings)
            {
                GameManager.Instance.LoadLevel(nextIndex);
            }
            else
            {
                GameManager.Instance.UpdateState(GameState.Gameplay);
            }
        }

        public void PauseButtonAction()
        {
            if (GameManager.Instance.CurrentState == GameState.Gameplay)
            {
                GameManager.Instance.UpdateState(GameState.Paused);
            }
        }

        public void ResumeButtonAction()
        {
            if (GameManager.Instance.CurrentState == GameState.Paused)
            {
                GameManager.Instance.UpdateState(GameState.Gameplay);
            }
        }

        public void RestartButtonAction()
        {
            GameManager.Instance.RestartLevel();
        }

        public void MainMenuButtonAction()
        {
            GameManager.Instance.UpdateState(GameState.MainMenu);
            Time.timeScale = 1f;

            if (UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings <= 1)
            {
                // Single scene mode: just switch UI state without reloading the scene to prevent singleton conflicts
                HandleStateChanged(GameState.MainMenu);
                if (mainMenuPanel != null)
                {
                    ApplyMainMenuBackground();
                    mainMenuPanel.SetActive(true);
                }
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(0);
            }
        }

        public void QuitButtonAction()
        {
            #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
            #else
                Application.Quit();
            #endif
        }

        // --- SHOP UPGRADES AND PANELS ---

        public void OpenShopAction()
        {
            if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
            if (shopPanel != null) shopPanel.SetActive(true);
            UpdateShopUI();
        }

        public void CloseShopAction()
        {
            if (shopPanel != null) shopPanel.SetActive(false);
            if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        }

        private void UpdateShopUI()
        {
            int currentGold = PlayerPrefs.GetInt("Pulsevania_TotalGold", 0);
            int hpUpgrade = PlayerPrefs.GetInt("Pulsevania_MaxHPUpgrade", 0);
            int atkUpgrade = PlayerPrefs.GetInt("Pulsevania_ATKUpgrade", 0);

            int hpCost = 50 * (hpUpgrade + 1);
            int atkCost = 75 * (atkUpgrade + 1);

            bool isTR = currentLanguage == GameLanguage.Turkish;
            if (shopGoldText != null) shopGoldText.text = isTR ? $"Toplam Altın: {currentGold}" : $"Total Gold: {currentGold}";
            if (hpUpgradeText != null) hpUpgradeText.text = isTR 
                ? $"Maksimum Can: {3 + hpUpgrade}\nYükseltme Bedeli: {hpCost} Altın" 
                : $"Max HP Hearts: {3 + hpUpgrade}\nUpgrade Cost: {hpCost} G";
            if (atkUpgradeText != null) atkUpgradeText.text = isTR 
                ? $"Saldırı Hasarı: {1 + atkUpgrade}\nYükseltme Bedeli: {atkCost} Altın" 
                : $"Melee Damage: {1 + atkUpgrade}\nUpgrade Cost: {atkCost} G";
        }

        public void UpgradeHPAction()
        {
            int currentGold = PlayerPrefs.GetInt("Pulsevania_TotalGold", 0);
            int hpUpgrade = PlayerPrefs.GetInt("Pulsevania_MaxHPUpgrade", 0);
            int cost = 50 * (hpUpgrade + 1);

            if (currentGold >= cost)
            {
                PlayerPrefs.SetInt("Pulsevania_TotalGold", currentGold - cost);
                PlayerPrefs.SetInt("Pulsevania_MaxHPUpgrade", hpUpgrade + 1);
                PlayerPrefs.Save();
                UpdateShopUI();
                if (btnUpgradeHP != null)
                {
                    string msg = currentLanguage == GameLanguage.Turkish ? "Maks Can Yükseltildi!" : "HP Upgraded!";
                    DamageTextPool.Instance.SpawnText(btnUpgradeHP.transform.position + Vector3.up, msg, Color.green);
                }
            }
            else
            {
                if (btnUpgradeHP != null)
                {
                    string msg = currentLanguage == GameLanguage.Turkish ? "Yetersiz Altın!" : "Not enough Gold!";
                    DamageTextPool.Instance.SpawnText(btnUpgradeHP.transform.position + Vector3.up, msg, Color.red);
                }
            }
        }

        public void UpgradeATKAction()
        {
            int currentGold = PlayerPrefs.GetInt("Pulsevania_TotalGold", 0);
            int atkUpgrade = PlayerPrefs.GetInt("Pulsevania_ATKUpgrade", 0);
            int cost = 75 * (atkUpgrade + 1);

            if (currentGold >= cost)
            {
                PlayerPrefs.SetInt("Pulsevania_TotalGold", currentGold - cost);
                PlayerPrefs.SetInt("Pulsevania_ATKUpgrade", atkUpgrade + 1);
                PlayerPrefs.Save();
                UpdateShopUI();
                if (btnUpgradeATK != null)
                {
                    string msg = currentLanguage == GameLanguage.Turkish ? "Saldırı Gücü Yükseltildi!" : "ATK Upgraded!";
                    DamageTextPool.Instance.SpawnText(btnUpgradeATK.transform.position + Vector3.up, msg, Color.green);
                }
            }
            else
            {
                if (btnUpgradeATK != null)
                {
                    string msg = currentLanguage == GameLanguage.Turkish ? "Yetersiz Altın!" : "Not enough Gold!";
                    DamageTextPool.Instance.SpawnText(btnUpgradeATK.transform.position + Vector3.up, msg, Color.red);
                }
            }
        }

        [Header("RPG Inventory System")]
        [SerializeField] private GameObject inventoryPanel;
        [SerializeField] private Button btnOpenInventory;
        [SerializeField] private Transform inventoryGridParent;
        [SerializeField] private Transform skeletonEquipmentParent;
        
        private List<GameObject> uiGridSlots = new List<GameObject>();
        private Dictionary<EquipSlot, GameObject> uiEquipSlots = new Dictionary<EquipSlot, GameObject>();

        private void CreateInventoryUI()
        {
            GameObject canvas = GameObject.Find("Canvas");
            if (canvas == null) return;

            // 1. Force CanvasScaler optimization for responsive mobile layouts
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
            }

            // 2. Erase any existing inventory buttons to prevent cache or layout overrides
            Transform hudTrans = canvas.transform.Find("GameplayHUD");
            if (hudTrans != null)
            {
                Transform oldBtn1 = hudTrans.Find("Btn_Inventory");
                if (oldBtn1 != null)
                {
                    if (Application.isPlaying) Destroy(oldBtn1.gameObject);
                    else DestroyImmediate(oldBtn1.gameObject);
                }
                Transform oldBtn2 = hudTrans.Find("Btn_Inventory_New");
                if (oldBtn2 != null)
                {
                    if (Application.isPlaying) Destroy(oldBtn2.gameObject);
                    else DestroyImmediate(oldBtn2.gameObject);
                }
            }

            Transform btnPotionTrans = canvas.transform.Find("GameplayHUD/BtnPotion");
            if (btnPotionTrans != null)
            {
                GameObject btnInvGo = new GameObject("Btn_Inventory_New");
                btnInvGo.transform.SetParent(canvas.transform.Find("GameplayHUD"), false);
                
                RectTransform rtPotion = btnPotionTrans.GetComponent<RectTransform>();
                RectTransform rtInv = btnInvGo.AddComponent<RectTransform>();
                
                // Absolute Layout Constraints
                rtInv.anchorMin = rtPotion.anchorMin;
                rtInv.anchorMax = rtPotion.anchorMax;
                rtInv.pivot = rtPotion.pivot;
                rtInv.sizeDelta = rtPotion.sizeDelta; // Clone exact width and height of Potion button
                rtInv.anchoredPosition = rtPotion.anchoredPosition + new Vector2(0f, rtPotion.sizeDelta.y + 10f); // 10px vertical gap

                Image img = btnInvGo.AddComponent<Image>();
                img.color = rtPotion.GetComponent<Image>() != null ? rtPotion.GetComponent<Image>().color : new Color(0.35f, 0.22f, 0.15f, 1f);

                Button btn = btnInvGo.AddComponent<Button>();
                btn.onClick.AddListener(ToggleInventoryPanel);

                GameObject textGo = new GameObject("Text");
                textGo.transform.SetParent(btnInvGo.transform, false);
                RectTransform rtText = textGo.AddComponent<RectTransform>();
                rtText.anchoredPosition = Vector2.zero;
                rtText.sizeDelta = rtInv.sizeDelta;
                
                Text txt = textGo.AddComponent<Text>();
                txt.text = "INVENTORY";
                txt.alignment = TextAnchor.MiddleCenter;

                var usePotionText = btnPotionTrans.GetComponentInChildren<Text>();
                if (usePotionText != null)
                {
                    txt.font = usePotionText.font;
                    txt.fontSize = usePotionText.fontSize;
                    txt.fontStyle = usePotionText.fontStyle;
                    txt.color = usePotionText.color;
                    txt.resizeTextForBestFit = false;
                }
                else
                {
                    txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    txt.color = Color.white;
                    txt.fontStyle = FontStyle.Bold;
                    txt.fontSize = 20;
                }

                btnOpenInventory = btn;
            }

            // 3. Create centered boxed Inventory Panel with a dim background backdrop
            Transform invPanelTrans = canvas.transform.Find("InventoryPanel");
            if (invPanelTrans == null)
            {
                // Dim Backdrop (100% width/height of screenspace)
                GameObject backdropGo = new GameObject("InventoryPanel");
                backdropGo.transform.SetParent(canvas.transform);
                RectTransform rtBackdrop = backdropGo.AddComponent<RectTransform>();
                rtBackdrop.anchorMin = Vector2.zero;
                rtBackdrop.anchorMax = Vector2.one;
                rtBackdrop.anchoredPosition = Vector2.zero;
                rtBackdrop.sizeDelta = Vector2.zero;

                Image backdropImg = backdropGo.AddComponent<Image>();
                backdropImg.color = new Color(0f, 0f, 0f, 0.6f); // Elegant 60% dimming

                // Central Fixed Container Parent Panel (Strict 900x540 centered box)
                GameObject containerGo = new GameObject("InventoryContainer");
                containerGo.transform.SetParent(backdropGo.transform);
                RectTransform rtContainer = containerGo.AddComponent<RectTransform>();
                rtContainer.anchorMin = new Vector2(0.5f, 0.5f);
                rtContainer.anchorMax = new Vector2(0.5f, 0.5f);
                rtContainer.pivot = new Vector2(0.5f, 0.5f);
                rtContainer.anchoredPosition = Vector2.zero;
                rtContainer.sizeDelta = new Vector2(900f, 540f);

                Image containerImg = containerGo.AddComponent<Image>();
                containerImg.color = new Color(0.06f, 0.06f, 0.08f, 0.98f); // Pure dark premium carbon tint

                // Prevent click propagation from closing the panel
                Button blockBtn = containerGo.AddComponent<Button>();
                blockBtn.transition = Selectable.Transition.None;

                containerGo.AddComponent<CanvasGroup>();

                // Close Button inside Container
                GameObject closeBtnGo = new GameObject("Btn_Close");
                closeBtnGo.transform.SetParent(containerGo.transform);
                RectTransform rtClose = closeBtnGo.AddComponent<RectTransform>();
                rtClose.anchorMin = new Vector2(1f, 1f);
                rtClose.anchorMax = new Vector2(1f, 1f);
                rtClose.pivot = new Vector2(1f, 1f);
                rtClose.anchoredPosition = new Vector2(-20f, -20f);
                rtClose.sizeDelta = new Vector2(30f, 30f);
                Image closeImg = closeBtnGo.AddComponent<Image>();
                closeImg.color = Color.red;
                Button closeBtn = closeBtnGo.AddComponent<Button>();
                closeBtn.onClick.AddListener(ToggleInventoryPanel);

                GameObject closeTextGo = new GameObject("Text");
                closeTextGo.transform.SetParent(closeBtnGo.transform);
                RectTransform rtCloseTxt = closeTextGo.AddComponent<RectTransform>();
                rtCloseTxt.anchoredPosition = Vector2.zero;
                rtCloseTxt.sizeDelta = rtClose.sizeDelta;
                Text closeTxt = closeTextGo.AddComponent<Text>();
                closeTxt.text = "X";
                closeTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                closeTxt.alignment = TextAnchor.MiddleCenter;
                closeTxt.color = Color.white;
                closeTxt.fontStyle = FontStyle.Bold;

                // Character Panel Title inside Container
                GameObject titleGo = new GameObject("Title");
                titleGo.transform.SetParent(containerGo.transform);
                RectTransform rtTitle = titleGo.AddComponent<RectTransform>();
                rtTitle.anchorMin = new Vector2(0.5f, 1f);
                rtTitle.anchorMax = new Vector2(0.5f, 1f);
                rtTitle.pivot = new Vector2(0.5f, 1f);
                rtTitle.anchoredPosition = new Vector2(0f, -20f);
                rtTitle.sizeDelta = new Vector2(500f, 40f);
                Text titleTxt = titleGo.AddComponent<Text>();
                titleTxt.text = currentLanguage == GameLanguage.Turkish ? "KARAKTER ENVANTERİ" : "CHARACTER INVENTORY";
                titleTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                titleTxt.alignment = TextAnchor.MiddleCenter;
                titleTxt.color = Color.yellow;
                titleTxt.fontStyle = FontStyle.Bold;
                titleTxt.fontSize = 32;

                // Left Grid Panel (Anchored Left inside Container)
                GameObject gridGo = new GameObject("InventoryGrid");
                gridGo.transform.SetParent(containerGo.transform);
                RectTransform rtGrid = gridGo.AddComponent<RectTransform>();
                rtGrid.anchorMin = new Vector2(0f, 0.5f);
                rtGrid.anchorMax = new Vector2(0f, 0.5f);
                rtGrid.pivot = new Vector2(0f, 0.5f);
                rtGrid.anchoredPosition = new Vector2(30f, 0f);
                rtGrid.sizeDelta = new Vector2(380f, 360f);
                inventoryGridParent = gridGo.transform;

                GridLayoutGroup gridLayout = gridGo.AddComponent<GridLayoutGroup>();
                gridLayout.cellSize = new Vector2(75f, 75f); // neat square slots
                gridLayout.spacing = new Vector2(20f, 90f); // Generous spacing for breathability
                gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
                gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                gridLayout.constraintCount = 4;

                uiGridSlots.Clear();
                for (int i = 0; i < 8; i++)
                {
                    GameObject slotGo = new GameObject($"Slot_{i}");
                    slotGo.transform.SetParent(gridGo.transform);
                    Image slotImg = slotGo.AddComponent<Image>();
                    slotImg.color = new Color(0.18f, 0.18f, 0.22f, 0.8f);
                    
                    GameObject itemIconGo = new GameObject("Icon");
                    itemIconGo.transform.SetParent(slotGo.transform);
                    RectTransform rtIcon = itemIconGo.AddComponent<RectTransform>();
                    rtIcon.anchorMin = Vector2.zero;
                    rtIcon.anchorMax = Vector2.one;
                    rtIcon.anchoredPosition = Vector2.zero;
                    rtIcon.sizeDelta = new Vector2(-10f, -10f);
                    Image iconImg = itemIconGo.AddComponent<Image>();
                    iconImg.color = Color.clear;

                    // Guarantee CanvasGroup is available programmatically
                    itemIconGo.AddComponent<CanvasGroup>();

                    InventoryDragHandler dragHandler = itemIconGo.AddComponent<InventoryDragHandler>();
                    dragHandler.slotIndex = i;

                    // Item Description Label underneath the Slot (Highly Ergonomic for Mobile)
                    GameObject labelGo = new GameObject("SlotLabel");
                    labelGo.transform.SetParent(slotGo.transform, false);
                    RectTransform labelRt = labelGo.AddComponent<RectTransform>();
                    labelRt.anchorMin = new Vector2(0.5f, 0f); // Center anchored horizontally
                    labelRt.anchorMax = new Vector2(0.5f, 0f); // Center anchored horizontally
                    labelRt.pivot = new Vector2(0.5f, 1f);
                    labelRt.anchoredPosition = new Vector2(0f, -8f);
                    labelRt.sizeDelta = new Vector2(110f, 75f); // Explicit width expanded to prevent overlap with larger font
                    
                    Text labelTxt = labelGo.AddComponent<Text>();
                    labelTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    labelTxt.fontSize = 20; // Expanded to 20px
                    labelTxt.fontStyle = FontStyle.Bold;
                    labelTxt.alignment = TextAnchor.UpperCenter;
                    labelTxt.supportRichText = true; // Enable rich text for colors
                    
                    Outline labelOutline = labelGo.AddComponent<Outline>();
                    labelOutline.effectColor = Color.black;
                    labelOutline.effectDistance = new Vector2(1.5f, 1.5f);

                    GameObject countGo = new GameObject("Count");
                    countGo.transform.SetParent(slotGo.transform, false);
                    RectTransform countRt = countGo.AddComponent<RectTransform>();
                    countRt.anchorMin = new Vector2(0f, 0f);
                    countRt.anchorMax = new Vector2(1f, 0.3f);
                    countRt.anchoredPosition = new Vector2(0f, 5f);
                    countRt.sizeDelta = Vector2.zero;
                    
                    Text countTxt = countGo.AddComponent<Text>();
                    countTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    countTxt.fontSize = 12;
                    countTxt.fontStyle = FontStyle.Bold;
                    countTxt.alignment = TextAnchor.LowerRight;
                    countTxt.color = Color.white;
                    
                    Outline countOutline = countGo.AddComponent<Outline>();
                    countOutline.effectColor = Color.black;
                    countOutline.effectDistance = new Vector2(1f, 1f);
                    
                    countGo.SetActive(false);

                    uiGridSlots.Add(slotGo);
                }

                // Right Skeleton / Doll Holder Panel (Anchored Right inside Container)
                GameObject skeletonGo = new GameObject("SkeletonDoll");
                skeletonGo.transform.SetParent(containerGo.transform);
                RectTransform rtSkel = skeletonGo.AddComponent<RectTransform>();
                rtSkel.anchorMin = new Vector2(1f, 0.5f);
                rtSkel.anchorMax = new Vector2(1f, 0.5f);
                rtSkel.pivot = new Vector2(1f, 0.5f);
                rtSkel.anchoredPosition = new Vector2(-50f, -10f);
                rtSkel.sizeDelta = new Vector2(450f, 420f);
                skeletonEquipmentParent = skeletonGo.transform;

                Image skelImg = skeletonGo.AddComponent<Image>();
                skelImg.color = new Color(0.1f, 0.1f, 0.14f, 0.85f);

                uiEquipSlots.Clear();
                uiEquipSlots.Clear();
                EquipSlot[] equipTypes = { 
                    EquipSlot.Head, 
                    EquipSlot.Chest, 
                    EquipSlot.Hands, 
                    EquipSlot.Legs, 
                    EquipSlot.Feet, 
                    EquipSlot.Weapon, 
                    EquipSlot.Shield, 
                    EquipSlot.ThrowingKnife 
                };
                string[] equipNames = { 
                    "Başlık", 
                    "Zırh", 
                    "Eldiven", 
                    "Pantolon", 
                    "Çizme", 
                    "Silah", 
                    "Kalkan", 
                    "Bıçak" 
                };
                
                // Asymmetric coordinates based on requirement spec
                // Central Stack: Head(0, 130), Chest(0, 45), Legs(0, -40), Feet(0, -125)
                // Right Stack: Gloves(130, 45)
                // Left Stack: Weapon(-130, 45), Shield(-130, -20), Knife(-130, -85)
                float[] equipXPositions = { 0f, 0f, 160f, 0f, 0f, -160f, -160f, -160f };
                float[] equipYPositions = { 130f, 45f, 45f, -40f, -125f, 45f, -20f, -85f };

                for (int i = 0; i < equipTypes.Length; i++)
                {
                    GameObject slotGo = new GameObject($"EquipSlot_{equipTypes[i]}");
                    slotGo.transform.SetParent(skeletonGo.transform);
                    RectTransform rtSlot = slotGo.AddComponent<RectTransform>();
                    rtSlot.anchoredPosition = new Vector3(equipXPositions[i], equipYPositions[i], 0f);
                    rtSlot.sizeDelta = new Vector2(130f, 65f);

                    Image slotImg = slotGo.AddComponent<Image>();
                    
                    // Create circle texture for Head, Hands, and Feet slots
                    if (equipTypes[i] == EquipSlot.Head || equipTypes[i] == EquipSlot.Hands || equipTypes[i] == EquipSlot.Feet)
                    {
                        slotImg.sprite = CreateCircleSprite(new Color(0.22f, 0.22f, 0.28f, 0.8f), 64);
                        slotImg.color = Color.white;
                    }
                    else
                    {
                        slotImg.color = new Color(0.22f, 0.22f, 0.28f, 0.8f);
                    }

                    EquipmentSlotUI slotUI = slotGo.AddComponent<EquipmentSlotUI>();
                    slotUI.targetSlot = equipTypes[i];

                    GameObject labelGo = new GameObject("Label");
                    labelGo.transform.SetParent(slotGo.transform);
                    RectTransform rtLabel = labelGo.AddComponent<RectTransform>();
                    rtLabel.anchorMin = Vector2.zero;
                    rtLabel.anchorMax = Vector2.one;
                    rtLabel.offsetMin = Vector2.zero;
                    rtLabel.offsetMax = Vector2.zero;
                    Text labelTxt = labelGo.AddComponent<Text>();
                    labelTxt.text = equipNames[i];
                    labelTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    labelTxt.alignment = TextAnchor.MiddleCenter;
                    labelTxt.color = Color.white;
                    labelTxt.fontStyle = FontStyle.Bold;
                    labelTxt.fontSize = 26; // Enlarged from 20 to 26 for better visibility
                    labelTxt.resizeTextForBestFit = false;

                    Outline labelOutline = labelGo.AddComponent<Outline>();
                    labelOutline.effectColor = Color.black;
                    labelOutline.effectDistance = new Vector2(1f, 1f);

                    GameObject itemIconGo = new GameObject("Icon");
                    itemIconGo.transform.SetParent(slotGo.transform);
                    RectTransform rtIcon = itemIconGo.AddComponent<RectTransform>();
                    rtIcon.anchorMin = Vector2.zero;
                    rtIcon.anchorMax = Vector2.one;
                    rtIcon.anchoredPosition = Vector2.zero;
                    rtIcon.sizeDelta = Vector2.zero;
                    Image iconImg = itemIconGo.AddComponent<Image>();
                    iconImg.color = Color.clear;

                    // Guarantee CanvasGroup is available programmatically
                    itemIconGo.AddComponent<CanvasGroup>();

                    InventoryDragHandler dragHandler = itemIconGo.AddComponent<InventoryDragHandler>();
                    dragHandler.slotIndex = -1;
                    dragHandler.equippedSlotType = equipTypes[i];

                    uiEquipSlots[equipTypes[i]] = slotGo;
                }

                inventoryPanel = backdropGo; // Main toggle toggles the backdrop
                inventoryPanel.SetActive(false);
            }
        }

        private Sprite CreateCircleSprite(Color color, int size)
        {
            Texture2D tex = new Texture2D(size, size);
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - r;
                    float dy = y - r;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist <= r)
                    {
                        if (dist >= r - 1.5f)
                            tex.SetPixel(x, y, Color.black); // border
                        else
                            tex.SetPixel(x, y, color);
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            tex.filterMode = FilterMode.Point;
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        public void ToggleInventoryPanel()
        {
            if (inventoryPanel != null)
            {
                bool active = !inventoryPanel.activeSelf;
                inventoryPanel.SetActive(active);

                if (active)
                {
                    inventoryPanel.transform.SetAsLastSibling();
                    if (activePlayer != null) activePlayer.SetControlsLocked(true);
                    UpdateInventoryUI();
                    
                    if (InventoryManager.Instance != null)
                    {
                        InventoryManager.Instance.LocatePlayerVisuals();
                    }

                    GameObject canvas = GameObject.Find("Canvas");
                    if (canvas != null)
                    {
                        CreateMerchantShopUI(canvas);
                    }
                }
                else
                {
                    if (activePlayer != null) activePlayer.SetControlsLocked(false);
                }
            }
        }

        public void UpdateInventoryUI()
        {
            if (InventoryManager.Instance == null) return;

            for (int i = 0; i < 8; i++)
            {
                if (i < uiGridSlots.Count)
                {
                    Transform iconTrans = uiGridSlots[i].transform.Find("Icon");
                    Transform labelTrans = uiGridSlots[i].transform.Find("SlotLabel");
                    Text labelTxt = labelTrans != null ? labelTrans.GetComponent<Text>() : null;

                    if (iconTrans != null)
                    {
                        Image iconImg = iconTrans.GetComponent<Image>();
                        Transform countTrans = uiGridSlots[i].transform.Find("Count");
                        Text countTxt = countTrans != null ? countTrans.GetComponent<Text>() : null;

                        if (i < InventoryManager.Instance.inventoryItems.Count)
                        {
                            ItemData item = InventoryManager.Instance.inventoryItems[i];
                            iconImg.sprite = item.icon;
                            iconImg.color = Color.white;

                            // Fill dynamic slot label text with rich text styling for elite breathability
                            if (labelTxt != null)
                            {
                                bool isTR = currentLanguage == GameLanguage.Turkish;
                                string statDesc = "";
                                if (item.statType != StatType.None)
                                {
                                    string statName = "";
                                    switch (item.statType)
                                    {
                                        case StatType.MaxHP: statName = isTR ? "Can" : "HP"; break;
                                        case StatType.MeleeDamage: statName = isTR ? "Hasar" : "Dmg"; break;
                                        case StatType.HeavyDamage: statName = isTR ? "Ağır H." : "HvDmg"; break;
                                        case StatType.RangedDamage: statName = isTR ? "Fırlat" : "RngDmg"; break;
                                        case StatType.RestoresHP: statName = isTR ? "İksir" : "Pot"; break;
                                        default: statName = item.statType.ToString(); break;
                                    }
                                    statDesc = $"\n<color=#50C878>+{item.statValue} {statName}</color>";
                                }

                                string displayName = GetLocalizedItemName(item.itemName, currentLanguage == GameLanguage.Turkish);
                                if (displayName.Length > 15)
                                {
                                    displayName = displayName.Substring(0, 13) + "..";
                                }

                                labelTxt.text = $"<color=#FFFDD0>{displayName}</color>{statDesc}";
                            }

                            if (countTxt != null)
                            {
                                if (item.count > 1)
                                {
                                    countTxt.text = $"x{item.count}";
                                    countTrans.gameObject.SetActive(true);
                                }
                                else
                                {
                                    countTxt.text = "";
                                    countTrans.gameObject.SetActive(false);
                                }
                            }
                        }
                        else
                        {
                            iconImg.sprite = null;
                            iconImg.color = Color.clear;
                            if (labelTxt != null) labelTxt.text = "";
                            if (countTrans != null) countTrans.gameObject.SetActive(false);
                        }
                    }
                }
            }

            EquipSlot[] slots = { EquipSlot.Head, EquipSlot.Chest, EquipSlot.Hands, EquipSlot.Legs, EquipSlot.Feet, EquipSlot.Weapon, EquipSlot.Shield, EquipSlot.ThrowingKnife };
            foreach (EquipSlot slot in slots)
            {
                if (uiEquipSlots.ContainsKey(slot))
                {
                    Transform iconTrans = uiEquipSlots[slot].transform.Find("Icon");
                    Transform labelTrans = uiEquipSlots[slot].transform.Find("Label");
                    if (iconTrans != null)
                    {
                        Image iconImg = iconTrans.GetComponent<Image>();
                        ItemData equipped = InventoryManager.Instance.equippedItems[slot];
                        if (equipped != null)
                        {
                            iconImg.sprite = equipped.icon;
                            iconImg.color = Color.white;
                            if (labelTrans != null) labelTrans.gameObject.SetActive(false);
                        }
                        else
                        {
                            iconImg.sprite = null;
                            iconImg.color = Color.clear;
                            if (labelTrans != null) labelTrans.gameObject.SetActive(true);
                        }
                    }
                }
            }
        }

        private static bool isIntroPlayed = false;

        private void StartIntroSequence()
        {
            if (isIntroPlayed)
            {
                if (mainMenuPanel != null)
                {
                    ApplyMainMenuBackground();
                    mainMenuPanel.SetActive(true);
                }
                return;
            }

            isIntroPlayed = true;
            if (mainMenuPanel != null)
            {
                ApplyMainMenuBackground(); // Pre-load background while the screen is black!
                mainMenuPanel.SetActive(false);
            }

            StartCoroutine(IntroSequenceRoutine());
        }

        private IEnumerator IntroSequenceRoutine()
        {
            GameObject canvas = GameObject.Find("Canvas");
            if (canvas == null)
            {
                Canvas mainCanvas = GameObject.FindObjectOfType<Canvas>();
                if (mainCanvas != null) canvas = mainCanvas.gameObject;
            }
            if (canvas == null)
            {
                if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
                yield break;
            }

            // Create Intro Overlay Panel
            GameObject introPanel = new GameObject("EktReklamIntroPanel");
            introPanel.transform.SetParent(canvas.transform, false);
            RectTransform rtIntro = introPanel.AddComponent<RectTransform>();
            rtIntro.anchorMin = Vector2.zero;
            rtIntro.anchorMax = Vector2.one;
            rtIntro.sizeDelta = Vector2.zero;

            Image bg = introPanel.AddComponent<Image>();
            bg.color = Color.black;

            CanvasGroup cg = introPanel.AddComponent<CanvasGroup>();
            cg.alpha = 1f;

            // Barcode Container Setup
            GameObject barcodeContainer = new GameObject("BarcodeContainer");
            barcodeContainer.transform.SetParent(introPanel.transform, false);
            RectTransform rtBarContainer = barcodeContainer.AddComponent<RectTransform>();
            rtBarContainer.anchorMin = new Vector2(0.5f, 0.5f);
            rtBarContainer.anchorMax = new Vector2(0.5f, 0.5f);
            rtBarContainer.pivot = new Vector2(0.5f, 0.5f);
            rtBarContainer.anchoredPosition = new Vector2(0f, 60f);
            rtBarContainer.sizeDelta = new Vector2(240f, 60f);

            // Pseudo Barcode stripes
            int[] barPattern = { 2, 4, 1, 1, 3, 2, 5, 1, 2, 4, 1, 3, 2, 1, 5, 2, 3, 1, 4, 2, 1, 3, 2 };
            float totalPatternWidth = 0f;
            foreach (int w in barPattern) totalPatternWidth += w * 3f + 2f;

            float curX = -totalPatternWidth / 2f;
            for (int i = 0; i < barPattern.Length; i++)
            {
                float w = barPattern[i] * 3f;
                if (i % 2 == 0)
                {
                    GameObject barGo = new GameObject($"Bar_{i}");
                    barGo.transform.SetParent(barcodeContainer.transform, false);
                    RectTransform rtBar = barGo.AddComponent<RectTransform>();
                    rtBar.anchorMin = new Vector2(0.5f, 0.5f);
                    rtBar.anchorMax = new Vector2(0.5f, 0.5f);
                    rtBar.pivot = new Vector2(0f, 0.5f);
                    rtBar.anchoredPosition = new Vector2(curX, 0f);
                    rtBar.sizeDelta = new Vector2(w, 60f);

                    Image barImg = barGo.AddComponent<Image>();
                    barImg.color = new Color(0.85f, 0.85f, 0.9f, 1f);
                }
                curX += w + 2f;
            }

            // EKT Barcode Label Underneath
            GameObject barcodeLabelGo = new GameObject("BarcodeLabel");
            barcodeLabelGo.transform.SetParent(barcodeContainer.transform, false);
            RectTransform rtBarLabel = barcodeLabelGo.AddComponent<RectTransform>();
            rtBarLabel.anchorMin = new Vector2(0.5f, 0f);
            rtBarLabel.anchorMax = new Vector2(0.5f, 0f);
            rtBarLabel.pivot = new Vector2(0.5f, 1f);
            rtBarLabel.anchoredPosition = new Vector2(0f, -5f);
            rtBarLabel.sizeDelta = new Vector2(240f, 20f);

            Text barcodeLabelTxt = barcodeLabelGo.AddComponent<Text>();
            barcodeLabelTxt.text = "EKT-7928-GAMES";
            barcodeLabelTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            barcodeLabelTxt.fontSize = 11;
            barcodeLabelTxt.color = new Color(0.6f, 0.6f, 0.65f, 1f);
            barcodeLabelTxt.alignment = TextAnchor.MiddleCenter;

            // Barcode Scan Laser Line (Core + Glow)
            GameObject laserGlowGo = new GameObject("LaserGlow");
            laserGlowGo.transform.SetParent(barcodeContainer.transform, false);
            RectTransform rtLaserGlow = laserGlowGo.AddComponent<RectTransform>();
            rtLaserGlow.anchorMin = new Vector2(0.5f, 1f);
            rtLaserGlow.anchorMax = new Vector2(0.5f, 1f);
            rtLaserGlow.pivot = new Vector2(0.5f, 0.5f);
            rtLaserGlow.anchoredPosition = new Vector2(0f, 0f);
            rtLaserGlow.sizeDelta = new Vector2(280f, 7f);
            Image laserGlowImg = laserGlowGo.AddComponent<Image>();
            laserGlowImg.color = new Color(1f, 0f, 0.1f, 0.35f);

            GameObject laserCoreGo = new GameObject("LaserCore");
            laserCoreGo.transform.SetParent(laserGlowGo.transform, false);
            RectTransform rtLaserCore = laserCoreGo.AddComponent<RectTransform>();
            rtLaserCore.anchorMin = Vector2.zero;
            rtLaserCore.anchorMax = Vector2.one;
            rtLaserCore.sizeDelta = Vector2.zero;
            Image laserCoreImg = laserCoreGo.AddComponent<Image>();
            laserCoreImg.color = new Color(1f, 0.3f, 0.3f, 1f);

            // Neon Label "EKT GAMES" (Cyan Glow + White Core)
            GameObject neonGlowGo = new GameObject("NeonGlow");
            neonGlowGo.transform.SetParent(introPanel.transform, false);
            RectTransform rtNeonGlow = neonGlowGo.AddComponent<RectTransform>();
            rtNeonGlow.anchorMin = new Vector2(0.5f, 0.5f);
            rtNeonGlow.anchorMax = new Vector2(0.5f, 0.5f);
            rtNeonGlow.pivot = new Vector2(0.5f, 0.5f);
            rtNeonGlow.anchoredPosition = new Vector2(0f, -40f);
            rtNeonGlow.sizeDelta = new Vector2(600f, 80f);

            Text neonGlowTxt = neonGlowGo.AddComponent<Text>();
            neonGlowTxt.text = "EKT GAMES";
            neonGlowTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            neonGlowTxt.fontSize = 46;
            neonGlowTxt.fontStyle = FontStyle.Bold;
            neonGlowTxt.alignment = TextAnchor.MiddleCenter;
            neonGlowTxt.color = new Color(0f, 0.7f, 1f, 0f); // Starts invisible

            Outline neonOutline = neonGlowGo.AddComponent<Outline>();
            neonOutline.effectColor = new Color(0f, 0.4f, 0.8f, 0.3f);
            neonOutline.effectDistance = new Vector2(3f, 3f);

            GameObject neonCoreGo = new GameObject("NeonCore");
            neonCoreGo.transform.SetParent(neonGlowGo.transform, false);
            RectTransform rtNeonCore = neonCoreGo.AddComponent<RectTransform>();
            rtNeonCore.anchorMin = Vector2.zero;
            rtNeonCore.anchorMax = Vector2.one;
            rtNeonCore.sizeDelta = Vector2.zero;

            Text neonCoreTxt = neonCoreGo.AddComponent<Text>();
            neonCoreTxt.text = "EKT GAMES";
            neonCoreTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            neonCoreTxt.fontSize = 44;
            neonCoreTxt.fontStyle = FontStyle.Bold;
            neonCoreTxt.alignment = TextAnchor.MiddleCenter;
            neonCoreTxt.color = new Color(1f, 1f, 1f, 0f); // Starts invisible

            // Subtitle Presents
            GameObject presentsGo = new GameObject("PresentsSubtitle");
            presentsGo.transform.SetParent(introPanel.transform, false);
            RectTransform rtPresents = presentsGo.AddComponent<RectTransform>();
            rtPresents.anchorMin = new Vector2(0.5f, 0.5f);
            rtPresents.anchorMax = new Vector2(0.5f, 0.5f);
            rtPresents.pivot = new Vector2(0.5f, 0.5f);
            rtPresents.anchoredPosition = new Vector2(0f, -110f);
            rtPresents.sizeDelta = new Vector2(400f, 30f);

            Text presentsTxt = presentsGo.AddComponent<Text>();
            presentsTxt.text = "PRESENTS";
            presentsTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            presentsTxt.fontSize = 13;
            presentsTxt.color = new Color(0.6f, 0.6f, 0.7f, 0f);
            presentsTxt.alignment = TextAnchor.MiddleCenter;

            // Animation Loop (Total 6.0 seconds, skippable by tap/click to open game fast)
            float elapsed = 0f;
            bool hasBeeped = false;
            CanvasGroup barcodeCg = barcodeContainer.AddComponent<CanvasGroup>();

            while (elapsed < 4.8f)
            {
                // Clamp delta time to prevent large time jumps during initial editor lag/loading
                elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.05f);

                // Allow players to skip the intro instantly by tapping/clicking (Input System compatible)
                bool skipPressed = false;
                if (UnityEngine.InputSystem.Keyboard.current != null && 
                    (UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame || 
                     UnityEngine.InputSystem.Keyboard.current.enterKey.wasPressedThisFrame)) 
                {
                    skipPressed = true;
                }
                if (UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame) 
                {
                    skipPressed = true;
                }
                if (UnityEngine.InputSystem.Touchscreen.current != null)
                {
                    for (int i = 0; i < UnityEngine.InputSystem.Touchscreen.current.touches.Count; i++)
                    {
                        var touch = UnityEngine.InputSystem.Touchscreen.current.touches[i];
                        if (touch.press.wasPressedThisFrame)
                        {
                            skipPressed = true;
                            break;
                        }
                    }
                }

                // En az 1.5 saniye boyunca intro'nun atlanmasını engelle, böylece barkod ve başlangıç yazısı görünür
                if (skipPressed && elapsed > 1.5f) 
                {
                    break;
                }

                // Phase 1: Barcode Scan (0.0 to 2.0s)
                if (elapsed < 2.0f)
                {
                    float scanAlpha = Mathf.Clamp01((2.0f - elapsed) / 0.3f);
                    barcodeCg.alpha = scanAlpha;

                    float laserProgress = Mathf.Clamp01(elapsed / 1.5f);
                    rtLaserGlow.anchoredPosition = new Vector2(0f, -laserProgress * 60f);

                    // Scan beep sound trigger at 1.5s
                    if (elapsed >= 1.5f && !hasBeeped)
                    {
                        hasBeeped = true;
                        AudioClip beepClip = CreateProceduralBeepClip();
                        if (beepClip != null)
                        {
                            AudioSource.PlayClipAtPoint(beepClip, Camera.main != null ? Camera.main.transform.position : Vector3.zero, 0.4f);
                        }
                    }
                }
                else
                {
                    barcodeContainer.SetActive(false);
                }

                // Phase 2 & 3: Logo flickering & glowing (1.3s'de başlasın, barkod silinmeden hemen önce)
                if (elapsed >= 1.3f)
                {
                    float logoAlpha = Mathf.Clamp01((elapsed - 1.3f) / 0.4f);

                    bool isFlickerOn = true;
                    if (elapsed > 1.3f && elapsed < 2.1f)
                    {
                        isFlickerOn = (Random.value > 0.4f);
                    }

                    float pulse = Mathf.PingPong(Time.unscaledTime * 2.0f, 1f);
                    float neonIntensity = 0.6f + pulse * 0.4f;
                    if (elapsed > 1.3f && elapsed < 2.1f && !isFlickerOn)
                    {
                        neonIntensity = 0.05f;
                    }

                    neonGlowTxt.color = new Color(0f, 0.7f, 1f, neonIntensity * 0.8f * logoAlpha);
                    neonCoreTxt.color = new Color(1f, 1f, 1f, logoAlpha * (isFlickerOn ? 1f : 0.1f));

                    // Presents fade-in (1.4s'de başlasın, logo ile eşzamanlı)
                    float presentsAlpha = Mathf.Clamp01((elapsed - 1.4f) / 0.6f);
                    presentsTxt.color = new Color(0.6f, 0.6f, 0.7f, presentsAlpha);
                }

                // Smoothly fade-out everything as we approach 4.8 seconds
                if (elapsed >= 4.0f)
                {
                    // Activate main menu background underneath so it cross-fades perfectly
                    if (mainMenuPanel != null && !mainMenuPanel.activeSelf)
                    {
                        ApplyMainMenuBackground();
                        mainMenuPanel.SetActive(true);
                    }

                    float fadeOutAlpha = Mathf.Clamp01((4.8f - elapsed) / 0.8f);
                    cg.alpha = fadeOutAlpha;
                }

                yield return null;
            }

            // Cleanup & load main menu
            Destroy(introPanel);

            if (mainMenuPanel != null)
            {
                if (!mainMenuPanel.activeSelf)
                {
                    ApplyMainMenuBackground();
                    mainMenuPanel.SetActive(true);
                }
                EnsureMainMenuButtons();
            }
        }

        private Sprite CreateBarcodeSprite()
        {
            int width = 32;
            int height = 16;
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            
            // Draw a barcode: alternating vertical black/white stripes
            for (int x = 0; x < width; x++)
            {
                // Alternating pattern representing barcode lines
                bool isBar = (x % 3 == 0 || x % 5 == 0 || x == 1 || x == 14 || x == 22 || x == 27) && (x > 1 && x < 30);
                Color c = isBar ? Color.white : Color.clear;
                for (int y = 0; y < height; y++)
                {
                    // Outer border
                    if (y == 0 || y == height - 1 || x == 0 || x == width - 1)
                    {
                        tex.SetPixel(x, y, Color.white);
                    }
                    else
                    {
                        tex.SetPixel(x, y, c);
                    }
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 16f);
        }

        private AudioClip CreateProceduralBeepClip()
        {
            int sampleRate = 44100;
            float duration = 0.08f; // Short beep tone
            float frequency = 1800f; // High frequency beep
            int sampleCount = (int)(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float progress = t / duration;
                float phase = 2f * Mathf.PI * frequency * t;
                samples[i] = Mathf.Sin(phase) * 0.4f * (1f - progress);
            }

            AudioClip clip = AudioClip.Create("ScanBeep", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static Sprite cachedBgSprite = null;

        private void ApplyMainMenuBackground()
        {
            if (mainMenuPanel == null)
            {
                Debug.LogError("ApplyMainMenuBackground: mainMenuPanel is NULL!");
                return;
            }

            Debug.Log("ApplyMainMenuBackground: Starting setup...");
            Sprite bgSprite = cachedBgSprite;

            if (bgSprite == null)
            {
                // 1. Try loading from Resources (crucial for builds)
                Texture2D resourceTex = Resources.Load<Texture2D>("MainMenuBackground");
                if (resourceTex != null)
                {
                    Debug.Log("ApplyMainMenuBackground: Loaded from Resources successfully!");
                    resourceTex.filterMode = FilterMode.Point;
                    bgSprite = Sprite.Create(resourceTex, new Rect(0, 0, resourceTex.width, resourceTex.height), new Vector2(0.5f, 0.5f));
                }
                else
                {
                    Debug.LogWarning("ApplyMainMenuBackground: Resources.Load returned NULL. Trying fallback disk loading...");
                }

                // 2. Fallback: Load from disk (for immediate editor testing before Unity meta import)
                if (bgSprite == null)
                {
                    string bgPath = System.IO.Path.Combine(Application.dataPath, "Resources/MainMenuBackground.png");
                    if (!System.IO.File.Exists(bgPath))
                    {
                        bgPath = System.IO.Path.Combine(Application.dataPath, "Textures/MainMenuBackground.png");
                    }

                    Debug.Log("ApplyMainMenuBackground: Disk fallback path = " + bgPath);
                    if (System.IO.File.Exists(bgPath))
                    {
                        byte[] fileData = System.IO.File.ReadAllBytes(bgPath);
                        Texture2D diskTex = new Texture2D(2, 2);
                        if (diskTex.LoadImage(fileData))
                        {
                            Debug.Log("ApplyMainMenuBackground: Loaded from Disk successfully!");
                            diskTex.filterMode = FilterMode.Point;
                            bgSprite = Sprite.Create(diskTex, new Rect(0, 0, diskTex.width, diskTex.height), new Vector2(0.5f, 0.5f));
                        }
                        else
                        {
                            Debug.LogError("ApplyMainMenuBackground: LoadImage failed on disk data.");
                        }
                    }
                    else
                    {
                        Debug.LogError("ApplyMainMenuBackground: Disk file does not exist at paths.");
                    }
                }
                cachedBgSprite = bgSprite;
            }

            if (bgSprite != null)
            {
                Debug.Log("ApplyMainMenuBackground: bgSprite is VALID! Assigning to UI...");
                
                // 1. If the parent mainMenuPanel itself has an Image component, disable it or set to clear
                // to make sure it doesn't occlude our custom background image!
                Image parentImg = mainMenuPanel.GetComponent<Image>();
                if (parentImg != null)
                {
                    parentImg.color = Color.clear;
                    parentImg.enabled = false;
                }

                // 2. Aggressively disable EVERY child under mainMenuPanel that is NOT a button or our background
                foreach (Transform child in mainMenuPanel.transform)
                {
                    if (child.name != "MainMenuBackground" && 
                        child.name != "NewGameBtn" && 
                        child.name != "LoadGameBtn" && 
                        child.name != "SettingsBtn" && 
                        child.name != "QuitBtn" &&
                        !child.name.Contains("Btn") && 
                        !child.name.Contains("Button"))
                    {
                        Debug.Log("ApplyMainMenuBackground: Disabling occlusion child panel = " + child.name);
                        child.gameObject.SetActive(false);
                    }
                }

                Transform bgTrans = mainMenuPanel.transform.Find("MainMenuBackground");
                GameObject bgGo;
                if (bgTrans == null)
                {
                    bgGo = new GameObject("MainMenuBackground");
                    bgGo.transform.SetParent(mainMenuPanel.transform, false);
                    bgGo.transform.SetAsFirstSibling(); // Push behind menu buttons
                    Debug.Log("ApplyMainMenuBackground: Created new MainMenuBackground game object.");
                }
                else
                {
                    bgGo = bgTrans.gameObject;
                    bgGo.SetActive(true);
                    Debug.Log("ApplyMainMenuBackground: Found existing MainMenuBackground game object.");
                }

                RectTransform rtBg = bgGo.GetComponent<RectTransform>();
                if (rtBg == null) rtBg = bgGo.AddComponent<RectTransform>();
                rtBg.anchorMin = Vector2.zero;
                rtBg.anchorMax = Vector2.one;
                rtBg.pivot = new Vector2(0.5f, 0.5f);
                rtBg.anchoredPosition = Vector2.zero;
                rtBg.sizeDelta = Vector2.zero;

                Image img = bgGo.GetComponent<Image>();
                if (img == null) img = bgGo.AddComponent<Image>();
                img.sprite = bgSprite;
                img.color = Color.white;
                Debug.Log("ApplyMainMenuBackground: Image component successfully set and colored white!");
            }
            else
            {
                Debug.LogError("ApplyMainMenuBackground: bgSprite ended up NULL!");
            }
        }

        private bool isTooltipLocked = false;

        public void ShowTooltip(ItemData item, Vector2 screenPos)
        {
            if (tooltipPanel == null || tooltipText == null) return;

            // Set large bold premium formatting
            tooltipText.fontSize = 15;
            tooltipText.fontStyle = FontStyle.Bold;
            tooltipText.supportRichText = true;

            bool isTR = currentLanguage == GameLanguage.Turkish;
            string slotNameStr = item.equipSlot.ToString();
            if (isTR)
            {
                switch (item.equipSlot)
                {
                    case EquipSlot.Head: slotNameStr = "Başlık"; break;
                    case EquipSlot.Chest: slotNameStr = "Zırh"; break;
                    case EquipSlot.Hands: slotNameStr = "Eldiven"; break;
                    case EquipSlot.Legs: slotNameStr = "Pantolon"; break;
                    case EquipSlot.Feet: slotNameStr = "Çizme"; break;
                    case EquipSlot.Weapon: slotNameStr = "Silah"; break;
                    case EquipSlot.Shield: slotNameStr = "Kalkan"; break;
                    case EquipSlot.ThrowingKnife: slotNameStr = "Bıçak"; break;
                    case EquipSlot.Consumable: slotNameStr = "Tüketilebilir"; break;
                }
            }

            string statNameStr = item.statType.ToString();
            if (isTR)
            {
                switch (item.statType)
                {
                    case StatType.MaxHP: statNameStr = "Maks Can"; break;
                    case StatType.MeleeDamage: statNameStr = "Yakın Dövüş Hasarı"; break;
                    case StatType.HeavyDamage: statNameStr = "Ağır Hasar"; break;
                    case StatType.RangedDamage: statNameStr = "Fırlatma Hasarı"; break;
                    case StatType.Armor: statNameStr = "Zırh"; break;
                    case StatType.MoveSpeed: statNameStr = "Hareket Hızı"; break;
                    case StatType.AttackSpeed: statNameStr = "Saldırı Hızı"; break;
                    case StatType.CritChance: statNameStr = "Kritik Şansı"; break;
                }
            }

            float bonus = item.statValue;
            string bonusStr = (item.statType == StatType.MoveSpeed || item.statType == StatType.AttackSpeed) ? $"+{(bonus * 100f):0}%" : $"+{bonus}";
            if (item.critChance > 0f)
            {
                bonusStr += isTR ? $" | Kritik: +{(item.critChance * 100f):0}%" : $" | Crit: +{(item.critChance * 100f):0}%";
            }

            string localizedName = GetLocalizedItemName(item.itemName, isTR);
            tooltipText.text = $"<b><size=18><color=white>{localizedName}</color></size></b>\n<color=cyan>{(isTR ? "Yuva" : "Slot")}: {slotNameStr}</color>\n<color=yellow>{statNameStr}: {bonusStr}</color>";
            if (item.goldPrice > 0)
            {
                tooltipText.text += isTR ? $"\n<color=orange>Değer: {item.goldPrice} Altın</color>" : $"\n<color=orange>Value: {item.goldPrice} Gold</color>";
            }

            tooltipPanel.SetActive(true);

            // Map screen pos to container local space
            RectTransform containerRT = tooltipPanel.transform.parent as RectTransform;
            if (containerRT != null)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(containerRT, screenPos, null, out Vector2 localPoint);
                // Place 15px right-bottom offset
                tooltipPanel.GetComponent<RectTransform>().anchoredPosition = localPoint + new Vector2(15f, -15f);
            }
        }

        public void LockTooltip(ItemData item, Vector2 screenPos)
        {
            ShowTooltip(item, screenPos);
            isTooltipLocked = true;
        }

        public void UnlockTooltip()
        {
            isTooltipLocked = false;
            HideTooltip();
        }

        public void HideTooltip()
        {
            if (isTooltipLocked) return; // Keep locked on single mobile tap
            if (tooltipPanel != null) tooltipPanel.SetActive(false);
        }

        public void ForceHideTooltip()
        {
            isTooltipLocked = false;
            if (tooltipPanel != null) tooltipPanel.SetActive(false);
        }

        // --- MERCHANT SHOP SYSTEM ---
        private GameObject merchantShopPanelGo;
        private Text merchantShopGoldText;

        public void ToggleShopPanel()
        {
            if (merchantShopPanelGo == null)
            {
                GameObject canvas = GameObject.Find("Canvas");
                if (canvas != null)
                {
                    Transform shopTrans = canvas.transform.Find("MerchantShopPanel");
                    if (shopTrans != null)
                    {
                        merchantShopPanelGo = shopTrans.gameObject;
                        Transform goldStatusTrans = shopTrans.Find("ShopContainer/GoldStatus");
                        if (goldStatusTrans != null)
                        {
                            merchantShopGoldText = goldStatusTrans.GetComponent<Text>();
                        }
                    }
                }
            }

            if (merchantShopPanelGo != null)
            {
                bool active = !merchantShopPanelGo.activeSelf;
                merchantShopPanelGo.SetActive(active);
                if (active)
                {
                    UpdateMerchantShopGold();
                }
            }
        }

        public void CloseShopPanel()
        {
            Debug.Log("[Pulsevania UI] Safe closing shop panel...");

            if (merchantShopPanelGo != null)
            {
                merchantShopPanelGo.SetActive(false);
                ResetSellMode();
                return;
            }

            Canvas mainCanvas = GameObject.FindObjectOfType<Canvas>();
            if (mainCanvas != null)
            {
                Transform shopPanelTransform = mainCanvas.transform.Find("MerchantShopPanel");
                if (shopPanelTransform != null)
                {
                    merchantShopPanelGo = shopPanelTransform.gameObject;
                    merchantShopPanelGo.SetActive(false);
                    ResetSellMode();
                }
            }
        }

        public void UpdateMerchantShopGold()
        {
            if (merchantShopGoldText != null && GameManager.Instance != null)
            {
                bool isTR = PlayerPrefs.GetString("GameLanguage", "Turkish") == "Turkish";
                merchantShopGoldText.text = isTR ? $"ALTININIZ: {GameManager.Instance.CurrentGold}" : $"YOUR GOLD: {GameManager.Instance.CurrentGold}";
            }
        }

        public void GoldKazanButtonAction()
        {
            if (IsGoldAdOnCooldown(out double _))
            {
                return;
            }

            if (Pulsevania.Core.AdManager.Instance != null)
            {
                Pulsevania.Core.AdManager.Instance.ShowRewardedInterstitialAd(() =>
                {
                    OnGoldAdWatchedSuccess();
                }, () =>
                {
                    Debug.Log("[UIManager] Gold rewarded interstitial ad not completed.");
                });
            }
            else
            {
                // Fallback (for safety in editor/development if ads fail to load)
                OnGoldAdWatchedSuccess();
            }
        }

        private void OnGoldAdWatchedSuccess()
        {
            if (GameManager.Instance != null)
            {
                int newGold = GameManager.Instance.CurrentGold + 100;
                GameManager.Instance.SetGold(newGold);
                UpdateMerchantShopGold();
            }

            int currentCount = PlayerPrefs.GetInt("Pulsevania_GoldAdWatchCount", 0);
            currentCount++;
            PlayerPrefs.SetInt("Pulsevania_GoldAdWatchCount", currentCount);

            if (currentCount >= 5)
            {
                PlayerPrefs.SetString("Pulsevania_GoldAdCooldownStartTime", System.DateTime.UtcNow.ToString());
            }
            PlayerPrefs.Save();

            UpdateGoldKazanButtonText();
        }

        private bool IsGoldAdOnCooldown(out double remainingMinutes)
        {
            remainingMinutes = 0;
            int watchCount = PlayerPrefs.GetInt("Pulsevania_GoldAdWatchCount", 0);
            if (watchCount < 5)
            {
                return false;
            }

            string cooldownStartStr = PlayerPrefs.GetString("Pulsevania_GoldAdCooldownStartTime", "");
            if (string.IsNullOrEmpty(cooldownStartStr))
            {
                PlayerPrefs.SetInt("Pulsevania_GoldAdWatchCount", 0);
                return false;
            }

            if (System.DateTime.TryParse(cooldownStartStr, out System.DateTime cooldownStart))
            {
                System.TimeSpan elapsed = System.DateTime.UtcNow - cooldownStart;
                double elapsedMinutes = elapsed.TotalMinutes;
                if (elapsedMinutes >= 120.0) // 120 minutes cooldown
                {
                    PlayerPrefs.SetInt("Pulsevania_GoldAdWatchCount", 0);
                    PlayerPrefs.DeleteKey("Pulsevania_GoldAdCooldownStartTime");
                    PlayerPrefs.Save();
                    return false;
                }
                else
                {
                    remainingMinutes = 120.0 - elapsedMinutes;
                    return true;
                }
            }

            return false;
        }

        private void UpdateGoldKazanButtonText()
        {
            if (merchantShopPanelGo == null) return;
            Transform window = merchantShopPanelGo.transform.Find("ShopWindow");
            if (window == null) return;
            Transform gkBtnTrans = window.Find("Btn_GoldKazan");
            if (gkBtnTrans == null) return;

            Text t = gkBtnTrans.GetComponentInChildren<Text>();
            if (t == null) return;

            Button btn = gkBtnTrans.GetComponent<Button>();
            Image img = gkBtnTrans.GetComponent<Image>();

            bool isTR = currentLanguage == GameLanguage.Turkish;
            
            if (IsGoldAdOnCooldown(out double remainingMinutes))
            {
                if (btn != null) btn.interactable = false;
                if (img != null) img.color = Color.gray;

                System.TimeSpan remaining = System.TimeSpan.FromMinutes(remainingMinutes);
                string waitText = string.Format("{0:D2}:{1:D2}:{2:D2}", remaining.Hours, remaining.Minutes, remaining.Seconds);
                t.text = isTR ? $"BEKLE: {waitText}" : $"WAIT: {waitText}";
            }
            else
            {
                if (btn != null) btn.interactable = true;
                if (img != null) img.color = new Color(0.18f, 0.54f, 0.34f, 1f); // SeaGreen

                int currentCount = PlayerPrefs.GetInt("Pulsevania_GoldAdWatchCount", 0);
                t.text = isTR 
                    ? $"GOLD KAZAN ({currentCount}/5)" 
                    : $"GET GOLD ({currentCount}/5)";
            }
        }

        private void CreateMerchantShopUI(GameObject canvas)
        {
            if (canvas == null) return;

            // 1. Destroy old shop panel if any
            Transform oldShop = canvas.transform.Find("MerchantShopPanel");
            if (oldShop != null)
            {
                if (Application.isPlaying) Destroy(oldShop.gameObject);
                else DestroyImmediate(oldShop.gameObject);
            }

            // 2. Backdrop
            GameObject backdropGo = new GameObject("MerchantShopPanel");
            backdropGo.transform.SetParent(canvas.transform, false);
            RectTransform rtBackdrop = backdropGo.AddComponent<RectTransform>();
            rtBackdrop.anchorMin = Vector2.zero;
            rtBackdrop.anchorMax = Vector2.one;
            rtBackdrop.sizeDelta = Vector2.zero;
            rtBackdrop.anchoredPosition = Vector2.zero;

            Image backdropImg = backdropGo.AddComponent<Image>();
            backdropImg.color = new Color(0f, 0f, 0f, 0.7f);

            Button backdropBtn = backdropGo.AddComponent<Button>();
            backdropBtn.onClick.AddListener(CloseShopPanel);

            // 3. Container
            GameObject containerGo = new GameObject("ShopContainer");
            containerGo.transform.SetParent(backdropGo.transform, false);
            RectTransform rtContainer = containerGo.AddComponent<RectTransform>();
            rtContainer.anchorMin = new Vector2(0.5f, 0.5f);
            rtContainer.anchorMax = new Vector2(0.5f, 0.5f);
            rtContainer.pivot = new Vector2(0.5f, 0.5f);
            rtContainer.anchoredPosition = Vector2.zero;
            rtContainer.sizeDelta = new Vector2(900f, 540f);

            Image containerImg = containerGo.AddComponent<Image>();
            containerImg.color = new Color(0.08f, 0.08f, 0.12f, 0.98f);

            Button blockBtn = containerGo.AddComponent<Button>();
            blockBtn.transition = Selectable.Transition.None;

            // Title
            GameObject titleGo = new GameObject("Title");
            titleGo.transform.SetParent(containerGo.transform, false);
            RectTransform rtTitle = titleGo.AddComponent<RectTransform>();
            rtTitle.anchorMin = new Vector2(0.5f, 1f);
            rtTitle.anchorMax = new Vector2(0.5f, 1f);
            rtTitle.pivot = new Vector2(0.5f, 1f);
            rtTitle.anchoredPosition = new Vector2(0f, -20f);
            rtTitle.sizeDelta = new Vector2(400f, 40f);

            bool isTR = PlayerPrefs.GetString("GameLanguage", "Turkish") == "Turkish";
            Text titleTxt = titleGo.AddComponent<Text>();
            titleTxt.text = isTR ? "SATICI MARKETİ" : "MERCHANT SHOP";
            titleTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleTxt.fontSize = 24;
            titleTxt.alignment = TextAnchor.MiddleCenter;
            titleTxt.color = Color.yellow;
            titleTxt.fontStyle = FontStyle.Bold;

            // Gold Status
            GameObject goldGo = new GameObject("GoldStatus");
            goldGo.transform.SetParent(containerGo.transform, false);
            RectTransform rtGold = goldGo.AddComponent<RectTransform>();
            rtGold.anchorMin = new Vector2(0.5f, 1f);
            rtGold.anchorMax = new Vector2(0.5f, 1f);
            rtGold.pivot = new Vector2(0.5f, 1f);
            rtGold.anchoredPosition = new Vector2(0f, -60f);
            rtGold.sizeDelta = new Vector2(400f, 30f);

            merchantShopGoldText = goldGo.AddComponent<Text>();
            merchantShopGoldText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            merchantShopGoldText.fontSize = 16;
            merchantShopGoldText.alignment = TextAnchor.MiddleCenter;
            merchantShopGoldText.color = Color.white;
            merchantShopGoldText.fontStyle = FontStyle.Bold;

            // Close Button
            GameObject closeGo = new GameObject("Btn_Close");
            closeGo.transform.SetParent(containerGo.transform, false);
            RectTransform rtClose = closeGo.AddComponent<RectTransform>();
            rtClose.anchorMin = new Vector2(1f, 1f);
            rtClose.anchorMax = new Vector2(1f, 1f);
            rtClose.pivot = new Vector2(1f, 1f);
            rtClose.anchoredPosition = new Vector2(-20f, -20f);
            rtClose.sizeDelta = new Vector2(30f, 30f);

            Image closeImg = closeGo.AddComponent<Image>();
            closeImg.color = Color.red;

            Button closeBtn = closeGo.AddComponent<Button>();
            closeBtn.onClick.AddListener(CloseShopPanel);

            GameObject closeTextGo = new GameObject("Text");
            closeTextGo.transform.SetParent(closeGo.transform, false);
            RectTransform rtCloseTxt = closeTextGo.AddComponent<RectTransform>();
            rtCloseTxt.anchorMin = Vector2.zero;
            rtCloseTxt.anchorMax = Vector2.one;
            rtCloseTxt.sizeDelta = Vector2.zero;
            rtCloseTxt.anchoredPosition = Vector2.zero;

            Text closeTxt = closeTextGo.AddComponent<Text>();
            closeTxt.text = "X";
            closeTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            closeTxt.fontSize = 16;
            closeTxt.alignment = TextAnchor.MiddleCenter;
            closeTxt.color = Color.white;
            closeTxt.fontStyle = FontStyle.Bold;

            // Kural 3: Market Gold Kazan Butonu (Rewarded Interstitial)
            GameObject goldKazanGo = new GameObject("Btn_GoldKazan");
            goldKazanGo.transform.SetParent(containerGo.transform, false);
            RectTransform rtGoldKazan = goldKazanGo.AddComponent<RectTransform>();
            rtGoldKazan.anchorMin = new Vector2(0f, 1f);
            rtGoldKazan.anchorMax = new Vector2(0f, 1f);
            rtGoldKazan.pivot = new Vector2(0f, 1f);
            rtGoldKazan.anchoredPosition = new Vector2(20f, -20f);
            rtGoldKazan.sizeDelta = new Vector2(140f, 35f);

            Image goldKazanImg = goldKazanGo.AddComponent<Image>();
            goldKazanImg.color = new Color(0.18f, 0.54f, 0.34f, 1.0f); // SeaGreen

            Button goldKazanBtn = goldKazanGo.AddComponent<Button>();
            goldKazanBtn.onClick.AddListener(GoldKazanButtonAction);

            GameObject gkTextGo = new GameObject("Text");
            gkTextGo.transform.SetParent(goldKazanGo.transform, false);
            RectTransform rtGkTxt = gkTextGo.AddComponent<RectTransform>();
            rtGkTxt.anchorMin = Vector2.zero;
            rtGkTxt.anchorMax = Vector2.one;
            rtGkTxt.sizeDelta = Vector2.zero;
            rtGkTxt.anchoredPosition = Vector2.zero;

            Text gkTxt = gkTextGo.AddComponent<Text>();
            gkTxt.text = isTR ? "GOLD KAZAN" : "GET GOLD";
            gkTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            gkTxt.fontSize = 14;
            gkTxt.alignment = TextAnchor.MiddleCenter;
            gkTxt.color = Color.white;
            gkTxt.fontStyle = FontStyle.Bold;

            // Grid Layout for 24 items
            GameObject gridGo = new GameObject("ItemsGrid");
            gridGo.transform.SetParent(containerGo.transform, false);
            RectTransform rtGrid = gridGo.AddComponent<RectTransform>();
            rtGrid.anchorMin = new Vector2(0.5f, 0f);
            rtGrid.anchorMax = new Vector2(0.5f, 0f);
            rtGrid.pivot = new Vector2(0.5f, 0f);
            rtGrid.anchoredPosition = new Vector2(0f, 30f);
            rtGrid.sizeDelta = new Vector2(850f, 410f);

            GridLayoutGroup grid = gridGo.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(95f, 125f);
            grid.spacing = new Vector2(10f, 10f);
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 8;

            // Populate items from InventoryManager database
            if (InventoryManager.Instance != null)
            {
                List<ItemData> clothes = new List<ItemData>();
                List<ItemData> weapons = new List<ItemData>();
                foreach (ItemData item in InventoryManager.Instance.itemDatabase)
                {
                    if (item.equipSlot == EquipSlot.Head ||
                        item.equipSlot == EquipSlot.Chest ||
                        item.equipSlot == EquipSlot.Hands ||
                        item.equipSlot == EquipSlot.Legs ||
                        item.equipSlot == EquipSlot.Feet)
                    {
                        clothes.Add(item);
                    }
                    else
                    {
                        weapons.Add(item);
                    }
                }

                List<ItemData> sortedItems = new List<ItemData>();
                sortedItems.AddRange(clothes);
                sortedItems.AddRange(weapons);

                foreach (ItemData item in sortedItems)
                {
                    GameObject slotGo = new GameObject(item.itemName + "_Slot");
                    slotGo.transform.SetParent(gridGo.transform, false);

                    Image slotImg = slotGo.AddComponent<Image>();
                    slotImg.color = new Color(0.18f, 0.18f, 0.22f, 0.8f);

                    Button slotBtn = slotGo.AddComponent<Button>();
                    slotBtn.onClick.AddListener(() => BuyItem(item));

                    // Icon
                    GameObject iconGo = new GameObject("Icon");
                    iconGo.transform.SetParent(slotGo.transform, false);
                    RectTransform rtIcon = iconGo.AddComponent<RectTransform>();
                    rtIcon.anchorMin = new Vector2(0.5f, 1f);
                    rtIcon.anchorMax = new Vector2(0.5f, 1f);
                    rtIcon.pivot = new Vector2(0.5f, 1f);
                    rtIcon.anchoredPosition = new Vector2(0f, -10f);
                    rtIcon.sizeDelta = new Vector2(50f, 50f);

                    Image iconImg = iconGo.AddComponent<Image>();
                    iconImg.sprite = item.icon;
                    iconImg.color = Color.white;

                    // Name
                    GameObject nameGo = new GameObject("Name");
                    nameGo.transform.SetParent(slotGo.transform, false);
                    RectTransform rtName = nameGo.AddComponent<RectTransform>();
                    rtName.anchorMin = new Vector2(0.5f, 0f);
                    rtName.anchorMax = new Vector2(0.5f, 0f);
                    rtName.pivot = new Vector2(0.5f, 0f);
                    rtName.anchoredPosition = new Vector2(0f, 32f); // Restored alignment
                    rtName.sizeDelta = new Vector2(95f, 35f); // Bound exactly to cell width to prevent overlap

                    Text nameTxt = nameGo.AddComponent<Text>();
                    nameTxt.text = GetLocalizedItemName(item.itemName, currentLanguage == GameLanguage.Turkish);
                    nameTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    nameTxt.fontSize = 16; // Optimized size for legibility without overlap
                    nameTxt.alignment = TextAnchor.MiddleCenter;
                    nameTxt.color = Color.white;
                    nameTxt.resizeTextForBestFit = true;
                    nameTxt.resizeTextMinSize = 10;
                    nameTxt.resizeTextMaxSize = 16; // Clamp to 16

                    // Price
                    GameObject priceGo = new GameObject("Price");
                    priceGo.transform.SetParent(slotGo.transform, false);
                    RectTransform rtPrice = priceGo.AddComponent<RectTransform>();
                    rtPrice.anchorMin = new Vector2(0.5f, 0f);
                    rtPrice.anchorMax = new Vector2(0.5f, 0f);
                    rtPrice.pivot = new Vector2(0.5f, 0f);
                    rtPrice.anchoredPosition = new Vector2(0f, 8f);
                    rtPrice.sizeDelta = new Vector2(95f, 22f); // Bound exactly to cell width to prevent overlap

                    Text priceTxt = priceGo.AddComponent<Text>();
                    priceTxt.text = $"{item.goldPrice} G";
                    priceTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    priceTxt.fontSize = 13; // Optimized price size to match
                    priceTxt.alignment = TextAnchor.MiddleCenter;
                    priceTxt.color = Color.yellow;
                    priceTxt.fontStyle = FontStyle.Bold;
                }
            }

            merchantShopPanelGo = backdropGo;
            merchantShopPanelGo.SetActive(false);
        }

        private void BuyItem(ItemData item)
        {
            if (item == null) return;

            // Health Potion limit control (Inventory + Cart cannot exceed 10)
            if (item.itemName == "Health Potion (Can Potu)")
            {
                int invCount = InventoryManager.Instance != null ? InventoryManager.Instance.GetTotalPotionCount() : 0;
                int cartCount = shopCart.ContainsKey(item) ? shopCart[item] : 0;
                if (invCount + cartCount >= 10)
                {
                    ShowShopWarning("Maksimum 10 can iksiri taşıyabilirsiniz!");
                    if (DamageTextPool.Instance != null && activePlayer != null)
                    {
                        DamageTextPool.Instance.SpawnText(activePlayer.transform.position + Vector3.up, "Max 10 Pot!", Color.red);
                    }
                    return;
                }
            }

            // Increment quantity in cart
            if (shopCart.ContainsKey(item))
            {
                shopCart[item]++;
            }
            else
            {
                shopCart[item] = 1;
            }

            // Play small confirmation sound
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(SoundEffect.CoinPickup);
            }

            // Spawn floating text confirmation at player position
            if (DamageTextPool.Instance != null && activePlayer != null)
            {
                string localizedName = GetLocalizedItemName(item.itemName, currentLanguage == GameLanguage.Turkish);
                string popupText = currentLanguage == GameLanguage.Turkish 
                    ? $"+1 {localizedName} Sepete Eklendi!" 
                    : $"+1 {localizedName} Added to Cart!";
                DamageTextPool.Instance.SpawnText(activePlayer.transform.position + Vector3.up, popupText, Color.green);
            }

            // Update cart button count text
            UpdateCartButtonText();
        }

        private void UpdateCartButtonText()
        {
            if (btnCartText == null) return;

            bool isTR = currentLanguage == GameLanguage.Turkish;

            if (isCartMode)
            {
                btnCartText.text = isTR ? "MARKETE DÖN" : "BACK TO SHOP";
            }
            else
            {
                int totalCount = 0;
                foreach (var kv in shopCart)
                {
                    totalCount += kv.Value;
                }
                btnCartText.text = isTR ? $"SEPET ({totalCount})" : $"CART ({totalCount})";
            }
        }

        private bool isSellMode = false;
        private GameObject shopItemsGridGo;
        private GameObject shopSellGridGo;
        private Text btnSellModeText;

        public void ResetSellMode()
        {
            isSellMode = false;
            isCartMode = false;
            shopCart.Clear();
            if (shopItemsGridGo != null) shopItemsGridGo.SetActive(true);
            if (shopSellGridGo != null) shopSellGridGo.SetActive(false);
            if (shopCartGridGo != null) shopCartGridGo.SetActive(false);
            if (btnSellModeText != null) btnSellModeText.text = currentLanguage == GameLanguage.Turkish ? "EŞYA SAT" : "SELL ITEMS";
            if (btnCheckoutGo != null) btnCheckoutGo.SetActive(false);
            UpdateCartButtonText();
        }

        public void ToggleSellMode()
        {
            isSellMode = !isSellMode;
            if (shopItemsGridGo != null) shopItemsGridGo.SetActive(!isSellMode);
            if (shopSellGridGo != null) shopSellGridGo.SetActive(isSellMode);

            if (btnSellModeText != null)
            {
                bool isTR = currentLanguage == GameLanguage.Turkish;
                btnSellModeText.text = isSellMode 
                    ? (isTR ? "MARKETE DÖN" : "BACK TO BUY") 
                    : (isTR ? "EŞYA SAT" : "SELL ITEMS");
            }

            Canvas mainCanvas = GameObject.FindObjectOfType<Canvas>();
            if (mainCanvas != null)
            {
                Transform scrollT = mainCanvas.transform.Find("MerchantShopPanel/ShopWindow/ShopScrollView") ?? mainCanvas.transform.Find("MerchantShopPanel(Clone)/ShopWindow/ShopScrollView");
                if (scrollT != null)
                {
                    ScrollRect scrollRect = scrollT.GetComponent<ScrollRect>();
                    if (scrollRect != null)
                    {
                        if (isSellMode && shopSellGridGo != null)
                            scrollRect.content = shopSellGridGo.GetComponent<RectTransform>();
                        else if (!isSellMode && shopItemsGridGo != null)
                            scrollRect.content = shopItemsGridGo.GetComponent<RectTransform>();
                    }
                }
            }

            if (isSellMode)
            {
                UpdateShopSellGrid();
            }
        }

        public void ToggleCartMode()
        {
            if (isSellMode)
            {
                ToggleSellMode(); // Disable sell mode
            }

            isCartMode = !isCartMode;

            if (shopItemsGridGo != null) shopItemsGridGo.SetActive(!isCartMode);
            if (shopSellGridGo != null) shopSellGridGo.SetActive(false);
            if (shopCartGridGo != null)
            {
                shopCartGridGo.SetActive(isCartMode);
                if (isCartMode)
                {
                    PopulateCartGrid();
                }
            }

            // Sync scroll content rect to the active grid
            Canvas mainCanvas = GameObject.FindObjectOfType<Canvas>();
            if (mainCanvas != null)
            {
                Transform scrollT = mainCanvas.transform.Find("MerchantShopPanel/ShopWindow/ShopScrollView") ?? mainCanvas.transform.Find("MerchantShopPanel(Clone)/ShopWindow/ShopScrollView");
                if (scrollT != null)
                {
                    ScrollRect scrollRect = scrollT.GetComponent<ScrollRect>();
                    if (scrollRect != null)
                    {
                        if (isCartMode && shopCartGridGo != null)
                            scrollRect.content = shopCartGridGo.GetComponent<RectTransform>();
                        else if (!isCartMode && shopItemsGridGo != null)
                            scrollRect.content = shopItemsGridGo.GetComponent<RectTransform>();
                    }
                }
            }

            // Toggle checkout button visibility based on cart mode
            if (btnCheckoutGo != null)
            {
                btnCheckoutGo.SetActive(isCartMode);
            }

            // Hide/Show Sell button when in cart mode
            if (mainCanvas != null)
            {
                Transform sellBtnT = mainCanvas.transform.Find("MerchantShopPanel/ShopWindow/Btn_SellMode") ?? mainCanvas.transform.Find("MerchantShopPanel(Clone)/ShopWindow/Btn_SellMode");
                if (sellBtnT != null)
                {
                    sellBtnT.gameObject.SetActive(!isCartMode);
                }
            }

            UpdateCartButtonText();
            UpdateCartGoldStatusText();
        }

        private void UpdateCartGoldStatusText()
        {
            if (merchantShopGoldText == null) return;

            bool isTR = currentLanguage == GameLanguage.Turkish;

            if (isCartMode)
            {
                int totalCost = CalculateCartTotal();
                merchantShopGoldText.text = isTR 
                    ? $"ALTININIZ: {GameManager.Instance.CurrentGold} / TOPLAM: {totalCost} Altın"
                    : $"YOUR GOLD: {GameManager.Instance.CurrentGold} / TOTAL: {totalCost} G";
            }
            else
            {
                merchantShopGoldText.text = isTR 
                    ? $"ALTININIZ: {GameManager.Instance.CurrentGold}"
                    : $"YOUR GOLD: {GameManager.Instance.CurrentGold}";
            }
        }

        private int CalculateCartTotal()
        {
            int total = 0;
            foreach (var kv in shopCart)
            {
                total += kv.Key.goldPrice * kv.Value;
            }
            return total;
        }

        private void PopulateCartGrid()
        {
            if (shopCartGridGo == null) return;

            // Clear old rows in CartGrid
            foreach (Transform child in shopCartGridGo.transform)
            {
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }

            if (shopCart.Count == 0)
            {
                // Display empty cart placeholder row
                GameObject emptyGo = new GameObject("EmptyPlaceholder");
                emptyGo.transform.SetParent(shopCartGridGo.transform, false);
                RectTransform rtEmpty = emptyGo.AddComponent<RectTransform>();
                rtEmpty.sizeDelta = new Vector2(980f, 100f);

                Text txt = emptyGo.AddComponent<Text>();
                txt.text = currentLanguage == GameLanguage.Turkish 
                    ? "Sepetiniz Boş! Ürün eklemek için markete dönün." 
                    : "Your Cart is Empty! Return to shop to add items.";
                txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                txt.fontSize = 24;
                txt.fontStyle = FontStyle.Normal;
                txt.alignment = TextAnchor.MiddleCenter;
                txt.color = Color.gray;
                return;
            }

            foreach (var kv in shopCart)
            {
                ItemData item = kv.Key;
                int qty = kv.Value;

                GameObject rowGo = new GameObject(item.itemName + "_CartRow");
                rowGo.transform.SetParent(shopCartGridGo.transform, false);

                Image rowImg = rowGo.AddComponent<Image>();
                rowImg.color = new Color(0.2f, 0.15f, 0.12f, 0.95f);

                RectTransform rtRow = rowGo.GetComponent<RectTransform>();
                rtRow.sizeDelta = new Vector2(980f, 100f);

                // Icon
                GameObject iconGo = new GameObject("Icon");
                iconGo.transform.SetParent(rowGo.transform, false);
                RectTransform rtIcon = iconGo.AddComponent<RectTransform>();
                rtIcon.anchorMin = new Vector2(0f, 0.5f);
                rtIcon.anchorMax = new Vector2(0f, 0.5f);
                rtIcon.pivot = new Vector2(0f, 0.5f);
                rtIcon.anchoredPosition = new Vector2(25f, 0f);
                rtIcon.sizeDelta = new Vector2(70f, 70f);

                Image iconImg = iconGo.AddComponent<Image>();
                iconImg.sprite = item.icon;

                // Name
                GameObject nameGo = new GameObject("Name");
                nameGo.transform.SetParent(rowGo.transform, false);
                RectTransform rtName = nameGo.AddComponent<RectTransform>();
                rtName.anchorMin = new Vector2(0f, 0.5f);
                rtName.anchorMax = new Vector2(0f, 0.5f);
                rtName.pivot = new Vector2(0f, 0.5f);
                rtName.anchoredPosition = new Vector2(110f, 0f);
                rtName.sizeDelta = new Vector2(300f, 40f);

                Text nameTxt = nameGo.AddComponent<Text>();
                nameTxt.text = GetLocalizedItemName(item.itemName, currentLanguage == GameLanguage.Turkish);
                nameTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                nameTxt.fontSize = 22;
                nameTxt.fontStyle = FontStyle.Bold;
                nameTxt.alignment = TextAnchor.MiddleLeft;
                nameTxt.color = Color.white;

                // Price Total Info
                GameObject priceGo = new GameObject("PriceInfo");
                priceGo.transform.SetParent(rowGo.transform, false);
                RectTransform rtPrice = priceGo.AddComponent<RectTransform>();
                rtPrice.anchorMin = new Vector2(0f, 0.5f);
                rtPrice.anchorMax = new Vector2(0f, 0.5f);
                rtPrice.pivot = new Vector2(0f, 0.5f);
                rtPrice.anchoredPosition = new Vector2(430f, 0f);
                rtPrice.sizeDelta = new Vector2(200f, 40f);

                Text priceTxt = priceGo.AddComponent<Text>();
                priceTxt.text = $"{item.goldPrice * qty} G ({item.goldPrice} G)";
                priceTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                priceTxt.fontSize = 20;
                priceTxt.alignment = TextAnchor.MiddleLeft;
                priceTxt.color = Color.yellow;

                // Quantity Controls Container
                GameObject ctrlGo = new GameObject("Controls");
                ctrlGo.transform.SetParent(rowGo.transform, false);
                RectTransform rtCtrl = ctrlGo.AddComponent<RectTransform>();
                rtCtrl.anchorMin = new Vector2(1f, 0.5f);
                rtCtrl.anchorMax = new Vector2(1f, 0.5f);
                rtCtrl.pivot = new Vector2(1f, 0.5f);
                rtCtrl.anchoredPosition = new Vector2(-25f, 0f);
                rtCtrl.sizeDelta = new Vector2(220f, 60f);

                // Minus Button (-)
                GameObject minGo = new GameObject("MinusBtn");
                minGo.transform.SetParent(ctrlGo.transform, false);
                RectTransform rtMin = minGo.AddComponent<RectTransform>();
                rtMin.anchorMin = new Vector2(0f, 0.5f);
                rtMin.anchorMax = new Vector2(0f, 0.5f);
                rtMin.pivot = new Vector2(0f, 0.5f);
                rtMin.anchoredPosition = new Vector2(0f, 0f);
                rtMin.sizeDelta = new Vector2(50f, 50f);

                Image minImg = minGo.AddComponent<Image>();
                minImg.color = new Color(0.35f, 0.15f, 0.15f, 1f); // Sleek red subtract theme
                Button minBtn = minGo.AddComponent<Button>();
                minBtn.onClick.AddListener(() => ModifyCartQuantity(item, -1));

                GameObject minTxtGo = new GameObject("Text");
                minTxtGo.transform.SetParent(minGo.transform, false);
                RectTransform rtMinTxt = minTxtGo.AddComponent<RectTransform>();
                rtMinTxt.anchorMin = Vector2.zero;
                rtMinTxt.anchorMax = Vector2.one;
                rtMinTxt.sizeDelta = Vector2.zero;
                Text minTxt = minTxtGo.AddComponent<Text>();
                minTxt.text = "-";
                minTxt.raycastTarget = false;
                minTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                minTxt.fontSize = 28;
                minTxt.fontStyle = FontStyle.Bold;
                minTxt.alignment = TextAnchor.MiddleCenter;
                minTxt.color = Color.white;

                // Quantity Display
                GameObject qtyGo = new GameObject("QtyText");
                qtyGo.transform.SetParent(ctrlGo.transform, false);
                RectTransform rtQty = qtyGo.AddComponent<RectTransform>();
                rtQty.anchorMin = new Vector2(0.5f, 0.5f);
                rtQty.anchorMax = new Vector2(0.5f, 0.5f);
                rtQty.pivot = new Vector2(0.5f, 0.5f);
                rtQty.anchoredPosition = new Vector2(0f, 0f);
                rtQty.sizeDelta = new Vector2(80f, 40f);

                Text qtyTxt = qtyGo.AddComponent<Text>();
                qtyTxt.text = qty.ToString();
                qtyTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                qtyTxt.fontSize = 24;
                qtyTxt.fontStyle = FontStyle.Bold;
                qtyTxt.alignment = TextAnchor.MiddleCenter;
                qtyTxt.color = Color.white;

                // Plus Button (+)
                GameObject plusGo = new GameObject("PlusBtn");
                plusGo.transform.SetParent(ctrlGo.transform, false);
                RectTransform rtPlus = plusGo.AddComponent<RectTransform>();
                rtPlus.anchorMin = new Vector2(1f, 0.5f);
                rtPlus.anchorMax = new Vector2(1f, 0.5f);
                rtPlus.pivot = new Vector2(1f, 0.5f);
                rtPlus.anchoredPosition = new Vector2(0f, 0f);
                rtPlus.sizeDelta = new Vector2(50f, 50f);

                Image plusImg = plusGo.AddComponent<Image>();
                plusImg.color = new Color(0.15f, 0.35f, 0.15f, 1f); // Sleek green add theme
                Button plusBtn = plusGo.AddComponent<Button>();
                plusBtn.onClick.AddListener(() => ModifyCartQuantity(item, 1));

                GameObject plusTxtGo = new GameObject("Text");
                plusTxtGo.transform.SetParent(plusGo.transform, false);
                RectTransform rtPlusTxt = plusTxtGo.AddComponent<RectTransform>();
                rtPlusTxt.anchorMin = Vector2.zero;
                rtPlusTxt.anchorMax = Vector2.one;
                rtPlusTxt.sizeDelta = Vector2.zero;
                Text plusTxt = plusTxtGo.AddComponent<Text>();
                plusTxt.text = "+";
                plusTxt.raycastTarget = false;
                plusTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                plusTxt.fontSize = 28;
                plusTxt.fontStyle = FontStyle.Bold;
                plusTxt.alignment = TextAnchor.MiddleCenter;
                plusTxt.color = Color.white;
            }
        }

        private void ModifyCartQuantity(ItemData item, int delta)
        {
            if (item == null) return;

            // Health Potion limit control on increment
            if (delta > 0 && item.itemName == "Health Potion (Can Potu)")
            {
                int invCount = InventoryManager.Instance != null ? InventoryManager.Instance.GetTotalPotionCount() : 0;
                int cartCount = shopCart.ContainsKey(item) ? shopCart[item] : 0;
                if (invCount + cartCount + delta > 10)
                {
                    ShowShopWarning("Maksimum 10 can iksiri taşıyabilirsiniz!");
                    if (DamageTextPool.Instance != null && activePlayer != null)
                    {
                        DamageTextPool.Instance.SpawnText(activePlayer.transform.position + Vector3.up, "Max 10 Pot!", Color.red);
                    }
                    return;
                }
            }

            if (shopCart.ContainsKey(item))
            {
                shopCart[item] += delta;
                if (shopCart[item] <= 0)
                {
                    shopCart.Remove(item);
                }
            }

            // Play small confirmation sound
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(SoundEffect.CoinPickup);
            }

            // Redraw Cart list and update totals
            PopulateCartGrid();
            UpdateCartButtonText();
            UpdateCartGoldStatusText();
        }

        private void CheckoutCart()
        {
            if (shopCart.Count == 0) return;
            if (InventoryManager.Instance == null) return;
            
            GameObject player = GameObject.FindWithTag("Player");
            if (player == null) return;
            PlayerController pc = player.GetComponent<PlayerController>();
            if (pc == null) return;

            int totalCost = CalculateCartTotal();
            bool isTR = currentLanguage == GameLanguage.Turkish;

            // 1. Check Gold
            if (GameManager.Instance.CurrentGold < totalCost)
            {
                ShowShopWarning(isTR ? "Yetersiz Bakiye! Alışveriş için yeterli altınınız yok." : "Insufficient Balance! You do not have enough gold for purchase.");
                if (DamageTextPool.Instance != null)
                {
                    DamageTextPool.Instance.SpawnText(player.transform.position + Vector3.up, isTR ? "Yetersiz Altın!" : "Not Enough Gold!", Color.red);
                }
                return;
            }

            // 2. Validate Extra Heart Limit and Inventory Slots
            int requiredSlots = 0;
            int cartPotionQty = 0;
            foreach (var kv in shopCart)
            {
                ItemData item = kv.Key;
                int qty = kv.Value;

                if (item.itemName == "Health Potion (Can Potu)")
                {
                    cartPotionQty += qty;
                }

                if (item.itemName == "Extra Heart")
                {
                    if (pc.extraHearts + qty > 3)
                    {
                        ShowShopWarning(isTR ? "Maksimum yedek can sınırına (3) ulaştınız!" : "You have reached the maximum spare hearts limit (3)!");
                        if (DamageTextPool.Instance != null)
                        {
                            DamageTextPool.Instance.SpawnText(player.transform.position + Vector3.up, isTR ? "Maks Yedek Can (3)!" : "Max Extra Hearts (3)!", Color.red);
                        }
                        return;
                    }
                }
                else if (item.itemName == "Health Potion (Can Potu)")
                {
                    // Can iksiri tek slotta birikir. Eger envanterde hic yoksa sadece 1 slot gerekir. Varsa 0 slot gerekir.
                    bool alreadyHasPotion = InventoryManager.Instance.inventoryItems.Exists(x => x.itemName == "Health Potion (Can Potu)");
                    if (!alreadyHasPotion)
                    {
                        requiredSlots += 1;
                    }
                }
                else
                {
                    // For regular items, calculate slot capacity
                    bool canStack = InventoryManager.Instance.inventoryItems.Exists(x => x.itemName == item.itemName);
                    if (!canStack)
                    {
                        requiredSlots += qty;
                    }
                }
            }

            // Potion threshold check before gold subtraction
            if (cartPotionQty > 0)
            {
                int currentPotCount = InventoryManager.Instance.GetTotalPotionCount();
                if (currentPotCount + cartPotionQty > 10)
                {
                    ShowShopWarning(isTR ? "Maksimum 10 can iksiri taşıyabilirsiniz!" : "You can carry a maximum of 10 health potions!");
                    if (DamageTextPool.Instance != null)
                    {
                        DamageTextPool.Instance.SpawnText(player.transform.position + Vector3.up, isTR ? "Maks 10 Pot!" : "Max 10 Potions!", Color.red);
                    }
                    return;
                }
            }

            int freeSlots = 8 - InventoryManager.Instance.inventoryItems.Count;
            if (freeSlots < requiredSlots)
            {
                ShowShopWarning(isTR ? "Envanter Dolu! Lütfen bazı eşyaları satın veya çöpe atın." : "Inventory Full! Please sell or discard some items.");
                if (DamageTextPool.Instance != null)
                {
                    DamageTextPool.Instance.SpawnText(player.transform.position + Vector3.up, isTR ? "Envanter Dolu!" : "Inventory Full!", Color.red);
                }
                return;
            }

            // 3. Process checkout
            if (GameManager.Instance.ConsumeGold(totalCost))
            {
                foreach (var kv in shopCart)
                {
                    ItemData item = kv.Key;
                    int qty = kv.Value;

                    if (item.itemName == "Extra Heart")
                    {
                        pc.extraHearts += qty;
                        pc.UpdateHeartsUI();
                    }
                    else
                    {
                        for (int i = 0; i < qty; i++)
                        {
                            ItemData addCopy = new ItemData(item.itemName, item.equipSlot, item.icon, item.equippedSprite, item.goldPrice, item.statType, item.statValue);
                            addCopy.count = 1;
                            InventoryManager.Instance.AddItem(addCopy);
                        }
                    }
                }

                // Checkout success! Clear cart
                shopCart.Clear();

                // Go back to buy mode
                ToggleCartMode();

                // Sound & Text
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlaySFX(SoundEffect.CoinPickup);
                }
                if (DamageTextPool.Instance != null)
                {
                    DamageTextPool.Instance.SpawnText(player.transform.position + Vector3.up, "Satin Alim Basarili!", Color.green);
                }

                UpdateMerchantShopGold();
                UpdateInventoryUI();
            }
        }

        public void UpdateShopSellGrid()
        {
            if (shopSellGridGo == null) return;

            // Clear old slot objects
            foreach (Transform child in shopSellGridGo.transform)
            {
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }

            if (InventoryManager.Instance == null) return;

            int maxSlots = 8; // Exactly 8 backpack slots matching character inventory

            for (int i = 0; i < maxSlots; i++)
            {
                int index = i;
                GameObject slotGo = new GameObject($"SellSlot_{index}");
                slotGo.transform.SetParent(shopSellGridGo.transform, false);

                Image slotImg = slotGo.AddComponent<Image>();
                slotImg.color = new Color(0.25f, 0.15f, 0.12f, 0.95f);

                if (index < InventoryManager.Instance.inventoryItems.Count)
                {
                    ItemData item = InventoryManager.Instance.inventoryItems[index];

                    Button slotBtn = slotGo.AddComponent<Button>();
                    slotBtn.onClick.AddListener(() => SellItem(item, index));

                    // Icon
                    GameObject iconGo = new GameObject("Icon");
                    iconGo.transform.SetParent(slotGo.transform, false);
                    RectTransform rtIcon = iconGo.AddComponent<RectTransform>();
                    rtIcon.anchorMin = new Vector2(0.5f, 0.5f);
                    rtIcon.anchorMax = new Vector2(0.5f, 0.5f);
                    rtIcon.pivot = new Vector2(0.5f, 0.5f);
                    rtIcon.anchoredPosition = new Vector2(0f, 10f); // slightly up to leave room for price at bottom
                    rtIcon.sizeDelta = new Vector2(64f, 64f);

                    Image iconImg = iconGo.AddComponent<Image>();
                    iconImg.sprite = item.icon;
                    iconImg.color = Color.white;
                    iconImg.raycastTarget = false;

                    // Quantity count if stackable/count > 1
                    if (item.count > 1)
                    {
                        GameObject countGo = new GameObject("Count");
                        countGo.transform.SetParent(slotGo.transform, false);
                        RectTransform countRt = countGo.AddComponent<RectTransform>();
                        countRt.anchorMin = new Vector2(0f, 0f);
                        countRt.anchorMax = new Vector2(1f, 1f);
                        countRt.anchoredPosition = new Vector2(0f, 0f);
                        countRt.sizeDelta = new Vector2(-10f, -10f);

                        Text countTxt = countGo.AddComponent<Text>();
                        countTxt.text = $"x{item.count}";
                        countTxt.raycastTarget = false;
                        countTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                        countTxt.fontSize = 14;
                        countTxt.fontStyle = FontStyle.Bold;
                        countTxt.alignment = TextAnchor.LowerRight;
                        countTxt.color = Color.white;

                        Outline countOutline = countGo.AddComponent<Outline>();
                        countOutline.effectColor = Color.black;
                        countOutline.effectDistance = new Vector2(1f, 1f);
                    }

                    // Sell Value
                    GameObject priceGo = new GameObject("SellValue");
                    priceGo.transform.SetParent(slotGo.transform, false);
                    RectTransform rtPrice = priceGo.AddComponent<RectTransform>();
                    rtPrice.anchorMin = new Vector2(0.5f, 0f);
                    rtPrice.anchorMax = new Vector2(0.5f, 0f);
                    rtPrice.pivot = new Vector2(0.5f, 0f);
                    rtPrice.anchoredPosition = new Vector2(0f, 8f);
                    rtPrice.sizeDelta = new Vector2(100f, 20f);

                    Text priceTxt = priceGo.AddComponent<Text>();
                    priceTxt.raycastTarget = false;
                    int sellVal = item.goldPrice / 2;
                    priceTxt.text = $"{sellVal} G";
                    priceTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    priceTxt.fontSize = 15;
                    priceTxt.alignment = TextAnchor.MiddleCenter;
                    priceTxt.color = Color.green;
                    priceTxt.fontStyle = FontStyle.Bold;
                    priceTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
                    priceTxt.verticalOverflow = VerticalWrapMode.Overflow;
                }
                else
                {
                    // Empty slot label
                    GameObject nameGo = new GameObject("Empty");
                    nameGo.transform.SetParent(slotGo.transform, false);
                    RectTransform rtName = nameGo.AddComponent<RectTransform>();
                    rtName.anchorMin = Vector2.zero;
                    rtName.anchorMax = Vector2.one;
                    rtName.sizeDelta = Vector2.zero;
                    rtName.anchoredPosition = Vector2.zero;

                    Text nameTxt = nameGo.AddComponent<Text>();
                    bool isTR = PlayerPrefs.GetString("GameLanguage", "Turkish") == "Turkish";
                    nameTxt.text = isTR ? "(BOŞ)" : "(EMPTY)";
                    nameTxt.raycastTarget = false;
                    nameTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    nameTxt.fontSize = 15;
                    nameTxt.alignment = TextAnchor.MiddleCenter;
                    nameTxt.color = Color.gray;
                }
            }
        }

        private void SellItem(ItemData item, int index)
        {
            if (item == null) return;
            if (InventoryManager.Instance == null) return;

            int payout = item.goldPrice / 2;
            GameManager.Instance.AddGold(payout);
            InventoryManager.Instance.inventoryItems.RemoveAt(index);

            UpdateMerchantShopGold();
            UpdateShopSellGrid();
            UpdateInventoryUI();

            if (DamageTextPool.Instance != null)
            {
                bool isTR = currentLanguage == GameLanguage.Turkish;
                string msg = isTR ? $"+{payout} Altın (Satıldı)" : $"+{payout} Gold (Sold)";
                DamageTextPool.Instance.SpawnText(GameObject.FindWithTag("Player").transform.position, msg, Color.green);
            }
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(SoundEffect.CoinPickup);
            }
        }

        private string GetItemStatDescription(ItemData item)
        {
            if (item == null) return "";
            bool isTR = currentLanguage == GameLanguage.Turkish;

            if (item.itemName == "Extra Heart")
            {
                return isTR ? "Yedek Can Kazandırır\n(İkinci Hayat)" : "Gives +1 Reserve Heart\n(Second Life)";
            }
            if (item.itemName.Contains("Potion"))
            {
                return isTR ? "Maksimum Canı\n%100 Yeniler" : "Restores 100% of\nMax Health Point";
            }
            
            if (item.equipSlot == EquipSlot.Shield)
            {
                if (item.itemName.Contains("Bronze") || item.itemName.Contains("Bronz"))
                {
                    return isTR ? "10sn Blok | %20 Absorbe" : "10s Block | 20% Absorb";
                }
                else if (item.itemName.Contains("Silver") || item.itemName.Contains("Gümüş"))
                {
                    return isTR ? "20sn Blok | %40 Absorbe" : "20s Block | 40% Absorb";
                }
                else if (item.itemName.Contains("Gold") || item.itemName.Contains("Altın"))
                {
                    return isTR ? "30sn Blok | %60 Absorbe" : "30s Block | 60% Absorb";
                }
            }
            
            string prefix = item.statValue > 0 ? "+" : "";
            switch (item.statType)
            {
                case StatType.MaxHP:
                    return isTR ? $"{prefix}{item.statValue} Maks Can" : $"{prefix}{item.statValue} Max HP";
                case StatType.MoveSpeed:
                    return isTR ? $"{prefix}%{Mathf.RoundToInt(item.statValue * 100f)} Hareket Hızı" : $"{prefix}{Mathf.RoundToInt(item.statValue * 100f)}% Move Speed";
                case StatType.AttackSpeed:
                    return isTR ? $"{prefix}%{Mathf.RoundToInt(item.statValue * 100f)} Saldırı Hızı" : $"{prefix}{Mathf.RoundToInt(item.statValue * 100f)}% Attack Speed";
                case StatType.MeleeDamage:
                    return isTR ? $"{prefix}{item.statValue} Hasar | +%{Mathf.RoundToInt(item.critChance * 100f)} Kritik" : $"{prefix}{item.statValue} Melee Dmg | +{Mathf.RoundToInt(item.critChance * 100f)}% Crit";
                case StatType.HeavyDamage:
                    return isTR ? $"{prefix}{item.statValue} Ağır Hasar | +%{Mathf.RoundToInt(item.critChance * 100f)} Kritik" : $"{prefix}{item.statValue} Heavy Dmg | +{Mathf.RoundToInt(item.critChance * 100f)}% Crit";
                case StatType.RangedDamage:
                    return isTR ? $"{prefix}{item.statValue} Menzilli Hasar | +%{Mathf.RoundToInt(item.critChance * 100f)} Kritik" : $"{prefix}{item.statValue} Ranged Dmg | +{Mathf.RoundToInt(item.critChance * 100f)}% Crit";
                default:
                    return isTR ? "Özellik etkisi yok" : "No stat effects";
            }
        }

        public void PopulateShopItemsProgrammatically(Transform parent)
        {
            if (parent == null) return;

            isSellMode = false;

            // Clean up pre-existing children that aren't part of our new UI system
            List<Transform> childrenToDestroy = new List<Transform>();
            foreach (Transform child in parent)
            {
                if (child.name != "ShopScrollView" && 
                    child.name != "Btn_SellMode" && 
                    child.name != "GoldStatus" && 
                    child.name != "Btn_CloseShop")
                {
                    childrenToDestroy.Add(child);
                }
            }
            foreach (Transform child in childrenToDestroy)
            {
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }

            // Setup big merchant title programmatically
            GameObject titleGo = new GameObject("Title");
            titleGo.transform.SetParent(parent, false);
            RectTransform rtTitle = titleGo.AddComponent<RectTransform>();
            rtTitle.anchorMin = new Vector2(0.5f, 1f);
            rtTitle.anchorMax = new Vector2(0.5f, 1f);
            rtTitle.pivot = new Vector2(0.5f, 1f);
            rtTitle.anchoredPosition = new Vector2(0f, -20f);
            rtTitle.sizeDelta = new Vector2(500f, 45f);
            Text titleTxt = titleGo.AddComponent<Text>();
            bool isTR = PlayerPrefs.GetString("GameLanguage", "Turkish") == "Turkish";
            titleTxt.text = isTR ? "SATICI MARKETİ" : "MERCHANT SHOP";
            titleTxt.raycastTarget = false;
            titleTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleTxt.fontSize = 36; // Huge title font size
            titleTxt.fontStyle = FontStyle.Bold;
            titleTxt.alignment = TextAnchor.MiddleCenter;
            titleTxt.color = Color.yellow;

            // Setup programmatic ScrollView
            Transform scrollT = parent.Find("ShopScrollView");
            ScrollRect scrollRect;
            Transform viewportT;
            if (scrollT == null)
            {
                GameObject scrollGo = new GameObject("ShopScrollView");
                scrollGo.transform.SetParent(parent, false);
                RectTransform rtScroll = scrollGo.AddComponent<RectTransform>();
                rtScroll.anchorMin = new Vector2(0.5f, 0.5f);
                rtScroll.anchorMax = new Vector2(0.5f, 0.5f);
                rtScroll.pivot = new Vector2(0.5f, 0.5f);
                rtScroll.anchoredPosition = new Vector2(0f, -55f);
                rtScroll.sizeDelta = new Vector2(1040f, 440f);

                scrollRect = scrollGo.AddComponent<ScrollRect>();
                scrollRect.horizontal = false;
                scrollRect.vertical = true;

                GameObject viewportGo = new GameObject("Viewport");
                viewportGo.transform.SetParent(scrollGo.transform, false);
                RectTransform rtView = viewportGo.AddComponent<RectTransform>();
                rtView.anchorMin = Vector2.zero;
                rtView.anchorMax = Vector2.one;
                rtView.sizeDelta = new Vector2(-30f, 0f); // Narrower to fit scrollbar
                rtView.anchoredPosition = new Vector2(-15f, 0f);
                viewportGo.AddComponent<RectMask2D>();

                viewportT = viewportGo.transform;
                scrollRect.viewport = rtView;

                // Create and link vertical scrollbar
                Scrollbar scrollbar = CreateProgrammaticScrollbar(scrollGo);
                scrollRect.verticalScrollbar = scrollbar;
                scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            }
            else
            {
                scrollRect = scrollT.GetComponent<ScrollRect>();
                viewportT = scrollT.Find("Viewport");
            }

            // Clean up old grids if they exist
            Transform oldGrid = viewportT.Find("ItemsGrid");
            if (oldGrid != null)
            {
                if (Application.isPlaying) Destroy(oldGrid.gameObject);
                else DestroyImmediate(oldGrid.gameObject);
            }
            Transform oldSellGrid = viewportT.Find("SellGrid");
            if (oldSellGrid != null)
            {
                if (Application.isPlaying) Destroy(oldSellGrid.gameObject);
                else DestroyImmediate(oldSellGrid.gameObject);
            }

            // Setup ItemsGrid inside Viewport
            shopItemsGridGo = new GameObject("ItemsGrid");
            shopItemsGridGo.transform.SetParent(viewportT, false);
            RectTransform rtGrid = shopItemsGridGo.AddComponent<RectTransform>();
            rtGrid.anchorMin = new Vector2(0.5f, 1f);
            rtGrid.anchorMax = new Vector2(0.5f, 1f);
            rtGrid.pivot = new Vector2(0.5f, 1f);
            rtGrid.anchoredPosition = Vector2.zero;
            rtGrid.sizeDelta = new Vector2(1000f, 480f);

            GridLayoutGroup grid = shopItemsGridGo.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(230f, 230f); // Wide card layout
            grid.spacing = new Vector2(15f, 15f);
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4; // 4 columns for layout structure

            ContentSizeFitter fitterGrid = shopItemsGridGo.AddComponent<ContentSizeFitter>();
            fitterGrid.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Setup SellGrid inside Viewport
            shopSellGridGo = new GameObject("SellGrid");
            shopSellGridGo.transform.SetParent(viewportT, false);
            RectTransform rtSellGrid = shopSellGridGo.AddComponent<RectTransform>();
            rtSellGrid.anchorMin = new Vector2(0.5f, 1f);
            rtSellGrid.anchorMax = new Vector2(0.5f, 1f);
            rtSellGrid.pivot = new Vector2(0.5f, 1f);
            rtSellGrid.anchoredPosition = Vector2.zero;
            rtSellGrid.sizeDelta = new Vector2(1000f, 480f);

            GridLayoutGroup sellGrid = shopSellGridGo.AddComponent<GridLayoutGroup>();
            sellGrid.cellSize = new Vector2(110f, 130f); // Compact square-ish slot matching the player's 8 slots grid
            sellGrid.spacing = new Vector2(15f, 15f);
            sellGrid.startAxis = GridLayoutGroup.Axis.Horizontal;
            sellGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            sellGrid.constraintCount = 4;
            sellGrid.childAlignment = TextAnchor.UpperCenter;

            ContentSizeFitter fitterSell = shopSellGridGo.AddComponent<ContentSizeFitter>();
            fitterSell.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            shopSellGridGo.SetActive(false);

            // Clean up old CartGrid if it exists
            Transform oldCartGrid = viewportT.Find("CartGrid");
            if (oldCartGrid != null)
            {
                if (Application.isPlaying) Destroy(oldCartGrid.gameObject);
                else DestroyImmediate(oldCartGrid.gameObject);
            }

            // Setup CartGrid inside Viewport
            shopCartGridGo = new GameObject("CartGrid");
            shopCartGridGo.transform.SetParent(viewportT, false);
            RectTransform rtCartGrid = shopCartGridGo.AddComponent<RectTransform>();
            rtCartGrid.anchorMin = new Vector2(0.5f, 1f);
            rtCartGrid.anchorMax = new Vector2(0.5f, 1f);
            rtCartGrid.pivot = new Vector2(0.5f, 1f);
            rtCartGrid.anchoredPosition = Vector2.zero;
            rtCartGrid.sizeDelta = new Vector2(1000f, 480f);

            GridLayoutGroup cartGrid = shopCartGridGo.AddComponent<GridLayoutGroup>();
            cartGrid.cellSize = new Vector2(980f, 100f); // Wide card row
            cartGrid.spacing = new Vector2(10f, 10f);
            cartGrid.startAxis = GridLayoutGroup.Axis.Horizontal;
            cartGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            cartGrid.constraintCount = 1;

            ContentSizeFitter fitterCart = shopCartGridGo.AddComponent<ContentSizeFitter>();
            fitterCart.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            shopCartGridGo.SetActive(false);

            scrollRect.content = rtGrid; // Default content is Buy grid

            // Setup Sell Mode switch button in the Top-Left
            Transform oldSellBtn = parent.Find("Btn_SellMode");
            if (oldSellBtn != null)
            {
                if (Application.isPlaying) Destroy(oldSellBtn.gameObject);
                else DestroyImmediate(oldSellBtn.gameObject);
            }

            GameObject sellBtnGo = new GameObject("Btn_SellMode");
            sellBtnGo.transform.SetParent(parent, false);
            RectTransform rtSellBtn = sellBtnGo.AddComponent<RectTransform>();
            rtSellBtn.anchorMin = new Vector2(0f, 1f);
            rtSellBtn.anchorMax = new Vector2(0f, 1f);
            rtSellBtn.pivot = new Vector2(0f, 1f);
            rtSellBtn.anchoredPosition = new Vector2(25f, -20f);
            rtSellBtn.sizeDelta = new Vector2(220f, 60f); // Expanded size to fit larger button text

            Image sellBtnImg = sellBtnGo.AddComponent<Image>();
            sellBtnImg.color = new Color(0.25f, 0.18f, 0.12f, 1f);

            Button sellBtn = sellBtnGo.AddComponent<Button>();
            sellBtn.onClick.AddListener(ToggleSellMode);

            GameObject sellTextGo = new GameObject("Text");
            sellTextGo.transform.SetParent(sellBtnGo.transform, false);
            RectTransform rtSellText = sellTextGo.AddComponent<RectTransform>();
            rtSellText.anchorMin = Vector2.zero;
            rtSellText.anchorMax = Vector2.one;
            rtSellText.sizeDelta = Vector2.zero;
            rtSellText.anchoredPosition = Vector2.zero;

            btnSellModeText = sellTextGo.AddComponent<Text>();
            btnSellModeText.text = currentLanguage == GameLanguage.Turkish ? "EŞYA SAT" : "SELL ITEMS";
            btnSellModeText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            btnSellModeText.fontSize = 18; // Larger Sell Button Text
            btnSellModeText.fontStyle = FontStyle.Bold;
            btnSellModeText.alignment = TextAnchor.MiddleCenter;
            btnSellModeText.color = Color.white;

            // Setup Gold Kazan Button in the Top-Left (below Sell Mode switch button)
            Transform oldGoldKazanBtn = parent.Find("Btn_GoldKazan");
            if (oldGoldKazanBtn != null)
            {
                if (Application.isPlaying) Destroy(oldGoldKazanBtn.gameObject);
                else DestroyImmediate(oldGoldKazanBtn.gameObject);
            }

            GameObject goldKazanGo = new GameObject("Btn_GoldKazan");
            goldKazanGo.transform.SetParent(parent, false);
            RectTransform rtGoldKazan = goldKazanGo.AddComponent<RectTransform>();
            rtGoldKazan.anchorMin = new Vector2(0f, 1f);
            rtGoldKazan.anchorMax = new Vector2(0f, 1f);
            rtGoldKazan.pivot = new Vector2(0f, 1f);
            rtGoldKazan.anchoredPosition = new Vector2(25f, -90f); // Placed directly below Btn_SellMode
            rtGoldKazan.sizeDelta = new Vector2(220f, 40f); // Matching width of Sell Mode button

            Image goldKazanImg = goldKazanGo.AddComponent<Image>();
            goldKazanImg.color = new Color(0.18f, 0.54f, 0.34f, 1f); // Sleek SeaGreen

            Button goldKazanBtn = goldKazanGo.AddComponent<Button>();
            goldKazanBtn.onClick.AddListener(GoldKazanButtonAction);

            GameObject gkTextGo = new GameObject("Text");
            gkTextGo.transform.SetParent(goldKazanGo.transform, false);
            RectTransform rtGkTxt = gkTextGo.AddComponent<RectTransform>();
            rtGkTxt.anchorMin = Vector2.zero;
            rtGkTxt.anchorMax = Vector2.one;
            rtGkTxt.sizeDelta = Vector2.zero;
            rtGkTxt.anchoredPosition = Vector2.zero;

            Text gkTxt = gkTextGo.AddComponent<Text>();
            gkTxt.text = currentLanguage == GameLanguage.Turkish ? "GOLD KAZAN" : "GET GOLD";
            gkTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            gkTxt.fontSize = 14;
            gkTxt.alignment = TextAnchor.MiddleCenter;
            gkTxt.color = Color.white;
            gkTxt.fontStyle = FontStyle.Bold;

            // İlk oluşturulduğunda cooldown metnini ve durumunu güncelle
            UpdateGoldKazanButtonText();

            // Setup Cart Button in the Top-Right (left of close button)
            Transform oldCartBtn = parent.Find("Btn_Cart");
            if (oldCartBtn != null)
            {
                if (Application.isPlaying) Destroy(oldCartBtn.gameObject);
                else DestroyImmediate(oldCartBtn.gameObject);
            }

            btnCartGo = new GameObject("Btn_Cart");
            btnCartGo.transform.SetParent(parent, false);
            RectTransform rtCartBtn = btnCartGo.AddComponent<RectTransform>();
            rtCartBtn.anchorMin = new Vector2(1f, 1f);
            rtCartBtn.anchorMax = new Vector2(1f, 1f);
            rtCartBtn.pivot = new Vector2(1f, 1f);
            rtCartBtn.anchoredPosition = new Vector2(-105f, -20f); // Placed to the left of Close button with 15px gap
            rtCartBtn.sizeDelta = new Vector2(220f, 60f);

            Image cartBtnImg = btnCartGo.AddComponent<Image>();
            cartBtnImg.color = new Color(0.18f, 0.15f, 0.25f, 1f); // Sleek dark purple/blue theme

            Button cartBtn = btnCartGo.AddComponent<Button>();
            cartBtn.onClick.AddListener(ToggleCartMode);

            GameObject cartTextGo = new GameObject("Text");
            cartTextGo.transform.SetParent(btnCartGo.transform, false);
            RectTransform rtCartText = cartTextGo.AddComponent<RectTransform>();
            rtCartText.anchorMin = Vector2.zero;
            rtCartText.anchorMax = Vector2.one;
            rtCartText.sizeDelta = Vector2.zero;

            btnCartText = cartTextGo.AddComponent<Text>();
            btnCartText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            btnCartText.fontSize = 18;
            btnCartText.fontStyle = FontStyle.Bold;
            btnCartText.alignment = TextAnchor.MiddleCenter;
            btnCartText.color = Color.white;
            btnCartText.raycastTarget = false;
            UpdateCartButtonText();

            // Setup Checkout Button in the Top-Right (left of cart button)
            Transform oldCheckoutBtn = parent.Find("Btn_Checkout");
            if (oldCheckoutBtn != null)
            {
                if (Application.isPlaying) Destroy(oldCheckoutBtn.gameObject);
                else DestroyImmediate(oldCheckoutBtn.gameObject);
            }

            btnCheckoutGo = new GameObject("Btn_Checkout");
            btnCheckoutGo.transform.SetParent(parent, false);
            RectTransform rtCheckoutBtn = btnCheckoutGo.AddComponent<RectTransform>();
            rtCheckoutBtn.anchorMin = new Vector2(1f, 1f);
            rtCheckoutBtn.anchorMax = new Vector2(1f, 1f);
            rtCheckoutBtn.pivot = new Vector2(1f, 1f);
            rtCheckoutBtn.anchoredPosition = new Vector2(-340f, -20f); // Placed to the left of Cart button with 15px gap
            rtCheckoutBtn.sizeDelta = new Vector2(220f, 60f);

            Image checkoutBtnImg = btnCheckoutGo.AddComponent<Image>();
            checkoutBtnImg.color = new Color(0.1f, 0.35f, 0.15f, 1f); // Vibrant green checkout theme

            Button checkoutBtn = btnCheckoutGo.AddComponent<Button>();
            checkoutBtn.onClick.AddListener(CheckoutCart);

            GameObject checkoutTextGo = new GameObject("Text");
            checkoutTextGo.transform.SetParent(btnCheckoutGo.transform, false);
            RectTransform rtCheckoutText = checkoutTextGo.AddComponent<RectTransform>();
            rtCheckoutText.anchorMin = Vector2.zero;
            rtCheckoutText.anchorMax = Vector2.one;
            rtCheckoutText.sizeDelta = Vector2.zero;

            btnCheckoutText = checkoutTextGo.AddComponent<Text>();
            btnCheckoutText.text = currentLanguage == GameLanguage.Turkish ? "SATIN AL" : "CHECKOUT";
            btnCheckoutText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            btnCheckoutText.fontSize = 18;
            btnCheckoutText.fontStyle = FontStyle.Bold;
            btnCheckoutText.alignment = TextAnchor.MiddleCenter;
            btnCheckoutText.color = Color.white;
            btnCheckoutText.raycastTarget = false;
            btnCheckoutGo.SetActive(false); // Only visible in Cart Mode

            // Gold status setup
            Transform oldGold = parent.Find("GoldStatus");
            if (oldGold != null)
            {
                if (Application.isPlaying) Destroy(oldGold.gameObject);
                else DestroyImmediate(oldGold.gameObject);
            }

            GameObject goldGo = new GameObject("GoldStatus");
            goldGo.transform.SetParent(parent, false);
            RectTransform rtGold = goldGo.AddComponent<RectTransform>();
            rtGold.anchorMin = new Vector2(0.5f, 1f);
            rtGold.anchorMax = new Vector2(0.5f, 1f);
            rtGold.pivot = new Vector2(0.5f, 1f);
            rtGold.anchoredPosition = new Vector2(0f, -75f); // Shifted down further below Title
            rtGold.sizeDelta = new Vector2(400f, 40f);

            merchantShopGoldText = goldGo.AddComponent<Text>();
            merchantShopGoldText.raycastTarget = false;
            merchantShopGoldText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            merchantShopGoldText.fontSize = 30; // Larger Gold Text (30px)
            merchantShopGoldText.alignment = TextAnchor.MiddleCenter;
            merchantShopGoldText.color = Color.yellow;
            merchantShopGoldText.fontStyle = FontStyle.Bold;

            UpdateMerchantShopGold();

            // Populate items from InventoryManager database
            if (InventoryManager.Instance != null)
            {
                List<ItemData> clothes = new List<ItemData>();
                List<ItemData> weapons = new List<ItemData>();
                foreach (ItemData item in InventoryManager.Instance.itemDatabase)
                {

                    if (item.equipSlot == EquipSlot.Head ||
                        item.equipSlot == EquipSlot.Chest ||
                        item.equipSlot == EquipSlot.Hands ||
                        item.equipSlot == EquipSlot.Legs ||
                        item.equipSlot == EquipSlot.Feet)
                    {
                        clothes.Add(item);
                    }
                    else
                    {
                        weapons.Add(item);
                    }
                }

                List<ItemData> sortedItems = new List<ItemData>();
                sortedItems.AddRange(clothes);
                sortedItems.AddRange(weapons);

                foreach (ItemData item in sortedItems)
                {
                    GameObject slotGo = new GameObject(item.itemName + "_Slot");
                    slotGo.transform.SetParent(shopItemsGridGo.transform, false);

                    Image slotImg = slotGo.AddComponent<Image>();
                    slotImg.color = new Color(0.2f, 0.15f, 0.12f, 0.95f);

                    Button slotBtn = slotGo.AddComponent<Button>();
                    slotBtn.onClick.AddListener(() => BuyItem(item));

                    // Icon
                    GameObject iconGo = new GameObject("Icon");
                    iconGo.transform.SetParent(slotGo.transform, false);
                    RectTransform rtIcon = iconGo.AddComponent<RectTransform>();
                    rtIcon.anchorMin = new Vector2(0.5f, 1f);
                    rtIcon.anchorMax = new Vector2(0.5f, 1f);
                    rtIcon.pivot = new Vector2(0.5f, 1f);
                    rtIcon.anchoredPosition = new Vector2(0f, -10f);
                    rtIcon.sizeDelta = new Vector2(64f, 64f);

                    Image iconImg = iconGo.AddComponent<Image>();
                    iconImg.sprite = item.icon;
                    iconImg.color = Color.white;
                    iconImg.raycastTarget = false;

                    // Name
                    GameObject nameGo = new GameObject("Name");
                    nameGo.transform.SetParent(slotGo.transform, false);
                    RectTransform rtName = nameGo.AddComponent<RectTransform>();
                    rtName.anchorMin = new Vector2(0.5f, 1f);
                    rtName.anchorMax = new Vector2(0.5f, 1f);
                    rtName.pivot = new Vector2(0.5f, 1f);
                    rtName.anchoredPosition = new Vector2(0f, -80f);
                    rtName.sizeDelta = new Vector2(220f, 25f);

                    Text nameTxt = nameGo.AddComponent<Text>();
                    nameTxt.text = GetLocalizedItemName(item.itemName, currentLanguage == GameLanguage.Turkish);
                    nameTxt.raycastTarget = false;
                    nameTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    nameTxt.fontSize = 22; // Expanded to 22px
                    nameTxt.fontStyle = FontStyle.Bold;
                    nameTxt.alignment = TextAnchor.MiddleCenter;
                    nameTxt.color = Color.white;
                    nameTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
                    nameTxt.verticalOverflow = VerticalWrapMode.Overflow;

                    // Description
                    GameObject descGo = new GameObject("Description");
                    descGo.transform.SetParent(slotGo.transform, false);
                    RectTransform rtDesc = descGo.AddComponent<RectTransform>();
                    rtDesc.anchorMin = new Vector2(0.5f, 1f);
                    rtDesc.anchorMax = new Vector2(0.5f, 1f);
                    rtDesc.pivot = new Vector2(0.5f, 1f);
                    rtDesc.anchoredPosition = new Vector2(0f, -110f);
                    rtDesc.sizeDelta = new Vector2(220f, 55f); // Expanded height for larger text

                    Text descTxt = descGo.AddComponent<Text>();
                    descTxt.text = GetItemStatDescription(item);
                    descTxt.raycastTarget = false;
                    descTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    descTxt.fontSize = 20; // Expanded to 20px
                    descTxt.alignment = TextAnchor.MiddleCenter;
                    descTxt.color = new Color(0.85f, 0.85f, 0.85f);
                    descTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
                    descTxt.verticalOverflow = VerticalWrapMode.Overflow;

                    // Price
                    GameObject priceGo = new GameObject("Price");
                    priceGo.transform.SetParent(slotGo.transform, false);
                    RectTransform rtPrice = priceGo.AddComponent<RectTransform>();
                    rtPrice.anchorMin = new Vector2(0.5f, 0f);
                    rtPrice.anchorMax = new Vector2(0.5f, 0f);
                    rtPrice.pivot = new Vector2(0.5f, 0f);
                    rtPrice.anchoredPosition = new Vector2(0f, 12f); // Lifted slightly
                    rtPrice.sizeDelta = new Vector2(220f, 25f);

                    Text priceTxt = priceGo.AddComponent<Text>();
                    priceTxt.text = $"{item.goldPrice} G";
                    priceTxt.raycastTarget = false;
                    priceTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    priceTxt.fontSize = 22; // Expanded to 22px
                    priceTxt.alignment = TextAnchor.MiddleCenter;
                    priceTxt.color = Color.yellow;
                    priceTxt.fontStyle = FontStyle.Bold;
                    priceTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
                    priceTxt.verticalOverflow = VerticalWrapMode.Overflow;
                }
            }
        }

        public void UpdateExtraHeartsUI(int count)
        {
            if (extraHeartsContainer == null) return;

            // Safe override to prevent layout stretching bugs
            extraHeartsContainer.childControlWidth = false;
            extraHeartsContainer.childControlHeight = false;
            extraHeartsContainer.childForceExpandWidth = false;
            extraHeartsContainer.childForceExpandHeight = false;
            extraHeartsContainer.spacing = 10f;

            // Clear old children
            foreach (Transform child in extraHeartsContainer.transform)
            {
                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }

            Sprite heartSprite = GetOrCreateHeartSprite();

            // Spawn count heart images
            for (int i = 0; i < count; i++)
            {
                GameObject heartGo = new GameObject("HeartIcon_" + i);
                heartGo.transform.SetParent(extraHeartsContainer.transform, false);

                RectTransform rect = heartGo.AddComponent<RectTransform>();
                rect.sizeDelta = new Vector2(36f, 36f); // Enlarged to 36x36 to look extremely prominent

                Image img = heartGo.AddComponent<Image>();
                img.sprite = heartSprite;
                img.color = Color.white;
            }
        }

        private Sprite cachedHeartSprite;
        private Sprite GetOrCreateHeartSprite()
        {
            if (cachedHeartSprite != null) return cachedHeartSprite;

            int width = 16;
            int height = 16;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;

            Color r = new Color(0.9f, 0.1f, 0.1f, 1f); // Vibrant Red
            Color b = Color.black;
            Color t = Color.clear;
            Color w = Color.white; // Highlights

            Color[] pixels = new Color[width * height];
            string[] rows = new string[] {
                "................", // 15
                "................", // 14
                "..XXX.....XXX...", // 13
                ".XRRRX...XRRRX..", // 12
                "XRRRRRX.XRRRRRX.", // 11
                "XRRWRRRXRRRRRRX.", // 10
                "XRRWRRRRRRRRRRX.", // 9
                ".XRRRRRRRRRRRRX.", // 8
                "..XRRRRRRRRRRX..", // 7
                "...XRRRRRRRRX...", // 6
                "....XRRRRRRX....", // 5
                ".....XRRRRX.....", // 4
                "......XRRX......", // 3
                ".......XX.......", // 2
                "................", // 1
                "................"  // 0
            };

            for (int y = 0; y < 16; y++)
            {
                string row = rows[15 - y];
                for (int x = 0; x < 16; x++)
                {
                    char c = row[x];
                    Color col = t;
                    if (c == 'X') col = b;
                    else if (c == 'R') col = r;
                    else if (c == 'W') col = w;
                    
                    texture.SetPixel(x, y, col);
                }
            }
            texture.Apply();

            cachedHeartSprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 16f);
            return cachedHeartSprite;
        }

        private void EnsureHealthBarUI(GameObject gameplayHUDGo)
        {
            if (gameplayHUDGo == null) return;

            // Remove/hide the old HPText if it exists
            Transform oldHpTextTrans = gameplayHUDGo.transform.Find("HPText");
            if (oldHpTextTrans != null)
            {
                oldHpTextTrans.gameObject.SetActive(false);
            }

            // Look for or create HeroPortrait on the left
            Transform portraitTrans = gameplayHUDGo.transform.Find("HeroPortrait");
            if (portraitTrans == null)
            {
                GameObject portraitGo = new GameObject("HeroPortrait");
                portraitGo.transform.SetParent(gameplayHUDGo.transform, false);
                
                RectTransform portRect = portraitGo.AddComponent<RectTransform>();
                portRect.anchorMin = new Vector2(0f, 1f);
                portRect.anchorMax = new Vector2(0f, 1f);
                portRect.pivot = new Vector2(0f, 1f);
                portRect.anchoredPosition = new Vector2(20f, -20f);
                portRect.sizeDelta = new Vector2(72f, 72f);

                Image portImg = portraitGo.AddComponent<Image>();
                portImg.sprite = CreateHeroPortraitSprite();
            }

            // Look for existing HealthBar Slider
            Transform sliderTrans = gameplayHUDGo.transform.Find("HealthBar");
            if (sliderTrans != null)
            {
                healthSlider = sliderTrans.GetComponent<Slider>();
                RectTransform sliderRect = sliderTrans.GetComponent<RectTransform>();
                sliderRect.anchoredPosition = new Vector2(104f, -24f);
                sliderRect.sizeDelta = new Vector2(220f, 25f);
                
                // Add or update Frame if not exists
                Transform frameTrans = sliderTrans.Find("Frame");
                if (frameTrans == null)
                {
                    GameObject frameGo = new GameObject("Frame");
                    frameGo.transform.SetParent(sliderTrans, false);
                    RectTransform frameRect = frameGo.AddComponent<RectTransform>();
                    frameRect.anchorMin = Vector2.zero;
                    frameRect.anchorMax = Vector2.one;
                    frameRect.sizeDelta = Vector2.zero;
                    frameRect.anchoredPosition = Vector2.zero;
                    Image frameImg = frameGo.AddComponent<Image>();
                    frameImg.sprite = CreateHealthBarFrameSprite(220, 25);
                }

                Transform percentTextTrans = sliderTrans.Find("PercentText");
                if (percentTextTrans != null)
                {
                    healthPercentText = percentTextTrans.GetComponent<Text>();
                    RectTransform ptRect = percentTextTrans.GetComponent<RectTransform>();
                    if (ptRect != null)
                    {
                        ptRect.anchorMin = new Vector2(0.5f, 0.5f);
                        ptRect.anchorMax = new Vector2(0.5f, 0.5f);
                        ptRect.pivot = new Vector2(0.5f, 0.5f);
                        ptRect.anchoredPosition = new Vector2(0f, 0f);
                        ptRect.sizeDelta = new Vector2(100f, 25f);
                    }
                    if (healthPercentText != null)
                    {
                        healthPercentText.alignment = TextAnchor.MiddleCenter;
                        healthPercentText.fontStyle = FontStyle.Bold;
                        
                        if (percentTextTrans.GetComponent<Shadow>() == null)
                        {
                            var shadow = percentTextTrans.gameObject.AddComponent<Shadow>();
                            shadow.effectColor = Color.black;
                            shadow.effectDistance = new Vector2(1.5f, -1.5f);
                        }
                    }
                }
            }
            else
            {
                // Create the Slider object
                GameObject sliderGo = new GameObject("HealthBar");
                sliderGo.transform.SetParent(gameplayHUDGo.transform, false);
                
                RectTransform sliderRect = sliderGo.AddComponent<RectTransform>();
                sliderRect.anchorMin = new Vector2(0f, 1f);
                sliderRect.anchorMax = new Vector2(0f, 1f);
                sliderRect.pivot = new Vector2(0f, 1f);
                sliderRect.anchoredPosition = new Vector2(104f, -24f);
                sliderRect.sizeDelta = new Vector2(220f, 25f);

                Slider slider = sliderGo.AddComponent<Slider>();
                
                // 1. Background (Padded inside)
                GameObject bgGo = new GameObject("Background");
                bgGo.transform.SetParent(sliderGo.transform, false);
                RectTransform bgRect = bgGo.AddComponent<RectTransform>();
                bgRect.anchorMin = Vector2.zero;
                bgRect.anchorMax = Vector2.one;
                bgRect.offsetMin = new Vector2(10f, 2f);
                bgRect.offsetMax = new Vector2(-10f, -2f);
                Image bgImg = bgGo.AddComponent<Image>();
                bgImg.color = new Color(0.1f, 0.1f, 0.12f, 1f); // Dark Charcoal

                // 2. Fill Area (Padded inside)
                GameObject fillAreaGo = new GameObject("Fill Area");
                fillAreaGo.transform.SetParent(sliderGo.transform, false);
                RectTransform fillAreaRect = fillAreaGo.AddComponent<RectTransform>();
                fillAreaRect.anchorMin = Vector2.zero;
                fillAreaRect.anchorMax = Vector2.one;
                fillAreaRect.offsetMin = new Vector2(12f, 3f);
                fillAreaRect.offsetMax = new Vector2(-12f, -3f);

                // 3. Fill
                GameObject fillGo = new GameObject("Fill");
                fillGo.transform.SetParent(fillAreaGo.transform, false);
                RectTransform fillRect = fillGo.AddComponent<RectTransform>();
                fillRect.anchorMin = Vector2.zero;
                fillRect.anchorMax = Vector2.one;
                fillRect.sizeDelta = Vector2.zero;
                fillRect.anchoredPosition = Vector2.zero;
                Image fillImg = fillGo.AddComponent<Image>();
                fillImg.color = new Color(0.9f, 0.1f, 0.1f, 1f); // Vibrant Red

                // 4. Overlaid Metallic Frame
                GameObject frameGo = new GameObject("Frame");
                frameGo.transform.SetParent(sliderGo.transform, false);
                RectTransform frameRect = frameGo.AddComponent<RectTransform>();
                frameRect.anchorMin = Vector2.zero;
                frameRect.anchorMax = Vector2.one;
                frameRect.sizeDelta = Vector2.zero;
                frameRect.anchoredPosition = Vector2.zero;
                Image frameImg = frameGo.AddComponent<Image>();
                frameImg.sprite = CreateHealthBarFrameSprite(220, 25);

                // Configure Slider references
                slider.fillRect = fillRect;
                slider.targetGraphic = fillImg;
                slider.minValue = 0f;
                slider.maxValue = 100f;
                slider.value = 100f;
                slider.interactable = false;

                // Text Label for Percent
                GameObject percentTextGo = new GameObject("PercentText");
                percentTextGo.transform.SetParent(sliderGo.transform, false);
                RectTransform ptRect = percentTextGo.AddComponent<RectTransform>();
                ptRect.anchorMin = new Vector2(0.5f, 0.5f);
                ptRect.anchorMax = new Vector2(0.5f, 0.5f);
                ptRect.pivot = new Vector2(0.5f, 0.5f);
                ptRect.anchoredPosition = new Vector2(0f, 0f);
                ptRect.sizeDelta = new Vector2(100f, 25f);

                Text percentTxt = percentTextGo.AddComponent<Text>();
                percentTxt.text = "100%";
                percentTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                percentTxt.fontSize = 18;
                percentTxt.fontStyle = FontStyle.Bold;
                percentTxt.alignment = TextAnchor.MiddleCenter;
                percentTxt.color = Color.white;

                var shadow = percentTextGo.AddComponent<Shadow>();
                shadow.effectColor = Color.black;
                shadow.effectDistance = new Vector2(1.5f, -1.5f);

                healthSlider = slider;
                healthPercentText = percentTxt;
            }

            // Look for existing ExtraHearts Container
            Transform containerTrans = gameplayHUDGo.transform.Find("ExtraHeartsContainer");
            if (containerTrans != null)
            {
                extraHeartsContainer = containerTrans.GetComponent<HorizontalLayoutGroup>();
                RectTransform containerRect = containerTrans.GetComponent<RectTransform>();
                containerRect.anchoredPosition = new Vector2(104f, -54f);
                if (extraHeartsContainer != null)
                {
                    extraHeartsContainer.childControlWidth = false;
                    extraHeartsContainer.childControlHeight = false;
                    extraHeartsContainer.childForceExpandWidth = false;
                    extraHeartsContainer.childForceExpandHeight = false;
                    extraHeartsContainer.spacing = 10f;
                }
            }
            else
            {
                // Create a horizontal layout group right below the Health Bar
                GameObject containerGo = new GameObject("ExtraHeartsContainer");
                containerGo.transform.SetParent(gameplayHUDGo.transform, false);

                RectTransform containerRect = containerGo.AddComponent<RectTransform>();
                containerRect.anchorMin = new Vector2(0f, 1f);
                containerRect.anchorMax = new Vector2(0f, 1f);
                containerRect.pivot = new Vector2(0f, 1f);
                containerRect.anchoredPosition = new Vector2(104f, -54f); // Shifted right of portrait and below health bar
                containerRect.sizeDelta = new Vector2(220f, 40f);

                HorizontalLayoutGroup layout = containerGo.AddComponent<HorizontalLayoutGroup>();
                layout.spacing = 10f;
                layout.childControlHeight = false;
                layout.childControlWidth = false;
                layout.childForceExpandHeight = false;
                layout.childForceExpandWidth = false;
                layout.childAlignment = TextAnchor.MiddleLeft;

                extraHeartsContainer = layout;
            }
        }

        private static Sprite cachedHeroPortraitSprite = null;

        private Sprite CreateHeroPortraitSprite()
        {
            if (cachedHeroPortraitSprite != null) return cachedHeroPortraitSprite;

            string path = System.IO.Path.Combine(Application.dataPath, "Sprites/hero_portrait.png");
            if (System.IO.File.Exists(path))
            {
                byte[] fileData = System.IO.File.ReadAllBytes(path);
                Texture2D tex = new Texture2D(2, 2);
                if (tex.LoadImage(fileData))
                {
                    tex.filterMode = FilterMode.Point;
                    cachedHeroPortraitSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                    return cachedHeroPortraitSprite;
                }
            }

            int size = 48;
            Texture2D fallbackTex = new Texture2D(size, size);
            fallbackTex.filterMode = FilterMode.Point;
            
            float cx = size / 2f;
            float cy = size / 2f;
            float r = size / 2f - 1f;

            Color steelLight = new Color(0.6f, 0.63f, 0.68f);
            Color steelMed = new Color(0.4f, 0.42f, 0.46f);
            Color steelDark = new Color(0.2f, 0.22f, 0.25f);
            Color gold = new Color(0.85f, 0.7f, 0.2f);
            Color plumeColor = new Color(0.85f, 0.15f, 0.15f); // Red plume

            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    float dx = x - cx;
                    float dy = y - cy;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    if (dist > r)
                    {
                        fallbackTex.SetPixel(x, y, Color.clear);
                    }
                    else if (dist >= r - 2f)
                    {
                        bool isHighlight = (dx < 0 && dy > 0) || (dy > Mathf.Abs(dx));
                        fallbackTex.SetPixel(x, y, isHighlight ? steelLight : steelDark);
                    }
                    else
                    {
                        float t = (float)y / size;
                        Color bgCol = Color.Lerp(new Color(0.05f, 0.05f, 0.15f), new Color(0.15f, 0.1f, 0.25f), t);
                        Color pixelCol = bgCol;

                        bool isPlume = (x >= 12 && x <= 22 && y >= 28 && y <= 40 && (x - 12) + (y - 28) > 4);
                        
                        float hdx = x - 24f;
                        float hdy = y - 27f;
                        float hdist = Mathf.Sqrt(hdx * hdx + hdy * hdy);
                        bool isHelmetDome = (hdist <= 8f && y >= 25);

                        bool isVisor = (x >= 22 && x <= 32 && y >= 20 && y <= 26);
                        bool isVisorSlit = (x >= 24 && x <= 30 && y == 23);
                        bool isEyeGlow = (x == 27 && y == 23);

                        bool isChin = (x >= 18 && x <= 28 && y >= 14 && y < 20);
                        bool isCollar = (x >= 16 && x <= 30 && y >= 10 && y < 14);
                        bool isShoulder = (x >= 10 && x <= 38 && y < 10);
                        bool isGoldTrim = isShoulder && (y == 8 || x == 12 || x == 36);

                        if (isPlume) pixelCol = plumeColor;
                        if (isHelmetDome) pixelCol = (hdx < 0) ? steelDark : steelMed;
                        if (isChin) pixelCol = steelMed;
                        if (isVisor)
                        {
                            if (isVisorSlit)
                            {
                                pixelCol = isEyeGlow ? Color.cyan : Color.black;
                            }
                            else
                            {
                                pixelCol = steelLight;
                            }
                        }
                        if (isCollar) pixelCol = steelDark;
                        if (isShoulder)
                        {
                            pixelCol = isGoldTrim ? gold : steelMed;
                        }

                        fallbackTex.SetPixel(x, y, pixelCol);
                    }
                }
            }

            fallbackTex.Apply();
            return Sprite.Create(fallbackTex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private Sprite CreateHealthBarFrameSprite(int w, int h)
        {
            Texture2D tex = new Texture2D(w, h);
            tex.filterMode = FilterMode.Point;

            Color darkBorder = new Color(0.12f, 0.12f, 0.15f, 1f);
            Color lightBorder = new Color(0.45f, 0.47f, 0.52f, 1f);
            Color midBorder = new Color(0.28f, 0.3f, 0.35f, 1f);

            int slantSize = 12;

            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    int topLimit = 0;
                    int bottomLimit = h - 1;

                    if (x < slantSize)
                    {
                        int dy = (slantSize - x) * h / (slantSize * 2);
                        topLimit = dy;
                        bottomLimit = h - 1 - dy;
                    }
                    else if (x >= w - slantSize)
                    {
                        int dy = (x - (w - slantSize)) * h / (slantSize * 2);
                        topLimit = dy;
                        bottomLimit = h - 1 - dy;
                    }

                    if (y < topLimit || y > bottomLimit)
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                    else if (y == topLimit || y == bottomLimit || 
                             (x < slantSize && (y == topLimit || y == bottomLimit)) ||
                             (x >= w - slantSize && (y == topLimit || y == bottomLimit)) ||
                             (x == 0 && y >= topLimit && y <= bottomLimit) ||
                             (x == w - 1 && y >= topLimit && y <= bottomLimit))
                    {
                        tex.SetPixel(x, y, darkBorder);
                    }
                    else if (y == topLimit + 1 || y == bottomLimit - 1 || x == 1 || x == w - 2)
                    {
                        bool isHighlight = (y == topLimit + 1 || x == 1);
                        tex.SetPixel(x, y, isHighlight ? lightBorder : midBorder);
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        public bool IsShopOpen()
        {
            bool isStatsShopActive = (shopPanel != null && shopPanel.activeSelf);
            bool isMerchantShopActive = (merchantShopPanelGo != null && merchantShopPanelGo.activeSelf);
            
            if (!isMerchantShopActive)
            {
                GameObject canvas = GameObject.Find("Canvas");
                if (canvas != null)
                {
                    Transform t = canvas.transform.Find("MerchantShopPanel");
                    if (t != null && t.gameObject.activeSelf)
                    {
                        isMerchantShopActive = true;
                    }
                }
            }
            
            return isStatsShopActive || isMerchantShopActive;
        }

        public bool IsInventoryOpen()
        {
            return inventoryPanel != null && inventoryPanel.activeSelf;
        }

        public bool IsBlockButtonPressed()
        {
            return blockHoldButton != null && blockHoldButton.IsPressed;
        }

        public void OpenSettingsAction()
        {
            EnsureSettingsAndSaveLoadUI();
            if (settingsPanelGo != null)
            {
                settingsPanelGo.SetActive(true);
                
                Slider masterS = settingsPanelGo.transform.Find("MasterVolumeSlider")?.GetComponent<Slider>();
                if (masterS != null && AudioManager.Instance != null)
                {
                    masterS.value = AudioManager.Instance.GetMasterVolume();
                }

                Slider musicS = settingsPanelGo.transform.Find("MusicVolumeSlider")?.GetComponent<Slider>();
                if (musicS != null && AudioManager.Instance != null)
                {
                    musicS.value = AudioManager.Instance.GetMusicVolume();
                }
            }
        }

        public void OpenSaveLoadAction(bool saving)
        {
            isSavingMode = saving;
            EnsureSettingsAndSaveLoadUI();
            
            if (saveLoadPanelGo != null)
            {
                saveLoadPanelGo.SetActive(true);
                
                bool isTR = currentLanguage == GameLanguage.Turkish;

                Text titleText = saveLoadPanelGo.transform.Find("Title")?.GetComponent<Text>();
                if (titleText != null)
                {
                    if (isSavingMode)
                    {
                        titleText.text = isTR ? "OYUNU KAYDET" : "SAVE GAME";
                    }
                    else
                    {
                        titleText.text = isTR ? "KAYIT YÜKLE" : "LOAD GAME";
                    }
                }

                Transform backBtn = saveLoadPanelGo.transform.Find("BackButton");
                if (backBtn != null)
                {
                    Text backTxt = backBtn.GetComponentInChildren<Text>();
                    if (backTxt != null) backTxt.text = isTR ? "GERİ" : "BACK";
                }
                
                for (int i = 1; i <= 3; i++)
                {
                    Text btnTxt = saveLoadPanelGo.transform.Find($"Slot_{i}_Button/Text")?.GetComponent<Text>();
                    if (btnTxt != null)
                    {
                        if (SaveManager.SaveExists(i))
                        {
                            GameSaveData data = SaveManager.Load(i);
                            btnTxt.text = isTR 
                                ? $"Slot {i}: Oda {data.levelIndex} - {data.goldCount} Altın"
                                : $"Slot {i}: Room {data.levelIndex} - {data.goldCount} Gold";
                        }
                        else
                        {
                            btnTxt.text = isTR ? $"Slot {i}: BOŞ" : $"Slot {i}: EMPTY";
                        }
                    }
                }
            }
        }

        private void EnsureSettingsAndSaveLoadUI()
        {
            GameObject canvas = GameObject.Find("Canvas");
            if (canvas == null) return;

            if (settingsPanelGo == null)
            {
                Transform existing = canvas.transform.Find("SettingsPanel");
                if (existing != null)
                {
                    settingsPanelGo = existing.gameObject;
                }
                else
                {
                    settingsPanelGo = new GameObject("SettingsPanel");
                    settingsPanelGo.transform.SetParent(canvas.transform, false);
                    
                    RectTransform rect = settingsPanelGo.AddComponent<RectTransform>();
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.offsetMin = Vector2.zero;
                    rect.offsetMax = Vector2.one;
                    
                    Image img = settingsPanelGo.AddComponent<Image>();
                    img.color = new Color(0.05f, 0.05f, 0.08f, 0.95f);
                    
                    CreateTextElement("Title", settingsPanelGo, "SETTINGS", 36, Color.yellow, new Vector2(0f, 250f));
                    
                    CreateTextElement("MasterLabel", settingsPanelGo, "MASTER VOLUME", 20, Color.white, new Vector2(0f, 120f));
                    GameObject masterSliderGo = CreateSliderElement("MasterVolumeSlider", settingsPanelGo, new Vector2(0f, 70f));
                    Slider masterSlider = masterSliderGo.GetComponent<Slider>();
                    masterSlider.onValueChanged.AddListener((val) => {
                        if (AudioManager.Instance != null) AudioManager.Instance.SetMasterVolume(val);
                    });
                    
                    CreateTextElement("MusicLabel", settingsPanelGo, "MUSIC VOLUME", 20, Color.white, new Vector2(0f, 0f));
                    GameObject musicSliderGo = CreateSliderElement("MusicVolumeSlider", settingsPanelGo, new Vector2(0f, -50f));
                    Slider musicSlider = musicSliderGo.GetComponent<Slider>();
                    musicSlider.onValueChanged.AddListener((val) => {
                        if (AudioManager.Instance != null) AudioManager.Instance.SetMusicVolume(val);
                    });

                    // Language Selector
                    CreateTextElement("LanguageLabel", settingsPanelGo, "LANGUAGE / DİL", 20, Color.white, new Vector2(0f, -115f));
                    
                    GameObject trBtnGo = CreateButtonElement("TR_Button", settingsPanelGo, "TÜRKÇE", new Vector2(-100f, -170f));
                    trBtnGo.GetComponent<RectTransform>().sizeDelta = new Vector2(160f, 40f);
                    trBtnGo.GetComponent<Button>().onClick.AddListener(() => {
                        SetLanguage(GameLanguage.Turkish);
                    });

                    GameObject enBtnGo = CreateButtonElement("EN_Button", settingsPanelGo, "ENGLISH", new Vector2(100f, -170f));
                    enBtnGo.GetComponent<RectTransform>().sizeDelta = new Vector2(160f, 40f);
                    enBtnGo.GetComponent<Button>().onClick.AddListener(() => {
                        SetLanguage(GameLanguage.English);
                    });
                    
                    GameObject backBtnGo = CreateButtonElement("BackButton", settingsPanelGo, "BACK", new Vector2(0f, -250f));
                    backBtnGo.GetComponent<Button>().onClick.AddListener(() => {
                        settingsPanelGo.SetActive(false);
                        HandleStateChanged(GameManager.Instance.CurrentState);
                    });

                    UpdateLocalizedTexts();
                    settingsPanelGo.SetActive(false);
                }
            }

            if (saveLoadPanelGo == null)
            {
                Transform existing = canvas.transform.Find("SaveLoadPanel");
                if (existing != null)
                {
                    saveLoadPanelGo = existing.gameObject;
                }
                else
                {
                    saveLoadPanelGo = new GameObject("SaveLoadPanel");
                    saveLoadPanelGo.transform.SetParent(canvas.transform, false);
                    
                    RectTransform rect = saveLoadPanelGo.AddComponent<RectTransform>();
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.offsetMin = Vector2.zero;
                    rect.offsetMax = Vector2.one;
                    
                    Image img = saveLoadPanelGo.AddComponent<Image>();
                    img.color = new Color(0.05f, 0.05f, 0.08f, 0.95f);
                    
                    CreateTextElement("Title", saveLoadPanelGo, "SELECT SAVE SLOT", 36, Color.yellow, new Vector2(0f, 250f));
                    
                    CreateSlotButton(1, new Vector2(0f, 100f));
                    CreateSlotButton(2, new Vector2(0f, 0f));
                    CreateSlotButton(3, new Vector2(0f, -100f));
                    
                    GameObject backBtnGo = CreateButtonElement("BackButton", saveLoadPanelGo, "BACK", new Vector2(0f, -220f));
                    backBtnGo.GetComponent<Button>().onClick.AddListener(() => {
                        saveLoadPanelGo.SetActive(false);
                        HandleStateChanged(GameManager.Instance.CurrentState);
                    });

                    saveLoadPanelGo.SetActive(false);
                }
            }
        }

        private void CreateTextElement(string name, GameObject parent, string text, int fontSize, Color color, Vector2 anchoredPos)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(500f, 50f);
            rect.anchoredPosition = anchoredPos;
            
            Text txt = go.AddComponent<Text>();
            txt.text = text;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = fontSize;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = color;
        }

        private GameObject CreateSliderElement(string name, GameObject parent, Vector2 anchoredPos)
        {
            GameObject sliderGo = new GameObject(name);
            sliderGo.transform.SetParent(parent.transform, false);
            
            RectTransform rect = sliderGo.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(300f, 20f);
            rect.anchoredPosition = anchoredPos;
            
            Slider slider = sliderGo.AddComponent<Slider>();
            
            GameObject bg = new GameObject("Background");
            bg.transform.SetParent(sliderGo.transform, false);
            RectTransform bgRect = bg.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.one;
            Image bgImg = bg.AddComponent<Image>();
            bgImg.color = Color.gray;
            
            GameObject fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(sliderGo.transform, false);
            RectTransform faRect = fillArea.AddComponent<RectTransform>();
            faRect.anchorMin = Vector2.zero;
            faRect.anchorMax = Vector2.one;
            faRect.offsetMin = Vector2.zero;
            faRect.offsetMax = Vector2.one;
            
            GameObject fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            RectTransform fillRect = fill.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            Image fillImg = fill.AddComponent<Image>();
            fillImg.color = Color.yellow;
            
            slider.fillRect = fillRect;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            
            return sliderGo;
        }

        private GameObject CreateButtonElement(string name, GameObject parent, string text, Vector2 anchoredPos)
        {
            GameObject btnGo = new GameObject(name);
            btnGo.transform.SetParent(parent.transform, false);
            
            RectTransform rect = btnGo.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(250f, 50f);
            rect.anchoredPosition = anchoredPos;
            
            Image img = btnGo.AddComponent<Image>();
            img.color = new Color(0.25f, 0.2f, 0.15f, 1f);
            
            Button btn = btnGo.AddComponent<Button>();
            
            GameObject textGo = new GameObject("Text");
            textGo.transform.SetParent(btnGo.transform, false);
            RectTransform txtRect = textGo.AddComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.offsetMin = Vector2.zero;
            txtRect.offsetMax = Vector2.one;
            
            Text txt = textGo.AddComponent<Text>();
            txt.text = text;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 18;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            
            return btnGo;
        }

        private void CreateSlotButton(int slotIndex, Vector2 anchoredPos)
        {
            GameObject btnGo = CreateButtonElement($"Slot_{slotIndex}_Button", saveLoadPanelGo, $"Slot {slotIndex}", anchoredPos);
            btnGo.GetComponent<RectTransform>().sizeDelta = new Vector2(400f, 60f);
            
            Button btn = btnGo.GetComponent<Button>();
            btn.onClick.AddListener(() => {
                HandleSlotSelected(slotIndex);
            });
        }

        private void HandleSlotSelected(int slotIndex)
        {
            if (isSavingMode)
            {
                SaveManager.Save(slotIndex);
                saveLoadPanelGo.SetActive(false);
                HandleStateChanged(GameManager.Instance.CurrentState);
                if (DamageTextPool.Instance != null)
                {
                    bool isTR = currentLanguage == GameLanguage.Turkish;
                    string msg = isTR ? $"Oyun Kaydedildi (Slot {slotIndex})" : $"Game Saved (Slot {slotIndex})";
                    GameObject pGo = GameObject.FindWithTag("Player");
                    Vector3 spawnPos = pGo != null ? pGo.transform.position : Vector3.zero;
                    DamageTextPool.Instance.SpawnText(spawnPos, msg, Color.green);
                }
            }
            else
            {
                GameSaveData data = SaveManager.Load(slotIndex);
                if (data != null)
                {
                    saveLoadPanelGo.SetActive(false);
                    
                    if (data.levelIndex != GameManager.Instance.CurrentLevelIndex || GameManager.Instance.CurrentState == GameState.MainMenu)
                    {
                        GameManager.shouldStartInGameplay = true;
                        GameManager.pendingLoadData = data;
                        GameManager.Instance.LoadLevel(data.levelIndex);
                    }
                    else
                    {
                        GameManager.ApplySaveData(data);
                        GameManager.Instance.UpdateState(GameState.Gameplay);
                    }
                    
                    if (DamageTextPool.Instance != null)
                    {
                        bool isTR = currentLanguage == GameLanguage.Turkish;
                        string msg = isTR ? $"Oyun Yüklendi (Slot {slotIndex})" : $"Game Loaded (Slot {slotIndex})";
                        GameObject pGo = GameObject.FindWithTag("Player");
                        Vector3 spawnPos = pGo != null ? pGo.transform.position : Vector3.zero;
                        DamageTextPool.Instance.SpawnText(spawnPos, msg, Color.green);
                    }
                }
                else
                {
                    if (DamageTextPool.Instance != null)
                    {
                        bool isTR = currentLanguage == GameLanguage.Turkish;
                        string msg = isTR ? "Slot Boş!" : "Slot Empty!";
                        GameObject pGo = GameObject.FindWithTag("Player");
                        Vector3 spawnPos = pGo != null ? pGo.transform.position : Vector3.zero;
                        DamageTextPool.Instance.SpawnText(spawnPos, msg, Color.red);
                    }
                }
            }
        }

        private void EnsureMainMenuButtons()
        {
            if (mainMenuPanel == null) return;

            foreach (Transform t in mainMenuPanel.transform)
            {
                if (t.name.Contains("Button") || t.name == "PlayButton" || t.name == "ShopButton" || t.name == "QuitButton" || t.name.Contains("Btn"))
                {
                    t.gameObject.SetActive(false);
                }
            }

            bool isTR = currentLanguage == GameLanguage.Turkish;

            CreateOrUpdateMenuButton("NewGameBtn", isTR ? "YENİ OYUN" : "NEW GAME", new Vector2(0f, 120f), () => {
                ShowStoryIntro();
            });
            
            CreateOrUpdateMenuButton("LoadGameBtn", isTR ? "KAYIT YÜKLE" : "LOAD GAME", new Vector2(0f, 40f), () => {
                OpenSaveLoadAction(false);
            });
            
            CreateOrUpdateMenuButton("SettingsBtn", isTR ? "AYARLAR" : "SETTINGS", new Vector2(0f, -40f), () => {
                OpenSettingsAction();
            });
            
            CreateOrUpdateMenuButton("QuitBtn", isTR ? "ÇIKIŞ" : "QUIT", new Vector2(0f, -120f), () => {
                QuitButtonAction();
            });
        }

        private GameObject CreateOrUpdateMenuButton(string name, string text, Vector2 anchoredPos, UnityEngine.Events.UnityAction callback)
        {
            Transform t = mainMenuPanel.transform.Find(name);
            GameObject btnGo;
            if (t != null)
            {
                btnGo = t.gameObject;
                btnGo.SetActive(true);
            }
            else
            {
                btnGo = CreateButtonElement(name, mainMenuPanel, text, anchoredPos);
            }
            
            btnGo.GetComponent<RectTransform>().anchoredPosition = anchoredPos;
            Text btnTxt = btnGo.transform.Find("Text")?.GetComponent<Text>();
            if (btnTxt != null) btnTxt.text = text;
            
            Button btn = btnGo.GetComponent<Button>();
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(callback);
            
            return btnGo;
        }

        private void EnsurePausePanelButtons()
        {
            if (pausePanel == null) return;

            foreach (Transform t in pausePanel.transform)
            {
                if (t.name.Contains("Button") || t.name == "ResumeButton" || t.name == "RestartButton" || t.name == "MainMenuButton" || t.name.Contains("Btn"))
                {
                    t.gameObject.SetActive(false);
                }
            }

            bool isTR = currentLanguage == GameLanguage.Turkish;

            CreateOrUpdatePauseButton("ResumeBtn", isTR ? "DEVAM ET" : "RESUME", new Vector2(0f, 150f), () => {
                ResumeButtonAction();
            });
            CreateOrUpdatePauseButton("SaveBtn", isTR ? "OYUNU KAYDET" : "SAVE GAME", new Vector2(0f, 90f), () => {
                OpenSaveLoadAction(true);
            });
            CreateOrUpdatePauseButton("LoadBtn", isTR ? "KAYIT YÜKLE" : "LOAD GAME", new Vector2(0f, 30f), () => {
                OpenSaveLoadAction(false);
            });
            CreateOrUpdatePauseButton("SettingsBtn", isTR ? "AYARLAR" : "SETTINGS", new Vector2(0f, -30f), () => {
                OpenSettingsAction();
            });
            CreateOrUpdatePauseButton("MainMenuBtn", isTR ? "ANA MENÜYE DÖN" : "RETURN TO MAIN MENU", new Vector2(0f, -90f), () => {
                MainMenuButtonAction();
            });
            CreateOrUpdatePauseButton("QuitBtn", isTR ? "ÇIKIŞ" : "QUIT", new Vector2(0f, -150f), () => {
                QuitButtonAction();
            });
        }

        private GameObject CreateOrUpdatePauseButton(string name, string text, Vector2 anchoredPos, UnityEngine.Events.UnityAction callback)
        {
            Transform t = pausePanel.transform.Find(name);
            GameObject btnGo;
            if (t != null)
            {
                btnGo = t.gameObject;
                btnGo.SetActive(true);
            }
            else
            {
                btnGo = CreateButtonElement(name, pausePanel, text, anchoredPos);
            }
            
            btnGo.GetComponent<RectTransform>().anchoredPosition = anchoredPos;
            Text btnTxt = btnGo.transform.Find("Text")?.GetComponent<Text>();
            if (btnTxt != null) btnTxt.text = text;
            
            Button btn = btnGo.GetComponent<Button>();
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(callback);
            
            return btnGo;
        }

        private GameObject fullWorldMapPanelGo;
        private Image[] fullWorldMapCells = new Image[50];

        public bool IsWorldMapOpen()
        {
            return fullWorldMapPanelGo != null && fullWorldMapPanelGo.activeSelf;
        }

        public void ToggleFullWorldMapPanel()
        {
            EnsureFullWorldMapUI();
            if (fullWorldMapPanelGo != null)
            {
                bool isCurrentlyActive = fullWorldMapPanelGo.activeSelf;
                fullWorldMapPanelGo.SetActive(!isCurrentlyActive);
                
                if (fullWorldMapPanelGo.activeSelf)
                {
                    Time.timeScale = 0f;
                    RefreshFullWorldMapUI();
                }
                else
                {
                    if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Gameplay)
                    {
                        Time.timeScale = 1f;
                    }
                }
            }
        }

        public void RefreshFullWorldMapUI()
        {
            if (MapManager.Instance == null) return;

            int activeRoomId = MapManager.Instance.GetCurrentRoomId();
            bool isTR = currentLanguage == GameLanguage.Turkish;
            string prefix = isTR ? "harita" : "map";

            for (int i = 0; i < 50; i++)
            {
                if (fullWorldMapCells[i] == null) continue;

                var room = MapManager.Instance.rooms[i];
                Text cellTxt = fullWorldMapCells[i].GetComponentInChildren<Text>();
                
                if (i == activeRoomId - 1)
                {
                    // Active orange
                    fullWorldMapCells[i].color = new Color(0.9f, 0.45f, 0f, 1f);
                    if (cellTxt != null) { cellTxt.color = Color.white; cellTxt.text = $"{prefix}{i + 1}"; }
                }
                else if (room.state == RoomState.Discovered || room.state == RoomState.Cleared)
                {
                    // Cleared green
                    fullWorldMapCells[i].color = new Color(0.1f, 0.6f, 0.2f, 1f);
                    if (cellTxt != null) { cellTxt.color = Color.white; cellTxt.text = $"{prefix}{i + 1}"; }
                }
                else
                {
                    // Fog of war dark grey with faded text
                    fullWorldMapCells[i].color = new Color(0.12f, 0.12f, 0.15f, 0.85f);
                    if (cellTxt != null) { cellTxt.color = new Color(1f, 1f, 1f, 0.15f); cellTxt.text = $"{prefix}{i + 1}"; }
                }
            }
        }

        private void EnsureFullWorldMapUI()
        {
            GameObject canvas = GameObject.Find("Canvas");
            if (canvas == null) return;

            if (fullWorldMapPanelGo == null)
            {
                Transform existing = canvas.transform.Find("WorldMapPanel");
                if (existing != null)
                {
                    fullWorldMapPanelGo = existing.gameObject;
                    Image imgComp = fullWorldMapPanelGo.GetComponent<Image>();
                    if (imgComp != null) imgComp.color = new Color(0f, 0f, 0f, 0.4f);
                }
                else
                {
                    fullWorldMapPanelGo = new GameObject("WorldMapPanel");
                    fullWorldMapPanelGo.transform.SetParent(canvas.transform, false);
                    
                    RectTransform rect = fullWorldMapPanelGo.AddComponent<RectTransform>();
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.offsetMin = Vector2.zero;
                    rect.offsetMax = Vector2.one;
                    
                    Image img = fullWorldMapPanelGo.AddComponent<Image>();
                    img.color = new Color(0f, 0f, 0f, 0.4f);
                    
                    bool isTR = (currentLanguage == GameLanguage.Turkish);
                    string titleText = isTR ? "ZİNDAN HARİTASI" : "DUNGEON MAP";
                    string legendText = isTR 
                        ? "Turuncu: Aktif Oda | Yeşil: Temizlenmiş/Güvenli Bölge" 
                        : "Orange: Active | Green: Cleared/Safe Zone";
                    string closeText = isTR ? "KAPAT" : "CLOSE";
                    
                    CreateTextElement("Title", fullWorldMapPanelGo, titleText, 36, Color.yellow, new Vector2(0f, 290f));
                    CreateTextElement("Legend", fullWorldMapPanelGo, legendText, 18, Color.white, new Vector2(0f, 240f));
                    
                    GameObject gridContainer = new GameObject("GridContainer");
                    gridContainer.transform.SetParent(fullWorldMapPanelGo.transform, false);
                    
                    RectTransform gridRect = gridContainer.AddComponent<RectTransform>();
                    gridRect.sizeDelta = new Vector2(1060f, 500f);
                    gridRect.anchoredPosition = new Vector2(0f, -20f);
                    
                    GridLayoutGroup grid = gridContainer.AddComponent<GridLayoutGroup>();
                    grid.cellSize = new Vector2(100f, 90f);
                    grid.spacing = new Vector2(6f, 6f);
                    grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                    grid.constraintCount = 10;
                    grid.childAlignment = TextAnchor.MiddleCenter;
                    
                    for (int i = 0; i < 50; i++)
                    {
                        GameObject cellGo = new GameObject($"MapCell_{i}");
                        cellGo.transform.SetParent(gridContainer.transform, false);
                        
                        Image cellImg = cellGo.AddComponent<Image>();
                        cellImg.color = new Color(0.12f, 0.12f, 0.15f, 0.85f);
 
                        // Premium outline for "boxed" look
                        Outline outline = cellGo.AddComponent<Outline>();
                        outline.effectColor = new Color(0.35f, 0.35f, 0.4f, 0.5f);
                        outline.effectDistance = new Vector2(1.5f, 1.5f);
                        
                        GameObject txtGo = new GameObject("Text");
                        txtGo.transform.SetParent(cellGo.transform, false);
                        RectTransform txtRt = txtGo.AddComponent<RectTransform>();
                        txtRt.anchorMin = Vector2.zero;
                        txtRt.anchorMax = Vector2.one;
                        txtRt.offsetMin = Vector2.zero;
                        txtRt.offsetMax = Vector2.zero;
                        
                        Text cellTxt = txtGo.AddComponent<Text>();
                        cellTxt.text = isTR ? $"harita{i + 1}" : $"map{i + 1}";
                        cellTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                        cellTxt.fontSize = 18;
                        cellTxt.fontStyle = FontStyle.Bold;
                        cellTxt.alignment = TextAnchor.MiddleCenter;
                        cellTxt.color = new Color(1f, 1f, 1f, 0.15f);
                        
                        fullWorldMapCells[i] = cellImg;
 
                        // Click to teleport (Test Mode Disabled)
                        // Button cellBtn = cellGo.AddComponent<Button>();
                        // int targetRoomIndex = i;
                        // cellBtn.onClick.AddListener(() => {
                        //     ToggleFullWorldMapPanel();
                        //     TriggerRoomTransition(targetRoomIndex + 1);
                        // });
                    }
                    
                    GameObject closeBtnGo = CreateButtonElement("CloseButton", fullWorldMapPanelGo, closeText, new Vector2(0f, -300f));
                    closeBtnGo.GetComponent<Button>().onClick.AddListener(() => {
                        ToggleFullWorldMapPanel();
                    });
                    
                    fullWorldMapPanelGo.SetActive(false);
                }
            }
        }

        public void TriggerRoomTransition(int targetRoomId = -1)
        {
            StartCoroutine(RoomTransitionRoutine(targetRoomId));
        }

        private IEnumerator RoomTransitionRoutine(int targetRoomId = -1)
        {
            GameObject canvas = GameObject.Find("Canvas");
            if (canvas == null) yield break;

            GameObject fadeGo = null;
            Transform existing = canvas.transform.Find("FadePanel");
            if (existing != null)
            {
                fadeGo = existing.gameObject;
            }
            else
            {
                fadeGo = new GameObject("FadePanel");
                fadeGo.transform.SetParent(canvas.transform, false);
                RectTransform r = fadeGo.AddComponent<RectTransform>();
                r.anchorMin = Vector2.zero;
                r.anchorMax = Vector2.one;
                r.offsetMin = Vector2.zero;
                r.offsetMax = Vector2.one;
                fadeGo.AddComponent<Image>().color = Color.clear;
            }

            fadeGo.SetActive(true);
            Image fadeImg = fadeGo.GetComponent<Image>();
            
            GameObject player = GameObject.FindWithTag("Player");
            PlayerController pc = player != null ? player.GetComponent<PlayerController>() : null;
            if (pc != null) pc.SetControlsLocked(true);

            float elapsed = 0f;
            float duration = 0.25f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                fadeImg.color = new Color(0f, 0f, 0f, t * t);
                yield return null;
            }
            fadeImg.color = Color.black;

            if (MapManager.Instance != null && player != null)
            {
                int currentId = MapManager.Instance.GetCurrentRoomId();
                int nextRoomId = targetRoomId;
                if (nextRoomId <= 0)
                {
                    nextRoomId = currentId + 1;
                    if (nextRoomId > 50) nextRoomId = 1;
                }

                // Kural 1: Sonraki odaya geçerken Interstitial (Zorunlu) Reklam göster
                if (nextRoomId == currentId + 1 && Pulsevania.Core.AdManager.Instance != null)
                {
                    bool isAdClosed = false;
                    Pulsevania.Core.AdManager.Instance.ShowInterstitialAd(() =>
                    {
                        isAdClosed = true;
                    });

                    while (!isAdClosed)
                    {
                        yield return null;
                    }
                }

                bool enteringFromLeft = true;
                if (nextRoomId < currentId)
                {
                    enteringFromLeft = false;
                }

                MapManager.Instance.SetActiveRoom(nextRoomId, enteringFromLeft);
            }

            yield return new WaitForSeconds(0.05f);

            elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float alpha = 1f - (t * t);
                fadeImg.color = new Color(0f, 0f, 0f, alpha);
                yield return null;
            }
            fadeImg.color = Color.clear;
            fadeGo.SetActive(false);

            if (pc != null) pc.SetControlsLocked(false);
        }

        private Dictionary<int, Sprite> circularSpriteCache = new Dictionary<int, Sprite>();

        private void EnsureErgonomicMobileHUD()
        {
            // Apply Safe Area component to shift touch controls away from camera notches/cutouts
            if (gameplayHUD != null)
            {
                if (gameplayHUD.GetComponent<Pulsevania.UI.SafeArea>() == null)
                {
                    gameplayHUD.AddComponent<Pulsevania.UI.SafeArea>();
                }

                Image hudBg = gameplayHUD.GetComponent<Image>();
                if (hudBg != null) hudBg.color = Color.clear;

                Transform bgT = gameplayHUD.transform.Find("Background");
                if (bgT != null)
                {
                    Image bgImg = bgT.GetComponent<Image>();
                    if (bgImg != null) bgImg.color = Color.clear;
                }
                Transform bottomBarT = gameplayHUD.transform.Find("BottomBar");
                if (bottomBarT != null)
                {
                    Image bbImg = bottomBarT.GetComponent<Image>();
                    if (bbImg != null) bbImg.color = Color.clear;
                }
            }

            // Hide old arrow buttons to replace them with the Joystick
            if (btnLeft != null) btnLeft.gameObject.SetActive(false);
            if (btnRight != null) btnRight.gameObject.SetActive(false);
            if (btnUp != null) btnUp.gameObject.SetActive(false);
            if (btnDown != null) btnDown.gameObject.SetActive(false);

            // Create/Ensure Virtual Joystick on the left
            if (gameplayHUD != null)
            {
                Transform joystickTrans = gameplayHUD.transform.Find("VirtualJoystick");
                if (joystickTrans == null)
                {
                    // Create Joystick Background
                    GameObject joystickGo = new GameObject("VirtualJoystick");
                    joystickGo.transform.SetParent(gameplayHUD.transform, false);

                    RectTransform joyRect = joystickGo.AddComponent<RectTransform>();
                    joyRect.anchorMin = new Vector2(0f, 0f);
                    joyRect.anchorMax = new Vector2(0f, 0f);
                    joyRect.pivot = new Vector2(0.5f, 0.5f);
                    joyRect.anchoredPosition = new Vector2(160f, 160f); // Ergonomic bottom-left position
                    joyRect.sizeDelta = new Vector2(160f, 160f);

                    Image joyBgImg = joystickGo.AddComponent<Image>();
                    joyBgImg.sprite = CreateJoystickBackgroundSprite();

                    // Create Joystick Handle (Stick)
                    GameObject handleGo = new GameObject("Handle");
                    handleGo.transform.SetParent(joystickGo.transform, false);

                    RectTransform handleRect = handleGo.AddComponent<RectTransform>();
                    handleRect.anchorMin = new Vector2(0.5f, 0.5f);
                    handleRect.anchorMax = new Vector2(0.5f, 0.5f);
                    handleRect.pivot = new Vector2(0.5f, 0.5f);
                    handleRect.anchoredPosition = Vector2.zero;
                    handleRect.sizeDelta = new Vector2(72f, 72f); // Knob size

                    Image handleImg = handleGo.AddComponent<Image>();
                    handleImg.sprite = CreateJoystickHandleSprite();

                    // Add VirtualJoystick script
                    virtualJoystick = joystickGo.AddComponent<Pulsevania.UI.VirtualJoystick>();
                }
                else
                {
                    virtualJoystick = joystickTrans.GetComponent<Pulsevania.UI.VirtualJoystick>();
                }
            }

            // Right Side Action Cluster (Arched layout for maximum ergonomics)
            StyleCircularButton(btnAttack, new Vector2(130f, 130f), new Vector2(-90f, 90f), new Vector2(1f, 0f), "ATK");
            StyleCircularButton(btnJump, new Vector2(120f, 120f), new Vector2(-230f, 90f), new Vector2(1f, 0f), "JUMP");
            
            if (btnKnife != null)
            {
                StyleCircularButton(btnKnife, new Vector2(120f, 120f), new Vector2(-160f, 190f), new Vector2(1f, 0f), "KNIFE");
            }

            StyleCircularButton(btnBlock, new Vector2(90f, 90f), new Vector2(-340f, 200f), new Vector2(1f, 0f), "BLOCK");
            StyleCircularButton(btnUsePotion, new Vector2(90f, 90f), new Vector2(-250f, 290f), new Vector2(1f, 0f), "POTION");
            StyleCircularButton(btnOpenInventory, new Vector2(80f, 80f), new Vector2(-90f, 290f), new Vector2(1f, 0f), "INV");
        }

        private void StyleCircularButton(Button btn, Vector2 size, Vector2 anchoredPos, Vector2 anchor, string labelText)
        {
            if (btn == null) return;
            StyleCircularButtonGo(btn.gameObject, size, anchoredPos, anchor, labelText);
        }

        private void StyleCircularButton(MobileHoldButton btn, Vector2 size, Vector2 anchoredPos, Vector2 anchor, string labelText)
        {
            if (btn == null) return;
            StyleCircularButtonGo(btn.gameObject, size, anchoredPos, anchor, labelText);
        }

        private Sprite cachedLeftArrow;
        private Sprite cachedRightArrow;
        private Sprite cachedUpArrow;
        private Sprite cachedDownArrow;

        private Sprite CreateArrowSprite(string direction)
        {
            if (direction == "left" && cachedLeftArrow != null) return cachedLeftArrow;
            if (direction == "right" && cachedRightArrow != null) return cachedRightArrow;
            if (direction == "up" && cachedUpArrow != null) return cachedUpArrow;
            if (direction == "down" && cachedDownArrow != null) return cachedDownArrow;

            int width = 16;
            int height = 16;
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool isArrowPixel = false;
                    if (direction == "left")
                    {
                        isArrowPixel = (x >= 3 && x <= 11 && y >= 8 - (x - 3) && y <= 8 + (x - 3) && x <= 8);
                        if (x > 8 && x <= 12 && y >= 6 && y <= 10) isArrowPixel = true;
                    }
                    else if (direction == "right")
                    {
                        isArrowPixel = (x >= 4 && x <= 12 && y >= 8 - (12 - x) && y <= 8 + (12 - x) && x >= 8);
                        if (x >= 3 && x < 8 && y >= 6 && y <= 10) isArrowPixel = true;
                    }
                    else if (direction == "up")
                    {
                        isArrowPixel = (y >= 4 && y <= 12 && x >= 8 - (12 - y) && x <= 8 + (12 - y) && y >= 8);
                        if (y >= 3 && y < 8 && x >= 6 && x <= 10) isArrowPixel = true;
                    }
                    else if (direction == "down")
                    {
                        isArrowPixel = (y >= 3 && y <= 11 && x >= 8 - (y - 3) && x <= 8 + (y - 3) && y <= 8);
                        if (y > 8 && y <= 12 && x >= 6 && x <= 10) isArrowPixel = true;
                    }

                    if (isArrowPixel)
                    {
                        tex.SetPixel(x, y, Color.white);
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            tex.Apply();
            Sprite generated = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 16f);

            if (direction == "left") cachedLeftArrow = generated;
            else if (direction == "right") cachedRightArrow = generated;
            else if (direction == "up") cachedUpArrow = generated;
            else if (direction == "down") cachedDownArrow = generated;

            return generated;
        }

        private Sprite CreateJoystickBackgroundSprite()
        {
            int size = 128;
            Texture2D tex = new Texture2D(size, size);
            tex.filterMode = FilterMode.Point;

            float cx = size / 2f;
            float cy = size / 2f;
            float r = size / 2f - 2f;

            Color gold = new Color(0.85f, 0.7f, 0.2f, 0.85f);
            Color darkMetal = new Color(0.12f, 0.14f, 0.18f, 0.85f);
            Color darkFill = new Color(0.08f, 0.09f, 0.12f, 0.45f);

            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    float dx = x - cx;
                    float dy = y - cy;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    if (dist > r)
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                    else if (dist >= r - 4f)
                    {
                        tex.SetPixel(x, y, gold);
                    }
                    else if (dist >= r - 6f)
                    {
                        tex.SetPixel(x, y, darkMetal);
                    }
                    else
                    {
                        tex.SetPixel(x, y, darkFill);
                    }
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private Sprite CreateJoystickHandleSprite()
        {
            int size = 64;
            Texture2D tex = new Texture2D(size, size);
            tex.filterMode = FilterMode.Point;

            float cx = size / 2f;
            float cy = size / 2f;
            float r = size / 2f - 1f;

            Color goldLight = new Color(1f, 0.85f, 0.4f, 0.95f);
            Color goldDark = new Color(0.75f, 0.6f, 0.15f, 0.95f);
            Color metalCenter = new Color(0.2f, 0.22f, 0.25f, 0.95f);

            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    float dx = x - cx;
                    float dy = y - cy;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    if (dist > r)
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                    else if (dist >= r - 2f)
                    {
                        tex.SetPixel(x, y, goldLight);
                    }
                    else if (dist >= r - 4f)
                    {
                        tex.SetPixel(x, y, goldDark);
                    }
                    else
                    {
                        float t = (float)y / size;
                        Color c = Color.Lerp(metalCenter, goldDark * 0.6f, t);
                        tex.SetPixel(x, y, c);
                    }
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private void StyleCircularButtonGo(GameObject btnGo, Vector2 size, Vector2 anchoredPos, Vector2 anchor, string labelText)
        {
            RectTransform rt = btnGo.GetComponent<RectTransform>();
            if (rt == null) rt = btnGo.AddComponent<RectTransform>();

            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;

            Image img = btnGo.GetComponent<Image>();
            if (img == null) img = btnGo.AddComponent<Image>();

            img.sprite = CreateCircularBorderSprite((int)size.x);
            img.color = new Color(1f, 1f, 1f, 0.35f);
            img.raycastTarget = true;

            var outline = btnGo.GetComponent<Outline>();
            if (outline != null) DestroyImmediate(outline);
            var shadow = btnGo.GetComponent<Shadow>();
            if (shadow != null) DestroyImmediate(shadow);

            Text txt = btnGo.GetComponentInChildren<Text>();
            if (txt == null)
            {
                GameObject txtGo = new GameObject("Text");
                txtGo.transform.SetParent(btnGo.transform, false);
                txt = txtGo.AddComponent<Text>();
            }

            RectTransform txtRt = txt.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = Vector2.zero;
            txtRt.offsetMax = Vector2.zero;
            txtRt.anchoredPosition = Vector2.zero;
            txtRt.localPosition = Vector3.zero;
            txtRt.localScale = Vector3.one;

            bool isArrow = (labelText == "◀" || labelText == "▶" || labelText == "▲" || labelText == "▼");
            
            Transform oldIcon = btnGo.transform.Find("ArrowIcon");
            if (oldIcon != null)
            {
                if (Application.isPlaying) Destroy(oldIcon.gameObject);
                else DestroyImmediate(oldIcon.gameObject);
            }

            if (isArrow)
            {
                txt.text = "";
                
                GameObject iconGo = new GameObject("ArrowIcon");
                iconGo.transform.SetParent(btnGo.transform, false);
                RectTransform iconRt = iconGo.AddComponent<RectTransform>();
                iconRt.anchorMin = new Vector2(0.5f, 0.5f);
                iconRt.anchorMax = new Vector2(0.5f, 0.5f);
                iconRt.pivot = new Vector2(0.5f, 0.5f);
                iconRt.sizeDelta = new Vector2(size.x * 0.5f, size.y * 0.5f);
                iconRt.anchoredPosition = Vector2.zero;

                Image iconImg = iconGo.AddComponent<Image>();
                string dir = "left";
                if (labelText == "◀") dir = "left";
                else if (labelText == "▶") dir = "right";
                else if (labelText == "▲") dir = "up";
                else if (labelText == "▼") dir = "down";

                iconImg.sprite = CreateArrowSprite(dir);
                iconImg.color = Color.white;
                iconImg.raycastTarget = false;
            }
            else
            {
                string finalLabel = labelText;
                bool isTR = currentLanguage == GameLanguage.Turkish;
                if (labelText == "ATK") finalLabel = isTR ? "SALDIR" : "ATK";
                else if (labelText == "JUMP") finalLabel = isTR ? "ZIPLA" : "JUMP";
                else if (labelText == "KNIFE") finalLabel = isTR ? "BIÇAK" : "KNIFE";
                else if (labelText == "BLOCK") finalLabel = isTR ? "BLOK" : "BLOCK";
                else if (labelText == "POTION") finalLabel = isTR ? "İKSİR" : "POTION";
                else if (labelText == "INV") finalLabel = isTR ? "ENV" : "INV";
                
                txt.text = finalLabel;
            }

            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.fontStyle = FontStyle.Bold;

            txt.fontSize = (int)(size.x * 0.25f);
            txt.resizeTextForBestFit = false;
        }

        private Sprite CreateCircularBorderSprite(int size)
        {
            if (circularSpriteCache == null) circularSpriteCache = new Dictionary<int, Sprite>();
            if (circularSpriteCache.ContainsKey(size)) return circularSpriteCache[size];

            Texture2D tex = new Texture2D(size, size);
            float radius = size * 0.5f;
            float radiusSqr = radius * radius;
            float borderThickness = Mathf.Max(2f, size * 0.05f);
            float innerRadiusSqr = (radius - borderThickness) * (radius - borderThickness);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - radius;
                    float dy = y - radius;
                    float distSqr = dx * dx + dy * dy;

                    if (distSqr <= radiusSqr)
                    {
                        if (distSqr >= innerRadiusSqr)
                        {
                            tex.SetPixel(x, y, Color.white);
                        }
                        else
                        {
                            tex.SetPixel(x, y, new Color(1f, 1f, 1f, 0.15f));
                        }
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            tex.filterMode = FilterMode.Point;
            tex.Apply();

            Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            circularSpriteCache[size] = sprite;
            return sprite;
        }

        private Scrollbar CreateProgrammaticScrollbar(GameObject parent)
        {
            GameObject scrollbarGo = new GameObject("VerticalScrollbar");
            scrollbarGo.transform.SetParent(parent.transform, false);
            RectTransform rtScrollbar = scrollbarGo.AddComponent<RectTransform>();
            rtScrollbar.anchorMin = new Vector2(1f, 0f);
            rtScrollbar.anchorMax = new Vector2(1f, 1f);
            rtScrollbar.pivot = new Vector2(1f, 0.5f);
            rtScrollbar.anchoredPosition = new Vector2(-5f, 0f);
            rtScrollbar.sizeDelta = new Vector2(15f, -10f);

            Image bgImg = scrollbarGo.AddComponent<Image>();
            bgImg.color = new Color(0.1f, 0.07f, 0.05f, 0.8f);

            Scrollbar scrollbar = scrollbarGo.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;

            // Sliding Area
            GameObject slidingArea = new GameObject("Sliding Area");
            slidingArea.transform.SetParent(scrollbarGo.transform, false);
            RectTransform rtSliding = slidingArea.AddComponent<RectTransform>();
            rtSliding.anchorMin = Vector2.zero;
            rtSliding.anchorMax = Vector2.one;
            rtSliding.sizeDelta = Vector2.zero;
            rtSliding.anchoredPosition = Vector2.zero;

            // Handle
            GameObject handle = new GameObject("Handle");
            handle.transform.SetParent(slidingArea.transform, false);
            RectTransform rtHandle = handle.AddComponent<RectTransform>();
            rtHandle.anchorMin = new Vector2(0f, 0f);
            rtHandle.anchorMax = new Vector2(1f, 1f);
            rtHandle.sizeDelta = Vector2.zero;
            rtHandle.anchoredPosition = Vector2.zero;

            Image handleImg = handle.AddComponent<Image>();
            handleImg.color = new Color(0.95f, 0.65f, 0.18f, 1f); // Gold handle

            scrollbar.handleRect = rtHandle;
            return scrollbar;
        }

        private GameObject hudStatsContainer;
        private Image hudGoldIcon;
        private Image hudKeysIcon;
        private Image hudPotionsIcon;

        private void EnsureHUDStatsContainer(GameObject gameplayHUDGo)
        {
            if (gameplayHUDGo == null) return;

            // Hide old standalone texts in pre-existing HUD
            Transform oldGoldText = gameplayHUDGo.transform.Find("GoldText");
            if (oldGoldText != null) oldGoldText.gameObject.SetActive(false);
            Transform oldKeysText = gameplayHUDGo.transform.Find("KeysText");
            if (oldKeysText != null) oldKeysText.gameObject.SetActive(false);
            Transform oldPotionsText = gameplayHUDGo.transform.Find("PotionsText");
            if (oldPotionsText != null) oldPotionsText.gameObject.SetActive(false);

            Transform oldContainer = gameplayHUDGo.transform.Find("HUDStatsRow");
            if (oldContainer != null)
            {
                if (Application.isPlaying) Destroy(oldContainer.gameObject);
                else DestroyImmediate(oldContainer.gameObject);
            }

            // Create container row next to health percent label
            GameObject rowGo = new GameObject("HUDStatsRow");
            rowGo.transform.SetParent(gameplayHUDGo.transform, false);
            
            RectTransform rtRow = rowGo.AddComponent<RectTransform>();
            rtRow.anchorMin = new Vector2(0f, 1f);
            rtRow.anchorMax = new Vector2(0f, 1f);
            rtRow.pivot = new Vector2(0f, 1f);
            rtRow.anchoredPosition = new Vector2(104f, -86f); // Positioned directly below the extra hearts
            rtRow.sizeDelta = new Vector2(600f, 50f);

            HorizontalLayoutGroup layout = rowGo.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 35f; // Extra spacing for larger items
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleLeft;

            // 1. Gold Indicator Group
            GameObject goldGroup = new GameObject("GoldGroup");
            goldGroup.transform.SetParent(rowGo.transform, false);
            RectTransform rtGGroup = goldGroup.AddComponent<RectTransform>();
            rtGGroup.sizeDelta = new Vector2(130f, 45f);

            GameObject gIconGo = new GameObject("Icon");
            gIconGo.transform.SetParent(goldGroup.transform, false);
            RectTransform rtGIcon = gIconGo.AddComponent<RectTransform>();
            rtGIcon.anchorMin = new Vector2(0f, 0.5f);
            rtGIcon.anchorMax = new Vector2(0f, 0.5f);
            rtGIcon.pivot = new Vector2(0f, 0.5f);
            rtGIcon.anchoredPosition = new Vector2(0f, 0f);
            rtGIcon.sizeDelta = new Vector2(42f, 42f); // Enlarged from 28 to 42
            hudGoldIcon = gIconGo.AddComponent<Image>();
            hudGoldIcon.sprite = CreateHUDGoldSprite();

            GameObject gTextGo = new GameObject("Text");
            gTextGo.transform.SetParent(goldGroup.transform, false);
            RectTransform rtGText = gTextGo.AddComponent<RectTransform>();
            rtGText.anchorMin = new Vector2(0f, 0.5f);
            rtGText.anchorMax = new Vector2(1f, 0.5f);
            rtGText.pivot = new Vector2(0f, 0.5f);
            rtGText.anchoredPosition = new Vector2(50f, 0f); // Offset to fit larger icon
            rtGText.sizeDelta = new Vector2(80f, 35f);
            goldText = gTextGo.AddComponent<Text>();
            goldText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            goldText.fontSize = 28; // Enlarged from 20 to 28
            goldText.fontStyle = FontStyle.Bold;
            goldText.color = Color.yellow;
            goldText.alignment = TextAnchor.MiddleLeft;

            // 2. Keys Indicator Group
            GameObject keysGroup = new GameObject("KeysGroup");
            keysGroup.transform.SetParent(rowGo.transform, false);
            RectTransform rtKGroup = keysGroup.AddComponent<RectTransform>();
            rtKGroup.sizeDelta = new Vector2(110f, 45f);

            GameObject kIconGo = new GameObject("Icon");
            kIconGo.transform.SetParent(keysGroup.transform, false);
            RectTransform rtKIcon = kIconGo.AddComponent<RectTransform>();
            rtKIcon.anchorMin = new Vector2(0f, 0.5f);
            rtKIcon.anchorMax = new Vector2(0f, 0.5f);
            rtKIcon.pivot = new Vector2(0f, 0.5f);
            rtKIcon.anchoredPosition = new Vector2(0f, 0f);
            rtKIcon.sizeDelta = new Vector2(42f, 42f); // Enlarged from 28 to 42
            hudKeysIcon = kIconGo.AddComponent<Image>();
            hudKeysIcon.sprite = CreateHUDKeySprite();

            GameObject kTextGo = new GameObject("Text");
            kTextGo.transform.SetParent(keysGroup.transform, false);
            RectTransform rtKText = kTextGo.AddComponent<RectTransform>();
            rtKText.anchorMin = new Vector2(0f, 0.5f);
            rtKText.anchorMax = new Vector2(1f, 0.5f);
            rtKText.pivot = new Vector2(0f, 0.5f);
            rtKText.anchoredPosition = new Vector2(50f, 0f); // Offset to fit larger icon
            rtKText.sizeDelta = new Vector2(60f, 35f);
            keysText = kTextGo.AddComponent<Text>();
            keysText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            keysText.fontSize = 28; // Enlarged from 20 to 28
            keysText.fontStyle = FontStyle.Bold;
            keysText.color = new Color(0.7f, 0f, 1f); // Purple Keys
            keysText.alignment = TextAnchor.MiddleLeft;

            // 3. Potions Indicator Group
            GameObject potionsGroup = new GameObject("PotionsGroup");
            potionsGroup.transform.SetParent(rowGo.transform, false);
            RectTransform rtPGroup = potionsGroup.AddComponent<RectTransform>();
            rtPGroup.sizeDelta = new Vector2(110f, 45f);

            GameObject pIconGo = new GameObject("Icon");
            pIconGo.transform.SetParent(potionsGroup.transform, false);
            RectTransform rtPIcon = pIconGo.AddComponent<RectTransform>();
            rtPIcon.anchorMin = new Vector2(0f, 0.5f);
            rtPIcon.anchorMax = new Vector2(0f, 0.5f);
            rtPIcon.pivot = new Vector2(0f, 0.5f);
            rtPIcon.anchoredPosition = new Vector2(0f, 0f);
            rtPIcon.sizeDelta = new Vector2(42f, 42f); // Enlarged from 28 to 42
            hudPotionsIcon = pIconGo.AddComponent<Image>();
            hudPotionsIcon.sprite = CreateHUDPotionSprite();

            GameObject pTextGo = new GameObject("Text");
            pTextGo.transform.SetParent(potionsGroup.transform, false);
            RectTransform rtPText = pTextGo.AddComponent<RectTransform>();
            rtPText.anchorMin = new Vector2(0f, 0.5f);
            rtPText.anchorMax = new Vector2(1f, 0.5f);
            rtPText.pivot = new Vector2(0f, 0.5f);
            rtPText.anchoredPosition = new Vector2(50f, 0f); // Offset to fit larger icon
            rtPText.sizeDelta = new Vector2(60f, 35f);
            potionsText = pTextGo.AddComponent<Text>();
            potionsText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            potionsText.fontSize = 28; // Enlarged from 20 to 28
            potionsText.fontStyle = FontStyle.Bold;
            potionsText.color = new Color(0.9f, 0.1f, 0.1f); // Red Potions
            potionsText.alignment = TextAnchor.MiddleLeft;

            hudStatsContainer = rowGo;
        }

        private Sprite CreateHUDGoldSprite()
        {
            Texture2D tex = new Texture2D(16, 16);
            float r = 8f;
            for (int x = 0; x < 16; x++) 
            {
                for (int y = 0; y < 16; y++) 
                {
                    float dx = x - r;
                    float dy = y - r;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist <= r)
                    {
                        if (dist >= r - 1.5f)
                            tex.SetPixel(x, y, Color.black);
                        else if (dist <= 3.5f)
                            tex.SetPixel(x, y, new Color(0.8f, 0.45f, 0f));
                        else
                            tex.SetPixel(x, y, Color.yellow);
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            tex.filterMode = FilterMode.Point;
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16f);
        }

        private Sprite CreateHUDKeySprite()
        {
            Texture2D texk = new Texture2D(16, 16);
            for (int x = 0; x < 16; x++) 
            {
                for (int y = 0; y < 16; y++) 
                {
                    bool isRing = (x > 4 && x < 11 && y > 9 && y < 15);
                    bool isRingHole = (x > 6 && x < 9 && y > 10 && y < 13);
                    bool isShaft = (x == 7 && y > 2 && y <= 9);
                    bool isTeeth = (x == 8 || x == 9) && (y == 3 || y == 5);

                    if ((isRing && !isRingHole) || isShaft || isTeeth)
                    {
                        if (isRingHole || (isRing && (x == 5 || x == 10 || y == 10 || y == 14)))
                            texk.SetPixel(x, y, Color.black);
                        else
                            texk.SetPixel(x, y, new Color(0.7f, 0f, 1f));
                    }
                    else
                    {
                        texk.SetPixel(x, y, Color.clear);
                    }
                }
            }
            texk.filterMode = FilterMode.Point;
            texk.Apply();
            return Sprite.Create(texk, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16f);
        }

        private Sprite CreateHUDPotionSprite()
        {
            Texture2D texp = new Texture2D(16, 16);
            for (int x = 0; x < 16; x++) 
            {
                for (int y = 0; y < 16; y++) 
                {
                    bool isFlask = (x > 3 && x < 12 && y > 1 && y < 11) || (x > 6 && x < 9 && y >= 11 && y < 15);
                    bool isBorder = (x == 4 || x == 11 || y == 2 || y == 10) && (y > 1 && y < 11);
                    
                    if (isFlask)
                    {
                        if (isBorder || y == 14 || x == 6 || x == 9)
                            texp.SetPixel(x, y, Color.black);
                        else if (y < 9)
                            texp.SetPixel(x, y, new Color(0.9f, 0.1f, 0.1f));
                        else
                            texp.SetPixel(x, y, Color.white);
                    }
                    else
                    {
                        texp.SetPixel(x, y, Color.clear);
                    }
                }
            }
            texp.filterMode = FilterMode.Point;
            texp.Apply();
            return Sprite.Create(texp, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16f);
        }

        private int currentStorySlide = 0;
        private GameObject storyPanelGo;
        private Text storyTitleText;
        private Text storyContentText;
        private Image storyIllustrationImg;

        public void ShowStoryIntro()
        {
            currentStorySlide = 0;

            GameObject canvas = GameObject.Find("Canvas");
            if (canvas == null)
            {
                Canvas mainCanvas = GameObject.FindObjectOfType<Canvas>();
                if (mainCanvas != null) canvas = mainCanvas.gameObject;
            }

            if (canvas == null)
            {
                if (GameManager.Instance != null) GameManager.Instance.NewGame();
                return;
            }

            // Find or create story panel under Canvas
            Transform existing = canvas.transform.Find("StoryIntroPanel");
            if (existing != null)
            {
                storyPanelGo = existing.gameObject;
            }
            else
            {
                storyPanelGo = new GameObject("StoryIntroPanel");
                storyPanelGo.transform.SetParent(canvas.transform, false);
                
                RectTransform rt = storyPanelGo.AddComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.sizeDelta = Vector2.zero;

                Image bg = storyPanelGo.AddComponent<Image>();
                bg.color = new Color(0.06f, 0.05f, 0.08f, 0.96f); // Glassmorphism dark background

                // Container window (Expanded to 860x500 to fit left graphic side-by-side)
                GameObject container = new GameObject("Container");
                container.transform.SetParent(storyPanelGo.transform, false);
                RectTransform rtCont = container.AddComponent<RectTransform>();
                rtCont.anchorMin = new Vector2(0.5f, 0.5f);
                rtCont.anchorMax = new Vector2(0.5f, 0.5f);
                rtCont.pivot = new Vector2(0.5f, 0.5f);
                rtCont.sizeDelta = new Vector2(860f, 500f);

                Image contImg = container.AddComponent<Image>();
                contImg.color = new Color(0.12f, 0.08f, 0.06f, 0.98f); // Beautiful dark brown panel

                // Left Graphic / Illustration Image
                GameObject illustrationGo = new GameObject("Illustration");
                illustrationGo.transform.SetParent(container.transform, false);
                RectTransform rtIllus = illustrationGo.AddComponent<RectTransform>();
                rtIllus.anchorMin = new Vector2(0f, 0.5f);
                rtIllus.anchorMax = new Vector2(0f, 0.5f);
                rtIllus.pivot = new Vector2(0f, 0.5f);
                rtIllus.anchoredPosition = new Vector2(30f, 10f); // 30px margin
                rtIllus.sizeDelta = new Vector2(340f, 340f);

                storyIllustrationImg = illustrationGo.AddComponent<Image>();

                // Title/Speaker text (Shifted to the right side of the container, left-anchored)
                GameObject titleGo = new GameObject("Title");
                titleGo.transform.SetParent(container.transform, false);
                RectTransform rtTitle = titleGo.AddComponent<RectTransform>();
                rtTitle.anchorMin = new Vector2(0f, 1f);
                rtTitle.anchorMax = new Vector2(0f, 1f);
                rtTitle.pivot = new Vector2(0f, 1f); // Left pivot for name alignment
                rtTitle.anchoredPosition = new Vector2(400f, -40f); // Placed on the right half
                rtTitle.sizeDelta = new Vector2(300f, 50f);

                storyTitleText = titleGo.AddComponent<Text>();
                storyTitleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                storyTitleText.fontSize = 44; // Enlarged from 36
                storyTitleText.fontStyle = FontStyle.Bold;
                storyTitleText.alignment = TextAnchor.MiddleLeft;
                storyTitleText.color = Color.yellow;

                // Story Content dialog text (Shifted to the right side, left-anchored)
                GameObject contentGo = new GameObject("Content");
                contentGo.transform.SetParent(container.transform, false);
                RectTransform rtContent = contentGo.AddComponent<RectTransform>();
                rtContent.anchorMin = new Vector2(0f, 0.5f);
                rtContent.anchorMax = new Vector2(0f, 0.5f);
                rtContent.pivot = new Vector2(0f, 0.5f); // Left pivot for dialog alignment
                rtContent.anchoredPosition = new Vector2(400f, 25f); // Shifted slightly higher to accommodate larger text size
                rtContent.sizeDelta = new Vector2(430f, 280f);

                storyContentText = contentGo.AddComponent<Text>();
                storyContentText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                storyContentText.fontSize = 28; // Enlarged from 24
                storyContentText.fontStyle = FontStyle.Normal;
                storyContentText.alignment = TextAnchor.MiddleLeft;
                storyContentText.color = Color.white;

                // Next Button (Devam Et)
                GameObject btnGo = new GameObject("NextBtn");
                btnGo.transform.SetParent(container.transform, false);
                RectTransform rtBtn = btnGo.AddComponent<RectTransform>();
                rtBtn.anchorMin = new Vector2(0f, 0f);
                rtBtn.anchorMax = new Vector2(0f, 0f);
                rtBtn.pivot = new Vector2(0f, 0f);
                rtBtn.anchoredPosition = new Vector2(400f, 30f); // Placed under the dialog text
                rtBtn.sizeDelta = new Vector2(200f, 55f);

                Image btnImg = btnGo.AddComponent<Image>();
                btnImg.color = new Color(0.25f, 0.18f, 0.12f, 1f);

                Button btn = btnGo.AddComponent<Button>();
                btn.onClick.AddListener(OnNextStorySlide);

                GameObject btnTxtGo = new GameObject("Text");
                btnTxtGo.transform.SetParent(btnGo.transform, false);
                RectTransform rtBtnTxt = btnTxtGo.AddComponent<RectTransform>();
                rtBtnTxt.anchorMin = Vector2.zero;
                rtBtnTxt.anchorMax = Vector2.one;
                rtBtnTxt.sizeDelta = Vector2.zero;

                Text btnTxt = btnTxtGo.AddComponent<Text>();
                btnTxt.text = "DEVAM ET";
                btnTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                btnTxt.fontSize = 24; // Enlarged from 20
                btnTxt.fontStyle = FontStyle.Bold;
                btnTxt.alignment = TextAnchor.MiddleCenter;
                btnTxt.color = Color.white;

                // Skip Button (GEÇ) on Top-Right of container
                GameObject skipGo = new GameObject("SkipBtn");
                skipGo.transform.SetParent(container.transform, false);
                RectTransform rtSkip = skipGo.AddComponent<RectTransform>();
                rtSkip.anchorMin = new Vector2(1f, 1f);
                rtSkip.anchorMax = new Vector2(1f, 1f);
                rtSkip.pivot = new Vector2(1f, 1f);
                rtSkip.anchoredPosition = new Vector2(-30f, -30f); // Top-right corner with 30px padding
                rtSkip.sizeDelta = new Vector2(120f, 45f);

                Image skipImg = skipGo.AddComponent<Image>();
                skipImg.color = new Color(0.35f, 0.15f, 0.15f, 0.85f); // Sleek reddish skip theme

                Button skipBtn = skipGo.AddComponent<Button>();
                skipBtn.onClick.AddListener(OnSkipStory);

                GameObject skipTxtGo = new GameObject("Text");
                skipTxtGo.transform.SetParent(skipGo.transform, false);
                RectTransform rtSkipTxt = skipTxtGo.AddComponent<RectTransform>();
                rtSkipTxt.anchorMin = Vector2.zero;
                rtSkipTxt.anchorMax = Vector2.one;
                rtSkipTxt.sizeDelta = Vector2.zero;

                Text skipTxt = skipTxtGo.AddComponent<Text>();
                skipTxt.text = "GEC (SKIP)";
                skipTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                skipTxt.fontSize = 20; // Enlarged from 16
                skipTxt.fontStyle = FontStyle.Bold;
                skipTxt.alignment = TextAnchor.MiddleCenter;
                skipTxt.color = Color.white;
            }

            storyPanelGo.SetActive(true);
            UpdateStorySlide();
        }

        private void UpdateStorySlide()
        {
            bool isTR = currentLanguage == GameLanguage.Turkish;

            if (currentStorySlide == 0)
            {
                storyTitleText.text = isTR ? "ANLATICI" : "NARRATOR";
                storyContentText.text = isTR 
                    ? "Gökler kızıla büründü ve Pulsevania toprakları bir gecede cehenneme döndü...\n\nKadim zindanların derinliklerinden yükselen Kızıl Ejderha (Red Dragon) krallığı yakıp yıktı ve sarayın biricik prensesi Seraphina'yı kaçırarak 50. odanın derinliklerine hapsetti. Krallığın son umudu, efsanevi şövalye Leonardo'nun omuzlarındaydı."
                    : "The skies turned crimson and the lands of Pulsevania turned into hell overnight...\n\nRising from the depths of ancient dungeons, the Red Dragon laid waste to the kingdom and kidnapped the palace's beloved Princess Seraphina, locking her in the depths of Room 50. The kingdom's last hope lay on the shoulders of the legendary knight, Leonardo.";
                
                if (storyIllustrationImg != null)
                {
                    storyIllustrationImg.sprite = LoadStorySprite("kingdom_destroyed.png", false);
                    storyIllustrationImg.color = Color.white;
                }
            }
            else if (currentStorySlide == 1)
            {
                storyTitleText.text = isTR ? "KRAL AURELIUS" : "KING AURELIUS";
                storyContentText.text = isTR
                    ? "\"Leonardo... Krallığım alevler içinde, halkım çaresiz. Ama en acısı, gözümün nuru, biricik kızım o iblisin elinde esir düştü.\n\nBenim yaşlı bedenim artık kılıç tutamıyor, dizlerim titriyor. Bu ihtiyar babanın feryadını duy şanlı savaşçı... Kızımı ejderhanın elinden kurtar!\""
                    : "\"Leonardo... My kingdom is in flames, my people are helpless. But most painful of all, the light of my eyes, my only daughter has fallen captive to that demon.\n\nMy old body can no longer hold a sword, my knees tremble. Hear this old father's cry, glorious warrior... Rescue my daughter from the dragon's grasp!\"";
                
                if (storyIllustrationImg != null)
                {
                    storyIllustrationImg.sprite = LoadStorySprite("king.png", true);
                    storyIllustrationImg.color = Color.white;
                }
            }
            else if (currentStorySlide == 2)
            {
                storyTitleText.text = isTR ? "LEONARDO" : "LEONARDO";
                storyContentText.text = isTR
                    ? "\"Kralım Aurelius, başınızı dik tutun! Pulsevania'nın şanını ve atalarımın cesaretini göğsümde taşıyorum. Adım Leonardo, bu kılıç krallığımızın özgürlüğü için sallanacak.\n\nKızıl Ejderha krallığımıza saldırmakla kendi sonunu hazırladı. Prenses Seraphina'yı o karanlık kafesten kurtaracağıma ve canavarın kafasını size getireceğime yemin ederim!\""
                    : "\"My King Aurelius, hold your head high! I carry the glory of Pulsevania and the courage of my ancestors in my chest. My name is Leonardo, this sword will strike for our kingdom's freedom.\n\nThe Red Dragon prepared its own end by attacking our kingdom. I swear to rescue Princess Seraphina from that dark cage and bring you the monster's head!\"";
                
                if (storyIllustrationImg != null)
                {
                    storyIllustrationImg.sprite = LoadStorySprite("leonardo.png", true);
                    storyIllustrationImg.color = Color.white;
                }
            }
            else if (currentStorySlide == 3)
            {
                storyTitleText.text = isTR ? "KRAL AURELIUS" : "KING AURELIUS";
                storyContentText.text = isTR
                    ? "\"Tanrılar seninle olsun evladım. Zindanın derinliklerine inen tam 50 oda var. Her oda bir öncekinden daha karanlık ve tehlikeli iblislerle dolu.\n\nSeviyeni yükseltmeli, zindanlardaki sandıklardan yeni ganimetler bulup güçlenmeli ve tüccardan aldığın kutsal iksirler ile hayatta kalmalısın. Yolun uzun ve amansız şövalye Leonardo...\""
                    : "\"May the gods be with you, my son. There are exactly 50 rooms descending to the depths of the dungeon. Each room is darker and filled with more dangerous demons than the last.\n\nYou must level up, find new loot in chests to grow stronger, and survive with the holy potions bought from the merchant. Your journey is long and relentless, knight Leonardo...\"";
                
                if (storyIllustrationImg != null)
                {
                    storyIllustrationImg.sprite = LoadStorySprite("king.png", true);
                    storyIllustrationImg.color = Color.white;
                }
            }
            else if (currentStorySlide == 4)
            {
                storyTitleText.text = isTR ? "LEONARDO" : "LEONARDO";
                storyContentText.text = isTR
                    ? "\"Ne kadar karanlık olursa olsun kralım, kılıcımın ışığı o ejderhanın kalbine saplanacak. Kalkanım çelikten, yüreğim inançtan.\n\n50. odaya ulaşıp prensesimizi o demir parmaklıkların ardından çekip alacağım. Maceraya hazırım, krallığımız kurtulacak!\""
                    : "\"No matter how dark it is, my King, the light of my sword will pierce that dragon's heart. My shield is steel, my heart is faith.\n\nI will reach Room 50 and pull our princess from behind those iron bars. I am ready for the adventure, our kingdom will be saved!\"";
                
                if (storyIllustrationImg != null)
                {
                    storyIllustrationImg.sprite = LoadStorySprite("leonardo.png", true);
                    storyIllustrationImg.color = Color.white;
                }
            }
        }

        private static Dictionary<string, Sprite> cachedStorySprites = new Dictionary<string, Sprite>();

        private Sprite LoadStorySprite(string filename, bool chromaKey)
        {
            string cacheKey = filename + "_" + chromaKey;
            if (cachedStorySprites.ContainsKey(cacheKey))
            {
                return cachedStorySprites[cacheKey];
            }

            string path = System.IO.Path.Combine(Application.dataPath, "Sprites/" + filename);
            if (!System.IO.File.Exists(path)) return null;

            byte[] data = System.IO.File.ReadAllBytes(path);
            Texture2D tex = new Texture2D(2, 2);
            tex.LoadImage(data);
            tex.filterMode = FilterMode.Point;

            if (chromaKey)
            {
                Color[] pixels = tex.GetPixels();
                for (int i = 0; i < pixels.Length; i++)
                {
                    // Chroma key white backdrop to transparent
                    if (pixels[i].r > 0.92f && pixels[i].g > 0.92f && pixels[i].b > 0.92f)
                    {
                        pixels[i] = Color.clear;
                    }
                }
                tex.SetPixels(pixels);
                tex.Apply();
            }

            Sprite sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            cachedStorySprites[cacheKey] = sp;
            return sp;
        }

        private void OnNextStorySlide()
        {
            currentStorySlide++;
            if (currentStorySlide < 5)
            {
                UpdateStorySlide();
            }
            else
            {
                // Finished story, start game!
                storyPanelGo.SetActive(false);
                ShowLoadingScreen();
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.NewGame();
                }
            }
        }

        private void OnSkipStory()
        {
            if (storyPanelGo != null) storyPanelGo.SetActive(false);
            ShowLoadingScreen();
            if (GameManager.Instance != null)
            {
                GameManager.Instance.NewGame();
            }
        }

        private GameObject victoryPanelGo;

        public void TriggerVictorySequence()
        {
            // Lock control
            if (activePlayer != null) activePlayer.SetControlsLocked(true);

            GameObject canvas = GameObject.Find("Canvas");
            if (canvas == null)
            {
                Canvas mainCanvas = GameObject.FindObjectOfType<Canvas>();
                if (mainCanvas != null) canvas = mainCanvas.gameObject;
            }

            if (canvas == null) return;

            // Find or create victory panel under Canvas
            Transform existing = canvas.transform.Find("VictoryPanel");
            if (existing != null)
            {
                victoryPanelGo = existing.gameObject;
            }
            else
            {
                victoryPanelGo = new GameObject("VictoryPanel");
                victoryPanelGo.transform.SetParent(canvas.transform, false);
                
                RectTransform rt = victoryPanelGo.AddComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.sizeDelta = Vector2.zero;

                Image bg = victoryPanelGo.AddComponent<Image>();
                bg.color = new Color(0.06f, 0.05f, 0.08f, 0.98f); // Royal dark backdrop

                // Container
                GameObject container = new GameObject("Container");
                container.transform.SetParent(victoryPanelGo.transform, false);
                RectTransform rtCont = container.AddComponent<RectTransform>();
                rtCont.anchorMin = new Vector2(0.5f, 0.5f);
                rtCont.anchorMax = new Vector2(0.5f, 0.5f);
                rtCont.pivot = new Vector2(0.5f, 0.5f);
                rtCont.sizeDelta = new Vector2(1000f, 750f);

                Image contImg = container.AddComponent<Image>();
                contImg.color = new Color(0.12f, 0.08f, 0.06f, 0.98f); // Royal brown panel

                // Title
                GameObject titleGo = new GameObject("Title");
                titleGo.transform.SetParent(container.transform, false);
                RectTransform rtTitle = titleGo.AddComponent<RectTransform>();
                rtTitle.anchorMin = new Vector2(0.5f, 1f);
                rtTitle.anchorMax = new Vector2(0.5f, 1f);
                rtTitle.pivot = new Vector2(0.5f, 1f);
                rtTitle.anchoredPosition = new Vector2(0f, -40f);
                rtTitle.sizeDelta = new Vector2(900f, 80f);

                Text titleTxt = titleGo.AddComponent<Text>();
                titleTxt.text = "TEBRIKLER! PRENSES KURTARILDI";
                titleTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                titleTxt.fontSize = 44;
                titleTxt.fontStyle = FontStyle.Bold;
                titleTxt.alignment = TextAnchor.MiddleCenter;
                titleTxt.color = Color.yellow;

                // Princess Image
                GameObject princessGo = new GameObject("PrincessImg");
                princessGo.transform.SetParent(container.transform, false);
                RectTransform rtPr = princessGo.AddComponent<RectTransform>();
                rtPr.anchorMin = new Vector2(0.5f, 0.5f);
                rtPr.anchorMax = new Vector2(0.5f, 0.5f);
                rtPr.pivot = new Vector2(0.5f, 0.5f);
                rtPr.anchoredPosition = new Vector2(0f, 140f);
                rtPr.sizeDelta = new Vector2(200f, 200f);

                Image prImg = princessGo.AddComponent<Image>();
                
                // Let's load the princess sprite
                if (MapManager.Instance != null)
                {
                    prImg.sprite = MapManager.Instance.LoadPrincessSprite();
                }

                // Victory story text
                GameObject textGo = new GameObject("Text");
                textGo.transform.SetParent(container.transform, false);
                RectTransform rtText = textGo.AddComponent<RectTransform>();
                rtText.anchorMin = new Vector2(0.5f, 0.5f);
                rtText.anchorMax = new Vector2(0.5f, 0.5f);
                rtText.pivot = new Vector2(0.5f, 0.5f);
                rtText.anchoredPosition = new Vector2(0f, -100f);
                rtText.sizeDelta = new Vector2(900f, 220f);

                Text storyTxt = textGo.AddComponent<Text>();
                storyTxt.text = "Karanlik zindanin efendisi Kizil Ejderha yenildi! Prensesi demir kafesten kurtardin ve kralin yanina geri goturdun.\n\nKral yasli gozleriyle sana minnettar... Pulsevania kralligina baris ve zafer senin sayende geri dondu! Sen gercek bir kahramansin!";
                storyTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                storyTxt.fontSize = 26;
                storyTxt.fontStyle = FontStyle.Normal;
                storyTxt.alignment = TextAnchor.MiddleCenter;
                storyTxt.color = Color.white;

                // Main Menu Button
                GameObject menuBtnGo = new GameObject("MenuBtn");
                menuBtnGo.transform.SetParent(container.transform, false);
                RectTransform rtMenuBtn = menuBtnGo.AddComponent<RectTransform>();
                rtMenuBtn.anchorMin = new Vector2(0.5f, 0f);
                rtMenuBtn.anchorMax = new Vector2(0.5f, 0f);
                rtMenuBtn.pivot = new Vector2(0.5f, 0f);
                rtMenuBtn.anchoredPosition = new Vector2(0f, 40f);
                rtMenuBtn.sizeDelta = new Vector2(400f, 70f);

                Image btnImg = menuBtnGo.AddComponent<Image>();
                btnImg.color = new Color(0.25f, 0.18f, 0.12f, 1f);

                Button btn = menuBtnGo.AddComponent<Button>();
                btn.onClick.AddListener(OnVictoryMenuClick);

                GameObject btnTxtGo = new GameObject("Text");
                btnTxtGo.transform.SetParent(menuBtnGo.transform, false);
                RectTransform rtBtnTxt = btnTxtGo.AddComponent<RectTransform>();
                rtBtnTxt.anchorMin = Vector2.zero;
                rtBtnTxt.anchorMax = Vector2.one;
                rtBtnTxt.sizeDelta = Vector2.zero;

                Text btnTxt = btnTxtGo.AddComponent<Text>();
                btnTxt.text = "ANA MENUYE DON";
                btnTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                btnTxt.fontSize = 24;
                btnTxt.fontStyle = FontStyle.Bold;
                btnTxt.alignment = TextAnchor.MiddleCenter;
                btnTxt.color = Color.white;
            }

            victoryPanelGo.SetActive(true);
        }

        private void OnVictoryMenuClick()
        {
            if (victoryPanelGo != null)
            {
                Destroy(victoryPanelGo);
                victoryPanelGo = null;
            }
            if (activePlayer != null) activePlayer.SetControlsLocked(false);

            MainMenuButtonAction();
        }

        public void ShowLoadingScreen()
        {
            if (loadingPanelGo != null) return;

            // Create a completely new Canvas specifically for loading screen to make it DontDestroyOnLoad
            loadingPanelGo = new GameObject("LoadingScreenCanvas");
            DontDestroyOnLoad(loadingPanelGo);

            Canvas canvas = loadingPanelGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999; // Make sure it's on top of everything!

            UnityEngine.UI.CanvasScaler scaler = loadingPanelGo.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            loadingPanelGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // Dark premium background panel
            GameObject bgGo = new GameObject("Background");
            bgGo.transform.SetParent(loadingPanelGo.transform, false);
            RectTransform rtBg = bgGo.AddComponent<RectTransform>();
            rtBg.anchorMin = Vector2.zero;
            rtBg.anchorMax = Vector2.one;
            rtBg.sizeDelta = Vector2.zero;

            Image bgImg = bgGo.AddComponent<Image>();
            bgImg.color = new Color(0.04f, 0.04f, 0.05f, 1f); // Deep premium black-purple zemin

            // Text
            GameObject textGo = new GameObject("LoadingText");
            textGo.transform.SetParent(loadingPanelGo.transform, false);
            RectTransform rtText = textGo.AddComponent<RectTransform>();
            rtText.anchorMin = new Vector2(0.5f, 0.5f);
            rtText.anchorMax = new Vector2(0.5f, 0.5f);
            rtText.pivot = new Vector2(0.5f, 0.5f);
            rtText.anchoredPosition = Vector2.zero;
            rtText.sizeDelta = new Vector2(600f, 100f);

            Text txt = textGo.AddComponent<Text>();
            txt.text = currentLanguage == GameLanguage.Turkish ? "ZİNDAN YÜKLENİYOR..." : "DUNGEON LOADING...";
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 40;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;

            // Simple pulse animation for text to make it premium
            StartCoroutine(PulseLoadingText(txt));
        }

        private IEnumerator PulseLoadingText(Text txt)
        {
            float elapsed = 0f;
            while (txt != null)
            {
                elapsed += Time.deltaTime;
                float alpha = 0.5f + Mathf.PingPong(elapsed * 1.5f, 0.5f); // Pulse between 0.5 and 1.0 alpha
                txt.color = new Color(1f, 1f, 1f, alpha);
                yield return null;
            }
        }

        public void HideLoadingScreen()
        {
            if (loadingPanelGo != null)
            {
                Destroy(loadingPanelGo);
                loadingPanelGo = null;
            }
        }

        private Coroutine activeShopWarningCoroutine;
        private Text shopWarningText;

        public void ShowShopWarning(string message)
        {
            GameObject canvas = GameObject.Find("Canvas");
            if (canvas == null)
            {
                Canvas mainCanvas = GameObject.FindObjectOfType<Canvas>();
                if (mainCanvas != null) canvas = mainCanvas.gameObject;
            }
            if (canvas == null) return;

            Transform shopPanel = canvas.transform.Find("MerchantShopPanel");
            if (shopPanel == null || !shopPanel.gameObject.activeSelf) return;

            Transform window = shopPanel.Find("ShopWindow");
            if (window == null) return;

            // Find or create warning text object
            Transform existing = window.Find("ShopWarningText");
            GameObject warningGo;
            if (existing != null)
            {
                warningGo = existing.gameObject;
                shopWarningText = warningGo.GetComponent<Text>();
            }
            else
            {
                warningGo = new GameObject("ShopWarningText");
                warningGo.transform.SetParent(window, false);

                RectTransform rt = warningGo.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0f);
                rt.anchorMax = new Vector2(0.5f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.anchoredPosition = new Vector2(0f, 30f); // Placed at the bottom inside window
                rt.sizeDelta = new Vector2(700f, 50f);

                shopWarningText = warningGo.AddComponent<Text>();
                shopWarningText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                shopWarningText.fontSize = 22;
                shopWarningText.fontStyle = FontStyle.Bold;
                shopWarningText.alignment = TextAnchor.MiddleCenter;
                shopWarningText.color = Color.red;
                shopWarningText.raycastTarget = false;
            }

            shopWarningText.text = message;
            warningGo.SetActive(true);

            if (activeShopWarningCoroutine != null)
            {
                StopCoroutine(activeShopWarningCoroutine);
            }
            activeShopWarningCoroutine = StartCoroutine(FadeOutShopWarning(warningGo));
        }

        private IEnumerator FadeOutShopWarning(GameObject warningGo)
        {
            float duration = 0.5f; // fade duration
            float elapsed = 0f;
            if (warningGo == null) yield break;
            Text txt = warningGo.GetComponent<Text>();
            if (txt == null) yield break;

            // Flash text a few times to get attention
            for (int i = 0; i < 3; i++)
            {
                if (txt == null) yield break;
                txt.color = Color.clear;
                yield return new WaitForSeconds(0.08f);
                if (txt == null) yield break;
                txt.color = Color.red;
                yield return new WaitForSeconds(0.08f);
            }

            yield return new WaitForSeconds(1.8f);

            // Fade out
            while (elapsed < duration)
            {
                if (txt == null) yield break;
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(1f, 0f, elapsed / duration);
                if (txt != null) txt.color = new Color(1f, 0f, 0f, alpha);
                yield return null;
            }

            if (warningGo != null) warningGo.SetActive(false);
            activeShopWarningCoroutine = null;
        }

        private GameObject rescueDialoguePanelGo;
        private Text dialogueSpeakerText;
        private Text dialogueContentText;
        private int currentDialogueIndex = 0;
        private bool isInRescueDialogue = false;

        private struct DialogueLine
        {
            public string speaker;
            public string content;
            public DialogueLine(string s, string c) { speaker = s; content = c; }
        }

        private System.Collections.Generic.List<DialogueLine> rescueDialogueLines = new System.Collections.Generic.List<DialogueLine>()
        {
            new DialogueLine("Prenses Seraphina", "Sen... beni kurtarmak için gönderilen şanlı şövalye misin? Ejderhanın kükremelerini her duyduğumda umudumu kaybediyordum. Sonunda birinin geleceğini biliyordum!"),
            new DialogueLine("Leonardo", "Evet prensesim. Kralımız Aurelius'un emriyle, sizin için bu zindanın tüm karanlığını aştım. Benim adım Leonardo. Ejderha artık Pulsevania için bir tehdit değil."),
            new DialogueLine("Prenses Seraphina", "Leonardo... Babamın seni seçmesi ne büyük bir lütuf. Krallığımızın gerçek kahramanı sensin. Şimdi evimize dönme ve Pulsevania'nın küllerinden yeniden doğuşunu kutlama zamanı!"),
            new DialogueLine("Leonardo", "Evet, prensesim. Pulsevania halkı bizi bekler. Zindandan çıkış yolunu açıyorum, gidelim!")
        };

        public void TriggerRescueDialogue()
        {
            if (isInRescueDialogue) return;
            isInRescueDialogue = true;
            currentDialogueIndex = 0;

            // Lock controls
            if (activePlayer != null) activePlayer.SetControlsLocked(true);

            // Create Dialogue UI
            CreateRescueDialogueUI();
            ShowNextRescueDialogueLine();
        }

        private void CreateRescueDialogueUI()
        {
            if (rescueDialoguePanelGo != null) return;

            GameObject canvas = GameObject.Find("Canvas");
            if (canvas == null)
            {
                Canvas mainCanvas = GameObject.FindObjectOfType<Canvas>();
                if (mainCanvas != null) canvas = mainCanvas.gameObject;
            }
            if (canvas == null) return;

            rescueDialoguePanelGo = new GameObject("RescueDialoguePanel");
            rescueDialoguePanelGo.transform.SetParent(canvas.transform, false);

            RectTransform rtPanel = rescueDialoguePanelGo.AddComponent<RectTransform>();
            rtPanel.anchorMin = new Vector2(0.5f, 0f);
            rtPanel.anchorMax = new Vector2(0.5f, 0f);
            rtPanel.pivot = new Vector2(0.5f, 0f);
            rtPanel.anchoredPosition = new Vector2(0f, 60f); // Bottom of screen
            rtPanel.sizeDelta = new Vector2(1000f, 260f);

            // Background card with premium glassmorphism dark look
            Image bgImg = rescueDialoguePanelGo.AddComponent<Image>();
            bgImg.color = new Color(0.06f, 0.05f, 0.08f, 0.95f);

            // Speaker Text
            GameObject speakerGo = new GameObject("SpeakerText");
            speakerGo.transform.SetParent(rescueDialoguePanelGo.transform, false);
            RectTransform rtSpeaker = speakerGo.AddComponent<RectTransform>();
            rtSpeaker.anchorMin = new Vector2(0f, 1f);
            rtSpeaker.anchorMax = new Vector2(0f, 1f);
            rtSpeaker.pivot = new Vector2(0f, 1f);
            rtSpeaker.anchoredPosition = new Vector2(30f, -25f);
            rtSpeaker.sizeDelta = new Vector2(500f, 50f);

            dialogueSpeakerText = speakerGo.AddComponent<Text>();
            dialogueSpeakerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            dialogueSpeakerText.fontSize = 32;
            dialogueSpeakerText.fontStyle = FontStyle.Bold;
            dialogueSpeakerText.color = Color.yellow; // Golden yellow for speaker name

            // Content Text
            GameObject contentGo = new GameObject("ContentText");
            contentGo.transform.SetParent(rescueDialoguePanelGo.transform, false);
            RectTransform rtContent = contentGo.AddComponent<RectTransform>();
            rtContent.anchorMin = Vector2.zero;
            rtContent.anchorMax = Vector2.one;
            rtContent.offsetMin = new Vector2(30f, 25f);
            rtContent.offsetMax = new Vector2(-30f, -75f);

            dialogueContentText = contentGo.AddComponent<Text>();
            dialogueContentText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            dialogueContentText.fontSize = 26;
            dialogueContentText.fontStyle = FontStyle.Normal;
            dialogueContentText.color = Color.white;
            dialogueContentText.alignment = TextAnchor.UpperLeft;

            // Continue Hint
            GameObject hintGo = new GameObject("HintText");
            hintGo.transform.SetParent(rescueDialoguePanelGo.transform, false);
            RectTransform rtHint = hintGo.AddComponent<RectTransform>();
            rtHint.anchorMin = new Vector2(1f, 0f);
            rtHint.anchorMax = new Vector2(1f, 0f);
            rtHint.pivot = new Vector2(1f, 0f);
            rtHint.anchoredPosition = new Vector2(-30f, 15f);
            rtHint.sizeDelta = new Vector2(350f, 30f);

            Text hintTxt = hintGo.AddComponent<Text>();
            hintTxt.text = "Devam etmek için [Tıklayın] veya [E] tuşuna basın";
            hintTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            hintTxt.fontSize = 16;
            hintTxt.fontStyle = FontStyle.Italic;
            hintTxt.color = new Color(1f, 1f, 1f, 0.6f);
            hintTxt.alignment = TextAnchor.MiddleRight;

            // Panel Button trigger to click next
            Button btn = rescueDialoguePanelGo.AddComponent<Button>();
            btn.onClick.AddListener(ShowNextRescueDialogueLine);
        }

        private void ShowNextRescueDialogueLine()
        {
            if (currentDialogueIndex < rescueDialogueLines.Count)
            {
                var line = rescueDialogueLines[currentDialogueIndex];
                dialogueSpeakerText.text = line.speaker;
                dialogueContentText.text = line.content;
                currentDialogueIndex++;
            }
            else
            {
                // Dialogue finished! Close panel and show victory
                if (rescueDialoguePanelGo != null)
                {
                    Destroy(rescueDialoguePanelGo);
                    rescueDialoguePanelGo = null;
                }
                isInRescueDialogue = false;

                TriggerVictorySequence();
            }
        }

        private GameObject princessNotePopupGo;
        private System.Action onPrincessNoteCloseCallback;

        public void ShowPrincessNotePopup(System.Action onClose)
        {
            onPrincessNoteCloseCallback = onClose;

            if (activePlayer != null) activePlayer.SetControlsLocked(true);

            GameObject canvas = GameObject.Find("Canvas");
            if (canvas == null)
            {
                Canvas mainCanvas = GameObject.FindObjectOfType<Canvas>();
                if (mainCanvas != null) canvas = mainCanvas.gameObject;
            }
            if (canvas == null) return;

            princessNotePopupGo = new GameObject("PrincessNotePopup");
            princessNotePopupGo.transform.SetParent(canvas.transform, false);

            RectTransform rtPopup = princessNotePopupGo.AddComponent<RectTransform>();
            rtPopup.anchorMin = new Vector2(0.5f, 0.5f);
            rtPopup.anchorMax = new Vector2(0.5f, 0.5f);
            rtPopup.pivot = new Vector2(0.5f, 0.5f);
            rtPopup.anchoredPosition = Vector2.zero;
            rtPopup.sizeDelta = new Vector2(950f, 650f);

            // Black tint screen blocker behind popup
            GameObject blockerGo = new GameObject("Blocker");
            blockerGo.transform.SetParent(princessNotePopupGo.transform, false);
            RectTransform rtBlocker = blockerGo.AddComponent<RectTransform>();
            rtBlocker.anchorMin = Vector2.zero;
            rtBlocker.anchorMax = Vector2.one;
            rtBlocker.sizeDelta = Vector2.zero;
            Image blockerImg = blockerGo.AddComponent<Image>();
            blockerImg.color = new Color(0f, 0f, 0f, 0.6f);

            // Back to foreground note card
            GameObject cardGo = new GameObject("Card");
            cardGo.transform.SetParent(princessNotePopupGo.transform, false);
            RectTransform rtCard = cardGo.AddComponent<RectTransform>();
            rtCard.anchorMin = new Vector2(0.5f, 0.5f);
            rtCard.anchorMax = new Vector2(0.5f, 0.5f);
            rtCard.pivot = new Vector2(0.5f, 0.5f);
            rtCard.anchoredPosition = Vector2.zero;
            rtCard.sizeDelta = new Vector2(820f, 540f);

            Image cardImg = cardGo.AddComponent<Image>();
            cardImg.color = new Color(0.92f, 0.85f, 0.7f, 1f); // Parchment paper color

            // Border/Outline
            Outline outline = cardGo.AddComponent<Outline>();
            outline.effectColor = new Color(0.3f, 0.2f, 0.1f, 1f);
            outline.effectDistance = new Vector2(6f, 6f);

            // Title
            GameObject titleGo = new GameObject("Title");
            titleGo.transform.SetParent(cardGo.transform, false);
            RectTransform rtTitle = titleGo.AddComponent<RectTransform>();
            rtTitle.anchorMin = new Vector2(0.5f, 1f);
            rtTitle.anchorMax = new Vector2(0.5f, 1f);
            rtTitle.pivot = new Vector2(0.5f, 1f);
            rtTitle.anchoredPosition = new Vector2(0f, -40f);
            rtTitle.sizeDelta = new Vector2(500f, 50f);

            bool isTR = PlayerPrefs.GetString("GameLanguage", "Turkish") == "Turkish";

            Text titleTxt = titleGo.AddComponent<Text>();
            titleTxt.text = isTR ? "GİZEMLİ BİR NOT" : "A MYSTERIOUS NOTE";
            titleTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleTxt.fontSize = 28;
            titleTxt.fontStyle = FontStyle.Bold;
            titleTxt.color = new Color(0.4f, 0.2f, 0.05f);
            titleTxt.alignment = TextAnchor.MiddleCenter;

            // Content
            GameObject contentGo = new GameObject("Content");
            contentGo.transform.SetParent(cardGo.transform, false);
            RectTransform rtContent = contentGo.AddComponent<RectTransform>();
            rtContent.anchorMin = Vector2.zero;
            rtContent.anchorMax = Vector2.one;
            rtContent.offsetMin = new Vector2(60f, 100f);
            rtContent.offsetMax = new Vector2(-60f, -80f);

            int currentRoom = MapManager.Instance != null ? MapManager.Instance.GetCurrentRoomId() : 10;
            string noteText = "";
            int noteFontSize = 24;
            FontStyle noteFontStyle = FontStyle.Italic;
            if (currentRoom == 30)
            {
                if (isTR)
                {
                    noteText = "Artık... artık dayanacak gücüm kalmadı. Günlerdir bu karanlık kafeste, aç ve susuz bir şekilde taşınıyorum. Boğazım kurudu, gözlerim kararıyor ve bedenim her geçen saat daha da bitkin düşüyor. Ejderhanın korkunç kükremeleri zindanın taş duvarlarında yankılandıkça içim ürperiyor.\n\nAma beni kurtarmak için yola çıkan o cesur yüreğin geleceğine inanıyorum. Lütfen acele edin... Zamanım tükeniyor.\n\n- Prenses Seraphina";
                }
                else
                {
                    noteText = "I have... no strength left to endure. For days, I have been moved in this dark cage, hungry and thirsty. My throat is parched, my eyes are growing dim, and my body feels weaker with every passing hour. I shiver as the terrifying roars of the dragon echo through the stone walls of the dungeon.\n\nBut I believe that the brave heart who set out to rescue me will come. Please hurry... My time is running out.\n\n- Princess Seraphina";
                }
                noteFontSize = 26; // Increased from 20 to 26 for legibility
                noteFontStyle = FontStyle.Normal; // Normal is much more legible than Italic
            }
            else
            {
                // Default Room 10
                if (isTR)
                {
                    noteText = "Beni burada zorla tutuyorlar. İlk defa gördüğüm canavarlar bunlar, şu an nereye gidiyoruz bilmiyorum... Eğer bu notu bulduysanız ben iyiyim ve korkmuyorum kurtarıcımın geleceğini biliyorum, acele et... Yollar çok karanlık ve tehlikeli.\n\n- Prenses Seraphina";
                }
                else
                {
                    noteText = "They are keeping me here by force. These are monsters I have never seen before, I do not know where we are going... If you found this note, I am fine and I am not afraid, I know my savior will come, hurry... The paths are very dark and dangerous.\n\n- Princess Seraphina";
                }
                noteFontSize = 28; // Increased from 24 to 28
                noteFontStyle = FontStyle.Normal;
            }

            Text contentTxt = contentGo.AddComponent<Text>();
            contentTxt.text = noteText;
            contentTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            contentTxt.fontSize = noteFontSize;
            contentTxt.fontStyle = noteFontStyle;
            contentTxt.color = new Color(0.12f, 0.07f, 0.02f); // Darker color for higher contrast and better legibility
            contentTxt.alignment = TextAnchor.MiddleLeft;

            // Close Button
            GameObject closeBtnGo = new GameObject("CloseBtn");
            closeBtnGo.transform.SetParent(cardGo.transform, false);
            RectTransform rtClose = closeBtnGo.AddComponent<RectTransform>();
            rtClose.anchorMin = new Vector2(0.5f, 0f);
            rtClose.anchorMax = new Vector2(0.5f, 0f);
            rtClose.pivot = new Vector2(0.5f, 0f);
            rtClose.anchoredPosition = new Vector2(0f, 30f);
            rtClose.sizeDelta = new Vector2(160f, 45f);

            Image btnImg = closeBtnGo.AddComponent<Image>();
            btnImg.color = new Color(0.4f, 0.2f, 0.05f);
            
            Button btn = closeBtnGo.AddComponent<Button>();
            btn.onClick.AddListener(ClosePrincessNotePopup);

            GameObject closeTxtGo = new GameObject("Text");
            closeTxtGo.transform.SetParent(closeBtnGo.transform, false);
            RectTransform rtCloseTxt = closeTxtGo.AddComponent<RectTransform>();
            rtCloseTxt.anchorMin = Vector2.zero;
            rtCloseTxt.anchorMax = Vector2.one;
            rtCloseTxt.sizeDelta = Vector2.zero;

            Text closeTxt = closeTxtGo.AddComponent<Text>();
            closeTxt.text = isTR ? "KAPAT" : "CLOSE";
            closeTxt.raycastTarget = false;
            closeTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            closeTxt.fontSize = 18;
            closeTxt.fontStyle = FontStyle.Bold;
            closeTxt.color = Color.white;
            closeTxt.alignment = TextAnchor.MiddleCenter;
        }

        public void ClosePrincessNotePopup()
        {
            if (princessNotePopupGo != null)
            {
                Destroy(princessNotePopupGo);
                princessNotePopupGo = null;
            }

            if (activePlayer != null) activePlayer.SetControlsLocked(false);

            if (onPrincessNoteCloseCallback != null)
            {
                onPrincessNoteCloseCallback.Invoke();
                onPrincessNoteCloseCallback = null;
            }
        }

        // --- LOCALIZATION SYSTEM SYSTEM ---
        public enum GameLanguage
        {
            Turkish,
            English
        }

        public GameLanguage currentLanguage = GameLanguage.Turkish;

        public void SetLanguage(GameLanguage lang)
        {
            currentLanguage = lang;
            PlayerPrefs.SetString("GameLanguage", lang.ToString());
            PlayerPrefs.Save();

            // Refresh UI dynamically
            UpdateLocalizedTexts();
        }

        private void UpdateLanguageButtonsUI()
        {
            if (settingsPanelGo == null) return;
            Transform trBtn = settingsPanelGo.transform.Find("TR_Button");
            Transform enBtn = settingsPanelGo.transform.Find("EN_Button");

            if (trBtn != null)
            {
                Image img = trBtn.GetComponent<Image>();
                Text txt = trBtn.GetComponentInChildren<Text>();
                if (currentLanguage == GameLanguage.Turkish)
                {
                    if (img != null) img.color = new Color(0.9f, 0.45f, 0f, 1f); // Active orange/gold
                    if (txt != null) txt.color = Color.white;
                }
                else
                {
                    if (img != null) img.color = new Color(0.18f, 0.18f, 0.22f, 0.8f); // Inactive dark gray
                    if (txt != null) txt.color = new Color(1f, 1f, 1f, 0.5f);
                }
            }

            if (enBtn != null)
            {
                Image img = enBtn.GetComponent<Image>();
                Text txt = enBtn.GetComponentInChildren<Text>();
                if (currentLanguage == GameLanguage.English)
                {
                    if (img != null) img.color = new Color(0.9f, 0.45f, 0f, 1f);
                    if (txt != null) txt.color = Color.white;
                }
                else
                {
                    if (img != null) img.color = new Color(0.18f, 0.18f, 0.22f, 0.8f);
                    if (txt != null) txt.color = new Color(1f, 1f, 1f, 0.5f);
                }
            }
        }

        public void UpdateLocalizedTexts()
        {
            // 1. Settings Panel
            if (settingsPanelGo != null)
            {
                Transform title = settingsPanelGo.transform.Find("Title");
                if (title != null) title.GetComponent<Text>().text = currentLanguage == GameLanguage.Turkish ? "AYARLAR" : "SETTINGS";

                Transform master = settingsPanelGo.transform.Find("MasterLabel");
                if (master != null) master.GetComponent<Text>().text = currentLanguage == GameLanguage.Turkish ? "SES SEVİYESİ" : "MASTER VOLUME";

                Transform music = settingsPanelGo.transform.Find("MusicLabel");
                if (music != null) music.GetComponent<Text>().text = currentLanguage == GameLanguage.Turkish ? "MÜZİK SEVİYESİ" : "MUSIC VOLUME";

                Transform langLbl = settingsPanelGo.transform.Find("LanguageLabel");
                if (langLbl != null) langLbl.GetComponent<Text>().text = currentLanguage == GameLanguage.Turkish ? "DİL SEÇENEĞİ" : "LANGUAGE";

                Transform backBtn = settingsPanelGo.transform.Find("BackButton");
                if (backBtn != null) backBtn.GetComponentInChildren<Text>().text = currentLanguage == GameLanguage.Turkish ? "GERİ" : "BACK";

                UpdateLanguageButtonsUI();
            }

            // 2. Main Menu Panel (Programmatic Buttons)
            if (mainMenuPanel != null)
            {
                bool isTR = currentLanguage == GameLanguage.Turkish;

                Transform newGame = mainMenuPanel.transform.Find("NewGameBtn");
                if (newGame != null) newGame.GetComponentInChildren<Text>().text = isTR ? "YENİ OYUN" : "NEW GAME";

                Transform loadGame = mainMenuPanel.transform.Find("LoadGameBtn");
                if (loadGame != null) loadGame.GetComponentInChildren<Text>().text = isTR ? "KAYIT YÜKLE" : "LOAD GAME";

                Transform settings = mainMenuPanel.transform.Find("SettingsBtn");
                if (settings != null) settings.GetComponentInChildren<Text>().text = isTR ? "AYARLAR" : "SETTINGS";

                Transform quit = mainMenuPanel.transform.Find("QuitBtn");
                if (quit != null) quit.GetComponentInChildren<Text>().text = isTR ? "ÇIKIŞ" : "QUIT";
            }

            // 3. Pause Panel (Programmatic Buttons)
            if (pausePanel != null)
            {
                bool isTR = currentLanguage == GameLanguage.Turkish;

                Transform resume = pausePanel.transform.Find("ResumeBtn");
                if (resume != null) resume.GetComponentInChildren<Text>().text = isTR ? "DEVAM ET" : "RESUME";

                Transform save = pausePanel.transform.Find("SaveBtn");
                if (save != null) save.GetComponentInChildren<Text>().text = isTR ? "OYUNU KAYDET" : "SAVE GAME";

                Transform load = pausePanel.transform.Find("LoadBtn");
                if (load != null) load.GetComponentInChildren<Text>().text = isTR ? "KAYIT YÜKLE" : "LOAD GAME";

                Transform settings = pausePanel.transform.Find("SettingsBtn");
                if (settings != null) settings.GetComponentInChildren<Text>().text = isTR ? "AYARLAR" : "SETTINGS";

                Transform menu = pausePanel.transform.Find("MainMenuBtn");
                if (menu != null) menu.GetComponentInChildren<Text>().text = isTR ? "ANA MENÜYE DÖN" : "RETURN TO MAIN MENU";

                Transform quit = pausePanel.transform.Find("QuitBtn");
                if (quit != null) quit.GetComponentInChildren<Text>().text = isTR ? "ÇIKIŞ" : "QUIT";
            }

            // 4. Game Over Panel
            if (gameOverPanel != null)
            {
                Transform savepoint = gameOverPanel.transform.Find("SavepointButton");
                if (savepoint != null) savepoint.GetComponentInChildren<Text>().text = currentLanguage == GameLanguage.Turkish ? "EN YAKIN CHECKPOINT" : "NEAREST CHECKPOINT";
                
                Transform restart = gameOverPanel.transform.Find("RestartButton");
                if (restart != null) restart.GetComponentInChildren<Text>().text = currentLanguage == GameLanguage.Turkish ? "YENİDEN BAŞLA (MAP 1)" : "RESTART RUN (MAP 1)";

                Transform quit = gameOverPanel.transform.Find("QuitButton");
                if (quit != null) quit.GetComponentInChildren<Text>().text = currentLanguage == GameLanguage.Turkish ? "ÇIKIŞ" : "QUIT GAME";

                Text titleTxt = gameOverPanel.GetComponentInChildren<Text>();
                if (titleTxt != null && titleTxt.name == "Title")
                {
                    titleTxt.text = currentLanguage == GameLanguage.Turkish ? "ELENDİNİZ" : "GAME OVER";
                }
            }

            // 5. Level Complete Panel
            if (levelCompletePanel != null)
            {
                Transform next = levelCompletePanel.transform.Find("NextLevelButton");
                if (next != null) next.GetComponentInChildren<Text>().text = currentLanguage == GameLanguage.Turkish ? "SONRAKİ ODA" : "NEXT ROOM";

                Transform menu = levelCompletePanel.transform.Find("MainMenuButton");
                if (menu != null) menu.GetComponentInChildren<Text>().text = currentLanguage == GameLanguage.Turkish ? "ANA MENÜ" : "MAIN MENU";
            }

            // 8. Stats Shop Panel
            if (shopPanel != null)
            {
                bool isTR = currentLanguage == GameLanguage.Turkish;
                Transform title = shopPanel.transform.Find("Title");
                if (title != null) title.GetComponent<Text>().text = isTR ? "YÜKSELTME MARKETİ" : "STATS SHOP";

                Transform close = shopPanel.transform.Find("CloseShopButton");
                if (close != null) close.GetComponentInChildren<Text>().text = isTR ? "KAPAT" : "CLOSE";

                UpdateShopUI();
            }

            // 9. Merchant Shop Panel
            if (merchantShopPanelGo != null)
            {
                bool isTR = currentLanguage == GameLanguage.Turkish;
                Transform title = merchantShopPanelGo.transform.Find("ShopContainer/Title");
                if (title != null) title.GetComponent<Text>().text = isTR ? "SATICI MARKETİ" : "MERCHANT SHOP";

                if (btnSellModeText != null)
                {
                    btnSellModeText.text = isSellMode 
                        ? (isTR ? "MARKETE DÖN" : "BACK TO BUY") 
                        : (isTR ? "EŞYA SAT" : "SELL ITEMS");
                }

                if (btnCheckoutText != null)
                {
                    btnCheckoutText.text = isTR ? "SATIN AL" : "CHECKOUT";
                }

                UpdateCartButtonText();
                UpdateCartGoldStatusText();
            }

            // 6. World Map Title & Legend
            if (fullWorldMapPanelGo != null)
            {
                Transform title = fullWorldMapPanelGo.transform.Find("Title");
                if (title != null) title.GetComponent<Text>().text = currentLanguage == GameLanguage.Turkish ? "ZİNDAN HARİTASI" : "DUNGEON MAP";

                Transform legend = fullWorldMapPanelGo.transform.Find("Legend");
                if (legend != null) legend.GetComponent<Text>().text = currentLanguage == GameLanguage.Turkish ? "Turuncu: Aktif Oda | Yeşil: Temizlenmiş/Güvenli Bölge" : "Orange: Active | Green: Cleared/Safe Zone";

                Transform close = fullWorldMapPanelGo.transform.Find("CloseButton");
                if (close != null) close.GetComponentInChildren<Text>().text = currentLanguage == GameLanguage.Turkish ? "KAPAT" : "CLOSE";

                // Update the text in grid cells to match the language
                RefreshFullWorldMapUI();
            }

            // 7. Update Inventory Equip Labels
            UpdateInventorySlotLabels();
            UpdateGameplayHUDButtonLabels();
        }

        private void UpdateGameplayHUDButtonLabels()
        {
            bool isTR = currentLanguage == GameLanguage.Turkish;

            if (btnAttack != null)
            {
                Text txt = btnAttack.GetComponentInChildren<Text>();
                if (txt != null) txt.text = isTR ? "SALDIR" : "ATK";
            }
            if (btnJump != null)
            {
                Text txt = btnJump.GetComponentInChildren<Text>();
                if (txt != null) txt.text = isTR ? "ZIPLA" : "JUMP";
            }
            if (btnKnife != null)
            {
                Text txt = btnKnife.GetComponentInChildren<Text>();
                if (txt != null) txt.text = isTR ? "BIÇAK" : "KNIFE";
            }
            if (btnBlock != null)
            {
                Text txt = btnBlock.GetComponentInChildren<Text>();
                if (txt != null) txt.text = isTR ? "BLOK" : "BLOCK";
            }
            if (btnUsePotion != null)
            {
                Text txt = btnUsePotion.GetComponentInChildren<Text>();
                if (txt != null) txt.text = isTR ? "İKSİR" : "POTION";
            }
            if (btnOpenInventory != null)
            {
                Text txt = btnOpenInventory.GetComponentInChildren<Text>();
                if (txt != null) txt.text = isTR ? "ENV" : "INV";
            }
        }

        private void UpdateInventorySlotLabels()
        {
            if (uiEquipSlots == null || uiEquipSlots.Count == 0) return;

            string[] namesTR = { "Başlık", "Zırh", "Eldiven", "Pantolon", "Çizme", "Silah", "Kalkan", "Bıçak" };
            string[] namesEN = { "Helmet", "Armor", "Gloves", "Pants", "Boots", "Weapon", "Shield", "Knife" };
            string[] chosenNames = currentLanguage == GameLanguage.Turkish ? namesTR : namesEN;

            EquipSlot[] slots = { 
                EquipSlot.Head, 
                EquipSlot.Chest, 
                EquipSlot.Hands, 
                EquipSlot.Legs, 
                EquipSlot.Feet, 
                EquipSlot.Weapon, 
                EquipSlot.Shield, 
                EquipSlot.ThrowingKnife 
            };

            for (int i = 0; i < slots.Length; i++)
            {
                if (uiEquipSlots.ContainsKey(slots[i]) && uiEquipSlots[slots[i]] != null)
                {
                    Transform label = uiEquipSlots[slots[i]].transform.Find("Label");
                    if (label != null)
                    {
                        Text txt = label.GetComponent<Text>();
                        if (txt != null) txt.text = chosenNames[i];
                    }
                }
            }
        }

        public static string GetLocalizedItemName(string name, bool isTurkish)
        {
            if (!isTurkish) return name;
            
            switch (name)
            {
                case "Bronze Helmet": return "Bronz Kask";
                case "Silver Helmet": return "Gümüş Kask";
                case "Gold Helmet": return "Altın Kask";
                case "Bronze Armor": return "Bronz Zırh";
                case "Silver Armor": return "Gümüş Zırh";
                case "Gold Armor": return "Altın Zırh";
                case "Bronze Boots": return "Bronz Çizme";
                case "Silver Boots": return "Gümüş Çizme";
                case "Gold Boots": return "Altın Çizme";
                case "Bronze Gloves": return "Bronz Eldiven";
                case "Silver Gloves": return "Gümüş Eldiven";
                case "Gold Gloves": return "Altın Eldiven";
                case "Bronze Pants": return "Bronz Pantolon";
                case "Silver Pants": return "Gümüş Pantolon";
                case "Gold Pants": return "Altın Pantolon";
                case "Bronze Shield": return "Bronz Kalkan";
                case "Silver Shield": return "Gümüş Kalkan";
                case "Gold Shield": return "Altın Kalkan";
                case "Bronze Sword": return "Bronz Kılıç";
                case "Silver Sword": return "Gümüş Kılıç";
                case "Gold Sword": return "Altın Kılıç";
                case "EKT Sword": return "EKT KILICI";
                case "Bronze Axe": return "Bronz Balta";
                case "Silver Axe": return "Gümüş Balta";
                case "Gold Axe": return "Altın Balta";
                case "Bronze Spear": return "Bronz Mızrak";
                case "Silver Spear": return "Gümüş Mızrak";
                case "Gold Spear": return "Altın Mızrak";
                case "Throwing Knife": return "Fırlatma Bıçağı";
                case "Masterwork Throwing Knife": return "Usta İşi Bıçak";
                case "Health Potion (Can Potu)": return "Can İksiri";
                case "Extra Heart": return "Ekstra Kalp";
            }
            return name;
        }
    }
}
