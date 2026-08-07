using System;
using System.IO;
using UnityEngine;

namespace Pulsevania.Core
{
    [System.Serializable]
    public class GameSaveData
    {
        public float playerPositionX;
        public float playerPositionY;
        public float currentHP;
        public int extraHearts;
        public int goldCount;
        public int keyCount;
        public int potionCount;
        public int levelIndex;

        // Equipped Items
        public string headItemName;
        public string chestItemName;
        public string handsItemName;
        public string feetItemName;
        public string weaponItemName;

        // Room persistence
        public int lastActiveRoomId;
        public System.Collections.Generic.List<int> roomStates;
        public System.Collections.Generic.List<bool> roomEnemiesSpawned;
        public System.Collections.Generic.List<bool> roomExitDoorsUnlocked;
    }

    public static class SaveManager
    {
        private static string GetSavePath(int slotIndex)
        {
            return Path.Combine(Application.persistentDataPath, $"save_slot_{slotIndex}.json");
        }

        public static bool SaveExists(int slotIndex)
        {
            return File.Exists(GetSavePath(slotIndex));
        }

        public static void Save(int slotIndex)
        {
            try
            {
                GameSaveData data = new GameSaveData();
                
                // Get player state
                GameObject player = GameObject.FindWithTag("Player");
                if (player != null)
                {
                    data.playerPositionX = player.transform.position.x;
                    data.playerPositionY = player.transform.position.y;
                    
                    PlayerController pc = player.GetComponent<PlayerController>();
                    if (pc != null)
                    {
                        data.currentHP = pc.currentHP;
                        data.extraHearts = pc.extraHearts;
                    }
                }
                else
                {
                    data.currentHP = 100f;
                    data.extraHearts = 1;
                }

                // Get inventory and run stats
                if (GameManager.Instance != null)
                {
                    data.goldCount = GameManager.Instance.CurrentGold;
                    data.keyCount = GameManager.Instance.CurrentKeys;
                    data.potionCount = GameManager.Instance.CurrentPotions;
                    data.levelIndex = GameManager.Instance.CurrentLevelIndex;
                }

                // Get equipped gear
                if (InventoryManager.Instance != null)
                {
                    var eq = InventoryManager.Instance.equippedItems;
                    data.headItemName = eq.ContainsKey(EquipSlot.Head) && eq[EquipSlot.Head] != null ? eq[EquipSlot.Head].itemName : "";
                    data.chestItemName = eq.ContainsKey(EquipSlot.Chest) && eq[EquipSlot.Chest] != null ? eq[EquipSlot.Chest].itemName : "";
                    data.handsItemName = eq.ContainsKey(EquipSlot.Hands) && eq[EquipSlot.Hands] != null ? eq[EquipSlot.Hands].itemName : "";
                    data.feetItemName = eq.ContainsKey(EquipSlot.Feet) && eq[EquipSlot.Feet] != null ? eq[EquipSlot.Feet].itemName : "";
                    data.weaponItemName = eq.ContainsKey(EquipSlot.Weapon) && eq[EquipSlot.Weapon] != null ? eq[EquipSlot.Weapon].itemName : "";
                }

                // Save MapManager rooms state
                if (MapManager.Instance != null)
                {
                    data.lastActiveRoomId = MapManager.Instance.GetCurrentRoomId();
                    data.roomStates = new System.Collections.Generic.List<int>();
                    data.roomEnemiesSpawned = new System.Collections.Generic.List<bool>();
                    data.roomExitDoorsUnlocked = new System.Collections.Generic.List<bool>();
                    foreach (var room in MapManager.Instance.rooms)
                    {
                        data.roomStates.Add((int)room.state);
                        data.roomEnemiesSpawned.Add(room.enemiesSpawned);
                        data.roomExitDoorsUnlocked.Add(room.exitDoorUnlocked);
                    }
                }

                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(GetSavePath(slotIndex), json);
                Debug.Log($"[Pulsevania] Saved game to slot {slotIndex} successfully.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Pulsevania] Failed to save game to slot {slotIndex}: {e.Message}");
            }
        }

        public static GameSaveData Load(int slotIndex)
        {
            string path = GetSavePath(slotIndex);
            if (!File.Exists(path)) return null;

            try
            {
                string json = File.ReadAllText(path);
                return JsonUtility.FromJson<GameSaveData>(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Pulsevania] Failed to load save from slot {slotIndex}: {e.Message}");
                return null;
            }
        }
    }
}
