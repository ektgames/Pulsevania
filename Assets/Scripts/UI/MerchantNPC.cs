using UnityEngine;

namespace Pulsevania.Core
{
    public class MerchantNPC : MonoBehaviour
    {
        [Header("Merchant Settings")]
        public float interactionRadius = 3.0f;
        private bool isPlayerNearby = false;
        private Transform playerTransform;
        private GameObject promptTextGo;

        private void Start()
        {
            // Enforce CircleCollider2D as Trigger for proximity check
            var triggerCol = GetComponent<CircleCollider2D>();
            if (triggerCol == null) triggerCol = gameObject.AddComponent<CircleCollider2D>();
            triggerCol.radius = interactionRadius;
            triggerCol.isTrigger = true;

            // Enforce separate Kinematic Rigidbody2D for flawless trigger registration
            var rb = GetComponent<Rigidbody2D>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.useFullKinematicContacts = true;

            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }

            // Create a detailed merchant shop stall background texture
            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            if (sr != null && sr.sprite == null)
            {
                sr.sprite = CreateDetailedShopSprite();
            }

            // Create prompt text above the merchant canopy
            promptTextGo = new GameObject("ShopPromptText");
            promptTextGo.transform.SetParent(transform);
            promptTextGo.transform.localPosition = new Vector3(0f, 3.2f, 0f); // Positioned above the canopy
            
            var tm = promptTextGo.AddComponent<TextMesh>();
            tm.text = PlayerPrefs.GetString("GameLanguage", "Turkish") == "English" ? "Tap to Shop" : "Alışveriş için tıkla";
            tm.fontSize = 24;
            tm.characterSize = 0.08f;
            tm.color = Color.yellow;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            
            var mr = promptTextGo.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = 30; // High sorting order to render in front
            
            promptTextGo.SetActive(false); // Hidden by default
        }

        private Sprite CreateDetailedShopSprite()
        {
            int w = 64;
            int h = 48;
            Texture2D tex = new Texture2D(w, h);
            
            // Colors matching the requested shop design
            Color purple = new Color(0.35f, 0.12f, 0.38f, 1f);
            Color lightPurple = new Color(0.55f, 0.22f, 0.58f, 1f);
            Color wood = new Color(0.4f, 0.22f, 0.12f, 1f);
            Color darkWood = new Color(0.25f, 0.12f, 0.05f, 1f);
            Color skin = new Color(0.95f, 0.75f, 0.6f, 1f);
            Color shirt = new Color(0.85f, 0.85f, 0.85f, 1f);
            Color gold = Color.yellow;
            Color iron = Color.gray;
            Color bottleBlue = new Color(0.2f, 0.6f, 0.9f, 1f);
            Color bottleRed = new Color(0.9f, 0.2f, 0.2f, 1f);

            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    Color c = Color.clear;

                    // 1. Purple Canopy (Tent) top: y = 28..44
                    float tx = x - 31.5f;
                    float tentHeight = 44f - Mathf.Abs(tx) * 0.45f;
                    bool isTent = (y >= 28 && y <= tentHeight && Mathf.Abs(tx) <= 26f);
                    bool isTentBorder = isTent && (y >= tentHeight - 2f || y <= 30f);

                    // 2. Wooden Pillars
                    bool isPillarLeft = (x >= 8 && x <= 9 && y < 32);
                    bool isPillarRight = (x >= 54 && x <= 55 && y < 32);
                    bool isPillarCenter = (x >= 31 && x <= 32 && y >= 16 && y < 45);

                    // 3. Wooden Counter/Table
                    bool isCounter = (y >= 0 && y <= 15 && x >= 6 && x <= 57);
                    bool isCounterTop = (y == 15 && x >= 5 && x <= 58);

                    // 4. Merchant sitting
                    float mx = x - 42f;
                    float my = y - 20f;
                    bool isMerchantHood = (mx * mx * 1.5f + (my - 5f) * (my - 5f) <= 18f && y >= 23);
                    bool isMerchantFace = (mx * mx * 1.5f + (my - 2f) * (my - 2f) <= 12f && y >= 20);
                    bool isMerchantBody = (mx * mx * 1.2f + my * my <= 36f && y >= 15 && y < 22);

                    // 5. Items on Counter
                    bool isSword = (x >= 15 && x <= 17 && y >= 16 && y <= 26);
                    bool isSwordHilt = (y == 23 && x >= 14 && x <= 18);
                    bool isBottle1 = (x >= 22 && x <= 23 && y >= 16 && y <= 19);
                    bool isBottle2 = (x >= 26 && x <= 27 && y >= 16 && y <= 19);
                    bool isCoins = (x >= 35 && x <= 37 && y >= 16 && y <= 18);

                    if (isPillarCenter)
                    {
                        c = darkWood;
                    }
                    else if (isMerchantFace)
                    {
                        c = skin;
                    }
                    else if (isMerchantHood)
                    {
                        c = purple;
                    }
                    else if (isMerchantBody)
                    {
                        c = shirt;
                    }
                    else if (isTentBorder)
                    {
                        c = lightPurple;
                    }
                    else if (isTent)
                    {
                        c = purple;
                    }
                    else if (isPillarLeft || isPillarRight)
                    {
                        c = wood;
                    }
                    else if (isCoins)
                    {
                        c = gold;
                    }
                    else if (isBottle1)
                    {
                        c = bottleBlue;
                    }
                    else if (isBottle2)
                    {
                        c = bottleRed;
                    }
                    else if (isSword)
                    {
                        c = iron;
                    }
                    else if (isSwordHilt)
                    {
                        c = gold;
                    }
                    else if (isCounterTop)
                    {
                        c = lightPurple;
                    }
                    else if (isCounter)
                    {
                        bool isPlank = (x % 6 == 0);
                        c = isPlank ? darkWood : wood;
                    }

                    tex.SetPixel(x, y, c);
                }
            }
            tex.filterMode = FilterMode.Point;
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 16f);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                isPlayerNearby = true;
                if (promptTextGo != null)
                {
                    string lang = PlayerPrefs.GetString("GameLanguage", "Turkish");
                    promptTextGo.GetComponent<TextMesh>().text = lang == "English" 
                        ? "Tap to Shop" 
                        : "Alışveriş için tıkla";
                    promptTextGo.SetActive(true);
                }
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                isPlayerNearby = false;
                if (promptTextGo != null)
                {
                    promptTextGo.SetActive(false);
                }
                if (UIManager.Instance != null)
                {
                    UIManager.Instance.CloseShopPanel();
                }
            }
        }

        private void Update()
        {
            // Keyboard 'E' check when player is nearby using the new InputSystem
            if (isPlayerNearby && UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.eKey.wasPressedThisFrame)
            {
                if (UIManager.Instance != null && UIManager.Instance.IsInventoryOpen())
                {
                    return;
                }
                OpenMerchantShop();
            }
        }

        private void OnMouseDown()
        {
            if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            if (UIManager.Instance != null && (UIManager.Instance.IsWorldMapOpen() || UIManager.Instance.IsInventoryOpen()))
            {
                return;
            }

            // Click/touch to interact - ignore click if UIManager shop is already open
            if (isPlayerNearby && (UIManager.Instance == null || !UIManager.Instance.IsShopOpen()))
            {
                OpenMerchantShop();
            }
        }

        private void OpenMerchantShop()
        {
            Debug.Log("[Pulsevania Merchant] Activating Shop Panel interface...");

            // 1. First, check if a global Canvas exists
            Canvas mainCanvas = GameObject.FindObjectOfType<Canvas>();
            if (mainCanvas == null)
            {
                Debug.LogError("[Pulsevania Merchant] CRITICAL: No Canvas found in the entire scene!");
                return;
            }

            // 2. Search for the ShopPanel including completely inactive hidden objects
            Transform shopPanelTransform = mainCanvas.transform.Find("MerchantShopPanel") ?? mainCanvas.transform.Find("MerchantShopPanel(Clone)");
            GameObject shopPanel = shopPanelTransform != null ? shopPanelTransform.gameObject : null;

            // If the shop panel exists and is active, do not recreate it to prevent resetting scroll position
            if (shopPanel != null && shopPanel.activeSelf)
            {
                Debug.Log("[Pulsevania Merchant] Shop panel is already active, skipping rebuild.");
                return;
            }

            // ABSOLUTE CLEAN STATE: Destroy existing panel to ensure fresh rebuilt scrollable UI
            if (shopPanel != null)
            {
                if (Application.isPlaying) Destroy(shopPanel);
                else DestroyImmediate(shopPanel);
                shopPanel = null;
            }

            // 3. Spawning a fresh modal blocker panel and nested ShopWindow
            if (shopPanel == null)
            {
                Debug.LogWarning("[Pulsevania Merchant] Generating fresh modal blocker panel...");
                
                // Parent full-screen blocker overlay
                shopPanel = new GameObject("MerchantShopPanel");
                shopPanel.transform.SetParent(mainCanvas.transform, false);
                
                var rect = shopPanel.AddComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.sizeDelta = Vector2.zero;
                rect.anchoredPosition = Vector2.zero;

                var img = shopPanel.AddComponent<UnityEngine.UI.Image>();
                img.color = new Color(0f, 0f, 0f, 0.6f); // Beautiful dark blocker overlay backdrop

                // Absorb all raycasts
                var canvasGroup = shopPanel.AddComponent<CanvasGroup>();
                canvasGroup.blocksRaycasts = true;
                canvasGroup.interactable = true;

                var blockerButton = shopPanel.AddComponent<UnityEngine.UI.Button>();
                blockerButton.transition = UnityEngine.UI.Selectable.Transition.None;

                // Child Window container (where the actual shop content sits)
                GameObject windowGo = new GameObject("ShopWindow");
                windowGo.transform.SetParent(shopPanel.transform, false);
                
                var winRect = windowGo.AddComponent<RectTransform>();
                winRect.anchorMin = new Vector2(0.5f, 0.5f);
                winRect.anchorMax = new Vector2(0.5f, 0.5f);
                winRect.pivot = new Vector2(0.5f, 0.5f);
                winRect.sizeDelta = new Vector2(1100f, 650f);
                winRect.anchoredPosition = Vector2.zero;

                var winImg = windowGo.AddComponent<UnityEngine.UI.Image>();
                winImg.color = new Color(0.12f, 0.08f, 0.06f, 0.98f); // Beautiful dark brown window backdrop
            }

            Transform shopWindowT = shopPanel.transform.Find("ShopWindow");

            // Ensure close button exists and works correctly inside ShopWindow
            Transform closeBtnT = shopWindowT.Find("Btn_CloseShop");
            if (closeBtnT == null)
            {
                GameObject closeBtnObj = new GameObject("Btn_CloseShop");
                closeBtnObj.transform.SetParent(shopWindowT, false);
                var closeRect = closeBtnObj.AddComponent<RectTransform>();
                closeRect.anchorMin = new Vector2(1f, 1f);
                closeRect.anchorMax = new Vector2(1f, 1f);
                closeRect.pivot = new Vector2(1f, 1f);
                closeRect.sizeDelta = new Vector2(40f, 40f);
                closeRect.anchoredPosition = new Vector2(-20f, -20f);
                
                var closeImg = closeBtnObj.AddComponent<UnityEngine.UI.Image>();
                closeImg.color = Color.red;

                var closeButton = closeBtnObj.AddComponent<UnityEngine.UI.Button>();
                
                GameObject textObj = new GameObject("Text");
                textObj.transform.SetParent(closeBtnObj.transform, false);
                var textRect = textObj.AddComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.sizeDelta = Vector2.zero;
                textRect.anchoredPosition = Vector2.zero;

                var closeText = textObj.AddComponent<UnityEngine.UI.Text>();
                closeText.text = "X";
                closeText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                closeText.fontSize = 24;
                closeText.alignment = TextAnchor.MiddleCenter;
                closeText.color = Color.white;
                closeText.fontStyle = FontStyle.Bold;
                
                GameObject targetPanel = shopPanel; // Local variable shadow capture
                closeButton.onClick.AddListener(() => { 
                    targetPanel.SetActive(false); 
                    if (Pulsevania.Core.UIManager.Instance != null) {
                        Pulsevania.Core.UIManager.Instance.ResetSellMode();
                    }
                });
            }

            // Trigger the automatic tiered item layout reconstruction inside this container
            if (Pulsevania.Core.UIManager.Instance != null)
            {
                Pulsevania.Core.UIManager.Instance.PopulateShopItemsProgrammatically(shopWindowT);
            }

            // 4. Force reveal the final targeted interface container
            shopPanel.SetActive(true);
            if (Pulsevania.Core.UIManager.Instance != null)
            {
                Pulsevania.Core.UIManager.Instance.UpdateMerchantShopGold();
            }
            Debug.Log("[Pulsevania Merchant] SUCCESS: Shop panel is now explicitly set to active on screen.");
        }
    }
}
