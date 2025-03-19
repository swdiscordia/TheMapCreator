#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
using System.IO;
using Managers.Maps.MapCreator;
using System;
using System.Collections.Generic;
using Managers.Cameras;
using CreatorMap.Scripts.Data;
using CreatorMap.Scripts.Extensions;
using MapCreator.Data.Models;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using Components.Maps;

namespace MapCreator.Editor
{
    public class MapCreatorWindow : EditorWindow
    {
        private int m_SelectedTab = 0;
        private readonly string[] m_Tabs = { "Map Settings", "Draw Mode" };

        // Map dimension settings
        private int m_MapWidth = (int)Models.Maps.MapConstants.Width;
        private int m_MapHeight = (int)Models.Maps.MapConstants.Height;
        private Vector2 m_MinMapSize = new Vector2(1, 1);
        private Vector2 m_MaxMapSize = new Vector2(50, 50);
        private bool m_ShowDefaultValues = true;

        // Map creation paths
        private const string MAP_ROOT_PATH = "Assets/CreatorMap/Maps";
        private const int MAP_ID_START = 100000000; // Starting ID for maps (9 digits)

        // Cell data bit flags
        private const ushort CELL_WALKABLE = 0x0001;              // Bit 1
        private const ushort CELL_NON_WALKABLE_FIGHT = 0x0002;   // Bit 2
        private const ushort CELL_NON_WALKABLE_RP = 0x0004;      // Bit 3
        private const ushort CELL_LINE_OF_SIGHT = 0x0008;        // Bit 4
        private const ushort CELL_BLUE = 0x0010;                 // Bit 5
        private const ushort CELL_RED = 0x0020;                  // Bit 6
        private const ushort CELL_VISIBLE = 0x0040;              // Bit 7
        private const ushort CELL_FARM = 0x0080;                 // Bit 8
        private const ushort CELL_HAVENBAG = 0x0100;            // Bit 9

        private bool m_ShouldCreateMap = false;

        // Draw mode enum
        public enum DrawMode
        {
            None,
            Walkable,
            NonWalkable,
            TilePlacement
        }
        
        private DrawMode m_CurrentDrawMode = DrawMode.None;
        private TileSpriteData m_SelectedTile = null;

        // Constants for data storage
        private const string DATA_FOLDER_PATH = "Assets/CreatorMap/Content/Data";
        private const string MAP_DATA_FILENAME = "MapData.asset";

        // Add icon texture field at the top of the class, after the enum declaration
        private Texture2D m_BrushIcon;

        private Vector2 m_TileScrollPosition;
        private List<TileSpriteData> m_AvailableTiles = new List<TileSpriteData>();
        public static DrawMode CurrentDrawMode => Instance.m_CurrentDrawMode;
        private static MapCreatorWindow Instance { get; set; }

        private bool m_UseClipping = true; // New variable for clipping toggle
        private bool m_ShowPlacementConfirmation = false; // Whether to show a confirmation dialog after placing a tile

        [MenuItem("Window/Map Creator/Map Creator", false, 100)]
        public static void ShowWindow()
        {
            Debug.Log("Opening Map Creator Window");
            var window = GetWindow<MapCreatorWindow>("Map Creator");
            window.minSize = new Vector2(300, 400);
            Instance = window;
            window.Show();
        }

        private void OnEnable()
        {
            Debug.Log("MapCreatorWindow OnEnable called");
            Instance = this;
            LoadAvailableTiles();
            // Remove any existing handler and add our own
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
            Debug.Log("Registered OnSceneGUI callback successfully");
        }

        private void OnDisable()
        {
            Debug.Log("MapCreatorWindow OnDisable called");
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void LoadAvailableTiles()
        {
            Debug.Log("Loading available tiles...");
            m_AvailableTiles.Clear();
            
            string tilesPath = "Assets/CreatorMap/Content/Tiles";
            if (!Directory.Exists(tilesPath))
            {
                Debug.Log($"Creating tiles directory at {tilesPath}");
                Directory.CreateDirectory(tilesPath);
                AssetDatabase.Refresh();
            }

            string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { tilesPath });
            Debug.Log($"Found {guids.Length} sprites in tiles directory");
            
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null)
                {
                    var tileData = new TileSpriteData
                    {
                        Id = sprite.name,
                        addressablePath = path,
                        Scale = 1f,
                        Order = 0,
                        Color = new TileColorData { Red = 1f, Green = 1f, Blue = 1f, Alpha = 1f }
                    };
                    m_AvailableTiles.Add(tileData);
                }
            }
        }

        private void OnGUI()
        {
            if (EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Map Creator is not available during Play Mode.", MessageType.Warning);
                return;
            }

            // Detect any mouse down events in the editor window itself
            Event e = Event.current;
            if (e != null && e.type == EventType.MouseDown)
            {
                Debug.Log($"Mouse down detected in editor window: button={e.button}, pos={e.mousePosition}");
            }
            
            // Global keyboard shortcut for placing tiles anywhere
            if (e != null && e.type == EventType.KeyDown && (e.keyCode == KeyCode.T || e.keyCode == KeyCode.Return))
            {
                if (m_CurrentDrawMode == DrawMode.TilePlacement && m_SelectedTile != null && SceneView.lastActiveSceneView != null)
                {
                    Debug.Log("*** PLACING TILE VIA KEYBOARD SHORTCUT T/RETURN ***");
                    PlaceTileAtCurrentMousePosition();
                    e.Use();
                    Repaint();
                }
            }

            EditorGUILayout.BeginVertical();
            
            m_SelectedTab = GUILayout.Toolbar(m_SelectedTab, m_Tabs);
            EditorGUILayout.Space();

            switch (m_SelectedTab)
            {
                case 0:
                    DrawMapSettingsTab();
                    break;
                case 1:
                    DrawDrawModeTab();
                    break;
            }

            // Add finalize button at the end
            DrawFinalizeButton();

            EditorGUILayout.EndVertical();

            // Handle map creation using delayCall to avoid recursive OnGUI
            if (m_ShouldCreateMap)
            {
                m_ShouldCreateMap = false;
                EditorApplication.delayCall += () =>
                {
                    CreateNewMap();
                    Repaint();
                };
            }
        }

        private void DrawMapSettingsTab()
        {
            EditorGUILayout.BeginVertical();
            
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Map Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            m_ShowDefaultValues = EditorGUILayout.ToggleLeft("Use Default Map Size", m_ShowDefaultValues);
            EditorGUILayout.Space();

            if (m_ShowDefaultValues)
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.IntField("Width (Default)", (int)Models.Maps.MapConstants.Width);
                    EditorGUILayout.IntField("Height (Default)", (int)Models.Maps.MapConstants.Height);
                }
            }
            else
            {
                m_MapWidth = EditorGUILayout.IntField("Width", m_MapWidth);
                m_MapHeight = EditorGUILayout.IntField("Height", m_MapHeight);

                m_MapWidth = Mathf.Clamp(m_MapWidth, (int)m_MinMapSize.x, (int)m_MaxMapSize.x);
                m_MapHeight = Mathf.Clamp(m_MapHeight, (int)m_MinMapSize.y, (int)m_MaxMapSize.y);

                EditorGUILayout.HelpBox(
                    $"Valid size range: {m_MinMapSize.x}-{m_MaxMapSize.x} (Width) x {m_MinMapSize.y}-{m_MaxMapSize.y} (Height)",
                    MessageType.Info
                );
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Tools", EditorStyles.boldLabel);
            
            using (new EditorGUI.DisabledScope(!IsValidMapSize()))
            {
                if (GUILayout.Button("Create New Map"))
                {
                    m_ShouldCreateMap = true;
                }
            }

            EditorGUILayout.EndVertical();
            
            EditorGUILayout.EndVertical();
        }

        private void DrawDrawModeTab()
        {
            GUILayout.BeginVertical(EditorStyles.helpBox);
            
            // Emergency tile placement button when in tile mode
            if (m_CurrentDrawMode == DrawMode.TilePlacement && m_SelectedTile != null)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                // Make button EVEN MORE noticeable
                GUI.backgroundColor = new Color(1f, 0.5f, 0f); // Bright orange
                GUIStyle bigButtonStyle = new GUIStyle(GUI.skin.button);
                bigButtonStyle.fontSize = 16;
                bigButtonStyle.fontStyle = FontStyle.Bold;
                bigButtonStyle.padding = new RectOffset(15, 15, 15, 15);
                bigButtonStyle.normal.textColor = Color.white;
                
                if (GUILayout.Button("PLACE TILE AT CURRENT MOUSE POSITION (100% WORKS)", bigButtonStyle, GUILayout.Height(60)))
                {
                    // Use direct placement without event system
                    Debug.Log("### DIRECT PLACEMENT FROM BUTTON CLICK ###");
                    
                    if (SceneView.lastActiveSceneView != null)
                    {
                        // Force placement at current mouse position - no delayCall
                        PlaceTileAtCurrentMousePosition();
                    }
                }
                
                GUI.backgroundColor = Color.white;
                
                EditorGUILayout.HelpBox(
                    "KEYBOARD SHORTCUTS:\n" +
                    "T or ENTER - Place tile at current mouse position\n" +
                    "SPACE or P - Place tile at current mouse position\n" +
                    "If clicking doesn't work, use one of these keyboard shortcuts!",
                    MessageType.Info);
                    
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(10);
            }
            
            // Draw Mode Selection - Make this more prominent
            GUILayout.Label("Draw Mode", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            
            // Walkable button (Green)
            GUI.backgroundColor = m_CurrentDrawMode == DrawMode.Walkable ? Color.green : Color.white;
            if (GUILayout.Button(new GUIContent("Walkable", "Mark cells as walkable"), GUILayout.Height(30)))
            {
                m_CurrentDrawMode = m_CurrentDrawMode == DrawMode.Walkable ? DrawMode.None : DrawMode.Walkable;
            }
            
            // Non-Walkable button (Red)
            GUI.backgroundColor = m_CurrentDrawMode == DrawMode.NonWalkable ? Color.red : Color.white;
            if (GUILayout.Button(new GUIContent("Non-Walkable", "Mark cells as non-walkable"), GUILayout.Height(30)))
            {
                m_CurrentDrawMode = m_CurrentDrawMode == DrawMode.NonWalkable ? DrawMode.None : DrawMode.NonWalkable;
            }
            
            // Tile Placement button (Blue)
            GUI.backgroundColor = m_CurrentDrawMode == DrawMode.TilePlacement ? Color.blue : Color.white;
            if (GUILayout.Button(new GUIContent("Place Tiles", "Place tiles on the map"), GUILayout.Height(30)))
            {
                m_CurrentDrawMode = m_CurrentDrawMode == DrawMode.TilePlacement ? DrawMode.None : DrawMode.TilePlacement;
            }
            
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            // Display current mode status
            EditorGUILayout.Space(5);
            GUIStyle statusStyle = new GUIStyle(EditorStyles.helpBox);
            statusStyle.fontSize = 12;
            statusStyle.fontStyle = FontStyle.Bold;
            statusStyle.alignment = TextAnchor.MiddleCenter;
            
            // Set color based on mode
            Color statusColor = Color.white;
            string statusText = "No Mode Selected";
            
            switch (m_CurrentDrawMode)
            {
                case DrawMode.Walkable:
                    statusColor = Color.green;
                    statusText = "MODE: WALKABLE - Click cells to make them walkable";
                    break;
                case DrawMode.NonWalkable:
                    statusColor = Color.red;
                    statusText = "MODE: NON-WALKABLE - Click cells to make them non-walkable";
                    break;
                case DrawMode.TilePlacement:
                    statusColor = Color.cyan;
                    statusText = "MODE: TILE PLACEMENT - Click in scene to place tiles";
                    break;
                default:
                    statusColor = Color.gray;
                    break;
            }
            
            GUI.backgroundColor = statusColor;
            GUILayout.Label(statusText, statusStyle, GUILayout.Height(25));
            GUI.backgroundColor = Color.white;

            // Add clipping toggle when in TilePlacement mode
            if (m_CurrentDrawMode == DrawMode.TilePlacement)
            {
                EditorGUILayout.Space(5);
                
                // Make snap toggle more prominent
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Snap to Grid:", GUILayout.Width(80));
                m_UseClipping = EditorGUILayout.Toggle(m_UseClipping, GUILayout.Width(20));
                GUI.backgroundColor = m_UseClipping ? Color.green : Color.gray;
                GUILayout.Label(m_UseClipping ? "ON" : "OFF", EditorStyles.boldLabel);
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
                
                // Add confirmation toggle
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Show Confirmation:", GUILayout.Width(120));
                m_ShowPlacementConfirmation = EditorGUILayout.Toggle(m_ShowPlacementConfirmation, GUILayout.Width(20));
                GUI.backgroundColor = m_ShowPlacementConfirmation ? Color.yellow : Color.gray;
                GUILayout.Label(m_ShowPlacementConfirmation ? "ON" : "OFF", EditorStyles.boldLabel);
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
                
                // Add tips for tile placement with more visual separation
                EditorGUILayout.Space(10);
                GUI.backgroundColor = new Color(0.9f, 0.9f, 1f);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUILayout.Label("Tile Placement Instructions:", EditorStyles.boldLabel);
                EditorGUILayout.Space(2);
                
                EditorGUILayout.LabelField("1. Select a tile from the list below");
                EditorGUILayout.LabelField("2. LEFT-CLICK directly in the scene view to place a tile");
                EditorGUILayout.LabelField("3. OR press SPACE/P key to place at current mouse position");
                EditorGUILayout.LabelField("4. Use 'Snap to Grid' option to align tiles to grid cells");
                
                EditorGUILayout.EndVertical();
                GUI.backgroundColor = Color.white;
                    
                // Add a direct placement button
                EditorGUILayout.Space(10);
                GUI.backgroundColor = Color.cyan;
                GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
                buttonStyle.fontStyle = FontStyle.Bold;
                
                if (m_SelectedTile != null && GUILayout.Button("Place Selected Tile at Mouse Position", buttonStyle, GUILayout.Height(35)))
                {
                    if (SceneView.lastActiveSceneView != null)
                    {
                        // Get the current mouse position in the scene view
                        Vector2 mousePos = Event.current.mousePosition;
                        // Convert mouse position to world position using raycast
                        Ray ray = HandleUtility.GUIPointToWorldRay(mousePos);
                        RaycastHit2D hit = Physics2D.Raycast(ray.origin, ray.direction);
                        
                        bool placed = false;
                        
                        if (hit.collider != null)
                        {
                            CellComponent cell = hit.collider.GetComponent<CellComponent>();
                            if (cell != null)
                            {
                                Vector3 position = m_UseClipping ? cell.transform.position : hit.point;
                                PlaceTileAtPosition(position, cell);
                                placed = true;
                                SceneView.RepaintAll();
                            }
                        }
                        
                        if (!placed)
                        {
                            // Fallback to nearest cell
                            Vector3 worldPoint = ray.GetPoint(10f);
                            var allCells = FindObjectsByType<CellComponent>(FindObjectsSortMode.None);
                            
                            if (allCells.Length > 0)
                            {
                                CellComponent nearestCell = null;
                                float nearestDistance = float.MaxValue;
                                
                                foreach (var cell in allCells)
                                {
                                    float distance = Vector2.Distance(cell.transform.position, worldPoint);
                                    if (distance < nearestDistance)
                                    {
                                        nearestDistance = distance;
                                        nearestCell = cell;
                                    }
                                }
                                
                                if (nearestCell != null && nearestDistance < 2f)
                                {
                                    PlaceTileAtPosition(nearestCell.transform.position, nearestCell);
                                    SceneView.RepaintAll();
                                }
                                else
                                {
                                    Debug.LogWarning("No cell found near mouse position. Cannot place tile.");
                                }
                            }
                            else
                            {
                                Debug.LogWarning("No cells found in scene. Cannot place tile.");
                            }
                        }
                    }
                    else
                    {
                        Debug.LogWarning("No active scene view found!");
                    }
                }
                GUI.backgroundColor = Color.white;
            }

            // Tile Selection (only show when in TilePlacement mode)
            if (m_CurrentDrawMode == DrawMode.TilePlacement)
            {
            EditorGUILayout.Space();
                GUILayout.Label("Tile Selection", EditorStyles.boldLabel);
                
                // Reload tiles button
                if (GUILayout.Button("Reload Available Tiles"))
                {
                    LoadAvailableTiles();
                }
                
                // Show selected tile info
                if (m_SelectedTile != null)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Selected Tile:", GUILayout.Width(100));
                    EditorGUILayout.LabelField(m_SelectedTile.Id, EditorStyles.boldLabel);
                    EditorGUILayout.EndHorizontal();
                }
                else
                {
                    EditorGUILayout.HelpBox("No tile selected. Please select a tile below.", MessageType.Warning);
                }
                
                // Scroll view for tiles
                m_TileScrollPosition = EditorGUILayout.BeginScrollView(m_TileScrollPosition);
                
                // Grid layout for tiles
                int columnCount = 4;
                for (int i = 0; i < m_AvailableTiles.Count; i += columnCount)
                {
                    EditorGUILayout.BeginHorizontal();
                    for (int j = 0; j < columnCount && i + j < m_AvailableTiles.Count; j++)
                    {
                        TileSpriteData tile = m_AvailableTiles[i + j];
                        bool isSelected = m_SelectedTile == tile;
                        
                        // Load preview sprite
                        Sprite previewSprite = AssetDatabase.LoadAssetAtPath<Sprite>(tile.addressablePath);
                        if (previewSprite != null)
                        {
                            GUI.backgroundColor = isSelected ? Color.blue : Color.white;
                            if (GUILayout.Button(new GUIContent(previewSprite.texture), GUILayout.Width(64), GUILayout.Height(64)))
                            {
                                m_SelectedTile = isSelected ? null : tile;
                                Debug.Log($"Selected tile: {(m_SelectedTile != null ? m_SelectedTile.Id : "None")}");
                            }
                            GUI.backgroundColor = Color.white;
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                }
                
                EditorGUILayout.EndScrollView();
            }

            GUILayout.EndVertical();
        }

        private bool IsValidMapSize()
        {
            if (m_ShowDefaultValues) return true;
            
            return m_MapWidth >= m_MinMapSize.x && m_MapWidth <= m_MaxMapSize.x &&
                   m_MapHeight >= m_MinMapSize.y && m_MapHeight <= m_MaxMapSize.y;
        }

        private void CreateNewMap()
        {
            try
            {
                // Get map dimensions
                int width = m_ShowDefaultValues ? (int)Models.Maps.MapConstants.Width : m_MapWidth;
                int height = m_ShowDefaultValues ? (int)Models.Maps.MapConstants.Height : m_MapHeight;

                // Create map directory if it doesn't exist
                if (!Directory.Exists(MAP_ROOT_PATH))
                {
                    Directory.CreateDirectory(MAP_ROOT_PATH);
                }

                // Get next map ID
                int mapId = GetNextMapId();
                int folderNumber = mapId % 10;
                string folderPath = Path.Combine(MAP_ROOT_PATH, folderNumber.ToString());
                string mapScenePath = Path.Combine(folderPath, $"{mapId}.unity");

                // Create folder if it doesn't exist
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                // Create new scene
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                // Create and setup main camera
                var mainCamera = new GameObject("Main Camera");
                var camera = mainCamera.AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 5f;
                camera.transform.position = new Vector3(6.2f, 2.7f, -10f);
                camera.tag = "MainCamera";
                camera.backgroundColor = new Color(15f/255f, 15f/255f, 15f/255f, 1f);
                camera.nearClipPlane = -1000f;
                camera.farClipPlane = 1000f;
                
                // Add camera controller
                mainCamera.AddComponent<GridCameraController>();

                // Create map object and add components
                var mapObject = new GameObject($"Map {mapId}");
                mapObject.transform.position = new Vector3(9.36051f, 5.147355f, -9.977507f);

                // Create map data
                var mapData = new MapCreator.Data.Models.MapBasicInformation
                {
                    id = mapId,
                    leftNeighbourId = -1,
                    rightNeighbourId = -1,
                    topNeighbourId = -1,
                    bottomNeighbourId = -1
                };

                // Initialize all cells with default values (walkable, visible)
                int totalCells = width * height * 2; // Multiply by 2 as per their implementation
                ushort defaultCellData = CELL_VISIBLE; // Walkable is 0 in the first bit
                for (ushort i = 0; i < totalCells; i++)
                {
                    mapData.cells.dictionary[i] = defaultCellData;
                }

                // Add the map data to the map object
                var mapComponent = mapObject.AddComponent<MapComponent>();
                // We need to convert the MapCreator.Data.Models.MapBasicInformation to Managers.Maps.MapCreator.MapBasicInformation
                var convertedMapData = new Managers.Maps.MapCreator.MapBasicInformation
                {
                    id = mapData.id,
                    leftNeighbourId = mapData.leftNeighbourId,
                    rightNeighbourId = mapData.rightNeighbourId,
                    topNeighbourId = mapData.topNeighbourId,
                    bottomNeighbourId = mapData.bottomNeighbourId
                };
                
                // Initialize the cells dictionary for the converted map data
                foreach (var kvp in mapData.cells.dictionary)
                {
                    convertedMapData.cells.dictionary[kvp.Key] = kvp.Value;
                }
                
                mapComponent.mapInformation = convertedMapData;

                // Add the grid manager and create grid immediately
                var gridManager = mapObject.AddComponent<MapCreatorGridManager>();
                // Set it as the instance since we're not going through Awake
                MapCreatorGridManager.Instance = gridManager;
                // Call CreateGrid directly to generate the grid now
                gridManager.CreateGrid();

                // Save the scene
                EditorSceneManager.SaveScene(scene, mapScenePath);
                Debug.Log($"Created new map at: {mapScenePath} (ID: {mapId}, Folder: {folderNumber})");

                // Focus the scene view on the grid
                if (SceneView.lastActiveSceneView != null)
                {
                    SceneView.lastActiveSceneView.FrameSelected();
                }

                // Select the map object in the hierarchy
                Selection.activeGameObject = mapObject;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error creating map: {e.Message}\n{e.StackTrace}");
            }
        }

        private int GetNextMapId()
        {
            int maxId = MAP_ID_START;

            // Check all folders (0-9)
            for (int i = 0; i < 10; i++)
            {
                string folderPath = Path.Combine(MAP_ROOT_PATH, i.ToString());
                if (!Directory.Exists(folderPath))
                    continue;

                var files = Directory.GetFiles(folderPath, "*.unity");
                foreach (var file in files)
                {
                    string fileName = Path.GetFileNameWithoutExtension(file);
                    if (int.TryParse(fileName, out int id))
                    {
                        maxId = Mathf.Max(maxId, id);
                    }
                }
            }

            return maxId + 1;
        }

        /// <summary>
        /// Collects sprite data from the current scene
        /// </summary>
        private void CollectSpriteData(MapCreator.Data.Models.MapBasicInformation mapData)
        {
            // Clear existing sprite data
            mapData.SpriteData = new MapSpriteData();
            
            // Find all sprite renderers in the scene
            var allSprites = UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
            if (allSprites == null || allSprites.Length == 0)
            {
                Debug.Log("No sprites found in the scene.");
                return;
            }
            
            // Process each sprite
            foreach (var spriteRenderer in allSprites)
            {
                if (spriteRenderer == null || spriteRenderer.sprite == null) continue;
                
                // Skip UI sprites or other special sprites
                if (spriteRenderer.gameObject.layer == LayerMask.NameToLayer("UI")) continue;
                
                // Check if it's a fixture (has rotation or non-uniform scale)
                bool isFixture = 
                    !Mathf.Approximately(spriteRenderer.transform.rotation.eulerAngles.z, 0) ||
                    !Mathf.Approximately(spriteRenderer.transform.localScale.x, spriteRenderer.transform.localScale.y);
                
                // Get sprite name (assuming ID is in the name)
                string spriteName = spriteRenderer.sprite.name;
                string spriteId = spriteName;
                
                // Try to extract numeric ID from name
                if (spriteName.Contains("_"))
                {
                    string[] parts = spriteName.Split('_');
                    if (parts.Length > 1)
                    {
                        spriteId = parts[parts.Length - 1];
                    }
                }
                
                // Create color data
                TileColorData colorData = new TileColorData
                {
                    Red = spriteRenderer.color.r,
                    Green = spriteRenderer.color.g,
                    Blue = spriteRenderer.color.b,
                    Alpha = spriteRenderer.color.a
                };
                
                if (isFixture)
                {
                    // Create fixture data
                    FixtureSpriteData fixtureData = new FixtureSpriteData
                    {
                        Id = spriteId,
                        Position = spriteRenderer.transform.position,
                        Scale = spriteRenderer.transform.localScale,
                        Rotation = spriteRenderer.transform.rotation.eulerAngles.z,
                        Order = spriteRenderer.sortingOrder,
                        Color = colorData
                    };
                    
                    mapData.SpriteData.Fixtures.Add(fixtureData);
                }
                else
                {
                    // Create tile data
                    TileSpriteData tileData = new TileSpriteData
                    {
                        Id = spriteId,
                        Position = spriteRenderer.transform.position,
                        Scale = spriteRenderer.transform.localScale.x, // Use x scale for uniform tiles
                        Order = spriteRenderer.sortingOrder,
                        FlipX = spriteRenderer.flipX,
                        Color = colorData
                    };
                    
                    mapData.SpriteData.Tiles.Add(tileData);
                }
            }
            
            Debug.Log($"Collected {mapData.SpriteData.Tiles.Count} tiles and {mapData.SpriteData.Fixtures.Count} fixtures from the scene.");
        }

        /// <summary>
        /// Saves map data in all formats
        /// </summary>
        private void SaveMapData(MapCreator.Data.Models.MapBasicInformation mapData)
        {
            // Create folder for map if it doesn't exist
            var folderNumber = (int)(mapData.id % 10);
            string folderPath = Path.Combine(MAP_ROOT_PATH, folderNumber.ToString());
            
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }
            
            // Save as JSON
            string jsonPath = Path.Combine(folderPath, $"{mapData.id}.json");
            string jsonData = JsonUtility.ToJson(mapData, true);
            File.WriteAllText(jsonPath, jsonData);
            
            // Save as binary
            SaveMapDataAsBinary(mapData);
            
            // Save as part of consolidated data
            SaveConsolidatedMapData(mapData);
            
            Debug.Log($"Saved map data to: {jsonPath}");
        }

        /// <summary>
        /// Saves map data as binary file
        /// </summary>
        private void SaveMapDataAsBinary(MapCreator.Data.Models.MapBasicInformation mapData)
        {
            var folderNumber = (int)(mapData.id % 10);
            string folderPath = Path.Combine(MAP_ROOT_PATH, folderNumber.ToString());
            string binaryPath = Path.Combine(folderPath, $"{mapData.id}.dat");
            
            try
            {
                using (FileStream fs = new FileStream(binaryPath, FileMode.Create))
                using (BinaryWriter writer = new BinaryWriter(fs))
                {
                    // Write map ID
                    writer.Write(mapData.id);
                    
                    // Write neighbor IDs
                    writer.Write(mapData.topNeighbourId);
                    writer.Write(mapData.bottomNeighbourId);
                    writer.Write(mapData.leftNeighbourId);
                    writer.Write(mapData.rightNeighbourId);
                    
                    // Write cell data
                    writer.Write(mapData.cells.dictionary.Count);
                    foreach (var pair in mapData.cells.dictionary)
                    {
                        writer.Write(pair.Key);
                        writer.Write(pair.Value);
                    }
                    
                    // Write tile sprite data
                    writer.Write(mapData.SpriteData.Tiles.Count);
                    foreach (var tile in mapData.SpriteData.Tiles)
                    {
                        writer.Write(tile.Id ?? string.Empty);
                        writer.Write(tile.Position.x);
                        writer.Write(tile.Position.y);
                        writer.Write(tile.Scale);
                        writer.Write(tile.Order);
                        writer.Write(tile.FlipX);
                        writer.Write(tile.Color.Red);
                        writer.Write(tile.Color.Green);
                        writer.Write(tile.Color.Blue);
                        writer.Write(tile.Color.Alpha);
                    }
                    
                    // Write fixture sprite data
                    writer.Write(mapData.SpriteData.Fixtures.Count);
                    foreach (var fixture in mapData.SpriteData.Fixtures)
                    {
                        writer.Write(fixture.Id ?? string.Empty);
                        writer.Write(fixture.Position.x);
                        writer.Write(fixture.Position.y);
                        writer.Write(fixture.Scale.x);
                        writer.Write(fixture.Scale.y);
                        writer.Write(fixture.Rotation);
                        writer.Write(fixture.Order);
                        writer.Write(fixture.Color.Red);
                        writer.Write(fixture.Color.Green);
                        writer.Write(fixture.Color.Blue);
                        writer.Write(fixture.Color.Alpha);
                    }
                }
                
                Debug.Log($"Saved binary map data to: {binaryPath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error saving binary map data: {ex.Message}");
            }
        }

        /// <summary>
        /// Saves map data as part of consolidated data
        /// </summary>
        private void SaveConsolidatedMapData(MapCreator.Data.Models.MapBasicInformation mapData)
        {
            try
            {
                // Ensure data folder exists
                if (!Directory.Exists(DATA_FOLDER_PATH))
                {
                    Directory.CreateDirectory(DATA_FOLDER_PATH);
                }
                
                string mapDataPath = Path.Combine(DATA_FOLDER_PATH, MAP_DATA_FILENAME);
                
                // Load existing container or create new one
                MapDataContainer container = LoadOrCreateMapDataContainer();
                
                // Update the map in the container
                container.UpdateMap(mapData);
                
                // Create binary data object
                BinaryDataObject dataObject = ScriptableObject.CreateInstance<BinaryDataObject>();
                
                // Serialize the container to binary
                using (MemoryStream stream = new MemoryStream())
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    container.Serialize(writer);
                    dataObject.data = stream.ToArray();
                }
                
                // Save as asset
                AssetDatabase.CreateAsset(dataObject, mapDataPath);
                AssetDatabase.SaveAssets();
                
                // Register as addressable
                AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
                if (settings != null)
                {
                    string assetGuid = AssetDatabase.AssetPathToGUID(mapDataPath);
                    
                    // Get or create default group
                    AddressableAssetGroup defaultGroup = settings.DefaultGroup;
                    if (defaultGroup == null)
                    {
                        Debug.LogWarning("Default addressable group not found. Creating one.");
                        defaultGroup = settings.CreateGroup("Default", false, false, false, null);
                    }
                    
                    // Add or update entry
                    string addressPath = "Content/Data/MapData";
                    var entry = settings.FindAssetEntry(assetGuid);
                    
                    if (entry == null)
                    {
                        entry = settings.CreateOrMoveEntry(assetGuid, defaultGroup);
                        entry.address = addressPath;
                    }
                    else if (entry.address != addressPath)
                    {
                        entry.address = addressPath;
                    }
                    
                    settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, entry, true);
                }
                else
                {
                    Debug.LogWarning("Addressable Settings not found. Make sure the Addressable Asset System package is installed.");
                }
                
                Debug.Log($"Saved consolidated map data to: {mapDataPath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error saving consolidated map data: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads or creates a map data container
        /// </summary>
        private MapDataContainer LoadOrCreateMapDataContainer()
        {
            string mapDataPath = Path.Combine(DATA_FOLDER_PATH, MAP_DATA_FILENAME);
            
            // Check if the asset exists
            if (File.Exists(mapDataPath))
            {
                try
                {
                    // Load the asset
                    BinaryDataObject dataObject = AssetDatabase.LoadAssetAtPath<BinaryDataObject>(mapDataPath);
                    if (dataObject != null && dataObject.data != null && dataObject.data.Length > 0)
                    {
                        // Deserialize the container
                        using (MemoryStream stream = new MemoryStream(dataObject.data))
                        using (BinaryReader reader = new BinaryReader(stream))
                        {
                            return MapDataContainer.Deserialize(reader);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error loading map data container: {ex.Message}");
                }
            }
            
            // Create a new container if loading failed or file doesn't exist
            return new MapDataContainer();
        }

        /// <summary>
        /// Loads map data from disk
        /// </summary>
        public MapCreator.Data.Models.MapBasicInformation LoadMapData(long mapId)
        {
            try
            {
                // Try to load from JSON first
                var folderNumber = (int)(mapId % 10);
                string folderPath = Path.Combine(MAP_ROOT_PATH, folderNumber.ToString());
                string jsonPath = Path.Combine(folderPath, $"{mapId}.json");
                
                if (File.Exists(jsonPath))
                {
                    string jsonData = File.ReadAllText(jsonPath);
                    return JsonUtility.FromJson<MapCreator.Data.Models.MapBasicInformation>(jsonData);
                }
                
                // If JSON not found, try loading from consolidated data
                string mapDataPath = Path.Combine(DATA_FOLDER_PATH, MAP_DATA_FILENAME);
                
                if (File.Exists(mapDataPath))
                {
                    BinaryDataObject dataObject = AssetDatabase.LoadAssetAtPath<BinaryDataObject>(mapDataPath);
                    if (dataObject != null && dataObject.data != null && dataObject.data.Length > 0)
                    {
                        // Deserialize the container
                        MapDataContainer container;
                        using (MemoryStream stream = new MemoryStream(dataObject.data))
                        using (BinaryReader reader = new BinaryReader(stream))
                        {
                            container = MapDataContainer.Deserialize(reader);
                        }
                        
                        // Get the map data
                        return container.ToMapBasicInformation(mapId);
                    }
                }
                
                Debug.LogWarning($"Map data not found for map {mapId}");
                return null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error loading map data: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Creates sprites from map data
        /// </summary>
        public void CreateSpritesFromData(MapCreator.Data.Models.MapBasicInformation mapInfo)
        {
            if (mapInfo == null || mapInfo.SpriteData == null)
            {
                Debug.LogWarning("No sprite data found in map info");
                return;
            }
            
            // Clean up existing sprites
            var existingSprites = UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
            foreach (var sprite in existingSprites)
            {
                if (sprite.gameObject.layer != LayerMask.NameToLayer("UI"))
                {
                    DestroyImmediate(sprite.gameObject);
                }
            }
            
            // Create tiles
            foreach (var tileData in mapInfo.SpriteData.Tiles)
            {
                CreateSpriteFromTileData(tileData);
            }
            
            // Create fixtures
            foreach (var fixtureData in mapInfo.SpriteData.Fixtures)
            {
                CreateSpriteFromFixtureData(fixtureData);
            }
            
            Debug.Log($"Created {mapInfo.SpriteData.Tiles.Count} tiles and {mapInfo.SpriteData.Fixtures.Count} fixtures");
        }

        /// <summary>
        /// Creates a sprite from tile data
        /// </summary>
        private void CreateSpriteFromTileData(TileSpriteData tileData)
        {
            try
            {
                // Load sprite
                Sprite sprite = LoadSpriteById(tileData.Id);
                if (sprite == null)
                {
                    Debug.LogWarning($"Failed to load sprite with ID {tileData.Id}");
                    return;
                }
                
                // Create GameObject
                GameObject spriteObj = new GameObject($"Tile_{tileData.Id}");
                spriteObj.transform.position = tileData.Position;
                spriteObj.transform.localScale = new Vector3(tileData.Scale, tileData.Scale, 1f);
                
                // Add SpriteRenderer
                SpriteRenderer spriteRenderer = spriteObj.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = sprite;
                spriteRenderer.sortingOrder = tileData.Order;
                spriteRenderer.flipX = tileData.FlipX;
                spriteRenderer.color = tileData.Color.ToColor();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error creating tile sprite: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates a sprite from fixture data
        /// </summary>
        private void CreateSpriteFromFixtureData(FixtureSpriteData fixtureData)
        {
            try
            {
                // Load sprite
                Sprite sprite = LoadSpriteById(fixtureData.Id);
                if (sprite == null)
                {
                    Debug.LogWarning($"Failed to load sprite with ID {fixtureData.Id}");
                    return;
                }
                
                // Create GameObject
                GameObject spriteObj = new GameObject($"Fixture_{fixtureData.Id}");
                spriteObj.transform.position = fixtureData.Position;
                spriteObj.transform.localScale = fixtureData.Scale;
                spriteObj.transform.rotation = Quaternion.Euler(0, 0, fixtureData.Rotation);
                
                // Add SpriteRenderer
                SpriteRenderer spriteRenderer = spriteObj.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = sprite;
                spriteRenderer.sortingOrder = fixtureData.Order;
                spriteRenderer.color = fixtureData.Color.ToColor();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error creating fixture sprite: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads a sprite by its ID
        /// </summary>
        private Sprite LoadSpriteById(string id)
        {
            // Try to find in project
            string[] guids = AssetDatabase.FindAssets($"t:Sprite {id}");
            
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            
            // Not found, try addressables
            // Note: This won't work in the editor directly, as addressables are runtime-only
            // For a complete solution, you would need to implement a runtime sprite loader
            
            // Try to find in our sprite folders
            string hexValue = id.Length >= 2 ? id.Substring(0, 2) : id.PadLeft(2, '0');
            string spritePath = $"Assets/CreatorMap/Content/Sprites/{hexValue}/{id}.png";
            
            if (File.Exists(spritePath))
            {
                return AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            }
            
            // Try main project's sprite folder
            spritePath = $"Assets/ploup/Content/Sprites/{hexValue}/{id}.png";
            
            if (File.Exists(spritePath))
            {
                return AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            }
            
            // Create a placeholder sprite if not found
            Texture2D texture = new Texture2D(32, 32);
            Color[] colors = new Color[32 * 32];
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = new Color(1, 0, 1, 1); // Magenta for missing sprites
            }
            texture.SetPixels(colors);
            texture.Apply();
            
            return Sprite.Create(texture, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f));
        }

        /// <summary>
        /// Static helper to check if a cell is walkable based on its data
        /// </summary>
        public static bool IsCellWalkable(ushort cellData)
        {
            // If CELL_WALKABLE bit is set, cell is NOT walkable
            return (cellData & CELL_WALKABLE) == 0;
        }

        /// <summary>
        /// Saves the current map state
        /// </summary>
        private void SaveCurrentMapState()
        {
            try
            {
                // Find map component
                var mapComponent = UnityEngine.Object.FindFirstObjectByType<MapComponent>();
                if (mapComponent == null || mapComponent.mapInformation == null)
                {
                    Debug.LogError("No map component or map information found!");
                    return;
                }
                
                // Get all cell components
                var allCells = UnityEngine.Object.FindObjectsByType<CellComponent>(FindObjectsSortMode.None);
                if (allCells == null || allCells.Length == 0)
                {
                    Debug.LogWarning("No cells found in the scene!");
                    return;
                }
                
                // Get map data from component
                var runtimeMapData = mapComponent.mapInformation;
                
                Debug.Log($"Starting SaveCurrentMapState for map {runtimeMapData.id}");
                
                // Convert to our data model format
                var mapData = new MapCreator.Data.Models.MapBasicInformation
                {
                    id = runtimeMapData.id,
                    leftNeighbourId = runtimeMapData.leftNeighbourId,
                    rightNeighbourId = runtimeMapData.rightNeighbourId,
                    topNeighbourId = runtimeMapData.topNeighbourId,
                    bottomNeighbourId = runtimeMapData.bottomNeighbourId,
                    SpriteData = new MapSpriteData()
                };
                
                // Initialize cell dictionary (start with empty dictionary)
                mapData.cells.dictionary = new Dictionary<ushort, ushort>();
                
                // Process cells from the scene
                int cellsProcessed = 0;
                int nonWalkableCells = 0;
                
                foreach (var cell in allCells)
                {
                    if (cell != null && cell.Cell != null)
                    {
                        // Get cell ID
                        ushort cellId = (ushort)cell.CellId;
                        
                        // Start with default (visible) - bit 7 (0x0040)
                        ushort cellData = CELL_VISIBLE;
                        
                        // Set walkability - bit 1 (0x0001)
                        // IMPORTANT: CELL_WALKABLE bit is set (1) for NON-walkable cells
                        if (!cell.Cell.IsWalkable)
                        {
                            cellData |= CELL_WALKABLE; // Set non-walkable bit
                            nonWalkableCells++;
                            Debug.Log($"Saving cell {cellId} as NON-walkable, data: {cellData}");
                        }
                        
                        // Update cell data in our map data object
                        mapData.cells.dictionary[cellId] = cellData;
                        cellsProcessed++;
                    }
                }
                
                Debug.Log($"Processed {cellsProcessed} cells, including {nonWalkableCells} non-walkable cells");
                
                // Verify some cell data was saved
                if (mapData.cells.dictionary.Count == 0)
                {
                    Debug.LogError("No cell data was saved! Map will be empty.");
                }
                
                // Collect sprite data
                CollectSpriteData(mapData);
                
                // Save map data
                SaveMapData(mapData);
                
                // Debug check: verify saved data
                foreach (var kvp in mapData.cells.dictionary)
                {
                    bool isWalkable = (kvp.Value & CELL_WALKABLE) == 0;
                    if (!isWalkable)
                    {
                        Debug.Log($"Verification: Cell {kvp.Key} saved with data {kvp.Value}, which is NON-walkable");
                    }
                }
                
                Debug.Log($"Saved map {mapData.id} with {cellsProcessed} cells ({nonWalkableCells} non-walkable) and {mapData.SpriteData.Tiles.Count + mapData.SpriteData.Fixtures.Count} sprites.");
                
                // Update the runtime data in the MapComponent to match what we just saved
                foreach (var kvp in mapData.cells.dictionary)
                {
                    runtimeMapData.cells.dictionary[kvp.Key] = kvp.Value;
                }
                
                // Show success message
                EditorUtility.DisplayDialog("Map Saved", 
                    $"Successfully saved map {mapData.id} with walkability data for {cellsProcessed} cells and {mapData.SpriteData.Tiles.Count + mapData.SpriteData.Fixtures.Count} sprites.", 
                    "OK");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error saving map state: {ex.Message}\n{ex.StackTrace}");
                EditorUtility.DisplayDialog("Error", $"Failed to save map: {ex.Message}", "OK");
            }
        }

        /// <summary>
        /// Save sprite data from scene objects
        /// </summary>
        private void SaveSpriteDataFromScene(MapCreator.Data.Models.MapBasicInformation mapData)
        {
            // Clear existing sprite data
            mapData.SpriteData = new MapSpriteData();
            
            // Find all sprite renderers in the scene
            var allSprites = UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
            if (allSprites == null || allSprites.Length == 0)
            {
                Debug.Log("No sprites found in the scene.");
                return;
            }
            
            // Process each sprite
            foreach (var spriteRenderer in allSprites)
            {
                if (spriteRenderer == null || spriteRenderer.sprite == null) continue;
                
                // Skip UI sprites or other special sprites
                if (spriteRenderer.gameObject.layer == LayerMask.NameToLayer("UI")) continue;
                
                // Check if it's a fixture (has rotation or non-uniform scale)
                bool isFixture = 
                    !Mathf.Approximately(spriteRenderer.transform.rotation.eulerAngles.z, 0) ||
                    !Mathf.Approximately(spriteRenderer.transform.localScale.x, spriteRenderer.transform.localScale.y);
                
                // Get sprite name (assuming ID is in the name)
                string spriteName = spriteRenderer.sprite.name;
                string spriteId = spriteName;
                
                // Try to extract numeric ID from name
                if (spriteName.Contains("_"))
                {
                    string[] parts = spriteName.Split('_');
                    if (parts.Length > 1)
                    {
                        spriteId = parts[parts.Length - 1];
                    }
                }
                
                // Create color data
                TileColorData colorData = new TileColorData
                {
                    Red = spriteRenderer.color.r,
                    Green = spriteRenderer.color.g,
                    Blue = spriteRenderer.color.b,
                    Alpha = spriteRenderer.color.a
                };
                
                if (isFixture)
                {
                    // Create fixture data
                    FixtureSpriteData fixtureData = new FixtureSpriteData
                    {
                        Id = spriteId,
                        Position = spriteRenderer.transform.position,
                        Scale = new Vector2(
                            spriteRenderer.transform.localScale.x,
                            spriteRenderer.transform.localScale.y
                        ),
                        Rotation = spriteRenderer.transform.rotation.eulerAngles.z,
                        Order = spriteRenderer.sortingOrder,
                        Color = colorData
                    };
                    
                    mapData.SpriteData.Fixtures.Add(fixtureData);
                }
                else
                {
                    // Create tile data
                    TileSpriteData tileData = new TileSpriteData
                    {
                        Id = spriteId,
                        Position = spriteRenderer.transform.position,
                        Scale = spriteRenderer.transform.localScale.x,
                        Order = spriteRenderer.sortingOrder,
                        FlipX = spriteRenderer.flipX,
                        Color = colorData
                    };
                    
                    mapData.SpriteData.Tiles.Add(tileData);
                }
            }
            
            Debug.Log($"Saved sprite data: {mapData.SpriteData.Tiles.Count} tiles, {mapData.SpriteData.Fixtures.Count} fixtures");
        }

        /// <summary>
        /// Convert editor MapBasicInformation to runtime MapBasicInformation
        /// </summary>
        private Managers.Maps.MapCreator.MapBasicInformation ConvertToRuntimeMapData(MapCreator.Data.Models.MapBasicInformation editorMapData)
        {
            var runtimeMapData = new Managers.Maps.MapCreator.MapBasicInformation
            {
                id = editorMapData.id,
                leftNeighbourId = editorMapData.leftNeighbourId,
                rightNeighbourId = editorMapData.rightNeighbourId,
                topNeighbourId = editorMapData.topNeighbourId,
                bottomNeighbourId = editorMapData.bottomNeighbourId
            };
            
            // Copy cell data
            foreach (var pair in editorMapData.cells.dictionary)
            {
                runtimeMapData.cells.dictionary[pair.Key] = pair.Value;
            }
            
            return runtimeMapData;
        }

        /// <summary>
        /// Finalize map by replacing cells with sprite objects
        /// </summary>
        private void FinalizeMap()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Cannot Finalize", "Map finalization is not available during Play Mode. Please exit Play Mode first.", "OK");
                return;
            }

            try
            {
                // Get current map data
                var mapComponent = UnityEngine.Object.FindFirstObjectByType<MapComponent>();
                if (mapComponent == null || mapComponent.mapInformation == null)
                {
                    Debug.LogError("No map component or map information found!");
                    return;
                }
                
                // Convert to our data model format
                var mapData = new MapCreator.Data.Models.MapBasicInformation
                {
                    id = mapComponent.mapInformation.id,
                    leftNeighbourId = mapComponent.mapInformation.leftNeighbourId,
                    rightNeighbourId = mapComponent.mapInformation.rightNeighbourId,
                    topNeighbourId = mapComponent.mapInformation.topNeighbourId,
                    bottomNeighbourId = mapComponent.mapInformation.bottomNeighbourId
                };
                
                // Copy cell data
                foreach (var pair in mapComponent.mapInformation.cells.dictionary)
                {
                    mapData.cells.dictionary[pair.Key] = pair.Value;
                }
                
                // Load sprite data from existing sprites or generate from serialized data
                if (mapData.SpriteData == null || mapData.SpriteData.Tiles.Count == 0)
                {
                    // If no sprite data exists, collect from the scene
                    SaveSpriteDataFromScene(mapData);
                }
                
                // Remove cell objects
                var cells = UnityEngine.Object.FindObjectsByType<Components.Maps.CellComponent>(FindObjectsSortMode.None);
                foreach (var cell in cells)
                {
                    DestroyImmediate(cell.gameObject);
                }
                
                // Remove any existing sprites
                var existingSprites = UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
                foreach (var sprite in existingSprites)
                {
                    if (sprite.gameObject.layer != LayerMask.NameToLayer("UI"))
                    {
                        DestroyImmediate(sprite.gameObject);
                    }
                }
                
                // Create map object if it doesn't exist
                var mapObject = GameObject.Find($"Map {mapData.id}");
                if (mapObject == null)
                {
                    mapObject = new GameObject($"Map {mapData.id}");
                    // Add MapComponent
                    mapComponent = mapObject.AddComponent<MapComponent>();
                    mapComponent.mapInformation = ConvertToRuntimeMapData(mapData);
                }
                
                // Create tile objects with proper structure for final map
                foreach (var tileData in mapData.SpriteData.Tiles)
                {
                    CreateFinalTileObject(mapObject.transform, tileData, false);
                }
                
                // Create fixture objects
                foreach (var fixtureData in mapData.SpriteData.Fixtures)
                {
                    CreateFinalFixtureObject(mapObject.transform, fixtureData);
                }
                
                // Save the scene
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                Debug.Log($"Map finalized! Created {mapData.SpriteData.Tiles.Count} tiles and {mapData.SpriteData.Fixtures.Count} fixtures");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error finalizing map: {ex.Message}\n{ex.StackTrace}");
                EditorUtility.DisplayDialog("Error", $"Failed to finalize map: {ex.Message}", "OK");
            }
        }
        
        /// <summary>
        /// Create a finalized tile object with the TileSprite component structure
        /// </summary>
        private GameObject CreateFinalTileObject(Transform parent, TileSpriteData tileData, bool isFixture)
        {
            // Format proper key and path
            string idPrefix = tileData.Id.Length >= 2 ? tileData.Id.Substring(0, 2) : "00";
            string key = $"Assets/Tiles/{idPrefix}/{tileData.Id}.png";
            
            // Create game object with proper naming convention
            string objectName = isFixture ? $"Fixture {key}" : $"Tile {key}";
            GameObject spriteObj = new GameObject(objectName);
            spriteObj.transform.SetParent(parent);
            spriteObj.transform.position = tileData.Position;
            
            // Default values for scale and rotation
            Vector2 scale = Vector2.one;
            float rotation = 0f;
            
            if (isFixture)
            {
                // Handle as fixture (likely from a FixtureSpriteData)
                var mapObj = parent.gameObject;
                var mapId = 0;
                if (mapObj.name.StartsWith("Map "))
                {
                    int.TryParse(mapObj.name.Substring(4), out mapId);
                }
                
                if (mapId > 0)
                {
                    var editorMapData = LoadMapData(mapId);
                    if (editorMapData != null && editorMapData.SpriteData != null)
                    {
                        foreach (var fixture in editorMapData.SpriteData.Fixtures)
                        {
                            if (fixture.Id == tileData.Id)
                            {
                                scale = fixture.Scale;
                                rotation = fixture.Rotation;
                                break;
                            }
                        }
                    }
                }
            }
            
            // Set scale and rotation
            spriteObj.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            spriteObj.transform.rotation = Quaternion.Euler(0, 0, rotation);
            
            // Add TileSprite component with the exact structure from the main project
            var tileSprite = spriteObj.AddComponent<Models.Maps.TileSprite>();
            tileSprite.id = tileData.Id;
            tileSprite.key = key;
            tileSprite.type = (byte)(isFixture ? 1 : 0);
            
            // Set color data
            if (isFixture)
            {
                // Fixtures use direct color values
                tileSprite.colorMultiplicatorIsOne = tileData.Color.IsOne();
                tileSprite.colorMultiplicatorR = tileData.Color.Red;
                tileSprite.colorMultiplicatorG = tileData.Color.Green;
                tileSprite.colorMultiplicatorB = tileData.Color.Blue;
                tileSprite.colorMultiplicatorA = tileData.Color.Alpha;
            }
            else
            {
                // Regular tiles use values multiplied by 255
                tileSprite.colorMultiplicatorIsOne = tileData.Color.IsOne();
                tileSprite.colorMultiplicatorR = tileData.Color.Red * 255f;
                tileSprite.colorMultiplicatorG = tileData.Color.Green * 255f;
                tileSprite.colorMultiplicatorB = tileData.Color.Blue * 255f;
                tileSprite.colorMultiplicatorA = 1f;
            }
            
            // Add SpriteRenderer (empty, will be populated by MapComponent at runtime)
            var spriteRenderer = spriteObj.AddComponent<SpriteRenderer>();
            spriteRenderer.sortingOrder = tileData.Order;
            if (!isFixture) spriteRenderer.flipX = tileData.FlipX;
            
            // Add ColorMatrixShader material
            Shader colorMatrixShader = Shader.Find("Custom/ColorMatrixShader");
            if (colorMatrixShader != null)
            {
                spriteRenderer.material = new Material(colorMatrixShader);
            }
            
            return spriteObj;
        }
        
        /// <summary>
        /// Create a finalized fixture object with the TileSprite component structure
        /// </summary>
        private GameObject CreateFinalFixtureObject(Transform parent, FixtureSpriteData fixtureData)
        {
            // Convert to TileSpriteData for compatibility
            TileSpriteData tileData = new TileSpriteData
            {
                Id = fixtureData.Id,
                Position = fixtureData.Position,
                Scale = 1f, // Will be overridden
                Order = fixtureData.Order,
                Color = new TileColorData
                {
                    Red = fixtureData.Color.Red,
                    Green = fixtureData.Color.Green,
                    Blue = fixtureData.Color.Blue,
                    Alpha = fixtureData.Color.Alpha
                }
            };
            
            return CreateFinalTileObject(parent, tileData, true);
        }
        
        /// <summary>
        /// Add "Finalize Map" button to the menu
        /// </summary>
        private void DrawFinalizeButton()
        {
            if (EditorApplication.isPlaying)
                return;

            EditorGUILayout.Space(10);
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            GUI.backgroundColor = new Color(0.2f, 0.6f, 0.9f);
            if (GUILayout.Button("FINALIZE MAP", GUILayout.Height(40), GUILayout.Width(200)))
            {
                if (EditorUtility.DisplayDialog("Finalize Map", 
                    "This will replace grid cells with game-ready sprite objects.\nContinue?", 
                    "Yes", "Cancel"))
                {
                    FinalizeMap();
                }
            }
            GUI.backgroundColor = Color.white;
            
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        [MenuItem("Window/Map Creator/Open Map...", false, 102)]
        public static void OpenMapMenu()
        {
            // Open a file dialog to select a map scene file
            string mapPath = EditorUtility.OpenFilePanel("Open Map", MAP_ROOT_PATH, "unity");
            if (string.IsNullOrEmpty(mapPath))
            {
                return;
            }

            // Convert to a path relative to the project
            if (mapPath.StartsWith(Application.dataPath))
            {
                mapPath = "Assets" + mapPath.Substring(Application.dataPath.Length);
            }

            // Extract map ID from the file name
            string fileName = Path.GetFileNameWithoutExtension(mapPath);
            if (!long.TryParse(fileName, out long mapId))
            {
                Debug.LogError($"Invalid map file name: {fileName}. Expected a numeric ID.");
                return;
            }

            OpenMap(mapId);
        }

        public static void OpenMap(long mapId)
        {
            try
            {
                // Build the scene path
                int folderNumber = (int)(mapId % 10);
                string mapScenePath = $"{MAP_ROOT_PATH}/{folderNumber}/{mapId}.unity";

                // Check if the scene exists
                if (!File.Exists(mapScenePath))
                {
                    Debug.LogError($"Map scene not found: {mapScenePath}");
                    return;
                }

                // Load the map data from saved files
                var editor = new MapCreatorWindow();
                var mapData = editor.LoadMapData(mapId);
                
                if (mapData == null)
                {
                    Debug.LogWarning($"No saved data found for map {mapId}. Opening the scene without restored data.");
                    EditorSceneManager.OpenScene(mapScenePath);
                    return;
                }
                
                Debug.Log($"Opening map {mapId}, found saved data with {mapData.cells.dictionary.Count} cells");
                
                // Debug walkability data before opening
                int nonWalkableCells = 0;
                foreach (var kvp in mapData.cells.dictionary)
                {
                    bool isWalkable = IsCellWalkable(kvp.Value);
                    if (!isWalkable)
                    {
                        nonWalkableCells++;
                        Debug.Log($"Cell {kvp.Key}: data={kvp.Value}, isWalkable={isWalkable}");
                    }
                }
                Debug.Log($"Map has {nonWalkableCells} non-walkable cells out of {mapData.cells.dictionary.Count} total cells");

                // Open the scene
                EditorSceneManager.OpenScene(mapScenePath);
                
                // Find the map in the scene
                var mapComponent = UnityEngine.Object.FindFirstObjectByType<MapComponent>();
                if (mapComponent == null)
                {
                    Debug.LogError("Map component not found in the opened scene!");
                    return;
                }
                
                // Convert data models
                var convertedMapData = new Managers.Maps.MapCreator.MapBasicInformation
                {
                    id = mapData.id,
                    leftNeighbourId = mapData.leftNeighbourId,
                    rightNeighbourId = mapData.rightNeighbourId,
                    topNeighbourId = mapData.topNeighbourId,
                    bottomNeighbourId = mapData.bottomNeighbourId
                };
                
                // Create a fresh dictionary
                convertedMapData.cells.dictionary = new Dictionary<ushort, ushort>();
                
                // Copy all cell data, ensuring walkability is preserved
                foreach (var kvp in mapData.cells.dictionary)
                {
                    convertedMapData.cells.dictionary[kvp.Key] = kvp.Value;
                    // Additional verification
                    bool isWalkable = IsCellWalkable(kvp.Value);
                    if (!isWalkable)
                    {
                        Debug.Log($"Set cell {kvp.Key} in map component as NON-walkable with data {kvp.Value}");
                    }
                }
                
                // Update the map component
                mapComponent.mapInformation = convertedMapData;
                
                // Force grid recreation to apply walkability
                var gridManager = UnityEngine.Object.FindFirstObjectByType<MapCreatorGridManager>();
                if (gridManager != null)
                {
                    Debug.Log("Forcing grid recreation to apply walkability settings...");
                    gridManager.CreateGrid();
                }
                else
                {
                    Debug.LogError("Grid manager not found in the scene!");
                }
                
                // Create sprites from saved data
                if (mapData.SpriteData != null)
                {
                    editor.CreateSpritesFromData(mapData);
                }
                
                // Verify cells after loading
                var loadedCells = UnityEngine.Object.FindObjectsByType<CellComponent>(FindObjectsSortMode.None);
                int loadedNonWalkable = 0;
                
                foreach (var cell in loadedCells)
                {
                    if (cell != null && cell.Cell != null && !cell.Cell.IsWalkable)
                    {
                        loadedNonWalkable++;
                        
                        // Verify the LineRenderer configuration for non-walkable cells
                        var lineRenderer = cell.GetComponent<LineRenderer>();
                        if (lineRenderer != null)
                        {
                            Debug.Log($"Non-walkable cell {cell.CellId} has LineRenderer with {lineRenderer.positionCount} points");
                            
                            // If it doesn't have 2 points, force it to have correct non-walkable visualization
                            if (lineRenderer.positionCount != 2)
                            {
                                Debug.LogWarning($"Fixing LineRenderer for non-walkable cell {cell.CellId}");
                                lineRenderer.positionCount = 2;
                                lineRenderer.SetPosition(0, new Vector3(0, 0, 0));
                                lineRenderer.SetPosition(1, new Vector3(0, 0, 1));
                            }
                            
                            // Ensure no collider for non-walkable cells
                            var collider = cell.GetComponent<PolygonCollider2D>();
                            if (collider != null)
                            {
                                Debug.LogWarning($"Removing collider from non-walkable cell {cell.CellId}");
                                UnityEngine.Object.DestroyImmediate(collider);
                            }
                        }
                    }
                }
                
                Debug.Log($"Successfully opened map {mapId} with {loadedCells.Length} cells, " +
                          $"{loadedNonWalkable} are non-walkable (expected {nonWalkableCells})");
                
                if (loadedNonWalkable != nonWalkableCells)
                {
                    Debug.LogWarning($"Non-walkable cell count mismatch! Expected {nonWalkableCells} but got {loadedNonWalkable}");
                }
                
                // Force scene repaint to ensure all visual changes are applied
                SceneView.RepaintAll();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error opening map: {ex.Message}\n{ex.StackTrace}");
                EditorUtility.DisplayDialog("Error", $"Failed to open map: {ex.Message}", "OK");
            }
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            // Skip if in play mode
            if (EditorApplication.isPlaying) return;

            Event e = Event.current;
            if (e == null) return;

            // Debug any mouse down events
            if (e.type == EventType.MouseDown)
            {
                Debug.Log($"SCENE VIEW MouseDown: button={e.button}, pos={e.mousePosition}, keyCode={e.keyCode}");
            }

            // Only continue for tile placement mode with a selected tile
            if (m_CurrentDrawMode == DrawMode.TilePlacement && m_SelectedTile != null)
            {
                // Draw preview during repaint
                if (e.type == EventType.Repaint)
                {
                    DrawTilePreviewAtMousePosition(sceneView);
                }
                
                // Handle mouse clicks directly
                if (e.type == EventType.MouseDown && e.button == 0)
                {
                    Debug.Log("LEFT CLICK DETECTED - Direct Processing");
                    
                    // Get ray at mouse position
                    Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                    bool placedTile = false;
                    
                    // First, try direct raycast
                    RaycastHit2D hit = Physics2D.Raycast(ray.origin, ray.direction);
                    if (hit.collider != null)
                    {
                        CellComponent cell = hit.collider.GetComponent<CellComponent>();
                        if (cell != null)
                        {
                            // Found a cell, place the tile
                            Vector3 position = m_UseClipping ? cell.transform.position : hit.point;
                            
                            Debug.Log($"DIRECT HIT: Placing tile at cell {cell.CellId}, position {position}");
                            
                            PlaceTileDirectly(position, cell);
                            placedTile = true;
                            
                            // Use the event
                            e.Use();
                        }
                    }
                    
                    // If no direct hit, try nearest cell
                    if (!placedTile)
                    {
                        Debug.Log("No direct hit, trying nearest cell");
                        Vector3 worldPoint = ray.GetPoint(10f);
                        var allCells = FindObjectsByType<CellComponent>(FindObjectsSortMode.None);
                        
                        if (allCells.Length > 0)
                        {
                            // Find nearest cell
                            CellComponent nearestCell = null;
                            float nearestDistance = float.MaxValue;
                            
                            foreach (var cell in allCells)
                            {
                                float distance = Vector2.Distance(cell.transform.position, worldPoint);
                                if (distance < nearestDistance)
                                {
                                    nearestDistance = distance;
                                    nearestCell = cell;
                                }
                            }
                            
                            if (nearestCell != null && nearestDistance < 2f)
                            {
                                Debug.Log($"NEAREST CELL: Placing tile at cell {nearestCell.CellId}, distance {nearestDistance}");
                                
                                PlaceTileDirectly(nearestCell.transform.position, nearestCell);
                                
                                // Use the event
                                e.Use();
                            }
                            else
                            {
                                Debug.Log($"Nearest cell too far: {nearestDistance} units");
                            }
                        }
                        else
                        {
                            Debug.Log("No cells found in scene");
                        }
                    }
                }
                
                // Add keyboard shortcut for tile placement
                if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Space || e.keyCode == KeyCode.P))
                {
                    PlaceTileAtCurrentMousePosition();
                    e.Use();
                    return;
                }
            }
            // Handle walkability mode
            else if (m_CurrentDrawMode == DrawMode.Walkable || m_CurrentDrawMode == DrawMode.NonWalkable)
            {
                if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0)
                {
                    Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                    RaycastHit2D hit = Physics2D.Raycast(ray.origin, ray.direction);

                    if (hit.collider != null)
                    {
                        var cell = hit.collider.GetComponent<CellComponent>();
                        if (cell != null)
                        {
                            bool isWalkable = m_CurrentDrawMode == DrawMode.Walkable;
                            SetCellWalkable(cell, isWalkable);
                            e.Use();
                        }
                    }
                }
            }
        }

        // Direct placement method that doesn't use delayCall
        private void PlaceTileDirectly(Vector3 position, CellComponent cell)
        {
            if (m_SelectedTile == null) return;
            
            try
            {
                Debug.Log($"PlaceTileDirectly: position={position}, cellId={cell.CellId}");
                
                // Get exact cell center position
                Vector3 cellCenter = cell.transform.position;
                // Ensure Z is exactly 0 for proper 2D rendering
                cellCenter.z = 0;
                
                // Create or find Tiles parent
                GameObject tilesParent = GameObject.Find("Tiles");
                if (tilesParent == null)
                {
                    Debug.Log("Creating new 'Tiles' parent object");
                    tilesParent = new GameObject("Tiles");
                    Undo.RegisterCreatedObjectUndo(tilesParent, "Create Tiles Parent");
                }
                
                // Create a unique name with timestamp to avoid duplicate names
                string timestamp = DateTime.Now.Ticks.ToString();
                string tileName = $"Tile_{m_SelectedTile.Id}_{timestamp}";
                
                // Create the tile game object
                GameObject tileObject = new GameObject(tileName);
                Debug.Log($"Created tile GameObject: {tileName}");
                
                // Register with Undo system so it can be undone
                Undo.RegisterCreatedObjectUndo(tileObject, "Place Tile");
                
                // Set parent and position - do this separately to avoid Transform errors
                Undo.SetTransformParent(tileObject.transform, tilesParent.transform, "Set Tile Parent");
                Undo.RecordObject(tileObject.transform, "Set Tile Position");
                
                // Load sprite
                Debug.Log($"Loading sprite from: {m_SelectedTile.addressablePath}");
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(m_SelectedTile.addressablePath);
                float scale = m_SelectedTile.Scale > 0 ? m_SelectedTile.Scale : 1f;
                
                if (sprite != null)
                {
                    Debug.Log($"Sprite loaded successfully: {sprite.name}");
                    
                    // If snap to grid is enabled, always place at exact cell center
                    if (m_UseClipping)
                    {
                        // Calculate the exact center of the isometric cell
                        float cellWidth = GridCameraController.CellWidth;
                        
                        // Get the cell's base position and add half the cell width for isometric center
                        Vector3 adjustedPosition = cell.transform.position;
                        adjustedPosition.x += cellWidth / 2f;  // Move to center horizontally
                        adjustedPosition.z = 0;  // Ensure Z is 0
                        
                        tileObject.transform.position = adjustedPosition;
                        Debug.Log($"Snap to grid ON: Placing tile at adjusted isometric position: {adjustedPosition}");
                    }
                    else
                    {
                        // When snap to grid is off, use the clicked position
                        tileObject.transform.position = position;
                        tileObject.transform.position = new Vector3(position.x, position.y, 0);
                        Debug.Log($"Snap to grid OFF: Placing tile at clicked position: {position}");
                    }
                    
                    // Set scale
                    tileObject.transform.localScale = Vector3.one * scale;
                    
                    // Add and configure SpriteRenderer through Undo
                    SpriteRenderer spriteRenderer = Undo.AddComponent<SpriteRenderer>(tileObject);
                    Undo.RecordObject(spriteRenderer, "Configure Sprite Renderer");
                    
                    spriteRenderer.sprite = sprite;
                    spriteRenderer.sortingOrder = m_SelectedTile.Order;
                    spriteRenderer.flipX = m_SelectedTile.FlipX;
                    
                    // Set color
                    Color color = new Color(
                        m_SelectedTile.Color.Red,
                        m_SelectedTile.Color.Green,
                        m_SelectedTile.Color.Blue,
                        m_SelectedTile.Color.Alpha
                    );
                    spriteRenderer.color = color;
                    
                    // Show visual feedback and notifications
                    Debug.Log($"✅ Tile successfully placed: {tileName} at position {tileObject.transform.position}");
                    ShowNotification($"Tile placed: {m_SelectedTile.Id}", MessageType.Info);
                    
                    // Select the created object to provide visual feedback
                    Selection.activeGameObject = tileObject;
                    EditorGUIUtility.PingObject(tileObject);
                }
                else
                {
                    // If sprite can't be loaded, just use the cell center position
                    tileObject.transform.position = cellCenter;
                    tileObject.transform.localScale = Vector3.one * scale;
                    
                    Debug.LogError($"Failed to load sprite from: {m_SelectedTile.addressablePath}");
                    ShowNotification($"Failed to load sprite: {m_SelectedTile.Id}", MessageType.Error);
                    
                    // Create a placeholder visual to indicate missing sprite
                    SpriteRenderer placeholderRenderer = Undo.AddComponent<SpriteRenderer>(tileObject);
                    placeholderRenderer.color = Color.red;
                    
                    GameObject placeholder = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    Undo.RegisterCreatedObjectUndo(placeholder, "Create Placeholder");
                    placeholder.transform.SetParent(tileObject.transform);
                    placeholder.transform.localPosition = Vector3.zero;
                    placeholder.transform.localScale = Vector3.one * 0.5f;
                    
                    Debug.LogWarning($"Created placeholder for missing sprite: {tileName}");
                }
                
                // Important: Mark the scene as dirty to force save
                EditorUtility.SetDirty(tileObject);
                if (tilesParent != null) EditorUtility.SetDirty(tilesParent);
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                
                // Force scene repaint
                SceneView.RepaintAll();
                
                // Show a dialog to confirm placement only if that option is enabled
                if (m_ShowPlacementConfirmation)
                {
                    EditorUtility.DisplayDialog("Tile Placed", 
                        $"Tile '{m_SelectedTile.Id}' successfully placed at position {tileObject.transform.position}.", 
                        "Continue");
                }
                
                // Log a VERY obvious message to make sure the user sees it happened
                Debug.Log($"███ TILE PLACEMENT SUCCESSFUL! ███ Tile {m_SelectedTile.Id} at {tileObject.transform.position}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error placing tile directly: {ex.Message}\n{ex.StackTrace}");
            }
        }

        // New helper method to directly place a tile at current mouse position
        private void PlaceTileAtCurrentMousePosition()
        {
            if (m_SelectedTile == null || SceneView.lastActiveSceneView == null) return;
            
            Debug.Log("PLACING TILE AT CURRENT MOUSE POSITION via key shortcut");
            
            // Use sceneView.camera to get a ray in the current view
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null) return;
            
            // Get current event - safely
            Event e = Event.current;
            if (e == null)
            {
                Debug.LogError("No current event!");
                return;
            }
            
            Vector2 mousePos = e.mousePosition;
            Ray ray = HandleUtility.GUIPointToWorldRay(mousePos);
            
            // Try direct raycast
            RaycastHit2D hit = Physics2D.Raycast(ray.origin, ray.direction);
            if (hit.collider != null)
            {
                CellComponent cell = hit.collider.GetComponent<CellComponent>();
                if (cell != null)
                {
                    Vector3 position = m_UseClipping ? cell.transform.position : hit.point;
                    PlaceTileDirectly(position, cell);
                    return;
                }
            }
            
            // Try nearest cell as fallback
            Vector3 worldPoint = ray.GetPoint(10f);
            var allCells = FindObjectsByType<CellComponent>(FindObjectsSortMode.None);
            
            if (allCells.Length > 0)
            {
                CellComponent nearestCell = null;
                float nearestDistance = float.MaxValue;
                
                foreach (var cell in allCells)
                {
                    float distance = Vector2.Distance(cell.transform.position, worldPoint);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearestCell = cell;
                    }
                }
                
                if (nearestCell != null && nearestDistance < 2f)
                {
                    PlaceTileDirectly(nearestCell.transform.position, nearestCell);
                }
                else
                {
                    Debug.LogWarning($"No cell found near enough to mouse position (distance: {nearestDistance})");
                }
            }
            else
            {
                Debug.LogWarning("No cells found in scene");
            }
        }

        private void PlaceTileAtPosition(Vector3 position, CellComponent cell)
        {
            try
            {
                Debug.Log($"PlaceTileAtPosition: position={position}, cellId={cell.CellId}");
                
                if (m_SelectedTile == null)
                {
                    Debug.LogError("Cannot place tile: No tile selected");
                    EditorUtility.DisplayDialog("Cannot Place Tile", "No tile selected. Please select a tile first.", "OK");
                    return;
                }
                
                // Get exact cell center position
                Vector3 cellCenter = cell.transform.position;
                // Ensure Z is exactly 0 for proper 2D rendering
                cellCenter.z = 0;
                
                // Create or find Tiles parent
                GameObject tilesParent = GameObject.Find("Tiles");
                if (tilesParent == null)
                {
                    Debug.Log("Creating new 'Tiles' parent object");
                    tilesParent = new GameObject("Tiles");
                    Undo.RegisterCreatedObjectUndo(tilesParent, "Create Tiles Parent");
                }
                
                // Create a unique name with timestamp to avoid duplicate names
                string timestamp = DateTime.Now.Ticks.ToString();
                string tileName = $"Tile_{m_SelectedTile.Id}_{timestamp}";
                
                // Create the tile game object
                GameObject tileObject = new GameObject(tileName);
                Debug.Log($"Created tile GameObject: {tileName}");
                
                // Register with Undo system so it can be undone
                Undo.RegisterCreatedObjectUndo(tileObject, "Place Tile");
                
                // Set parent and position - do this separately to avoid Transform errors
                Undo.SetTransformParent(tileObject.transform, tilesParent.transform, "Set Tile Parent");
                Undo.RecordObject(tileObject.transform, "Set Tile Position");
                
                // Load sprite
                Debug.Log($"Loading sprite from: {m_SelectedTile.addressablePath}");
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(m_SelectedTile.addressablePath);
                float scale = m_SelectedTile.Scale > 0 ? m_SelectedTile.Scale : 1f;
                
                if (sprite != null)
                {
                    Debug.Log($"Sprite loaded successfully: {sprite.name}");
                    
                    // If snap to grid is enabled, always place at exact cell center
                    if (m_UseClipping)
                    {
                        // Calculate the exact center of the isometric cell
                        float cellWidth = GridCameraController.CellWidth;
                        
                        // Get the cell's base position and add half the cell width for isometric center
                        Vector3 adjustedPosition = cell.transform.position;
                        adjustedPosition.x += cellWidth / 2f;  // Move to center horizontally
                        adjustedPosition.z = 0;  // Ensure Z is 0
                        
                        tileObject.transform.position = adjustedPosition;
                        Debug.Log($"Snap to grid ON: Placing tile at adjusted isometric position: {adjustedPosition}");
                    }
                    else
                    {
                        // When snap to grid is off, use the clicked position
                        tileObject.transform.position = position;
                        tileObject.transform.position = new Vector3(position.x, position.y, 0);
                        Debug.Log($"Snap to grid OFF: Placing tile at clicked position: {position}");
                    }
                    
                    // Set scale
                    tileObject.transform.localScale = Vector3.one * scale;
                    
                    // Add and configure SpriteRenderer through Undo
                    SpriteRenderer spriteRenderer = Undo.AddComponent<SpriteRenderer>(tileObject);
                    Undo.RecordObject(spriteRenderer, "Configure Sprite Renderer");
                    
                    spriteRenderer.sprite = sprite;
                    spriteRenderer.sortingOrder = m_SelectedTile.Order;
                    spriteRenderer.flipX = m_SelectedTile.FlipX;
                    
                    // Set color
                    Color color = new Color(
                        m_SelectedTile.Color.Red,
                        m_SelectedTile.Color.Green,
                        m_SelectedTile.Color.Blue,
                        m_SelectedTile.Color.Alpha
                    );
                    spriteRenderer.color = color;
                    
                    // Show visual feedback and notifications
                    Debug.Log($"✅ Tile successfully placed: {tileName} at position {tileObject.transform.position}");
                    ShowNotification($"Tile placed: {m_SelectedTile.Id}", MessageType.Info);
                    
                    // Select the created object to provide visual feedback
                    Selection.activeGameObject = tileObject;
                    EditorGUIUtility.PingObject(tileObject);
                }
                else
                {
                    // If sprite can't be loaded, just use the cell center position
                    tileObject.transform.position = cellCenter;
                    tileObject.transform.localScale = Vector3.one * scale;
                    
                    Debug.LogError($"Failed to load sprite from: {m_SelectedTile.addressablePath}");
                    ShowNotification($"Failed to load sprite: {m_SelectedTile.Id}", MessageType.Error);
                    
                    // Create a placeholder visual to indicate missing sprite
                    SpriteRenderer placeholderRenderer = Undo.AddComponent<SpriteRenderer>(tileObject);
                    placeholderRenderer.color = Color.red;
                    
                    GameObject placeholder = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    Undo.RegisterCreatedObjectUndo(placeholder, "Create Placeholder");
                    placeholder.transform.SetParent(tileObject.transform);
                    placeholder.transform.localPosition = Vector3.zero;
                    placeholder.transform.localScale = Vector3.one * 0.5f;
                    
                    Debug.LogWarning($"Created placeholder for missing sprite: {tileName}");
                }
                
                // Important: Mark the scene as dirty to force save
                EditorUtility.SetDirty(tileObject);
                if (tilesParent != null) EditorUtility.SetDirty(tilesParent);
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                
                // Force repaint all views - REMOVED HandleUtility.Repaint() call that was causing issues
                SceneView.RepaintAll();
                
                // Show a dialog to confirm placement
                if (m_ShowPlacementConfirmation)
                {
                    EditorUtility.DisplayDialog("Tile Placed", 
                        $"Tile '{m_SelectedTile.Id}' successfully placed at position {tileObject.transform.position}.", 
                        "Continue");
                }
                
                Debug.Log($"Tile placement complete for {tileName}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error placing tile: {ex.Message}\n{ex.StackTrace}");
                ShowNotification($"Error placing tile: {ex.Message}", MessageType.Error);
                EditorUtility.DisplayDialog("Error", $"Failed to place tile: {ex.Message}", "OK");
            }
        }

        // Helper method to show notifications in the editor
        private void ShowNotification(string message, MessageType messageType)
        {
            // Show the notification in the editor window
            this.ShowNotification(new GUIContent(message));
            
            // Also try to show in scene view for better visibility
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
            {
                GUIContent content = new GUIContent($" {message} ");
                Color color;
                
                switch (messageType)
                {
                    case MessageType.Error:
                        color = new Color(1f, 0.3f, 0.3f, 0.8f); // Red
                        break;
                    case MessageType.Warning:
                        color = new Color(1f, 0.9f, 0.2f, 0.8f); // Yellow
                        break;
                    default:
                        color = new Color(0.2f, 0.8f, 0.2f, 0.8f); // Green
                        break;
                }
                
                // Draw this inside the editor update
                EditorApplication.delayCall += () => {
                    sceneView.ShowNotification(content, 2f); // Show for 2 seconds
                };
                
                // Flash the current selection as visual feedback
                if (messageType == MessageType.Info && Selection.activeGameObject != null)
                {
                    EditorGUIUtility.PingObject(Selection.activeGameObject);
                }
            }
        }

        private void SetCellWalkable(CellComponent cell, bool walkable)
        {
            // Accéder à la propriété IsWalkable via la méthode appropriée
            var cellData = cell.Cell;
            if (cellData != null)
            {
                var field = cellData.GetType().GetField("m_IsWalkable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    field.SetValue(cellData, walkable);
                    // Force le rafraîchissement de la scène
                    SceneView.RepaintAll();
                }
            }
        }

        private void DrawTilePreviewAtMousePosition(SceneView sceneView)
        {
            if (m_SelectedTile == null) return;
            
            // Get current mouse position
            Vector2 mousePosition = Event.current.mousePosition;
            Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
            
            // First, try raycast with all hits
            RaycastHit2D[] hits = Physics2D.RaycastAll(ray.origin, ray.direction, 100f);
            
            foreach (var hit in hits)
            {
                if (hit.collider != null)
                {
                    var cell = hit.collider.GetComponent<CellComponent>();
                    if (cell != null)
                    {
                        // Calculate preview position based on snap settings
                        Vector3 previewPos;
                        if (m_UseClipping)
                        {
                            // When using snap to grid, calculate the exact center of the isometric cell
                            float cellWidth = GridCameraController.CellWidth;
                            
                            // Get the cell's base position and add half the cell width for isometric center
                            previewPos = cell.transform.position;
                            previewPos.x += cellWidth / 2f;  // Move to center horizontally
                            previewPos.z = 0;  // Ensure Z is 0
                        }
                        else
                        {
                            previewPos = hit.point;
                        }
                        
                        DrawTilePreview(previewPos, cell);
                        return;
                    }
                }
            }
            
            // If raycast failed, find nearest cell
            Vector3 worldPoint = ray.origin + ray.direction * 10f;
            var allCells = UnityEngine.Object.FindObjectsByType<CellComponent>(FindObjectsSortMode.None);
            
            if (allCells.Length > 0)
            {
                CellComponent nearestCell = null;
                float nearestDistance = float.MaxValue;
                
                foreach (var cell in allCells)
                {
                    if (cell != null)
                    {
                        float distance = Vector2.Distance(cell.transform.position, worldPoint);
                        if (distance < nearestDistance)
                        {
                            nearestDistance = distance;
                            nearestCell = cell;
                        }
                    }
                }
                
                if (nearestCell != null && nearestDistance < 5f)
                {
                    // Calculate preview position for nearest cell
                    Vector3 previewPos;
                    if (m_UseClipping)
                    {
                        float cellWidth = GridCameraController.CellWidth;
                        previewPos = nearestCell.transform.position;
                        previewPos.x += cellWidth / 2f;
                        previewPos.z = 0;
                    }
                    else
                    {
                        previewPos = worldPoint;
                    }
                    
                    DrawTilePreview(previewPos, nearestCell);
                }
            }
        }

        private void DrawTilePreview(Vector3 position, CellComponent cell)
        {
            if (m_SelectedTile == null) return;
            
            // Load the sprite from the path
            Sprite previewSprite = AssetDatabase.LoadAssetAtPath<Sprite>(m_SelectedTile.addressablePath);
            if (previewSprite == null)
            {
                Debug.LogWarning($"Could not load preview sprite from {m_SelectedTile.addressablePath}");
                return;
            }
            
            // Calculate preview position based on snap settings
            Vector3 previewPosition;
            if (m_UseClipping)
            {
                // When using snap to grid, calculate the exact center of the isometric cell
                float cellWidth = GridCameraController.CellWidth;
                
                // Get the cell's base position and add half the cell width for isometric center
                previewPosition = cell.transform.position;
                previewPosition.x += cellWidth / 2f;  // Move to center horizontally
                previewPosition.z = 0;  // Ensure Z is 0
                
                Debug.Log($"Preview (Snap ON): Using cell center {previewPosition}, Cell Width: {cellWidth}");
            }
            else
            {
                // For free placement, use the exact position from the parameter
                previewPosition = position;
                previewPosition.z = 0;
                Debug.Log($"Preview (Snap OFF): Using mouse position {previewPosition}");
            }
            
            // Use a more visible highlight color
            Color fillColor = new Color(0.2f, 0.7f, 1f, 0.4f);  // Brighter blue with more opacity
            Color outlineColor = new Color(0.3f, 0.8f, 1f, 0.9f); // Even brighter outline with high opacity
            
            // Calculate sprite bounds based on sprite size
            Vector2 spriteSize = previewSprite.bounds.size;
            float scale = m_SelectedTile.Scale > 0 ? m_SelectedTile.Scale : 1f;
            
            // Calculate corner positions for highlighting the cell
            Vector3[] corners = new Vector3[]
            {
                previewPosition + new Vector3(-spriteSize.x/2f * scale, spriteSize.y/2f * scale, 0),
                previewPosition + new Vector3(spriteSize.x/2f * scale, spriteSize.y/2f * scale, 0),
                previewPosition + new Vector3(spriteSize.x/2f * scale, -spriteSize.y/2f * scale, 0),
                previewPosition + new Vector3(-spriteSize.x/2f * scale, -spriteSize.y/2f * scale, 0)
            };
            
            // Draw solid area with outline to highlight placement area
            Handles.DrawSolidRectangleWithOutline(corners, fillColor, outlineColor);
            
            // Draw additional crosshair at the center for better precision
            Handles.color = Color.yellow;
            float crossSize = 0.1f;
            Handles.DrawLine(previewPosition + new Vector3(-crossSize, 0, 0), previewPosition + new Vector3(crossSize, 0, 0));
            Handles.DrawLine(previewPosition + new Vector3(0, -crossSize, 0), previewPosition + new Vector3(0, crossSize, 0));
            
            // Draw preview sprite as a textured quad in the scene view
            Handles.BeginGUI();
            {
                // Convert world corners to screen space
                Vector2[] screenCorners = new Vector2[4];
                for (int i = 0; i < 4; i++)
                {
                    screenCorners[i] = HandleUtility.WorldToGUIPoint(corners[i]);
                }
                
                // Calculate rect for the sprite - fix the order of corners to prevent inversion
                float minX = Mathf.Min(screenCorners[0].x, screenCorners[1].x, screenCorners[2].x, screenCorners[3].x);
                float maxX = Mathf.Max(screenCorners[0].x, screenCorners[1].x, screenCorners[2].x, screenCorners[3].x);
                float minY = Mathf.Min(screenCorners[0].y, screenCorners[1].y, screenCorners[2].y, screenCorners[3].y);
                float maxY = Mathf.Max(screenCorners[0].y, screenCorners[1].y, screenCorners[2].y, screenCorners[3].y);

                Rect rect = new Rect(minX, minY, maxX - minX, maxY - minY);
                
                // Draw the preview sprite with slightly higher opacity
                GUI.color = new Color(1, 1, 1, 0.8f);
                
                if (m_SelectedTile.FlipX)
                {
                    // Handle flipped rendering
                    Matrix4x4 matrix = GUI.matrix;
                    GUIUtility.ScaleAroundPivot(new Vector2(-1, 1), new Vector2(rect.center.x, rect.center.y));
                    GUI.DrawTexture(rect, previewSprite.texture, ScaleMode.StretchToFill);
                    GUI.matrix = matrix;
                }
                else
                {
                    GUI.DrawTexture(rect, previewSprite.texture, ScaleMode.StretchToFill);
                }
                
                // Reset color
                GUI.color = Color.white;
            }
            Handles.EndGUI();
        }

        private void PlaceTileAtCell(Vector2Int cellPosition)
        {
            if (m_SelectedTile == null)
            {
                Debug.LogError("No tile selected!");
                return;
            }

            var mapComponent = FindObjectOfType<MapComponent>();
            if (mapComponent == null)
            {
                Debug.LogError("No MapComponent found in scene!");
                return;
            }

            // Get the map data
            var mapInformation = mapComponent.mapInformation;
            if (mapInformation == null)
            {
                Debug.LogError("Map data is null!");
                return;
            }

            // Find the corresponding cell to get exact position
            var allCells = FindObjectsByType<CellComponent>(FindObjectsSortMode.None);
            CellComponent targetCell = null;

            foreach (var cell in allCells)
            {
                Vector2Int cellPos = new Vector2Int(Mathf.RoundToInt(cell.transform.position.x), Mathf.RoundToInt(cell.transform.position.y));
                if (cellPos == cellPosition)
                {
                    targetCell = cell;
                    break;
                }
            }

            if (targetCell == null)
            {
                Debug.LogError($"Could not find cell at position {cellPosition}");
                return;
            }

            // Get exact cell center position
            Vector3 cellCenter = targetCell.transform.position;
            // Ensure Z is exactly 0 for proper 2D rendering
            cellCenter.z = 0;

            // Create a new tile object
            var tileObject = new GameObject($"Tile_{cellPosition.x}_{cellPosition.y}");
            
            // Create or find "Tiles" parent
            GameObject tilesParent = GameObject.Find("Tiles");
            if (tilesParent == null)
            {
                tilesParent = new GameObject("Tiles");
                Undo.RegisterCreatedObjectUndo(tilesParent, "Create Tiles Parent");
            }
            
            tileObject.transform.SetParent(tilesParent.transform);
            
            // Load sprite from path
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(m_SelectedTile.addressablePath);
            if (sprite == null)
            {
                Debug.LogError($"Failed to load sprite from path: {m_SelectedTile.addressablePath}");
                DestroyImmediate(tileObject);
                return;
            }
            
            // Always place at cell center (when snap to grid is enabled, but this method always uses snap)
            tileObject.transform.position = cellCenter;
            Debug.Log($"Placed tile at cell center: {cellCenter}");
            
            // Set scale based on tile data
            float scale = m_SelectedTile.Scale > 0 ? m_SelectedTile.Scale : 1f;
            tileObject.transform.localScale = Vector3.one * scale;
            
            // Add SpriteRenderer component
            var spriteRenderer = tileObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.sortingOrder = 0;

            // Mark the scene as dirty to save changes
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            
            Debug.Log($"Placed tile {m_SelectedTile.Id} at position {tileObject.transform.position}");
        }
    }
}
#endif 