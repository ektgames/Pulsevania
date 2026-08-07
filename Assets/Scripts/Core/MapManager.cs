using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Pulsevania.Core
{
    public enum RoomState
    {
        Locked,
        Discovered,
        Cleared
    }

    public enum RoomStyle
    {
        Cave,
        HighHills,
        DeepUnderground
    }

    [System.Serializable]
    public class RoomData
    {
        public int roomId;
        public RoomState state = RoomState.Locked;
        public bool enemiesSpawned = true;
        public bool exitDoorUnlocked = false;
    }

    public enum CellType
    {
        Solid,
        Empty,
        Ladder,
        Water,
        Lava,
        Spikes,
        KeyChest,
        YellowChest,
        BlueChest,
        KeyGuardian,
        Patroller,
        BreakableWall,
        Boss,
        Princess
    }

    public class MapManager : MonoBehaviour
    {
        private static MapManager instance;
        private struct Chamber
        {
            public int cx, cy, rx, ry;
            public RoomStyle style;
        }
        public static MapManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindFirstObjectByType<MapManager>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("MapManager");
                        instance = go.AddComponent<MapManager>();
                    }
                }
                return instance;
            }
        }

        public List<RoomData> rooms = new List<RoomData>();
        
        // Logical grid configurations
        public float roomWidth = 16f;
        public float roomHeight = 8f;
        public float originX = -80f;
        public float originY = -20f;

        private CellType[,] currentGrid;
        private int currentGridWidth;
        private int currentGridHeight;
        private RoomStyle[,] currentCellStyles;

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeRooms();
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }

        private struct LadderRange
        {
            public int x;
            public float minY;
            public float maxY;
        }
        private List<LadderRange> activeLadders = new List<LadderRange>();

        private List<GameObject> activeRoomEntities = new List<GameObject>();
        private int lastActiveRoomId = -1;
        
        // Material caching to make level transition instantaneous and fix the black screen delay
        private System.Collections.Generic.Dictionary<string, Material> cachedMaterials = new System.Collections.Generic.Dictionary<string, Material>();

        private Material GetCachedMaterial(string key, System.Func<Texture2D> textureGenerator)
        {
            if (cachedMaterials.TryGetValue(key, out Material mat) && mat != null)
            {
                return mat;
            }
            Material newMat = new Material(Shader.Find("Sprites/Default"));
            // Use sharedMaterial assignment for automatic draw call batching, no need for custom instancing
            Texture2D tex = textureGenerator();
            newMat.mainTexture = tex;
            cachedMaterials[key] = newMat;
            return newMat;
        }

        private Mesh quadMesh;
        private Mesh GetQuadMesh()
        {
            if (quadMesh == null)
            {
                GameObject tempQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                MeshFilter mf = tempQuad.GetComponent<MeshFilter>();
                if (mf != null) quadMesh = mf.sharedMesh;
                DestroyImmediate(tempQuad);
            }
            return quadMesh;
        }

        public void CombineLevelGrid()
        {
            GameObject levelGridGo = GameObject.Find("LevelGrid");
            if (levelGridGo == null) return;

            MeshFilter[] meshFilters = levelGridGo.GetComponentsInChildren<MeshFilter>();
            System.Collections.Generic.Dictionary<Material, System.Collections.Generic.List<CombineInstance>> combineDict = 
                new System.Collections.Generic.Dictionary<Material, System.Collections.Generic.List<CombineInstance>>();

            foreach (MeshFilter mf in meshFilters)
            {
                if (mf.gameObject == levelGridGo) continue;

                MeshRenderer mr = mf.GetComponent<MeshRenderer>();
                if (mr == null || mr.sharedMaterial == null) continue;

                if (mf.gameObject.name.Contains("Door") || mf.gameObject.name.Contains("Chest") || mf.gameObject.name.Contains("NPC") || mf.gameObject.name.Contains("Portal") || mf.gameObject.name.Contains("BreakableWall"))
                    continue;

                if (!combineDict.ContainsKey(mr.sharedMaterial))
                {
                    combineDict[mr.sharedMaterial] = new System.Collections.Generic.List<CombineInstance>();
                }

                CombineInstance ci = new CombineInstance();
                ci.mesh = mf.sharedMesh;
                ci.transform = levelGridGo.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                combineDict[mr.sharedMaterial].Add(ci);
            }

            int groupIndex = 0;
            foreach (var kvp in combineDict)
            {
                Material mat = kvp.Key;
                var combines = kvp.Value;

                GameObject combinedGo = new GameObject("CombinedGrid_" + groupIndex);
                combinedGo.transform.SetParent(levelGridGo.transform, false);

                MeshFilter combinedMf = combinedGo.AddComponent<MeshFilter>();
                MeshRenderer combinedMr = combinedGo.AddComponent<MeshRenderer>();
                combinedMr.sharedMaterial = mat;

                Mesh combinedMesh = new Mesh();
                combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                combinedMesh.CombineMeshes(combines.ToArray(), true, true);
                combinedMf.sharedMesh = combinedMesh;

                groupIndex++;
            }

            foreach (MeshFilter mf in meshFilters)
            {
                if (mf.gameObject == levelGridGo || mf.gameObject.name.Contains("CombinedGrid_")) continue;
                if (mf.gameObject.name.Contains("Door") || mf.gameObject.name.Contains("Chest") || mf.gameObject.name.Contains("NPC") || mf.gameObject.name.Contains("Portal") || mf.gameObject.name.Contains("BreakableWall"))
                    continue;

                if (mf.gameObject.GetComponent<BoxCollider2D>() != null)
                {
                    MeshRenderer mr = mf.gameObject.GetComponent<MeshRenderer>();
                    if (mr != null) Destroy(mr);
                    Destroy(mf);
                }
                else
                {
                    Destroy(mf.gameObject);
                }
            }

            CompositeCollider2D compCol = levelGridGo.GetComponent<CompositeCollider2D>();
            if (compCol != null)
            {
                compCol.GenerateGeometry();
            }
        }

        public void InitializeRooms()
        {
            rooms.Clear();
            for (int i = 0; i < 50; i++)
            {
                rooms.Add(new RoomData { roomId = i + 1, state = RoomState.Locked, enemiesSpawned = true });
            }
            rooms[0].state = RoomState.Discovered;
        }

        private void Update()
        {
            if (lastActiveRoomId > 0)
            {
                DiscoverRoom(lastActiveRoomId);
            }
        }

        public float GetRoomWidthForLevel(int roomId)
        {
            // Every single level gets longer as the player progresses (from 100 units at Level 1 to 247 units at Level 50)
            return 97f + roomId * 3f;
        }

        public RoomStyle GetRoomStyle(int roomId)
        {
            if (roomId == 10 || roomId == 20 || roomId == 30 || roomId == 40 || roomId == 50)
                return RoomStyle.DeepUnderground;
            if (roomId == 1 || roomId == 4 || roomId == 7)
                return RoomStyle.DeepUnderground;
            if (roomId == 16)
                return RoomStyle.DeepUnderground;
            
            int pattern = roomId % 3;
            if (pattern == 0) return RoomStyle.Cave;
            if (pattern == 1) return RoomStyle.HighHills;
            return RoomStyle.DeepUnderground;
        }

        public RoomStyle GetCellStyle(Vector3 pos)
        {
            if (currentCellStyles == null) return RoomStyle.DeepUnderground;
            
            int index = lastActiveRoomId - 1;
            float width = GetRoomWidthForLevel(lastActiveRoomId);
            float startX = originX + (index % 10) * width;
            float roomY = originY + (4 - (index / 10)) * roomHeight + roomHeight / 2f - 2f;

            int lenX = currentCellStyles.GetLength(0);
            int lenY = currentCellStyles.GetLength(1);

            int gx = Mathf.Clamp(Mathf.RoundToInt(pos.x - startX), 0, lenX - 1);
            int gy = Mathf.Clamp(Mathf.RoundToInt(pos.y - (roomY - 5f)), 0, lenY - 1);
            
            return currentCellStyles[gx, gy];
        }

        public float GetRoomWidthForRow(int row)
        {
            int levelId = row * 10 + 5;
            return GetRoomWidthForLevel(levelId);
        }

        private float GetActualFloorY(float posX, float startX, float roomY, int widthInt, int height, CellType[,] grid, int startCy = 15)
        {
            int gridX = Mathf.Clamp(Mathf.RoundToInt(posX - startX), 0, widthInt - 1);
            int floorY = 1;
            
            // Bypass the solid roof/ceiling of the map from height - 2 downwards to find empty cave space
            int searchY = height - 2;
            while (searchY >= 1 && grid[gridX, searchY] == CellType.Solid)
            {
                searchY--;
            }
            
            // Now searchY is in empty space (or we reached 1). Search downwards to find the actual floor.
            for (int y = searchY; y >= 1; y--)
            {
                if (grid[gridX, y] == CellType.Solid)
                {
                    floorY = y + 1;
                    break;
                }
            }
            return roomY - 5f + floorY;
        }

        private void SpawnThematicBackgrounds(int roomId, float centerX, float roomY, float width, HashSet<int> poolCoords, CellType[,] grid)
        {
            int biome = (roomId - 1) / 10;
            if (biome < 0) biome = 0;
            if (biome > 4) biome = 4;

            int index = roomId - 1;
            int row = index / 10;
            int col = index % 10;
            float startX = originX + col * width;
            float endX = startX + width;
            int widthInt = Mathf.FloorToInt(width);
            int height = 30; // Matches grid height of 30

            // 1. SKY GRADIENT BACKDROP
            GameObject bgBackdrop = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bgBackdrop.name = $"BG_Backdrop_Biome_{biome}";
            bgBackdrop.transform.position = new Vector3(centerX, roomY + 12f, 5.0f); // Positioned higher
            bgBackdrop.transform.localScale = new Vector3(width + 8f, 55f, 1f); // Increased vertical height to cover high climbs

            var col1 = bgBackdrop.GetComponent<Collider>();
            if (col1 != null) DestroyImmediate(col1);

            var renderer1 = bgBackdrop.GetComponent<MeshRenderer>();
            if (renderer1 != null)
            {
                renderer1.material = new Material(Shader.Find("Sprites/Default"));
                
                RoomStyle style = GetRoomStyle(roomId);
                Color topCol = Color.cyan;
                Color botCol = Color.white;

                if (style == RoomStyle.Cave)
                {
                    if (biome == 4) // Magma Keep Cave
                    {
                        topCol = new Color(0.04f, 0.01f, 0.01f);
                        botCol = new Color(0.18f, 0.04f, 0.01f);
                    }
                    else if (biome == 2) // Frozen Cave
                    {
                        topCol = new Color(0.02f, 0.05f, 0.1f);
                        botCol = new Color(0.08f, 0.15f, 0.28f);
                    }
                    else
                    {
                        topCol = new Color(0.02f, 0.02f, 0.05f);
                        botCol = new Color(0.08f, 0.08f, 0.12f);
                    }
                }
                else if (style == RoomStyle.HighHills)
                {
                    if (biome == 2) // Frozen Hills
                    {
                        topCol = new Color(0.45f, 0.65f, 0.85f);
                        botCol = new Color(0.95f, 0.95f, 1.0f);
                    }
                    else if (biome == 3) // Void Cellar
                    {
                        topCol = new Color(0.02f, 0.01f, 0.04f);
                        botCol = new Color(0.12f, 0.02f, 0.18f);
                    }
                    else if (biome == 4) // Magma Hills
                    {
                        topCol = new Color(0.18f, 0.08f, 0.1f);
                        botCol = new Color(0.48f, 0.18f, 0.1f);
                    }
                    else
                    {
                        topCol = new Color(0.35f, 0.65f, 0.95f);
                        botCol = new Color(0.85f, 0.85f, 0.9f);
                    }
                }
                else // DeepUnderground / Castle
                {
                    if (biome == 3) // Void Dungeon
                    {
                        topCol = new Color(0.02f, 0.01f, 0.04f);
                        botCol = new Color(0.12f, 0.02f, 0.18f);
                    }
                    else if (biome == 4) // Lava Keep Dungeon
                    {
                        topCol = new Color(0.05f, 0.02f, 0.02f);
                        botCol = new Color(0.22f, 0.05f, 0.02f);
                    }
                    else
                    {
                        topCol = new Color(0.01f, 0.01f, 0.02f);
                        botCol = new Color(0.12f, 0.08f, 0.15f);
                    }
                }

                if (roomId == 13)
                {
                    topCol = new Color(0.01f, 0.01f, 0.03f); // Çok koyu gotik lacivert/siyah
                    botCol = new Color(0.08f, 0.05f, 0.12f); // Koyu mor/lacivert tapınak loşluğu
                }

                if (roomId == 19 || roomId == 22 || roomId == 25)
                {
                    topCol = new Color(0.01f, 0.01f, 0.02f); // Koyu gotik lacivert/siyah
                    botCol = new Color(0.08f, 0.05f, 0.12f); // Koyu mor/lacivert tapınak loşluğu
                }

                renderer1.material.mainTexture = CreateGradientTexture(botCol, topCol);
            }
            activeRoomEntities.Add(bgBackdrop);

            // 2. LAYERED DISTANT SILHOUETTES / MOUNTAINS
            int distCount = Mathf.FloorToInt(width / 35f);
            if (distCount < 3) distCount = 3;
            for (int i = 0; i < distCount; i++)
            {
                float mountainX = startX + 15f + i * (width - 30f) / (distCount - 1);
                
                GameObject mountGo = new GameObject($"BG_Mountain_{biome}_{i}");
                mountGo.transform.position = new Vector3(mountainX, roomY - 4.0f, 4.5f);
                mountGo.transform.localScale = new Vector3(2.5f, 2.5f, 1f);

                var sr = mountGo.AddComponent<SpriteRenderer>();
                sr.sortingOrder = 1;

                if (biome == 1) // Temple pyramids
                {
                    sr.sprite = CreateTempleSpiresSprite(24, 24);
                }
                else
                {
                    sr.sprite = CreateMountainSprite(biome, 32, 24);
                }

                activeRoomEntities.Add(mountGo);
            }

            // 3. LAYERED BACKGROUND FOREGROUND PROPS
            Random.InitState(roomId * 76543 + 1234);

            if (biome == 0) // Mossy Forest: Trees and Bushes
            {
                int treeCount = Mathf.FloorToInt(width / 16f);
                for (int i = 0; i < treeCount; i++)
                {
                    float tx = startX + 8f + i * (width - 16f) / (treeCount - 1) + Random.Range(-2f, 2f);
                    
                    int checkX = Mathf.RoundToInt(tx);
                    if (poolCoords != null && poolCoords.Contains(checkX))
                    {
                        int left = checkX;
                        int right = checkX;
                        while (left > startX && poolCoords.Contains(left)) left--;
                        while (right < endX && poolCoords.Contains(right)) right++;

                        if (checkX - left <= right - checkX && left > startX) tx = left;
                        else if (right < endX) tx = right;
                    }

                    GameObject treeGo = new GameObject("BG_Tree");
                    float actualTreeY = GetActualFloorY(tx, startX, roomY, widthInt, height, grid) - 0.5f;
                    treeGo.transform.position = new Vector3(tx, actualTreeY, 3.8f);
                    treeGo.transform.localScale = new Vector3(1.5f + Random.Range(0f, 0.5f), 1.5f + Random.Range(0f, 0.5f), 1f);

                    var sr = treeGo.AddComponent<SpriteRenderer>();
                    sr.sortingOrder = 2;
                    sr.sprite = CreateTreeSprite((Random.value > 0.5f));

                    activeRoomEntities.Add(treeGo);
                }
            }
            else if (biome == 1) // Ancient Temple: Columns
            {
                int pillarCount = Mathf.FloorToInt(width / 20f);
                for (int i = 0; i < pillarCount; i++)
                {
                    float px = startX + 10f + i * (width - 20f) / (pillarCount - 1);
                    
                    GameObject pillarGo = new GameObject("BG_Pillar");
                    float actualPillarFloorY = GetActualFloorY(px, startX, roomY, widthInt, height, grid) - 0.5f;
                    pillarGo.transform.position = new Vector3(px, actualPillarFloorY + 3.0f, 3.8f);
                    pillarGo.transform.localScale = new Vector3(1.8f, 1.5f, 1f);

                    var sr = pillarGo.AddComponent<SpriteRenderer>();
                    sr.sortingOrder = 2;

                    Texture2D pTex = new Texture2D(16, 64);
                    Color sand = new Color(0.6f, 0.5f, 0.38f);
                    Color shade = new Color(0.48f, 0.38f, 0.28f);
                    for (int x = 0; x < 16; x++)
                    {
                        for (int y = 0; y < 64; y++)
                        {
                            bool isBorder = (x == 0 || x == 15 || x == 1 || x == 14);
                            bool isJoint = (y % 8 == 0 || y % 8 == 1);
                            if (isBorder || isJoint) pTex.SetPixel(x, y, shade);
                            else pTex.SetPixel(x, y, sand);
                        }
                    }
                    pTex.filterMode = FilterMode.Point;
                    pTex.Apply();
                    sr.sprite = Sprite.Create(pTex, new Rect(0, 0, 16, 64), new Vector2(0.5f, 0.5f), 16f);

                    activeRoomEntities.Add(pillarGo);
                }
            }
            else if (biome == 2) // Frozen Cavern: Snowy trees & ice waterfalls
            {
                int treeCount = Mathf.FloorToInt(width / 22f);
                for (int i = 0; i < treeCount; i++)
                {
                    float tx = startX + 12f + i * (width - 24f) / (treeCount - 1) + Random.Range(-3f, 3f);

                    int checkX = Mathf.RoundToInt(tx);
                    if (poolCoords != null && poolCoords.Contains(checkX))
                    {
                        int left = checkX;
                        int right = checkX;
                        while (left > startX && poolCoords.Contains(left)) left--;
                        while (right < endX && poolCoords.Contains(right)) right++;

                        if (checkX - left <= right - checkX && left > startX) tx = left;
                        else if (right < endX) tx = right;
                    }

                    GameObject treeGo = new GameObject("BG_SnowyTree");
                    float actualTreeY = GetActualFloorY(tx, startX, roomY, widthInt, height, grid) - 0.5f;
                    treeGo.transform.position = new Vector3(tx, actualTreeY, 3.8f);
                    treeGo.transform.localScale = new Vector3(1.6f, 1.6f, 1f);

                    var sr = treeGo.AddComponent<SpriteRenderer>();
                    sr.sortingOrder = 2;
                    sr.sprite = CreateTreeSprite(true, true);

                    activeRoomEntities.Add(treeGo);
                }

                // Ice Waterfalls
                for (int i = 0; i < 2; i++)
                {
                    float wx = startX + width * (0.25f + i * 0.5f) + Random.Range(-5f, 5f);
                    GameObject wGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    wGo.name = "BG_IceWaterfall";
                    wGo.transform.position = new Vector3(wx, roomY + 4f, 3.9f);
                    wGo.transform.localScale = new Vector3(3f, 18f, 1f);

                    DestroyImmediate(wGo.GetComponent<Collider>());
                    var mr = wGo.GetComponent<MeshRenderer>();
                    if (mr != null)
                    {
                        mr.material = new Material(Shader.Find("Sprites/Default"));
                        mr.material.mainTexture = CreateWaterfallTexture(false);
                        var anim = wGo.AddComponent<BackgroundAnimator>();
                        anim.type = BackgroundAnimator.AnimationType.ScrollVertical;
                        anim.speed = -0.6f;
                    }
                    activeRoomEntities.Add(wGo);
                }
            }
            else if (biome == 3) // Void Cellar: Swirling portal and hanging chains
            {
                GameObject portalGo = new GameObject("BG_VoidPortal");
                portalGo.transform.position = new Vector3(centerX, roomY + 4f, 4.0f);
                portalGo.transform.localScale = new Vector3(12f, 12f, 1f);
                var sr = portalGo.AddComponent<SpriteRenderer>();
                sr.sortingOrder = 2;
                sr.sprite = CreateVoidPortalSprite();
                
                var anim = portalGo.AddComponent<BackgroundAnimator>();
                anim.type = BackgroundAnimator.AnimationType.Rotate;
                anim.speed = 15f;

                activeRoomEntities.Add(portalGo);

                int chainCount = Mathf.FloorToInt(width / 18f);
                for (int i = 0; i < chainCount; i++)
                {
                    float cx = startX + 9f + i * (width - 18f) / (chainCount - 1);
                    GameObject chainGo = new GameObject("BG_VoidChain");
                    chainGo.transform.position = new Vector3(cx, roomY + 10f, 3.8f);
                    chainGo.transform.localScale = new Vector3(0.5f, 8f, 1f);

                    var chainSr = chainGo.AddComponent<SpriteRenderer>();
                    chainSr.sortingOrder = 2;

                    Texture2D cTex = new Texture2D(8, 32);
                    Color iron = new Color(0.3f, 0.3f, 0.35f);
                    for (int y = 0; y < 32; y++)
                    {
                        for (int x = 0; x < 8; x++)
                        {
                            bool isLink = (x == 1 || x == 6 || y % 8 == 0 || y % 8 == 4);
                            cTex.SetPixel(x, y, isLink ? iron : Color.clear);
                        }
                    }
                    cTex.filterMode = FilterMode.Point;
                    cTex.Apply();
                    chainSr.sprite = Sprite.Create(cTex, new Rect(0, 0, 8, 32), new Vector2(0.5f, 1f), 16f);

                    activeRoomEntities.Add(chainGo);
                }
            }
            else if (biome == 4) // Lava Cave: Magma pillars and Lava waterfalls
            {
                int lavCount = 3;
                for (int i = 0; i < lavCount; i++)
                {
                    float wx = startX + 20f + i * (width - 40f) / (lavCount - 1);
                    GameObject wGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    wGo.name = "BG_LavaWaterfall";
                    wGo.transform.position = new Vector3(wx, roomY + 4f, 3.9f);
                    wGo.transform.localScale = new Vector3(4f, 18f, 1f);

                    DestroyImmediate(wGo.GetComponent<Collider>());
                    var mr = wGo.GetComponent<MeshRenderer>();
                    if (mr != null)
                    {
                        mr.material = new Material(Shader.Find("Sprites/Default"));
                        mr.material.mainTexture = CreateWaterfallTexture(true);
                        var anim = wGo.AddComponent<BackgroundAnimator>();
                        anim.type = BackgroundAnimator.AnimationType.ScrollVertical;
                        anim.speed = -1.2f;
                    }
                    activeRoomEntities.Add(wGo);
                }

                int pillarCount = Mathf.FloorToInt(width / 24f);
                for (int i = 0; i < pillarCount; i++)
                {
                    float px = startX + 12f + i * (width - 24f) / (pillarCount - 1);
                    GameObject pillarGo = new GameObject("BG_MagmaPillar");
                    float actualPillarFloorY = GetActualFloorY(px, startX, roomY, widthInt, height, grid) - 0.5f;
                    pillarGo.transform.position = new Vector3(px, actualPillarFloorY, 3.8f);
                    pillarGo.transform.localScale = new Vector3(2.5f, 7f, 1f);

                    var sr = pillarGo.AddComponent<SpriteRenderer>();
                    sr.sortingOrder = 2;

                    Texture2D pTex = new Texture2D(16, 32);
                    Color obs = new Color(0.08f, 0.08f, 0.09f);
                    Color lava = new Color(1f, 0.3f, 0f);
                    for (int x = 0; x < 16; x++)
                    {
                        for (int y = 0; y < 32; y++)
                        {
                            bool isLavaCrack = ((x + y) % 6 == 0);
                            pTex.SetPixel(x, y, isLavaCrack ? lava : obs);
                        }
                    }
                    pTex.filterMode = FilterMode.Point;
                    pTex.Apply();
                    sr.sprite = Sprite.Create(pTex, new Rect(0, 0, 16, 32), new Vector2(0.5f, 0f), 16f);

                    activeRoomEntities.Add(pillarGo);
                }
            }

            // Spawn clouds and eagles for HighHills style
            if (GetRoomStyle(roomId) == RoomStyle.HighHills)
            {
                int numClouds = Random.Range(3, 6);
                for (int i = 0; i < numClouds; i++)
                {
                    float cx = startX + Random.Range(5f, width - 5f);
                    float cy = roomY + Random.Range(3f, 10f);
                    SpawnMovingCloud(new Vector3(cx, cy, 4.2f), startX - 10f, endX + 10f);
                }

                int numEagles = Random.Range(1, 3);
                for (int i = 0; i < numEagles; i++)
                {
                    float ex = startX + Random.Range(5f, width - 5f);
                    float ey = roomY + Random.Range(4f, 11f);
                    SpawnBackgroundEagle(new Vector3(ex, ey, 4.1f), startX - 15f, endX + 15f);
                }
            }
        }

        private Texture2D CreateGradientTexture(Color bottomColor, Color topColor)
        {
            Texture2D tex = new Texture2D(1, 16);
            for (int y = 0; y < 16; y++)
            {
                float t = y / 15f;
                tex.SetPixel(0, y, Color.Lerp(bottomColor, topColor, t));
            }
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.Apply();
            return tex;
        }

        private Sprite CreateMountainSprite(int biome, int w, int h)
        {
            Texture2D tex = new Texture2D(w, h);
            Color mountainColor = Color.grey;
            if (biome == 0) mountainColor = new Color(0.2f, 0.45f, 0.25f, 0.4f);
            else if (biome == 1) mountainColor = new Color(0.35f, 0.25f, 0.18f, 0.5f);
            else if (biome == 2) mountainColor = new Color(0.18f, 0.3f, 0.45f, 0.5f);
            else if (biome == 3) mountainColor = new Color(0.15f, 0.05f, 0.22f, 0.5f);
            else if (biome == 4) mountainColor = new Color(0.25f, 0.05f, 0.05f, 0.6f);

            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    float distFromCenter = Mathf.Abs(x - w / 2f);
                    float heightLimit = h - 1 - distFromCenter * (2f * h / w);
                    if (y <= heightLimit)
                    {
                        float shade = 0.6f + 0.4f * (y / (float)h);
                        tex.SetPixel(x, y, mountainColor * shade);
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            tex.filterMode = FilterMode.Point;
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 16f);
        }

        private Sprite CreateTempleSpiresSprite(int w, int h)
        {
            Texture2D tex = new Texture2D(w, h);
            Color c = new Color(0.35f, 0.28f, 0.22f, 0.5f);
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    bool isSpire = (x >= 4 && x <= 11 && y <= h - 4) || (x == 7 || x == 8);
                    tex.SetPixel(x, y, isSpire ? c : Color.clear);
                }
            }
            tex.filterMode = FilterMode.Point;
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 16f);
        }

        private Sprite CreateTreeSprite(bool isPine, bool isSnowy = false)
        {
            int w = 16;
            int h = 32;
            Texture2D tex = new Texture2D(w, h);
            Color trunk = new Color(0.4f, 0.25f, 0.15f);
            Color leaf = isSnowy ? Color.white : new Color(0.12f, 0.45f, 0.18f);
            Color darkLeaf = isSnowy ? new Color(0.7f, 0.8f, 0.85f) : new Color(0.08f, 0.32f, 0.12f);

            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    tex.SetPixel(x, y, Color.clear);

            for (int y = 0; y < 18; y++)
            {
                for (int x = 7; x <= 8; x++)
                    tex.SetPixel(x, y, trunk);
            }

            if (isPine)
            {
                for (int ly = 10; ly < 32; ly++)
                {
                    int layer = (ly - 10) / 7;
                    int maxW = 7 - layer * 2 - (ly % 7) / 2;
                    if (maxW < 1) maxW = 1;
                    for (int x = 8 - maxW; x <= 8 + maxW; x++)
                    {
                        Color c = (ly % 2 == 0) ? leaf : darkLeaf;
                        tex.SetPixel(x, ly, c);
                    }
                }
            }
            else
            {
                for (int ly = 8; ly < 32; ly++)
                {
                    float dy = ly - 20f;
                    for (int lx = 1; lx < 15; lx++)
                    {
                        float dx = lx - 7.5f;
                        float dist = Mathf.Sqrt(dx * dx * 1.8f + dy * dy * 0.8f);
                        if (dist <= 8f)
                        {
                            Color c = (dist > 5.5f) ? darkLeaf : leaf;
                            tex.SetPixel(lx, ly, c);
                        }
                    }
                }
            }

            tex.filterMode = FilterMode.Point;
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 16f);
        }

        private Texture2D CreateWaterfallTexture(bool isLava)
        {
            int w = 8;
            int h = 32;
            Texture2D tex = new Texture2D(w, h);
            Color c1 = isLava ? new Color(1f, 0.35f, 0f) : new Color(0f, 0.45f, 0.9f, 0.75f);
            Color c2 = isLava ? new Color(1f, 0.8f, 0f) : new Color(0.6f, 0.85f, 1f, 0.85f);
            Color foam = isLava ? new Color(0.5f, 0.05f, 0f) : new Color(1f, 1f, 1f, 0.9f);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int wave = Mathf.FloorToInt(Mathf.Sin(y * 0.5f + x * 0.8f) * 1.5f + 4f);
                    Color pixelColor = (x == wave) ? foam : ((x + y) % 3 == 0 ? c2 : c1);
                    tex.SetPixel(x, y, pixelColor);
                }
            }
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Point;
            tex.Apply();
            return tex;
        }

        private Sprite CreateVoidPortalSprite()
        {
            int size = 32;
            Texture2D tex = new Texture2D(size, size);
            Color p1 = new Color(0.9f, 0.1f, 0.8f, 0.9f);
            Color p2 = new Color(0.4f, 0.05f, 0.6f, 0.7f);
            Color core = Color.white;

            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    float dx = x - 15.5f;
                    float dy = y - 15.5f;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float angle = Mathf.Atan2(dy, dx);

                    if (dist <= 3.5f)
                    {
                        tex.SetPixel(x, y, core);
                    }
                    else if (dist <= 14f)
                    {
                        float spiral = Mathf.Sin(dist * 0.5f - angle * 2f);
                        Color c = (spiral > 0f) ? p1 : p2;
                        float alpha = 1f - (dist / 14f);
                        tex.SetPixel(x, y, new Color(c.r, c.g, c.b, c.a * alpha));
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            tex.filterMode = FilterMode.Point;
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 16f);
        }

        private bool IsSpaceAboveEmpty(int x, int y, CellType[,] grid, int height)
        {
            // Ensure at least 3 blocks of vertical empty space above the chest
            for (int dy = 1; dy <= 3; dy++)
            {
                if (y + dy >= height || grid[x, y + dy] != CellType.Empty)
                {
                    return false;
                }
            }
            return true;
        }

        private void SpawnLadderToChest(int cx, int cy, CellType[,] grid)
        {
            // Search down from cy - 1 to find the local solid floor of the chamber
            int y = cy - 1;
            while (y > 1)
            {
                if (grid[cx, y] == CellType.Solid)
                {
                    break; // Met local floor
                }
                grid[cx, y] = CellType.Ladder;
                // Clear left and right cells to make climbing comfortable
                if (cx > 1) grid[cx - 1, y] = CellType.Empty;
                if (cx < currentGridWidth - 2) grid[cx + 1, y] = CellType.Empty;
                y--;
            }
        }

        private Vector2Int FindValidFloorPos(Chamber ch, Chamber poolCh, CellType[,] grid, int widthInt, int height)
        {
            int poolLeft = poolCh.cx - poolCh.rx + 2;
            int poolRight = poolCh.cx + poolCh.rx - 2;
            bool isPoolCh = (ch.cx == poolCh.cx && ch.cy == poolCh.cy && poolCh.rx > 0);

            // Limit search to the bottom 3 rows of the chamber to guarantee placing on the actual cavern floor
            int minY = ch.cy - ch.ry;
            int maxY = ch.cy - ch.ry + 2;

            // Tier 1: Perfect flat spot (solid under left/right, empty left/right, 2-thick solid foundation)
            for (int y = minY; y <= maxY; y++)
            {
                if (y <= 2 || y >= height - 1) continue;
                
                for (int dx = 3; dx <= ch.rx; dx++)
                {
                    int[] xOffsets = { dx, -dx };
                    foreach (int ox in xOffsets)
                    {
                        int x = ch.cx + ox;
                        if (x > 2 && x < widthInt - 3)
                        {
                            if (isPoolCh && x >= poolLeft && x <= poolRight) continue;
                            if (lastActiveRoomId == 15 && x >= 24 && x <= 48) continue;

                            if (grid[x, y] == CellType.Empty && grid[x, y - 1] == CellType.Solid &&
                                grid[x, y - 2] == CellType.Solid && grid[x - 1, y - 2] == CellType.Solid && grid[x + 1, y - 2] == CellType.Solid)
                            {
                                if (grid[x - 1, y - 1] == CellType.Solid && grid[x + 1, y - 1] == CellType.Solid &&
                                    grid[x - 1, y] == CellType.Empty && grid[x + 1, y] == CellType.Empty)
                                {
                                    if (grid[x, y] != CellType.YellowChest && 
                                        grid[x, y] != CellType.BlueChest && 
                                        grid[x, y] != CellType.KeyChest && 
                                        grid[x, y] != CellType.Princess &&
                                        IsSpaceAboveEmpty(x, y, grid, height))
                                    {
                                        return new Vector2Int(x, y);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Tier 2: Relaxed flat spot (solid under left/right, but allowing wall on one side)
            for (int y = minY; y <= maxY; y++)
            {
                if (y <= 1 || y >= height - 1) continue;
                
                for (int dx = 3; dx <= ch.rx; dx++)
                {
                    int[] xOffsets = { dx, -dx };
                    foreach (int ox in xOffsets)
                    {
                        int x = ch.cx + ox;
                        if (x > 1 && x < widthInt - 2)
                        {
                            if (isPoolCh && x >= poolLeft && x <= poolRight) continue;
                            if (lastActiveRoomId == 15 && x >= 24 && x <= 48) continue;

                            if (grid[x, y] == CellType.Empty && grid[x, y - 1] == CellType.Solid)
                            {
                                if (grid[x - 1, y - 1] == CellType.Solid && grid[x + 1, y - 1] == CellType.Solid)
                                {
                                    if (grid[x, y] != CellType.YellowChest && 
                                        grid[x, y] != CellType.BlueChest && 
                                        grid[x, y] != CellType.KeyChest && 
                                        grid[x, y] != CellType.Princess &&
                                        IsSpaceAboveEmpty(x, y, grid, height))
                                    {
                                        return new Vector2Int(x, y);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Tier 3: Any solid ground under
            for (int y = minY; y <= maxY; y++)
            {
                if (y <= 1 || y >= height - 1) continue;
                
                for (int dx = 3; dx <= ch.rx; dx++)
                {
                    int[] xOffsets = { dx, -dx };
                    foreach (int ox in xOffsets)
                    {
                        int x = ch.cx + ox;
                        if (x > 1 && x < widthInt - 2)
                        {
                            if (isPoolCh && x >= poolLeft && x <= poolRight) continue;
                            if (lastActiveRoomId == 15 && x >= 24 && x <= 48) continue;

                            if (grid[x, y] == CellType.Empty && grid[x, y - 1] == CellType.Solid)
                            {
                                if (grid[x, y] != CellType.YellowChest && 
                                    grid[x, y] != CellType.BlueChest && 
                                    grid[x, y] != CellType.KeyChest && 
                                    grid[x, y] != CellType.Princess &&
                                    IsSpaceAboveEmpty(x, y, grid, height))
                                {
                                    return new Vector2Int(x, y);
                                }
                            }
                        }
                    }
                }
            }


            int fallbackX = isPoolCh ? (ch.cx - ch.rx + 1) : ch.cx;
            int fallbackY = ch.cy;
            while (fallbackY > 1 && grid[fallbackX, fallbackY - 1] == CellType.Empty) fallbackY--;
            return new Vector2Int(fallbackX, fallbackY);
        }

        private Vector2Int FindValidGuardianPos(Chamber ch, Chamber poolCh, Vector2Int keyPos, CellType[,] grid, int widthInt, int height)
        {
            int poolLeft = poolCh.cx - poolCh.rx + 2;
            int poolRight = poolCh.cx + poolCh.rx - 2;
            bool isPoolCh = (ch.cx == poolCh.cx && ch.cy == poolCh.cy && poolCh.rx > 0);

            // Tier 1: Perfect flat spot
            for (int dy = -ch.ry; dy <= ch.ry; dy++)
            {
                int y = ch.cy + dy;
                if (y <= 1 || y >= height - 1) continue;
                
                for (int dx = 0; dx <= ch.rx; dx++)
                {
                    int[] xOffsets = { -dx, dx };
                    foreach (int ox in xOffsets)
                    {
                        int x = ch.cx + ox;
                        if (x > 2 && x < widthInt - 3 && Mathf.Abs(x - keyPos.x) >= 3)
                        {
                            if (isPoolCh && x >= poolLeft && x <= poolRight) continue;
                            if (lastActiveRoomId == 15 && x >= 24 && x <= 48) continue;

                            if (grid[x, y] == CellType.Empty && grid[x, y - 1] == CellType.Solid)
                            {
                                if (grid[x - 1, y - 1] == CellType.Solid && grid[x + 1, y - 1] == CellType.Solid &&
                                    grid[x - 1, y] == CellType.Empty && grid[x + 1, y] == CellType.Empty)
                                {
                                    if (grid[x, y] != CellType.KeyChest && 
                                        grid[x, y] != CellType.YellowChest && 
                                        grid[x, y] != CellType.BlueChest)
                                    {
                                        return new Vector2Int(x, y);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Tier 2: Relaxed flat spot
            for (int dy = -ch.ry; dy <= ch.ry; dy++)
            {
                int y = ch.cy + dy;
                if (y <= 1 || y >= height - 1) continue;
                
                for (int dx = 0; dx <= ch.rx; dx++)
                {
                    int[] xOffsets = { -dx, dx };
                    foreach (int ox in xOffsets)
                    {
                        int x = ch.cx + ox;
                        if (x > 1 && x < widthInt - 2 && Mathf.Abs(x - keyPos.x) >= 3)
                        {
                            if (isPoolCh && x >= poolLeft && x <= poolRight) continue;
                            if (lastActiveRoomId == 15 && x >= 24 && x <= 48) continue;

                            if (grid[x, y] == CellType.Empty && grid[x, y - 1] == CellType.Solid)
                            {
                                if (grid[x - 1, y - 1] == CellType.Solid && grid[x + 1, y - 1] == CellType.Solid)
                                {
                                    if (grid[x, y] != CellType.KeyChest && 
                                        grid[x, y] != CellType.YellowChest && 
                                        grid[x, y] != CellType.BlueChest)
                                    {
                                        return new Vector2Int(x, y);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Tier 3: Any solid ground
            for (int dy = -ch.ry; dy <= ch.ry; dy++)
            {
                int y = ch.cy + dy;
                if (y <= 1 || y >= height - 1) continue;
                
                for (int dx = 0; dx <= ch.rx; dx++)
                {
                    int[] xOffsets = { -dx, dx };
                    foreach (int ox in xOffsets)
                    {
                        int x = ch.cx + ox;
                        if (x > 1 && x < widthInt - 2 && Mathf.Abs(x - keyPos.x) >= 3)
                        {
                            if (isPoolCh && x >= poolLeft && x <= poolRight) continue;
                            if (lastActiveRoomId == 15 && x >= 24 && x <= 48) continue;

                            if (grid[x, y] == CellType.Empty && grid[x, y - 1] == CellType.Solid)
                            {
                                if (grid[x, y] != CellType.KeyChest && 
                                    grid[x, y] != CellType.YellowChest && 
                                    grid[x, y] != CellType.BlueChest)
                                {
                                    return new Vector2Int(x, y);
                                }
                            }
                        }
                    }
                }
            }

            int fallbackX = keyPos.x - 3;
            if (fallbackX < 2) fallbackX = keyPos.x + 3;
            int fallbackY = keyPos.y;
            return new Vector2Int(fallbackX, fallbackY);
        }

        public void SetActiveRoom(int roomId)
        {
            SetActiveRoom(roomId, true);
        }

        public void SetActiveRoom(int roomId, bool enteringFromLeft)
        {
            if (roomId < 1 || roomId > 50) return;
            lastActiveRoomId = roomId;

            // Savepoint lock mechanism
            if (roomId == 10 || roomId == 20 || roomId == 30 || roomId == 40 || roomId == 50)
            {
                int currentUnlockedSavepoint = PlayerPrefs.GetInt("ActiveSavepointRoomId", 0);
                if (roomId > currentUnlockedSavepoint)
                {
                    PlayerPrefs.SetInt("ActiveSavepointRoomId", roomId);
                    PlayerPrefs.Save();
                    Debug.Log($"[Savepoint] Unlocked savepoint at Room {roomId}");
                }
            }

            activeLadders.Clear();

            // Clean up any static editor-built LevelGrid children, merchant NPCs, enemies, doors, and chests
            GameObject levelGridGo = GameObject.Find("LevelGrid");
            if (levelGridGo != null)
            {
                // Explicitly destroy dynamically generated combined meshes to prevent memory leak
                MeshFilter[] childMfs = levelGridGo.GetComponentsInChildren<MeshFilter>();
                foreach (var mf in childMfs)
                {
                    if (mf != null && mf.sharedMesh != null)
                    {
                        if (mf.gameObject.name.Contains("CombinedGrid_"))
                        {
                            Destroy(mf.sharedMesh);
                        }
                    }
                }
            }
            if (levelGridGo != null)
            {
                DestroyImmediate(levelGridGo);
            }
            levelGridGo = new GameObject("LevelGrid");

            // Destroy any existing static scene objects to avoid overlap duplicates
            foreach (var m in FindObjectsOfType<MerchantNPC>()) DestroyImmediate(m.gameObject);
            foreach (var e in FindObjectsOfType<EnemyGuardian>()) DestroyImmediate(e.gameObject);
            foreach (var c in FindObjectsOfType<PulsevaniaChest>()) DestroyImmediate(c.gameObject);
            foreach (var kc in FindObjectsOfType<KeyChest>()) DestroyImmediate(kc.gameObject);
            foreach (var d in FindObjectsOfType<LockedDoor>()) DestroyImmediate(d.gameObject);

            // Clean up any stray collectibles, loot, and equipment drops from previous room
            foreach (var lp in FindObjectsOfType<LootPickup>()) DestroyImmediate(lp.gameObject);
            foreach (var ep in FindObjectsOfType<EquipmentItemPickup>()) DestroyImmediate(ep.gameObject);
            foreach (var rk in FindObjectsOfType<RoomKeyPickup>()) DestroyImmediate(rk.gameObject);

            Rigidbody2D gridRb = levelGridGo.GetComponent<Rigidbody2D>();
            if (gridRb == null) gridRb = levelGridGo.AddComponent<Rigidbody2D>();
            gridRb.bodyType = RigidbodyType2D.Static;

            CompositeCollider2D gridComp = levelGridGo.GetComponent<CompositeCollider2D>();
            if (gridComp == null) gridComp = levelGridGo.AddComponent<CompositeCollider2D>();
            gridComp.geometryType = CompositeCollider2D.GeometryType.Outlines;

            foreach (var go in activeRoomEntities)
            {
                if (go != null) DestroyImmediate(go);
            }
            activeRoomEntities.Clear();

            int index = roomId - 1;
            int row = index / 10;
            int col = index % 10;
            int biome = index / 10;
            if (biome < 0) biome = 0;
            if (biome > 4) biome = 4;

            float width = GetRoomWidthForLevel(roomId);
            bool isBossRoom = (roomId == 10 || roomId == 20 || roomId == 30 || roomId == 40 || roomId == 50);
            if (isBossRoom)
            {
                width = (roomId == 20 || roomId == 30 || roomId == 40 || roomId == 50) ? 110f : 80f;
            }

            float centerX = originX + col * width + width / 2f;
            float roomY = originY + (4 - row) * roomHeight + roomHeight / 2f - 2f;

            int startX = Mathf.FloorToInt(originX + col * width);
            int endX = Mathf.FloorToInt(originX + (col + 1) * width);

            // Use reproducible seed based on room ID to guarantee distinct layouts that stay constant when returning
            Random.InitState(roomId * 98765 + 4321);

            int widthInt = Mathf.FloorToInt(width);
            int height = 30; // Expanded Y grid height from 18 to 30 for massive verticality
            CellType[,] grid = new CellType[widthInt, height];

            currentGrid = grid;
            currentGridWidth = widthInt;
            currentGridHeight = height;

            // 1. Initialize all as Solid
            for (int x = 0; x < widthInt; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    grid[x, y] = CellType.Solid;
                }
            }

            List<Chamber> chambers = new List<Chamber>();
            int progressionExitChamberIndex = 0;

            if (isBossRoom)
            {
                // Custom Hand-Designed Grand Dungeon Layout (never random, 100% stable and high quality)

                if (roomId == 10)
                {
                    // --- REDESIGN MAP 10 BOSS ROOM ---
                    // 1. Re-initialize chambers list with spacious chamber definitions
                    chambers.Add(new Chamber { cx = 10, cy = 3, rx = 8, ry = 2, style = RoomStyle.DeepUnderground });    // Entry Chamber (Floor Y = 2)
                    chambers.Add(new Chamber { cx = 22, cy = 23, rx = 11, ry = 3, style = RoomStyle.DeepUnderground });  // Top-Left Chamber
                    chambers.Add(new Chamber { cx = 52, cy = 23, rx = 10, ry = 3, style = RoomStyle.DeepUnderground });  // Top-Middle Chamber
                    chambers.Add(new Chamber { cx = 38, cy = 10, rx = 15, ry = 8, style = RoomStyle.DeepUnderground });  // Center-Bottom Lake Chamber
                    chambers.Add(new Chamber { cx = 68, cy = 3, rx = 10, ry = 2, style = RoomStyle.DeepUnderground });   // Grand Boss Arena (Floor Y = 2)

                    progressionExitChamberIndex = 4;

                    // 2. Carve Entry Chamber (x = 2 to 19, y = 2 to 14)
                    for (int x = 2; x <= 19; x++)
                    {
                        for (int y = 2; y <= 14; y++)
                        {
                            grid[x, y] = CellType.Empty;
                        }
                    }

                    // 3. Carve Left Ladder Shaft (x = 18, y = 2 to 24)
                    for (int y = 2; y <= 24; y++)
                    {
                        grid[17, y] = CellType.Empty;
                        grid[18, y] = CellType.Ladder;
                        grid[19, y] = CellType.Empty;
                    }

                    // 4. Carve Top-Left Chamber (x = 20 to 35, y = 20 to 26)
                    for (int x = 20; x <= 35; x++)
                    {
                        for (int y = 20; y <= 26; y++)
                        {
                            grid[x, y] = CellType.Empty;
                        }
                    }

                    // 5. Carve Center-Bottom Lake Chamber (x = 20 to 52, y = 2 to 17)
                    for (int x = 20; x <= 52; x++)
                    {
                        for (int y = 2; y <= 17; y++)
                        {
                            grid[x, y] = CellType.Empty;
                        }
                    }

                    // 6. Place the Lake at the bottom of Center-Bottom Chamber (x = 28 to 44, y = 2 to 3)
                    for (int x = 27; x <= 45; x++)
                    {
                        grid[x, 1] = CellType.Solid;
                    }
                    grid[27, 2] = CellType.Solid;
                    grid[27, 3] = CellType.Solid;
                    grid[45, 2] = CellType.Solid;
                    grid[45, 3] = CellType.Solid;

                    for (int x = 28; x <= 44; x++)
                    {
                        grid[x, 2] = CellType.Water;
                        grid[x, 3] = CellType.Water;
                    }

                    // 7. Place floating bridge/stepping stones above the lake (y = 5)
                    grid[31, 5] = CellType.Solid;
                    grid[32, 5] = CellType.Solid;
                    grid[35, 5] = CellType.Solid;
                    grid[36, 5] = CellType.Solid;
                    grid[39, 5] = CellType.Solid;
                    grid[40, 5] = CellType.Solid;
                    grid[43, 5] = CellType.Solid;
                    grid[44, 5] = CellType.Solid;

                    // 8. Place spikes on Top-Left Floor
                    grid[24, 20] = CellType.Spikes;
                    grid[25, 20] = CellType.Spikes;

                    // 9. Carve Top-Middle Chamber (x = 40 to 60, y = 20 to 26)
                    for (int x = 40; x <= 60; x++)
                    {
                        for (int y = 20; y <= 26; y++)
                        {
                            grid[x, y] = CellType.Empty;
                        }
                    }

                    // 10. Carve Right Ladder Shaft (x = 51, y = 2 to 24)
                    for (int y = 2; y <= 24; y++)
                    {
                        grid[50, y] = CellType.Empty;
                        grid[51, y] = CellType.Ladder;
                        grid[52, y] = CellType.Empty;
                    }

                    // 11. Carve Grand Boss Arena (x = 53 to 77, y = 2 to 26)
                    for (int x = 53; x <= 77; x++)
                    {
                        for (int y = 2; y <= 26; y++)
                        {
                            grid[x, y] = CellType.Empty;
                        }
                    }
                }
                else if (roomId == 20)
                {
                    // --- EL YAPIMI MAJESTİK MAP 20 BOSS ODASI ---
                    // 1. Define custom chambers for layout mapping
                    chambers.Add(new Chamber { cx = 8, cy = 11, rx = 6, ry = 2, style = RoomStyle.DeepUnderground });    // Entry Chamber (Ferah)
                    chambers.Add(new Chamber { cx = 48, cy = 24, rx = 30, ry = 3, style = RoomStyle.DeepUnderground });  // Floor 3 (Top) Chamber (Çok Geniş)
                    chambers.Add(new Chamber { cx = 48, cy = 5, rx = 30, ry = 3, style = RoomStyle.DeepUnderground });   // Floor 1 (Bottom) Chamber (Çok Geniş)
                    chambers.Add(new Chamber { cx = 95, cy = 13, rx = 13, ry = 5, style = RoomStyle.DeepUnderground });  // Exit/Boss Arena (Devasa ve Ferah)

                    progressionExitChamberIndex = 3;

                    // 2. Carve Entry Room (Floor 2, Left)
                    for (int x = 2; x <= 15; x++)
                    {
                        for (int y = 10; y <= 16; y++)
                        {
                            grid[x, y] = CellType.Empty;
                        }
                    }

                    // 3. Carve Left Vertical Shaft & Ladder (Geniş ve yüksek)
                    for (int x = 16; x <= 18; x++)
                    {
                        for (int y = 2; y <= 28; y++)
                        {
                            grid[x, y] = (x == 17) ? CellType.Ladder : CellType.Empty;
                        }
                    }

                    // 4. Carve Floor 3 (Top Floor - Genişletilmiş)
                    for (int x = 19; x <= 78; x++)
                    {
                        for (int y = 21; y <= 27; y++)
                        {
                            grid[x, y] = CellType.Empty;
                        }
                    }

                    // 5. Carve Floor 1 (Bottom Floor - Genişletilmiş)
                    for (int x = 19; x <= 78; x++)
                    {
                        for (int y = 2; y <= 8; y++)
                        {
                            grid[x, y] = CellType.Empty;
                        }
                    }

                    // 6. Carve Right Vertical Shaft & Ladder (Geniş ve yüksek)
                    for (int x = 79; x <= 81; x++)
                    {
                        for (int y = 2; y <= 28; y++)
                        {
                            grid[x, y] = (x == 80) ? CellType.Ladder : CellType.Empty;
                        }
                    }

                    // 7. Carve Exit/Boss Arena (Devasa ve yüksek)
                    for (int x = 82; x <= 107; x++)
                    {
                        for (int y = 9; y <= 20; y++)
                        {
                            grid[x, y] = CellType.Empty;
                        }
                    }

                    // 8. Floor 3 (Top Floor) is a clean path (no spikes or hazards as requested)
                    // Keep grid empty to guarantee a safe, walkable corridor

                    // 9. Flat Walking Platform for Floor 1 (Lava Lake completely removed as requested)
                    for (int x = 19; x <= 78; x++)
                    {
                        grid[x, 1] = CellType.Solid;
                        grid[x, 2] = CellType.Solid; // Solid flat ground walkway
                        for (int y = 3; y <= 8; y++)
                        {
                            grid[x, y] = CellType.Empty; // Empty air space
                        }
                    }

                    // 10. Carve a Dead-End Chamber on Floor 2 above the old lake (entered from left ladder shaft)
                    // x = 19 to 45, y = 10 to 13.
                    for (int x = 19; x <= 45; x++)
                    {
                        grid[x, 9] = CellType.Solid; // Solid floor for the dead-end room
                        for (int y = 10; y <= 13; y++)
                        {
                            grid[x, y] = CellType.Empty; // Empty air inside the dead-end room
                        }
                    }
                    // Block the right end of this chamber to make it a dead-end
                    for (int y = 10; y <= 14; y++)
                    {
                        grid[46, y] = CellType.Solid;
                    }
                }
                else if (roomId == 30 || roomId == 40 || roomId == 50)
                {
                    // --- MAP 30, MAP 40 & MAP 50 CUSTOM SPACIOUS BOSS ROOMS ---
                    // 1. Define custom chambers for layout mapping
                    chambers.Add(new Chamber { cx = 8, cy = 10, rx = 6, ry = 2, style = RoomStyle.DeepUnderground });    // Entry Chamber
                    chambers.Add(new Chamber { cx = 48, cy = 24, rx = 30, ry = 3, style = RoomStyle.DeepUnderground });  // Floor 3 (Top) Chamber
                    chambers.Add(new Chamber { cx = 48, cy = 5, rx = 30, ry = 3, style = RoomStyle.DeepUnderground });   // Floor 1 (Bottom) Chamber
                    chambers.Add(new Chamber { cx = 95, cy = 12, rx = 12, ry = 4, style = RoomStyle.DeepUnderground });  // Exit/Boss Chamber

                    progressionExitChamberIndex = 3;

                    // 2. Carve Entry Room (Floor 2, Left)
                    for (int x = 2; x <= 15; x++)
                    {
                        for (int y = 9; y <= 15; y++)
                        {
                            grid[x, y] = CellType.Empty;
                        }
                    }

                    // 3. Carve Left Vertical Shaft & Ladder
                    for (int x = 16; x <= 18; x++)
                    {
                        for (int y = 2; y <= 28; y++)
                        {
                            grid[x, y] = (x == 17) ? CellType.Ladder : CellType.Empty;
                        }
                    }

                    // 4. Carve Floor 3 (Top Floor)
                    for (int x = 19; x <= 78; x++)
                    {
                        for (int y = 21; y <= 27; y++)
                        {
                            grid[x, y] = CellType.Empty;
                        }
                    }

                    // 5. Carve Floor 1 (Bottom Floor)
                    for (int x = 19; x <= 78; x++)
                    {
                        for (int y = 2; y <= 8; y++)
                        {
                            grid[x, y] = CellType.Empty;
                        }
                    }

                    // 6. Carve Right Vertical Shaft & Ladder
                    for (int x = 79; x <= 81; x++)
                    {
                        for (int y = 2; y <= 28; y++)
                        {
                            grid[x, y] = (x == 80) ? CellType.Ladder : CellType.Empty;
                        }
                    }

                    // 7. Carve Exit/Boss Arena
                    for (int x = 82; x <= 107; x++)
                    {
                        for (int y = 8; y <= 20; y++)
                        {
                            grid[x, y] = CellType.Empty;
                        }
                    }

                    // 8. Place a dead-end chamber on Floor 2 above the bottom lake (entered from left ladder shaft)
                    for (int x = 19; x <= 45; x++)
                    {
                        grid[x, 9] = CellType.Solid; // Solid floor for the dead-end room
                        for (int y = 10; y <= 14; y++)
                        {
                            grid[x, y] = CellType.Empty; // Empty air inside the dead-end room
                        }
                    }
                    // Block the right end of this chamber to make it a dead-end
                    for (int y = 10; y <= 14; y++)
                    {
                        grid[46, y] = CellType.Solid;
                    }

                    if (roomId == 30 || roomId == 50)
                    {
                        // 9. Place a custom lava lake at Floor 1 (Bottom Floor) from x = 24 to 73
                        for (int x = 23; x <= 74; x++)
                        {
                            grid[x, 1] = CellType.Solid;
                        }
                        grid[23, 2] = CellType.Solid;
                        grid[23, 3] = CellType.Solid;
                        grid[74, 2] = CellType.Solid;
                        grid[74, 3] = CellType.Solid;

                        for (int x = 24; x <= 73; x++)
                        {
                            grid[x, 2] = CellType.Lava;
                            grid[x, 3] = CellType.Lava;
                        }

                        // 10. Draw custom 2-tile wide floating platforms over this lava lake
                        int currentHeight = 5;
                        int sx = 25;
                        while (sx <= 72)
                        {
                            grid[sx, currentHeight] = CellType.Solid;
                            if (sx + 1 <= 72)
                            {
                                grid[sx + 1, currentHeight] = CellType.Solid;
                            }

                            // Clear headroom above platforms
                            for (int dy = 1; dy <= 3; dy++)
                            {
                                grid[sx, currentHeight + dy] = CellType.Empty;
                                if (sx + 1 <= 72)
                                {
                                    grid[sx + 1, currentHeight + dy] = CellType.Empty;
                                }
                            }

                            sx += 4; // 2-wide platform + 2-wide gap
                        }
                    }
                    else if (roomId == 40)
                    {
                        // 9. Bottom floor (Floor 1) Lava Lake from x = 24 to 48
                        for (int x = 23; x <= 49; x++)
                        {
                            grid[x, 1] = CellType.Solid;
                        }
                        grid[23, 2] = CellType.Solid;
                        grid[23, 3] = CellType.Solid;
                        grid[49, 2] = CellType.Solid;
                        grid[49, 3] = CellType.Solid;

                        for (int x = 24; x <= 48; x++)
                        {
                            grid[x, 2] = CellType.Lava;
                            grid[x, 3] = CellType.Lava;
                        }

                        // Bottom walkway solid ground from x = 49 to 78
                        for (int x = 50; x <= 78; x++)
                        {
                            grid[x, 1] = CellType.Solid;
                            grid[x, 2] = CellType.Solid;
                        }

                        // Top floor (Floor 3) Water Lake from x = 52 to 76
                        for (int x = 51; x <= 77; x++)
                        {
                            grid[x, 20] = CellType.Solid;
                        }
                        grid[51, 21] = CellType.Solid;
                        grid[51, 22] = CellType.Solid;
                        grid[77, 21] = CellType.Solid;
                        grid[77, 22] = CellType.Solid;

                        for (int x = 52; x <= 76; x++)
                        {
                            grid[x, 21] = CellType.Water;
                            grid[x, 22] = CellType.Water;
                        }

                        // 10. Draw platforms over bottom Lava Lake (x = 24 to 48)
                        int currentHeight = 5;
                        int sx = 25;
                        while (sx <= 47)
                        {
                            grid[sx, currentHeight] = CellType.Solid;
                            if (sx + 1 <= 47)
                            {
                                grid[sx + 1, currentHeight] = CellType.Solid;
                            }

                            // Clear headroom above platforms
                            for (int dy = 1; dy <= 3; dy++)
                            {
                                grid[sx, currentHeight + dy] = CellType.Empty;
                                if (sx + 1 <= 47)
                                {
                                    grid[sx + 1, currentHeight + dy] = CellType.Empty;
                                }
                            }

                            sx += 4;
                        }

                        // 11. Draw platforms over top Water Lake (x = 52 to 76)
                        int topCurrentHeight = 24;
                        int tsx = 53;
                        while (tsx <= 75)
                        {
                            grid[tsx, topCurrentHeight] = CellType.Solid;
                            if (tsx + 1 <= 75)
                            {
                                grid[tsx + 1, topCurrentHeight] = CellType.Solid;
                            }

                            // Clear headroom above platforms
                            for (int dy = 1; dy <= 3; dy++)
                            {
                                grid[tsx, topCurrentHeight + dy] = CellType.Empty;
                                if (tsx + 1 <= 75)
                                {
                                    grid[tsx + 1, topCurrentHeight + dy] = CellType.Empty;
                                }
                            }

                            tsx += 4;
                        }
                    }
                }
                else
                {
                    // 1. Define custom chambers for layout mapping
                    chambers.Add(new Chamber { cx = 8, cy = 12, rx = 6, ry = 2, style = RoomStyle.DeepUnderground });   // Entry Chamber
                    chambers.Add(new Chamber { cx = 33, cy = 22, rx = 15, ry = 2, style = RoomStyle.DeepUnderground }); // Floor 3 (Top) Chamber
                    chambers.Add(new Chamber { cx = 33, cy = 3, rx = 15, ry = 2, style = RoomStyle.DeepUnderground });   // Floor 1 (Bottom) Chamber
                    chambers.Add(new Chamber { cx = 65, cy = 12, rx = 12, ry = 2, style = RoomStyle.DeepUnderground });  // Exit/Boss Chamber

                    progressionExitChamberIndex = 3;

                    // 2. Carve Entry Room (Floor 2, Left)
                    for (int x = 2; x <= 15; x++)
                    {
                        for (int y = 11; y <= 14; y++)
                        {
                            grid[x, y] = CellType.Empty;
                        }
                    }

                    // 3. Carve Left Vertical Shaft & Ladder
                    for (int x = 16; x <= 18; x++)
                    {
                        for (int y = 2; y <= 24; y++)
                        {
                            grid[x, y] = (x == 17) ? CellType.Ladder : CellType.Empty;
                        }
                    }

                    // 4. Carve Floor 3 (Top Floor)
                    for (int x = 19; x <= 48; x++)
                    {
                        for (int y = 21; y <= 24; y++)
                        {
                            grid[x, y] = CellType.Empty;
                        }
                    }

                    // 5. Carve Floor 1 (Bottom Floor)
                    for (int x = 19; x <= 48; x++)
                    {
                        for (int y = 2; y <= 5; y++)
                        {
                            grid[x, y] = CellType.Empty;
                        }
                    }

                    // 6. Carve Right Vertical Shaft & Ladder
                    for (int x = 49; x <= 51; x++)
                    {
                        for (int y = 2; y <= 24; y++)
                        {
                            grid[x, y] = (x == 50) ? CellType.Ladder : CellType.Empty;
                        }
                    }

                    // 7. Carve Exit/Boss Arena (Floor 2, Right)
                    for (int x = 52; x <= 77; x++)
                    {
                        for (int y = 11; y <= 16; y++) // Extra headroom for boss
                        {
                            grid[x, y] = CellType.Empty;
                        }
                    }

                    // 8. Place spikes (traps) on Floor 3
                    grid[24, 21] = CellType.Spikes;
                    grid[25, 21] = CellType.Spikes;
                    grid[36, 21] = CellType.Spikes;
                    grid[37, 21] = CellType.Spikes;

                    // 9. Place water/lava pool on Floor 1
                    bool isLava = (Random.value > 0.5f);
                    CellType poolType = isLava ? CellType.Lava : CellType.Water;
                    // Build a U-shaped solid stone wall around Floor 1 pool (from x = 24 to 36)
                    for (int x = 23; x <= 37; x++)
                    {
                        grid[x, 1] = CellType.Solid;
                    }
                    grid[23, 2] = CellType.Solid;
                    grid[23, 3] = CellType.Solid;
                    grid[37, 2] = CellType.Solid;
                    grid[37, 3] = CellType.Solid;

                    for (int x = 24; x <= 36; x++)
                    {
                        grid[x, 2] = poolType;
                        grid[x, 3] = poolType;
                    }
                    // Solid stepping stone pillar in the middle of Floor 1 pool
                    grid[29, 2] = CellType.Solid;
                    grid[30, 2] = CellType.Solid;
                    grid[29, 3] = CellType.Solid;
                    grid[30, 3] = CellType.Solid;
                }

                // Initialize styles for boss rooms
                currentCellStyles = new RoomStyle[widthInt, height];
                for (int x = 0; x < widthInt; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        currentCellStyles[x, y] = RoomStyle.DeepUnderground;
                    }
                }
            }
            else
            {
                // Procedural subterranean maze with 3 to 6 chambers for normal rooms
                int numChambers = 3 + (roomId / 10);
                if (numChambers > 6) numChambers = 6;

                for (int i = 0; i < numChambers; i++)
                {
                    int minX = 14 + i * ((widthInt - 30) / numChambers);
                    int maxX = 14 + (i + 1) * ((widthInt - 30) / numChambers);
                    int cx = Random.Range(minX, maxX);
                    // High-quality vertical distribution: low, middle, and high zones
                    int cy = (i % 3 == 0) ? Random.Range(4, 8) : ((i % 3 == 1) ? Random.Range(12, 16) : Random.Range(20, 24));
                    int rx = Random.Range(10, 14); // Expanded to 20-28 width for grander, more spacious caves
                    int ry = Random.Range(7, 11);  // Expanded to 14-22 height for taller, airy caves

                    RoomStyle chStyle = RoomStyle.DeepUnderground;
                    if (cy > 15)
                    {
                        chStyle = RoomStyle.HighHills;
                    }
                    else
                    {
                        chStyle = (i % 2 == 0) ? RoomStyle.Cave : RoomStyle.DeepUnderground;
                    }
                    chambers.Add(new Chamber { cx = cx, cy = cy, rx = rx, ry = ry, style = chStyle });
                }

                int mainChambersCount = chambers.Count;
                progressionExitChamberIndex = mainChambersCount - 1;

                // Create 1 or 2 dead-end chambers branching off the main sequence
                System.Collections.Generic.List<KeyValuePair<Chamber, Chamber>> deadEndConnections = new System.Collections.Generic.List<KeyValuePair<Chamber, Chamber>>();
                int numDeadEnds = Random.Range(1, 3);
                for (int d = 0; d < numDeadEnds; d++)
                {
                    int parentIdx = Random.Range(0, mainChambersCount);
                    Chamber parent = chambers[parentIdx];

                    // Distribute dead ends vertically based on parent chamber position
                    int deadEndCy = parent.cy;
                    if (parent.cy <= 10)
                    {
                        deadEndCy = Random.Range(18, 25);
                    }
                    else if (parent.cy >= 18)
                    {
                        deadEndCy = Random.Range(4, 10);
                    }
                    else
                    {
                        deadEndCy = (Random.value > 0.5f) ? Random.Range(4, 7) : Random.Range(20, 25);
                    }

                    int deadEndCx = Mathf.Clamp(parent.cx + Random.Range(-4, 5), 14, widthInt - 15);
                    int rx = Random.Range(9, 13);  // Expanded dead-ends to 18-26 width
                    int ry = Random.Range(7, 10);  // Expanded dead-ends to 14-20 height

                    RoomStyle style = (deadEndCy > 15) ? RoomStyle.HighHills : RoomStyle.Cave;
                    Chamber deadEnd = new Chamber { cx = deadEndCx, cy = deadEndCy, rx = rx, ry = ry, style = style };
                    
                    chambers.Add(deadEnd);
                    deadEndConnections.Add(new KeyValuePair<Chamber, Chamber>(deadEnd, parent));
                }

                // Carve rounded ellipse chambers first (ellipses are carved first so tunnels/ladders are carved ON TOP and not overwritten)
                foreach (var ch in chambers)
                {
                    for (int x = ch.cx - ch.rx; x <= ch.cx + ch.rx; x++)
                    {
                        for (int y = ch.cy - ch.ry; y <= ch.cy + ch.ry; y++)
                        {
                            if (x > 1 && x < widthInt - 2 && y > 1 && y < height - 2)
                            {
                                float dx = (float)(x - ch.cx) / ch.rx;
                                float dy = (float)(y - ch.cy) / ch.ry;
                                if (dx * dx + dy * dy <= 1.2f)
                                {
                                    if (grid[x, y] != CellType.Ladder)
                                    {
                                        grid[x, y] = CellType.Empty;
                                    }
                                }
                            }
                        }
                    }
                }

                // Now carve all dead-end connections (ladders and tunnels) on top of the ellipses
                foreach (var conn in deadEndConnections)
                {
                    Chamber deadEnd = conn.Key;
                    Chamber parent = conn.Value;

                    // Connect deadEnd to parent with a vertical shaft and ladder at parent.cx
                    int midX = parent.cx;
                    // Lower startY_S to parent floor, raise endY_S to deadEnd ceiling to prevent floating ladders, clamped to safe grid range
                    int startY_S = Mathf.Clamp(Mathf.Min(parent.cy - parent.ry, deadEnd.cy - deadEnd.ry), 1, height - 2);
                    int endY_S = Mathf.Clamp(Mathf.Max(parent.cy, deadEnd.cy) + 1, 1, height - 2);
                    for (int y = startY_S; y <= endY_S; y++)
                    {
                        grid[midX - 1, y] = CellType.Empty;
                        grid[midX, y] = CellType.Ladder; // Place ladder in center
                        grid[midX + 1, y] = CellType.Empty;
                    }
                    
                    // Also carve horizontal tunnel from midX to deadEnd.cx at deadEnd.cy
                    int startX_DE = Mathf.Min(midX, deadEnd.cx);
                    int endX_DE = Mathf.Max(midX, deadEnd.cx);
                    for (int x = startX_DE; x <= endX_DE; x++)
                    {
                        if (grid[x, deadEnd.cy - 1] != CellType.Ladder) grid[x, deadEnd.cy - 1] = CellType.Empty;
                        if (grid[x, deadEnd.cy] != CellType.Ladder) grid[x, deadEnd.cy] = CellType.Empty;
                        if (grid[x, deadEnd.cy + 1] != CellType.Ladder) grid[x, deadEnd.cy + 1] = CellType.Empty;
                    }
                }

                // Floating platforms placement has been deferred to run after ellipse carving so they are not deleted.

                // Carve Entry tunnel at chambers[0].cy
                int carveEntryCy = chambers[0].cy;
                for (int x = 0; x <= chambers[0].cx; x++)
                {
                    for (int y = carveEntryCy - 1; y <= carveEntryCy + 1; y++)
                    {
                        if (grid[x, y] != CellType.Ladder) grid[x, y] = CellType.Empty;
                    }
                }

                // Carve Exit tunnel at chambers[progressionExitChamberIndex].cy
                int carveExitCy = chambers[progressionExitChamberIndex].cy;
                for (int x = chambers[progressionExitChamberIndex].cx; x < widthInt; x++)
                {
                    for (int y = carveExitCy - 1; y <= carveExitCy + 1; y++)
                    {
                        if (grid[x, y] != CellType.Ladder) grid[x, y] = CellType.Empty;
                    }
                }

                // Connect main chambers sequentially
                for (int i = 0; i < mainChambersCount - 1; i++)
                {
                    Chamber c1 = chambers[i];
                    Chamber c2 = chambers[i + 1];

                    int midX = (c1.cx + c2.cx) / 2;

                    // Carve horizontal tunnel from c1.cx to midX at c1.cy
                    int startX_T1 = Mathf.Min(c1.cx, midX);
                    int endX_T1 = Mathf.Max(c1.cx, midX);
                    for (int x = startX_T1; x <= endX_T1; x++)
                    {
                        if (grid[x, c1.cy - 1] != CellType.Ladder) grid[x, c1.cy - 1] = CellType.Empty;
                        if (grid[x, c1.cy] != CellType.Ladder) grid[x, c1.cy] = CellType.Empty;
                        if (grid[x, c1.cy + 1] != CellType.Ladder) grid[x, c1.cy + 1] = CellType.Empty;
                    }

                    // Carve vertical shaft with ladder from c1.cy to c2.cy at midX
                    // Lower startY_S to floor of lower room, raise endY_S to ceiling of upper room to prevent floating ladders, clamped to safe grid range
                    int startY_S = Mathf.Clamp(Mathf.Min(c1.cy - c1.ry, c2.cy - c2.ry), 1, height - 2);
                    int endY_S = Mathf.Clamp(Mathf.Max(c1.cy, c2.cy) + 1, 1, height - 2);
                    for (int y = startY_S; y <= endY_S; y++)
                    {
                        grid[midX - 1, y] = CellType.Empty;
                        grid[midX, y] = CellType.Ladder; // Place ladder in center
                        grid[midX + 1, y] = CellType.Empty;
                    }

                    // Carve horizontal tunnel from midX to c2.cx at c2.cy
                    int startX_T2 = Mathf.Min(midX, c2.cx);
                    int endX_T2 = Mathf.Max(midX, c2.cx);
                    for (int x = startX_T2; x <= endX_T2; x++)
                    {
                        if (grid[x, c2.cy - 1] != CellType.Ladder) grid[x, c2.cy - 1] = CellType.Empty;
                        if (grid[x, c2.cy] != CellType.Ladder) grid[x, c2.cy] = CellType.Empty;
                        if (grid[x, c2.cy + 1] != CellType.Ladder) grid[x, c2.cy + 1] = CellType.Empty;
                    }
                }
            }

            if (!isBossRoom)
            {
                // 2.5 Post-processing: Fill narrow pits (1 or 2 blocks wide) in the terrain floor
                FillPits(grid, widthInt, height);

                // 2.6 Spawn floating platforms in large chambers to enable jumping navigation (metroidvania staircase style)
                // This is run AFTER ellipse carving and pit filling so the platforms are not deleted/modified.
                foreach (var ch in chambers)
                {
                    if (ch.ry >= 4)
                    {
                        int numPlats = ch.ry / 2;
                        for (int p = 0; p < numPlats; p++)
                        {
                            // Lower jump height to 2 units per step for effortless climbing (prevents player getting stuck)
                            int platY = ch.cy - ch.ry + 2 + p * 2;
                            if (platY >= ch.cy + ch.ry - 1) continue;

                            int platLength = Random.Range(3, 6);
                            int startX_Plat = (p % 2 == 0) ? ch.cx - ch.rx + 2 : ch.cx + ch.rx - 2 - platLength;
                            startX_Plat = Mathf.Max(startX_Plat, ch.cx - ch.rx + 1);
                            int endX_Plat = Mathf.Min(startX_Plat + platLength, ch.cx + ch.rx - 1);

                            for (int px = startX_Plat; px <= endX_Plat; px++)
                            {
                                if (px > 1 && px < widthInt - 2 && platY > 1 && platY < height - 2)
                                {
                                    if (grid[px, platY] != CellType.Ladder)
                                    {
                                        grid[px, platY] = CellType.Solid;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (roomId == 4)
            {
                // Manually carve a new chamber (room) at the top-right of Map 4
                // 1. Carve the room itself (x = 90 to 102, y = 18 to 25)
                for (int x = 89; x <= 102; x++)
                {
                    for (int y = 17; y <= 26; y++)
                    {
                        if (y == 17 || x == 89 || y == 26)
                        {
                            // solid boundary walls for the new chamber
                            grid[x, y] = CellType.Solid;
                        }
                        else
                        {
                            // empty inside
                            grid[x, y] = CellType.Empty;
                        }
                    }
                }

                // 2. Carve connection shaft & ladder at x = 86 to 88 from y = 4 to 20
                for (int y = 4; y <= 20; y++)
                {
                    grid[86, y] = CellType.Empty;
                    grid[87, y] = CellType.Ladder;
                    grid[88, y] = CellType.Empty;
                }
                
                // Ensure the entry to the new chamber is open
                grid[89, 18] = CellType.Empty;
                grid[89, 19] = CellType.Empty;
                grid[90, 18] = CellType.Empty;
                grid[90, 19] = CellType.Empty;

                // Add to chambers list for style reference
                chambers.Add(new Chamber {
                    cx = 95,
                    cy = 22,
                    rx = 5,
                    ry = 4,
                    style = RoomStyle.DeepUnderground
                });
            }

            if (roomId == 6)
            {
                // Find the ladder's X coordinate in Map 6
                int ladderX = 50; // default fallback
                for (int x = 20; x < widthInt - 20; x++)
                {
                    bool isLadderCol = false;
                    for (int y = 2; y < height - 2; y++)
                    {
                        if (grid[x, y] == CellType.Ladder)
                        {
                            isLadderCol = true;
                            break;
                        }
                    }
                    if (isLadderCol)
                    {
                        ladderX = x;
                        break;
                    }
                }

                // Left hanging platform: 8 blocks to the left of the ladder
                int leftStart = ladderX - 11;
                int leftEnd = ladderX - 6;
                for (int x = leftStart; x <= leftEnd; x++)
                {
                    if (x >= 2 && x < widthInt - 2)
                    {
                        grid[x, 10] = CellType.Solid;
                        grid[x, 11] = CellType.Solid;
                        grid[x, 12] = CellType.Empty;
                        grid[x, 13] = CellType.Empty;
                        grid[x, 14] = CellType.Empty;
                        if (currentCellStyles != null)
                        {
                            currentCellStyles[x, 11] = RoomStyle.DeepUnderground;
                            currentCellStyles[x, 10] = RoomStyle.DeepUnderground;
                        }
                    }
                }

                // Right hanging platform: 8 blocks to the right of the ladder
                int rightStart = ladderX + 6;
                int rightEnd = ladderX + 11;
                for (int x = rightStart; x <= rightEnd; x++)
                {
                    if (x >= 2 && x < widthInt - 2)
                    {
                        grid[x, 14] = CellType.Solid;
                        grid[x, 15] = CellType.Solid;
                        grid[x, 16] = CellType.Empty;
                        grid[x, 17] = CellType.Empty;
                        grid[x, 18] = CellType.Empty;
                        if (currentCellStyles != null)
                        {
                            currentCellStyles[x, 15] = RoomStyle.DeepUnderground;
                            currentCellStyles[x, 14] = RoomStyle.DeepUnderground;
                        }
                    }
                }
            }

            if (roomId == 7)
            {
                // Manually carve a new chamber for the Purple Chest (x = 40 to 48, y = 18 to 25)
                for (int x = 39; x <= 49; x++)
                {
                    for (int y = 17; y <= 26; y++)
                    {
                        if (y == 17 || x == 39 || x == 49 || y == 26)
                        {
                            // solid walls of the room
                            grid[x, y] = CellType.Solid;
                        }
                        else
                        {
                            // empty inside
                            grid[x, y] = CellType.Empty;
                        }
                    }
                }

                // Carve connection ladder at x = 37 from y = 4 to 20
                for (int y = 4; y <= 20; y++)
                {
                    grid[36, y] = CellType.Empty;
                    grid[37, y] = CellType.Ladder;
                    grid[38, y] = CellType.Empty;
                }
                
                // Open the entrance to the new chamber
                grid[39, 18] = CellType.Empty;
                grid[39, 19] = CellType.Empty;
                grid[40, 18] = CellType.Empty;
                grid[40, 19] = CellType.Empty;

                // Add to chambers list so the procedural Key Chest spawning selects this chamber!
                chambers.Add(new Chamber {
                    cx = 44,
                    cy = 22,
                    rx = 4,
                    ry = 4,
                    style = RoomStyle.DeepUnderground
                });

                // Move the Gold Chest (Yellow Chest) to a safe, manually designed platform near the start
                // Platform is at x = 12 to 18, y = 3
                for (int x = 12; x <= 18; x++)
                {
                    grid[x, 2] = CellType.Solid;
                    grid[x, 3] = CellType.Solid;
                    grid[x, 4] = CellType.Empty;
                    grid[x, 5] = CellType.Empty;
                    if (currentCellStyles != null)
                    {
                        currentCellStyles[x, 3] = RoomStyle.DeepUnderground;
                        currentCellStyles[x, 2] = RoomStyle.DeepUnderground;
                    }
                }

                // Manually carve a platform for the Blue Chest at the middle-right area (x = 85 to 91, y = 11)
                for (int x = 85; x <= 91; x++)
                {
                    grid[x, 10] = CellType.Solid;
                    grid[x, 11] = CellType.Solid;
                    grid[x, 12] = CellType.Empty;
                    grid[x, 13] = CellType.Empty;
                    if (currentCellStyles != null)
                    {
                        currentCellStyles[x, 11] = RoomStyle.DeepUnderground;
                        currentCellStyles[x, 10] = RoomStyle.DeepUnderground;
                    }
                }

                // Manually place Patrollers (NPCs/Monsters) at safe, guaranteed coordinates
                grid[25, 4] = CellType.Patroller;
                grid[75, 4] = CellType.Patroller;
                grid[88, 12] = CellType.Patroller; // guarding the Blue Chest!

                grid[25, 3] = CellType.Solid;
                grid[75, 3] = CellType.Solid;
            }

            if (roomId == 8)
            {
                // Manually carve a new chamber for the Purple Chest (x = 40 to 48, y = 18 to 25)
                for (int x = 39; x <= 49; x++)
                {
                    for (int y = 17; y <= 26; y++)
                    {
                        if (y == 17 || x == 39 || x == 49 || y == 26)
                        {
                            // solid walls of the room
                            grid[x, y] = CellType.Solid;
                        }
                        else
                        {
                            // empty inside
                            grid[x, y] = CellType.Empty;
                        }
                    }
                }

                // Carve connection ladder at x = 37 from y = 4 to 20
                for (int y = 4; y <= 20; y++)
                {
                    grid[36, y] = CellType.Empty;
                    grid[37, y] = CellType.Ladder;
                    grid[38, y] = CellType.Empty;
                }
                
                // Open the entrance to the new chamber
                grid[39, 18] = CellType.Empty;
                grid[39, 19] = CellType.Empty;
                grid[40, 18] = CellType.Empty;
                grid[40, 19] = CellType.Empty;

                // Add to chambers list so the procedural Key Chest spawning selects this chamber!
                chambers.Add(new Chamber {
                    cx = 44,
                    cy = 22,
                    rx = 4,
                    ry = 4,
                    style = RoomStyle.DeepUnderground
                });

                // Move the Gold Chest (Yellow Chest) to a safe, manually designed platform near the start
                // Platform is at x = 12 to 18, y = 3
                for (int x = 12; x <= 18; x++)
                {
                    grid[x, 2] = CellType.Solid;
                    grid[x, 3] = CellType.Solid;
                    grid[x, 4] = CellType.Empty;
                    grid[x, 5] = CellType.Empty;
                    if (currentCellStyles != null)
                    {
                        currentCellStyles[x, 3] = RoomStyle.DeepUnderground;
                        currentCellStyles[x, 2] = RoomStyle.DeepUnderground;
                    }
                }

                // Manually carve a platform for the Blue Chest at the middle-right area (x = 85 to 91, y = 11)
                for (int x = 85; x <= 91; x++)
                {
                    grid[x, 10] = CellType.Solid;
                    grid[x, 11] = CellType.Solid;
                    grid[x, 12] = CellType.Empty;
                    grid[x, 13] = CellType.Empty;
                    if (currentCellStyles != null)
                    {
                        currentCellStyles[x, 11] = RoomStyle.DeepUnderground;
                        currentCellStyles[x, 10] = RoomStyle.DeepUnderground;
                    }
                }

                // --- NEW FLOATING PLATFORMS FOR MAP 8 TO REACH BLUE CHEST ---
                // Platform 1: x = 56 to 61, y = 15
                for (int x = 56; x <= 61; x++)
                {
                    grid[x, 14] = CellType.Solid;
                    grid[x, 15] = CellType.Solid;
                    grid[x, 16] = CellType.Empty;
                    grid[x, 17] = CellType.Empty;
                    if (currentCellStyles != null)
                    {
                        currentCellStyles[x, 15] = RoomStyle.DeepUnderground;
                        currentCellStyles[x, 14] = RoomStyle.DeepUnderground;
                    }
                }

                // Platform 2: x = 70 to 75, y = 13
                for (int x = 70; x <= 75; x++)
                {
                    grid[x, 12] = CellType.Solid;
                    grid[x, 13] = CellType.Solid;
                    grid[x, 14] = CellType.Empty;
                    grid[x, 15] = CellType.Empty;
                    if (currentCellStyles != null)
                    {
                        currentCellStyles[x, 13] = RoomStyle.DeepUnderground;
                        currentCellStyles[x, 12] = RoomStyle.DeepUnderground;
                    }
                }

                // --- REMOVE HANGING BLOCK NEAR NPC SPAWN ---
                // Clear grid at x = 75 to give the NPC space to spawn comfortably and walk
                for (int y = 5; y <= 9; y++)
                {
                    grid[75, y] = CellType.Empty;
                    grid[74, y] = CellType.Empty;
                    grid[76, y] = CellType.Empty;
                }

                // Manually place Patrollers (NPCs/Monsters) at safe, guaranteed coordinates
                grid[25, 4] = CellType.Patroller;
                grid[75, 4] = CellType.Patroller;
                grid[88, 12] = CellType.Patroller; // guarding the Blue Chest!

                grid[25, 3] = CellType.Solid;
                grid[75, 3] = CellType.Solid;
            }

            if (roomId == 9)
            {
                // Manually ensure solid floors and carve clear spaces for the player's path and NPCs
                
                // 1. Lower-left floor & NPC (x = 20 to 26, y = 4)
                for (int x = 20; x <= 26; x++)
                {
                    grid[x, 2] = CellType.Solid;
                    grid[x, 3] = CellType.Solid;
                    grid[x, 4] = CellType.Empty;
                    grid[x, 5] = CellType.Empty;
                }
                grid[22, 4] = CellType.Patroller;

                // 2. Lower-middle floor & NPC (x = 50 to 58, y = 4)
                for (int x = 50; x <= 58; x++)
                {
                    grid[x, 2] = CellType.Solid;
                    grid[x, 3] = CellType.Solid;
                    grid[x, 4] = CellType.Empty;
                    grid[x, 5] = CellType.Empty;
                }
                grid[55, 4] = CellType.Patroller;

                // 3. Lower-right floor & NPC (x = 80 to 88, y = 4)
                for (int x = 80; x <= 88; x++)
                {
                    grid[x, 2] = CellType.Solid;
                    grid[x, 3] = CellType.Solid;
                    grid[x, 4] = CellType.Empty;
                    grid[x, 5] = CellType.Empty;
                }
                grid[85, 4] = CellType.Patroller;

                // 4. Intermediate platform & NPC in the middle (x = 65 to 71, y = 12)
                for (int x = 65; x <= 71; x++)
                {
                    grid[x, 10] = CellType.Solid;
                    grid[x, 11] = CellType.Solid;
                    grid[x, 12] = CellType.Empty;
                    grid[x, 13] = CellType.Empty;
                    if (currentCellStyles != null)
                    {
                        currentCellStyles[x, 11] = RoomStyle.DeepUnderground;
                        currentCellStyles[x, 10] = RoomStyle.DeepUnderground;
                    }
                }
                grid[68, 12] = CellType.Patroller;
            }

            if (roomId == 12)
            {
                // Ensure room style styling for custom platforms
                // 1. Lower-middle platform (x = 30 to 40, y = 7-8)
                for (int x = 25; x <= 45; x++)
                {
                    for (int y = 9; y <= 15; y++)
                    {
                        grid[x, y] = CellType.Empty;
                    }
                }
                for (int x = 30; x <= 40; x++)
                {
                    grid[x, 7] = CellType.Solid;
                    grid[x, 8] = CellType.Solid;
                }

                // 2. Mid-high platform (x = 60 to 70, y = 13-14)
                for (int x = 55; x <= 75; x++)
                {
                    for (int y = 15; y <= 21; y++)
                    {
                        grid[x, y] = CellType.Empty;
                    }
                }
                for (int x = 60; x <= 70; x++)
                {
                    grid[x, 13] = CellType.Solid;
                    grid[x, 14] = CellType.Solid;
                }

                // 3. High platform (x = 90 to 100, y = 19-20)
                for (int x = 85; x <= 105; x++)
                {
                    for (int y = 21; y <= 27; y++)
                    {
                        grid[x, y] = CellType.Empty;
                    }
                }
                for (int x = 90; x <= 100; x++)
                {
                    grid[x, 19] = CellType.Solid;
                    grid[x, 20] = CellType.Solid;
                }

                // Ladders to connect ground to platform 1, platform 1 to platform 2, platform 2 to platform 3
                // Ground to Platform 1 Ladder (x = 27)
                for (int y = 2; y <= 8; y++)
                {
                    grid[27, y] = CellType.Ladder;
                    grid[26, y] = CellType.Empty;
                    grid[28, y] = CellType.Empty;
                }

                // Platform 1 to Platform 2 Ladder (x = 42)
                for (int y = 8; y <= 14; y++)
                {
                    grid[42, y] = CellType.Ladder;
                    grid[41, y] = CellType.Empty;
                    grid[43, y] = CellType.Empty;
                }

                // Platform 2 to Platform 3 Ladder (x = 57)
                for (int y = 14; y <= 20; y++)
                {
                    grid[57, y] = CellType.Ladder;
                    grid[56, y] = CellType.Empty;
                    grid[58, y] = CellType.Empty;
                }

                // Platform 3 to exit height helper ladder (x = 102)
                for (int y = 2; y <= 20; y++)
                {
                    grid[102, y] = CellType.Ladder;
                    grid[101, y] = CellType.Empty;
                    grid[103, y] = CellType.Empty;
                }

                // Place manual Enemy Patrollers in key accessible locations
                grid[32, 9] = CellType.Patroller; // Platform 1 (Gold Chest)
                grid[62, 15] = CellType.Patroller; // Platform 2 (Item Chest)
                grid[18, 2] = CellType.Patroller; // Ground level near entry
                grid[18, 1] = CellType.Solid; // Ground floor support
            }

            if (roomId == 13)
            {
                // Ensure room style styling for custom platforms
                // 1. Lower-middle platform (x = 30 to 40, y = 7-8)
                for (int x = 25; x <= 45; x++)
                {
                    for (int y = 9; y <= 15; y++)
                    {
                        grid[x, y] = CellType.Empty;
                    }
                }
                for (int x = 30; x <= 40; x++)
                {
                    grid[x, 7] = CellType.Solid;
                    grid[x, 8] = CellType.Solid;
                }

                // 2. Mid-high platform (x = 60 to 70, y = 13-14)
                for (int x = 55; x <= 75; x++)
                {
                    for (int y = 15; y <= 21; y++)
                    {
                        grid[x, y] = CellType.Empty;
                    }
                }
                for (int x = 60; x <= 70; x++)
                {
                    grid[x, 13] = CellType.Solid;
                    grid[x, 14] = CellType.Solid;
                }

                // 3. High platform (x = 90 to 100, y = 19-20)
                for (int x = 85; x <= 105; x++)
                {
                    for (int y = 21; y <= 27; y++)
                    {
                        grid[x, y] = CellType.Empty;
                    }
                }
                for (int x = 90; x <= 100; x++)
                {
                    grid[x, 19] = CellType.Solid;
                    grid[x, 20] = CellType.Solid;
                }

                // Ladders to connect ground to platform 1, platform 1 to platform 2, platform 2 to platform 3
                // Ground to Platform 1 Ladder (x = 27)
                for (int y = 2; y <= 8; y++)
                {
                    grid[27, y] = CellType.Ladder;
                    grid[26, y] = CellType.Empty;
                    grid[28, y] = CellType.Empty;
                }

                // Platform 1 to Platform 2 Ladder (x = 42)
                for (int y = 8; y <= 14; y++)
                {
                    grid[42, y] = CellType.Ladder;
                    grid[41, y] = CellType.Empty;
                    grid[43, y] = CellType.Empty;
                }

                // Platform 2 to Platform 3 Ladder (x = 57)
                for (int y = 14; y <= 20; y++)
                {
                    grid[57, y] = CellType.Ladder;
                    grid[56, y] = CellType.Empty;
                    grid[58, y] = CellType.Empty;
                }

                // Platform 3 to exit height helper ladder (x = 102)
                for (int y = 2; y <= 20; y++)
                {
                    grid[102, y] = CellType.Ladder;
                    grid[101, y] = CellType.Empty;
                    grid[103, y] = CellType.Empty;
                }
            }

            if (roomId == 15)
            {
                // Create a manual platform for the Key Chest
                for (int x = 85; x <= 105; x++)
                {
                    for (int y = 15; y <= 21; y++)
                    {
                        grid[x, y] = CellType.Empty;
                    }
                }
                for (int x = 90; x <= 100; x++)
                {
                    grid[x, 13] = CellType.Solid;
                    grid[x, 14] = CellType.Solid;
                }

                // Ladder from ground level to this platform
                for (int y = 2; y <= 14; y++)
                {
                    grid[91, y] = CellType.Ladder;
                    grid[90, y] = CellType.Empty;
                    grid[92, y] = CellType.Empty;
                }
            }

            // Initialize cell styles based on closest chamber
            currentCellStyles = new RoomStyle[widthInt, height];
            for (int x = 0; x < widthInt; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (isBossRoom)
                    {
                        currentCellStyles[x, y] = RoomStyle.DeepUnderground;
                    }
                    else
                    {
                        float minDist = float.MaxValue;
                        RoomStyle closestStyle = RoomStyle.DeepUnderground;
                        foreach (var ch in chambers)
                        {
                            float dx = x - ch.cx;
                            float dy = y - ch.cy;
                            float dist = dx * dx + dy * dy;
                            if (dist < minDist)
                            {
                                minDist = dist;
                                closestStyle = ch.style;
                            }
                        }
                        currentCellStyles[x, y] = closestStyle;
                    }
                }
            }

            // Outer border solid shell (ceiling and floor)            // Guarantee solid roof, floor, and outer walls at grid boundaries to prevent escaping
            for (int x = 0; x < widthInt; x++)
            {
                grid[x, 0] = CellType.Solid;
                grid[x, height - 1] = CellType.Solid;
            }
            for (int y = 0; y < height; y++)
            {
                grid[0, y] = CellType.Solid;
                grid[1, y] = CellType.Solid;
                grid[widthInt - 2, y] = CellType.Solid;
                grid[widthInt - 1, y] = CellType.Solid;
            }

            // Extend all ladders down to the ground to prevent floating in the air
            for (int x = 0; x < widthInt; x++)
            {
                for (int y = 1; y < height; y++)
                {
                    if (grid[x, y] == CellType.Ladder && grid[x, y - 1] == CellType.Empty)
                    {
                        int checkY = y - 1;
                        while (checkY >= 1 && grid[x, checkY] == CellType.Empty)
                        {
                            grid[x, checkY] = CellType.Ladder;
                            checkY--;
                        }
                    }
                }
            }

            // 3. Place Entities if room not cleared
            HashSet<int> poolCoords = new HashSet<int>();
            if (rooms[index].state != RoomState.Cleared)
            {
                // Determine highest and pool chambers first (ensure poolCh is never the entry chambers[0] or exit chambers[progressionExitChamberIndex])
                Chamber highestCh = chambers[0];
                foreach (var ch in chambers)
                {
                    if (ch.cy > highestCh.cy) highestCh = ch;
                }

                Chamber poolCh = chambers.Count > 2 ? chambers[1] : chambers[0];
                for (int i = 1; i < chambers.Count; i++)
                {
                    if (i == progressionExitChamberIndex) continue;
                    if (chambers[i].cy < poolCh.cy && chambers[i].cx != highestCh.cx)
                    {
                        poolCh = chambers[i];
                    }
                }

                if (roomId == 30 || roomId == 40)
                {
                    poolCh = chambers[2]; // Force Floor 1 to be the pool chamber, so other calculations (like patroller spawning) recognize the bottom chamber as the pool location
                }

                if (isBossRoom)
                {
                    // Spawn Boss, Princess, and Boss Chests in the last (exit) chamber of the procedural maze
                    int exitChIdx = progressionExitChamberIndex;
                    Vector2Int bossPos = FindValidFloorPos(chambers[exitChIdx], poolCh, grid, widthInt, height);
                    
                    // Force a robust, solid, flat platform under the boss and chests
                    int minPlatX = bossPos.x - 6;
                    int maxPlatX = bossPos.x + 6;
                    for (int px = minPlatX; px <= maxPlatX; px++)
                    {
                        if (px >= 2 && px <= widthInt - 3)
                        {
                            if (bossPos.y - 1 >= 0 && grid[px, bossPos.y - 1] != CellType.Ladder) grid[px, bossPos.y - 1] = CellType.Solid;
                            if (bossPos.y - 2 >= 0 && grid[px, bossPos.y - 2] != CellType.Ladder) grid[px, bossPos.y - 2] = CellType.Solid;
                            if (bossPos.y >= 0 && bossPos.y < height && grid[px, bossPos.y] != CellType.Ladder) grid[px, bossPos.y] = CellType.Empty;
                            if (bossPos.y + 1 < height && grid[px, bossPos.y + 1] != CellType.Ladder) grid[px, bossPos.y + 1] = CellType.Empty;
                            if (bossPos.y + 2 < height && grid[px, bossPos.y + 2] != CellType.Ladder) grid[px, bossPos.y + 2] = CellType.Empty;
                            if (bossPos.y + 3 < height && grid[px, bossPos.y + 3] != CellType.Ladder) grid[px, bossPos.y + 3] = CellType.Empty;
                            if (bossPos.y + 4 < height && grid[px, bossPos.y + 4] != CellType.Ladder) grid[px, bossPos.y + 4] = CellType.Empty;
                            if (bossPos.y + 5 < height && grid[px, bossPos.y + 5] != CellType.Ladder) grid[px, bossPos.y + 5] = CellType.Empty;

                            if (currentCellStyles != null)
                            {
                                if (bossPos.y - 1 >= 0) currentCellStyles[px, bossPos.y - 1] = RoomStyle.DeepUnderground;
                                if (bossPos.y - 2 >= 0) currentCellStyles[px, bossPos.y - 2] = RoomStyle.DeepUnderground;
                            }
                        }
                    }

                    if (roomId == 10)
                    {
                        // Redesigned Room 10: Boss in the arena, chests distributed in branching chambers
                        grid[bossPos.x, bossPos.y] = CellType.Boss;

                        // Purple Chest & Guardian in Top-Middle Chamber (x = 52, y = 20)
                        grid[52, 21] = CellType.KeyChest;
                        grid[48, 21] = CellType.KeyGuardian;
                        SpawnLadderToChest(52, 21, grid);

                        // Blue Chest in Top-Left Chamber (x = 28, y = 20)
                        grid[28, 21] = CellType.BlueChest;
                        SpawnLadderToChest(28, 21, grid);

                        // Yellow Chest (Gold Chest) in Entry Chamber (x = 8, y = 2)
                        grid[8, 2] = CellType.YellowChest;
                        SpawnLadderToChest(8, 2, grid);
                    }
                    else
                    {
                        if (roomId == 50)
                        {
                            grid[bossPos.x, bossPos.y] = CellType.Boss;
                            grid[bossPos.x + 8, bossPos.y] = CellType.Princess;
                        }
                        else
                        {
                            grid[bossPos.x, bossPos.y] = CellType.Boss;
                        }

                        if (roomId == 20)
                        {
                            // Map 20 Custom Chest Distribution inside accessible paths
                            // 1. Blue Chest on Floor 1 flat ground walkway
                            grid[25, 3] = CellType.BlueChest;
                            SpawnLadderToChest(25, 3, grid);

                            // 2. Yellow Chest (Gold) in Floor 2 Dead-End Chamber (entered from left shaft)
                            grid[42, 10] = CellType.YellowChest;
                            SpawnLadderToChest(42, 10, grid);

                            // 3. Key Chest in Boss Arena
                            grid[95, 10] = CellType.KeyChest;
                            SpawnLadderToChest(95, 10, grid);
                        }
                        else if (roomId == 30 || roomId == 40 || roomId == 50)
                        {
                            // Map 30, Map 40 & Map 50 Custom Chest Distribution inside accessible paths (to keep them in different locations)
                            // 1. Blue Chest on Floor 3 (Top Floor) walkway (around x = 30)
                            grid[30, 22] = CellType.BlueChest;
                            SpawnLadderToChest(30, 22, grid);

                            // 2. Yellow Chest (Gold) in Floor 2 Dead-End Chamber (around x = 25)
                            grid[25, 10] = CellType.YellowChest;
                            SpawnLadderToChest(25, 10, grid);

                            // 3. Key Chest in Boss Arena (far right side, away from the boss)
                            grid[102, bossPos.y] = CellType.KeyChest;
                            SpawnLadderToChest(102, bossPos.y, grid);
                        }
                        else
                        {
                            // Always spawn all 3 chests in every boss room
                            grid[bossPos.x - 4, bossPos.y] = CellType.KeyChest;
                            grid[bossPos.x - 2, bossPos.y] = CellType.YellowChest;
                            grid[bossPos.x - 3, bossPos.y] = CellType.BlueChest;

                            SpawnLadderToChest(bossPos.x - 4, bossPos.y, grid);
                            SpawnLadderToChest(bossPos.x - 2, bossPos.y, grid);
                            SpawnLadderToChest(bossPos.x - 3, bossPos.y, grid);
                        }
                    }
                }
                else
                {
                    // Non-Boss Room: Place chests, spikes, and enemies
                    // Distribute chests to completely unique chambers to prevent overlapping/stacking
                    
                    int safePoolLeft = poolCh.cx - poolCh.rx + 2;
                    int safePoolRight = poolCh.cx + poolCh.rx - 2;

                    if (roomId == 3)
                    {
                        // Manual layout and chest override for Room 3
                        
                        // Clean corridors and pathways first:
                        // Corridor from lake (x=55) to far right (x=95) at y=12
                        for (int px = 55; px <= 95; px++)
                        {
                            grid[px, 11] = CellType.Solid;
                            grid[px, 10] = CellType.Solid;
                            grid[px, 12] = CellType.Empty;
                            grid[px, 13] = CellType.Empty;
                            grid[px, 14] = CellType.Empty;
                            if (currentCellStyles != null)
                            {
                                currentCellStyles[px, 11] = RoomStyle.DeepUnderground;
                                currentCellStyles[px, 10] = RoomStyle.DeepUnderground;
                            }
                        }

                        // Vertical shaft with ladder at x = 80 connecting y=11 to y=22
                        for (int py = 11; py <= 22; py++)
                        {
                            grid[80, py] = CellType.Ladder;
                            grid[79, py] = CellType.Empty;
                            grid[81, py] = CellType.Empty;
                        }

                        // Key Chest platform and corridor at y=22
                        for (int px = 70; px <= 82; px++)
                        {
                            if (px >= 2 && px <= widthInt - 3)
                            {
                                grid[px, 21] = CellType.Solid;
                                grid[px, 20] = CellType.Solid;
                                grid[px, 22] = CellType.Empty;
                                grid[px, 23] = CellType.Empty;
                                grid[px, 24] = CellType.Empty;
                                if (currentCellStyles != null)
                                {
                                    currentCellStyles[px, 21] = RoomStyle.DeepUnderground;
                                    currentCellStyles[px, 20] = RoomStyle.DeepUnderground;
                                }
                            }
                        }

                        // Blue Chest platform at x=20, y=8
                        for (int px = 18; px <= 24; px++)
                        {
                            if (px >= 2 && px <= widthInt - 3)
                            {
                                grid[px, 7] = CellType.Solid;
                                grid[px, 6] = CellType.Solid;
                                grid[px, 8] = CellType.Empty;
                                grid[px, 9] = CellType.Empty;
                                if (currentCellStyles != null)
                                {
                                    currentCellStyles[px, 7] = RoomStyle.DeepUnderground;
                                    currentCellStyles[px, 6] = RoomStyle.DeepUnderground;
                                }
                            }
                        }

                        grid[76, 22] = CellType.KeyChest;
                        grid[72, 22] = CellType.KeyGuardian;
                        SpawnLadderToChest(76, 22, grid);

                        grid[20, 8] = CellType.BlueChest;
                        SpawnLadderToChest(20, 8, grid);

                        grid[88, 12] = CellType.YellowChest;
                        SpawnLadderToChest(88, 12, grid);
                    }
                    else if (roomId == 12)
                    {
                        grid[35, 9] = CellType.YellowChest;
                        SpawnLadderToChest(35, 9, grid);

                        grid[65, 15] = CellType.BlueChest;
                        SpawnLadderToChest(65, 15, grid);

                        grid[95, 21] = CellType.KeyChest;
                        grid[92, 21] = CellType.KeyGuardian;
                        SpawnLadderToChest(95, 21, grid);
                    }
                    else if (roomId == 13)
                    {
                        grid[35, 9] = CellType.YellowChest;
                        SpawnLadderToChest(35, 9, grid);

                        grid[65, 15] = CellType.BlueChest;
                        SpawnLadderToChest(65, 15, grid);

                        grid[95, 21] = CellType.KeyChest;
                        grid[92, 21] = CellType.KeyGuardian;
                        SpawnLadderToChest(95, 21, grid);
                    }
                    else if (roomId == 15)
                    {
                        grid[95, 15] = CellType.KeyChest;
                        grid[92, 15] = CellType.KeyGuardian;
                        SpawnLadderToChest(95, 15, grid);

                        // Spawn Gold Chest (YellowChest) dynamically in Floor 3 (Chamber 1)
                        if (chambers.Count > 1)
                        {
                            Vector2Int yPos = FindValidFloorPos(chambers[1], poolCh, grid, widthInt, height);
                            if (yPos.x > 0)
                            {
                                grid[yPos.x, yPos.y] = CellType.YellowChest;
                                if (currentCellStyles != null)
                                {
                                    if (yPos.y - 1 >= 0) currentCellStyles[yPos.x, yPos.y - 1] = RoomStyle.DeepUnderground;
                                    if (yPos.y - 2 >= 0) currentCellStyles[yPos.x, yPos.y - 2] = RoomStyle.DeepUnderground;
                                }
                                SpawnLadderToChest(yPos.x, yPos.y, grid);
                            }
                        }

                        // Spawn Item Chest (BlueChest) dynamically in Exit Chamber (Chamber 3)
                        if (progressionExitChamberIndex > 0)
                        {
                            Vector2Int bPos = FindValidFloorPos(chambers[progressionExitChamberIndex], poolCh, grid, widthInt, height);
                            if (bPos.x > 0)
                            {
                                grid[bPos.x, bPos.y] = CellType.BlueChest;
                                if (currentCellStyles != null)
                                {
                                    if (bPos.y - 1 >= 0) currentCellStyles[bPos.x, bPos.y - 1] = RoomStyle.DeepUnderground;
                                    if (bPos.y - 2 >= 0) currentCellStyles[bPos.x, bPos.y - 2] = RoomStyle.DeepUnderground;
                                }
                                SpawnLadderToChest(bPos.x, bPos.y, grid);
                            }
                        }
                    }
                    else if (roomId == 16)
                    {
                        // 1. Spawn Gold Chest (YellowChest) on Floor 3 (Top Floor) - x = 35, y = 21
                        grid[35, 21] = CellType.YellowChest;
                        SpawnLadderToChest(35, 21, grid);
                        if (currentCellStyles != null)
                        {
                            currentCellStyles[35, 20] = RoomStyle.DeepUnderground;
                            currentCellStyles[35, 19] = RoomStyle.DeepUnderground;
                        }

                        // 2. Get Exit Chamber metrics dynamically
                        Chamber exitCh = chambers[progressionExitChamberIndex];
                        int floorY = exitCh.cy - exitCh.ry;
                        
                        int blueChestX = exitCh.cx - 2;
                        int keyGuardianX = exitCh.cx;
                        int keyChestX = exitCh.cx + 2;
                        int entityY = Mathf.Max(3, floorY + 1);

                        // Spawn Item Chest (BlueChest)
                        grid[blueChestX, entityY] = CellType.BlueChest;
                        SpawnLadderToChest(blueChestX, entityY, grid);
                        if (currentCellStyles != null)
                        {
                            if (blueChestX >= 0 && blueChestX < widthInt)
                            {
                                if (entityY - 1 >= 0 && entityY - 1 < height) currentCellStyles[blueChestX, entityY - 1] = RoomStyle.DeepUnderground;
                                if (entityY - 2 >= 0 && entityY - 2 < height) currentCellStyles[blueChestX, entityY - 2] = RoomStyle.DeepUnderground;
                            }
                        }

                        // Spawn Purple Chest (KeyChest) and Key Guardian NPC
                        grid[keyChestX, entityY] = CellType.KeyChest;
                        grid[keyGuardianX, entityY] = CellType.KeyGuardian;
                        SpawnLadderToChest(keyChestX, entityY, grid);
                        if (currentCellStyles != null)
                        {
                            if (keyChestX >= 0 && keyChestX < widthInt)
                            {
                                if (entityY - 1 >= 0 && entityY - 1 < height) currentCellStyles[keyChestX, entityY - 1] = RoomStyle.DeepUnderground;
                                if (entityY - 2 >= 0 && entityY - 2 < height) currentCellStyles[keyChestX, entityY - 2] = RoomStyle.DeepUnderground;
                            }
                            if (keyGuardianX >= 0 && keyGuardianX < widthInt)
                            {
                                if (entityY - 1 >= 0 && entityY - 1 < height) currentCellStyles[keyGuardianX, entityY - 1] = RoomStyle.DeepUnderground;
                                if (entityY - 2 >= 0 && entityY - 2 < height) currentCellStyles[keyGuardianX, entityY - 2] = RoomStyle.DeepUnderground;
                            }
                        }
                    }
                    else if (roomId == 18)
                    {
                        // 1. Spawn Gold Chest (YellowChest) dynamically in Entry Chamber (Chamber 0)
                        Chamber entryCh = chambers[0];
                        int entryFloorY = entryCh.cy - entryCh.ry;
                        int goldChestX = entryCh.cx + 2;
                        int goldChestY = Mathf.Max(3, entryFloorY + 1);

                        grid[goldChestX, goldChestY] = CellType.YellowChest;
                        SpawnLadderToChest(goldChestX, goldChestY, grid);
                        if (currentCellStyles != null)
                        {
                            if (goldChestX >= 0 && goldChestX < widthInt)
                            {
                                if (goldChestY - 1 >= 0 && goldChestY - 1 < height) currentCellStyles[goldChestX, goldChestY - 1] = RoomStyle.DeepUnderground;
                                if (goldChestY - 2 >= 0 && goldChestY - 2 < height) currentCellStyles[goldChestX, goldChestY - 2] = RoomStyle.DeepUnderground;
                            }
                        }

                        // 2. Get Exit Chamber metrics dynamically
                        Chamber exitCh = chambers[progressionExitChamberIndex];
                        int floorY = exitCh.cy - exitCh.ry;
                        
                        int blueChestX = exitCh.cx - 2;
                        int keyGuardianX = exitCh.cx;
                        int keyChestX = exitCh.cx + 2;
                        int entityY = Mathf.Max(3, floorY + 1);

                        // Spawn Item Chest (BlueChest)
                        grid[blueChestX, entityY] = CellType.BlueChest;
                        SpawnLadderToChest(blueChestX, entityY, grid);
                        if (currentCellStyles != null)
                        {
                            if (blueChestX >= 0 && blueChestX < widthInt)
                            {
                                if (entityY - 1 >= 0 && entityY - 1 < height) currentCellStyles[blueChestX, entityY - 1] = RoomStyle.DeepUnderground;
                                if (entityY - 2 >= 0 && entityY - 2 < height) currentCellStyles[blueChestX, entityY - 2] = RoomStyle.DeepUnderground;
                            }
                        }

                        // Spawn Purple Chest (KeyChest) and Key Guardian NPC
                        grid[keyChestX, entityY] = CellType.KeyChest;
                        grid[keyGuardianX, entityY] = CellType.KeyGuardian;
                        SpawnLadderToChest(keyChestX, entityY, grid);
                        if (currentCellStyles != null)
                        {
                            if (keyChestX >= 0 && keyChestX < widthInt)
                            {
                                if (entityY - 1 >= 0 && entityY - 1 < height) currentCellStyles[keyChestX, entityY - 1] = RoomStyle.DeepUnderground;
                                if (entityY - 2 >= 0 && entityY - 2 < height) currentCellStyles[keyChestX, entityY - 2] = RoomStyle.DeepUnderground;
                            }
                            if (keyGuardianX >= 0 && keyGuardianX < widthInt)
                            {
                                if (entityY - 1 >= 0 && entityY - 1 < height) currentCellStyles[keyGuardianX, entityY - 1] = RoomStyle.DeepUnderground;
                                if (entityY - 2 >= 0 && entityY - 2 < height) currentCellStyles[keyGuardianX, entityY - 2] = RoomStyle.DeepUnderground;
                            }
                        }
                    }
                    else
                    {
                        // 1. Key Chest & Key Guardian in a Dead-End Chamber (Last Chamber in list)
                        // Allowed to spawn in all rooms (including Room 1) as requested.
                        if (roomId == 24)
                        {
                            // Map 24 Custom Safe Placement for Key Chest and Key Guardian on the left flat shore (not inside lake!)
                            grid[23, 11] = CellType.KeyChest;
                            grid[20, 11] = CellType.KeyGuardian;
                            SpawnLadderToChest(23, 11, grid);

                            // Force dry, solid platform tiles under them
                            grid[23, 10] = CellType.Solid;
                            grid[20, 10] = CellType.Solid;
                            grid[21, 10] = CellType.Solid;
                            grid[22, 10] = CellType.Solid;
                        }
                        else if (roomId == 4)
                        {
                            // Manual placement of Key Chest and Key Guardian inside the new Room 4 chamber
                            grid[95, 18] = CellType.KeyChest;
                            grid[92, 18] = CellType.KeyGuardian;
                            
                            // Style overrides for the platform
                            for (int px = 90; px <= 100; px++)
                            {
                                if (currentCellStyles != null)
                                {
                                    currentCellStyles[px, 17] = RoomStyle.DeepUnderground;
                                    currentCellStyles[px, 16] = RoomStyle.DeepUnderground;
                                }
                            }
                        }
                        else if (roomId == 35)
                        {
                            Chamber highCh = highestCh;
                            int targetY = highCh.cy - highCh.ry + 1;
                            int targetKeyX = highCh.cx + 3;
                            int targetGuardX = highCh.cx;

                            // Force dry, solid platform tiles under them
                            for (int px = targetGuardX - 2; px <= targetKeyX + 2; px++)
                            {
                                if (px >= 2 && px <= widthInt - 3)
                                {
                                    grid[px, targetY - 1] = CellType.Solid;
                                    grid[px, targetY - 2] = CellType.Solid;
                                    grid[px, targetY] = CellType.Empty;
                                    grid[px, targetY + 1] = CellType.Empty;
                                    grid[px, targetY + 2] = CellType.Empty;
                                    if (currentCellStyles != null)
                                    {
                                        currentCellStyles[px, targetY - 1] = RoomStyle.DeepUnderground;
                                        currentCellStyles[px, targetY - 2] = RoomStyle.DeepUnderground;
                                    }
                                }
                            }

                            grid[targetKeyX, targetY] = CellType.KeyChest;
                            grid[targetGuardX, targetY] = CellType.KeyGuardian;
                            SpawnLadderToChest(targetKeyX, targetY, grid);
                        }
                        else if (roomId == 38)
                        {
                            Chamber highCh = highestCh;
                            int targetY = highCh.cy - highCh.ry + 1;
                            int targetKeyX = highCh.cx + 3;
                            int targetGuardX = highCh.cx;

                            // Force dry, solid platform tiles under them
                            for (int px = targetGuardX - 2; px <= targetKeyX + 2; px++)
                            {
                                if (px >= 2 && px <= widthInt - 3)
                                {
                                    grid[px, targetY - 1] = CellType.Solid;
                                    grid[px, targetY - 2] = CellType.Solid;
                                    grid[px, targetY] = CellType.Empty;
                                    grid[px, targetY + 1] = CellType.Empty;
                                    grid[px, targetY + 2] = CellType.Empty;
                                    if (currentCellStyles != null)
                                    {
                                        currentCellStyles[px, targetY - 1] = RoomStyle.DeepUnderground;
                                        currentCellStyles[px, targetY - 2] = RoomStyle.DeepUnderground;
                                    }
                                }
                            }

                            grid[targetKeyX, targetY] = CellType.KeyChest;
                            grid[targetGuardX, targetY] = CellType.KeyGuardian;
                            SpawnLadderToChest(targetKeyX, targetY, grid);
                        }
                        else if (roomId == 42)
                        {
                            Chamber highCh = highestCh;
                            int targetY = highCh.cy - highCh.ry + 1;
                            int targetKeyX = highCh.cx + 3;
                            int targetGuardX = highCh.cx;

                            // Force dry, solid platform tiles under them
                            for (int px = targetGuardX - 2; px <= targetKeyX + 2; px++)
                            {
                                if (px >= 2 && px <= widthInt - 3)
                                {
                                    if (targetY - 1 >= 0 && targetY - 1 < height) grid[px, targetY - 1] = CellType.Solid;
                                    if (targetY - 2 >= 0 && targetY - 2 < height) grid[px, targetY - 2] = CellType.Solid;
                                    if (targetY >= 0 && targetY < height) grid[px, targetY] = CellType.Empty;
                                    if (targetY + 1 >= 0 && targetY + 1 < height) grid[px, targetY + 1] = CellType.Empty;
                                    if (targetY + 2 >= 0 && targetY + 2 < height) grid[px, targetY + 2] = CellType.Empty;
                                    if (currentCellStyles != null)
                                    {
                                        if (targetY - 1 >= 0 && targetY - 1 < height) currentCellStyles[px, targetY - 1] = RoomStyle.DeepUnderground;
                                        if (targetY - 2 >= 0 && targetY - 2 < height) currentCellStyles[px, targetY - 2] = RoomStyle.DeepUnderground;
                                    }
                                }
                            }

                            if (targetKeyX >= 0 && targetKeyX < widthInt && targetY >= 0 && targetY < height)
                                grid[targetKeyX, targetY] = CellType.KeyChest;
                            if (targetGuardX >= 0 && targetGuardX < widthInt && targetY >= 0 && targetY < height)
                                grid[targetGuardX, targetY] = CellType.KeyGuardian;
                            if (targetKeyX >= 0 && targetKeyX < widthInt && targetY >= 0 && targetY < height)
                                SpawnLadderToChest(targetKeyX, targetY, grid);
                        }
                        else if (roomId == 48)
                        {
                            Chamber highCh = highestCh;
                            int targetY = highCh.cy - highCh.ry + 1;
                            int targetKeyX = highCh.cx + 3;
                            int targetGuardX = highCh.cx;

                            // Force dry, solid platform tiles under them
                            for (int px = targetGuardX - 2; px <= targetKeyX + 2; px++)
                            {
                                if (px >= 2 && px <= widthInt - 3)
                                {
                                    if (targetY - 1 >= 0 && targetY - 1 < height) grid[px, targetY - 1] = CellType.Solid;
                                    if (targetY - 2 >= 0 && targetY - 2 < height) grid[px, targetY - 2] = CellType.Solid;
                                    if (targetY >= 0 && targetY < height) grid[px, targetY] = CellType.Empty;
                                    if (targetY + 1 >= 0 && targetY + 1 < height) grid[px, targetY + 1] = CellType.Empty;
                                    if (targetY + 2 >= 0 && targetY + 2 < height) grid[px, targetY + 2] = CellType.Empty;
                                    if (currentCellStyles != null)
                                    {
                                        if (targetY - 1 >= 0 && targetY - 1 < height) currentCellStyles[px, targetY - 1] = RoomStyle.DeepUnderground;
                                        if (targetY - 2 >= 0 && targetY - 2 < height) currentCellStyles[px, targetY - 2] = RoomStyle.DeepUnderground;
                                    }
                                }
                            }

                            if (targetKeyX >= 0 && targetKeyX < widthInt && targetY >= 0 && targetY < height)
                                grid[targetKeyX, targetY] = CellType.KeyChest;
                            if (targetGuardX >= 0 && targetGuardX < widthInt && targetY >= 0 && targetY < height)
                                grid[targetGuardX, targetY] = CellType.KeyGuardian;
                            if (targetKeyX >= 0 && targetKeyX < widthInt && targetY >= 0 && targetY < height)
                                SpawnLadderToChest(targetKeyX, targetY, grid);
                        }
                        else
                        {
                            int keyChIdx = chambers.Count - 1;
                            // Select a dead-end chamber that is not the pool chamber to prevent overlap
                            for (int cIdx = chambers.Count - 1; cIdx >= 0; cIdx--)
                            {
                                if (chambers[cIdx].cx != poolCh.cx || chambers[cIdx].cy != poolCh.cy)
                                {
                                    keyChIdx = cIdx;
                                    break;
                                }
                            }
                            // Find a valid flat floor of size >= 3 (never a single block)
                            Vector2Int keyPos = FindValidFloorPos(chambers[keyChIdx], poolCh, grid, widthInt, height);

                            // Decide whether to place guardian on the left (-3) or right (+3)
                            int guardX = keyPos.x - 3;
                            bool isPoolCh = (chambers[keyChIdx].cx == poolCh.cx && chambers[keyChIdx].cy == poolCh.cy && poolCh.rx > 0);

                            // Left placement is valid if it doesn't cross the left room boundary and is not inside the pool
                            bool leftValid = (guardX >= 2 && (!isPoolCh || guardX < safePoolLeft || guardX > safePoolRight));
                            if (!leftValid)
                            {
                                guardX = keyPos.x + 3; // Fallback to right side
                            }

                            // Force a robust, solid, flat platform under both the Key Chest and the Key Guardian
                            // to guarantee they are on flat ground, not in pits, and not at a cliff edge.
                            // We extend the platform 1 block past the leftmost and rightmost entity for safety.
                            int minPlatformX = Mathf.Min(keyPos.x, guardX) - 1;
                            int maxPlatformX = Mathf.Max(keyPos.x, guardX) + 1;

                            for (int px = minPlatformX; px <= maxPlatformX; px++)
                            {
                                if (px >= 2 && px <= widthInt - 3)
                                {
                                    if (keyPos.y - 1 >= 0 && grid[px, keyPos.y - 1] != CellType.Ladder) grid[px, keyPos.y - 1] = CellType.Solid;
                                    if (keyPos.y - 2 >= 0 && grid[px, keyPos.y - 2] != CellType.Ladder) grid[px, keyPos.y - 2] = CellType.Solid;
                                    if (keyPos.y >= 0 && keyPos.y < height && grid[px, keyPos.y] != CellType.Ladder) grid[px, keyPos.y] = CellType.Empty;
                                    if (keyPos.y + 1 < height && grid[px, keyPos.y + 1] != CellType.Ladder) grid[px, keyPos.y + 1] = CellType.Empty;
                                    if (keyPos.y + 2 < height && grid[px, keyPos.y + 2] != CellType.Ladder) grid[px, keyPos.y + 2] = CellType.Empty;
                                    if (keyPos.y + 3 < height && grid[px, keyPos.y + 3] != CellType.Ladder) grid[px, keyPos.y + 3] = CellType.Empty;

                                    if (currentCellStyles != null)
                                    {
                                        if (keyPos.y - 1 >= 0) currentCellStyles[px, keyPos.y - 1] = RoomStyle.DeepUnderground;
                                        if (keyPos.y - 2 >= 0) currentCellStyles[px, keyPos.y - 2] = RoomStyle.DeepUnderground;
                                    }
                                }
                            }

                            grid[keyPos.x, keyPos.y] = CellType.KeyChest;
                            grid[guardX, keyPos.y] = CellType.KeyGuardian;
                            SpawnLadderToChest(keyPos.x, keyPos.y, grid);
                        }

                        // 2. Yellow Chest in Chamber 1 (Second Chamber of progression sequence)
                        if (roomId == 24)
                        {
                            // Place Yellow Chest (Gold Chest) on Floor 3 (Top Floor)
                            grid[33, 21] = CellType.YellowChest;
                            SpawnLadderToChest(33, 21, grid);
                            if (currentCellStyles != null)
                            {
                                currentCellStyles[33, 20] = RoomStyle.DeepUnderground;
                                currentCellStyles[33, 19] = RoomStyle.DeepUnderground;
                            }
                        }
                        else if (roomId == 7 || roomId == 8)
                        {
                            // Place the Gold Chest on its new safe platform
                            grid[15, 4] = CellType.YellowChest;
                            SpawnLadderToChest(15, 4, grid);
                        }
                        else if (roomId == 2)
                        {
                            // Place Yellow Chest (Gold Chest) in a safe, guaranteed place with a solid platform
                            for (int px = 30; px <= 34; px++)
                            {
                                if (px >= 2 && px <= widthInt - 3)
                                {
                                    grid[px, 11] = CellType.Solid;
                                    grid[px, 10] = CellType.Solid;
                                    grid[px, 12] = CellType.Empty;
                                    grid[px, 13] = CellType.Empty;
                                    if (currentCellStyles != null)
                                    {
                                        currentCellStyles[px, 11] = RoomStyle.DeepUnderground;
                                        currentCellStyles[px, 10] = RoomStyle.DeepUnderground;
                                    }
                                }
                            }
                            grid[32, 12] = CellType.YellowChest;
                            SpawnLadderToChest(32, 12, grid);
                        }
                        else if (roomId == 48)
                        {
                            Chamber targetCh = chambers[0];
                            int targetY = targetCh.cy - targetCh.ry + 1;
                            int chestX = targetCh.cx + 2;

                            for (int px = chestX - 1; px <= chestX + 1; px++)
                            {
                                if (px >= 2 && px <= widthInt - 3)
                                {
                                    if (targetY - 1 >= 0 && targetY - 1 < height) grid[px, targetY - 1] = CellType.Solid;
                                    if (targetY - 2 >= 0 && targetY - 2 < height) grid[px, targetY - 2] = CellType.Solid;
                                    if (targetY >= 0 && targetY < height) grid[px, targetY] = CellType.Empty;
                                    if (targetY + 1 >= 0 && targetY + 1 < height) grid[px, targetY + 1] = CellType.Empty;
                                    if (currentCellStyles != null)
                                    {
                                        if (targetY - 1 >= 0 && targetY - 1 < height) currentCellStyles[px, targetY - 1] = RoomStyle.DeepUnderground;
                                        if (targetY - 2 >= 0 && targetY - 2 < height) currentCellStyles[px, targetY - 2] = RoomStyle.DeepUnderground;
                                    }
                                }
                            }
                            if (chestX >= 0 && chestX < widthInt && targetY >= 0 && targetY < height)
                            {
                                grid[chestX, targetY] = CellType.YellowChest;
                                SpawnLadderToChest(chestX, targetY, grid);
                            }
                        }
                        else if (chambers.Count > 1)
                        {
                            Vector2Int yPos = FindValidFloorPos(chambers[1], poolCh, grid, widthInt, height);
                            grid[yPos.x, yPos.y] = CellType.YellowChest;
                            if (currentCellStyles != null)
                            {
                                if (yPos.y - 1 >= 0) currentCellStyles[yPos.x, yPos.y - 1] = RoomStyle.DeepUnderground;
                                if (yPos.y - 2 >= 0) currentCellStyles[yPos.x, yPos.y - 2] = RoomStyle.DeepUnderground;
                            }
                            SpawnLadderToChest(yPos.x, yPos.y, grid);
                        }

                        // 3. Blue Chest in last chamber of progression sequence (progressionExitChamberIndex)
                        if (roomId == 24)
                        {
                            // Place Blue Chest in exit chamber on the right (safe and dry)
                            grid[70, 11] = CellType.BlueChest;
                            SpawnLadderToChest(70, 11, grid);
                            if (currentCellStyles != null)
                            {
                                currentCellStyles[70, 10] = RoomStyle.DeepUnderground;
                                currentCellStyles[70, 9] = RoomStyle.DeepUnderground;
                            }
                        }
                        else if (roomId == 6)
                        {
                            // Find the ladder's X coordinate dynamically
                            int ladderX = 50;
                            for (int x = 20; x < widthInt - 20; x++)
                            {
                                bool isLadderCol = false;
                                for (int y = 2; y < height - 2; y++)
                                {
                                    if (grid[x, y] == CellType.Ladder)
                                    {
                                        isLadderCol = true;
                                        break;
                                    }
                                }
                                if (isLadderCol)
                                {
                                    ladderX = x;
                                    break;
                                }
                            }
                            
                            // Place Blue Chest on the left hanging platform
                            int chestX = ladderX - 8;
                            grid[chestX, 12] = CellType.BlueChest;
                            SpawnLadderToChest(chestX, 12, grid);
                        }
                        else if (roomId == 7 || roomId == 8)
                        {
                            // Place Blue Chest on the middle-right platform
                            grid[88, 12] = CellType.BlueChest;
                            SpawnLadderToChest(88, 12, grid);
                        }
                        else if (roomId == 48)
                        {
                            Chamber targetCh = chambers[progressionExitChamberIndex];
                            int targetY = targetCh.cy - targetCh.ry + 1;
                            int chestX = targetCh.cx - 2;

                            for (int px = chestX - 1; px <= chestX + 1; px++)
                            {
                                if (px >= 2 && px <= widthInt - 3)
                                {
                                    if (targetY - 1 >= 0 && targetY - 1 < height) grid[px, targetY - 1] = CellType.Solid;
                                    if (targetY - 2 >= 0 && targetY - 2 < height) grid[px, targetY - 2] = CellType.Solid;
                                    if (targetY >= 0 && targetY < height) grid[px, targetY] = CellType.Empty;
                                    if (targetY + 1 >= 0 && targetY + 1 < height) grid[px, targetY + 1] = CellType.Empty;
                                    if (currentCellStyles != null)
                                    {
                                        if (targetY - 1 >= 0 && targetY - 1 < height) currentCellStyles[px, targetY - 1] = RoomStyle.DeepUnderground;
                                        if (targetY - 2 >= 0 && targetY - 2 < height) currentCellStyles[px, targetY - 2] = RoomStyle.DeepUnderground;
                                    }
                                }
                            }
                            if (chestX >= 0 && chestX < widthInt && targetY >= 0 && targetY < height)
                            {
                                grid[chestX, targetY] = CellType.BlueChest;
                                SpawnLadderToChest(chestX, targetY, grid);
                            }
                        }
                        else if (progressionExitChamberIndex > 0)
                        {
                            Vector2Int bPos = FindValidFloorPos(chambers[progressionExitChamberIndex], poolCh, grid, widthInt, height);
                            grid[bPos.x, bPos.y] = CellType.BlueChest;
                            if (currentCellStyles != null)
                            {
                                if (bPos.y - 1 >= 0) currentCellStyles[bPos.x, bPos.y - 1] = RoomStyle.DeepUnderground;
                                if (bPos.y - 2 >= 0) currentCellStyles[bPos.x, bPos.y - 2] = RoomStyle.DeepUnderground;
                            }
                            SpawnLadderToChest(bPos.x, bPos.y, grid);
                        }
                    }

                }

                int poolLeft = poolCh.cx - poolCh.rx + 2;
                int poolRight = poolCh.cx + poolCh.rx - 2;
                int poolBottomY = Mathf.Clamp(poolCh.cy - poolCh.ry, 2, height - 3);

                if (roomId != 30 && roomId != 40)
                {
                    bool isLava = (Random.value > 0.5f);
                    CellType poolType = isLava ? CellType.Lava : CellType.Water;

                    // Build a U-shaped solid stone/dirt wall around the water/lava lake so it is inside a pool container
                    // Protect key chest, boss, princess, and guardian from being overwritten by solid pool walls.
                    for (int x = poolLeft - 1; x <= poolRight + 1; x++)
                    {
                        if (x > 1 && x < widthInt - 2)
                        {
                            if (grid[x, poolBottomY - 1] != CellType.KeyChest && 
                                grid[x, poolBottomY - 1] != CellType.KeyGuardian &&
                                grid[x, poolBottomY - 1] != CellType.Boss &&
                                grid[x, poolBottomY - 1] != CellType.Princess &&
                                grid[x, poolBottomY - 1] != CellType.Ladder)
                            {
                                grid[x, poolBottomY - 1] = CellType.Solid;
                            }
                        }
                    }
                    for (int y = poolBottomY; y <= poolBottomY + 2; y++)
                    {
                        if (poolLeft - 1 > 1)
                        {
                            if (grid[poolLeft - 1, y] != CellType.KeyChest && 
                                grid[poolLeft - 1, y] != CellType.KeyGuardian &&
                                grid[poolLeft - 1, y] != CellType.Boss &&
                                grid[poolLeft - 1, y] != CellType.Princess &&
                                grid[poolLeft - 1, y] != CellType.Ladder)
                            {
                                grid[poolLeft - 1, y] = CellType.Solid;
                            }
                        }
                        if (poolRight + 1 < widthInt - 2)
                        {
                            if (grid[poolRight + 1, y] != CellType.KeyChest && 
                                grid[poolRight + 1, y] != CellType.KeyGuardian &&
                                grid[poolRight + 1, y] != CellType.Boss &&
                                grid[poolRight + 1, y] != CellType.Princess &&
                                grid[poolRight + 1, y] != CellType.Ladder)
                            {
                                grid[poolRight + 1, y] = CellType.Solid;
                            }
                        }
                    }

                    for (int x = poolLeft; x <= poolRight; x++)
                    {
                        if (x > 1 && x < widthInt - 2)
                        {
                            for (int py = 0; py <= 2; py++)
                            {
                                int cy = poolBottomY + py;
                                if (grid[x, cy] != CellType.KeyChest &&
                                    grid[x, cy] != CellType.YellowChest &&
                                    grid[x, cy] != CellType.BlueChest &&
                                    grid[x, cy] != CellType.KeyGuardian &&
                                    grid[x, cy] != CellType.Boss &&
                                    grid[x, cy] != CellType.Princess)
                                {
                                    if ((roomId == 21 || roomId == 22) && py == 2)
                                    {
                                        grid[x, cy] = CellType.Solid; // Draw a bridge path over the lake on Map 21 and Map 22
                                    }
                                    else
                                    {
                                        grid[x, cy] = poolType;
                                    }
                                }
                            }
                            // Add pool x coordinates to poolCoords for backgrounds
                            poolCoords.Add(startX + x);
                        }
                    }
                }

                // Spawn a solid 2x2 jumping stepping stone pillar in the middle of wide pools (exclude room 23, 24, 25, 26, 29, 31, 35, 37, 39, 41, and 48)
                int poolMidX = poolCh.cx;
                if (roomId != 23 && roomId != 24 && roomId != 25 && roomId != 26 && roomId != 29 && roomId != 31 && roomId != 35 && roomId != 37 && roomId != 39 && roomId != 41 && roomId != 48 && poolCh.rx >= 5 && poolMidX > 5 && poolMidX < widthInt - 5)
                {
                    for (int dx = -1; dx <= 0; dx++)
                    {
                        for (int dy = 0; dy <= 1; dy++)
                        {
                            int cx = poolMidX + dx;
                            int cy = poolBottomY + dy;
                            if (grid[cx, cy] != CellType.KeyChest &&
                                grid[cx, cy] != CellType.KeyGuardian &&
                                grid[cx, cy] != CellType.Boss &&
                                grid[cx, cy] != CellType.Princess &&
                                grid[cx, cy] != CellType.Ladder)
                            {
                                grid[cx, cy] = CellType.Solid;
                            }
                        }
                    }
                }

                // Map 23: Draw custom floating 2-tile wide step platforms descending to the right over the lake
                if (roomId == 23)
                {
                    int currentHeight = poolBottomY + 5;
                    int sx = poolLeft;
                    while (sx <= poolRight)
                    {
                        grid[sx, currentHeight] = CellType.Solid;
                        if (sx + 1 <= poolRight)
                        {
                            grid[sx + 1, currentHeight] = CellType.Solid;
                        }

                        // Clear headroom above platforms
                        for (int dy = 1; dy <= 3; dy++)
                        {
                            if (currentHeight + dy < height)
                            {
                                grid[sx, currentHeight + dy] = CellType.Empty;
                                if (sx + 1 <= poolRight)
                                {
                                    grid[sx + 1, currentHeight + dy] = CellType.Empty;
                                }
                            }
                        }

                        sx += 4; // 2-wide platform + 2-wide gap
                        currentHeight--; // Descend step-by-step
                        if (currentHeight < poolBottomY + 1)
                        {
                            currentHeight = poolBottomY + 1;
                        }
                    }
                }

                // Map 24: Draw custom flat 2-tile wide floating platforms over the lake
                if (roomId == 24)
                {
                    int currentHeight = poolBottomY + 2;
                    int sx = poolLeft;
                    while (sx <= poolRight)
                    {
                        grid[sx, currentHeight] = CellType.Solid;
                        if (sx + 1 <= poolRight)
                        {
                            grid[sx + 1, currentHeight] = CellType.Solid;
                        }

                        // Clear headroom above platforms
                        for (int dy = 1; dy <= 3; dy++)
                        {
                            if (currentHeight + dy < height)
                            {
                                grid[sx, currentHeight + dy] = CellType.Empty;
                                if (sx + 1 <= poolRight)
                                {
                                    grid[sx + 1, currentHeight + dy] = CellType.Empty;
                                }
                            }
                        }

                        sx += 4; // 2-wide platform + 2-wide gap
                    }
                }

                // Map 25: Draw custom flat 2-tile wide floating platforms over the lava lake
                if (roomId == 25)
                {
                    int currentHeight = poolBottomY + 2;
                    int sx = poolLeft;
                    while (sx <= poolRight)
                    {
                        grid[sx, currentHeight] = CellType.Solid;
                        if (sx + 1 <= poolRight)
                        {
                            grid[sx + 1, currentHeight] = CellType.Solid;
                        }

                        // Clear headroom above platforms
                        for (int dy = 1; dy <= 3; dy++)
                        {
                            if (currentHeight + dy < height)
                            {
                                grid[sx, currentHeight + dy] = CellType.Empty;
                                if (sx + 1 <= poolRight)
                                {
                                    grid[sx + 1, currentHeight + dy] = CellType.Empty;
                                }
                            }
                        }

                        sx += 4; // 2-wide platform + 2-wide gap
                    }
                }

                // Map 26: Draw custom flat 2-tile wide floating platforms over the lake
                if (roomId == 26)
                {
                    int currentHeight = poolBottomY + 2;
                    int sx = poolLeft;
                    while (sx <= poolRight)
                    {
                        grid[sx, currentHeight] = CellType.Solid;
                        if (sx + 1 <= poolRight)
                        {
                            grid[sx + 1, currentHeight] = CellType.Solid;
                        }

                        // Clear headroom above platforms
                        for (int dy = 1; dy <= 3; dy++)
                        {
                            if (currentHeight + dy < height)
                            {
                                grid[sx, currentHeight + dy] = CellType.Empty;
                                if (sx + 1 <= poolRight)
                                {
                                    grid[sx + 1, currentHeight + dy] = CellType.Empty;
                                }
                            }
                        }

                        sx += 4; // 2-wide platform + 2-wide gap
                    }
                }

                // Map 29: Draw custom flat 2-tile wide floating platforms over the lava lake
                if (roomId == 29)
                {
                    int currentHeight = poolBottomY + 2;
                    int sx = poolLeft;
                    while (sx <= poolRight)
                    {
                        grid[sx, currentHeight] = CellType.Solid;
                        if (sx + 1 <= poolRight)
                        {
                            grid[sx + 1, currentHeight] = CellType.Solid;
                        }

                        // Clear headroom above platforms
                        for (int dy = 1; dy <= 3; dy++)
                        {
                            if (currentHeight + dy < height)
                            {
                                grid[sx, currentHeight + dy] = CellType.Empty;
                                if (sx + 1 <= poolRight)
                                {
                                    grid[sx + 1, currentHeight + dy] = CellType.Empty;
                                }
                            }
                        }

                        sx += 4; // 2-wide platform + 2-wide gap
                    }
                }

                // Map 31: Draw custom flat 2-tile wide floating platforms over the lava lake
                if (roomId == 31)
                {
                    int currentHeight = poolBottomY + 2;
                    int sx = poolLeft;
                    while (sx <= poolRight)
                    {
                        grid[sx, currentHeight] = CellType.Solid;
                        if (sx + 1 <= poolRight)
                        {
                            grid[sx + 1, currentHeight] = CellType.Solid;
                        }

                        // Clear headroom above platforms
                        for (int dy = 1; dy <= 3; dy++)
                        {
                            if (currentHeight + dy < height)
                            {
                                grid[sx, currentHeight + dy] = CellType.Empty;
                                if (sx + 1 <= poolRight)
                                {
                                    grid[sx + 1, currentHeight + dy] = CellType.Empty;
                                }
                            }
                        }

                        sx += 4; // 2-wide platform + 2-wide gap
                    }
                }

                // Map 35: Draw custom flat 2-tile wide floating platforms over the lake
                if (roomId == 35)
                {
                    int currentHeight = poolBottomY + 2;
                    int sx = poolLeft;
                    while (sx <= poolRight)
                    {
                        grid[sx, currentHeight] = CellType.Solid;
                        if (sx + 1 <= poolRight)
                        {
                            grid[sx + 1, currentHeight] = CellType.Solid;
                        }

                        // Clear headroom above platforms
                        for (int dy = 1; dy <= 3; dy++)
                        {
                            if (currentHeight + dy < height)
                            {
                                grid[sx, currentHeight + dy] = CellType.Empty;
                                if (sx + 1 <= poolRight)
                                {
                                    grid[sx + 1, currentHeight + dy] = CellType.Empty;
                                }
                            }
                        }

                        sx += 4; // 2-wide platform + 2-wide gap
                    }
                }

                // Map 37: Draw custom flat 2-tile wide floating platforms over the lake
                if (roomId == 37)
                {
                    int currentHeight = poolBottomY + 2;
                    int sx = poolLeft;
                    while (sx <= poolRight)
                    {
                        grid[sx, currentHeight] = CellType.Solid;
                        if (sx + 1 <= poolRight)
                        {
                            grid[sx + 1, currentHeight] = CellType.Solid;
                        }

                        // Clear headroom above platforms
                        for (int dy = 1; dy <= 3; dy++)
                        {
                            if (currentHeight + dy < height)
                            {
                                grid[sx, currentHeight + dy] = CellType.Empty;
                                if (sx + 1 <= poolRight)
                                {
                                    grid[sx + 1, currentHeight + dy] = CellType.Empty;
                                }
                            }
                        }

                        sx += 4; // 2-wide platform + 2-wide gap
                    }
                }

                // Map 39: Draw custom flat 2-tile wide floating platforms over the lake
                if (roomId == 39)
                {
                    int currentHeight = poolBottomY + 2;
                    int sx = poolLeft;
                    while (sx <= poolRight)
                    {
                        grid[sx, currentHeight] = CellType.Solid;
                        if (sx + 1 <= poolRight)
                        {
                            grid[sx + 1, currentHeight] = CellType.Solid;
                        }

                        // Clear headroom above platforms
                        for (int dy = 1; dy <= 3; dy++)
                        {
                            if (currentHeight + dy < height)
                            {
                                grid[sx, currentHeight + dy] = CellType.Empty;
                                if (sx + 1 <= poolRight)
                                {
                                    grid[sx + 1, currentHeight + dy] = CellType.Empty;
                                }
                            }
                        }

                        sx += 4; // 2-wide platform + 2-wide gap
                    }
                }

                // Map 41 & Map 48: Draw custom flat 2-tile wide floating platforms over the lava lake
                if (roomId == 41 || roomId == 48)
                {
                    int currentHeight = poolBottomY + 2;
                    int sx = poolLeft;
                    while (sx <= poolRight)
                    {
                        grid[sx, currentHeight] = CellType.Solid;
                        if (sx + 1 <= poolRight)
                        {
                            grid[sx + 1, currentHeight] = CellType.Solid;
                        }

                        // Clear headroom above platforms
                        for (int dy = 1; dy <= 3; dy++)
                        {
                            if (currentHeight + dy < height)
                            {
                                grid[sx, currentHeight + dy] = CellType.Empty;
                                if (sx + 1 <= poolRight)
                                {
                                    grid[sx + 1, currentHeight + dy] = CellType.Empty;
                                }
                            }
                        }

                        sx += 4; // 2-wide platform + 2-wide gap
                    }
                }

                // Place a breakable wall blocking the entrance of the highest chamber
                int bWallX = highestCh.cx - highestCh.rx - 1;
                if (bWallX > 5 && bWallX < widthInt - 5)
                {
                    for (int y = highestCh.cy - highestCh.ry; y <= highestCh.cy + highestCh.ry; y++)
                    {
                        if (y > 1 && y < height - 2 && grid[bWallX, y] == CellType.Empty)
                        {
                            grid[bWallX, y] = CellType.BreakableWall;
                        }
                    }
                }

                // Place spikes
                for (int x = 10; x < widthInt - 10; x++)
                    {
                        for (int y = 2; y < height - 2; y++)
                        {
                            if (grid[x, y] == CellType.Empty && grid[x, y - 1] == CellType.Solid)
                            {
                                if (grid[x - 1, y] == CellType.Empty && grid[x + 1, y] == CellType.Empty)
                                {
                                    if (Random.value < 0.05f)
                                    {
                                        grid[x, y] = CellType.Spikes;
                                    }
                                }
                            }
                        }
                    }

                    // Gather and shuffle valid patrol positions (avoiding pool range)
                    System.Collections.Generic.List<Vector2Int> validPatrolPositions = new System.Collections.Generic.List<Vector2Int>();
                    poolLeft = poolCh.cx - poolCh.rx + 2;
                    poolRight = poolCh.cx + poolCh.rx - 2;
                    bool isPoolRoom = (poolCh.rx > 0);

                    // Prioritize 3-block wide, 2-block deep solid ground so they only spawn on robust floors (never thin floating platforms)
                    for (int x = 10; x < widthInt - 10; x++)
                    {
                        if (isPoolRoom && x >= poolLeft && x <= poolRight) continue;
                        if (roomId == 15 && x >= 24 && x <= 48) continue;

                        for (int y = 3; y < height - 2; y++)
                        {
                            if (roomId == 15 && y > 5 && x >= 20 && x <= 52) continue;

                            if (grid[x, y] == CellType.Empty && grid[x, y - 1] == CellType.Solid)
                            {
                                if (grid[x - 1, y] == CellType.Empty && grid[x + 1, y] == CellType.Empty &&
                                    grid[x - 1, y - 1] == CellType.Solid && grid[x + 1, y - 1] == CellType.Solid &&
                                    grid[x, y - 2] == CellType.Solid && grid[x - 1, y - 2] == CellType.Solid && grid[x + 1, y - 2] == CellType.Solid)
                                {
                                    validPatrolPositions.Add(new Vector2Int(x, y));
                                }
                            }
                        }
                    }

                    // Fallback: If no perfect positions found, allow 1-thick platform of size >= 3
                    if (validPatrolPositions.Count == 0)
                    {
                        for (int x = 10; x < widthInt - 10; x++)
                        {
                            if (isPoolRoom && x >= poolLeft && x <= poolRight) continue;
                            if (roomId == 15 && x >= 24 && x <= 48) continue;

                            for (int y = 2; y < height - 2; y++)
                            {
                                if (roomId == 15 && y > 5 && x >= 20 && x <= 52) continue;

                                if (grid[x, y] == CellType.Empty && grid[x, y - 1] == CellType.Solid)
                                {
                                    if (grid[x - 1, y] == CellType.Empty && grid[x + 1, y] == CellType.Empty &&
                                        grid[x - 1, y - 1] == CellType.Solid && grid[x + 1, y - 1] == CellType.Solid)
                                    {
                                        validPatrolPositions.Add(new Vector2Int(x, y));
                                    }
                                }
                            }
                        }
                    }

                    // Shuffle positions
                    for (int i = 0; i < validPatrolPositions.Count; i++)
                    {
                        Vector2Int temp = validPatrolPositions[i];
                        int randomIndex = Random.Range(i, validPatrolPositions.Count);
                        validPatrolPositions[i] = validPatrolPositions[randomIndex];
                        validPatrolPositions[randomIndex] = temp;
                    }

                    // Place exact level-based number of patrollers (GetExactMonsterCountForLevel(roomId) - 1)
                    int targetPatrollerCount = GetExactMonsterCountForLevel(roomId) - 1;
                    if (targetPatrollerCount < 0) targetPatrollerCount = 0;

                    int patrollersPlaced = 0;
                    for (int i = 0; i < validPatrolPositions.Count && patrollersPlaced < targetPatrollerCount; i++)
                    {
                        Vector2Int pos = validPatrolPositions[i];
                        grid[pos.x, pos.y] = CellType.Patroller;
                        patrollersPlaced++;
                    }

                }

            // Spawn background visual textures
            SpawnThematicBackgrounds(roomId, centerX, roomY, width, poolCoords, grid);

            // Spawn left and right boundary solid walls
            for (float y = roomY - 5f; y <= roomY + 13f; y += 1f)
            {
                SpawnWallTile(new Vector3(startX - 1f, y, 0f), biome);
                SpawnWallTile(new Vector3(endX, y, 0f), biome);
            }

            // SPAWNING PASS: Loop grid and instantiate all tiles and entities
            activeLadders.Clear();

            // First pass: Register and spawn vertical ladders
            for (int x = 0; x < widthInt; x++)
            {
                int ladderStart = -1;
                for (int y = 0; y < height; y++)
                {
                    if (grid[x, y] == CellType.Ladder)
                    {
                        if (ladderStart == -1) ladderStart = y;
                    }
                    else
                    {
                        if (ladderStart != -1)
                        {
                            float minY = roomY - 5f + ladderStart;
                            float maxY = roomY - 5f + y - 1;
                            activeLadders.Add(new LadderRange {
                                x = startX + x,
                                minY = minY,
                                maxY = maxY
                            });

                            float h = maxY - minY + 1f;
                            SpawnLadder(new Vector3(startX + x, minY + h / 2f, 0f), h);
                            ladderStart = -1;
                        }
                    }
                }
            }

            // Second pass: Spawn all block tiles and entities
            for (int x = 0; x < widthInt; x++)
            {
                float posX = startX + x;
                for (int y = 0; y < height; y++)
                {
                    float posY = roomY - 5f + y;
                    CellType type = grid[x, y];

                    if (type != CellType.Solid)
                    {
                        RoomStyle style = currentCellStyles[x, y];
                        if (style == RoomStyle.Cave || style == RoomStyle.DeepUnderground)
                        {
                            SpawnBackgroundWallTile(new Vector3(posX, posY, 0.5f), biome, style);
                        }
                    }

                    if (type == CellType.Solid)
                    {
                        // Ground tile is any solid cell with Empty air/ladder above it
                        bool isGround = (y < height - 1 && (grid[x, y + 1] == CellType.Empty || grid[x, y + 1] == CellType.Ladder || grid[x, y + 1] == CellType.Water || grid[x, y + 1] == CellType.Lava || grid[x, y + 1] == CellType.Spikes));
                        if (isGround)
                        {
                            SpawnGroundTile(new Vector3(posX, posY, 0f), biome);
                        }
                        else
                        {
                            // Optimize: Only spawn wall tiles exposed to a non-solid tile (8-way check to seal diagonals) to save GameObjects
                            bool isExposed = false;
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                for (int dy = -1; dy <= 1; dy++)
                                {
                                    if (dx == 0 && dy == 0) continue;
                                    int nx = x + dx;
                                    int ny = y + dy;
                                    if (nx >= 0 && nx < widthInt && ny >= 0 && ny < height)
                                    {
                                        if (grid[nx, ny] != CellType.Solid)
                                        {
                                            isExposed = true;
                                            break;
                                        }
                                    }
                                }
                                if (isExposed) break;
                            }

                            if (isExposed)
                            {
                                SpawnWallTile(new Vector3(posX, posY, 0f), biome);
                            }
                        }
                    }
                    else if (type == CellType.Water)
                    {
                        SpawnWaterTile(new Vector3(posX, posY, 0f));
                        // Check if ground needs to be spawned below the pool
                        if (y > 0 && grid[x, y - 1] == CellType.Solid)
                        {
                            SpawnGroundTile(new Vector3(posX, posY - 1f, 0f), biome);
                        }
                    }
                    else if (type == CellType.Lava)
                    {
                        SpawnLavaTile(new Vector3(posX, posY, 0f));
                        if (y > 0 && grid[x, y - 1] == CellType.Solid)
                        {
                            SpawnGroundTile(new Vector3(posX, posY - 1f, 0f), biome);
                        }
                    }
                    else if (type == CellType.Spikes)
                    {
                        SpawnSpikeTile(new Vector3(posX, posY, 0f));
                    }
                    else if (type == CellType.BreakableWall)
                    {
                        SpawnBreakableWall(new Vector3(posX, posY, 0f), biome);
                    }
                    else if (type == CellType.YellowChest)
                    {
                        if (rooms[roomId - 1].state != RoomState.Cleared)
                        {
                            SpawnNormalChest(new Vector3(posX, posY, 0f), PulsevaniaChest.ChestType.Yellow);
                        }
                    }
                    else if (type == CellType.BlueChest)
                    {
                        if (rooms[roomId - 1].state != RoomState.Cleared)
                        {
                            SpawnNormalChest(new Vector3(posX, posY, 0f), PulsevaniaChest.ChestType.Blue);
                        }
                    }
                    else if (type == CellType.KeyChest)
                    {
                        if (rooms[roomId - 1].state != RoomState.Cleared)
                        {
                            SpawnKeyChest(new Vector3(posX, posY, 0f));
                        }
                    }
                    else if (type == CellType.KeyGuardian)
                    {
                        if (rooms[roomId - 1].enemiesSpawned)
                        {
                            int hp = 15 + (roomId - 1) * 3;
                            int dmg = 4 + Mathf.RoundToInt((roomId - 1) * 0.6f);
                            float speed = 2.8f + (roomId - 1) * 0.03f;
                            EnemyGuardian.MonsterBehavior beh = EnemyGuardian.MonsterBehavior.ClubMelee;
                            
                            SpawnMonster(new Vector3(posX, posY + 0.25f, 0f), beh, hp, dmg, speed, 1f, roomId, true, posX + 4f);
                        }
                    }
                    else if (type == CellType.Patroller)
                    {
                        if (rooms[roomId - 1].enemiesSpawned)
                        {
                            int hp = 15 + (roomId - 1) * 3;
                            int dmg = 4 + Mathf.RoundToInt((roomId - 1) * 0.6f);
                            float speed = 2.8f + (roomId - 1) * 0.03f;
                            
                            int r = Random.Range(0, 3);
                            EnemyGuardian.MonsterBehavior beh = (EnemyGuardian.MonsterBehavior)r;
                            float spawnY = (beh == EnemyGuardian.MonsterBehavior.FlameMage) ? (posY + 0.8f) : (posY + 0.25f);
                            
                            SpawnMonster(new Vector3(posX, spawnY, 0f), beh, hp, dmg, speed, 1f, roomId, false, 0f);
                        }
                    }
                    else if (type == CellType.Boss)
                    {
                        if (rooms[roomId - 1].enemiesSpawned)
                        {
                            if (roomId == 10) SpawnMonster(new Vector3(posX, posY, 0f), EnemyGuardian.MonsterBehavior.Boss, 140, 5, 2.6f, 2.0f, 10);
                            else if (roomId == 20) SpawnMonster(new Vector3(posX, posY, 0f), EnemyGuardian.MonsterBehavior.Boss, 250, 8, 2.8f, 2.2f, 20);
                            else if (roomId == 30) SpawnMonster(new Vector3(posX, posY, 0f), EnemyGuardian.MonsterBehavior.Boss, 380, 11, 3.0f, 2.0f, 30); // Set scale to 2.0f
                            else if (roomId == 40) SpawnMonster(new Vector3(posX, posY, 0f), EnemyGuardian.MonsterBehavior.Boss, 550, 15, 3.2f, 1.0f, 40); // Set scale to 1.0f (same size as player)
                            else if (roomId == 50) SpawnMonster(new Vector3(posX, posY, 0f), EnemyGuardian.MonsterBehavior.Boss, 800, 20, 3.4f, 1.1f, 50, false, 0f);
                        }
                    }
                    else if (type == CellType.Princess)
                    {
                        SpawnPrincessInCage(posX, posY);
                    }
                    else if (type == CellType.Empty)
                    {
                        RoomStyle style = currentCellStyles[x, y];
                        // High-quality floor decorations (grass blades, wild flowers, urns, crystals, ice shards) on top of solid tiles
                        if (y > 0 && grid[x, y - 1] == CellType.Solid)
                        {
                            if (Random.value < 0.15f) // Lush 15% decoration density
                            {
                                SpawnFloorDecoration(new Vector3(posX, posY - 0.5f, 0f), biome, style);
                            }
                        }

                        // Ceiling decorations (stalactites)
                        if (style == RoomStyle.Cave && y < height - 1 && grid[x, y + 1] == CellType.Solid)
                        {
                            if (Random.value < 0.08f)
                            {
                                SpawnStalactite(new Vector3(posX, posY + 0.5f, 0f), biome);
                            }
                        }
                        else if (style == RoomStyle.DeepUnderground)
                        {
                            // Spaced torches on background wall
                            if (y == 5 && x % 9 == 4)
                            {
                                SpawnTorch(new Vector3(posX, posY, 0f));
                            }
                        }
                    }
                }
            }

            // Always spawn Entry and Exit doors at visible floor levels
            Chamber exitChamber = chambers[progressionExitChamberIndex];
            int doorExitCy = exitChamber.cy;
            if (roomId == 40)
            {
                int gridX = widthInt - 4;
                int searchY = height - 2;
                while (searchY >= 1 && grid[gridX, searchY] == CellType.Solid)
                {
                    searchY--;
                }
                int floorGridY = 1;
                for (int y = searchY; y >= 1; y--)
                {
                    if (grid[gridX, y] == CellType.Solid)
                    {
                        floorGridY = y + 1;
                        break;
                    }
                }
                doorExitCy = floorGridY + 1;
            }
            // Carve empty space in the grid around the exit door to prevent wall overlap
            for (int dy = -1; dy <= 1; dy++)
            {
                grid[widthInt - 3, doorExitCy + dy] = CellType.Empty;
                grid[widthInt - 4, doorExitCy + dy] = CellType.Empty;
                grid[widthInt - 5, doorExitCy + dy] = CellType.Empty;
            }
            float exitDoorY = roomY - 5f + (doorExitCy - 1);
            if (roomId == 20)
            {
                exitDoorY = roomY - 5f + 9f;
            }
            else if (roomId == 30)
            {
                exitDoorY = roomY - 5f + 8f;
            }
            if (roomId != 50)
            {
                SpawnExitDoor(new Vector3(startX + width - 3.5f, exitDoorY, -0.5f));
            }
            if (roomId > 1)
            {
                Chamber entryChamber = chambers[0];
                int doorEntryCy = entryChamber.cy;
                // Carve empty space in the grid around the entry door to prevent wall overlap
                for (int dy = -1; dy <= 1; dy++)
                {
                    grid[2, doorEntryCy + dy] = CellType.Empty;
                    grid[3, doorEntryCy + dy] = CellType.Empty;
                    grid[4, doorEntryCy + dy] = CellType.Empty;
                }
                float entryDoorY = roomY - 5f + (doorEntryCy - 1);
                SpawnEntryDoor(new Vector3(startX + 3.5f, entryDoorY, -0.5f));
            }

            // Spawn Merchant in specific rooms (Map 1, 5, 10, 15, 20, 25, 30, 35, 40, 45, 50)
            if (roomId == 1 || roomId == 5 || roomId == 10 || roomId == 15 || roomId == 20 || 
                roomId == 25 || roomId == 30 || roomId == 35 || roomId == 40 || roomId == 45 || roomId == 50)
            {
                float merchantX = startX + 7.5f; // Standing right next to the entry door (at startX + 3.5f)
                Chamber entryChamber = chambers[0];
                int doorEntryCy = entryChamber.cy;
                float merchantY = roomY - 5f + (doorEntryCy - 1); // Stand exactly on the entry floor level!
                SpawnMerchant(new Vector3(merchantX, merchantY, -0.5f));
            }

            // Spawn Princess Clue Note in Room 10 and Room 30
            if (roomId == 10 || roomId == 30)
            {
                float noteX = startX + width - 7.5f;
                int exitChCy = chambers[progressionExitChamberIndex].cy;
                float actualFloorY = GetActualFloorY(noteX, startX, roomY, widthInt, height, grid, exitChCy);
                // Note pivot is at (0.5f, 0.5f) and scaled height is 3.0f (2.0 * 1.5), so center noteY = actualFloorY + 1.5f stands perfectly on floor.
                float noteY = actualFloorY + 1.5f;
                SpawnPrincessNote(new Vector3(noteX, noteY, -0.5f));
            }

            // Combine level mesh to reduce draw calls to single digits and boost FPS
            CombineLevelGrid();

            // Rebuild geometry immediately
            Physics2D.SyncTransforms();
            if (gridComp != null)
            {
                gridComp.GenerateGeometry();
            }

            // Position Player based on entry direction (left/right) at tunnel floor height
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                int playerEntryCy = chambers[0].cy;
                int playerExitCy = chambers[progressionExitChamberIndex].cy;
                if (enteringFromLeft)
                {
                    float spawnY = roomY - 5f + playerEntryCy - 0.7f;
                    player.transform.position = new Vector3(startX + 4f, spawnY, 0f);
                }
                else
                {
                    float spawnY = roomY - 5f + playerExitCy - 0.7f;
                    player.transform.position = new Vector3(startX + width - 4f, spawnY, 0f);
                }
                Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
                if (rb != null) rb.linearVelocity = Vector2.zero;
            }

            // Sync Minimap
            if (UIManager.Instance != null)
            {
                UIManager.Instance.RefreshMapUI();
            }
        }

        private void SpawnGroundTile(Vector3 pos, int biome)
        {
            // If the tile lies on a ladder vertical climbing path (above the floor), skip spawning it
            int tx = Mathf.RoundToInt(pos.x);
            float roomY = originY + (4 - (lastActiveRoomId - 1) / 10) * roomHeight + roomHeight / 2f - 2f;
            if (pos.y > roomY - 4.5f)
            {
                foreach (var ladder in activeLadders)
                {
                    if (tx == ladder.x && pos.y >= ladder.minY && pos.y <= ladder.maxY + 0.5f)
                    {
                        return; // Walkthrough hole for ladder
                    }
                }
            }

            // Rope Bridge styling for HighHills concept
            if (GetCellStyle(pos) == RoomStyle.HighHills && currentGrid != null)
            {
                int rIndex = lastActiveRoomId - 1;
                float rWidth = GetRoomWidthForLevel(lastActiveRoomId);
                float rStartX = originX + (rIndex % 10) * rWidth;
                int gx = Mathf.Clamp(Mathf.FloorToInt(pos.x - rStartX), 0, currentGridWidth - 1);
                int gy = Mathf.Clamp(Mathf.FloorToInt(pos.y - (roomY - 5f)), 0, currentGridHeight - 1);

                if (gy > 0 && (currentGrid[gx, gy - 1] == CellType.Empty || currentGrid[gx, gy - 1] == CellType.Ladder))
                {
                    SpawnRopeBridgeTile(pos, biome);
                    return;
                }
            }

            GameObject tile = new GameObject("GroundTile");
            tile.transform.position = pos;
            tile.layer = LayerMask.NameToLayer("Ground");
            
            GameObject grid = GameObject.Find("LevelGrid");
            if (grid != null) tile.transform.SetParent(grid.transform);
            
            var boxCol = tile.AddComponent<BoxCollider2D>();
            boxCol.usedByComposite = true;
            
            MeshFilter mf = tile.AddComponent<MeshFilter>();
            mf.sharedMesh = GetQuadMesh();
            
            var renderer = tile.AddComponent<MeshRenderer>();
            if (renderer != null)
            {
                // Material caching using sharedMaterial to prevent cloning and enable batching
                renderer.sharedMaterial = GetCachedMaterial("GroundTile_Biome_" + biome, () => {
                    Texture2D tex = new Texture2D(16, 16);
                    for (int x = 0; x < 16; x++)
                    {
                        for (int y = 0; y < 16; y++)
                        {
                            Color c = Color.grey;
                            bool isJoint = (y == 0 || y == 8 || (y < 8 && x == 8) || (y >= 8 && (x == 4 || x == 12)));

                            if (biome == 0) // Mossy Forest / Grassland
                            {
                                if (y >= 12) // Green grass cap with tiny blades
                                {
                                    bool isBlade = (y == 12 && (x % 3 == 0));
                                    c = isBlade ? new Color(0.12f, 0.45f, 0.1f) : new Color(0.2f, 0.6f, 0.15f);
                                }
                                else if (y == 11) // Transition dirt highlight
                                {
                                    c = new Color(0.48f, 0.32f, 0.18f);
                                }
                                else // Brown dirt fill with joint lines
                                {
                                    c = isJoint ? new Color(0.2f, 0.12f, 0.05f) : new Color(0.35f, 0.22f, 0.12f);
                                }
                            }
                            else if (biome == 1) // Ancient Temple
                            {
                                if (y >= 12) // Sand gold cap
                                {
                                    c = new Color(0.85f, 0.75f, 0.45f);
                                }
                                else if (y == 11) // Golden trim
                                {
                                    c = new Color(0.7f, 0.55f, 0.25f);
                                }
                                else // Sandstone bricks
                                {
                                    c = isJoint ? new Color(0.25f, 0.18f, 0.1f) : new Color(0.55f, 0.45f, 0.32f);
                                }
                            }
                            else if (biome == 2) // Frozen Cavern
                            {
                                if (y >= 12) // White snow cap
                                {
                                    c = new Color(0.95f, 0.98f, 1f);
                                }
                                else if (y == 11) // Cyan ice highlight
                                {
                                    c = new Color(0.6f, 0.85f, 0.95f);
                                }
                                else // Frosty blue ice rock bricks
                                {
                                    c = isJoint ? new Color(0.08f, 0.15f, 0.3f) : new Color(0.2f, 0.45f, 0.65f);
                                }
                            }
                            else if (biome == 3) // Void Cellar
                            {
                                if (y >= 12) // Purple void crust
                                {
                                    c = new Color(0.85f, 0.2f, 0.9f);
                                }
                                else if (y == 11) // Neon violet neon highlight
                                {
                                    c = new Color(0.45f, 0.08f, 0.55f);
                                }
                                else // Dark void stone bricks
                                {
                                    c = isJoint ? new Color(0.05f, 0.02f, 0.1f) : new Color(0.15f, 0.1f, 0.22f);
                                }
                            }
                            else // Magma Keep
                            {
                                if (y >= 12) // Glowing orange lava crust
                                {
                                    c = new Color(1f, 0.35f, 0f);
                                }
                                else if (y == 11) // Dark volcanic ash
                                {
                                    c = new Color(0.6f, 0.1f, 0f);
                                }
                                else // Dark obsidian bricks
                                {
                                    c = isJoint ? new Color(0.02f, 0.02f, 0.02f) : new Color(0.12f, 0.12f, 0.14f);
                                }
                            }

                            tex.SetPixel(x, y, c);
                        }
                    }
                    tex.filterMode = FilterMode.Point;
                    tex.Apply();
                    return tex;
                });
            }
            activeRoomEntities.Add(tile);
        }

        private void SpawnWallTile(Vector3 pos, int biome)
        {
            // If the tile lies on a ladder vertical climbing path (above the floor), skip spawning it (necessary for thickened platforms)
            int tx = Mathf.RoundToInt(pos.x);
            float roomY = originY + (4 - (lastActiveRoomId - 1) / 10) * roomHeight + roomHeight / 2f - 2f;
            if (pos.y > roomY - 4.5f)
            {
                foreach (var ladder in activeLadders)
                {
                    if (tx == ladder.x && pos.y >= ladder.minY && pos.y <= ladder.maxY + 0.5f)
                    {
                        return; // Walkthrough hole for ladder
                    }
                }
            }

            GameObject tile = new GameObject("WallTile");
            tile.transform.position = pos;
            tile.layer = LayerMask.NameToLayer("Ground");

            GameObject grid = GameObject.Find("LevelGrid");
            if (grid != null) tile.transform.SetParent(grid.transform);
            
            var boxCol = tile.AddComponent<BoxCollider2D>();
            boxCol.usedByComposite = true;
            
            MeshFilter mf = tile.AddComponent<MeshFilter>();
            mf.sharedMesh = GetQuadMesh();
            
            var renderer = tile.AddComponent<MeshRenderer>();
            if (renderer != null)
            {
                bool isHighHills = (GetCellStyle(pos) == RoomStyle.HighHills);
                bool isCeiling = isHighHills && (pos.y >= (roomY - 5f + currentGridHeight - 1f) - 0.1f);
                if (isCeiling)
                {
                    tile.name = "CloudCeilingTile";
                }

                // Material caching using sharedMaterial to prevent cloning and enable batching
                renderer.sharedMaterial = GetCachedMaterial("WallTile_Biome_" + biome + "_Ceiling_" + isCeiling, () => {
                    Texture2D tex = new Texture2D(16, 16);
                    for (int x = 0; x < 16; x++)
                    {
                        for (int y = 0; y < 16; y++)
                        {
                            Color c = Color.grey;
                            if (isCeiling)
                            {
                                float dx = x - 7.5f;
                                float dy = y - 7.5f;
                                bool isPuff = (dx * dx + dy * dy <= 45f) || (y <= 3);
                                c = isPuff ? ((y <= 5) ? new Color(0.8f, 0.82f, 0.88f) : new Color(0.95f, 0.95f, 0.98f)) : new Color(0.9f, 0.92f, 0.95f);
                            }
                            else
                            {
                                bool isJoint = (y == 0 || y == 8 || (y < 8 && x == 8) || (y >= 8 && (x == 4 || x == 12)));

                                if (biome == 0) // Dark earth / stone fill
                                {
                                    c = isJoint ? new Color(0.12f, 0.08f, 0.05f) : new Color(0.24f, 0.16f, 0.1f);
                                }
                                else if (biome == 1) // Dark sandstone bricks
                                {
                                    c = isJoint ? new Color(0.2f, 0.15f, 0.1f) : new Color(0.38f, 0.3f, 0.22f);
                                }
                                else if (biome == 2) // Dark frozen ice bricks
                                {
                                    c = isJoint ? new Color(0.05f, 0.1f, 0.2f) : new Color(0.12f, 0.28f, 0.42f);
                                }
                                else if (biome == 3) // Deep void cellar violet bricks
                                {
                                    c = isJoint ? new Color(0.04f, 0.02f, 0.08f) : new Color(0.1f, 0.06f, 0.16f);
                                }
                                else // Obsidian dark bricks
                                {
                                    c = isJoint ? new Color(0.01f, 0.01f, 0.01f) : new Color(0.06f, 0.06f, 0.07f);
                                }
                            }

                            tex.SetPixel(x, y, c);
                        }
                    }
                    tex.filterMode = FilterMode.Point;
                    tex.Apply();
                    return tex;
                });
            }
            activeRoomEntities.Add(tile);
        }

        private void SpawnLavaTile(Vector3 pos)
        {
            GameObject tile = new GameObject("LavaTile");
            tile.transform.position = pos;
            
            var box = tile.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            
            tile.AddComponent<LavaTile>();
            
            MeshFilter mf = tile.AddComponent<MeshFilter>();
            mf.sharedMesh = GetQuadMesh();
            
            var renderer = tile.AddComponent<MeshRenderer>();
            if (renderer != null)
            {
                // Material caching using sharedMaterial to prevent cloning and enable batching
                renderer.sharedMaterial = GetCachedMaterial("LavaTile", () => {
                    Texture2D tex = new Texture2D(16, 16);
                    for (int x = 0; x < 16; x++)
                    {
                        for (int y = 0; y < 16; y++)
                        {
                            float noise = Mathf.Sin(x * 0.8f) * Mathf.Cos(y * 0.8f);
                            if (noise > 0.4f) tex.SetPixel(x, y, new Color(1f, 0.8f, 0f));
                            else if (noise < -0.4f) tex.SetPixel(x, y, new Color(0.6f, 0f, 0f));
                            else tex.SetPixel(x, y, new Color(1f, 0.3f, 0f));
                        }
                    }
                    tex.filterMode = FilterMode.Point;
                    tex.Apply();
                    return tex;
                });
            }
            activeRoomEntities.Add(tile);
        }

        private void SpawnSpikeTile(Vector3 pos)
        {
            GameObject tile = new GameObject("SpikeTile");
            tile.transform.position = pos;
            tile.transform.localScale = new Vector3(1f, 0.4f, 1f);
            
            var box = tile.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            
            tile.AddComponent<SpikeTile>();
            
            MeshFilter mf = tile.AddComponent<MeshFilter>();
            mf.sharedMesh = GetQuadMesh();
            
            var renderer = tile.AddComponent<MeshRenderer>();
            if (renderer != null)
            {
                // Material caching using sharedMaterial to prevent cloning and enable batching
                renderer.sharedMaterial = GetCachedMaterial("SpikeTile", () => {
                    Texture2D tex = new Texture2D(16, 16);
                    for (int x = 0; x < 16; x++)
                    {
                        for (int y = 0; y < 16; y++)
                        {
                            bool isSpike = (y >= (15 - Mathf.Abs(x - 8) * 2));
                            tex.SetPixel(x, y, isSpike ? new Color(0.5f, 0.5f, 0.55f) : Color.clear);
                        }
                    }
                    tex.filterMode = FilterMode.Point;
                    tex.Apply();
                    return tex;
                });
            }
            activeRoomEntities.Add(tile);
        }

        private void SpawnWaterTile(Vector3 pos)
        {
            GameObject tile = new GameObject("WaterTile");
            tile.transform.position = pos;
            
            var box = tile.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            
            tile.AddComponent<WaterBody>();
            
            MeshFilter mf = tile.AddComponent<MeshFilter>();
            mf.sharedMesh = GetQuadMesh();
            
            var renderer = tile.AddComponent<MeshRenderer>();
            if (renderer != null)
            {
                // Material caching using sharedMaterial to prevent cloning and enable batching
                renderer.sharedMaterial = GetCachedMaterial("WaterTile", () => {
                    Texture2D tex = new Texture2D(16, 16);
                    for (int x = 0; x < 16; x++)
                    {
                        for (int y = 0; y < 16; y++)
                        {
                            bool isWave = (y == Mathf.FloorToInt(Mathf.Sin(x * 0.5f) * 2f + 8f) || y == Mathf.FloorToInt(Mathf.Sin(x * 0.5f) * 2f + 14f));
                            Color c = isWave ? new Color(0.7f, 0.9f, 1f, 0.8f) : new Color(0f, 0.4f, 0.8f, 0.5f);
                            tex.SetPixel(x, y, c);
                        }
                    }
                    tex.filterMode = FilterMode.Point;
                    tex.Apply();
                    return tex;
                });
            }
            activeRoomEntities.Add(tile);
        }

        private Sprite CreateMonsterSprite(EnemyGuardian.MonsterBehavior behavior, AnimState state, int frame, Color bodyColor, int roomLevel)
        {
            bool isBoss = (behavior == EnemyGuardian.MonsterBehavior.Boss);
            int size = isBoss ? 32 : 16;
            Texture2D tex = new Texture2D(size, size);
            
            // Set all transparent first
            for (int x = 0; x < size; x++)
                for (int y = 0; y < size; y++)
                    tex.SetPixel(x, y, Color.clear);

            int group = (roomLevel - 1) / 10;
            if (group < 0) group = 0;
            if (group > 4) group = 4;

            if (isBoss)
            {
                // Bosses (32x32 size)
                if (roomLevel == 10) // Moss Golem King
                {
                    Color stone = new Color(0.4f, 0.42f, 0.45f);
                    Color moss = new Color(0.15f, 0.6f, 0.2f);
                    Color eye = Color.red;

                    for (int x = 6; x < 26; x++)
                    {
                        for (int y = 4; y < 28; y++)
                        {
                            bool isGlowEye = (y == 20 && (x == 11 || x == 20));
                            bool isStone = (x > 8 && x < 23 && y > 6 && y < 25);
                            bool isMossHighlight = isStone && ((x + y) % 5 == 0 || y >= 22);
                            bool isOutline = (x == 8 || x == 23) && (y >= 6 && y <= 25) || (y == 6 || y == 25) && (x >= 8 && x <= 23);

                            if (isGlowEye) tex.SetPixel(x, y, eye);
                            else if (isMossHighlight) tex.SetPixel(x, y, moss);
                            else if (isStone) tex.SetPixel(x, y, stone);
                            else if (isOutline) tex.SetPixel(x, y, new Color(0.15f, 0.15f, 0.18f));
                        }
                    }

                    // Moss Golem Arms
                    int armOffset = (state == AnimState.Attack && frame == 1) ? -4 : 0;
                    for (int y = 8 + armOffset; y < 18 + armOffset; y++)
                    {
                        for (int x = 4; x <= 8; x++) tex.SetPixel(x, y, stone);
                        for (int x = 23; x <= 27; x++) tex.SetPixel(x, y, stone);
                    }
                }
                else if (roomLevel == 20) // Sphinx
                {
                    Color gold = new Color(0.85f, 0.7f, 0.2f);
                    Color blue = new Color(0.1f, 0.4f, 0.75f);
                    Color wings = new Color(0.35f, 0.35f, 0.4f);

                    // Wings flapping
                    int wingY = (frame == 0) ? 2 : -2;
                    for (int x = 2; x < 30; x++)
                    {
                        for (int y = 10; y < 28; y++)
                        {
                            bool isWing = (x < 10 || x > 21) && (y >= 14 + wingY && y <= 24 + wingY);
                            if (isWing) tex.SetPixel(x, y, wings);
                        }
                    }

                    // Sphinx Body & Head
                    for (int x = 8; x < 24; x++)
                    {
                        for (int y = 4; y < 24; y++)
                        {
                            bool isBlueStripe = (y % 4 == 0);
                            bool isHead = (x >= 11 && x <= 20 && y >= 14);
                            bool isBody = (x >= 9 && x <= 22 && y >= 4 && y < 14);

                            if (isHead)
                            {
                                if (isBlueStripe) tex.SetPixel(x, y, blue);
                                else tex.SetPixel(x, y, gold);
                            }
                            else if (isBody)
                            {
                                tex.SetPixel(x, y, gold);
                            }
                        }
                    }

                    // Sphinx Eyes
                    tex.SetPixel(13, 18, Color.cyan);
                    tex.SetPixel(18, 18, Color.cyan);
                }
                else if (roomLevel == 30) // Yeti King
                {
                    Color fur = Color.white;
                    Color shade = new Color(0.8f, 0.85f, 0.95f);
                    Color horn = new Color(0.4f, 0.45f, 0.5f);
                    Color claw = new Color(0.2f, 0.7f, 1f);

                    for (int x = 6; x < 26; x++)
                    {
                        for (int y = 4; y < 28; y++)
                        {
                            bool isHorn = ((x >= 7 && x <= 9) || (x >= 22 && x <= 24)) && (y >= 22 && y <= 26);
                            bool isBody = (x > 8 && x < 23 && y > 5 && y < 23);
                            bool isClaw = (y == 5 && (x == 9 || x == 10 || x == 21 || x == 22));

                            if (isHorn) tex.SetPixel(x, y, horn);
                            else if (isClaw) tex.SetPixel(x, y, claw);
                            else if (isBody)
                            {
                                if ((x + y) % 3 == 0) tex.SetPixel(x, y, shade);
                                else tex.SetPixel(x, y, fur);
                            }
                        }
                    }
                    // Glowing Blue Eyes
                    tex.SetPixel(12, 18, Color.cyan);
                    tex.SetPixel(19, 18, Color.cyan);
                }
                else if (roomLevel == 40) // Void Arch-Devourer (Flying/Hovering)
                {
                    Color voidColor = new Color(0.15f, 0.05f, 0.25f);
                    Color pulse = new Color(0.9f, 0.1f, 0.8f);
                    Color eyeWhite = Color.white;

                    // Pulsing core
                    int pulseRadius = (frame == 0) ? 6 : 8;

                    for (int x = 0; x < 32; x++)
                    {
                        for (int y = 0; y < 32; y++)
                        {
                            float dx = x - 15.5f;
                            float dy = y - 15.5f;
                            float dist = Mathf.Sqrt(dx * dx + dy * dy);

                            if (dist <= 4f) tex.SetPixel(x, y, Color.red); // Giant red pupil
                            else if (dist <= 8f) tex.SetPixel(x, y, eyeWhite);
                            else if (dist <= pulseRadius + 3) tex.SetPixel(x, y, pulse);
                            else if (dist <= 14f)
                            {
                                // Tentacles/Eye spikes
                                bool isSpike = ((x + y) % 6 == 0 || (x - y) % 6 == 0);
                                if (isSpike) tex.SetPixel(x, y, voidColor);
                            }
                        }
                    }
                }
                else // roomLevel == 50 Magma Dragon Lord
                {
                    Color obsidian = new Color(0.1f, 0.08f, 0.08f);
                    Color magma = new Color(1f, 0.3f, 0f);
                    Color fire = new Color(1f, 0.7f, 0f);

                    // Wings flapping
                    int wingOffset = (frame == 0) ? 3 : -3;
                    for (int x = 1; x < 31; x++)
                    {
                        for (int y = 8; y < 28; y++)
                        {
                            bool isWing = (x < 9 || x > 22) && (y >= 12 + wingOffset && y <= 22 + wingOffset);
                            if (isWing) tex.SetPixel(x, y, magma);
                        }
                    }

                    // Dragon Head & Body
                    for (int x = 8; x < 24; x++)
                    {
                        for (int y = 3; y < 25; y++)
                        {
                            bool isHead = (x >= 10 && x <= 21 && y >= 14);
                            bool isBody = (x >= 9 && x <= 22 && y >= 3 && y < 14);
                            bool isLavaCrack = ((x + y) % 4 == 0);

                            if (isHead || isBody)
                            {
                                if (isLavaCrack) tex.SetPixel(x, y, fire);
                                else tex.SetPixel(x, y, obsidian);
                            }
                        }
                    }

                    // Burning eyes
                    tex.SetPixel(12, 19, Color.yellow);
                    tex.SetPixel(19, 19, Color.yellow);
                }
            }
            else
            {
                // Normal Monsters (16x16 size)
                if (group == 0) // Biome 0: Mossy Forest (Goblin, Spore Shroom, Spore Bat)
                {
                    if (behavior == EnemyGuardian.MonsterBehavior.ClubMelee) // Moss Goblin
                    {
                        for (int x = 3; x < 13; x++)
                        {
                            for (int y = 2; y < 14; y++)
                            {
                                bool isBody = (x >= 4 && x <= 11 && y >= 3 && y <= 11);
                                bool isEyes = (x == 5 || x == 10) && y == 8;
                                bool isClub = false;
                                if (state == AnimState.Attack)
                                    isClub = (x >= 9 && x <= 14 && y >= 6 && y <= 11);
                                else
                                    isClub = (x >= 10 && x <= 12 && y >= 2 && y <= 8);

                                if (isEyes) tex.SetPixel(x, y, Color.red);
                                else if (isClub) tex.SetPixel(x, y, new Color(0.45f, 0.3f, 0.15f));
                                else if (isBody)
                                {
                                    if (y <= 5) tex.SetPixel(x, y, new Color(0.35f, 0.2f, 0.1f)); // Brown pants
                                    else tex.SetPixel(x, y, new Color(0.2f, 0.65f, 0.2f)); // Green skin
                                }
                            }
                        }
                        // Bouncy leg animations
                        int legY = (state == AnimState.Walk && frame == 1) ? 2 : 1;
                        tex.SetPixel(5, legY, new Color(0.2f, 0.65f, 0.2f));
                        tex.SetPixel(10, legY, new Color(0.2f, 0.65f, 0.2f));
                    }
                    else if (behavior == EnemyGuardian.MonsterBehavior.DaggerThrower) // Spore Shroom
                    {
                        for (int x = 2; x < 14; x++)
                        {
                            for (int y = 3; y < 14; y++)
                            {
                                bool isCap = (x >= 3 && x <= 12 && y >= 7 && y <= 12);
                                bool isSpot = isCap && ((x + y) % 3 == 0);
                                bool isStalk = (x >= 6 && x <= 9 && y >= 3 && y < 7);

                                if (isSpot) tex.SetPixel(x, y, Color.yellow);
                                else if (isCap) tex.SetPixel(x, y, new Color(0.6f, 0.1f, 0.5f)); // Purple
                                else if (isStalk) tex.SetPixel(x, y, Color.white);
                            }
                        }
                        int legY = (state == AnimState.Walk && frame == 1) ? 2 : 1;
                        tex.SetPixel(5, legY, Color.black);
                        tex.SetPixel(10, legY, Color.black);
                    }
                    else // Spore Bat (Flying)
                    {
                        int wingY = (frame == 0) ? 2 : -2;
                        for (int x = 0; x < 16; x++)
                        {
                            for (int y = 2; y < 14; y++)
                            {
                                bool isBody = (x >= 5 && x <= 10 && y >= 4 && y <= 9);
                                bool isWing = (x < 5 || x > 10) && (y >= 6 + wingY && y <= 10 + wingY);

                                if (isBody)
                                {
                                    if (x == 6 || x == 9) tex.SetPixel(x, y, Color.green); // Lime eyes
                                    else tex.SetPixel(x, y, new Color(0.25f, 0.25f, 0.3f)); // Grey fur
                                }
                                else if (isWing) tex.SetPixel(x, y, new Color(0.15f, 0.15f, 0.18f));
                            }
                        }
                    }
                }
                else if (group == 1) // Biome 1: Ancient Temple (Golem, Snake, Sun Eagle)
                {
                    if (behavior == EnemyGuardian.MonsterBehavior.ClubMelee) // Temple Golem
                    {
                        for (int x = 3; x < 13; x++)
                        {
                            for (int y = 2; y < 14; y++)
                            {
                                bool isBody = (x >= 4 && x <= 11 && y >= 3 && y <= 12);
                                bool isRune = isBody && ((x == 6 || x == 9) && y >= 6 && y <= 9);

                                if (isRune) tex.SetPixel(x, y, Color.cyan); // Blue runes
                                else if (isBody) tex.SetPixel(x, y, new Color(0.8f, 0.65f, 0.45f)); // Sandstone
                            }
                        }
                        int legY = (state == AnimState.Walk && frame == 1) ? 2 : 1;
                        tex.SetPixel(5, legY, new Color(0.5f, 0.4f, 0.3f));
                        tex.SetPixel(10, legY, new Color(0.5f, 0.4f, 0.3f));
                    }
                    else if (behavior == EnemyGuardian.MonsterBehavior.DaggerThrower) // Cobra Spitter
                    {
                        for (int x = 2; x < 14; x++)
                        {
                            for (int y = 2; y < 14; y++)
                            {
                                bool isBody = (x >= 4 && x <= 11 && y >= 3 && y <= 11);
                                bool isHood = (y >= 8 && y <= 11 && (x == 3 || x == 12));
                                bool isEye = (x == 6 || x == 9) && y == 10;

                                if (isEye) tex.SetPixel(x, y, Color.red);
                                else if (isBody || isHood) tex.SetPixel(x, y, new Color(0.2f, 0.5f, 0.2f)); // Snake green
                            }
                        }
                        // Serpent wave
                        int tailX = (frame == 0) ? 4 : 11;
                        tex.SetPixel(tailX, 1, new Color(0.15f, 0.4f, 0.15f));
                    }
                    else // Sun Eagle (Flying)
                    {
                        int wingY = (frame == 0) ? 3 : -3;
                        for (int x = 0; x < 16; x++)
                        {
                            for (int y = 2; y < 14; y++)
                            {
                                bool isBody = (x >= 5 && x <= 10 && y >= 4 && y <= 9);
                                bool isWing = (x < 5 || x > 10) && (y >= 5 + wingY && y <= 9 + wingY);

                                if (isBody)
                                {
                                    if (x == 9 && y == 7) tex.SetPixel(x, y, Color.white); // Beak
                                    else tex.SetPixel(x, y, new Color(0.85f, 0.55f, 0.1f)); // Gold feathers
                                }
                                else if (isWing) tex.SetPixel(x, y, new Color(0.7f, 0.4f, 0.05f));
                            }
                        }
                    }
                }
                else if (group == 2) // Biome 2: Frozen Caverns (Ice Golem, Frost Spider, Frost Wraith)
                {
                    if (behavior == EnemyGuardian.MonsterBehavior.ClubMelee) // Ice Golem
                    {
                        for (int x = 3; x < 13; x++)
                        {
                            for (int y = 2; y < 14; y++)
                            {
                                bool isBody = (x >= 4 && x <= 11 && y >= 3 && y <= 12);
                                bool isSpike = isBody && ((x + y) % 3 == 0);

                                if (isSpike) tex.SetPixel(x, y, Color.white);
                                else if (isBody) tex.SetPixel(x, y, new Color(0.4f, 0.8f, 1f)); // Ice cyan
                            }
                        }
                        int legY = (state == AnimState.Walk && frame == 1) ? 2 : 1;
                        tex.SetPixel(5, legY, new Color(0.2f, 0.5f, 0.7f));
                        tex.SetPixel(10, legY, new Color(0.2f, 0.5f, 0.7f));
                    }
                    else if (behavior == EnemyGuardian.MonsterBehavior.DaggerThrower) // Frost Spider
                    {
                        for (int x = 2; x < 14; x++)
                        {
                            for (int y = 2; y < 14; y++)
                            {
                                bool isBody = (x >= 4 && x <= 11 && y >= 4 && y <= 10);
                                bool isEye = (x == 5 || x == 10) && y == 8;

                                if (isEye) tex.SetPixel(x, y, Color.red);
                                else if (isBody) tex.SetPixel(x, y, new Color(0.25f, 0.6f, 0.65f)); // Ice teal
                            }
                        }
                        // Legs crawling
                        int offset = (frame == 0) ? 0 : 1;
                        tex.SetPixel(3 + offset, 2, Color.black);
                        tex.SetPixel(12 - offset, 2, Color.black);
                        tex.SetPixel(2 + offset, 4, Color.black);
                        tex.SetPixel(13 - offset, 4, Color.black);
                    }
                    else // Frost Wraith (Flying)
                    {
                        for (int x = 3; x < 13; x++)
                        {
                            for (int y = 2; y < 14; y++)
                            {
                                bool isBody = (x >= 5 && x <= 10 && y >= 4 && y <= 11);
                                if (isBody)
                                {
                                    if (x == 6 || x == 9) tex.SetPixel(x, y, Color.cyan);
                                    else tex.SetPixel(x, y, new Color(0.85f, 0.95f, 1f, 0.75f)); // Translucent icy ghost
                                }
                            }
                        }
                    }
                }
                else if (group == 3) // Biome 3: Void Cellar (Void Horror, Void Eye, Void Flyer)
                {
                    if (behavior == EnemyGuardian.MonsterBehavior.ClubMelee) // Void Horror
                    {
                        for (int x = 3; x < 13; x++)
                        {
                            for (int y = 2; y < 14; y++)
                            {
                                bool isBody = (x >= 4 && x <= 11 && y >= 3 && y <= 12);
                                bool isHorn = ((x == 4 || x == 11) && y == 12);
                                bool isEye = x == 7 && y == 9;

                                if (isEye) tex.SetPixel(x, y, Color.red);
                                else if (isHorn) tex.SetPixel(x, y, new Color(1f, 0.2f, 0.8f)); // Neon pink horn
                                else if (isBody) tex.SetPixel(x, y, new Color(0.2f, 0.05f, 0.35f)); // Void purple
                            }
                        }
                        int legY = (state == AnimState.Walk && frame == 1) ? 2 : 1;
                        tex.SetPixel(5, legY, Color.black);
                        tex.SetPixel(10, legY, Color.black);
                    }
                    else if (behavior == EnemyGuardian.MonsterBehavior.DaggerThrower) // Void Eye
                    {
                        for (int x = 2; x < 14; x++)
                        {
                            for (int y = 2; y < 14; y++)
                            {
                                float dx = x - 7.5f;
                                float dy = y - 7.5f;
                                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                                if (dist <= 1.5f) tex.SetPixel(x, y, Color.red);
                                else if (dist <= 3.5f) tex.SetPixel(x, y, Color.white);
                                else if (dist <= 5.0f) tex.SetPixel(x, y, new Color(0.4f, 0.1f, 0.5f));
                            }
                        }
                        // Small tentacles
                        int offset = (frame == 0) ? 0 : 2;
                        tex.SetPixel(5 + offset, 2, new Color(0.4f, 0.1f, 0.5f));
                        tex.SetPixel(9 - offset, 2, new Color(0.4f, 0.1f, 0.5f));
                    }
                    else // Void Flyer (Flying)
                    {
                        int wingY = (frame == 0) ? 2 : -2;
                        for (int x = 0; x < 16; x++)
                        {
                            for (int y = 2; y < 14; y++)
                            {
                                bool isBody = (x >= 5 && x <= 10 && y >= 4 && y <= 9);
                                bool isWing = (x < 5 || x > 10) && (y >= 6 + wingY && y <= 10 + wingY);

                                if (isBody)
                                {
                                    if (x == 6 || x == 9) tex.SetPixel(x, y, new Color(1f, 0f, 0.8f)); // neon pink eyes
                                    else tex.SetPixel(x, y, new Color(0.12f, 0.02f, 0.22f));
                                }
                                else if (isWing) tex.SetPixel(x, y, new Color(0.2f, 0.05f, 0.35f));
                            }
                        }
                    }
                }
                else // Biome 4: Magma Keep (Lava Fiend, Fire Scorpion, Fire Imp)
                {
                    if (behavior == EnemyGuardian.MonsterBehavior.ClubMelee) // Lava Fiend
                    {
                        for (int x = 3; x < 13; x++)
                        {
                            for (int y = 2; y < 14; y++)
                            {
                                bool isBody = (x >= 4 && x <= 11 && y >= 3 && y <= 12);
                                bool isMagma = isBody && ((x + y) % 3 == 0);

                                if (isMagma) tex.SetPixel(x, y, new Color(1f, 0.35f, 0f)); // Glowing lava
                                else if (isBody) tex.SetPixel(x, y, new Color(0.12f, 0.1f, 0.1f)); // Obsidian
                            }
                        }
                        int legY = (state == AnimState.Walk && frame == 1) ? 2 : 1;
                        tex.SetPixel(5, legY, new Color(0.4f, 0f, 0f));
                        tex.SetPixel(10, legY, new Color(0.4f, 0f, 0f));
                    }
                    else if (behavior == EnemyGuardian.MonsterBehavior.DaggerThrower) // Fire Scorpion
                    {
                        for (int x = 2; x < 14; x++)
                        {
                            for (int y = 2; y < 14; y++)
                            {
                                bool isBody = (x >= 4 && x <= 11 && y >= 3 && y <= 8);
                                bool isTail = (x >= 7 && x <= 9 && y >= 9 && y <= 12) || (x == 10 && y == 13);

                                if (isTail) tex.SetPixel(x, y, new Color(1f, 0.3f, 0f)); // Glowing magma tail
                                else if (isBody) tex.SetPixel(x, y, new Color(0.5f, 0.1f, 0.05f)); // Dark red
                            }
                        }
                        int legY = (state == AnimState.Walk && frame == 1) ? 2 : 1;
                        tex.SetPixel(4, legY, Color.black);
                        tex.SetPixel(11, legY, Color.black);
                    }
                    else // Fire Imp (Flying)
                    {
                        int wingY = (frame == 0) ? 3 : -3;
                        for (int x = 0; x < 16; x++)
                        {
                            for (int y = 2; y < 14; y++)
                            {
                                bool isBody = (x >= 5 && x <= 10 && y >= 4 && y <= 9);
                                bool isWing = (x < 5 || x > 10) && (y >= 5 + wingY && y <= 9 + wingY);

                                if (isBody)
                                {
                                    if (x == 7 && y == 8) tex.SetPixel(x, y, Color.yellow);
                                    else tex.SetPixel(x, y, new Color(0.8f, 0.15f, 0f)); // Red body
                                }
                                else if (isWing) tex.SetPixel(x, y, new Color(1f, 0.5f, 0f)); // Fire wings
                            }
                        }
                    }
                }
            }

            // Apply death greyscale if dead
            if (state == AnimState.Death)
            {
                for (int x = 0; x < size; x++)
                {
                    for (int y = 0; y < size; y++)
                    {
                        Color c = tex.GetPixel(x, y);
                        if (c.a > 0.05f)
                        {
                            // Convert to a very dark stone/grey corpse
                            float gray = (c.r + c.g + c.b) / 3f;
                            tex.SetPixel(x, y, new Color(gray * 0.4f, gray * 0.4f, gray * 0.45f, c.a));
                        }
                    }
                }
            }

            tex.filterMode = FilterMode.Point;
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 16f);
        }

        private void SpawnMonster(Vector3 pos, EnemyGuardian.MonsterBehavior behavior, int hp, int dmg, float speed, float scale, int roomLevel, bool isKeyGuardian = false, float guardXCenter = 0f)
        {
            GameObject guardian = new GameObject(behavior == EnemyGuardian.MonsterBehavior.Boss ? "BossEnemy" : "GuardianEnemy");
            guardian.transform.position = pos;
            guardian.layer = LayerMask.NameToLayer("Enemy");

            var rb = guardian.AddComponent<Rigidbody2D>();
            rb.gravityScale = 2.5f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;

            var hs = guardian.AddComponent<HealthSystem>();
            var dmgComp = guardian.AddComponent<Damageable>();
            dmgComp.Team = Team.Enemy;

            var col = guardian.AddComponent<BoxCollider2D>();
            col.size = new Vector2(0.8f, 1.2f);
            
            // Assign a frictionless physics material to prevent sticky walls
            PhysicsMaterial2D frictionlessMat = new PhysicsMaterial2D("EnemyFrictionless");
            frictionlessMat.friction = 0f;
            frictionlessMat.bounciness = 0f;
            col.sharedMaterial = frictionlessMat;

            GameObject child = null;
            bool loadedCustom = false;
            SpriteAnimator animator = null;

            if (behavior == EnemyGuardian.MonsterBehavior.Boss && roomLevel == 50)
            {
                GameObject dragonPrefab = Resources.Load<GameObject>("DragonRed");
                if (dragonPrefab != null)
                {
                    child = Instantiate(dragonPrefab);
                    child.name = "Visual";
                    child.transform.SetParent(guardian.transform, false);

                    var prefabRb = child.GetComponent<Rigidbody2D>();
                    if (prefabRb != null) DestroyImmediate(prefabRb);
                    var prefabCol = child.GetComponent<Collider2D>();
                    if (prefabCol != null) DestroyImmediate(prefabCol);

                    loadedCustom = true;
                }
            }

            if (child == null)
            {
                child = new GameObject("Visual");
                child.transform.SetParent(guardian.transform, false);
                var sr = child.AddComponent<SpriteRenderer>();
                sr.sortingOrder = 15;
                sr.color = Color.white;
                animator = child.AddComponent<SpriteAnimator>();

#if UNITY_EDITOR
                if (behavior != EnemyGuardian.MonsterBehavior.Boss || (behavior == EnemyGuardian.MonsterBehavior.Boss && (roomLevel == 10 || roomLevel == 20 || roomLevel == 30 || roomLevel == 40)))
                {
                    loadedCustom = LoadCustomMonsterAnimations(animator, behavior, roomLevel);
                }
#endif
            }

            Color baseColor = Color.white; // Kept for method compatibility, colors resolved procedurally

            if (loadedCustom)
            {
                string creature = GetCreatureNameForMonster(behavior, roomLevel);
                float scaleMult, yOffset;
                GetVisualConfigForCreature(creature, behavior == EnemyGuardian.MonsterBehavior.Boss, out scaleMult, out yOffset);
                child.transform.localScale = new Vector3(scaleMult, scaleMult, 1f);
                child.transform.localPosition = new Vector3(0f, yOffset, 0f);

                // Set initial sprite from animator clip to get correct sprite dimensions
                SpriteAnimator.AnimationClip idleClip;
                if (animator != null && animator.TryGetClip(AnimState.Idle, out idleClip) && idleClip.frames != null && idleClip.frames.Length > 0)
                {
                    var sr = child.GetComponent<SpriteRenderer>();
                    if (sr != null) sr.sprite = idleClip.frames[0];
                }

                // Use a tight, standard physics collider for all monsters to prevent floating and invisible walls.
                if (behavior == EnemyGuardian.MonsterBehavior.Boss)
                {
                    if (roomLevel == 30)
                    {
                        col.size = new Vector2(1.2f, 1.8f);
                        col.offset = new Vector2(0f, 0f);
                        child.transform.localPosition = new Vector3(0f, yOffset, 0f); // Use yOffset directly
                    }
                    else if (roomLevel == 40)
                    {
                        col.size = new Vector2(0.8f, 1.2f); // Same as player
                        col.offset = new Vector2(0f, 0f);
                        child.transform.localPosition = new Vector3(0f, yOffset, 0f); // Use yOffset directly
                    }
                    else if (roomLevel == 50)
                    {
                        col.size = new Vector2(0.9f, 1.2f); // Match player scale
                        col.offset = new Vector2(0f, 0.05f);
                        child.transform.localPosition = new Vector3(0f, yOffset, 0f); // Use yOffset directly
                    }
                    else
                    {
                        col.size = new Vector2(1.6f, 2.4f);
                        col.offset = new Vector2(0f, 0f);
                        child.transform.localPosition = new Vector3(0f, yOffset * 0.3f, 0f);
                    }
                }
                else
                {
                    col.size = new Vector2(0.8f, 1.2f);
                    col.offset = new Vector2(0f, 0f);
                    child.transform.localPosition = new Vector3(0f, 0f, 0f);
                }
            }

            if (!loadedCustom)
            {
                List<SpriteAnimator.AnimationClip> animClips = new List<SpriteAnimator.AnimationClip>();

                // Idle Clip (2 frames)
                animClips.Add(new SpriteAnimator.AnimationClip
                {
                    state = AnimState.Idle,
                    frames = new Sprite[] {
                        CreateMonsterSprite(behavior, AnimState.Idle, 0, baseColor, roomLevel),
                        CreateMonsterSprite(behavior, AnimState.Idle, 1, baseColor, roomLevel)
                    },
                    frameRate = 4f,
                    loop = true
                });

                // Walk Clip (2 frames)
                animClips.Add(new SpriteAnimator.AnimationClip
                {
                    state = AnimState.Walk,
                    frames = new Sprite[] {
                        CreateMonsterSprite(behavior, AnimState.Walk, 0, baseColor, roomLevel),
                        CreateMonsterSprite(behavior, AnimState.Walk, 1, baseColor, roomLevel)
                    },
                    frameRate = 6f,
                    loop = true
                });

                // Attack Clip (2 frames)
                animClips.Add(new SpriteAnimator.AnimationClip
                {
                    state = AnimState.Attack,
                    frames = new Sprite[] {
                        CreateMonsterSprite(behavior, AnimState.Attack, 0, baseColor, roomLevel),
                        CreateMonsterSprite(behavior, AnimState.Attack, 1, baseColor, roomLevel)
                    },
                    frameRate = 8f,
                    loop = false
                });

                // Death Clip (1 frame)
                animClips.Add(new SpriteAnimator.AnimationClip
                {
                    state = AnimState.Death,
                    frames = new Sprite[] {
                        CreateMonsterSprite(behavior, AnimState.Death, 0, baseColor, roomLevel)
                    },
                    frameRate = 1f,
                    loop = false
                });

                animator.SetClips(animClips);
            }


            // Clamp monster scale to not exceed player size (max 1.0f) for non-bosses only
            float clampedScale = scale;
            if (behavior != EnemyGuardian.MonsterBehavior.Boss)
            {
                clampedScale = Mathf.Min(scale, 1.0f);
            }

            var enemyGuardian = guardian.AddComponent<EnemyGuardian>();
            enemyGuardian.InitializeStats(behavior, hp, dmg, speed, clampedScale, roomLevel);
            enemyGuardian.isKeyGuardian = isKeyGuardian;
            enemyGuardian.guardXCenter = guardXCenter;

            activeRoomEntities.Add(guardian);
        }

        public int GetExactMonsterCountForLevel(int levelId)
        {
            if (levelId >= 1 && levelId <= 4)   return 1;
            if (levelId >= 5 && levelId <= 9)   return 2;
            if (levelId == 10)                  return 2;
            if (levelId >= 11 && levelId <= 14) return 3;
            if (levelId >= 15 && levelId <= 20) return 4;
            if (levelId >= 21 && levelId <= 29) return 5;
            if (levelId == 30)                  return 5;
            if (levelId >= 31 && levelId <= 39) return 6;
            if (levelId == 40)                  return 6;
            if (levelId >= 41 && levelId <= 49) return 7;
            if (levelId == 50)                  return 0;

            return 1;
        }

        private void SpawnMonstersForRoom(int roomId, float centerX, float roomY)
        {
            if (roomId == 50)
            {
                return;
            }

            int count = GetExactMonsterCountForLevel(roomId);

            int roomIndexNormal = roomId - 1;
            int rColNormal = roomIndexNormal % 10;
            int rRowNormal = roomIndexNormal / 10;
            float widthNormal = GetRoomWidthForLevel(roomId);
            float startXNormal = originX + rColNormal * widthNormal;
            float endX = startXNormal + widthNormal;

            bool spawnBoss = (roomId == 10 || roomId == 20 || roomId == 30 || roomId == 40);

            // Progressive stats scaling per level (roomId)
            int hp = 15 + (roomId - 1) * 3;
            int dmg = 4 + Mathf.RoundToInt((roomId - 1) * 0.6f);
            float speed = 2.8f + (roomId - 1) * 0.03f;

            float keyChestPlatformX = startXNormal + widthNormal - 45f;

            for (int i = 0; i < count; i++)
            {
                EnemyGuardian.MonsterBehavior beh = (EnemyGuardian.MonsterBehavior)(i % 3);

                if (i == 0)
                {
                    // Designated Key Guardian on Key Chest platform, offset X by 5 units to avoid spawning directly on top of the chest
                    // Key chest platform is at roomY + 4f. So Y = roomY + 5.1f sets it grounded on the platform.
                    Vector3 guardianPos = new Vector3(keyChestPlatformX + 5f, roomY + 5.1f, 0f);
                    SpawnMonster(guardianPos, beh, hp, dmg, speed, 1f, roomId, true, keyChestPlatformX + 9f);
                }
                else
                {
                    // General patrollers scattered on floor
                    float scatterX = startXNormal + 15f + (i - 1) * 20f;
                    if (scatterX > endX - 15f) scatterX = endX - 15f;
                    
                    // Spawn flying patrollers in the air, ground patrollers grounded on the floor (Y = roomY - 2.9f)
                    float spawnY = (beh == EnemyGuardian.MonsterBehavior.FlameMage) ? (roomY + 1.0f) : (roomY - 2.9f);
                    Vector3 patrollerPos = new Vector3(scatterX, spawnY, 0f);
                    SpawnMonster(patrollerPos, beh, hp, dmg, speed, 1f, roomId, false, 0f);
                }
            }

            if (spawnBoss)
            {
                int bossHp = 150;
                int bossDmg = 15;
                float bossSpeed = 3.2f;
                float bossScale = 1.5f;

                if (roomId == 20) { bossHp = 300; bossDmg = 26; bossSpeed = 3.5f; bossScale = 1.8f; }
                else if (roomId == 30) { bossHp = 550; bossDmg = 38; bossSpeed = 3.6f; bossScale = 2.0f; } // Set scale to 2.0f (together with 1.8x child scale it will be slightly bigger than player)
                else if (roomId == 40) { bossHp = 800; bossDmg = 50; bossSpeed = 3.8f; bossScale = 1.0f; } // Set scale to 1.0f (same size as player)

                // Map 10 boss is a ground golem/slime, spawn it near the floor to avoid getting stuck in the ceiling
                float spawnYOffset = (roomId == 10) ? 2.0f : 12f;
                SpawnMonster(new Vector3(centerX + 15f, roomY + spawnYOffset, 0f), EnemyGuardian.MonsterBehavior.Boss, bossHp, bossDmg, bossSpeed, bossScale, roomId);
            }
        }

        private void SpawnPrincessInCage(float posX, float posY)
        {
            GameObject cageGo = new GameObject("PrincessRescueCage");
            cageGo.transform.position = new Vector3(posX, posY + 1.1f, 0f);
            activeRoomEntities.Add(cageGo);

            // Add SpriteRenderer for Princess stand
            GameObject princessGo = new GameObject("Princess");
            princessGo.transform.SetParent(cageGo.transform, false);
            princessGo.transform.localPosition = new Vector3(0f, -0.5f, 0f);
            princessGo.transform.localScale = new Vector3(1.0f, 1.0f, 1f); // Match player height

            SpriteRenderer princessSr = princessGo.AddComponent<SpriteRenderer>();
            princessSr.sprite = LoadPrincessSprite();
            princessSr.sortingOrder = 5;

            // Back shadow panel
            GameObject bgGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bgGo.name = "CageBG";
            bgGo.transform.SetParent(cageGo.transform, false);
            bgGo.transform.localPosition = new Vector3(0f, 0f, 0.1f);
            bgGo.transform.localScale = new Vector3(2.5f, 3.2f, 1f);
            Destroy(bgGo.GetComponent<Collider>());
            MeshRenderer bgMr = bgGo.GetComponent<MeshRenderer>();
            bgMr.material.shader = Shader.Find("Sprites/Default");
            bgMr.material.color = new Color(0.1f, 0.08f, 0.08f, 0.75f);
            activeRoomEntities.Add(bgGo);

            // Cage borders and vertical bars
            CreateCageBar(cageGo.transform, new Vector3(-1.25f, 0f, -0.1f), new Vector3(0.15f, 3.2f, 1f), Color.gray);
            CreateCageBar(cageGo.transform, new Vector3(1.25f, 0f, -0.1f), new Vector3(0.15f, 3.2f, 1f), Color.gray);
            CreateCageBar(cageGo.transform, new Vector3(0f, 1.55f, -0.1f), new Vector3(2.65f, 0.15f, 1f), Color.gray);
            CreateCageBar(cageGo.transform, new Vector3(0f, -1.55f, -0.1f), new Vector3(2.65f, 0.15f, 1f), Color.gray);

            CreateCageBar(cageGo.transform, new Vector3(-0.62f, 0f, -0.2f), new Vector3(0.08f, 3.0f, 1f), new Color(0.35f, 0.35f, 0.35f, 1f));
            CreateCageBar(cageGo.transform, new Vector3(0f, 0f, -0.2f), new Vector3(0.08f, 3.0f, 1f), new Color(0.35f, 0.35f, 0.35f, 1f));
            CreateCageBar(cageGo.transform, new Vector3(0.62f, 0f, -0.2f), new Vector3(0.08f, 3.0f, 1f), new Color(0.35f, 0.35f, 0.35f, 1f));

            // Rescue trigger
            BoxCollider2D col = cageGo.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(3.0f, 3.0f);
            col.offset = new Vector2(0f, 0f);

            // Trigger script
            cageGo.AddComponent<PrincessRescueTrigger>();
        }

        private void CreateCageBar(Transform parent, Vector3 localPos, Vector3 scale, Color color)
        {
            GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bar.name = "CageBar";
            bar.transform.SetParent(parent, false);
            bar.transform.localPosition = localPos;
            bar.transform.localScale = scale;
            Destroy(bar.GetComponent<Collider>());
            MeshRenderer mr = bar.GetComponent<MeshRenderer>();
            mr.material.shader = Shader.Find("Sprites/Default");
            mr.material.color = color;
        }

        private static Sprite cachedPrincessSprite = null;

        private Sprite CreateProceduralPrincessSprite()
        {
            int width = 32;
            int height = 32;
            Texture2D tex = new Texture2D(width, height);
            tex.filterMode = FilterMode.Point;

            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;

            // Beautiful color palette
            Color hairColor = new Color(1.0f, 0.85f, 0.2f); // Golden blonde
            Color hairShadowColor = new Color(0.85f, 0.7f, 0.1f);
            Color skinColor = new Color(1.0f, 0.88f, 0.82f); // Peach skin
            Color dressColor = new Color(1.0f, 0.41f, 0.71f); // Hot pink
            Color dressTrimColor = new Color(1.0f, 0.7f, 0.85f); // Light pink
            Color crownColor = new Color(1.0f, 0.84f, 0f); // Gold crown
            Color eyeColor = new Color(0f, 0.75f, 1.0f); // Blue eyes
            Color lipColor = new Color(1.0f, 0.3f, 0.4f); // Rosy lips

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int idx = y * width + x;
                    
                    // Crown (top of the head)
                    if (y >= 22 && y <= 24 && x >= 13 && x <= 18)
                    {
                        if (y == 24 && (x == 13 || x == 15 || x == 18)) pixels[idx] = crownColor;
                        else if (y == 23 && (x == 14 || x == 16 || x == 17)) pixels[idx] = crownColor;
                        else if (y == 22) pixels[idx] = crownColor;
                    }
                    // Hair (frames face down to shoulders)
                    else if (y >= 12 && y <= 21 && ((x >= 11 && x <= 12) || (x >= 19 && x <= 20) || (y >= 20 && x >= 12 && x <= 19)))
                    {
                        if (x == 11 || x == 20 || y == 12) pixels[idx] = hairShadowColor;
                        else pixels[idx] = hairColor;
                    }
                    // Face / Skin
                    else if (y >= 14 && y <= 19 && x >= 13 && x <= 18)
                    {
                        pixels[idx] = skinColor;
                        if (y == 17 && (x == 14 || x == 17)) pixels[idx] = eyeColor;
                        if (y == 15 && (x == 15 || x == 16)) pixels[idx] = lipColor;
                    }
                    // Dress / Skirt (tapered downwards from y=13 to y=1)
                    else if (y >= 1 && y <= 13)
                    {
                        int halfWidth = (13 - y) / 2 + 3;
                        if (x >= 16 - halfWidth && x <= 15 + halfWidth)
                        {
                            if (y == 1 || y == 7 || x == 16 - halfWidth || x == 15 + halfWidth)
                            {
                                pixels[idx] = dressTrimColor;
                            }
                            else
                            {
                                pixels[idx] = dressColor;
                            }
                        }
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0f), 24f); // 24 PPU for perfect height matching player (1.33 units tall)
        }

        public Sprite LoadPrincessSprite()
        {
            if (cachedPrincessSprite != null) return cachedPrincessSprite;
            cachedPrincessSprite = CreateProceduralPrincessSprite();
            return cachedPrincessSprite;
        }

        private void SpawnNormalChest(Vector3 pos, PulsevaniaChest.ChestType type)
        {
            Physics2D.SyncTransforms();
            RaycastHit2D[] hits = Physics2D.RaycastAll(pos, Vector2.down, 25f);
            
            // Sort hits by y-coordinate descending (top to bottom)
            System.Array.Sort(hits, (a, b) => b.point.y.CompareTo(a.point.y));

            bool placed = false;
            foreach (var hit in hits)
            {
                if (hit.collider != null && !hit.collider.isTrigger)
                {
                    // Check if it is a wide platform (not a single tile)
                    float centerY = hit.point.y - 0.5f;
                    Vector2 leftCheck = new Vector2(hit.point.x - 1f, centerY);
                    Vector2 rightCheck = new Vector2(hit.point.x + 1f, centerY);
                    
                    Collider2D leftCol = Physics2D.OverlapPoint(leftCheck);
                    Collider2D rightCol = Physics2D.OverlapPoint(rightCheck);
                    
                    bool leftSolid = (leftCol != null && !leftCol.isTrigger);
                    bool rightSolid = (rightCol != null && !rightCol.isTrigger);

                    if (leftSolid || rightSolid)
                    {
                        pos.y = hit.point.y + 0.5f;
                        placed = true;
                        break;
                    }
                }
            }

            // Fallback: If no wide platform was found, place it on the first solid hit
            if (!placed)
            {
                foreach (var hit in hits)
                {
                    if (hit.collider != null && !hit.collider.isTrigger)
                    {
                        pos.y = hit.point.y + 0.5f;
                        break;
                    }
                }
            }

            GameObject chest = new GameObject("NormalChest");
            chest.transform.position = pos;
            chest.layer = LayerMask.NameToLayer("Enemy");

            var sr = chest.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 10;

            chest.AddComponent<BoxCollider2D>().size = new Vector2(1f, 0.8f);
            
            var chestComp = chest.AddComponent<PulsevaniaChest>();
            chestComp.chestType = type;

            activeRoomEntities.Add(chest);
        }

        private void FillPits(CellType[,] grid, int width, int height)
        {
            // Traverse from bottom to top to fill narrow crevices correctly
            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 2; x < width - 2; x++)
                {
                    // Check for 1-block wide pit
                    if (grid[x, y] == CellType.Empty && grid[x, y - 1] == CellType.Solid)
                    {
                        if (grid[x - 1, y] == CellType.Solid && grid[x + 1, y] == CellType.Solid)
                        {
                            grid[x, y] = CellType.Solid;
                        }
                    }
                }

                for (int x = 2; x < width - 3; x++)
                {
                    // Check for 2-block wide pit
                    if (grid[x, y] == CellType.Empty && grid[x + 1, y] == CellType.Empty &&
                        grid[x, y - 1] == CellType.Solid && grid[x + 1, y - 1] == CellType.Solid)
                    {
                        if (grid[x - 1, y] == CellType.Solid && grid[x + 2, y] == CellType.Solid)
                        {
                            grid[x, y] = CellType.Solid;
                            grid[x + 1, y] = CellType.Solid;
                        }
                    }
                }
            }
        }

        private void SpawnKeyChest(Vector3 pos)
        {
            Physics2D.SyncTransforms();
            RaycastHit2D[] hits = Physics2D.RaycastAll(pos, Vector2.down, 25f);
            
            // Sort hits by y-coordinate descending (top to bottom)
            System.Array.Sort(hits, (a, b) => b.point.y.CompareTo(a.point.y));

            bool placed = false;
            foreach (var hit in hits)
            {
                if (hit.collider != null && !hit.collider.isTrigger)
                {
                    // Check if it is a wide platform (not a single tile)
                    float centerY = hit.point.y - 0.5f;
                    Vector2 leftCheck = new Vector2(hit.point.x - 1f, centerY);
                    Vector2 rightCheck = new Vector2(hit.point.x + 1f, centerY);
                    
                    Collider2D leftCol = Physics2D.OverlapPoint(leftCheck);
                    Collider2D rightCol = Physics2D.OverlapPoint(rightCheck);
                    
                    bool leftSolid = (leftCol != null && !leftCol.isTrigger);
                    bool rightSolid = (rightCol != null && !rightCol.isTrigger);

                    if (leftSolid || rightSolid)
                    {
                        pos.y = hit.point.y + 0.5f;
                        placed = true;
                        break;
                    }
                }
            }

            // Fallback: If no wide platform was found, place it on the first solid hit
            if (!placed)
            {
                foreach (var hit in hits)
                {
                    if (hit.collider != null && !hit.collider.isTrigger)
                    {
                        pos.y = hit.point.y + 0.5f;
                        break;
                    }
                }
            }

            GameObject chest = new GameObject("KeyChest");
            chest.transform.position = pos;
            chest.layer = LayerMask.NameToLayer("Enemy");

            var sr = chest.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 10;
            
            // Generate a beautiful procedural purple key chest texture (replacing red/magenta!)
            Texture2D tex = new Texture2D(16, 16);
            Color themeColor = new Color(0.7f, 0f, 1f); // Rich Purple
            for (int x = 0; x < 16; x++)
            {
                for (int y = 0; y < 16; y++)
                {
                    bool isOutline = (x == 0 || x == 15 || y == 0 || y == 15);
                    bool isBand = (x == 3 || x == 4 || x == 11 || x == 12);
                    bool isLock = (x >= 7 && x <= 8 && y >= 6 && y <= 8);
                    bool isLidSeparation = (y == 9);

                    if (isOutline)
                    {
                        tex.SetPixel(x, y, new Color(0.1f, 0.05f, 0.02f, 1f));
                    }
                    else if (isLock)
                    {
                        tex.SetPixel(x, y, Color.gray);
                    }
                    else if (isBand)
                    {
                        tex.SetPixel(x, y, themeColor);
                    }
                    else if (isLidSeparation)
                    {
                        tex.SetPixel(x, y, Color.black);
                    }
                    else
                    {
                        float shade = 0.35f + (y / 24f);
                        tex.SetPixel(x, y, new Color(0.45f * shade, 0.25f * shade, 0.15f * shade, 1f));
                    }
                }
            }
            tex.filterMode = FilterMode.Point;
            tex.Apply();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16f);

            chest.transform.localScale = Vector3.one; // 1.0f scale
            chest.AddComponent<BoxCollider2D>().size = new Vector2(1f, 0.8f);
            chest.AddComponent<KeyChest>();
            activeRoomEntities.Add(chest);
        }

        private Sprite CreateDetailedDoorSprite(Color woodBaseColor, bool hasLock)
        {
            // Draw a majestic 32x48 door texture
            int w = 32;
            int h = 48;
            Texture2D tex = new Texture2D(w, h);
            Color stone = new Color(0.4f, 0.42f, 0.45f);
            Color stoneDark = new Color(0.22f, 0.22f, 0.25f);
            Color gold = new Color(0.85f, 0.7f, 0.15f);
            Color black = new Color(0.05f, 0.05f, 0.05f);
            Color redCrest = new Color(0.85f, 0.1f, 0.1f);
            Color iron = new Color(0.25f, 0.25f, 0.28f);

            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    // 1. Stone Arch Frame (gothic curved arch at top y >= 40, and pillars on left/right x <= 3, x >= 28)
                    bool isPillarLeft = (x <= 3);
                    bool isPillarRight = (x >= 28);
                    
                    // Curved arch formula for the top:
                    float dx = x - 15.5f;
                    float dy = y - 36f;
                    bool isArch = (y >= 38 && (dx * dx * 0.6f + dy * dy >= 120f) && (dx * dx * 0.6f + dy * dy <= 220f));
                    bool isArchInner = (y >= 36 && (dx * dx * 0.6f + dy * dy < 120f));
                    
                    Color c = woodBaseColor;

                    if (isPillarLeft || isPillarRight || isArch)
                    {
                        // Stone texture with joint lines
                        bool isJoint = (y % 8 == 0 || (isPillarLeft && x == 3) || (isPillarRight && x == 28));
                        c = isJoint ? stoneDark : stone;
                        // Add glowing gold runic markings on the pillars
                        if (!isJoint && ((y % 8 == 3 || y % 8 == 5) && (x == 1 || x == 30)))
                        {
                            c = gold;
                        }
                    }
                    else if (isArchInner || y >= 38)
                    {
                        // Deep dark shadow/recess inside the archway
                        c = black;
                    }
                    else
                    {
                        // 2. Wooden Double Doors (x = 4..27, y = 0..37)
                        // Plank vertical lines (separators at x=15 and x=16)
                        bool isPlankSeparator = (x == 15 || x == 16);
                        // Horizontal iron reinforcements (straps) at y=10..11 and y=26..27
                        bool isStrap = (y == 10 || y == 11 || y == 26 || y == 27);
                        // Shield crest at the center
                        float cx = x - 15.5f;
                        float cy = y - 18.5f;
                        bool isCrest = (cx * cx * 1.5f + cy * cy <= 16f);
                        bool isCrestBorder = isCrest && (cx * cx * 1.5f + cy * cy >= 10f);

                        if (isCrestBorder)
                        {
                            c = gold;
                        }
                        else if (isCrest)
                        {
                            c = redCrest;
                            // Draw keyhole inside the crest if it has a lock
                            if (hasLock && Mathf.Abs(cx) <= 1f && cy >= -1.5f && cy <= 1.5f)
                            {
                                c = black;
                            }
                        }
                        else if (isStrap)
                        {
                            c = iron;
                            // studs on strap
                            if (x == 6 || x == 11 || x == 20 || x == 25)
                            {
                                c = gold;
                            }
                        }
                        else if (isPlankSeparator)
                        {
                            c = woodBaseColor * 0.4f;
                        }
                        else
                        {
                            // Wooden grain details
                            float noise = Mathf.Sin(y * 0.5f + x * 0.2f) * 0.08f;
                            c = Color.Lerp(woodBaseColor, black, -noise);
                            c = Color.Lerp(c, Color.white, noise > 0f ? noise * 0.5f : 0f);
                        }
                    }

                    tex.SetPixel(x, y, c);
                }
            }

            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 16f);
        }

        private void SpawnExitDoor(Vector3 pos)
        {
            GameObject door = new GameObject("ExitDoor");
            door.transform.position = pos; // Align bottom exactly with walking floor (Y = roomY - 3.5f)
            door.transform.localScale = new Vector3(0.6f, 0.73f, 1f); // Standard door size

            var sr = door.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 3; // in front of background, behind player
            sr.color = Color.white; // No tint, show procedural colors
            sr.sprite = CreateDetailedDoorSprite(new Color(0.48f, 0.25f, 0.12f), true); // Mahogany red-brown with crest

            var col = door.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(1.5f, 2.2f);
            col.offset = new Vector2(0f, 1.1f); // align collider center with bottom pivot
            
            door.AddComponent<RoomExitDoor>();
            activeRoomEntities.Add(door);

            // Flank the exit door with two beautiful glowing torches!
            SpawnTorch(pos + Vector3.left * 1.0f + Vector3.up * 0.8f);
            SpawnTorch(pos + Vector3.right * 1.0f + Vector3.up * 0.8f);
            // Spawn a mysterious magic purple exit portal ambient light glow!
            SpawnAmbientLightGlow(pos + Vector3.up * 0.8f, new Color(0.7f, 0.2f, 1f), 3.5f);
        }

        private void SpawnEntryDoor(Vector3 pos)
        {
            GameObject door = new GameObject("EntryDoor");
            door.transform.position = pos; // Align bottom exactly with walking floor (Y = roomY - 3.5f)
            door.transform.localScale = new Vector3(0.6f, 0.73f, 1f); // Standard door size

            var sr = door.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 3; // in front of background, behind player
            sr.color = Color.white; // No tint, show procedural colors
            sr.sprite = CreateDetailedDoorSprite(new Color(0.25f, 0.35f, 0.25f), false); // Greenish rustic wood, no keyhole

            var col = door.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(1.5f, 2.2f);
            col.offset = new Vector2(0f, 1.1f); // align collider center with bottom pivot
            
            door.AddComponent<RoomEntryDoor>();
            activeRoomEntities.Add(door);

            // Flank the entry door with two beautiful glowing torches!
            SpawnTorch(pos + Vector3.left * 1.0f + Vector3.up * 0.8f);
            SpawnTorch(pos + Vector3.right * 1.0f + Vector3.up * 0.8f);
        }

        private void SpawnGuardian(Vector3 pos)
        {
            GameObject guardian = new GameObject("GuardianEnemy");
            guardian.transform.position = pos;
            guardian.layer = LayerMask.NameToLayer("Enemy");

            var rb = guardian.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;

            var hs = guardian.AddComponent<HealthSystem>();
            hs.SetMaxHealth(25);

            var dmg = guardian.AddComponent<Damageable>();
            dmg.Team = Team.Enemy;

            GameObject child = new GameObject("Visual");
            child.transform.SetParent(guardian.transform, false);
            var sr = child.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 15;
            
            BaseEnemyAI refEnemy = FindFirstObjectByType<BaseEnemyAI>();
            if (refEnemy != null)
            {
                var refSr = refEnemy.GetComponentInChildren<SpriteRenderer>();
                if (refSr != null) sr.sprite = refSr.sprite;
                sr.color = Color.red;
            }
            else
            {
                Texture2D tex = new Texture2D(16, 16);
                for (int x = 0; x < 16; x++)
                    for (int y = 0; y < 16; y++)
                        tex.SetPixel(x, y, Color.red);
                tex.Apply();
                sr.sprite = Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16f);
            }

            var col = guardian.AddComponent<BoxCollider2D>();
            col.size = new Vector2(0.8f, 1.2f);

            guardian.AddComponent<EnemyGuardian>();
            activeRoomEntities.Add(guardian);
        }

        public void DiscoverRoom(int roomId)
        {
            int index = roomId - 1;
            if (index < 0 || index >= 50) return;
            if (rooms[index].state == RoomState.Locked)
            {
                rooms[index].state = RoomState.Discovered;
                if (UIManager.Instance != null)
                {
                    UIManager.Instance.RefreshMapUI();
                }
            }
        }

        public void ClearRoom(int roomId)
        {
            int index = roomId - 1;
            if (index < 0 || index >= 50) return;
            
            // Mark both room cleared and enemies cleared
            rooms[index].enemiesSpawned = false;

            if (rooms[index].state != RoomState.Cleared)
            {
                rooms[index].state = RoomState.Cleared;
                if (UIManager.Instance != null)
                {
                    UIManager.Instance.RefreshMapUI();
                }
            }
        }

        private void SpawnBreakableWall(Vector3 pos, int biome)
        {
            GameObject wall = new GameObject("BreakableWall");
            wall.transform.position = pos;
            wall.layer = LayerMask.NameToLayer("Ground"); // So it blocks movement

            GameObject grid = GameObject.Find("LevelGrid");
            if (grid != null) wall.transform.SetParent(grid.transform);

            wall.AddComponent<BoxCollider2D>();
            
            var hs = wall.AddComponent<HealthSystem>();
            hs.SetMaxHealth(3); // Takes 3 hits to destroy (much better player feedback!)
            
            wall.AddComponent<Damageable>();

            wall.AddComponent<BreakableWall>();

            var spriteRenderer = wall.AddComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                spriteRenderer.sortingOrder = 5; // behind player/monsters (10/15), in front of background
                
                // Draw a beautiful cracked brick texture
                Texture2D tex = new Texture2D(16, 16);
                for (int x = 0; x < 16; x++)
                {
                    for (int y = 0; y < 16; y++)
                    {
                        bool isBorder = (x == 0 || x == 15 || y == 0 || y == 15);
                        bool isCrack = (x == y || x == 15 - y || x == 8 || y == 8);
                        
                        if (isBorder)
                            tex.SetPixel(x, y, new Color(0.2f, 0.2f, 0.2f));
                        else if (isCrack)
                            tex.SetPixel(x, y, new Color(0.3f, 0.1f, 0.1f)); // Dark red cracks
                        else
                            tex.SetPixel(x, y, new Color(0.5f, 0.45f, 0.4f)); // Cracked block base
                    }
                }
                tex.filterMode = FilterMode.Point;
                tex.Apply();
                
                spriteRenderer.sprite = Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16f);
            }

            activeRoomEntities.Add(wall);
        }

        private void SpawnWallShooterTrap(Vector3 pos, Vector2 direction)
        {
            GameObject trap = GameObject.CreatePrimitive(PrimitiveType.Quad);
            trap.name = "WallShooterTrap";
            trap.transform.position = pos;
            
            var col = trap.GetComponent<Collider>();
            if (col != null) DestroyImmediate(col);
            
            trap.AddComponent<BoxCollider2D>().size = new Vector2(0.9f, 0.9f);
            
            var wallShooter = trap.AddComponent<WallShooterTrap>();
            wallShooter.shootDirection = direction;
            wallShooter.shootCooldown = 2.0f; // shoots every 2 seconds
            wallShooter.damage = 10;

            var renderer = trap.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.material = new Material(Shader.Find("Sprites/Default"));
                
                // Draw a mouth/cannon face pointing in the shooting direction
                Texture2D tex = new Texture2D(16, 16);
                for (int x = 0; x < 16; x++)
                {
                    for (int y = 0; y < 16; y++)
                    {
                        bool isBorder = (x == 0 || x == 15 || y == 0 || y == 15);
                        bool isBarrel = false;
                        if (direction == Vector2.left)
                            isBarrel = (x <= 5 && y >= 6 && y <= 10);
                        else
                            isBarrel = (x >= 10 && y >= 6 && y <= 10);
                            
                        bool isEye = (x >= 6 && x <= 9 && (y == 12 || y == 4));

                        if (isBarrel)
                            tex.SetPixel(x, y, Color.black);
                        else if (isEye)
                            tex.SetPixel(x, y, Color.red); // glowing red trap eye
                        else if (isBorder)
                            tex.SetPixel(x, y, new Color(0.2f, 0.2f, 0.25f));
                        else
                            tex.SetPixel(x, y, new Color(0.4f, 0.4f, 0.45f));
                    }
                }
                tex.filterMode = FilterMode.Point;
                tex.Apply();
                
                renderer.material.mainTexture = tex;
            }

            activeRoomEntities.Add(trap);
        }

        private void SpawnLadder(Vector3 pos, float height)
        {
            int tx = Mathf.RoundToInt(pos.x);
            bool exists = false;
            foreach (var l in activeLadders)
            {
                if (l.x == tx) { exists = true; break; }
            }
            if (!exists)
            {
                activeLadders.Add(new LadderRange {
                    x = tx,
                    minY = pos.y - height / 2f,
                    maxY = pos.y + height / 2f
                });
            }

            GameObject ladderGo = new GameObject("Ladder");
            ladderGo.transform.position = pos;
            
            var ladderComp = ladderGo.AddComponent<Ladder>();
            
            var boxCol = ladderGo.GetComponent<BoxCollider2D>();
            if (boxCol == null) boxCol = ladderGo.AddComponent<BoxCollider2D>();
            boxCol.isTrigger = true;
            boxCol.size = new Vector2(0.8f, height + 1.2f);
            boxCol.offset = new Vector2(0f, 0.6f);
            
            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(ladderGo.transform, false);
            var sr = visual.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 4; // Behind players/monsters, in front of background
            
            int pixelsHeight = Mathf.RoundToInt(height * 16f);
            if (pixelsHeight < 16) pixelsHeight = 16;
            Texture2D tex = new Texture2D(16, pixelsHeight);
            bool isHighHills = (GetCellStyle(pos) == RoomStyle.HighHills);
            for (int x = 0; x < 16; x++)
            {
                for (int y = 0; y < pixelsHeight; y++)
                {
                    if (isHighHills)
                    {
                        // Draw a thick climbable rope in the middle
                        bool isRopeBody = (x >= 6 && x <= 9);
                        bool isKnot = isRopeBody && (y % 6 == 0 || y % 6 == 1);
                        bool isFiber = isRopeBody && ((x + y) % 3 == 0);
                        
                        if (isKnot)
                        {
                            tex.SetPixel(x, y, new Color(0.55f, 0.48f, 0.38f)); // Darker knot color
                        }
                        else if (isFiber)
                        {
                            tex.SetPixel(x, y, new Color(0.75f, 0.68f, 0.58f)); // Lighter fiber highlight
                        }
                        else if (isRopeBody)
                        {
                            tex.SetPixel(x, y, new Color(0.68f, 0.6f, 0.5f)); // Base rope color
                        }
                        else
                        {
                            tex.SetPixel(x, y, Color.clear);
                        }
                    }
                    else
                    {
                        bool isRail = (x <= 2 || x >= 13);
                        bool isRung = (y % 8 == 0 || y % 8 == 1) && (x > 2 && x < 13);
                        
                        if (isRail || isRung)
                        {
                            tex.SetPixel(x, y, new Color(0.5f, 0.3f, 0.1f)); // wood brown
                        }
                        else
                        {
                            tex.SetPixel(x, y, Color.clear);
                        }
                    }
                }
            }
            tex.filterMode = FilterMode.Point;
            tex.Apply();
            
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 16, pixelsHeight), new Vector2(0.5f, 0.5f), 16f);
            
            activeRoomEntities.Add(ladderGo);
        }

        public int GetCurrentRoomId()
        {
            if (lastActiveRoomId <= 0) return 1;
            return lastActiveRoomId;
        }

        private void SpawnMerchant(Vector3 pos)
        {
            GameObject merchantGo = new GameObject("MerchantNPC");
            merchantGo.transform.position = pos;
            merchantGo.transform.localScale = new Vector3(1.2f, 1.2f, 1f);

            var sr = merchantGo.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 5;

            merchantGo.AddComponent<MerchantNPC>();

            activeRoomEntities.Add(merchantGo);
        }

        private void SpawnPrincessNote(Vector3 pos)
        {
            GameObject noteGo = new GameObject("PrincessNote");
            noteGo.transform.position = pos;
            noteGo.transform.localScale = new Vector3(1.5f, 1.5f, 1f);

            var sr = noteGo.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 4;
            
            // Programmatically draw broken cage + wood base + wax-sealed paper roll sprite
            Texture2D tex = new Texture2D(32, 32);
            for (int x = 0; x < 32; x++)
            {
                for (int y = 0; y < 32; y++)
                {
                    // 1. Ahsap Alt Taban (Wood platform)
                    bool isBase = y >= 2 && y <= 5 && x >= 2 && x <= 29;
                    bool isBaseHighlight = y == 5 && x >= 2 && x <= 29;
                    
                    // 2. Kafes Kemeri Kubbesi (Iron Arch Dome)
                    float dx = x - 15.5f;
                    float dy = y - 18f;
                    float distSqr = dx * dx + dy * dy;
                    bool isArch = distSqr >= 120f && distSqr <= 160f && y >= 18;

                    // 3. Parmakliklar (Iron Bars)
                    // Bar 1 (Sol):
                    bool isBar1 = x == 6 && y >= 5 && y <= 18;
                    // Bar 2 (Kırık - Sol Orta): y > 11 kısmı yok
                    bool isBar2 = x == 11 && y >= 5 && y <= 11;
                    // Bar 3 (Kırık - Sağ Orta): y < 14 kısmı yok
                    bool isBar3 = x == 20 && y >= 14 && y <= 18;
                    // Bar 4 (Sağ):
                    bool isBar4 = x == 25 && y >= 5 && y <= 18;

                    // 4. Parşömen Mektup (Paper scroll at bottom center)
                    bool isPaper = x >= 12 && x <= 19 && y >= 4 && y <= 9;
                    bool isSeal = x >= 15 && x <= 16 && y >= 6 && y <= 7; // Kırmızı mühür damgası

                    if (isSeal && isPaper)
                    {
                        tex.SetPixel(x, y, new Color(0.8f, 0.1f, 0.1f, 1f)); // Kırmızı mum mühür
                    }
                    else if (isPaper)
                    {
                        tex.SetPixel(x, y, new Color(0.96f, 0.92f, 0.82f, 1f)); // Krem rengi kağıt
                    }
                    else if (isBaseHighlight)
                    {
                        tex.SetPixel(x, y, new Color(0.6f, 0.38f, 0.22f, 1f)); // Ahşap ışık çizgisi
                    }
                    else if (isBase)
                    {
                        tex.SetPixel(x, y, new Color(0.4f, 0.22f, 0.12f, 1f)); // Ahşap taban
                    }
                    else if (isArch || isBar1 || isBar2 || isBar3 || isBar4)
                    {
                        tex.SetPixel(x, y, new Color(0.28f, 0.28f, 0.32f, 1f)); // Demir kafes rengi
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            tex.filterMode = FilterMode.Point;
            tex.Apply();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 16f);

            var col = noteGo.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(1.8f, 1.8f);

            noteGo.AddComponent<PrincessNoteInteractable>();
            activeRoomEntities.Add(noteGo);
        }

#if UNITY_EDITOR
        private bool LoadCustomMonsterAnimations(SpriteAnimator animator, EnemyGuardian.MonsterBehavior behavior, int roomLevel)
        {
            List<SpriteAnimator.AnimationClip> animClips = new List<SpriteAnimator.AnimationClip>();

            // If it is the Level 30 Boss (Bringer of Death), load from Assets/Bringer Of Death/Sprite Sheet/Bringer-of-Death-SpritSheet.png
            if (behavior == EnemyGuardian.MonsterBehavior.Boss && roomLevel == 30)
            {
                string path = "Assets/Bringer Of Death/Sprite Sheet/Bringer-of-Death-SpritSheet.png";
                Sprite[] allSprites = LoadBringerSprites(path);
                if (allSprites != null && allSprites.Length >= 64)
                {
                    // 1. Idle (0..7)
                    Sprite[] idleSprites = ExtractSprites(allSprites, 0, 8);
                    animClips.Add(new SpriteAnimator.AnimationClip { state = AnimState.Idle, frames = idleSprites, frameRate = 8f, loop = true });

                    // 2. Walk (8..15)
                    Sprite[] walkSprites = ExtractSprites(allSprites, 8, 8);
                    animClips.Add(new SpriteAnimator.AnimationClip { state = AnimState.Walk, frames = walkSprites, frameRate = 8f, loop = true });

                    // 3. Attack (16..25)
                    Sprite[] attackSprites = ExtractSprites(allSprites, 16, 10);
                    animClips.Add(new SpriteAnimator.AnimationClip { state = AnimState.Attack, frames = attackSprites, frameRate = 10f, loop = false });

                    // 4. Hurt (26..28)
                    Sprite[] hurtSprites = ExtractSprites(allSprites, 26, 3);
                    animClips.Add(new SpriteAnimator.AnimationClip { state = AnimState.Hurt, frames = hurtSprites, frameRate = 8f, loop = false });

                    // 5. Death (29..38)
                    Sprite[] deathSprites = ExtractSprites(allSprites, 29, 10);
                    animClips.Add(new SpriteAnimator.AnimationClip { state = AnimState.Death, frames = deathSprites, frameRate = 8f, loop = false });

                    // 6. Cast (39..47)
                    Sprite[] castSprites = ExtractSprites(allSprites, 39, 9);
                    animClips.Add(new SpriteAnimator.AnimationClip { state = AnimState.Cast, frames = castSprites, frameRate = 8f, loop = false });

                    // 7. Spell (48..63)
                    Sprite[] spellSprites = ExtractSprites(allSprites, 48, 16);
                    animClips.Add(new SpriteAnimator.AnimationClip { state = AnimState.Spell, frames = spellSprites, frameRate = 12f, loop = false });

                    if (animClips.Count > 0)
                    {
                        animator.SetClips(animClips);
                        return true;
                    }
                }
                return false;
            }

            // If it is the Level 20 Boss (Dragon Warrior), load from Dragon Warrior Files/Dragon Warrior PNG
            if (behavior == EnemyGuardian.MonsterBehavior.Boss && roomLevel == 20)
            {
                string folder = "Assets/Dragon Warrior Files/Dragon Warrior PNG";

                // 1. Idle (idle_01 to idle_06)
                Sprite[] idleSprites = LoadMultipleAssetSprites(folder, "idle", 6, "00");
                if (idleSprites != null && idleSprites.Length > 0)
                    animClips.Add(new SpriteAnimator.AnimationClip { state = AnimState.Idle, frames = idleSprites, frameRate = 8f, loop = true });

                // 2. Walk (walk_01 to walk_06)
                Sprite[] walkSprites = LoadMultipleAssetSprites(folder, "walk", 6, "00");
                if (walkSprites == null || walkSprites.Length == 0) walkSprites = idleSprites;
                animClips.Add(new SpriteAnimator.AnimationClip { state = AnimState.Walk, frames = walkSprites, frameRate = 10f, loop = true });

                // 3. Attack (strike_01 to strike_05)
                Sprite[] attackSprites = LoadMultipleAssetSprites(folder, "strike", 5, "00");
                if (attackSprites == null || attackSprites.Length == 0) attackSprites = idleSprites;
                animClips.Add(new SpriteAnimator.AnimationClip { state = AnimState.Attack, frames = attackSprites, frameRate = 12f, loop = false });

                // 4. Death (die_001 to die_010)
                Sprite[] deathSprites = LoadMultipleAssetSprites(folder, "die", 10, "000");
                if (deathSprites == null || deathSprites.Length == 0) deathSprites = new Sprite[] { idleSprites[0] };
                animClips.Add(new SpriteAnimator.AnimationClip { state = AnimState.Death, frames = deathSprites, frameRate = 8f, loop = false });

                // 5. Hurt (hurt_01 to hurt_02)
                Sprite[] hurtSprites = LoadMultipleAssetSprites(folder, "hurt", 2, "00");
                if (hurtSprites == null || hurtSprites.Length == 0) hurtSprites = new Sprite[] { idleSprites[0] };
                animClips.Add(new SpriteAnimator.AnimationClip { state = AnimState.Hurt, frames = hurtSprites, frameRate = 8f, loop = false });

                if (animClips.Count > 0)
                {
                    animator.SetClips(animClips);
                    return true;
                }
                return false;
            }

            // If it is the Level 10 Boss, load from the new NYKNCK Slime folder!
            if (behavior == EnemyGuardian.MonsterBehavior.Boss && roomLevel == 10)
            {
                // 1. Idle
                Sprite[] idleSprites = LoadSlimeSpritesFromFolder("Idle");
                if (idleSprites != null && idleSprites.Length > 0)
                    animClips.Add(new SpriteAnimator.AnimationClip { state = AnimState.Idle, frames = idleSprites, frameRate = 6f, loop = true });

                // 2. Walk
                Sprite[] walkSprites = LoadSlimeSpritesFromFolder("Walk");
                if (walkSprites == null || walkSprites.Length == 0) walkSprites = idleSprites;
                animClips.Add(new SpriteAnimator.AnimationClip { state = AnimState.Walk, frames = walkSprites, frameRate = 8f, loop = true });

                // 3. Attack (spin)
                Sprite[] attackSprites = LoadSlimeSpritesFromFolder("spin");
                if (attackSprites == null || attackSprites.Length == 0) attackSprites = idleSprites;
                animClips.Add(new SpriteAnimator.AnimationClip { state = AnimState.Attack, frames = attackSprites, frameRate = 12f, loop = false });

                // 4. Death (Sleep)
                Sprite[] deathSprites = LoadSlimeSpritesFromFolder("Sleep");
                if (deathSprites == null || deathSprites.Length == 0) deathSprites = new Sprite[] { idleSprites[0] };
                animClips.Add(new SpriteAnimator.AnimationClip { state = AnimState.Death, frames = deathSprites, frameRate = 8f, loop = false });

                // 5. Hurt (Jump)
                Sprite[] hurtSprites = LoadSlimeSpritesFromFolder("Jump");
                if (hurtSprites == null || hurtSprites.Length == 0) hurtSprites = new Sprite[] { idleSprites[0] };
                animClips.Add(new SpriteAnimator.AnimationClip { state = AnimState.Hurt, frames = hurtSprites, frameRate = 8f, loop = false });

                if (animClips.Count > 0)
                {
                    animator.SetClips(animClips);
                    return true;
                }
                return false;
            }

            // If it is the Level 40 Boss (Evil Wizard Boss), load from the new EVil Wizard folder!
            if (behavior == EnemyGuardian.MonsterBehavior.Boss && roomLevel == 40)
            {
                // 1. Idle
                Sprite[] idleSprites = LoadAssetSprites("Assets/EVil Wizard/Sprites/Idle.png");
                if (idleSprites != null && idleSprites.Length > 0)
                    animClips.Add(new SpriteAnimator.AnimationClip { state = AnimState.Idle, frames = idleSprites, frameRate = 6f, loop = true });

                // 2. Walk
                Sprite[] walkSprites = LoadAssetSprites("Assets/EVil Wizard/Sprites/Move.png");
                if (walkSprites == null || walkSprites.Length == 0) walkSprites = idleSprites;
                animClips.Add(new SpriteAnimator.AnimationClip { state = AnimState.Walk, frames = walkSprites, frameRate = 8f, loop = true });

                // 3. Attack
                Sprite[] attackSprites = LoadAssetSprites("Assets/EVil Wizard/Sprites/Attack.png");
                if (attackSprites == null || attackSprites.Length == 0) attackSprites = idleSprites;
                animClips.Add(new SpriteAnimator.AnimationClip { state = AnimState.Attack, frames = attackSprites, frameRate = 8f, loop = false });

                // 4. Death
                Sprite[] deathSprites = LoadAssetSprites("Assets/EVil Wizard/Sprites/Death.png");
                if (deathSprites == null || deathSprites.Length == 0) deathSprites = new Sprite[] { idleSprites[0] };
                animClips.Add(new SpriteAnimator.AnimationClip { state = AnimState.Death, frames = deathSprites, frameRate = 8f, loop = false });

                // 5. Hurt
                Sprite[] hurtSprites = LoadAssetSprites("Assets/EVil Wizard/Sprites/Take Hit.png");
                if (hurtSprites == null || hurtSprites.Length == 0) hurtSprites = new Sprite[] { idleSprites[0] };
                animClips.Add(new SpriteAnimator.AnimationClip { state = AnimState.Hurt, frames = hurtSprites, frameRate = 8f, loop = false });

                if (animClips.Count > 0)
                {
                    animator.SetClips(animClips);
                    return true;
                }
                return false;
            }

            // 1. Load Idle
            string idlePath = GetAssetPathForMonster(behavior, roomLevel, AnimState.Idle);
            Sprite[] idleSpritesNormal = LoadAssetSprites(idlePath);
            if (idleSpritesNormal == null || idleSpritesNormal.Length == 0) return false;
            animClips.Add(new SpriteAnimator.AnimationClip { state = AnimState.Idle, frames = idleSpritesNormal, frameRate = 6f, loop = true });

            // 2. Load Walk
            string walkPath = GetAssetPathForMonster(behavior, roomLevel, AnimState.Walk);
            Sprite[] walkSpritesNormal = LoadAssetSprites(walkPath);
            if (walkSpritesNormal == null || walkSpritesNormal.Length == 0) walkSpritesNormal = idleSpritesNormal;
            animClips.Add(new SpriteAnimator.AnimationClip { state = AnimState.Walk, frames = walkSpritesNormal, frameRate = 8f, loop = true });

            // 3. Load Attack
            string attackPath = GetAssetPathForMonster(behavior, roomLevel, AnimState.Attack);
            Sprite[] attackSpritesNormal = LoadAssetSprites(attackPath);
            if (attackSpritesNormal == null || attackSpritesNormal.Length == 0) attackSpritesNormal = idleSpritesNormal;
            animClips.Add(new SpriteAnimator.AnimationClip { state = AnimState.Attack, frames = attackSpritesNormal, frameRate = 10f, loop = false });

            // 4. Load Death
            string deathPath = GetAssetPathForMonster(behavior, roomLevel, AnimState.Death);
            Sprite[] deathSpritesNormal = LoadAssetSprites(deathPath);
            if (deathSpritesNormal == null || deathSpritesNormal.Length == 0) deathSpritesNormal = new Sprite[] { idleSpritesNormal[0] };
            animClips.Add(new SpriteAnimator.AnimationClip { state = AnimState.Death, frames = deathSpritesNormal, frameRate = 8f, loop = false });

            // 5. Load Hurt
            string hurtPath = GetAssetPathForMonster(behavior, roomLevel, AnimState.Hurt);
            Sprite[] hurtSpritesNormal = LoadAssetSprites(hurtPath);
            if (hurtSpritesNormal == null || hurtSpritesNormal.Length == 0) hurtSpritesNormal = new Sprite[] { idleSpritesNormal[0] };
            animClips.Add(new SpriteAnimator.AnimationClip { state = AnimState.Hurt, frames = hurtSpritesNormal, frameRate = 8f, loop = false });

            animator.SetClips(animClips);
            return true;
        }

        private Sprite[] LoadSlimeSpritesFromFolder(string folderName)
        {
            string folderPath = "Assets/Slime/" + folderName;
            if (!System.IO.Directory.Exists(folderPath))
            {
                if (folderName == "spin" && System.IO.Directory.Exists("Assets/Slime/Spin")) folderPath = "Assets/Slime/Spin";
                else if (folderName == "Walk" && System.IO.Directory.Exists("Assets/Slime/walk")) folderPath = "Assets/Slime/walk";
                else return null;
            }

            string[] files = System.IO.Directory.GetFiles(folderPath, "*.png");
            System.Collections.Generic.List<Sprite> list = new System.Collections.Generic.List<Sprite>();
            foreach (var file in files)
            {
                string assetPath = file.Replace("\\", "/");
                Sprite s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                if (s != null)
                {
                    list.Add(s);
                }
            }
            list.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase));
            return list.ToArray();
        }

        private Sprite[] LoadAssetSprites(string path)
        {
            object[] assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);
            if (assets == null || assets.Length == 0) return null;

            List<Sprite> sprites = new List<Sprite>();
            foreach (var asset in assets)
            {
                if (asset is Sprite sprite)
                {
                    sprites.Add(sprite);
                }
            }

            if (sprites.Count > 0)
            {
                // Sort by name to keep them in order of frame sequence
                sprites.Sort((a, b) => a.name.CompareTo(b.name));
                return sprites.ToArray();
            }
            return null;
        }

        private Sprite[] LoadMultipleAssetSprites(string folderPath, string filePrefix, int frameCount, string format = "00")
        {
            List<Sprite> sprites = new List<Sprite>();
            for (int i = 1; i <= frameCount; i++)
            {
                string frameStr = i.ToString(format);
                string fullPath = $"{folderPath}/{filePrefix}_{frameStr}.png";
                Sprite sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(fullPath);
                if (sp != null)
                {
                    sprites.Add(sp);
                }
            }
            if (sprites.Count > 0)
            {
                return sprites.ToArray();
            }
            return null;
        }

        private Sprite[] LoadBringerSprites(string path)
        {
            object[] assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);
            if (assets == null || assets.Length == 0) return null;

            List<Sprite> sprites = new List<Sprite>();
            foreach (var asset in assets)
            {
                if (asset is Sprite sprite)
                {
                    sprites.Add(sprite);
                }
            }

            if (sprites.Count > 0)
            {
                // Numerically sort sprites: Bringer-of-Death-SpritSheet_0, Bringer-of-Death-SpritSheet_1, ..., Bringer-of-Death-SpritSheet_63
                sprites.Sort((a, b) => {
                    int numA = GetSpriteNumber(a.name);
                    int numB = GetSpriteNumber(b.name);
                    return numA.CompareTo(numB);
                });
                return sprites.ToArray();
            }
            return null;
        }

        private static int GetSpriteNumber(string name)
        {
            int lastUnderscore = name.LastIndexOf('_');
            if (lastUnderscore >= 0 && lastUnderscore < name.Length - 1)
            {
                int val;
                if (int.TryParse(name.Substring(lastUnderscore + 1), out val))
                {
                    return val;
                }
            }
            return 0;
        }

        private Sprite[] ExtractSprites(Sprite[] source, int startIndex, int count)
        {
            if (source == null || startIndex < 0 || startIndex + count > source.Length) return null;
            Sprite[] result = new Sprite[count];
            System.Array.Copy(source, startIndex, result, 0, count);
            return result;
        }
#endif

        private string GetAssetPathForMonster(EnemyGuardian.MonsterBehavior behavior, int roomLevel, AnimState state)
        {
            if (behavior == EnemyGuardian.MonsterBehavior.Boss && roomLevel == 10)
            {
                string slimeFile = "";
                switch (state)
                {
                    case AnimState.Idle: slimeFile = "idle.png"; break;
                    case AnimState.Walk: slimeFile = "walk.png"; break;
                    case AnimState.Attack: slimeFile = "attack.png"; break;
                    case AnimState.Death: slimeFile = "death.png"; break;
                    case AnimState.Hurt: slimeFile = "hurt.png"; break;
                }
                return $"Assets/Monsters Creatures Fantasy 2/Sprites/Slime/{slimeFile}";
            }

            if (behavior == EnemyGuardian.MonsterBehavior.Boss && roomLevel == 40)
            {
                string wizardFile = "";
                switch (state)
                {
                    case AnimState.Idle: wizardFile = "Idle.png"; break;
                    case AnimState.Walk: wizardFile = "Move.png"; break;
                    case AnimState.Attack: wizardFile = "Attack.png"; break;
                    case AnimState.Death: wizardFile = "Death.png"; break;
                    case AnimState.Hurt: wizardFile = "Take Hit.png"; break;
                }
                return $"Assets/EVil Wizard/Sprites/{wizardFile}";
            }

            int biome = (roomLevel - 1) / 10;
            if (biome < 0) biome = 0;
            if (biome > 4) biome = 4;

            string creature = "";
            string baseFolder = "Assets/Monsters Creatures Fantasy/Sprites";
            
            if (biome == 0) // Mossy Forest
            {
                if (behavior == EnemyGuardian.MonsterBehavior.ClubMelee) creature = "Goblin";
                else if (behavior == EnemyGuardian.MonsterBehavior.DaggerThrower) creature = "Mushroom";
                else if (behavior == EnemyGuardian.MonsterBehavior.FlameMage) creature = "Flying eye";
                else creature = "Goblin";
            }
            else if (biome == 1) // Ancient Temple
            {
                if (behavior == EnemyGuardian.MonsterBehavior.ClubMelee) creature = "Skeleton";
                else if (behavior == EnemyGuardian.MonsterBehavior.DaggerThrower) { creature = "Slime"; baseFolder = "Assets/Monsters Creatures Fantasy 2/Sprites"; }
                else if (behavior == EnemyGuardian.MonsterBehavior.FlameMage) { creature = "Bat"; baseFolder = "Assets/Monsters Creatures Fantasy 2/Sprites"; }
                else creature = "Skeleton";
            }
            else if (biome == 2) // Frozen Cavern
            {
                if (behavior == EnemyGuardian.MonsterBehavior.ClubMelee) { creature = "Slime"; baseFolder = "Assets/Monsters Creatures Fantasy 2/Sprites"; }
                else if (behavior == EnemyGuardian.MonsterBehavior.DaggerThrower) { creature = "Rat"; baseFolder = "Assets/Monsters Creatures Fantasy 2/Sprites"; }
                else if (behavior == EnemyGuardian.MonsterBehavior.FlameMage) { creature = "Bat"; baseFolder = "Assets/Monsters Creatures Fantasy 2/Sprites"; }
                else { creature = "Slime"; baseFolder = "Assets/Monsters Creatures Fantasy 2/Sprites"; }
            }
            else if (biome == 3) // Void Cellar
            {
                if (behavior == EnemyGuardian.MonsterBehavior.ClubMelee) { creature = "Mimic"; baseFolder = "Assets/Monsters Creatures Fantasy 2/Sprites"; }
                else if (behavior == EnemyGuardian.MonsterBehavior.DaggerThrower) creature = "Skeleton";
                else if (behavior == EnemyGuardian.MonsterBehavior.FlameMage) creature = "Flying eye";
                else { creature = "Mimic"; baseFolder = "Assets/Monsters Creatures Fantasy 2/Sprites"; }
            }
            else // Magma Keep
            {
                if (behavior == EnemyGuardian.MonsterBehavior.ClubMelee) creature = "Skeleton";
                else if (behavior == EnemyGuardian.MonsterBehavior.DaggerThrower) { creature = "Slime"; baseFolder = "Assets/Monsters Creatures Fantasy 2/Sprites"; }
                else if (behavior == EnemyGuardian.MonsterBehavior.FlameMage) { creature = "Bat"; baseFolder = "Assets/Monsters Creatures Fantasy 2/Sprites"; }
                else creature = "Skeleton";
            }

            string file = "";
            switch (state)
            {
                case AnimState.Idle:
                    if (creature == "Flying eye") file = "Flight.png";
                    else if (creature == "Bat") file = "fly.png";
                    else if (creature == "Mimic") file = "idle_transformed.png";
                    else file = "Idle.png";
                    break;
                case AnimState.Walk:
                    if (creature == "Flying eye") file = "Flight.png";
                    else if (creature == "Bat") file = "fly.png";
                    else if (creature == "Skeleton") file = "Walk.png";
                    else if (creature == "Slime") file = "walk.png";
                    else if (creature == "Mimic") file = "walk.png";
                    else if (creature == "Rat") file = "run.png";
                    else if (creature == "Goblin" || creature == "Mushroom") file = "Run.png";
                    else file = "Idle.png";
                    break;
                case AnimState.Attack:
                    if (creature == "Flying eye" || creature == "Goblin" || creature == "Mushroom" || creature == "Skeleton") file = "Attack1.png";
                    else if (creature == "Bat" || creature == "Slime") file = "attack.png";
                    else if (creature == "Rat") file = "attack_bite.png";
                    else if (creature == "Mimic") file = "attack_1.png";
                    break;
                case AnimState.Death:
                    if (creature == "Rat") file = "rat-death.png";
                    else if (creature == "Bat" || creature == "Slime" || creature == "Mimic") file = "death.png";
                    else file = "Death.png";
                    break;
                case AnimState.Hurt:
                    if (creature == "Bat" || creature == "Slime" || creature == "Mimic" || creature == "Rat") file = "hurt.png";
                    else file = "Take Hit.png";
                    break;
            }

            return $"{baseFolder}/{creature}/{file}";
        }

        private string GetCreatureNameForMonster(EnemyGuardian.MonsterBehavior behavior, int roomLevel)
        {
            if (behavior == EnemyGuardian.MonsterBehavior.Boss && roomLevel == 10)
            {
                return "Slime";
            }
            if (behavior == EnemyGuardian.MonsterBehavior.Boss && roomLevel == 30)
            {
                return "Bringer of Death";
            }
            if (behavior == EnemyGuardian.MonsterBehavior.Boss && roomLevel == 20)
            {
                return "Dragon Warrior";
            }
            if (behavior == EnemyGuardian.MonsterBehavior.Boss && roomLevel == 40)
            {
                return "Evil Wizard";
            }
            if (behavior == EnemyGuardian.MonsterBehavior.Boss && roomLevel == 50)
            {
                return "Magma Dragon";
            }

            int biome = (roomLevel - 1) / 10;
            if (biome < 0) biome = 0;
            if (biome > 4) biome = 4;

            if (biome == 0) // Mossy Forest
            {
                if (behavior == EnemyGuardian.MonsterBehavior.ClubMelee) return "Goblin";
                if (behavior == EnemyGuardian.MonsterBehavior.DaggerThrower) return "Mushroom";
                if (behavior == EnemyGuardian.MonsterBehavior.FlameMage) return "Flying eye";
                return "Goblin";
            }
            else if (biome == 1) // Ancient Temple
            {
                if (behavior == EnemyGuardian.MonsterBehavior.ClubMelee) return "Skeleton";
                if (behavior == EnemyGuardian.MonsterBehavior.DaggerThrower) return "Slime";
                if (behavior == EnemyGuardian.MonsterBehavior.FlameMage) return "Bat";
                return "Skeleton";
            }
            else if (biome == 2) // Frozen Cavern
            {
                if (behavior == EnemyGuardian.MonsterBehavior.ClubMelee) return "Slime";
                if (behavior == EnemyGuardian.MonsterBehavior.DaggerThrower) return "Rat";
                if (behavior == EnemyGuardian.MonsterBehavior.FlameMage) return "Bat";
                return "Slime";
            }
            else if (biome == 3) // Void Cellar
            {
                if (behavior == EnemyGuardian.MonsterBehavior.ClubMelee) return "Mimic";
                if (behavior == EnemyGuardian.MonsterBehavior.DaggerThrower) return "Skeleton";
                if (behavior == EnemyGuardian.MonsterBehavior.FlameMage) return "Flying eye";
                return "Mimic";
            }
            else // Magma Keep
            {
                if (behavior == EnemyGuardian.MonsterBehavior.ClubMelee) return "Skeleton";
                if (behavior == EnemyGuardian.MonsterBehavior.DaggerThrower) return "Slime";
                if (behavior == EnemyGuardian.MonsterBehavior.FlameMage) return "Bat";
                return "Skeleton";
            }
        }

        private void GetVisualConfigForCreature(string creature, bool isBoss, out float scaleMult, out float yOffset)
        {
            scaleMult = 1f;
            yOffset = 0f;

            switch (creature)
            {
                case "Bringer of Death":
                    scaleMult = isBoss ? 1.8f : 1.2f; // Scale multiplier is 1.8f since parent scale is 2.0f
                    yOffset = isBoss ? -0.06f : -0.06f; // Standing flat on the ground
                    break;
                case "Dragon Warrior":
                    scaleMult = isBoss ? 1.0f : 1.0f; // Kept at its original import size as requested
                    yOffset = isBoss ? -0.5f : -0.5f;
                    break;
                case "Evil Wizard":
                    scaleMult = isBoss ? 3.2f : 1.0f; // Scale multiplier is 3.2f since parent scale is 1.0f (player size)
                    yOffset = isBoss ? 0.0f : 0.0f; // Standing flat on the ground
                    break;
                case "Magma Dragon":
                    scaleMult = isBoss ? 0.55f : 0.55f;
                    yOffset = isBoss ? -0.5f : -0.5f; // Standing flat on the ground
                    break;
                case "Goblin":
                    scaleMult = 3.2f;
                    yOffset = -0.85f;
                    break;
                case "Mushroom":
                    scaleMult = 3.2f;
                    yOffset = -0.85f;
                    break;
                case "Skeleton":
                    scaleMult = 3.2f;
                    yOffset = -0.85f;
                    break;
                case "Flying eye":
                    scaleMult = 3.0f;
                    yOffset = 0.5f;
                    break;
                case "Bat":
                    scaleMult = 2.8f;
                    yOffset = 0.4f;
                    break;
                case "Slime":
                    scaleMult = isBoss ? 6.25f : 2.8f;
                    yOffset = isBoss ? 0.2f : 0.2f;
                    break;
                case "Rat":
                    scaleMult = 2.8f;
                    yOffset = 0.2f;
                    break;
                case "Mimic":
                    scaleMult = 2.8f;
                    yOffset = 0.2f;
                    break;
            }
        }

        private void SpawnCrystal(Vector3 pos, int biome)
        {
            GameObject crystal = new GameObject("CrystalDecoration");
            crystal.transform.position = pos;
            crystal.transform.localScale = new Vector3(0.6f + Random.Range(0f, 0.4f), 0.6f + Random.Range(0f, 0.4f), 1f);

            var sr = crystal.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 3; // in front of background, behind player

            // Spawn soft glowing crystal ambient light glow!
            SpawnAmbientLightGlow(pos + Vector3.up * 0.3f, new Color(0.2f, 0.8f, 1f), 3.5f);

            // Draw a beautiful glowing crystal sprite
            Texture2D tex = new Texture2D(16, 16);
            Color cMain = Color.cyan;
            Color cSec = Color.white;

            if (biome == 0) { cMain = new Color(0f, 0.9f, 0.4f, 0.9f); cSec = new Color(0.6f, 1f, 0.8f, 0.95f); } // Emerald
            else if (biome == 1) { cMain = new Color(1f, 0.7f, 0f, 0.9f); cSec = new Color(1f, 0.95f, 0.6f, 0.95f); } // Amber
            else if (biome == 2) { cMain = new Color(0f, 0.75f, 1f, 0.9f); cSec = new Color(0.7f, 0.95f, 1f, 0.95f); } // Ice Blue
            else if (biome == 3) { cMain = new Color(0.75f, 0f, 1f, 0.9f); cSec = new Color(0.95f, 0.7f, 1f, 0.95f); } // Void Purple
            else { cMain = new Color(1f, 0.3f, 0f, 0.9f); cSec = new Color(1f, 0.8f, 0.2f, 0.95f); } // Magma Orange

            for (int x = 0; x < 16; x++)
            {
                for (int y = 0; y < 16; y++)
                {
                    float dx = x - 7.5f;
                    float dy = y - 3f;
                    bool isShard1 = (Mathf.Abs(dx) * 1.5f + dy * 0.7f <= 6f && dy >= 0);
                    bool isShard2 = (Mathf.Abs(dx - 3f) * 1.8f + (dy - 2f) * 0.8f <= 4f && dy >= 2);
                    bool isShard3 = (Mathf.Abs(dx + 3f) * 1.8f + (dy - 1f) * 0.8f <= 4f && dy >= 1);

                    if (isShard1 || isShard2 || isShard3)
                    {
                        bool isHighlight = (x == 7 || x == 10 || x == 4 || y == 14 || y == 10);
                        tex.SetPixel(x, y, isHighlight ? cSec : cMain);
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            tex.filterMode = FilterMode.Point;
            tex.Apply();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.1f), 16f);

            crystal.AddComponent<CrystalGlow>();
            activeRoomEntities.Add(crystal);
        }

        private void SpawnStalactite(Vector3 pos, int biome)
        {
            GameObject spike = new GameObject("StalactiteDecoration");
            spike.transform.position = pos;
            spike.transform.localScale = new Vector3(0.8f + Random.Range(0f, 0.4f), 0.8f + Random.Range(0f, 0.6f), 1f);

            var sr = spike.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 3;

            Color stone = GetWallColorForBiome(biome);

            Texture2D tex = new Texture2D(16, 16);
            for (int x = 0; x < 16; x++)
            {
                for (int y = 0; y < 16; y++)
                {
                    float dist = Mathf.Abs(x - 7.5f);
                    float limit = (15 - y) * 0.5f;
                    if (dist <= limit + 0.5f)
                    {
                        float shade = 0.5f + (y / 32f);
                        if (x == 7 || x == 8) shade += 0.15f;
                        tex.SetPixel(x, y, stone * shade);
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            tex.filterMode = FilterMode.Point;
            tex.Apply();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 1.0f), 16f);
            activeRoomEntities.Add(spike);
        }

        private void SpawnStalagmite(Vector3 pos, int biome)
        {
            GameObject spike = new GameObject("StalagmiteDecoration");
            spike.transform.position = pos;
            spike.transform.localScale = new Vector3(0.8f + Random.Range(0f, 0.4f), 0.8f + Random.Range(0f, 0.6f), 1f);

            var sr = spike.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 3;

            Color stone = GetWallColorForBiome(biome);

            Texture2D tex = new Texture2D(16, 16);
            for (int x = 0; x < 16; x++)
            {
                for (int y = 0; y < 16; y++)
                {
                    float dist = Mathf.Abs(x - 7.5f);
                    float limit = y * 0.5f;
                    if (dist <= (7.5f - limit))
                    {
                        float shade = 0.5f + ((15 - y) / 32f);
                        if (x == 7 || x == 8) shade += 0.15f;
                        tex.SetPixel(x, y, stone * shade);
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            tex.filterMode = FilterMode.Point;
            tex.Apply();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.0f), 16f);
            activeRoomEntities.Add(spike);
        }

        private Color GetWallColorForBiome(int biome)
        {
            if (biome == 0) return new Color(0.24f, 0.16f, 0.1f);
            if (biome == 1) return new Color(0.38f, 0.3f, 0.22f);
            if (biome == 2) return new Color(0.12f, 0.28f, 0.42f);
            if (biome == 3) return new Color(0.1f, 0.06f, 0.16f);
            return new Color(0.06f, 0.06f, 0.07f);
        }

        private void SpawnTorch(Vector3 pos)
        {
            GameObject torch = new GameObject("WallTorch");
            torch.transform.position = pos;
            torch.transform.localScale = new Vector3(1.2f, 1.2f, 1f);

            var sr = torch.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 3;

            Sprite[] frames = new Sprite[3];
            for (int f = 0; f < 3; f++)
            {
                Texture2D tex = new Texture2D(16, 16);
                Color wood = new Color(0.4f, 0.25f, 0.1f);
                Color iron = new Color(0.3f, 0.3f, 0.35f);
                Color fire1 = new Color(1f, 0.35f, 0f);
                Color fire2 = new Color(1f, 0.8f, 0f);
                Color fire3 = new Color(1f, 0.95f, 0.6f);

                for (int x = 0; x < 16; x++)
                {
                    for (int y = 0; y < 16; y++)
                    {
                        bool isHolder = (x == 7 || x == 8) && (y >= 2 && y <= 7);
                        bool isSconce = (x >= 5 && x <= 10) && (y == 7 || y == 8);
                        
                        float dx = x - 7.5f;
                        float dy = y - 9f;
                        bool isFire = false;
                        Color fireCol = fire1;

                        if (f == 0)
                        {
                            isFire = (Mathf.Abs(dx) <= 2.5f - dy * 0.4f && dy >= 0 && dy <= 5);
                            if (dy > 3) fireCol = fire3;
                            else if (dy > 1.5f) fireCol = fire2;
                        }
                        else if (f == 1)
                        {
                            isFire = (Mathf.Abs(dx + 0.5f) <= 2.5f - dy * 0.4f && dy >= 0 && dy <= 5);
                            if (dy > 3) fireCol = fire3;
                            else if (dy > 1.5f) fireCol = fire2;
                        }
                        else
                        {
                            isFire = (Mathf.Abs(dx - 0.5f) <= 2.5f - dy * 0.4f && dy >= 0 && dy <= 5);
                            if (dy > 3) fireCol = fire3;
                            else if (dy > 1.5f) fireCol = fire2;
                        }

                        if (isFire && y >= 9)
                        {
                            tex.SetPixel(x, y, fireCol);
                        }
                        else if (isSconce)
                        {
                            tex.SetPixel(x, y, iron);
                        }
                        else if (isHolder)
                        {
                            tex.SetPixel(x, y, wood);
                        }
                        else
                        {
                            tex.SetPixel(x, y, Color.clear);
                        }
                    }
                }
                tex.filterMode = FilterMode.Point;
                tex.Apply();
                frames[f] = Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.3f), 16f);
            }

            sr.sprite = frames[0];
            var tf = torch.AddComponent<TorchFlame>();
            tf.frames = frames;
            tf.fps = 7f + Random.Range(-1f, 1f);

            activeRoomEntities.Add(torch);
            // Spawn soft warm orange fire ambient light glow!
            SpawnAmbientLightGlow(pos + Vector3.up * 0.2f, new Color(1f, 0.5f, 0.1f), 4.5f);
        }

        private void SpawnMovingCloud(Vector3 pos, float startX, float endX)
        {
            GameObject cloud = new GameObject("BG_Cloud");
            cloud.transform.position = pos;
            cloud.transform.localScale = new Vector3(Random.Range(2f, 3.5f), Random.Range(1f, 1.8f), 1f);

            var sr = cloud.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 2;
            
            Texture2D tex = new Texture2D(32, 16);
            Color cCloud = new Color(1f, 1f, 1f, 0.55f);
            Color cCloudDark = new Color(0.85f, 0.9f, 0.95f, 0.55f);

            for (int x = 0; x < 32; x++)
            {
                for (int y = 0; y < 16; y++)
                {
                    float dx = (x - 15.5f) / 13f;
                    float dy = (y - 5f) / 6f;
                    float dist = dx * dx + dy * dy;
                    if (dist <= 0.8f)
                    {
                        bool isFluffyBase = (y <= 4);
                        tex.SetPixel(x, y, isFluffyBase ? cCloudDark : cCloud);
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            tex.filterMode = FilterMode.Point;
            tex.Apply();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 32, 16), new Vector2(0.5f, 0.5f), 16f);

            var mc = cloud.AddComponent<MovingCloud>();
            mc.speed = Random.Range(0.2f, 0.6f);
            mc.startX = startX;
            mc.endX = endX;

            activeRoomEntities.Add(cloud);
        }

        private void SpawnBackgroundEagle(Vector3 pos, float startX, float endX)
        {
            GameObject eagle = new GameObject("BG_Eagle");
            eagle.transform.position = pos;
            eagle.transform.localScale = new Vector3(1.2f, 1.2f, 1f);

            var sr = eagle.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 2;

            Sprite[] frames = new Sprite[2];
            Color body = new Color(0.25f, 0.2f, 0.18f);
            Color beak = Color.yellow;

            for (int f = 0; f < 2; f++)
            {
                Texture2D tex = new Texture2D(16, 16);
                for (int x = 0; x < 16; x++)
                {
                    for (int y = 0; y < 16; y++)
                    {
                        bool isBody = (x >= 6 && x <= 10) && (y >= 6 && y <= 10);
                        bool isHead = (x == 10 || x == 11) && (y == 9 || y == 10);
                        bool isBeak = (x == 12 && y == 9);
                        bool isWing = false;

                        if (f == 0)
                        {
                            isWing = (x < 6 && y >= 7 && y <= 12 && (x + y >= 12)) || 
                                     (x > 10 && y >= 7 && y <= 12 && (y - x >= 0));
                        }
                        else
                        {
                            isWing = (x < 6 && y >= 3 && y <= 8 && (y - x <= 2)) || 
                                     (x > 10 && y >= 3 && y <= 8 && (x + y <= 18));
                        }

                        if (isBeak) tex.SetPixel(x, y, beak);
                        else if (isBody || isHead || isWing) tex.SetPixel(x, y, body);
                        else tex.SetPixel(x, y, Color.clear);
                    }
                }
                tex.filterMode = FilterMode.Point;
                tex.Apply();
                frames[f] = Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16f);
            }

            sr.sprite = frames[0];
            var be = eagle.AddComponent<BackgroundEagle>();
            be.frames = frames;
            be.speed = Random.Range(1.8f, 3.2f);
            be.flapFps = 6f + Random.Range(-1f, 1f);
            be.startX = startX;
            be.endX = endX;

            activeRoomEntities.Add(eagle);
        }

        private void SpawnRopeBridgeTile(Vector3 pos, int biome)
        {
            GameObject tile = new GameObject("RopeBridgeTile");
            tile.transform.position = pos;
            tile.layer = LayerMask.NameToLayer("Ground");
            
            GameObject grid = GameObject.Find("LevelGrid");
            if (grid != null) tile.transform.SetParent(grid.transform);
            
            var boxCol = tile.AddComponent<BoxCollider2D>();
            boxCol.usedByComposite = true;
            
            MeshFilter mf = tile.AddComponent<MeshFilter>();
            mf.sharedMesh = GetQuadMesh();
            
            var renderer = tile.AddComponent<MeshRenderer>();
            if (renderer != null)
            {
                // Material caching using sharedMaterial to prevent cloning and enable batching
                renderer.sharedMaterial = GetCachedMaterial("RopeBridge_Biome_" + biome, () => {
                    Texture2D tex = new Texture2D(16, 16);
                    Color brown = new Color(0.45f, 0.28f, 0.15f);
                    Color darkBrown = new Color(0.28f, 0.16f, 0.08f);
                    Color rope = new Color(0.7f, 0.62f, 0.52f);
                    
                    for (int x = 0; x < 16; x++)
                    {
                        for (int y = 0; y < 16; y++)
                        {
                            bool isPlank = (y >= 0 && y <= 3);
                            bool isPlankBorder = isPlank && (x % 4 == 0 || y == 3);
                            
                            bool isRopeHorizontal = (y == 11 || y == 12);
                            bool isRopeHanger = (x == 2 || x == 13) && (y >= 4 && y <= 10);

                            if (isPlankBorder) tex.SetPixel(x, y, darkBrown);
                            else if (isPlank) tex.SetPixel(x, y, brown);
                            else if (isRopeHorizontal || isRopeHanger) tex.SetPixel(x, y, rope);
                            else tex.SetPixel(x, y, Color.clear);
                        }
                    }
                    tex.filterMode = FilterMode.Point;
                    tex.Apply();
                    return tex;
                });
            }
            activeRoomEntities.Add(tile);
        }

        public void ClearMapEntities()
        {
            foreach (var go in activeRoomEntities)
            {
                if (go != null) Destroy(go);
            }
            activeRoomEntities.Clear();

            GameObject levelGridGo = GameObject.Find("LevelGrid");
            if (levelGridGo != null) Destroy(levelGridGo);
        }

        private void SpawnBackgroundWallTile(Vector3 pos, int biome, RoomStyle style)
        {
            GameObject bgTile = new GameObject("BG_WallTile");
            bgTile.transform.position = pos;
            
            GameObject gridGo = GameObject.Find("LevelGrid");
            if (gridGo != null) bgTile.transform.SetParent(gridGo.transform);

            MeshFilter mf = bgTile.AddComponent<MeshFilter>();
            mf.sharedMesh = GetQuadMesh();

            var renderer = bgTile.AddComponent<MeshRenderer>();
            if (renderer != null)
            {
                // Material caching using sharedMaterial to prevent cloning and enable batching
                renderer.sharedMaterial = GetCachedMaterial("BG_WallTile_Biome_" + biome + "_Style_" + style, () => {
                    Texture2D tex = new Texture2D(16, 16);
                    for (int x = 0; x < 16; x++)
                    {
                        for (int y = 0; y < 16; y++)
                        {
                            Color c = Color.black;
                            bool isJoint = (y == 0 || y == 8 || (y < 8 && x == 8) || (y >= 8 && (x == 4 || x == 12)));

                            if (style == RoomStyle.Cave)
                            {
                                float noise = Mathf.Sin(x * 0.4f) * Mathf.Cos(y * 0.4f) * 0.05f;
                                Color baseCol = Color.grey;
                                if (biome == 0) baseCol = new Color(0.18f, 0.12f, 0.08f);
                                else if (biome == 1) baseCol = new Color(0.25f, 0.2f, 0.15f);
                                else if (biome == 2) baseCol = new Color(0.08f, 0.18f, 0.28f);
                                else if (biome == 3) baseCol = new Color(0.07f, 0.04f, 0.11f);
                                else baseCol = new Color(0.04f, 0.04f, 0.05f);

                                c = Color.Lerp(baseCol, Color.black, 0.5f - noise);
                            }
                            else // DeepUnderground
                            {
                                Color baseCol = Color.grey;
                                Color jointCol = Color.black;
                                if (biome == 0) { baseCol = new Color(0.12f, 0.08f, 0.05f); jointCol = new Color(0.06f, 0.04f, 0.02f); }
                                else if (biome == 1) { baseCol = new Color(0.18f, 0.14f, 0.1f); jointCol = new Color(0.09f, 0.07f, 0.05f); }
                                else if (biome == 2) { baseCol = new Color(0.05f, 0.12f, 0.2f); jointCol = new Color(0.02f, 0.05f, 0.09f); }
                                else if (biome == 3) { baseCol = new Color(0.05f, 0.03f, 0.08f); jointCol = new Color(0.02f, 0.01f, 0.04f); }
                                else { baseCol = new Color(0.03f, 0.03f, 0.04f); jointCol = new Color(0.01f, 0.01f, 0.02f); }

                                c = isJoint ? jointCol : baseCol;
                            }
                            tex.SetPixel(x, y, c);
                        }
                    }
                    tex.filterMode = FilterMode.Point;
                    tex.Apply();
                    return tex;
                });
            }
            activeRoomEntities.Add(bgTile);
        }

        private void SpawnAmbientLightGlow(Vector3 pos, Color color, float size)
        {
            GameObject glow = new GameObject("LightGlow");
            glow.transform.position = new Vector3(pos.x, pos.y, 4.0f); // background depth
            glow.transform.localScale = new Vector3(size, size, 1f);

            var sr = glow.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 1; // behind props, in front of backdrop
            sr.color = new Color(color.r, color.g, color.b, 0.15f); // subtle soft glow

            Texture2D tex = new Texture2D(32, 32);
            for (int x = 0; x < 32; x++)
            {
                for (int y = 0; y < 32; y++)
                {
                    float dx = (x - 15.5f) / 15.5f;
                    float dy = (y - 15.5f) / 15.5f;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(1f - dist);
                    alpha = alpha * alpha; // smooth falloff
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.filterMode = FilterMode.Bilinear;
            tex.Apply();

            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32f);
            activeRoomEntities.Add(glow);
        }

        private void SpawnFloorDecoration(Vector3 pos, int biome, RoomStyle style)
        {
            GameObject decor = new GameObject("FloorDecoration");
            decor.transform.position = pos;
            
            var sr = decor.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 3; // in front of background/solid block, behind player
            
            Texture2D tex = new Texture2D(16, 16);
            for (int x = 0; x < 16; x++)
            {
                for (int y = 0; y < 16; y++) tex.SetPixel(x, y, Color.clear);
            }

            float r = Random.value;
            if (biome == 0) // Forest: grass tuft, wild flower, or small stone
            {
                if (r < 0.4f) // Grass tuft
                {
                    decor.name = "GrassTuft";
                    Color green = new Color(0.2f, 0.6f, 0.15f);
                    Color darkGreen = new Color(0.12f, 0.45f, 0.1f);
                    for (int x = 2; x < 14; x++)
                    {
                        int h = Random.Range(4, 12);
                        for (int y = 0; y < h; y++)
                        {
                            tex.SetPixel(x, y, (x % 3 == 0) ? darkGreen : green);
                        }
                    }
                }
                else if (r < 0.7f) // Wild flower
                {
                    decor.name = "WildFlower";
                    Color stem = new Color(0.15f, 0.5f, 0.12f);
                    Color petal = (Random.value > 0.5f) ? Color.red : Color.yellow;
                    for (int y = 0; y < 8; y++) tex.SetPixel(8, y, stem);
                    tex.SetPixel(8, 8, Color.yellow);
                    tex.SetPixel(7, 8, petal);
                    tex.SetPixel(9, 8, petal);
                    tex.SetPixel(8, 7, petal);
                    tex.SetPixel(8, 9, petal);
                }
                else // Small pebble/stone
                {
                    decor.name = "Pebble";
                    Color stoneColor = new Color(0.45f, 0.45f, 0.48f);
                    Color highlight = new Color(0.58f, 0.58f, 0.6f);
                    for (int x = 4; x < 12; x++)
                    {
                        int h = 4 - Mathf.Abs(x - 8);
                        for (int y = 0; y < h; y++)
                        {
                            tex.SetPixel(x, y, (y == h - 1) ? highlight : stoneColor);
                        }
                    }
                }
            }
            else if (biome == 1) // Ancient Temple: gold dust, small urn/vase, or slab
            {
                if (r < 0.5f) // Urn/Vase
                {
                    decor.name = "TempleUrn";
                    Color terracotta = new Color(0.7f, 0.45f, 0.3f);
                    Color shadow = new Color(0.5f, 0.3f, 0.2f);
                    for (int x = 5; x <= 11; x++)
                    {
                        int h = (x == 5 || x == 11) ? 8 : 10;
                        for (int y = 0; y < h; y++)
                        {
                            tex.SetPixel(x, y, (x == 5 || y == 0) ? shadow : terracotta);
                        }
                    }
                }
                else // Stone brick slab
                {
                    decor.name = "StoneSlab";
                    Color grey = new Color(0.4f, 0.4f, 0.4f);
                    Color shade = new Color(0.25f, 0.25f, 0.25f);
                    for (int x = 3; x < 13; x++)
                    {
                        for (int y = 0; y < 4; y++)
                        {
                            tex.SetPixel(x, y, (x == 3 || y == 0) ? shade : grey);
                        }
                    }
                }
            }
            else if (biome == 2) // Frozen Crypt: ice shard or snow pile
            {
                if (r < 0.5f) // Ice shard
                {
                    decor.name = "IceShard";
                    Color ice = new Color(0.6f, 0.85f, 1f, 0.8f);
                    Color glint = Color.white;
                    for (int x = 4; x < 12; x++)
                    {
                        int h = 10 - Mathf.Abs(x - 8) * 2;
                        for (int y = 0; y < h; y++)
                        {
                            tex.SetPixel(x, y, (x == 8 || y == h - 1) ? glint : ice);
                        }
                    }
                }
                else // Snow pile
                {
                    decor.name = "SnowPile";
                    Color snow = new Color(0.95f, 0.95f, 1f);
                    Color shading = new Color(0.75f, 0.8f, 0.9f);
                    for (int x = 2; x < 14; x++)
                    {
                        int h = 5 - Mathf.Abs(x - 8) / 2;
                        for (int y = 0; y < h; y++)
                        {
                            tex.SetPixel(x, y, (y == 0) ? shading : snow);
                        }
                    }
                }
            }
            else if (biome == 3) // Void Crypt: purple crystal, skull or bone
            {
                if (r < 0.5f) // Skull
                {
                    decor.name = "BoneSkull";
                    Color bone = new Color(0.85f, 0.85f, 0.8f);
                    Color voidBlack = new Color(0.05f, 0.05f, 0.05f);
                    for (int x = 5; x <= 11; x++)
                    {
                        for (int y = 0; y <= 6; y++)
                        {
                            if (y == 3 && (x == 7 || x == 9)) tex.SetPixel(x, y, voidBlack);
                            else tex.SetPixel(x, y, bone);
                        }
                    }
                }
                else // Purple void spore/crystal
                {
                    decor.name = "VoidCrystal";
                    Color purple = new Color(0.5f, 0.1f, 0.8f);
                    Color highlight = new Color(0.8f, 0.4f, 1f);
                    for (int x = 5; x <= 11; x++)
                    {
                        int h = 9 - Mathf.Abs(x - 8) * 2;
                        for (int y = 0; y < h; y++)
                        {
                            tex.SetPixel(x, y, (y == h - 1) ? highlight : purple);
                        }
                    }
                }
            }
            else // Magma Keep: ash mound or basalt spike
            {
                if (r < 0.5f) // Basalt spike
                {
                    decor.name = "BasaltSpike";
                    Color basalt = new Color(0.15f, 0.15f, 0.18f);
                    Color glow = new Color(1f, 0.3f, 0f);
                    for (int x = 4; x < 12; x++)
                    {
                        int h = 9 - Mathf.Abs(x - 8) * 2;
                        for (int y = 0; y < h; y++)
                        {
                            tex.SetPixel(x, y, (x == 8) ? glow : basalt);
                        }
                    }
                }
                else // Ash mound
                {
                    decor.name = "AshMound";
                    Color ash = new Color(0.3f, 0.25f, 0.25f);
                    Color darkAsh = new Color(0.18f, 0.15f, 0.15f);
                    for (int x = 2; x < 14; x++)
                    {
                        int h = 4 - Mathf.Abs(x - 8) / 3;
                        for (int y = 0; y < h; y++)
                        {
                            tex.SetPixel(x, y, (y == 0) ? darkAsh : ash);
                        }
                    }
                }
            }

            tex.filterMode = FilterMode.Point;
            tex.Apply();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0f), 16f); // aligned to bottom
            activeRoomEntities.Add(decor);
        }
    }

    public class CrystalGlow : MonoBehaviour
    {
        private SpriteRenderer sr;
        private float baseAlpha = 0.8f;
        private float glowSpeed = 2f;
        private float offset;

        private void Start()
        {
            sr = GetComponent<SpriteRenderer>();
            if (sr != null) baseAlpha = sr.color.a;
            offset = Random.Range(0f, 6.28f);
            glowSpeed = Random.Range(1.5f, 2.5f);
        }

        private void Update()
        {
            if (sr != null)
            {
                Color c = sr.color;
                c.a = baseAlpha * (0.6f + 0.4f * Mathf.Sin(Time.time * glowSpeed + offset));
                sr.color = c;
            }
        }
    }

    public class TorchFlame : MonoBehaviour
    {
        public Sprite[] frames;
        public float fps = 6f;
        private SpriteRenderer sr;
        private int currentFrame;
        private float timer;

        private void Start()
        {
            sr = GetComponent<SpriteRenderer>();
        }

        private void Update()
        {
            if (frames == null || frames.Length == 0 || sr == null) return;
            timer += Time.deltaTime;
            if (timer >= 1f / fps)
            {
                timer -= 1f / fps;
                currentFrame = (currentFrame + 1) % frames.Length;
                sr.sprite = frames[currentFrame];
            }
        }
    }

    public class MovingCloud : MonoBehaviour
    {
        public float speed = 1.0f;
        public float startX;
        public float endX;

        private void Update()
        {
            transform.Translate(Vector3.right * speed * Time.deltaTime);
            if (transform.position.x > endX)
            {
                Vector3 pos = transform.position;
                pos.x = startX;
                transform.position = pos;
            }
        }
    }

    public class BackgroundEagle : MonoBehaviour
    {
        public Sprite[] frames;
        public float flapFps = 8f;
        public float speed = 3.0f;
        public float startX;
        public float endX;
        
        private SpriteRenderer sr;
        private int currentFrame;
        private float timer;

        private void Start()
        {
            sr = GetComponent<SpriteRenderer>();
        }

        private void Update()
        {
            transform.Translate(Vector3.right * speed * Time.deltaTime);
            if (transform.position.x > endX)
            {
                Vector3 pos = transform.position;
                pos.x = startX;
                transform.position = pos;
            }

            if (frames == null || frames.Length == 0 || sr == null) return;
            timer += Time.deltaTime;
            if (timer >= 1f / flapFps)
            {
                timer -= 1f / flapFps;
                currentFrame = (currentFrame + 1) % frames.Length;
                sr.sprite = frames[currentFrame];
            }
        }
    }
}
