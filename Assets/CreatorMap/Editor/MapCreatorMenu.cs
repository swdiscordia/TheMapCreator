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
            SpriteDrawing
        }
        
        private static DrawMode m_CurrentDrawMode = DrawMode.None;
        
        public static DrawMode CurrentDrawMode
        {
            get { return m_CurrentDrawMode; }
        }

        // Constants for data storage
        private const string DATA_FOLDER_PATH = "Assets/CreatorMap/Content/Data";
        private const string MAP_DATA_FILENAME = "MapData.asset";

        // Add icon texture field at the top of the class, after the enum declaration
        private Texture2D m_BrushIcon;

        // Add MenuItem attribute to show the window
        [MenuItem("Window/Map Creator/Map Creator", false, 100)]
        public static void ShowWindow()
        {
            var window = GetWindow<MapCreatorWindow>("Map Creator");
            window.minSize = new Vector2(300, 400);
            window.Show();
        }

        private void OnGUI()
        {
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

            // Add finalize button at the end
            DrawFinalizeButton();
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
            EditorGUILayout.BeginVertical();
            
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Draw Tools", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Create horizontal layout for the buttons
            EditorGUILayout.BeginHorizontal();
            
            // Create a GUIStyle for our square buttons
            var normalButtonStyle = new GUIStyle(GUI.skin.button);
            normalButtonStyle.normal.background = EditorGUIUtility.whiteTexture;
            normalButtonStyle.fixedWidth = 30;
            normalButtonStyle.fixedHeight = 30;
            
            // Create a style for selected buttons
            var selectedButtonStyle = new GUIStyle(normalButtonStyle);
            selectedButtonStyle.normal.background = EditorGUIUtility.whiteTexture;
            // Add a border to the selected button
            selectedButtonStyle.border = new RectOffset(2, 2, 2, 2);
            // Make the selected button slightly bigger
            selectedButtonStyle.fixedWidth = 34;
            selectedButtonStyle.fixedHeight = 34;
            // Add a dark outline
            selectedButtonStyle.margin = new RectOffset(-2, -2, -2, -2);

            // Store original GUI color
            var originalColor = GUI.color;

            // Green button for walkable
            GUI.color = new Color(0.2f, 0.8f, 0.2f); // Bright green
            bool isWalkableMode = m_CurrentDrawMode == DrawMode.Walkable;
            
            // Use the selected style if this mode is currently active
            if (GUILayout.Button("", isWalkableMode ? selectedButtonStyle : normalButtonStyle))
            {
                // Toggle between walkable and none, making sure to turn off non-walkable if it was active
                if (m_CurrentDrawMode == DrawMode.Walkable)
                {
                    m_CurrentDrawMode = DrawMode.None;
                }
                else
                {
                    m_CurrentDrawMode = DrawMode.Walkable;
                }
                SceneView.RepaintAll();
            }

            // Red button for non-walkable
            GUI.color = new Color(0.8f, 0.2f, 0.2f); // Bright red
            bool isNonWalkableMode = m_CurrentDrawMode == DrawMode.NonWalkable;
            
            // Use the selected style if this mode is currently active
            if (GUILayout.Button("", isNonWalkableMode ? selectedButtonStyle : normalButtonStyle))
            {
                // Toggle between non-walkable and none, making sure to turn off walkable if it was active
                if (m_CurrentDrawMode == DrawMode.NonWalkable)
                {
                    m_CurrentDrawMode = DrawMode.None;
                }
                else
                {
                    m_CurrentDrawMode = DrawMode.NonWalkable;
                }
                SceneView.RepaintAll();
            }

            // Restore original GUI color
            GUI.color = originalColor;
            
            // Add space between buttons
            GUILayout.Space(10);
            
            // Load the brush icon if not already loaded
            if (m_BrushIcon == null)
            {
                m_BrushIcon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/CreatorMap/Resources/Icons/brush_10946725.png");
            }
            
            // Create a special style for the brush button to match the other buttons
            var brushButtonStyle = new GUIStyle(normalButtonStyle);
            brushButtonStyle.fixedWidth = 40;
            brushButtonStyle.fixedHeight = 30;
            
            // Create a style for selected brush button
            var selectedBrushButtonStyle = new GUIStyle(selectedButtonStyle);
            selectedBrushButtonStyle.fixedWidth = 44;
            selectedBrushButtonStyle.fixedHeight = 34;
            
            // Check if sprite drawing mode is active
            bool isSpriteDrawingMode = m_CurrentDrawMode == DrawMode.SpriteDrawing;
            
            // Add the brush button with icon
            if (GUILayout.Button(m_BrushIcon, isSpriteDrawingMode ? selectedBrushButtonStyle : brushButtonStyle))
            {
                // Toggle between sprite drawing and none
                if (m_CurrentDrawMode == DrawMode.SpriteDrawing)
                {
                    m_CurrentDrawMode = DrawMode.None;
                }
                else
                {
                    m_CurrentDrawMode = DrawMode.SpriteDrawing;
                    
                    // Open Assets/CreatorMap/Content/Tiles folder and search for sprites
                    string tilesPath = "Assets/CreatorMap/Content/Tiles";
                    
                    // Create the directory if it doesn't exist
                    if (!Directory.Exists(tilesPath))
                    {
                        Directory.CreateDirectory(tilesPath);
                        AssetDatabase.Refresh();
                    }
                    
                    // Find all sprite assets in this folder and its subfolders
                    string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { tilesPath });
                    List<Sprite> sprites = new List<Sprite>();
                    
                    foreach (string guid in guids)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guid);
                        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                        if (sprite != null)
                        {
                            sprites.Add(sprite);
                        }
                    }
                    
                    Debug.Log($"Found {sprites.Count} sprites in {tilesPath} and its subfolders");
                    
                    // Open the folder in the Project window
                    UnityEngine.Object folderObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(tilesPath);
                    if (folderObject != null)
                    {
                        Selection.activeObject = folderObject;
                        EditorGUIUtility.PingObject(folderObject);
                    }
                }
                SceneView.RepaintAll();
            }

            EditorGUILayout.EndHorizontal();

            // Display current mode with a more descriptive message
            EditorGUILayout.Space(5);
            string modeDescription = "No drawing mode selected";
            if (m_CurrentDrawMode == DrawMode.Walkable)
                modeDescription = "Drawing walkable cells";
            else if (m_CurrentDrawMode == DrawMode.NonWalkable)
                modeDescription = "Drawing non-walkable cells";
            else if (m_CurrentDrawMode == DrawMode.SpriteDrawing)
                modeDescription = "Drawing sprites";
                
            EditorGUILayout.LabelField($"Current Mode: {modeDescription}", EditorStyles.boldLabel);

            EditorGUILayout.Space(10);
            
            // Add a Save Map button
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace(); // Center the button
            
            // Use a blue color for the save button
            GUI.backgroundColor = new Color(0.4f, 0.6f, 1.0f);
            if (GUILayout.Button("Save Map", GUILayout.Height(30), GUILayout.Width(120)))
            {
                SaveCurrentMapState();
            }
            GUI.backgroundColor = originalColor;
            
            GUILayout.FlexibleSpace(); // Center the button
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(5);

            EditorGUILayout.EndVertical();
            
            EditorGUILayout.EndVertical();
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
            
            if (isFixture)
            {
                // Handle as fixture (likely from a FixtureSpriteData)
                FixtureSpriteData fixtureData = null;
                
                // Try to find matching fixture data in our local data structure
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
                                fixtureData = fixture;
                                break;
                            }
                        }
                    }
                }
                
                // Use default values if fixture data not found
                Vector2 scale = fixtureData != null ? fixtureData.Scale : Vector2.one;
                float rotation = fixtureData != null ? fixtureData.Rotation : 0f;
                
                // Set scale and rotation
                spriteObj.transform.localScale = new Vector3(scale.x, scale.y, 1f);
                spriteObj.transform.rotation = Quaternion.Euler(0, 0, rotation);
            }
            else
            {
                // Handle as regular tile
                spriteObj.transform.localScale = new Vector3(tileData.Scale, tileData.Scale, 1f);
            }
            
            // Add TileSprite component with the exact structure from the main project
            var tileSprite = spriteObj.AddComponent<Models.Maps.TileSprite>();
            tileSprite.id = tileData.Id;
            tileSprite.key = key;
            tileSprite.type = (byte)(isFixture ? 1 : 0);
            
            // Set color data
            if (isFixture)
            {
                // Fixtures use direct color values
                tileSprite.colorMultiplicatorIsOne = tileData.Color.IsOne;
                tileSprite.colorMultiplicatorR = tileData.Color.Red;
                tileSprite.colorMultiplicatorG = tileData.Color.Green;
                tileSprite.colorMultiplicatorB = tileData.Color.Blue;
                tileSprite.colorMultiplicatorA = tileData.Color.Alpha;
            }
            else
            {
                // Regular tiles use values multiplied by 255
                tileSprite.colorMultiplicatorIsOne = tileData.Color.IsOne;
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
            GUILayout.Space(10);
            
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
    }
}
#endif 