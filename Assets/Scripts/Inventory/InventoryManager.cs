using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Pulsevania.Core
{
    public class InventoryManager : MonoBehaviour
    {
        public static InventoryManager Instance { get; private set; }

        [Header("Equipment References")]
        public SpriteRenderer visualHead;
        public SpriteRenderer visualChest;
        public SpriteRenderer visualHands;
        public SpriteRenderer visualLegs;
        public SpriteRenderer visualFeet;
        public SpriteRenderer visualWeapon;
        public SpriteRenderer visualShield;
        public SpriteRenderer visualThrowingKnife;

        [Header("Item Database")]
        public List<ItemData> itemDatabase = new List<ItemData>();

        [Header("Player Inventory Status")]
        public List<ItemData> inventoryItems = new List<ItemData>();
        public Dictionary<EquipSlot, ItemData> equippedItems = new Dictionary<EquipSlot, ItemData>();

        // Generated Item Sprites for UI & player
        public Sprite headIcon, headBody;
        public Sprite chestIcon, chestBody;
        public Sprite handsIcon, handsBody;
        public Sprite legsIcon, legsBody;
        public Sprite feetIcon, feetBody;
        public Sprite swordIcon, swordBody;
        public Sprite axeIcon, axeBody;
        public Sprite shieldIcon, shieldBody;
        public Sprite knifeIcon, knifeBody;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                GenerateItemSprites();
                InitializeDatabase();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            LocatePlayerVisuals();
        }

        public void LocatePlayerVisuals()
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                Transform headT = player.transform.Find("Equip_Helmet") ?? player.transform.Find("Visual_Head");
                if (headT != null) visualHead = headT.GetComponent<SpriteRenderer>();

                Transform chestT = player.transform.Find("Equip_Armor") ?? player.transform.Find("Visual_Chest");
                if (chestT != null) visualChest = chestT.GetComponent<SpriteRenderer>();

                Transform handsT = player.transform.Find("Equip_Gloves") ?? player.transform.Find("Visual_Hands");
                if (handsT != null) visualHands = handsT.GetComponent<SpriteRenderer>();

                Transform legsT = player.transform.Find("Visual_Legs");
                if (legsT != null) visualLegs = legsT.GetComponent<SpriteRenderer>();

                Transform feetT = player.transform.Find("Equip_Boots") ?? player.transform.Find("Visual_Feet");
                if (feetT != null) visualFeet = feetT.GetComponent<SpriteRenderer>();

                Transform weaponT = player.transform.Find("Visual_Weapon");
                if (weaponT != null) visualWeapon = weaponT.GetComponent<SpriteRenderer>();

                Transform shieldT = player.transform.Find("Visual_Shield");
                if (shieldT != null) visualShield = shieldT.GetComponent<SpriteRenderer>();

                Transform knifeT = player.transform.Find("Visual_ThrowingKnife");
                if (knifeT != null) visualThrowingKnife = knifeT.GetComponent<SpriteRenderer>();

                UpdateVisualEquipment();
            }
        }

        private Sprite CreateTieredSprite(string[] design, string tier, Vector2? customPivot = null)
        {
            var pal = new Dictionary<char, Color> {
                { '.', Color.clear },
                { 'P', new Color(0.9f, 0.1f, 0.2f) },      // Royal Crimson Plume (Red)
                { 'H', new Color(0.9f, 0.9f, 0.95f) }     // Metallic visors/highlights (Shiny White)
            };

            if (tier == "Bronze")
            {
                pal['W'] = new Color(0.2f, 0.1f, 0.05f);      // Deep copper/brown outline
                pal['S'] = new Color(0.45f, 0.22f, 0.08f);    // Dark bronze shadow
                pal['M'] = new Color(0.7f, 0.38f, 0.12f);     // Mid-tone bronze
                pal['K'] = new Color(0.95f, 0.65f, 0.18f);    // Bright amber highlight
            }
            else if (tier == "Silver")
            {
                pal['W'] = new Color(0.2f, 0.22f, 0.28f);     // Slate-grey border
                pal['S'] = new Color(0.42f, 0.52f, 0.65f);    // Stark steel blue mid-tone
                pal['M'] = new Color(0.72f, 0.75f, 0.82f);    // Pure metallic slate-grey
                pal['K'] = new Color(1f, 1f, 1f);             // Brilliant white reflection tip
            }
            else if (tier == "Gold")
            {
                pal['W'] = new Color(0.42f, 0.22f, 0.02f);    // Deep royal orange-yellow border/shadow
                pal['S'] = new Color(0.75f, 0.48f, 0.04f);    // Luminous orange-yellow mid-tone
                pal['M'] = new Color(0.96f, 0.78f, 0.08f);    // Luminous gold core
                pal['K'] = new Color(1f, 0.94f, 0.48f);       // Radiant light gold highlight
            }
            else if (tier == "EKT")
            {
                pal['W'] = new Color(0.18f, 0.05f, 0.28f);    // Dark purple outline
                pal['M'] = new Color(0.6f, 0.2f, 0.9f);       // Rich purple blade core
                pal['S'] = new Color(0.85f, 0.55f, 0.05f);    // Gold shadow/guard
                pal['K'] = new Color(0.98f, 0.85f, 0.2f);     // Radiant gold highlights
            }
            else // custom/default
            {
                pal['W'] = new Color(0.12f, 0.12f, 0.15f);    // Black outline
                pal['M'] = new Color(0.5f, 0.3f, 0.1f);       // Wood
                pal['S'] = new Color(0.3f, 0.15f, 0.05f);     // Dark Wood
                pal['K'] = new Color(0.12f, 0.12f, 0.15f);
            }

            Vector2 pivot = customPivot ?? new Vector2(0.5f, 0.5f);
            return CreateProceduralItemSprite(design, pal, pivot);
        }

        private void GenerateItemSprites()
        {
            // Empty sprite pool as we generate tiered sprites dynamically inside InitializeDatabase
        }

        private Sprite CreateProceduralItemSprite(string[] design, Dictionary<char, Color> colorMap)
        {
            return CreateProceduralItemSprite(design, colorMap, new Vector2(0.5f, 0.5f));
        }

        private Sprite CreateProceduralItemSprite(string[] design, Dictionary<char, Color> colorMap, Vector2 pivot)
        {
            int h = design.Length;
            int w = 0;
            // Find maximum width dynamically to handle uneven design rows safely
            for (int i = 0; i < h; i++)
            {
                if (design[i].Length > w) w = design[i].Length;
            }

            Texture2D tex = new Texture2D(w, h);
            for (int y = 0; y < h; y++)
            {
                int row = h - 1 - y;
                for (int x = 0; x < w; x++)
                {
                    Color col = Color.clear;
                    if (x < design[row].Length)
                    {
                        char c = design[row][x];
                        col = colorMap.ContainsKey(c) ? colorMap[c] : Color.clear;
                    }
                    tex.SetPixel(x, y, col);
                }
            }
            tex.filterMode = FilterMode.Point;
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), pivot, 16f);
        }

        private Sprite CreateColorSprite(Color color, int w, int h)
        {
            Texture2D tex = new Texture2D(w, h);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (x > 0 && x < w - 1 && y > 0 && y < h - 1)
                        tex.SetPixel(x, y, color);
                    else
                        tex.SetPixel(x, y, Color.black);
                }
            }
            tex.filterMode = FilterMode.Point;
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 16f);
        }

        private void InitializeDatabase()
        {
            // Designs
            string[] helmetDesign = {
                "......PPPP......",
                "....PPWWWWWW....",
                "...PWWKKKKWW....",
                "..PWWMMMMMMW....",
                ".WMMWWMMWWMMW...",
                ".WMMHHHHHHMMW...",
                ".WMMSSMMSSMMW...",
                "..WMMMMMMMMW....",
                "...WWWWWWWW....."
            };

            string[] chestDesign = {
                "...WW....WW...",
                "..WKKW..WKKW..",
                ".WKKKKKKKKKKW.",
                ".WKKHHMMHHKKW.",
                ".WKKHMMMMHKKW.",
                ".WKKHHMMHHKKW.",
                "..WKKKKKKKKW..",
                "...WWWWWWWW..."
            };

            string[] handsDesign = {
                "....WWWW....",
                "...WKKKKW...",
                "..WKKKKKKW..",
                "..WMMSMSMW..",
                "...WKKKKW...",
                "....WWWW...."
            };

            string[] legsDesign = {
                ".....WWWWWW.....",
                "....WKKKKKKW....",
                "....WKKKKKKW....",
                "....WKK..KKW....",
                "....WKS..SKW....",
                "....WKK..KKW....",
                ".....WW..WW....."
            };

            string[] feetDesign = {
                "......WW..WW....",
                ".....WKKWWKKW...",
                "....WKKHWKKHW...",
                "....WWWWWWWW...."
            };

            string[] swordDesign = {
                "......W.........",
                ".....WMW........",
                "....WMW.........",
                "...WMW..........",
                "..WSW...........",
                ".WSW............",
                "WKW.............",
                ".W.............."
            };

            string[] axeDesign = {
                "....WMMW........",
                "...WMMMW........",
                "..WMMMMMW.......",
                "..WMMSWMMW......",
                "...W.SW.W.......",
                "....SW..........",
                "....S...........",
                "....S..........."
            };

            string[] spearDesign = {
                ".......WMW......",
                "......WMSW......",
                ".....W.S.W......",
                "......S.........",
                ".....S..........",
                "....S...........",
                "...S............",
                "..S............."
            };

            string[] knifeDesign = {
                "......W.....",
                ".....WMW....",
                "....WSW.....",
                "...WKW......",
                "....W......."
            };

            string[] shieldDesign = {
                "....WWWWWW....",
                "...WMMMMMMW...",
                "..WMMMMMMMMW..",
                "..WMMSSMMSSW..",
                "..WMMSSMMSSW..",
                "..WMMMMMMMMW..",
                "...WMMMMMMW...",
                "....WWWWWW...."
            };

            string[] potionDesign = {
                ".....WW.....",
                "....WGGW....",
                "...WGGGGW...",
                "..WRRRRRRW..",
                "..WRRRRRRW..",
                "..WRRRRRRW..",
                "...WRRRRW...",
                "....WWWW...."
            };

            // Dynamic palettes for custom icons
            var potionPal = new Dictionary<char, Color> {
                { '.', Color.clear },
                { 'W', new Color(0.12f, 0.12f, 0.15f) },
                { 'G', new Color(0.6f, 0.8f, 0.85f) }, // Flask Glass Cyan
                { 'R', new Color(0.9f, 0.1f, 0.1f) }  // Vibrant Red Health Liquid
            };

            Sprite potionIcon = CreateProceduralItemSprite(potionDesign, potionPal);

            string[] heartDesign = {
                "....WWWW....WWWW....",
                "..WWMMMMWWWWMMMMWW..",
                ".WMMMMMMMMMMMMMMMMW.",
                ".WMMMMMMMMMMMMMMMMW.",
                ".WMMSSMMMMMMMMMMMMW.",
                ".WMMSSMMMMMMMMMMMMW.",
                "..WMMMMMMMMMMMMMMW..",
                "...WMMMMMMMMMMMMW...",
                "....WMMMMMMMMMMW....",
                ".....WMMMMMMMMW.....",
                "......WMMMMMMW......",
                ".......WMMMMW.......",
                "........WMMW........",
                ".........WW........."
            };

            var heartPal = new Dictionary<char, Color> {
                { '.', Color.clear },
                { 'W', new Color(0.12f, 0.12f, 0.15f) },
                { 'M', new Color(0.9f, 0.1f, 0.1f) },
                { 'S', Color.white }
            };

            Sprite heartIcon = CreateProceduralItemSprite(heartDesign, heartPal);

            itemDatabase.Clear();

            // 1. Helmets (MaxHP veren zırhlar)
            itemDatabase.Add(new ItemData("Bronze Helmet", EquipSlot.Head, CreateTieredSprite(helmetDesign, "Bronze"), CreateTieredSprite(helmetDesign, "Bronze"), 25, StatType.MaxHP, 15f));
            itemDatabase.Add(new ItemData("Silver Helmet", EquipSlot.Head, CreateTieredSprite(helmetDesign, "Silver"), CreateTieredSprite(helmetDesign, "Silver"), 60, StatType.MaxHP, 35f));
            itemDatabase.Add(new ItemData("Gold Helmet", EquipSlot.Head, CreateTieredSprite(helmetDesign, "Gold"), CreateTieredSprite(helmetDesign, "Gold"), 250, StatType.MaxHP, 70f));

            // 2. Armor (MaxHP veren zırhlar)
            itemDatabase.Add(new ItemData("Bronze Armor", EquipSlot.Chest, CreateTieredSprite(chestDesign, "Bronze"), CreateTieredSprite(chestDesign, "Bronze"), 35, StatType.MaxHP, 25f));
            itemDatabase.Add(new ItemData("Silver Armor", EquipSlot.Chest, CreateTieredSprite(chestDesign, "Silver"), CreateTieredSprite(chestDesign, "Silver"), 80, StatType.MaxHP, 60f));
            itemDatabase.Add(new ItemData("Gold Armor", EquipSlot.Chest, CreateTieredSprite(chestDesign, "Gold"), CreateTieredSprite(chestDesign, "Gold"), 320, StatType.MaxHP, 120f));

            // 3. Boots (MaxHP veren zırhlar)
            itemDatabase.Add(new ItemData("Bronze Boots", EquipSlot.Feet, CreateTieredSprite(feetDesign, "Bronze"), CreateTieredSprite(feetDesign, "Bronze"), 25, StatType.MaxHP, 10f));
            itemDatabase.Add(new ItemData("Silver Boots", EquipSlot.Feet, CreateTieredSprite(feetDesign, "Silver"), CreateTieredSprite(feetDesign, "Silver"), 50, StatType.MaxHP, 25f));
            itemDatabase.Add(new ItemData("Gold Boots", EquipSlot.Feet, CreateTieredSprite(feetDesign, "Gold"), CreateTieredSprite(feetDesign, "Gold"), 220, StatType.MaxHP, 50f));

            // 4. Gloves (MaxHP veren zırhlar)
            itemDatabase.Add(new ItemData("Bronze Gloves", EquipSlot.Hands, CreateTieredSprite(handsDesign, "Bronze"), CreateTieredSprite(handsDesign, "Bronze"), 25, StatType.MaxHP, 10f));
            itemDatabase.Add(new ItemData("Silver Gloves", EquipSlot.Hands, CreateTieredSprite(handsDesign, "Silver"), CreateTieredSprite(handsDesign, "Silver"), 50, StatType.MaxHP, 25f));
            itemDatabase.Add(new ItemData("Gold Gloves", EquipSlot.Hands, CreateTieredSprite(handsDesign, "Gold"), CreateTieredSprite(handsDesign, "Gold"), 220, StatType.MaxHP, 50f));

            // 4.5. Pants (MaxHP veren zırhlar)
            itemDatabase.Add(new ItemData("Bronze Pants", EquipSlot.Legs, CreateTieredSprite(legsDesign, "Bronze"), CreateTieredSprite(legsDesign, "Bronze"), 30, StatType.MaxHP, 20f));
            itemDatabase.Add(new ItemData("Silver Pants", EquipSlot.Legs, CreateTieredSprite(legsDesign, "Silver"), CreateTieredSprite(legsDesign, "Silver"), 70, StatType.MaxHP, 45f));
            itemDatabase.Add(new ItemData("Gold Pants", EquipSlot.Legs, CreateTieredSprite(legsDesign, "Gold"), CreateTieredSprite(legsDesign, "Gold"), 280, StatType.MaxHP, 90f));

            // 5. Swords (Hasar ve Kritik)
            itemDatabase.Add(new ItemData("Bronze Sword", EquipSlot.Weapon, CreateTieredSprite(swordDesign, "Bronze", new Vector2(0.2f, 0.15f)), CreateTieredSprite(swordDesign, "Bronze", new Vector2(0.2f, 0.15f)), 35, StatType.MeleeDamage, 3f, 0.12f));
            itemDatabase.Add(new ItemData("Silver Sword", EquipSlot.Weapon, CreateTieredSprite(swordDesign, "Silver", new Vector2(0.2f, 0.15f)), CreateTieredSprite(swordDesign, "Silver", new Vector2(0.2f, 0.15f)), 70, StatType.MeleeDamage, 7f, 0.22f));
            itemDatabase.Add(new ItemData("Gold Sword", EquipSlot.Weapon, CreateTieredSprite(swordDesign, "Gold", new Vector2(0.2f, 0.15f)), CreateTieredSprite(swordDesign, "Gold", new Vector2(0.2f, 0.15f)), 300, StatType.MeleeDamage, 12f, 0.35f));
            itemDatabase.Add(new ItemData("EKT Sword", EquipSlot.Weapon, CreateTieredSprite(swordDesign, "EKT", new Vector2(0.2f, 0.15f)), CreateTieredSprite(swordDesign, "EKT", new Vector2(0.2f, 0.15f)), 3000, StatType.MeleeDamage, 25f, 0.50f));

            // 6. Axes (Hasar ve Kritik)
            itemDatabase.Add(new ItemData("Bronze Axe", EquipSlot.Weapon, CreateTieredSprite(axeDesign, "Bronze", new Vector2(0.2f, 0.15f)), CreateTieredSprite(axeDesign, "Bronze", new Vector2(0.2f, 0.15f)), 35, StatType.HeavyDamage, 4.5f, 0.08f));
            itemDatabase.Add(new ItemData("Silver Axe", EquipSlot.Weapon, CreateTieredSprite(axeDesign, "Silver", new Vector2(0.2f, 0.15f)), CreateTieredSprite(axeDesign, "Silver", new Vector2(0.2f, 0.15f)), 70, StatType.HeavyDamage, 8.5f, 0.15f));
            itemDatabase.Add(new ItemData("Gold Axe", EquipSlot.Weapon, CreateTieredSprite(axeDesign, "Gold", new Vector2(0.2f, 0.15f)), CreateTieredSprite(axeDesign, "Gold", new Vector2(0.2f, 0.15f)), 300, StatType.HeavyDamage, 15f, 0.25f));

            // 7. Spears (Hasar ve Kritik)
            itemDatabase.Add(new ItemData("Bronze Spear", EquipSlot.Weapon, CreateTieredSprite(spearDesign, "Bronze", new Vector2(0.2f, 0.15f)), CreateTieredSprite(spearDesign, "Bronze", new Vector2(0.2f, 0.15f)), 45, StatType.MeleeDamage, 3.5f, 0.10f));
            itemDatabase.Add(new ItemData("Silver Spear", EquipSlot.Weapon, CreateTieredSprite(spearDesign, "Silver", new Vector2(0.2f, 0.15f)), CreateTieredSprite(spearDesign, "Silver", new Vector2(0.2f, 0.15f)), 80, StatType.MeleeDamage, 6f, 0.18f));
            itemDatabase.Add(new ItemData("Gold Spear", EquipSlot.Weapon, CreateTieredSprite(spearDesign, "Gold", new Vector2(0.2f, 0.15f)), CreateTieredSprite(spearDesign, "Gold", new Vector2(0.2f, 0.15f)), 320, StatType.MeleeDamage, 11f, 0.30f));

            // 8. Throwing Knives (Hasar ve Kritik)
            itemDatabase.Add(new ItemData("Throwing Knife", EquipSlot.ThrowingKnife, CreateTieredSprite(knifeDesign, "Silver", new Vector2(0.2f, 0.15f)), CreateTieredSprite(knifeDesign, "Silver", new Vector2(0.2f, 0.15f)), 12, StatType.RangedDamage, 8f, 0.15f));
            itemDatabase.Add(new ItemData("Masterwork Throwing Knife", EquipSlot.ThrowingKnife, CreateTieredSprite(knifeDesign, "Gold", new Vector2(0.2f, 0.15f)), CreateTieredSprite(knifeDesign, "Gold", new Vector2(0.2f, 0.15f)), 100, StatType.RangedDamage, 20f, 0.30f));

            // 8.5. Shields (Kalkanlar artık HP artışı vermiyor)
            itemDatabase.Add(new ItemData("Bronze Shield", EquipSlot.Shield, CreateTieredSprite(shieldDesign, "Bronze"), CreateTieredSprite(shieldDesign, "Bronze"), 40, StatType.None, 0f));
            itemDatabase.Add(new ItemData("Silver Shield", EquipSlot.Shield, CreateTieredSprite(shieldDesign, "Silver"), CreateTieredSprite(shieldDesign, "Silver"), 80, StatType.None, 0f));
            itemDatabase.Add(new ItemData("Gold Shield", EquipSlot.Shield, CreateTieredSprite(shieldDesign, "Gold"), CreateTieredSprite(shieldDesign, "Gold"), 320, StatType.None, 0f));

            // 9. Consumables
            itemDatabase.Add(new ItemData("Health Potion (Can Potu)", EquipSlot.Consumable, potionIcon, potionIcon, 15, StatType.RestoresHP, 1f));

            // 10. Extra Hearts (Second Chance / Save Point currency)
            itemDatabase.Add(new ItemData("Extra Heart", EquipSlot.Consumable, heartIcon, heartIcon, 100, StatType.None, 1f));

            equippedItems[EquipSlot.Head] = null;
            equippedItems[EquipSlot.Chest] = null;
            equippedItems[EquipSlot.Hands] = null;
            equippedItems[EquipSlot.Legs] = null;
            equippedItems[EquipSlot.Feet] = null;
            
            // Oyuna baslarken kuşanılmış 1 adet Bronz Kılıç olsun
            ItemData bronzeSword = itemDatabase.Find(x => x.itemName == "Bronze Sword");
            if (bronzeSword != null)
            {
                ItemData weaponCopy = new ItemData(bronzeSword.itemName, bronzeSword.equipSlot, bronzeSword.icon, bronzeSword.equippedSprite, bronzeSword.goldPrice, bronzeSword.statType, bronzeSword.statValue);
                weaponCopy.count = 1;
                equippedItems[EquipSlot.Weapon] = weaponCopy;
            }
            else
            {
                equippedItems[EquipSlot.Weapon] = null;
            }
            
            equippedItems[EquipSlot.Shield] = null;
            equippedItems[EquipSlot.ThrowingKnife] = null;
        }

        public void ResetInventory()
        {
            inventoryItems.Clear();
            equippedItems[EquipSlot.Head] = null;
            equippedItems[EquipSlot.Chest] = null;
            equippedItems[EquipSlot.Hands] = null;
            equippedItems[EquipSlot.Legs] = null;
            equippedItems[EquipSlot.Feet] = null;

            ItemData bronzeSword = itemDatabase.Find(x => x.itemName == "Bronze Sword");
            if (bronzeSword != null)
            {
                ItemData weaponCopy = new ItemData(bronzeSword.itemName, bronzeSword.equipSlot, bronzeSword.icon, bronzeSword.equippedSprite, bronzeSword.goldPrice, bronzeSword.statType, bronzeSword.statValue);
                weaponCopy.count = 1;
                equippedItems[EquipSlot.Weapon] = weaponCopy;
            }
            else
            {
                equippedItems[EquipSlot.Weapon] = null;
            }

            equippedItems[EquipSlot.Shield] = null;
            equippedItems[EquipSlot.ThrowingKnife] = null;

            UpdateVisualEquipment();

            if (UIManager.Instance != null)
            {
                UIManager.Instance.UpdateInventoryUI();
            }
            SyncHUDPotions();
        }

        public bool AddItem(ItemData item)
        {
            if (item == null) return false;

            // Health Potion (Can Potu) özel kuralı (En fazla 10 adet taşınabilir, tek kutuda birikir)
            if (item.itemName == "Health Potion (Can Potu)")
            {
                ItemData existingPot = inventoryItems.Find(x => x.itemName == "Health Potion (Can Potu)");
                if (existingPot != null)
                {
                    if (existingPot.count >= 10)
                    {
                        if (DamageTextPool.Instance != null)
                        {
                            DamageTextPool.Instance.SpawnText(GameObject.FindWithTag("Player").transform.position, "Maksimum Pot Sınırı (10)!", Color.red);
                        }
                        if (UIManager.Instance != null)
                        {
                            UIManager.Instance.ShowShopWarning("Maksimum Pot Sınırına (10) Ulaştınız!");
                        }
                        return false;
                    }
                    
                    existingPot.count = Mathf.Min(10, existingPot.count + item.count);
                    Debug.Log($"[Pulsevania Inventory] Potions stacked: Count: {existingPot.count}");
                    
                    if (UIManager.Instance != null)
                    {
                        UIManager.Instance.UpdateInventoryUI();
                    }
                    SyncHUDPotions();
                    return true;
                }
            }

            ItemData existing = inventoryItems.Find(x => x.itemName == item.itemName);
            if (existing != null && item.itemName != "Health Potion (Can Potu)")
            {
                existing.count += item.count;
                Debug.Log($"[Pulsevania Inventory] Item stacked: {item.itemName}, Count: {existing.count}");
                if (UIManager.Instance != null)
                {
                    UIManager.Instance.UpdateInventoryUI();
                }
                SyncHUDPotions();
                return true;
            }

            if (inventoryItems.Count >= 8)
            {
                if (DamageTextPool.Instance != null)
                {
                    bool isTR = PlayerPrefs.GetString("GameLanguage", "Turkish") == "Turkish";
                    DamageTextPool.Instance.SpawnText(GameObject.FindWithTag("Player").transform.position, isTR ? "Envanter Dolu!" : "Inventory Full!", Color.red);
                }
                return false;
            }

            if (item.itemName == "Health Potion (Can Potu)")
            {
                item.count = Mathf.Min(10, item.count);
            }
            else
            {
                item.count = 1;
            }

            inventoryItems.Add(item);
            Debug.Log($"[Pulsevania Inventory] Item added: {item.itemName}");
            
            if (UIManager.Instance != null)
            {
                UIManager.Instance.UpdateInventoryUI();
            }
            SyncHUDPotions();
            return true;
        }

        public void EquipItem(ItemData item, int fromIndex)
        {
            if (item == null) return;

            EquipSlot slot = item.equipSlot;
            if (equippedItems[slot] != null)
            {
                AddItem(equippedItems[slot]);
            }

            if (item.count > 1)
            {
                item.count--;
                ItemData equipCopy = new ItemData(item.itemName, item.equipSlot, item.icon, item.equippedSprite, item.goldPrice, item.statType, item.statValue);
                equipCopy.count = 1;
                equippedItems[slot] = equipCopy;
            }
            else
            {
                equippedItems[slot] = item;
                inventoryItems.RemoveAt(fromIndex);
            }

            if (slot == EquipSlot.ThrowingKnife)
            {
                GameObject playerObj = GameObject.FindWithTag("Player");
                if (playerObj != null)
                {
                    PlayerController pc = playerObj.GetComponent<PlayerController>();
                    if (pc != null) pc.knifeAmmo = 10;
                }
            }

            UpdateVisualEquipment();

            if (UIManager.Instance != null)
            {
                UIManager.Instance.UpdateInventoryUI();
            }
        }

        public void UnequipItem(EquipSlot slot)
        {
            if (equippedItems[slot] == null) return;

            if (inventoryItems.Count < 8)
            {
                inventoryItems.Add(equippedItems[slot]);
                equippedItems[slot] = null;

                UpdateVisualEquipment();

                if (UIManager.Instance != null)
                {
                    UIManager.Instance.UpdateInventoryUI();
                }
            }
            else
            {
                if (DamageTextPool.Instance != null)
                {
                    bool isTR = PlayerPrefs.GetString("GameLanguage", "Turkish") == "Turkish";
                    DamageTextPool.Instance.SpawnText(GameObject.FindWithTag("Player").transform.position, isTR ? "Envanter Dolu!" : "Inventory Full!", Color.red);
                }
            }
        }

        public void UpdateVisualEquipment()
        {
            if (visualHead != null) visualHead.enabled = false;
            if (visualChest != null) visualChest.enabled = false;
            if (visualHands != null) visualHands.enabled = false;
            if (visualLegs != null) visualLegs.enabled = false;
            if (visualFeet != null) visualFeet.enabled = false;
            if (visualWeapon != null) visualWeapon.enabled = false;
            if (visualShield != null) visualShield.enabled = false;
            if (visualThrowingKnife != null) visualThrowingKnife.enabled = false;

            RecalculateStats();
        }

        public void RecalculateStats()
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player == null) return;

            PlayerController controller = player.GetComponent<PlayerController>();
            if (controller == null) return;

            int maxHPBonus = 0;
            float moveSpeed = 0f;
            float attackSpeed = 0f;
            int meleeDmg = 0;
            int heavyDmg = 0;
            int rangedDmg = 0;
            float critChance = 0f;

            foreach (var kvp in equippedItems)
            {
                ItemData item = kvp.Value;
                if (item == null) continue;

                switch (item.statType)
                {
                    case StatType.MaxHP:
                        maxHPBonus += (int)item.statValue;
                        break;
                    case StatType.MoveSpeed:
                        moveSpeed += item.statValue;
                        break;
                    case StatType.AttackSpeed:
                        attackSpeed += item.statValue;
                        break;
                    case StatType.MeleeDamage:
                        meleeDmg += (int)item.statValue;
                        critChance += item.critChance;
                        break;
                    case StatType.HeavyDamage:
                        heavyDmg += (int)item.statValue;
                        critChance += item.critChance;
                        break;
                    case StatType.RangedDamage:
                        rangedDmg += (int)item.statValue;
                        critChance += item.critChance;
                        break;
                }
            }

            controller.SyncEquipmentStats(maxHPBonus, moveSpeed, attackSpeed, meleeDmg, heavyDmg, rangedDmg, critChance);
        }

        public void UseConsumable(int fromIndex)
        {
            if (fromIndex < 0 || fromIndex >= inventoryItems.Count) return;
            ItemData item = inventoryItems[fromIndex];
            if (item.equipSlot != EquipSlot.Consumable) return;

            GameObject player = GameObject.FindWithTag("Player");
            if (player == null) return;
            PlayerController pc = player.GetComponent<PlayerController>();
            if (pc == null) return;

            if (item.itemName == "Extra Heart")
            {
                if (pc.extraHearts < 3)
                {
                    pc.extraHearts++;
                    pc.UpdateHeartsUI();

                    if (item.count > 1) item.count--;
                    else inventoryItems.RemoveAt(fromIndex);

                    if (AudioManager.Instance != null)
                    {
                        AudioManager.Instance.PlaySFX(SoundEffect.CoinPickup);
                    }

                    if (UIManager.Instance != null)
                    {
                        UIManager.Instance.UpdateInventoryUI();
                    }

                    if (DamageTextPool.Instance != null)
                    {
                        bool isTR = PlayerPrefs.GetString("GameLanguage", "Turkish") == "Turkish";
                        DamageTextPool.Instance.SpawnText(player.transform.position + Vector3.up, isTR ? "+1 Ekstra Can!" : "+1 Extra Heart!", Color.red);
                    }
                }
                else
                {
                    if (DamageTextPool.Instance != null)
                    {
                        bool isTR = PlayerPrefs.GetString("GameLanguage", "Turkish") == "Turkish";
                        DamageTextPool.Instance.SpawnText(player.transform.position + Vector3.up, isTR ? "Maksimum Ekstra Can (3)!" : "Max Reserve Hearts (3)!", Color.red);
                    }
                }
                return;
            }

            if (item.itemName == "Health Potion (Can Potu)")
            {
                if (pc.currentHP < pc.maxHP)
                {
                    pc.Heal(33.3f);

                    if (item.count > 1) item.count--;
                    else inventoryItems.RemoveAt(fromIndex);

                    if (AudioManager.Instance != null)
                    {
                        AudioManager.Instance.PlaySFX(SoundEffect.CoinPickup);
                    }

                    if (UIManager.Instance != null)
                    {
                        UIManager.Instance.UpdateInventoryUI();
                    }
                    SyncHUDPotions();
                }
                else
                {
                    if (DamageTextPool.Instance != null)
                    {
                        bool isTR = PlayerPrefs.GetString("GameLanguage", "Turkish") == "Turkish";
                        DamageTextPool.Instance.SpawnText(player.transform.position + Vector3.up, isTR ? "Can Dolu!" : "Health Maxed!", Color.green);
                    }
                }
            }
        }

        public void EquipItemByName(EquipSlot slot, string itemName)
        {
            if (string.IsNullOrEmpty(itemName))
            {
                equippedItems[slot] = null;
            }
            else
            {
                ItemData item = itemDatabase.Find(x => x.itemName == itemName);
                if (item != null)
                {
                    equippedItems[slot] = item;
                    if (slot == EquipSlot.ThrowingKnife)
                    {
                        GameObject playerObj = GameObject.FindWithTag("Player");
                        if (playerObj != null)
                        {
                            PlayerController pc = playerObj.GetComponent<PlayerController>();
                            if (pc != null) pc.knifeAmmo = 10;
                        }
                    }
                }
                else
                {
                    equippedItems[slot] = null;
                }
            }
            UpdateVisualEquipment();
        }

        public int GetTotalPotionCount()
        {
            int total = 0;
            foreach (var item in inventoryItems)
            {
                if (item != null && item.itemName == "Health Potion (Can Potu)")
                {
                    total += item.count;
                }
            }
            return total;
        }

        public void SyncHUDPotions()
        {
            if (UIManager.Instance != null)
            {
                int potCount = GetTotalPotionCount();
                UIManager.Instance.UpdatePotionsUI(potCount);
            }
        }
    }
}
