#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
using System.IO;
using System;
using System.Collections.Generic;
using Managers.Cameras;
using CreatorMap.Scripts.Data;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using Components.Maps;
using UnityEngine.SceneManagement;
using System.Linq;
using CreatorMap.Scripts.Core.Grid;
using CreatorMap.Scripts.Core;
using CreatorMap.Scripts.Editor;
// Adding explicit references to ensure they're included in compilation
using TileSpriteData = CreatorMap.Scripts.Data.TileSpriteData;
using TileColorData = CreatorMap.Scripts.Data.TileColorData;
// Add explicit references to avoid ambiguity for types used in map creation
using MapCreatorGridData = CreatorMap.Scripts.Core.Grid.MapCreatorGridManager.GridData;
using MapCreatorCellData = CreatorMap.Scripts.Core.Grid.MapCreatorGridManager.CellData;

namespace MapCreator.Editor
{
    public class NewMapCreatorWindow : EditorWindow
    {
        private int m_SelectedTab = 0;
        private readonly string[] m_Tabs = { "Map Settings", "Draw Mode" };

        // Scroll position for draw mode
        private Vector2 m_DrawModeScrollPosition;

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
            CellProperties,
            TilePlacement
        }
        
        private DrawMode m_CurrentDrawMode = DrawMode.None;
        private DrawMode m_LastActiveDrawMode = DrawMode.CellProperties; // Update default mode
        private TileSpriteData m_SelectedTile = null;
        private bool m_IsFixtureTile = false; // Add flag for fixture tiles
        private bool m_EraserMode = false; // New flag for eraser mode instead of using DrawMode
        
        // Flags pour les états de cellule
        private bool m_CellWalkable = true;  // Ajout de la propriété walkable (true par défaut)
        private bool m_CellNonWalkableFight = false;
        private bool m_CellNonWalkableRP = false; 
        private bool m_CellBlockLineOfSight = false;
        private bool m_CellBlue = false;
        private bool m_CellRed = false;
        private bool m_CellVisible = true;  // Par défaut à true
        private bool m_CellFarm = false;
        private bool m_CellHavenbag = false;
        
        // Option pour réinitialiser une cellule (tout retirer sauf walkable)
        private bool m_ResetCell = false;
        
        // Option pour afficher les indicateurs visuels
        private bool m_ShowPropertyIndicators = true;

        // Constants for data storage
        private const string DATA_FOLDER_PATH = "Assets/CreatorMap/Content/Data";
        private const string MAP_DATA_FILENAME = "MapData.asset";

        // Add icon texture fields
        private Texture2D m_BrushIcon;
        private Texture2D m_EraserIcon; // Icon for eraser tool

        private Vector2 m_TileScrollPosition;
        private List<TileSpriteData> m_AvailableTiles = new List<TileSpriteData>();
        public static DrawMode CurrentDrawMode => Instance?.m_CurrentDrawMode ?? DrawMode.None;
        public static NewMapCreatorWindow Instance { get; private set; }

        private bool m_UseClipping = true; // New variable for clipping toggle

        // Dictionary to store asset paths separately
        private Dictionary<string, string> m_AssetPaths = new Dictionary<string, string>();
        
        // Tile placement variables
        private Dictionary<string, Texture2D> m_TilePreviews = new Dictionary<string, Texture2D>();
        private Texture2D m_DefaultTileTexture;
        
        // Static constructor to initialize static fields
        static NewMapCreatorWindow()
        {
            Debug.Log("NewMapCreatorWindow static constructor called");
        }

        // Helper method to store and retrieve asset paths
        private void SetAssetPath(string tileId, string path)
        {
            if (string.IsNullOrEmpty(tileId) || string.IsNullOrEmpty(path))
                return;
                
            m_AssetPaths[tileId] = path;
        }

        [MenuItem("Window/Map Creator/New Map Creator", false, 100)]
        public static void ShowWindow()
        {
            Debug.Log("Opening New Map Creator Window");
            var window = GetWindow<NewMapCreatorWindow>("New Map Creator");
            window.minSize = new Vector2(300, 400);
            
            // Ensure the instance is set
            if (Instance == null)
            {
                Instance = window;
            }
            
            window.Show();
        }

        private void OnEnable()
        {
            Debug.Log("NewMapCreatorWindow OnEnable called");
            Instance = this;
            LoadAvailableTiles();
            // Remove any existing handler and add our own
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
            Debug.Log("Registered OnSceneGUI callback successfully");
            
            // Load eraser icon
            m_EraserIcon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/CreatorMap/Editor/Icons/eraser_10946685.png");
            if (m_EraserIcon == null)
            {
                Debug.LogWarning("Eraser icon not found at Assets/CreatorMap/Editor/Icons/eraser_10946685.png");
            }
        }

        private void OnDisable()
        {
            Debug.Log("NewMapCreatorWindow OnDisable called");
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void LoadAvailableTiles()
        {
            m_AvailableTiles.Clear();
            m_AssetPaths.Clear(); // Clear the asset paths dictionary
            
            try
            {
                // Log when we start loading
                Debug.Log("Starting to load available tiles...");
                
                // Search more broadly in multiple locations for sprites
                string[] searchFolders = new[] { 
                    "Assets/CreatorMap/Tiles", 
                    "Assets/CreatorMap/Content/Tiles"
                };
                
                List<string> allGuids = new List<string>();
                
                foreach (string folder in searchFolders)
                {
                    if (System.IO.Directory.Exists(folder))
                    {
                        Debug.Log($"Searching for sprites in: {folder}");
                        string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { folder });
                        allGuids.AddRange(guids);
                        Debug.Log($"Found {guids.Length} sprites in {folder}");
                    }
                    else
                    {
                        Debug.LogWarning($"Directory not found: {folder}");
                    }
                }
                
                Debug.Log($"Total sprites found: {allGuids.Count}");
                
                if (allGuids.Count == 0)
                {
                    Debug.LogWarning("No tile sprites found in any of the search directories");
                    return;
                }
                
                foreach (string guid in allGuids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    if (sprite != null)
                    {
                        // Extract clean numeric ID from the sprite name
                        string numericId = new string(sprite.name.Where(char.IsDigit).ToArray());
                        if (string.IsNullOrEmpty(numericId))
                        {
                            Debug.LogWarning($"Skipping sprite with no numeric ID: {sprite.name} at {path}");
                            continue;
                        }
                        
                        var tileData = new TileSpriteData
                        {
                            // Use only the numeric ID without the GUID
                            Id = numericId, 
                            Position = Vector2.zero,
                            Scale = 1f,
                            Order = 0,
                            Color = new TileColorData { Red = 1f, Green = 1f, Blue = 1f, Alpha = 1f }
                        };
                        
                        // Store the asset path for later use
                        SetAssetPath(tileData.Id, path);
                        Debug.Log($"Added tile: ID={tileData.Id}, Path={path}");
                        
                        m_AvailableTiles.Add(tileData);
                    }
                    else
                    {
                        Debug.LogWarning($"Could not load sprite at path: {path}");
                }
                }
                
                Debug.Log($"Finished loading {m_AvailableTiles.Count} available tiles.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error loading available tiles: {ex.Message}");
            }
        }

        private void OnGUI()
        {
            // When in play mode, show warning without using any layout groups
            if (EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Map Creator is not available during Play Mode.", MessageType.Warning);
                return;
            }

            try
            {
                // Use the safest approach by wrapping everything in a try/finally block
                // with a single top-level layout group
                GUILayout.BeginVertical();
                
                // Tab selection
                m_SelectedTab = GUILayout.Toolbar(m_SelectedTab, m_Tabs);
                EditorGUILayout.Space();

                // Tab content
                if (m_SelectedTab == 0)
                {
                    DrawMapSettingsTabSafe();
                }
                else
                {
                    DrawDrawModeTabSafe();
                }
            }
            finally
            {
                // This will ALWAYS execute even if an exception occurs,
                // ensuring the layout group is properly closed
                GUILayout.EndVertical();
            }
        }

        // Completely safe version that uses minimal layout groups
        private void DrawMapSettingsTabSafe()
        {
            // Map Settings box
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            try
            {
                EditorGUILayout.LabelField("Map Settings", EditorStyles.boldLabel);
                EditorGUILayout.Space();

                m_ShowDefaultValues = EditorGUILayout.ToggleLeft("Use Default Map Size", m_ShowDefaultValues);
                EditorGUILayout.Space();

                if (m_ShowDefaultValues)
                {
                    GUI.enabled = false;
                    EditorGUILayout.IntField("Width (Default)", (int)Models.Maps.MapConstants.Width);
                    EditorGUILayout.IntField("Height (Default)", (int)Models.Maps.MapConstants.Height);
                    GUI.enabled = true;
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
            }
            finally
            {
                EditorGUILayout.EndVertical();
            }
            
            EditorGUILayout.Space(10);
            
            // Create Map box
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            try
            {
                EditorGUILayout.LabelField("Create New Map", EditorStyles.boldLabel);
                
                if (GUILayout.Button("Create Map", GUILayout.Height(40)))
                {
                    if (IsValidMapSize())
                    {
                        m_ShouldCreateMap = true;
                        Debug.Log("Creating new map with settings: " + 
                            (m_ShowDefaultValues ? "Default size" : $"Size: {m_MapWidth}x{m_MapHeight}"));
                        
                        CreateNewMap();
                    }
                    else
                    {
                        Debug.LogError("Invalid map size. Please check your settings.");
                        EditorUtility.DisplayDialog("Invalid Map Size", 
                            "Please enter valid map size values within the allowed range.", "OK");
                    }
                }
            }
            finally
            {
                EditorGUILayout.EndVertical();
            }
        }

        // Completely safe version that uses minimal layout groups with try/finally protection
        private void DrawDrawModeTabSafe()
        {
            // Add a scroll position field at the class level if it doesn't exist already
            // private Vector2 m_DrawModeScrollPosition;
            
            // Begin scroll view that wraps all content
            m_DrawModeScrollPosition = EditorGUILayout.BeginScrollView(m_DrawModeScrollPosition);
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            try
            {
                EditorGUILayout.LabelField("Map Drawing Tools", EditorStyles.boldLabel);
                EditorGUILayout.Space();
                
                // Add a master toggle for drawing mode
                bool isCurrentlyDrawing = m_CurrentDrawMode != DrawMode.None;
                GUI.backgroundColor = isCurrentlyDrawing ? Color.green : Color.gray;
                if (GUILayout.Button(isCurrentlyDrawing ? "Drawing Enabled (Click to Disable)" : "Drawing Disabled (Click to Enable)", 
                    GUILayout.Height(30)))
                {
                    if (isCurrentlyDrawing)
                    {
                        // Disable drawing by setting to None
                        m_CurrentDrawMode = DrawMode.None;
                        Debug.Log("Drawing disabled");
                    }
                    else
                    {
                        // Enable drawing with the last active mode or default to CellProperties
                        m_CurrentDrawMode = m_LastActiveDrawMode != DrawMode.None ? m_LastActiveDrawMode : DrawMode.CellProperties;
                        Debug.Log($"Drawing enabled with mode: {m_CurrentDrawMode}");
                    }
                }
                GUI.backgroundColor = Color.white;
                
                EditorGUILayout.Space(10);
                
                // Draw mode selection buttons
                EditorGUILayout.LabelField("Select Draw Mode:", EditorStyles.boldLabel);
                
                EditorGUILayout.BeginHorizontal();
                try
                {
                    GUI.backgroundColor = m_CurrentDrawMode == DrawMode.CellProperties ? Color.green : Color.white;
                    if (GUILayout.Button("Cell Properties", GUILayout.Height(30)))
                    {
                        m_CurrentDrawMode = DrawMode.CellProperties;
                        m_LastActiveDrawMode = DrawMode.CellProperties;
                        Debug.Log("Draw Mode set to: Cell Properties");
                    }
                    
                    GUI.backgroundColor = m_CurrentDrawMode == DrawMode.TilePlacement ? Color.yellow : Color.white;
                    if (GUILayout.Button("Tile Placement", GUILayout.Height(30)))
                    {
                        m_CurrentDrawMode = DrawMode.TilePlacement;
                        m_LastActiveDrawMode = DrawMode.TilePlacement;
                        Debug.Log("Draw Mode set to: Tile Placement");
                    }
                    
                    GUI.backgroundColor = Color.white;
                }
                finally
                {
                    EditorGUILayout.EndHorizontal();
                }
                
                EditorGUILayout.Space(10);
                
                // Display current mode
                EditorGUILayout.LabelField($"Current Mode: {m_CurrentDrawMode}", EditorStyles.boldLabel);
                
                EditorGUILayout.Space(10);
                
                // Section pour les états de cellule
                if (m_CurrentDrawMode == DrawMode.CellProperties)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    try
                    {
                        EditorGUILayout.LabelField("Cell Properties:", EditorStyles.boldLabel);
                        EditorGUILayout.Space(5);
                        
                        // Propriété primaire: Walkable
                        GUIContent walkableContent = new GUIContent("Walkable", 
                            "If checked, entities can walk on this cell");
                        m_CellWalkable = EditorGUILayout.Toggle(walkableContent, m_CellWalkable);
                        
                        EditorGUILayout.Space(3);
                        
                        // Autres propriétés d'accessibilité
                        GUIContent fightContent = new GUIContent("Non-Walkable During Fight", 
                            "If checked, this cell cannot be walked on during combat");
                        m_CellNonWalkableFight = EditorGUILayout.Toggle(fightContent, m_CellNonWalkableFight);
                        
                        GUIContent rpContent = new GUIContent("Non-Walkable During RP", 
                            "If checked, this cell cannot be walked on during role-play");
                        m_CellNonWalkableRP = EditorGUILayout.Toggle(rpContent, m_CellNonWalkableRP);
                        
                        GUIContent losContent = new GUIContent("Block Line of Sight", 
                            "If checked, this cell blocks line of sight");
                        m_CellBlockLineOfSight = EditorGUILayout.Toggle(losContent, m_CellBlockLineOfSight);
                        
                        EditorGUILayout.Space(5);
                        
                        // Propriétés de couleur
                        EditorGUILayout.BeginHorizontal();
                        GUIContent blueContent = new GUIContent("Blue Cell", "Mark this cell as blue");
                        m_CellBlue = EditorGUILayout.Toggle(blueContent, m_CellBlue);
                        
                        GUIContent redContent = new GUIContent("Red Cell", "Mark this cell as red");
                        m_CellRed = EditorGUILayout.Toggle(redContent, m_CellRed);
                        EditorGUILayout.EndHorizontal();
                        
                        // Propriétés de visibilité
                        GUIContent visibleContent = new GUIContent("Visible Cell", 
                            "If checked, this cell is visible");
                        m_CellVisible = EditorGUILayout.Toggle(visibleContent, m_CellVisible);
                        
                        EditorGUILayout.Space(5);
                        
                        // Propriétés spéciales
                        EditorGUILayout.BeginHorizontal();
                        GUIContent farmContent = new GUIContent("Farm Cell", "Mark as a farm cell");
                        m_CellFarm = EditorGUILayout.Toggle(farmContent, m_CellFarm);
                        
                        GUIContent havenContent = new GUIContent("Havenbag Cell", "Mark as a havenbag cell");
                        m_CellHavenbag = EditorGUILayout.Toggle(havenContent, m_CellHavenbag);
                        EditorGUILayout.EndHorizontal();
                        
                        EditorGUILayout.Space(10);
                        
                        // Option pour réinitialiser une cellule
                        GUI.backgroundColor = Color.yellow; // Couleur d'avertissement
                        GUIContent resetContent = new GUIContent("Reset Cell", 
                            "Reset cell to walkable state with no other properties");
                        m_ResetCell = EditorGUILayout.Toggle(resetContent, m_ResetCell);
                        GUI.backgroundColor = Color.white; // Restaurer la couleur par défaut
                    }
                    finally
                    {
                        EditorGUILayout.EndVertical();
                    }
                }
                
                // Options supplémentaires
                EditorGUILayout.Space(10);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                // Option pour afficher les indicateurs visuels
                EditorGUILayout.LabelField("Options d'affichage", EditorStyles.boldLabel);
                
                bool showIndicators = EditorGUILayout.Toggle(
                    new GUIContent("Afficher les indicateurs", 
                        "Afficher les symboles visuels pour les propriétés des cellules dans l'éditeur"), 
                    m_ShowPropertyIndicators);
                
                if (showIndicators != m_ShowPropertyIndicators)
                {
                    m_ShowPropertyIndicators = showIndicators;
                    SceneView.RepaintAll(); // Mettre à jour la vue immédiatement
                }
                
                EditorGUILayout.EndVertical();
                
                // Tile drawing options
                EditorGUILayout.Space(10);
                
                // Only show tile selection when in tile placement mode
                if (m_CurrentDrawMode == DrawMode.TilePlacement)
                {
                    // Add fixture checkbox
                    bool newIsFixture = EditorGUILayout.Toggle("Is Fixture", m_IsFixtureTile);
                    if (newIsFixture != m_IsFixtureTile)
                    {
                        m_IsFixtureTile = newIsFixture;
                        Debug.Log($"Fixture mode set to: {m_IsFixtureTile}");
                    }
                    
                    // Add clipping toggle
                    bool newUseClipping = EditorGUILayout.Toggle("Clip to Grid", m_UseClipping);
                    if (newUseClipping != m_UseClipping)
                    {
                        m_UseClipping = newUseClipping;
                        Debug.Log($"Clip to Grid mode set to: {m_UseClipping}");
                    }
                    
                    EditorGUILayout.Space(5);
                    
                    // Add Available Tiles label with eraser button to the right
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Available Tiles:", EditorStyles.boldLabel, GUILayout.ExpandWidth(true));
                    
                    // Create a style for the eraser button with visual push effect
                    GUIStyle eraserButtonStyle = new GUIStyle(GUI.skin.button);
                    if (m_EraserMode)
                    {
                        // Visual push effect when eraser is selected
                        eraserButtonStyle.normal.background = EditorGUIUtility.whiteTexture;
                        eraserButtonStyle.normal.textColor = Color.white;
                        eraserButtonStyle.hover.background = EditorGUIUtility.whiteTexture;
                        eraserButtonStyle.hover.textColor = Color.white;
                    }
                    
                    // Draw the eraser button with icon
                    GUIContent eraserContent = new GUIContent(
                        m_EraserIcon, 
                        "Eraser Tool - Right click to erase at mouse position"
                    );
                    
                    if (GUILayout.Button(eraserContent, eraserButtonStyle, GUILayout.Width(32), GUILayout.Height(32)))
                    {
                        m_EraserMode = !m_EraserMode;
                        Debug.Log($"Eraser tool {(m_EraserMode ? "enabled" : "disabled")}");
                    }
                    
                    // Highlight eraser button when selected
                    if (m_EraserMode)
                    {
                        Rect lastRect = GUILayoutUtility.GetLastRect();
                        EditorGUI.DrawRect(lastRect, new Color(1f, 0.5f, 0.5f, 0.3f));
                    }
                    
                    EditorGUILayout.EndHorizontal();
                    
                    // Display the tile selection grid
                    EditorGUILayout.Space();
                    m_TileScrollPosition = EditorGUILayout.BeginScrollView(m_TileScrollPosition, 
                        GUILayout.Height(200));
                    
                    // Create a grid of tile buttons
                    int columns = 4;
                    int i = 0;
                    
                    if (m_AvailableTiles.Count == 0)
                    {
                        EditorGUILayout.HelpBox("No tiles available. Make sure you have sprite files in Assets/CreatorMap/Content/Tiles", MessageType.Warning);
                        
                        if (GUILayout.Button("Refresh Tiles"))
                        {
                            LoadAvailableTiles();
                        }
                    }
                    else
                    {
                        EditorGUILayout.BeginHorizontal();
                        
                        foreach (var tile in m_AvailableTiles)
                        {
                            if (i > 0 && i % columns == 0)
                            {
                                EditorGUILayout.EndHorizontal();
                                EditorGUILayout.BeginHorizontal();
                            }
                            
                            // Get or create tile preview
                            Texture2D preview = GetTilePreview(tile);
                            
                            // Display tile button
                            if (GUILayout.Button(new GUIContent(preview, tile.Id), 
                                GUILayout.Width(64), GUILayout.Height(64)))
                            {
                                m_SelectedTile = tile;
                            }
                            
                            // Highlight selected tile
                            if (m_SelectedTile != null && m_SelectedTile.Id == tile.Id)
                            {
                                Rect lastRect = GUILayoutUtility.GetLastRect();
                                EditorGUI.DrawRect(lastRect, new Color(0, 1, 0, 0.3f));
                            }
                            
                            i++;
                        }
                        
                        // Complete the horizontal group
                        if (i % columns != 0)
                        {
                            for (int j = 0; j < columns - (i % columns); j++)
                            {
                                GUILayout.Space(64);
                            }
                        }
                        
                        EditorGUILayout.EndHorizontal();
                    }
                    
                    EditorGUILayout.EndScrollView();
                    
                    // Show selected tile properties
                    if (m_SelectedTile != null)
                    {
                        EditorGUILayout.Space(10);
                        
                        // Selected tile preview section - show a larger preview
                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                        try
                        {
                            EditorGUILayout.LabelField("Selected Tile Preview", EditorStyles.boldLabel);
                            
                            // Get preview texture
                            Texture2D preview = GetTilePreview(m_SelectedTile);
                            
                            // Calculate preview size (maintain aspect ratio but limit to max size)
                            float maxPreviewSize = 128f;
                            float width = preview.width;
                            float height = preview.height;
                            
                            if (width > height)
                            {
                                // Landscape
                                if (width > maxPreviewSize)
                                {
                                    float ratio = maxPreviewSize / width;
                                    width = maxPreviewSize;
                                    height *= ratio;
                                                    }
                                                }
                                                else
                                                {
                                // Portrait or square
                                if (height > maxPreviewSize)
                                {
                                    float ratio = maxPreviewSize / height;
                                    height = maxPreviewSize;
                                    width *= ratio;
                                }
                            }
                            
                            // Draw the preview texture
                            Rect previewRect = GUILayoutUtility.GetRect(width, height);
                            // Center the preview
                            previewRect.x = (EditorGUIUtility.currentViewWidth - width) * 0.5f;
                            GUI.DrawTexture(previewRect, preview, ScaleMode.ScaleToFit);
                            
                            // Display asset information
                            EditorGUILayout.LabelField("Tile ID: " + m_SelectedTile.Id);
                            EditorGUILayout.LabelField("Type: " + (m_IsFixtureTile ? "Fixture" : "Regular Tile"));
                            
                            string path = GetAddressablePath(m_SelectedTile);
                            EditorGUILayout.LabelField("Asset Path: ", EditorStyles.boldLabel);
                            EditorGUILayout.SelectableLabel(path, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                                        }
                                        finally
                                        {
                                            EditorGUILayout.EndVertical();
                                        }
                        
                        EditorGUILayout.Space(10);
                        
                        // Original tile properties section
                        EditorGUILayout.LabelField("Tile Properties", EditorStyles.boldLabel);
                        
                        // Tile layer/order selection - Using IntField instead of slider for greater range
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField("Layer Order:", GUILayout.Width(100));
                        int newOrder = EditorGUILayout.IntField(m_SelectedTile.Order);
                        EditorGUILayout.EndHorizontal();
                        
                        // Display the allowed range information
                        EditorGUILayout.HelpBox("Layer order can be any integer value (negative or positive), including values beyond 33,000.", MessageType.Info);
                        
                        if (newOrder != m_SelectedTile.Order)
                        {
                            m_SelectedTile.Order = newOrder;
                        }
                        
                        // Flip X option
                        bool newFlipX = EditorGUILayout.Toggle("Flip Horizontally", m_SelectedTile.FlipX);
                        if (newFlipX != m_SelectedTile.FlipX)
                        {
                            m_SelectedTile.FlipX = newFlipX;
                        }
                        
                        // Flip Y option
                        bool newFlipY = EditorGUILayout.Toggle("Flip Vertically", m_SelectedTile.FlipY);
                        if (newFlipY != m_SelectedTile.FlipY)
                        {
                            m_SelectedTile.FlipY = newFlipY;
                        }
                        
                        // Scale option
                        float newScale = EditorGUILayout.Slider("Scale", m_SelectedTile.Scale, 0.5f, 2f);
                        if (!Mathf.Approximately(newScale, m_SelectedTile.Scale))
                        {
                            m_SelectedTile.Scale = newScale;
                        }
                    }
                }
            }
            finally
            {
                EditorGUILayout.EndVertical();
            }
            
            // End the scroll view
            EditorGUILayout.EndScrollView();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            // Handle scene view interactions
            Event e = Event.current;
            if (e == null) return;

            // Handle keyboard shortcuts
            if (e.type == EventType.KeyDown)
            {
                // Toggle drawing mode with Space key
                if (e.keyCode == KeyCode.Space)
                {
                    ToggleDrawingMode();
                    e.Use(); // Mark event as used
                    sceneView.Repaint();
                    Repaint(); // Repaint the window
                }
            }

            // Debug the current mode
            Handles.BeginGUI();
            GUI.Label(new Rect(10, 10, 200, 20), $"Current Mode: {m_CurrentDrawMode}");
            Handles.EndGUI();
        }

        // Helper method to toggle drawing mode
        private void ToggleDrawingMode()
        {
            if (m_CurrentDrawMode == DrawMode.None)
            {
                // Enable drawing with the last active mode
                m_CurrentDrawMode = m_LastActiveDrawMode != DrawMode.None ? m_LastActiveDrawMode : DrawMode.CellProperties;
                Debug.Log($"Drawing enabled with mode: {m_CurrentDrawMode}");
            }
            else
            {
                // Disable drawing
                m_CurrentDrawMode = DrawMode.None;
                Debug.Log("Drawing disabled");
            }
        }

        private bool IsValidMapSize()
        {
            if (m_ShowDefaultValues) return true;
            return m_MapWidth >= m_MinMapSize.x && m_MapWidth <= m_MaxMapSize.x
                && m_MapHeight >= m_MinMapSize.y && m_MapHeight <= m_MaxMapSize.y;
        }

        // Get the addressable path for a tile
        private string GetAddressablePath(TileSpriteData tileData)
        {
            if (tileData == null)
                return string.Empty;
                
            // Get numeric ID
            string numericId = tileData.Id;
            
            // Get subfolder
            string subfolder = numericId.Length >= 2 ? numericId.Substring(0, 2) : numericId;
            
            // Format the path according to whether it's a fixture or not
            string prefix = m_IsFixtureTile ? "Fixture Assets" : "Tiles Assets";
            return $"{prefix}/Tiles/{subfolder}/{numericId}.png";
        }

        // Méthode pour créer une nouvelle carte
        private void CreateNewMap()
        {
            try
            {
                // Créer le répertoire de base s'il n'existe pas
                if (!Directory.Exists(MAP_ROOT_PATH))
                {
                    Directory.CreateDirectory(MAP_ROOT_PATH);
                }
                
                // Générer un ID unique pour la carte
                int mapId = MAP_ID_START + UnityEngine.Random.Range(0, 1000000);
                string sceneObjectName = $"Scene {mapId}"; // Nom de l'objet principal de la scène
                string mapObjectName = $"Map {mapId}"; // Nom de l'objet Map
                
                // Determine subfolder using modulo 10 (last digit of ID)
                int folderIndex = mapId % 10;
                string subfolderPath = $"{MAP_ROOT_PATH}/{folderIndex}";
                
                // Create subfolder if it doesn't exist
                if (!Directory.Exists(subfolderPath))
                {
                    Directory.CreateDirectory(subfolderPath);
                    AssetDatabase.Refresh();
                }
                
                // Créer une scène pour la carte avec juste le numéro comme nom
                string scenePath = $"{subfolderPath}/{mapId}.unity";
                EditorSceneManager.SaveScene(EditorSceneManager.NewScene(NewSceneSetup.EmptyScene), scenePath);
                
                // Ouvrir la scène
                EditorSceneManager.OpenScene(scenePath);
                
                // Configuration de la taille de la carte
                int width = m_ShowDefaultValues ? (int)Models.Maps.MapConstants.Width : m_MapWidth;
                int height = m_ShowDefaultValues ? (int)Models.Maps.MapConstants.Height : m_MapHeight;
                
                // 1. Setup camera with proper configuration
                var cameraObject = new GameObject("Main Camera");
                var camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 4.85f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(15f/255f, 15f/255f, 15f/255f, 1f); // RGB(15,15,15)
                camera.nearClipPlane = -1000f;
                camera.farClipPlane = 1000f;
                camera.depth = -1f;
                
                // Set exact camera position
                cameraObject.transform.position = new Vector3(6.2f, 2.7f, -10f);
                
                // Add camera controller with proper settings
                var cameraController = cameraObject.AddComponent<GridCameraController>();
                
                // 2. Create the Grid object with GridManager
                var gridObject = new GameObject("GridManager");
                // Position the grid at specific position
                gridObject.transform.position = new Vector3(9.36051f, 5.147355f, -9.977507f);
                
                // Add the GridManager component
                var gridManager = gridObject.AddComponent<MapCreatorGridManager>();
                
                // 3. Create the Map object with GridManager reference
                var mapObject = new GameObject(mapObjectName);
                
                // Set transform position to origin
                mapObject.transform.position = Vector3.zero;
                
                // Add the MapComponent for compatibility with the main project
                var mapComponent = mapObject.AddComponent<Components.Maps.MapComponent>();
                
                // Make sure mapInformation exists
                mapComponent.mapInformation = new CreatorMap.Scripts.Data.MapBasicInformation();
                mapComponent.mapInformation.id = mapId;
                
                // Initialize the cells in MapComponent
                mapComponent.mapInformation.InitializeAllCells();
                
                // Set background color to black
                mapComponent.backgroundColor = Color.black;
                
                // Initialize GridManager data
                gridManager.gridData.id = mapId;
                gridManager.gridData.cells = new List<CreatorMap.Scripts.Core.Grid.MapCreatorGridManager.CellData>();
                gridManager.gridData.cellsDict = new Dictionary<ushort, uint>();
                
                // Copy cell data from MapComponent to GridManager
                foreach (var cellPair in mapComponent.mapInformation.cells.dictionary)
                {
                    gridManager.gridData.cells.Add(new CreatorMap.Scripts.Core.Grid.MapCreatorGridManager.CellData(cellPair.Key, cellPair.Value));
                    gridManager.gridData.cellsDict[cellPair.Key] = cellPair.Value;
                }
                
                // Add editor controller to GridManager
                var editorController = gridObject.AddComponent<CreatorMap.Scripts.Editor.MapEditorController>();
                
                // Add GridDataSync to ensure data synchronization
                var gridDataSync = gridObject.AddComponent<CreatorMap.Scripts.Core.Grid.GridDataSync>();
                
                // Initialize the grid
                gridManager.CreateGrid();
                
                // Keep GridManager as a separate object in the scene
                gridObject.transform.SetParent(null);
                
                // Ensure everything is marked as dirty
                UnityEditor.EditorUtility.SetDirty(gridManager);
                UnityEditor.EditorUtility.SetDirty(gridObject);
                UnityEditor.EditorUtility.SetDirty(mapComponent);
                UnityEditor.EditorUtility.SetDirty(mapObject);
                
                // Log creation with subfolder information
                Debug.Log($"Scene '{sceneObjectName}' with Map '{mapObjectName}' (ID: {mapId}) created in folder {folderIndex} with size {width}x{height}.");
                
                // Passer en mode dessin
                m_SelectedTab = 1; // Passer à l'onglet Draw Mode
                m_CurrentDrawMode = DrawMode.CellProperties; // Définir le mode par défaut
                
                // Enregistrer la scène
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                
                // Marquer la scène comme dirty pour qu'Unity la sauvegarde
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                
                // Force repaint of the window to update the UI
                Repaint();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error creating map: {ex.Message}");
                EditorUtility.DisplayDialog("Error", $"Failed to create map: {ex.Message}", "OK");
            }
        }

        // Add this method to get tile previews
        private Texture2D GetTilePreview(TileSpriteData tileData)
        {
            if (tileData == null)
            {
                Debug.LogWarning("GetTilePreview called with null tileData");
                return GetDefaultTileTexture();
            }
            
            // Check if we have a cached preview
            if (m_TilePreviews.TryGetValue(tileData.Id, out Texture2D preview) && preview != null)
            {
                return preview;
            }
            
            Debug.Log($"Generating preview for tile ID: {tileData.Id}");
            
            // Try to load the sprite using the direct asset path stored in dictionary
            if (m_AssetPaths.TryGetValue(tileData.Id, out string assetPath) && !string.IsNullOrEmpty(assetPath))
            {
                Debug.Log($"Loading preview from asset path: {assetPath}");
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                if (sprite != null && sprite.texture != null)
                {
                    // Cache and return
                    m_TilePreviews[tileData.Id] = sprite.texture;
                    return sprite.texture;
                }
                else
                {
                    Debug.LogWarning($"Failed to load sprite for preview from path: {assetPath}");
                }
            }
            else
            {
                Debug.LogWarning($"No asset path found for tile ID: {tileData.Id}");
            }
            
            // Try alternative paths if direct path failed
            string numericId = tileData.Id;
            string[] alternativePaths = new[] {
                $"Assets/CreatorMap/Tiles/{numericId.Substring(0, Math.Min(2, numericId.Length))}/{numericId}.png",
                $"Assets/CreatorMap/Content/Tiles/{numericId.Substring(0, Math.Min(2, numericId.Length))}/{numericId}.png"
            };
            
            foreach (string altPath in alternativePaths)
            {
                if (System.IO.File.Exists(altPath))
                {
                    Debug.Log($"Trying alternative path for preview: {altPath}");
                    Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(altPath);
                    if (sprite != null && sprite.texture != null)
                    {
                        // Cache the path for future use
                        SetAssetPath(tileData.Id, altPath);
                        // Cache and return the texture
                        m_TilePreviews[tileData.Id] = sprite.texture;
                        Debug.Log($"Successfully loaded preview from alternative path: {altPath}");
                        return sprite.texture;
                    }
                }
            }
            
            // Return default texture if no sprite found
            Debug.LogWarning($"Failed to load preview for tile ID: {tileData.Id}, using default texture");
            return GetDefaultTileTexture();
        }
        
        // Helper method to create a default texture
        private Texture2D GetDefaultTileTexture()
        {
            if (m_DefaultTileTexture == null)
            {
                m_DefaultTileTexture = new Texture2D(64, 64);
                Color[] colors = new Color[64 * 64];
                
                // Create a checkerboard pattern like in the placeholder sprite
                for (int y = 0; y < 64; y++)
                {
                    for (int x = 0; x < 64; x++)
                    {
                        bool isEvenX = (x / 8) % 2 == 0;
                        bool isEvenY = (y / 8) % 2 == 0;
                        
                        if (isEvenX == isEvenY)
                        {
                            colors[y * 64 + x] = new Color(0.8f, 0.3f, 0.8f, 1.0f); // Purple
                        }
                        else
                        {
                            colors[y * 64 + x] = new Color(0.3f, 0.8f, 0.8f, 1.0f); // Cyan
                        }
                        
                        // Add border
                        if (x < 2 || x > 61 || y < 2 || y > 61)
                        {
                            colors[y * 64 + x] = new Color(0.1f, 0.1f, 0.1f, 1.0f);
                        }
                    }
                }
                
                m_DefaultTileTexture.SetPixels(colors);
                m_DefaultTileTexture.Apply();
                Debug.Log("Created default tile texture with checkerboard pattern");
            }
            
            return m_DefaultTileTexture;
        }

        // Add these public methods to allow the MapCreatorSceneGUI to access the current mode and selected tile
        public DrawMode GetCurrentDrawMode()
        {
            return m_CurrentDrawMode;
        }

        public TileSpriteData GetSelectedTile()
        {
            return m_SelectedTile;
        }
        
        // Add method to get the fixture status
        public bool IsFixtureTile()
        {
            return m_IsFixtureTile;
        }

        // Get asset path for a tile ID
        public string GetAssetPath(string tileId)
        {
            if (string.IsNullOrEmpty(tileId))
                return null;
            
            if (m_AssetPaths.TryGetValue(tileId, out string path))
                return path;
            
            return null;
        }

        public void OnInspectorUpdate()
        {
            // This will be called at 10 frames per second
            Repaint();
        }
        
        /// <summary>
        /// Helper method to check and ensure compatibility with runtime loading
        /// </summary>
        [MenuItem("Tools/TheMapCreator/Fix Runtime References")]
        public static void FixRuntimeReferences()
        {
            Debug.Log("Checking map components for runtime compatibility...");
            
            // Find all our TileSprite components in the scene
            var allTiles = FindObjectsOfType<CreatorMap.Scripts.TileSprite>();
            if (allTiles.Length == 0)
            {
                Debug.LogWarning("No tile sprites found in the scene to fix");
                return;
            }
            
            Debug.Log($"Found {allTiles.Length} tile sprites to process");
            
            foreach (var tile in allTiles)
            {
                // Make sure the SpriteRenderer has the correct flip settings
                var renderer = tile.GetComponent<SpriteRenderer>();
                if (renderer != null)
                {
                    renderer.flipX = tile.flipX;
                    renderer.flipY = tile.flipY;
                }
            }
            
            Debug.Log("Runtime reference fix complete. Map should now load correctly at runtime.");
        }

        // Add method to check if clipping is enabled
        public bool UseClipping()
        {
            return m_UseClipping;
        }

        // Getter pour l'affichage des indicateurs
        public bool ShowPropertyIndicators()
        {
            return m_ShowPropertyIndicators;
        }
        
        // Getter pour l'option de réinitialisation de cellule
        public bool ShouldResetCell()
        {
            return m_ResetCell;
        }
        
        // Helper method to access current instance
        private static NewMapCreatorWindow NewMapCreatorWindowType => Instance;

        public ushort GetCellProperties()
        {
            // Si l'option de réinitialisation est activée, renvoyer une cellule walkable sans autres états
            if (m_ResetCell)
            {
                // Réinitialiser - toujours retourner 0 (cellule walkable sans autres propriétés)
                ushort result = 0;
                
                // Réinitialiser l'option après utilisation
                m_ResetCell = false;
                
                return result;
            }
            
            ushort properties = 0;
            
            // Walkable est un cas spécial - bit 0 est 0 si walkable, 1 si non-walkable
            if (!m_CellWalkable) properties |= CELL_WALKABLE;
            
            // Ajouter uniquement les propriétés qui sont cochées
            if (m_CellNonWalkableFight) properties |= CELL_NON_WALKABLE_FIGHT;
            if (m_CellNonWalkableRP) properties |= CELL_NON_WALKABLE_RP;
            if (m_CellBlockLineOfSight) properties |= CELL_LINE_OF_SIGHT;
            if (m_CellBlue) properties |= CELL_BLUE;
            if (m_CellRed) properties |= CELL_RED;
            if (m_CellVisible) properties |= CELL_VISIBLE;
            if (m_CellFarm) properties |= CELL_FARM;
            if (m_CellHavenbag) properties |= CELL_HAVENBAG;
            
            return properties;
        }

        // Getter for eraser mode
        public bool IsEraserMode()
        {
            return m_EraserMode;
        }
    }
}
#endif 