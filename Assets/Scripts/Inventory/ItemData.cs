using UnityEngine;

namespace Pulsevania.Core
{
    public enum EquipSlot { Head, Chest, Hands, Legs, Feet, Weapon, Shield, ThrowingKnife, Consumable, None }

    public enum StatType { Armor, MoveSpeed, AttackSpeed, MeleeDamage, HeavyDamage, RangedDamage, RestoresHP, MaxHP, CritChance, None }

    [System.Serializable]
    public class ItemData
    {
        public string itemName;
        public EquipSlot equipSlot;
        public Sprite icon;
        public Sprite equippedSprite;
        public int goldPrice;
        public StatType statType;
        public float statValue;
        public float critChance = 0f;
        public int count = 1;

        public ItemData(string name, EquipSlot slot, Sprite iconSprite, Sprite bodySprite, int price, StatType stat, float val, float crit = 0f)
        {
            itemName = name;
            equipSlot = slot;
            icon = iconSprite;
            equippedSprite = bodySprite;
            goldPrice = price;
            statType = stat;
            statValue = val;
            critChance = crit;
        }
    }
}
