#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using UnityEngine.Tilemaps;

namespace MapCreator.Editor
{
    public class MapCreatorSpriteEditor : EditorWindow
    {
        private Vector2 m_ScrollPosition;
        private string m_SearchQuery = "";
        private Sprite m_SelectedSprite;
        private List<Sprite> m_AvailableSprites = new List<Sprite>();
        private int m_SortingOrder = 0;
        private Vector2 m_Scale = Vector2.one;
        private float m_Rotation = 0f;
        private Color m_SpriteColor = Color.white;
        private bool m_CreateAsFixture = false;
        private bool m_ShowAdvancedOptions = false;
        private float m_LastRefreshTime = 0;
        
        // Sprite placement mode
        private enum PlacementMode
        {
            Single,
            TilePattern
        }
        
        private PlacementMode m_CurrentMode = PlacementMode.Single;
        
        [MenuItem("Window/Map Creator/Sprite Editor")]
        public static void ShowWindow()
        {
            GetWindow<MapCreatorSpriteEditor>("Sprite Editor");
        }
        
        private void OnEnable()
        {
            RefreshSpriteList();
            
            // Register for scene GUI events to handle sprite placement
            SceneView.duringSceneGui += OnSceneGUI;
            
            // Set last refresh time to avoid rapid refreshes
            m_LastRefreshTime = Time.realtimeSinceStartup;
        }
        
        private void OnDisable()
        {
            // Unregister scene GUI events
            SceneView.duringSceneGui -= OnSceneGUI;
        }
        
        private void OnSceneGUI(SceneView sceneView)
        {
            if (m_SelectedSprite == null) return;
            
            // Handle keyboard shortcuts
            HandleKeyboardInput();
            
            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && !e.alt && !e.shift && !e.control)
            {
                // Convert mouse position to world position
                Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                Vector3 mousePosition = ray.origin;
                mousePosition.z = 0;
                
                if (m_CurrentMode == PlacementMode.Single)
                {
                    // Place single sprite at click position
                    PlaceSpriteAtPosition(mousePosition);
                    e.Use();
                }
                else if (m_CurrentMode == PlacementMode.TilePattern)
                {
                    // Place a pattern of sprites
                    PlaceSpritePattern(mousePosition);
                    e.Use();
                }
            }
            
            // Preview sprite at mouse position
            if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag)
            {
                SceneView.RepaintAll();
            }
            
            // Draw preview sprite at mouse position
            if (e.type == EventType.Repaint)
            {
                Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                Vector3 mousePosition = ray.origin;
                mousePosition.z = 0;
                
                // Round position to grid
                mousePosition = RoundToGrid(mousePosition);
                
                // Draw preview sprite
                Handles.color = new Color(1, 1, 1, 0.5f);
                
                if (m_CurrentMode == PlacementMode.Single)
                {
                    // Draw single sprite preview
                    DrawSpritePreview(mousePosition);
                }
                else if (m_CurrentMode == PlacementMode.TilePattern)
                {
                    // Draw pattern preview
                    DrawPatternPreview(mousePosition);
                }
            }
        }
        
        private void OnGUI()
        {
            EditorGUILayout.BeginVertical();
            
            // Title and info
            EditorGUILayout.LabelField("Sprite Editor", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Select and place sprites on the map. Click in the Scene view to place sprites.", MessageType.Info);
            
            EditorGUILayout.Space(10);
            
            // Search bar
            EditorGUILayout.BeginHorizontal();
            string newSearchQuery = EditorGUILayout.TextField("Search", m_SearchQuery);
            if (newSearchQuery != m_SearchQuery)
            {
                m_SearchQuery = newSearchQuery;
                RefreshSpriteList();
            }
            
            // Refresh button
            if (GUILayout.Button("Refresh", GUILayout.Width(80)))
            {
                RefreshSpriteList(true);
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(5);
            
            // Placement mode selection
            m_CurrentMode = (PlacementMode)EditorGUILayout.EnumPopup("Placement Mode", m_CurrentMode);
            
            EditorGUILayout.Space(10);
            
            // Sprite settings
            EditorGUILayout.LabelField("Sprite Settings", EditorStyles.boldLabel);
            
            m_SortingOrder = EditorGUILayout.IntField("Sorting Order", m_SortingOrder);
            m_SpriteColor = EditorGUILayout.ColorField("Color", m_SpriteColor);
            
            // Advanced settings toggle
            m_ShowAdvancedOptions = EditorGUILayout.Foldout(m_ShowAdvancedOptions, "Advanced Options");
            if (m_ShowAdvancedOptions)
            {
                EditorGUI.indentLevel++;
                
                m_CreateAsFixture = EditorGUILayout.Toggle("Create as Fixture", m_CreateAsFixture);
                
                if (m_CreateAsFixture)
                {
                    m_Scale = EditorGUILayout.Vector2Field("Scale", m_Scale);
                    m_Rotation = EditorGUILayout.Slider("Rotation", m_Rotation, 0f, 360f);
                }
                else
                {
                    m_Scale = new Vector2(
                        EditorGUILayout.FloatField("Scale", m_Scale.x),
                        m_Scale.x
                    );
                }
                
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.Space(20);
            
            // Available sprites list
            EditorGUILayout.LabelField("Available Sprites", EditorStyles.boldLabel);
            
            // Display sprite count
            EditorGUILayout.LabelField($"Found {m_AvailableSprites.Count} sprites", EditorStyles.miniLabel);
            
            // Scroll view for sprites
            m_ScrollPosition = EditorGUILayout.BeginScrollView(m_ScrollPosition);
            
            // Grid size for sprite preview
            const int gridSize = 80;
            const int padding = 5;
            
            // Calculate how many sprites can fit in one row
            int thumbsPerRow = Mathf.Max(1, Mathf.FloorToInt((EditorGUIUtility.currentViewWidth - 30) / gridSize));
            
            // Draw sprites in a grid
            int columnCount = 0;
            EditorGUILayout.BeginHorizontal();
            
            foreach (Sprite sprite in m_AvailableSprites)
            {
                // Start a new row if needed
                if (columnCount >= thumbsPerRow)
                {
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                    columnCount = 0;
                }
                
                // Sprite button
                EditorGUILayout.BeginVertical(GUILayout.Width(gridSize));
                
                // Highlight selected sprite
                bool isSelected = m_SelectedSprite == sprite;
                GUI.backgroundColor = isSelected ? Color.cyan : Color.white;
                
                // Create button with sprite preview
                Rect buttonRect = GUILayoutUtility.GetRect(gridSize - padding * 2, gridSize - padding * 2);
                if (GUI.Button(buttonRect, ""))
                {
                    m_SelectedSprite = sprite;
                }
                
                // Draw sprite preview
                if (sprite != null)
                {
                    Rect spriteRect = new Rect(
                        buttonRect.x + padding,
                        buttonRect.y + padding,
                        buttonRect.width - padding * 2,
                        buttonRect.height - padding * 2
                    );
                    
                    GUI.DrawTextureWithTexCoords(
                        spriteRect,
                        sprite.texture,
                        new Rect(
                            sprite.textureRect.x / sprite.texture.width,
                            sprite.textureRect.y / sprite.texture.height,
                            sprite.textureRect.width / sprite.texture.width,
                            sprite.textureRect.height / sprite.texture.height
                        )
                    );
                    
                    // Display sprite name below
                    string displayName = sprite.name;
                    if (displayName.Length > 10)
                    {
                        displayName = displayName.Substring(0, 8) + "..";
                    }
                    
                    EditorGUILayout.LabelField(displayName, EditorStyles.centeredGreyMiniLabel);
                }
                
                // Reset color
                GUI.backgroundColor = Color.white;
                
                EditorGUILayout.EndVertical();
                
                columnCount++;
            }
            
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();
            
            EditorGUILayout.EndVertical();
        }
        
        private void RefreshSpriteList(bool forceRefresh = false)
        {
            // Avoid refreshing too frequently
            if (!forceRefresh && Time.realtimeSinceStartup - m_LastRefreshTime < 1.0f)
            {
                return;
            }
            
            m_LastRefreshTime = Time.realtimeSinceStartup;
            m_AvailableSprites.Clear();
            
            // Find all sprites matching search query
            string[] guids;
            
            if (string.IsNullOrEmpty(m_SearchQuery))
            {
                guids = AssetDatabase.FindAssets("t:Sprite", new[] { "Assets/CreatorMap/Content/Sprites" });
            }
            else
            {
                guids = AssetDatabase.FindAssets($"t:Sprite {m_SearchQuery}", new[] { "Assets/CreatorMap/Content/Sprites" });
            }
            
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                
                if (sprite != null)
                {
                    m_AvailableSprites.Add(sprite);
                }
            }
        }
        
        private void PlaceSpriteAtPosition(Vector3 position)
        {
            if (m_SelectedSprite == null) return;
            
            // Round to grid
            position = RoundToGrid(position);
            
            // Create game object
            string goName = m_CreateAsFixture ? $"Fixture_{m_SelectedSprite.name}" : $"Tile_{m_SelectedSprite.name}";
            GameObject spriteObject = new GameObject(goName);
            spriteObject.transform.position = position;
            
            if (m_CreateAsFixture)
            {
                spriteObject.transform.localScale = m_Scale;
                spriteObject.transform.rotation = Quaternion.Euler(0, 0, m_Rotation);
            }
            else
            {
                spriteObject.transform.localScale = new Vector3(m_Scale.x, m_Scale.x, 1);
            }
            
            // Add sprite renderer
            SpriteRenderer renderer = spriteObject.AddComponent<SpriteRenderer>();
            renderer.sprite = m_SelectedSprite;
            renderer.sortingOrder = m_SortingOrder;
            renderer.color = m_SpriteColor;
            
            // Register undo
            Undo.RegisterCreatedObjectUndo(spriteObject, "Place Sprite");
            
            // Select the created game object
            Selection.activeGameObject = spriteObject;
        }
        
        private void PlaceSpritePattern(Vector3 position)
        {
            if (m_SelectedSprite == null) return;
            
            // Round to grid
            position = RoundToGrid(position);
            
            // Create a parent object for the pattern
            GameObject patternParent = new GameObject($"Pattern_{m_SelectedSprite.name}");
            patternParent.transform.position = position;
            
            // Place a 3x3 pattern
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    Vector3 offset = new Vector3(x, y, 0);
                    
                    // Create game object for each tile
                    GameObject spriteObject = new GameObject($"Tile_{m_SelectedSprite.name}_{x}_{y}");
                    spriteObject.transform.parent = patternParent.transform;
                    spriteObject.transform.localPosition = offset;
                    spriteObject.transform.localScale = new Vector3(m_Scale.x, m_Scale.x, 1);
                    
                    // Add sprite renderer
                    SpriteRenderer renderer = spriteObject.AddComponent<SpriteRenderer>();
                    renderer.sprite = m_SelectedSprite;
                    renderer.sortingOrder = m_SortingOrder;
                    renderer.color = m_SpriteColor;
                }
            }
            
            // Register undo
            Undo.RegisterCreatedObjectUndo(patternParent, "Place Sprite Pattern");
            
            // Select the created pattern
            Selection.activeGameObject = patternParent;
        }
        
        private void DrawSpritePreview(Vector3 position)
        {
            if (m_SelectedSprite == null) return;
            
            // Calculate sprite bounds
            Vector2 spriteSize = m_SelectedSprite.bounds.size;
            
            // Draw outline
            if (m_CreateAsFixture)
            {
                Matrix4x4 rotationMatrix = Matrix4x4.TRS(
                    position,
                    Quaternion.Euler(0, 0, m_Rotation),
                    new Vector3(m_Scale.x, m_Scale.y, 1)
                );
                
                Handles.matrix = rotationMatrix;
                
                Vector3 topLeft = new Vector3(-spriteSize.x / 2, spriteSize.y / 2, 0);
                Vector3 topRight = new Vector3(spriteSize.x / 2, spriteSize.y / 2, 0);
                Vector3 bottomRight = new Vector3(spriteSize.x / 2, -spriteSize.y / 2, 0);
                Vector3 bottomLeft = new Vector3(-spriteSize.x / 2, -spriteSize.y / 2, 0);
                
                Handles.DrawLine(topLeft, topRight);
                Handles.DrawLine(topRight, bottomRight);
                Handles.DrawLine(bottomRight, bottomLeft);
                Handles.DrawLine(bottomLeft, topLeft);
                
                Handles.matrix = Matrix4x4.identity;
            }
            else
            {
                float scale = m_Scale.x;
                
                Vector3 topLeft = position + new Vector3(-spriteSize.x * scale / 2, spriteSize.y * scale / 2, 0);
                Vector3 topRight = position + new Vector3(spriteSize.x * scale / 2, spriteSize.y * scale / 2, 0);
                Vector3 bottomRight = position + new Vector3(spriteSize.x * scale / 2, -spriteSize.y * scale / 2, 0);
                Vector3 bottomLeft = position + new Vector3(-spriteSize.x * scale / 2, -spriteSize.y * scale / 2, 0);
                
                Handles.DrawLine(topLeft, topRight);
                Handles.DrawLine(topRight, bottomRight);
                Handles.DrawLine(bottomRight, bottomLeft);
                Handles.DrawLine(bottomLeft, topLeft);
            }
        }
        
        private void DrawPatternPreview(Vector3 position)
        {
            if (m_SelectedSprite == null) return;
            
            // Draw a 3x3 grid preview
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    Vector3 tilePosition = position + new Vector3(x, y, 0);
                    DrawSpritePreview(tilePosition);
                }
            }
        }
        
        private Vector3 RoundToGrid(Vector3 position)
        {
            // Round to nearest 0.5 units
            position.x = Mathf.Round(position.x * 2) / 2;
            position.y = Mathf.Round(position.y * 2) / 2;
            return position;
        }
        
        private void HandleKeyboardInput()
        {
            Event e = Event.current;
            
            if (e.type == EventType.KeyDown)
            {
                bool handled = false;
                
                // Page up/down adjusts sorting order
                if (e.keyCode == KeyCode.PageUp)
                {
                    m_SortingOrder++;
                    handled = true;
                }
                else if (e.keyCode == KeyCode.PageDown)
                {
                    m_SortingOrder--;
                    handled = true;
                }
                // [ and ] adjust scale
                else if (e.keyCode == KeyCode.LeftBracket)
                {
                    m_Scale = new Vector2(
                        Mathf.Max(0.1f, m_Scale.x - 0.1f),
                        m_CreateAsFixture ? Mathf.Max(0.1f, m_Scale.y - 0.1f) : m_Scale.y
                    );
                    handled = true;
                }
                else if (e.keyCode == KeyCode.RightBracket)
                {
                    m_Scale = new Vector2(
                        m_Scale.x + 0.1f,
                        m_CreateAsFixture ? m_Scale.y + 0.1f : m_Scale.y
                    );
                    handled = true;
                }
                // Rotation with < and >
                else if (e.keyCode == KeyCode.Comma && m_CreateAsFixture)
                {
                    m_Rotation = (m_Rotation - 15f) % 360f;
                    if (m_Rotation < 0) m_Rotation += 360f;
                    handled = true;
                }
                else if (e.keyCode == KeyCode.Period && m_CreateAsFixture)
                {
                    m_Rotation = (m_Rotation + 15f) % 360f;
                    handled = true;
                }
                // F toggles fixture mode
                else if (e.keyCode == KeyCode.F)
                {
                    m_CreateAsFixture = !m_CreateAsFixture;
                    handled = true;
                }
                // Numeric keys 1-9 select sprites from the list
                else if (e.keyCode >= KeyCode.Alpha1 && e.keyCode <= KeyCode.Alpha9)
                {
                    int index = (int)e.keyCode - (int)KeyCode.Alpha1;
                    if (index < m_AvailableSprites.Count)
                    {
                        m_SelectedSprite = m_AvailableSprites[index];
                        handled = true;
                    }
                }
                
                if (handled)
                {
                    e.Use();
                    Repaint();
                    SceneView.RepaintAll();
                }
            }
        }
    }
}
#endif 