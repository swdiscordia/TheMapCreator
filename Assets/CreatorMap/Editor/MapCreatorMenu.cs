#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
using System.IO;
using Managers.Maps.MapCreator;
using System;
using System.Collections.Generic;
using Managers.Cameras;

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

        [MenuItem("MapCreator/Open Map Creator")]
        public static void OpenMapCreator()
        {
            var window = GetWindow<MapCreatorWindow>("Map Creator");
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

            // Draw mode tools will be implemented here
            EditorGUILayout.HelpBox("Draw mode tools coming soon!", MessageType.Info);

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
                var mapData = new MapBasicInformation
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
                mapComponent.mapInformation = mapData;

                // Add the grid manager and create grid immediately
                var gridManager = mapObject.AddComponent<MapCreatorGridManager>();
                // Set it as the instance since we're not going through Awake
                MapCreatorGridManager.Instance = gridManager;
                // Call CreateGrid directly to generate the grid now
                var createGridMethod = typeof(MapCreatorGridManager).GetMethod("CreateGrid", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                createGridMethod.Invoke(gridManager, null);

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

        // Helper method to load map data
        private MapBasicInformation LoadMapData(int mapId)
        {
            int folderNumber = mapId % 10;
            string folderPath = Path.Combine(MAP_ROOT_PATH, folderNumber.ToString());
            string mapDataPath = Path.Combine(folderPath, $"{mapId}.json");

            if (!File.Exists(mapDataPath))
            {
                Debug.LogError($"Map data file not found: {mapDataPath}");
                return null;
            }

            string jsonData = File.ReadAllText(mapDataPath);
            return JsonUtility.FromJson<MapBasicInformation>(jsonData);
        }

        // Helper method to save map data
        private void SaveMapData(MapBasicInformation mapData)
        {
            var folderNumber = (int)(mapData.id % 10); // Explicit cast to int after modulo
            string folderPath = Path.Combine(MAP_ROOT_PATH, folderNumber.ToString());
            string mapDataPath = Path.Combine(folderPath, $"{mapData.id}.json");

            string jsonData = JsonUtility.ToJson(mapData, true);
            File.WriteAllText(mapDataPath, jsonData);
            Debug.Log($"Saved map data to: {mapDataPath}");
        }
    }
}
#endif 