using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Pulsevania.Core
{
    public enum GameState
    {
        MainMenu,
        Gameplay,
        Paused,
        GameOver,
        LevelComplete
    }

    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }
        public static GameSaveData pendingLoadData = null;
        public static bool shouldStartInGameplay = false;
        public static bool isNewGameSpawning = false;

        [Header("State Settings")]
        [SerializeField] private GameState initialGameState = GameState.MainMenu;

        // Events
        public static event Action<GameState> OnStateChanged;
        public static event Action<int> OnGoldChanged;
        public static event Action<int> OnKeysChanged;
        public static event Action<int> OnPotionsChanged;
        public static event Action OnPlayerSpawned;

        // Current Run State
        public GameState CurrentState { get; private set; }
        public int CurrentGold { get; private set; }
        public int CurrentKeys { get; private set; }
        public int CurrentPotions { get; private set; }
        public int CurrentLevelIndex { get; private set; } = 1;

        // Persistence Constants
        private const string GoldSaveKey = "Pulsevania_TotalGold";
        private const string HighScoreSaveKey = "Pulsevania_HighScore";
        private const string LevelUnlockSaveKey = "Pulsevania_UnlockedLevel";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            DisableRenderingDebugger();
            InitializeGame();
        }

        private void InitializeGame()
        {
            if (shouldStartInGameplay)
            {
                shouldStartInGameplay = false;
                CurrentState = GameState.Gameplay;
                Time.timeScale = 1f;
            }
            else
            {
                CurrentState = initialGameState;
                if (CurrentState == GameState.MainMenu)
                {
                    Time.timeScale = 0f;
                }
            }
            CurrentLevelIndex = SceneManager.GetActiveScene().buildIndex;
            LoadPersistentData();
            ResetRunStats();
        }

        public void UpdateState(GameState newState)
        {
            if (CurrentState == newState) return;

            CurrentState = newState;

            switch (newState)
            {
                case GameState.MainMenu:
                    Time.timeScale = 1f;
                    
                    // Clear map geometry and room objects
                    if (MapManager.Instance != null)
                    {
                        MapManager.Instance.ClearMapEntities();
                        MapManager.Instance.InitializeRooms();
                    }

                    // Destroy players and stray entities
                    GameObject playerGo = GameObject.FindWithTag("Player");
                    if (playerGo != null) Destroy(playerGo);

                    // Clean up enemies by component type instead of tag to prevent UnityException
                    BaseEnemyAI[] aiEnemies = FindObjectsByType<BaseEnemyAI>(FindObjectsSortMode.None);
                    foreach (var e in aiEnemies)
                    {
                        if (e != null) Destroy(e.gameObject);
                    }

                    EnemyGuardian[] guardianEnemies = FindObjectsByType<EnemyGuardian>(FindObjectsSortMode.None);
                    foreach (var g in guardianEnemies)
                    {
                        if (g != null) Destroy(g.gameObject);
                    }

                    GameObject note = GameObject.Find("PrincessNote");
                    if (note != null) Destroy(note);

                    GameObject merchant = GameObject.Find("MerchantNPC");
                    if (merchant != null) Destroy(merchant);
                    break;
                case GameState.Gameplay:
                    Time.timeScale = 1f;
                    break;
                case GameState.Paused:
                    Time.timeScale = 0f;
                    break;
                case GameState.GameOver:
                    Time.timeScale = 0f;
                    break;
                case GameState.LevelComplete:
                    Time.timeScale = 0f;
                    SaveLevelCompletion();
                    break;
            }

            OnStateChanged?.Invoke(newState);
        }

        // Persistent data loading
        private void LoadPersistentData()
        {
            PlayerPrefs.SetInt(GoldSaveKey, 5000);
            PlayerPrefs.Save();
        }

        // Gold management
        public void AddGold(int amount)
        {
            if (amount < 0) return;
            CurrentGold += amount;
            OnGoldChanged?.Invoke(CurrentGold);

            if (AudioManager.Instance != null && amount > 0)
            {
                AudioManager.Instance.PlaySFX(SoundEffect.CoinPickup);
            }
        }

        public bool ConsumeGold(int amount)
        {
            if (amount < 0 || CurrentGold < amount) return false;
            CurrentGold -= amount;
            OnGoldChanged?.Invoke(CurrentGold);
            return true;
        }

        public void SavePersistentGold()
        {
            int savedGold = PlayerPrefs.GetInt(GoldSaveKey, 0);
            PlayerPrefs.SetInt(GoldSaveKey, savedGold + CurrentGold);
            PlayerPrefs.Save();
            CurrentGold = 0;
            OnGoldChanged?.Invoke(0);
        }

        // Keys management
        public void AddKey(int amount = 1)
        {
            if (amount < 0) return;
            CurrentKeys += amount;
            OnKeysChanged?.Invoke(CurrentKeys);
        }

        public bool UseKey()
        {
            if (CurrentKeys <= 0) return false;
            CurrentKeys--;
            OnKeysChanged?.Invoke(CurrentKeys);
            return true;
        }

        // Potions management
        public void AddPotion(int amount = 1)
        {
            if (amount < 0) return;
            CurrentPotions += amount;
            OnPotionsChanged?.Invoke(CurrentPotions);
        }

        public bool UsePotion()
        {
            if (CurrentPotions <= 0) return false;
            CurrentPotions--;
            OnPotionsChanged?.Invoke(CurrentPotions);
            return true;
        }

        // Level flow management
        public void LoadLevel(int levelBuildIndex)
        {
            ResetRunStats();
            CurrentLevelIndex = levelBuildIndex;
            SceneManager.LoadScene(levelBuildIndex);
            UpdateState(GameState.Gameplay);
        }

        public void LoadNextLevel()
        {
            int nextIndex = SceneManager.GetActiveScene().buildIndex + 1;
            if (nextIndex < SceneManager.sceneCountInBuildSettings)
            {
                LoadLevel(nextIndex);
            }
            else
            {
                // No more levels, return to Main Menu
                UpdateState(GameState.MainMenu);
                SceneManager.LoadScene(0);
            }
        }

        public void RestartLevel()
        {
            LoadLevel(SceneManager.GetActiveScene().buildIndex);
        }

        public void CompleteLevel()
        {
            UpdateState(GameState.LevelComplete);
        }

        public void TriggerPlayerDeath()
        {
            int activeSavepoint = PlayerPrefs.GetInt("ActiveSavepointRoomId", 0);
            if (activeSavepoint < 10)
            {
                SavePersistentGold();
            }
            UpdateState(GameState.GameOver);
        }

        private void SaveLevelCompletion()
        {
            int currentUnlocked = PlayerPrefs.GetInt(LevelUnlockSaveKey, 1);
            if (CurrentLevelIndex >= currentUnlocked)
            {
                PlayerPrefs.SetInt(LevelUnlockSaveKey, CurrentLevelIndex + 1);
            }
            SavePersistentGold();
            PlayerPrefs.Save();
        }

        public int GetUnlockedLevelIndex()
        {
            return PlayerPrefs.GetInt(LevelUnlockSaveKey, 1);
        }

        public void SaveHighScore(int score)
        {
            int currentHigh = PlayerPrefs.GetInt(HighScoreSaveKey, 0);
            if (score > currentHigh)
            {
                PlayerPrefs.SetInt(HighScoreSaveKey, score);
                PlayerPrefs.Save();
            }
        }

        public int GetHighScore()
        {
            return PlayerPrefs.GetInt(HighScoreSaveKey, 0);
        }

        private void ResetRunStats()
        {
            CurrentGold = 0;
            CurrentKeys = 0;
            CurrentPotions = 0;
            OnGoldChanged?.Invoke(CurrentGold);
            OnKeysChanged?.Invoke(0);
            OnPotionsChanged?.Invoke(0);
        }

        // Methods to notify spawn
        public void NotifyPlayerSpawned()
        {
            OnPlayerSpawned?.Invoke();
        }

        public void NewGame()
        {
            ResetRunStats();
            PlayerPrefs.DeleteKey("Pulsevania_ATKUpgrade");
            PlayerPrefs.DeleteKey("Pulsevania_HPUpgrade");
            PlayerPrefs.DeleteKey(LevelUnlockSaveKey);
            PlayerPrefs.DeleteKey("Pulsevania_GoldAdWatchCount");
            PlayerPrefs.DeleteKey("Pulsevania_GoldAdCooldownStartTime");
            PlayerPrefs.DeleteKey("ActiveSavepointRoomId");
            PlayerPrefs.Save();

            isNewGameSpawning = true;
            shouldStartInGameplay = true;

            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.ResetInventory();
            }

            if (MapManager.Instance != null)
            {
                MapManager.Instance.InitializeRooms();
            }

            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                var pc = player.GetComponent<PlayerController>();
                if (pc != null)
                {
                    pc.ResetPlayerStatus();
                }
            }

            shouldStartInGameplay = true;
            LoadLevel(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }

        public void LoadNearestSavepoint(int savepointRoomId)
        {
            if (UIManager.Instance != null)
            {
                UIManager.Instance.CloseGameOverPanel();
            }

            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                var pc = player.GetComponent<PlayerController>();
                if (pc != null)
                {
                    pc.currentHP = pc.maxHP;
                    pc.extraHearts = 1;
                    pc.UpdateHealthUI();
                    pc.UpdateHeartsUI();

                    var anim = player.GetComponentInChildren<Animator>();
                    if (anim != null)
                    {
                        anim.ResetTrigger("Hurt");
                        anim.ResetTrigger("Death");
                        anim.Play("Idle");
                    }
                }

                Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.bodyType = RigidbodyType2D.Dynamic;
                    rb.linearVelocity = Vector2.zero;
                }
            }

            SetKeys(0);

            UpdateState(GameState.Gameplay);

            int roomIndex = savepointRoomId - 1;
            if (MapManager.Instance != null && roomIndex >= 0 && roomIndex < MapManager.Instance.rooms.Count)
            {
                MapManager.Instance.rooms[roomIndex].state = RoomState.Discovered;
                MapManager.Instance.rooms[roomIndex].enemiesSpawned = true;
                MapManager.Instance.rooms[roomIndex].exitDoorUnlocked = false;
            }

            CurrentLevelIndex = savepointRoomId;
            if (MapManager.Instance != null)
            {
                MapManager.Instance.SetActiveRoom(savepointRoomId);
            }
        }

        public void SetGold(int gold)
        {
            CurrentGold = gold;
            OnGoldChanged?.Invoke(CurrentGold);
        }

        public void SetKeys(int keys)
        {
            CurrentKeys = keys;
            OnKeysChanged?.Invoke(CurrentKeys);
        }

        public void SetPotions(int potions)
        {
            CurrentPotions = potions;
            OnPotionsChanged?.Invoke(CurrentPotions);
        }

        public void SetLevelIndex(int index)
        {
            CurrentLevelIndex = index;
        }

        public static void ApplySaveData(GameSaveData data)
        {
            if (data == null) return;

            // 1. Restore persistent manager data (gold, keys, potions, inventory gear, rooms state)
            if (Instance != null)
            {
                Instance.SetGold(data.goldCount);
                Instance.SetKeys(data.keyCount);
                Instance.SetPotions(data.potionCount);
                Instance.SetLevelIndex(data.levelIndex);
            }

            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.EquipItemByName(EquipSlot.Head, data.headItemName);
                InventoryManager.Instance.EquipItemByName(EquipSlot.Chest, data.chestItemName);
                InventoryManager.Instance.EquipItemByName(EquipSlot.Hands, data.handsItemName);
                InventoryManager.Instance.EquipItemByName(EquipSlot.Feet, data.feetItemName);
                InventoryManager.Instance.EquipItemByName(EquipSlot.Weapon, data.weaponItemName);
            }

            // 2. Load rooms status and draw active room maps (so that SetActiveRoom finishes execution first)
            if (MapManager.Instance != null && data.roomStates != null && data.roomStates.Count == 50)
            {
                for (int i = 0; i < 50; i++)
                {
                    MapManager.Instance.rooms[i].state = (RoomState)data.roomStates[i];
                    MapManager.Instance.rooms[i].enemiesSpawned = data.roomEnemiesSpawned[i];
                    if (data.roomExitDoorsUnlocked != null && data.roomExitDoorsUnlocked.Count == 50)
                    {
                        MapManager.Instance.rooms[i].exitDoorUnlocked = data.roomExitDoorsUnlocked[i];
                    }
                }
                
                int targetRoomId = data.lastActiveRoomId;
                if (targetRoomId <= 0)
                {
                    float relativeX = data.playerPositionX - (-80f);
                    targetRoomId = Mathf.FloorToInt(relativeX / 16f) + 1;
                    if (targetRoomId < 1) targetRoomId = 1;
                    if (targetRoomId > 50) targetRoomId = 50;
                }
                
                // Draw map layout (which internally teleports player to door entry initially)
                MapManager.Instance.SetActiveRoom(targetRoomId);

                if (UIManager.Instance != null)
                {
                    UIManager.Instance.RefreshMapUI();
                }
            }

            // 3. Final Step: Overwrite player position and stats to ensure player stays exactly where they saved
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                var pc = player.GetComponent<PlayerController>();
                if (pc != null)
                {
                    pc.currentHP = data.currentHP;
                    pc.extraHearts = data.extraHearts;
                    pc.UpdateHealthUI();
                    pc.UpdateHeartsUI();
                }
                // Zero out any velocity
                Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
                if (rb != null) rb.linearVelocity = Vector2.zero;
            }
        }

        private void DisableRenderingDebugger()
        {
            try
            {
                UnityEngine.Rendering.DebugManager.instance.enableRuntimeUI = false;
                Debug.Log("[GameManager] URP Runtime Debugger UI has been disabled successfully.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GameManager] Failed to disable Rendering Debugger: " + ex.Message);
            }
        }
    }
}
