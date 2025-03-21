#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System;
using System.Linq;
using CreatorMap.Scripts;
using CreatorMap.Scripts.Data;
using Managers.Maps.MapCreator;
using Components.Maps;
// UPDATED: More explicit reference to NewMapCreatorWindow
using NewMapCreatorWindowType = MapCreator.Editor.NewMapCreatorWindow;
// Adding a direct reference to MapCreatorWindowHelper
using static MapCreator.Editor.MapCreatorWindowHelper;

namespace MapCreator.Editor
{
    // This class handles scene GUI interactions for the Map Creator
    public class MapCreatorSceneGUI
    {
        private static Color s_HighlightColor = new Color(1f, 0.92f, 0.016f, 0.8f); // Bright yellow with 80% opacity
        private static Color s_NonWalkableColor = new Color(0.8f, 0.2f, 0.2f, 0.5f); // Red with 50% opacity
        private static Color s_WalkableColor = new Color(1f, 1f, 1f, 0.12f); // Default walkable color
        
        private static CellComponent s_LastHighlightedCell;
        private static Dictionary<CellComponent, Material> s_OriginalMaterials = new Dictionary<CellComponent, Material>();
        
        // Dictionnaire pour stocker les points d'origine du LineRenderer de chaque cellule
        private static Dictionary<CellComponent, Vector3[]> s_OriginalPoints = new Dictionary<CellComponent, Vector3[]>();
        
        // Texture pour l'icône de blé pour les cellules de ferme
        private static Texture2D s_BleTexture;
        
        // Texture pour l'icône de l'œil fermé pour les cellules bloquant la ligne de vue
        private static Texture2D s_ClosedEyeTexture;

        [InitializeOnLoadMethod]
        static void Initialize()
        {
            // Force recompilation by changing this comment
            Debug.Log("MapCreatorSceneGUI initialized - using NewMapCreatorWindowType");
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
            
            // Charger les textures
            LoadTextures();
        }
        
        // Méthode pour charger les textures
        private static void LoadTextures()
        {
            // Charger la texture de blé
            s_BleTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/CreatorMap/Editor/Icons/ble.png");
            if (s_BleTexture == null)
            {
                Debug.LogWarning("Texture ble.png not found at Assets/CreatorMap/Editor/Icons/ble.png");
            }
            else
            {
                Debug.Log("Successfully loaded ble.png from Icons folder");
            }
            
            // Charger la texture de l'œil fermé
            s_ClosedEyeTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/CreatorMap/Editor/Icons/eye_13949475.png");
            if (s_ClosedEyeTexture == null)
            {
                Debug.LogWarning("Texture eye_13949475.png not found at Assets/CreatorMap/Editor/Icons/eye_13949475.png");
            }
            else
            {
                Debug.Log("Successfully loaded eye_13949475.png from Icons folder");
            }
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            var window = NewMapCreatorWindowType.Instance;
            if (window == null)
                return;
                
            var currentMode = window.GetCurrentDrawMode();
            
            // Show mode status overlay in Scene view
            Handles.BeginGUI();
            
            // Create a background box for better visibility
            var boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.normal.background = MakeTexture(200, 80, 
                currentMode != NewMapCreatorWindowType.DrawMode.None ? 
                new Color(0.2f, 0.8f, 0.2f, 0.8f) : 
                new Color(0.5f, 0.5f, 0.5f, 0.8f));
            boxStyle.normal.textColor = Color.white;
            boxStyle.fontStyle = FontStyle.Bold;
            boxStyle.alignment = TextAnchor.MiddleCenter;
            boxStyle.fontSize = 12;
            
            GUI.Box(new Rect(10, 10, 220, 50), 
                currentMode != NewMapCreatorWindowType.DrawMode.None ? 
                $"Drawing Enabled: {currentMode}" : 
                "Drawing Disabled", 
                boxStyle);
                
            // Show additional info for clipping status when in TilePlacement mode
            if (currentMode == NewMapCreatorWindowType.DrawMode.TilePlacement && window.UseClipping() == false)
            {
                GUI.Box(new Rect(10, 70, 220, 30), 
                    "Free Placement Mode", 
                    boxStyle);
            }
            
            // Afficher la légende des indicateurs si elle est activée
            if (window.ShowPropertyIndicators())
            {
                DrawIndicatorLegend(sceneView);
            }
                
            Handles.EndGUI();
            
            // Dessiner les indicateurs de propriétés pour toutes les cellules
            DrawCellPropertyIndicators();
            
            // Skip processing if drawing is disabled
            if (currentMode == NewMapCreatorWindowType.DrawMode.None)
                return;
            
            // Cache the current event
            Event e = Event.current;
            int controlId = GUIUtility.GetControlID(FocusType.Passive);
            
            if (e == null) return;
            
            // Draw the preview at mouse position regardless of event type
            if (currentMode == NewMapCreatorWindowType.DrawMode.TilePlacement)
            {
                DrawTilePreviewAtMouse(e);
            }
            
            // Process different events
            switch (e.type)
            {
                case EventType.MouseMove:
                    HandleMouseHover(e);
                    e.Use();
                    break;
                    
                case EventType.MouseDrag:
                    HandleMouseHover(e);
                    if (e.button == 0) // Left mouse button
                    {
                        HandleMouseClick(e);
                    }
                    else if (e.button == 1) // Right mouse button - erase
                    {
                        // Use eraser with right click regardless of current mode
                        Vector3 erasePosition = HandleUtility.GUIPointToWorldRay(e.mousePosition).origin;
                        erasePosition.z = 0; // Ensure z is 0 for 2D
                        EraseTileAtPosition(erasePosition);
                    }
                    e.Use();
                    break;
                    
                case EventType.MouseDown:
                    if (e.button == 0) // Left mouse button
                    {
                        HandleMouseClick(e);
                    }
                    else if (e.button == 1) // Right mouse button - erase
                    {
                        // Use eraser with right click regardless of current mode
                        Vector3 erasePosition = HandleUtility.GUIPointToWorldRay(e.mousePosition).origin;
                        erasePosition.z = 0; // Ensure z is 0 for 2D
                        EraseTileAtPosition(erasePosition);
                    }
                    e.Use();
                    break;
                    
                case EventType.MouseLeaveWindow:
                    ClearHighlight();
                    break;
            }

            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        }

        private static void HandleMouseHover(Event e)
        {
            // Clear any existing highlight
            ClearHighlight();

            // Cast ray to find object under mouse
            var mousePos = e.mousePosition;
            var ray = HandleUtility.GUIPointToWorldRay(mousePos);
            Vector3 mouseWorldPos = ray.origin;
            mouseWorldPos.z = 0; // Ensure z is 0 for 2D
            
            // First attempt: raycast to find cells with collider
            var hits = Physics2D.RaycastAll(mouseWorldPos, Vector2.zero, 0.1f);
            
            CellComponent foundCell = null;
            
            // Find first valid cell with collider
            foreach (var hit in hits)
            {
                if (hit.collider == null) continue;
                
                var cell = hit.collider.GetComponent<CellComponent>();
                if (cell != null && cell.Cell != null && IsValidCell(cell))
                {
                    foundCell = cell;
                    break;
                }
            }
            
            // If no cell found through raycast, try finding the closest cell
            if (foundCell == null)
            {
                // Get all cells in the scene
                CellComponent[] allCells = GameObject.FindObjectsOfType<CellComponent>();
                
                // Distance threshold to consider a cell being under the mouse
                float minDistance = 0.5f;
                float closestDistance = float.MaxValue;
                
                foreach (var cell in allCells)
                {
                    if (cell == null || cell.Cell == null || !IsValidCell(cell)) continue;
                    
                    // Get the cell center position
                    Vector3 cellCenter = GetCellCenterPosition(cell);
                    
                    // Calculate distance to mouse position (in XY plane)
                    float distance = Vector2.Distance(
                        new Vector2(mouseWorldPos.x, mouseWorldPos.y),
                        new Vector2(cellCenter.x, cellCenter.y)
                    );
                    
                    // If this cell is closer than any previous cell and within threshold
                    if (distance < minDistance && distance < closestDistance)
                    {
                        closestDistance = distance;
                        foundCell = cell;
                    }
                }
            }
            
            // If a valid cell was found, highlight it
            if (foundCell != null)
            {
                HighlightCell(foundCell);
            }
            
            // Force repaint to update visuals
            SceneView.RepaintAll();
        }

        private static void HandleMouseClick(Event e)
        {
            // Get currently highlighted cell
            CellComponent cell = s_LastHighlightedCell;
            if (cell == null) return;
            
            // Get window instance and check current mode
            var window = NewMapCreatorWindowType.Instance;
            if (window == null) return;
            
            // Get current draw mode
            var drawMode = window.GetCurrentDrawMode();
            
            switch (drawMode)
            {
                case NewMapCreatorWindowType.DrawMode.CellProperties:
                    ApplyCellProperties(cell);
                    break;
                    
                case NewMapCreatorWindowType.DrawMode.TilePlacement:
                    // Check if eraser mode is enabled
                    if (window.IsEraserMode())
                    {
                        // Get mouse position for erasing
                        Vector3 erasePosition = HandleUtility.GUIPointToWorldRay(e.mousePosition).origin;
                        erasePosition.z = 0; // Ensure z is 0 for 2D
                        
                        // Erase tiles at the clicked position
                        EraseTileAtPosition(erasePosition);
                    }
                    else
                    {
                        // Normal tile placement mode
                        // Get the currently selected tile
                        var selectedTile = window.GetSelectedTile();
                        if (selectedTile != null)
                        {
                            // Check if we're using clipping or free placement
                            if (window.UseClipping())
                            {
                                // Normal cell-based placement
                                PlaceTileAtCell(cell, selectedTile);
                            }
                            else
                            {
                                // Free placement mode - use mouse position directly
                                Vector3 mousePosition = HandleUtility.GUIPointToWorldRay(e.mousePosition).origin;
                                mousePosition.z = 0; // Ensure z is 0 for 2D
                                
                                // Create a new position vector just for the position
                                Vector3 positionOnly = new Vector3(mousePosition.x, mousePosition.y, 0);
                                
                                // Place tile at the exact mouse position
                                PlaceTileAtPosition(positionOnly, selectedTile);
                            }
                        }
                    }
                    break;
            }
        }
        
        private static void ApplyCellProperties(CellComponent cell)
        {
            if (cell == null || cell.Cell == null) return;

            Undo.RecordObject(cell.gameObject, "Change Cell Properties");

            // Get cell properties from window
            var window = NewMapCreatorWindowType.Instance;
            if (window == null) return;
            
            // Vérifier si on doit réinitialiser la cellule
            bool resetCell = window.ShouldResetCell();
            
            // Get all properties set in the UI
            ushort cellProperties = window.GetCellProperties();
                
            // Apply properties directly
            cell.Cell.Data = (short)cellProperties;
            
            if (resetCell)
            {
                Debug.Log($"Reset cell {cell.name} - Cell set to walkable with no other properties");
            }
            else
            {
                Debug.Log($"Applied cell properties to {cell.name} - Data: {cell.Cell.Data} (IsWalkable: {cell.Cell.IsWalkable})");
            }

            // Update cell visual state based on walkability
            if (cell.Cell.IsWalkable)
            {
                RestoreWalkableCell(cell);
            }
            else
            {
                SetupNonWalkableCell(cell);
            }

            // If this was our highlighted cell, re-highlight it
            if (s_LastHighlightedCell == cell)
            {
                HighlightCell(cell);
            }
            
            // Force repaint to see changes immediately
            SceneView.RepaintAll();
        }

        private static void SetCellWalkable(CellComponent cell, bool walkable)
        {
            if (cell == null || cell.Cell == null) return;

            Undo.RecordObject(cell.gameObject, "Change Cell Properties");

            // Récupérer les propriétés depuis la fenêtre
            var window = NewMapCreatorWindowType.Instance;
            ushort additionalProperties = window != null ? window.GetCellProperties() : (ushort)0;
            
            // Mettre à jour les données de la cellule
            // Bit 0 détermine si c'est walkable (0) ou non (1)
            ushort cellData = additionalProperties;
            if (!walkable)
                cellData |= 0x0001; // Ajouter le bit 0 pour non-walkable
                
            cell.Cell.Data = (short)cellData;
            
            Debug.Log($"Cell {cell.name} data set to {cell.Cell.Data} (IsWalkable: {cell.Cell.IsWalkable})");

            if (walkable)
            {
                // Restore walkable configuration
                Debug.Log($"Restoring walkable configuration for cell {cell.name}");
                RestoreWalkableCell(cell);
            }
            else
            {
                // Set up non-walkable configuration
                Debug.Log($"Setting up non-walkable configuration for cell {cell.name}");
                SetupNonWalkableCell(cell);
            }

            // If this was our highlighted cell, re-highlight it
            if (s_LastHighlightedCell == cell)
            {
                HighlightCell(cell);
            }
            
            // Force repaint to see changes immediately
            SceneView.RepaintAll();
        }
        
        private static void RestoreWalkableCell(CellComponent cell)
        {
            if (cell == null) return;
            
            Debug.Log($"RestoreWalkableCell: Starting restoration of parent cell {cell.name}");
            
            // Mise à jour du statut de la cellule parent
            if (cell.Cell != null)
            {
                bool wasWalkable = cell.Cell.IsWalkable;
                // Préserver les autres bits et modifier uniquement le bit walkable (bit 0)
                // Pour rendre walkable: mettre le bit 0 à 0 (en utilisant un AND avec ~0x0001)
                cell.Cell.Data = (short)(cell.Cell.Data & ~0x0001);
                Debug.Log($"RestoreWalkableCell: Parent cell data changed from {(wasWalkable ? "walkable" : "non-walkable")} to walkable - Data: {cell.Cell.Data}");
            }
            
            // Vérifier si la cellule est toujours non-walkable during RP
            bool isNonWalkableDuringRP = (cell.Cell.Data & 0x0004) != 0; // Bit 2 = 1
            
            // Restaurer l'état du collider pour les cellules walkable
            var collider = cell.GetComponent<PolygonCollider2D>();
            if (collider == null)
            {
                // Si le collider a été supprimé (lors du passage à non-walkable), on le recrée
                Debug.Log($"RestoreWalkableCell: Creating new polygon collider for parent cell");
                collider = cell.gameObject.AddComponent<PolygonCollider2D>();
            }
            
            // S'assurer que le collider est activé
            collider.enabled = true;
            Debug.Log($"RestoreWalkableCell: Enabled polygon collider on parent cell");
            
            // Restaurer la forme d'origine du LineRenderer si on l'a stockée 
            // et que la cellule n'est plus non-walkable during RP
            var lineRenderer = cell.GetComponent<LineRenderer>();
            if (lineRenderer != null && !isNonWalkableDuringRP)
            {
                // Toujours définir la couche de tri sur "UI" avec un ordre élevé
                lineRenderer.sortingLayerName = "UI";
                lineRenderer.sortingOrder = 32700;
                
                // Vérifier si on a stocké les points d'origine pour cette cellule
                if (s_OriginalPoints.TryGetValue(cell, out Vector3[] originalPoints) && originalPoints.Length > 0)
                {
                    // Restaurer les points d'origine
                    lineRenderer.positionCount = originalPoints.Length;
                    for (int i = 0; i < originalPoints.Length; i++)
                    {
                        lineRenderer.SetPosition(i, originalPoints[i]);
                    }
                    
                    // Mettre à jour les points du collider
                    if (collider != null && originalPoints.Length >= 4) // Au moins 4 points pour un losange
                    {
                        try
                        {
                            // IMPORTANT: Les points du collider doivent être relatifs à la position de l'objet 
                            // (en espace local) et non en coordonnées mondiales
                            Vector2[] colliderPoints = new Vector2[4]; // On utilise seulement les 4 premiers points car le 5ème est identique au premier pour fermer la boucle
                            
                            // Conversion des points du LineRenderer (coordonnées monde) vers coordonnées locales pour le collider
                            colliderPoints[0] = new Vector2(0, 0); // Bottom left corner
                            colliderPoints[1] = new Vector2(
                                originalPoints[1].x - cell.transform.position.x, 
                                originalPoints[1].y - cell.transform.position.y); // Top left corner
                            colliderPoints[2] = new Vector2(
                                originalPoints[2].x - cell.transform.position.x, 
                                originalPoints[2].y - cell.transform.position.y); // Top right corner
                            colliderPoints[3] = new Vector2(
                                originalPoints[3].x - cell.transform.position.x, 
                                originalPoints[3].y - cell.transform.position.y); // Bottom right corner
                            
                            collider.SetPath(0, colliderPoints);
                            Debug.Log($"RestoreWalkableCell: Set collider with points: {string.Join(", ", colliderPoints)}");
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"Error setting collider points: {ex.Message}");
                        }
                    }
                    
                    Debug.Log($"RestoreWalkableCell: Restored original LineRenderer shape with {originalPoints.Length} points");
                    
                    // Supprimer les points stockés car on n'en a plus besoin
                    // mais seulement si la cellule n'est plus non-walkable during RP
                    if (!isNonWalkableDuringRP)
                    {
                        s_OriginalPoints.Remove(cell);
                    }
                }
                else
                {
                    Debug.LogWarning($"RestoreWalkableCell: No original points found for cell {cell.name}, cannot restore LineRenderer shape properly");
                }
            }
            
            Debug.Log($"RestoreWalkableCell: Completed restoration of parent cell {cell.name} to walkable state");
        }
        
        private static void SetupNonWalkableCell(CellComponent cell)
        {
            if (cell == null) return;
            
            Debug.Log($"SetupNonWalkableCell: Setting up parent cell {cell.name} as non-walkable");
            
            // Mise à jour du statut de la cellule parent
            if (cell.Cell != null)
            {
                // Préserver les autres bits et modifier uniquement le bit walkable (bit 0)
                // Pour rendre non-walkable: mettre le bit 0 à 1 (en utilisant un OR avec 0x0001)
                cell.Cell.Data = (short)(cell.Cell.Data | 0x0001);
                Debug.Log($"SetupNonWalkableCell: Parent cell data set to non-walkable - Data: {cell.Cell.Data}");
            }
            
            // Vérifier si la cellule est non-walkable OU non-walkable during RP
            // Bit 0 = 1 pour non-walkable, Bit 2 = 1 pour non-walkable during RP
            bool isNonWalkable = !cell.Cell.IsWalkable; // Bit 0 = 1
            bool isNonWalkableDuringRP = (cell.Cell.Data & 0x0004) != 0; // Bit 2 = 1
            
            // Stocker les points d'origine du LineRenderer avant de les modifier
            var lineRenderer = cell.GetComponent<LineRenderer>();
            if (lineRenderer != null)
            {
                // Toujours définir la couche de tri sur "UI" avec un ordre élevé
                lineRenderer.sortingLayerName = "UI";
                lineRenderer.sortingOrder = 32700;
                
                // Stocker les points actuels pour pouvoir les restaurer plus tard
                int pointCount = lineRenderer.positionCount;
                if (pointCount > 0 && !s_OriginalPoints.ContainsKey(cell))
                {
                    Vector3[] points = new Vector3[pointCount];
                    for (int i = 0; i < pointCount; i++)
                    {
                        points[i] = lineRenderer.GetPosition(i);
                    }
                    s_OriginalPoints[cell] = points;
                    Debug.Log($"SetupNonWalkableCell: Stored original LineRenderer shape with {pointCount} points");
                }
                
                // Modifier le LineRenderer seulement pour les cellules non-walkable OU non-walkable during RP
                if (isNonWalkable || isNonWalkableDuringRP)
                {
                    // Pour les cellules non-walkable ou non-walkable during RP, configurer le LineRenderer avec exactement 2 points
                    lineRenderer.positionCount = 2;
                    lineRenderer.SetPosition(0, new Vector3(0, 0, 0)); // Premier point à (0,0,0)
                    lineRenderer.SetPosition(1, new Vector3(0, 0, 1)); // Deuxième point à (0,0,1)
                    
                    // Keep original color (same as walkable)
                    Material transparentMaterial = new Material(Shader.Find("Sprites/Default"));
                    transparentMaterial.color = s_WalkableColor;
                    lineRenderer.material = transparentMaterial;
                    lineRenderer.startColor = new Color(1, 1, 1, 1f);
                    lineRenderer.endColor = new Color(1, 1, 1, 1f);
                    
                    Debug.Log($"SetupNonWalkableCell: Set LineRenderer to have exactly two points: (0,0,0) and (0,0,1)");
                }
            }
            
            // Retirer le collider pour les cellules non-walkable
            var collider = cell.GetComponent<PolygonCollider2D>();
            if (collider != null)
            {
                UnityEngine.Object.DestroyImmediate(collider);
                Debug.Log($"SetupNonWalkableCell: Removed polygon collider from parent cell");
            }
            
            Debug.Log($"SetupNonWalkableCell: Completed setup of parent cell {cell.name} as non-walkable");
        }
        
        private static void UpdateCellVisual(CellComponent cell)
        {
            // This method is simplified and no longer used
            // Keeping it for compatibility, but redirecting to SetCellWalkable
            if (cell == null || cell.Cell == null) return;
            
            bool isWalkable = cell.Cell.IsWalkable;
            SetCellWalkable(cell, isWalkable);
        }
        
        private static void SetupNonWalkableLineRenderer(CellComponent cell)
        {
            if (cell == null) return;
            
            var lineRenderer = cell.GetComponent<LineRenderer>();
            if (lineRenderer == null) return;
            
            // Vérifier si la cellule est non-walkable ou non-walkable during RP
            // Seules ces deux propriétés nécessitent un LineRenderer à 2 points
            bool isNonWalkable = !cell.Cell.IsWalkable; // Bit 0 = 1
            bool isNonWalkableDuringRP = (cell.Cell.Data & 0x0004) != 0; // Bit 2 = 1 (CELL_NON_WALKABLE_RP)
            
            // Appliquer les 2 indices uniquement aux cellules non-walkable ou non-walkable during RP
            // Les cellules Line of Sight (bit 3) gardent leur LineRenderer normal
            if (isNonWalkable || isNonWalkableDuringRP)
            {
                // Stocker les points originaux si pas déjà fait
                if (!s_OriginalPoints.ContainsKey(cell))
                {
                    int pointCount = lineRenderer.positionCount;
                    Vector3[] points = new Vector3[pointCount];
                    lineRenderer.GetPositions(points);
                    s_OriginalPoints[cell] = points;
                    Debug.Log($"SetupNonWalkableLineRenderer: Stored original LineRenderer shape with {pointCount} points");
                }
                
                // Définir un LineRenderer "invisible" avec juste 2 points
                lineRenderer.positionCount = 2;
                lineRenderer.SetPosition(0, new Vector3(0, 0, 0));
                lineRenderer.SetPosition(1, new Vector3(0, 0, 1));
                Debug.Log($"SetupNonWalkableLineRenderer: Set LineRenderer to have exactly two points for non-walkable cell");
            }
        }
        
        private static void RemoveNonWalkableComponents(CellComponent cell)
        {
            // Clean up any previously added components
            if (cell == null) return;
            
            // Remove MeshRenderer if it exists
            MeshRenderer meshRenderer = cell.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                UnityEngine.Object.DestroyImmediate(meshRenderer);
            }
            
            // Remove MeshFilter if it exists
            MeshFilter meshFilter = cell.GetComponent<MeshFilter>();
            if (meshFilter != null)
            {
                UnityEngine.Object.DestroyImmediate(meshFilter);
            }
        }

        private static void AddBackgroundToNonWalkableCell(CellComponent cell)
        {
            // This method is no longer needed
            // We're not adding any background to non-walkable cells
        }

        private static void HighlightCell(CellComponent cell)
        {
            if (cell == null) return;
            
            // Store the cell being highlighted
            s_LastHighlightedCell = cell;
            
            // Save the original material if we haven't already
            var lineRenderer = cell.GetComponent<LineRenderer>();
            if (lineRenderer != null)
            {
                // Store original material if not already stored
                if (!s_OriginalMaterials.ContainsKey(cell) && lineRenderer.sharedMaterial != null)
                {
                    s_OriginalMaterials.Add(cell, lineRenderer.sharedMaterial);
                }
                
                // Create highlight material
                Material highlightMaterial = new Material(Shader.Find("Sprites/Default"));
                highlightMaterial.color = s_HighlightColor;
                
                // Apply highlight
                lineRenderer.sharedMaterial = highlightMaterial;
            }
        }

        private static void ClearHighlight()
        {
            if (s_LastHighlightedCell != null)
            {
                var lineRenderer = s_LastHighlightedCell.GetComponent<LineRenderer>();
                if (lineRenderer != null)
                {
                    // Restore original material if we have it stored
                    if (s_OriginalMaterials.TryGetValue(s_LastHighlightedCell, out Material originalMaterial))
                    {
                        lineRenderer.sharedMaterial = originalMaterial;
                    }
                    else
                    {
                        // Create a default material with the walkable color (non-walkable cells also use the same color)
                        Material newMaterial = new Material(Shader.Find("Sprites/Default"));
                        newMaterial.color = s_WalkableColor;
                        lineRenderer.sharedMaterial = newMaterial;
                        
                        // Store for future use
                        s_OriginalMaterials[s_LastHighlightedCell] = newMaterial;
                    }
                }
                
                s_LastHighlightedCell = null;
            }
        }

        // Helper method to check if a cell is valid (within the map boundaries)
        private static bool IsValidCell(CellComponent cell)
        {
            if (cell == null || cell.Cell == null) return false;
            
            // Use the MapPoint.IsInMap logic to validate cells
            // This ensures we only interact with cells that are within the valid map area
            short cellId = cell.CellId;
            
            // Validate using cell ID range - only cells in the appropriate range are valid
            return cellId >= 0 && cellId < Models.Maps.MapConstants.MapSize;
        }

        // Add this method to place a tile at the given cell
        private static void PlaceTileAtCell(CellComponent cell, TileSpriteData tileData)
        {
            if (cell == null || tileData == null) return;
            
            // Find map component in scene
            var mapComponent = GameObject.FindObjectOfType<MapComponent>();
            if (mapComponent == null || mapComponent.mapInformation == null) return;
            
            // Get the cell center position - the key for proper placement
            Vector3 cellPosition = GetCellCenterPosition(cell);
            
            // Check if we're placing a fixture
            bool isFixture = NewMapCreatorWindowType.Instance != null && NewMapCreatorWindowType.Instance.IsFixtureTile();
            
            // Check if we should clip to grid
            bool useClipping = NewMapCreatorWindowType.Instance != null && NewMapCreatorWindowType.Instance.UseClipping();
            
            // If not clipping to grid, use the exact mouse position instead of cell center
            if (!useClipping)
            {
                // Convert mouse position to world position
                Vector3 mousePosition = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition).origin;
                mousePosition.z = 0; // Ensure z is 0 for 2D
                cellPosition = mousePosition;
            }
            
            // Create a new tile data instance for this placement
            var newTile = new TileSpriteData
            {
                Id = tileData.Id,
                Position = new Vector2(cellPosition.x, cellPosition.y),
                Scale = tileData.Scale,
                Order = tileData.Order,
                FlipX = tileData.FlipX,
                FlipY = tileData.FlipY,
                Color = new TileColorData 
                { 
                    Red = tileData.Color.Red,
                    Green = tileData.Color.Green,
                    Blue = tileData.Color.Blue,
                    Alpha = tileData.Color.Alpha
                },
                IsFixture = isFixture // Store fixture state
            };
            
            // Add to map's sprite data
            if (mapComponent.mapInformation.SpriteData == null)
            {
                mapComponent.mapInformation.SpriteData = new MapSpriteData();
            }
            
            mapComponent.mapInformation.SpriteData.Tiles.Add(newTile);
            
            // Create visual representation in scene
            CreateTileVisualInEditor(cellPosition, newTile);
            
            // Mark scene as dirty for saving
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        }

        // New method to calculate the proper center position of a cell
        private static Vector3 GetCellCenterPosition(CellComponent cell)
        {
            if (cell == null) return Vector3.zero;
            
            // Get LineRenderer component which contains the shape of the cell
            var lineRenderer = cell.GetComponent<LineRenderer>();
            if (lineRenderer == null || lineRenderer.positionCount < 3)
            {
                // Fallback to transform position if LineRenderer not found
                return cell.transform.position;
            }
            
            // For cells with LineRenderer, calculate the center by averaging all vertices
            Vector3 sum = Vector3.zero;
            int pointCount = 0;
            
            for (int i = 0; i < lineRenderer.positionCount; i++)
            {
                // Skip duplicated closing point if it exists
                if (i == lineRenderer.positionCount - 1 && 
                    lineRenderer.GetPosition(i) == lineRenderer.GetPosition(0))
                    continue;
                
                sum += lineRenderer.GetPosition(i);
                pointCount++;
            }
            
            // Calculate the center
            Vector3 center = (pointCount > 0) ? sum / pointCount : cell.transform.position;
            
            // If the cell is using local space for points, convert to world space
            if (!lineRenderer.useWorldSpace)
            {
                center = cell.transform.TransformPoint(center);
            }
            
            return center;
        }

        // Helper method to extract the numeric ID from a tile ID string
        private static string ExtractTileId(string fullId)
        {
            // In our new implementation, the tile.Id is already just the numeric ID
            // so we can simply return it without additional processing
            return fullId;
        }

        // Create visual representation of a tile in the editor
        private static void CreateTileVisualInEditor(Vector3 position, TileSpriteData tileData)
        {
            // Use the clean numeric ID directly from tileData.Id
            string numericId = tileData.Id;
            
            // IMPORTANT: DON'T adjust position - use exactly what was passed in
            // This will match the preview position exactly
            
            // Check if the tile already exists at this position with this id
            var existingTiles = GameObject.FindObjectsOfType<GameObject>()
                .Where(go => go.transform.position == position && go.name.Contains(numericId))
                .ToArray();
            
            if (existingTiles.Length > 0)
            {
                // Tile already exists at this position, no need to create another
                return;
            }
            
            // Get path to use in name - this matches how the ploup project names tile objects
            string addressablePath = GetTileAddressablePath(numericId);
            
            // Create a GameObject with just the addressable path (no prefix duplicating what's already in the path)
            var tileObj = new GameObject(addressablePath);
            
            // Use EXACT position with no adjustments
            tileObj.transform.position = position;
            
            // Get texture to create sprite
            Texture2D tileTexture = FindTileTexture(tileData.Id);
            
            // Add SpriteRenderer component
            var renderer = tileObj.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = tileData.Order;
            renderer.flipX = tileData.FlipX;
            renderer.flipY = tileData.FlipY;
            
            // Apply scale
            tileObj.transform.localScale = new Vector3(tileData.Scale, tileData.Scale, 1f);
            
            // Add TileSprite component for identification with ploup
            var tileSprite = tileObj.AddComponent<CreatorMap.Scripts.TileSprite>();
            tileSprite.id = numericId;
            tileSprite.key = addressablePath;
            
            // Set tile type based on whether it's a fixture or not
            tileSprite.type = (byte)(tileData.IsFixture ? 1 : 0); // 0 = ground tile, 1 = fixture
            
            // Set flipping properties - explicitly copying from tileData
            tileSprite.flipX = tileData.FlipX;
            tileSprite.flipY = tileData.FlipY;
            
            // Set colorMultiplicatorIsOne based on the tile data color
            tileSprite.colorMultiplicatorIsOne = tileData.Color.IsOne;
            
            // Set color multiplier - default to special color values if using default color
            if (tileData.Color.IsOne)
            {
                // Use a bright yellow color as default (like in main project)
                tileSprite.colorMultiplicatorR = 264f;
                tileSprite.colorMultiplicatorG = 256f;
                tileSprite.colorMultiplicatorB = 200f;
                tileSprite.colorMultiplicatorA = 0f;
            }
            else
            {
                // Use custom color values from tileData
                tileSprite.colorMultiplicatorR = tileData.Color.Red;
                tileSprite.colorMultiplicatorG = tileData.Color.Green;
                tileSprite.colorMultiplicatorB = tileData.Color.Blue;
                tileSprite.colorMultiplicatorA = tileData.Color.Alpha;
            }
            
            // Always load and apply the ColorMatrixShader
            Shader colorMatrixShader = Shader.Find("Custom/ColorMatrixShader");
            if (colorMatrixShader != null)
            {
                // Create the material with our shader
                Material material = new Material(colorMatrixShader);
                
                // If we found a texture, create a sprite from it
                if (tileTexture != null)
                {
                    // Apply texture to the material
                    material.SetTexture("_MainTex", tileTexture);
                    material.SetFloat("_UseDefaultShape", 0f); // Use the texture, not default shape
                    
                    // IMPORTANT: Use the same pivot for both ground tiles and fixtures
                    // This ensures visual consistency with the preview
                    Vector2 pivot = new Vector2(0.5f, 0.5f); // Center pivot for all tiles
                    
                    // Create the sprite with the center pivot
                    var sprite = Sprite.Create(tileTexture, new Rect(0, 0, tileTexture.width, tileTexture.height), pivot);
                    renderer.sprite = sprite;
                    
                    Debug.Log($"Created tile at exact position {position} with type {tileSprite.type}, pivot {pivot}");
                }
                else
                {
                    // No texture found, enable default shape rendering in shader
                    material.SetFloat("_UseDefaultShape", 1f);
                    material.SetFloat("_CircleRadius", 0.5f);
                }
                
                // Apply color to material only if not using default color
                if (!tileSprite.colorMultiplicatorIsOne)
                {
                    // Apply different color handling based on tile type
                    if (tileSprite.type == 0) // Ground tile
                    {
                        material.SetColor(Shader.PropertyToID("_Color"), new Color(
                            tileSprite.colorMultiplicatorR / 255f,
                            tileSprite.colorMultiplicatorG / 255f,
                            tileSprite.colorMultiplicatorB / 255f,
                            1f
                        ));
                    }
                    else // Fixture
                    {
                        // Check if using high range values
                        bool useHighRange = tileSprite.colorMultiplicatorR > 1.0f || 
                                          tileSprite.colorMultiplicatorG > 1.0f || 
                                          tileSprite.colorMultiplicatorB > 1.0f;
                        
                        if (useHighRange)
                        {
                            material.SetColor(Shader.PropertyToID("_Color"), new Color(
                                tileSprite.colorMultiplicatorR / 255f,
                                tileSprite.colorMultiplicatorG / 255f,
                                tileSprite.colorMultiplicatorB / 255f,
                                tileSprite.colorMultiplicatorA > 1.0f ? tileSprite.colorMultiplicatorA / 255f : tileSprite.colorMultiplicatorA
                            ));
                        }
                        else
                        {
                            material.SetColor(Shader.PropertyToID("_Color"), new Color(
                                tileSprite.colorMultiplicatorR,
                                tileSprite.colorMultiplicatorG,
                                tileSprite.colorMultiplicatorB,
                                tileSprite.colorMultiplicatorA
                            ));
                        }
                    }
                    
                    // Apply material to renderer
                    renderer.sharedMaterial = material;
                }
            }

            // Set parent to map component
            var mapComponent = GameObject.FindObjectOfType<MapComponent>();
            if (mapComponent != null)
            {
                tileObj.transform.SetParent(mapComponent.transform);
            }

            // Mark scene as dirty
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        }

        // Helper method to find a tile texture from various possible locations
        private static Texture2D FindTileTexture(string tileId)
        {
            if (string.IsNullOrEmpty(tileId))
                return null;
                
            Texture2D tileTexture = null;
            
            // Try to load from NewMapCreatorWindow's asset path first
            var window = NewMapCreatorWindowType.Instance;
            if (window != null)
            {
                string path = window.GetAssetPath(tileId);
                if (!string.IsNullOrEmpty(path))
                {
                    tileTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (tileTexture != null)
                        return tileTexture;
                }
            }
            
            // Generate potential paths for the tile texture
            List<string> possiblePaths = new List<string>();
            
            if (tileId.Length >= 2)
            {
                string subfolder = tileId.Substring(0, 2);
                possiblePaths.AddRange(new string[]
                {
                    $"Assets/CreatorMap/Tiles/{subfolder}/{tileId}.png",
                    $"Assets/CreatorMap/Content/Tiles/{subfolder}/{tileId}.png",
                    $"Assets/CreatorMap/Tiles/{tileId}.png",
                    $"Assets/CreatorMap/Content/Tiles/{tileId}.png"
                });
            }
            else
            {
                possiblePaths.AddRange(new string[]
                {
                    $"Assets/CreatorMap/Tiles/{tileId}.png",
                    $"Assets/CreatorMap/Content/Tiles/{tileId}.png"
                });
            }
            
            // Try each path
            foreach (var path in possiblePaths)
            {
                if (System.IO.File.Exists(path))
                {
                    tileTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (tileTexture != null)
                        return tileTexture;
                }
            }
            
            // If texture not found in asset paths, try Resources folder
            tileTexture = Resources.Load<Texture2D>($"Tiles/{tileId}");
            
            return tileTexture;
        }

        private static void CreatePlaceholderSprite(SpriteRenderer renderer)
        {
            // Create a colorful placeholder to make it more visible
            Texture2D texture = new Texture2D(64, 64);
            Color[] colors = new Color[64 * 64];
            
            // Create a checkerboard pattern
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    // Create a bright, highly visible pattern
                    bool isEvenX = (x / 8) % 2 == 0;
                    bool isEvenY = (y / 8) % 2 == 0;
                    
                    if (isEvenX == isEvenY)
                    {
                        colors[y * 64 + x] = new Color(0.8f, 0.3f, 0.8f, 1.0f); // Bright purple
                    }
                    else
                    {
                        colors[y * 64 + x] = new Color(0.3f, 0.8f, 0.8f, 1.0f); // Bright cyan
                    }
                    
                    // Add border
                    if (x < 2 || x > 61 || y < 2 || y > 61)
                    {
                        colors[y * 64 + x] = new Color(0.1f, 0.1f, 0.1f, 1.0f); // Dark border
                    }
                }
            }
            
            texture.SetPixels(colors);
            texture.Apply();
            
            // Create a sprite from this texture
            Sprite sprite = Sprite.Create(
                texture, 
                new Rect(0, 0, texture.width, texture.height), 
                new Vector2(0.5f, 0.5f), // Center pivot
                100.0f, // Pixels per unit
                0, // Extrude edges
                SpriteMeshType.FullRect
            );
            
            // Set the sprite and make sure alpha is 1
            renderer.sprite = sprite;
            renderer.color = new Color(renderer.color.r, renderer.color.g, renderer.color.b, 1.0f);
            
            Debug.Log("Created placeholder sprite with checkerboard pattern");
        }

        // Helper method to get the full path based on a tile ID
        private static string GetTileAddressablePath(string tileId)
        {
            // Clean the ID to just numbers
            string numericId = new string(tileId.Where(char.IsDigit).ToArray());
            if (string.IsNullOrEmpty(numericId))
                return tileId; // Fallback to original
            
            // Get the first two digits for the subfolder (or just use the first digit if only one digit)
            string subfolder = numericId.Length >= 2 ? numericId.Substring(0, 2) : numericId;
            
            // Check if we should use fixture path format
            bool isFixture = NewMapCreatorWindowType.Instance != null && NewMapCreatorWindowType.Instance.IsFixtureTile();
            
            // Format the full path according to pattern (Tiles Assets/Tiles/... or Fixture Assets/Tiles/...)
            string prefix = isFixture ? "Fixture Assets" : "Tiles Assets";
            return $"{prefix}/Tiles/{subfolder}/{numericId}.png";
        }

        // Helper method to create a solid color texture for GUI
        private static Texture2D MakeTexture(int width, int height, Color color)
        {
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }
            
            Texture2D texture = new Texture2D(width, height);
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        // Add this method for free placement mode
        private static void PlaceTileAtPosition(Vector3 position, TileSpriteData tileData)
        {
            if (tileData == null) return;
            
            // Find map component in scene
            var mapComponent = GameObject.FindObjectOfType<MapComponent>();
            if (mapComponent == null || mapComponent.mapInformation == null) return;
            
            // Check if we're placing a fixture
            bool isFixture = NewMapCreatorWindowType.Instance != null && NewMapCreatorWindowType.Instance.IsFixtureTile();
            
            // Create a new tile data instance for this placement
            var newTile = new TileSpriteData
            {
                Id = tileData.Id,
                Position = new Vector2(position.x, position.y),
                Scale = tileData.Scale,
                Order = tileData.Order,
                FlipX = tileData.FlipX,
                FlipY = tileData.FlipY,
                Color = new TileColorData 
                { 
                    Red = tileData.Color.Red,
                    Green = tileData.Color.Green,
                    Blue = tileData.Color.Blue,
                    Alpha = tileData.Color.Alpha
                },
                IsFixture = isFixture
            };
            
            // Add to map's sprite data
            if (mapComponent.mapInformation.SpriteData == null)
            {
                mapComponent.mapInformation.SpriteData = new MapSpriteData();
            }
            
            mapComponent.mapInformation.SpriteData.Tiles.Add(newTile);
            
            // Create visual representation in scene
            CreateTileVisualInEditor(position, newTile);
            
            // Mark scene as dirty for saving
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        }

        // Draw tile preview at mouse position
        private static void DrawTilePreviewAtMouse(Event e)
        {
            // Get the window instance
            var window = NewMapCreatorWindowType.Instance;
            
            if (window == null)
                return;
                
            // Get the currently selected tile
            var selectedTile = window.GetSelectedTile();
            if (selectedTile == null)
                return;
                
            // Get mouse position in world space
            Vector3 mousePosition = HandleUtility.GUIPointToWorldRay(e.mousePosition).origin;
            mousePosition.z = 0; // Ensure z is 0 for 2D
            
            // If clipping is enabled, snap to grid cell
            bool useClipping = window.UseClipping();
            CellComponent hoverCell = null;
            
            if (useClipping)
            {
                // Find the cell under the mouse to get the center position
                var hits = Physics2D.RaycastAll(mousePosition, Vector2.zero);
                foreach (var hit in hits)
                {
                    if (hit.collider == null) continue;
                    
                    var cell = hit.collider.GetComponent<CellComponent>();
                    if (cell != null && cell.Cell != null && IsValidCell(cell))
                    {
                        // Get the center position of the cell
                        mousePosition = GetCellCenterPosition(cell);
                        hoverCell = cell;
                        break;
                    }
                }
            }
            
            // Get proper path and load sprite for preview
            string numericId = selectedTile.Id;
            string path = window.GetAssetPath(numericId);
            Sprite previewSprite = null;
            
            // Try to load sprite from primary path
            if (!string.IsNullOrEmpty(path))
            {
                previewSprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (previewSprite != null)
                {
                    Debug.Log($"Preview: Successfully loaded sprite from primary path: {path}");
                }
            }
            
            // If primary path failed, try alternative paths
            if (previewSprite == null)
            {
                // Try content path
                if (numericId.Length >= 2)
                {
                    string subfolder = numericId.Substring(0, 2);
                    string altPath = $"Assets/CreatorMap/Content/Tiles/{subfolder}/{numericId}.png";
                    if (System.IO.File.Exists(altPath))
                    {
                        previewSprite = AssetDatabase.LoadAssetAtPath<Sprite>(altPath);
                        if (previewSprite != null)
                        {
                            Debug.Log($"Preview: Successfully loaded sprite from content path: {altPath}");
                        }
                    }
                }
                
                // Try another alternative path structure
                if (previewSprite == null && numericId.Length >= 2)
                {
                    string subfolder = numericId.Substring(0, 2);
                    string thirdAltPath = $"Assets/CreatorMap/Tiles/{subfolder}/{numericId}.png";
                    if (System.IO.File.Exists(thirdAltPath))
                    {
                        previewSprite = AssetDatabase.LoadAssetAtPath<Sprite>(thirdAltPath);
                        if (previewSprite != null)
                        {
                            Debug.Log($"Preview: Successfully loaded sprite from third path: {thirdAltPath}");
                        }
                    }
                }
            }
            
            // Draw preview
            if (previewSprite != null)
            {
                // Fix the Z position to ensure proper visibility
                mousePosition.z = 0;
                
                float scale = selectedTile.Scale;
                Quaternion rotation = Quaternion.identity;
                
                // Apply color with higher opacity for better visibility
                Color color = selectedTile.Color.ToColor();
                Color previewColor = new Color(color.r, color.g, color.b, 0.85f); // More opaque
                
                // Create material for preview with better visibility settings
                Material previewMaterial = new Material(Shader.Find("Sprites/Default"));
                previewMaterial.mainTexture = previewSprite.texture;
                previewMaterial.color = previewColor;
                
                // Get sprite dimensions with proper scale
                float spriteWidth = previewSprite.bounds.size.x;
                float spriteHeight = previewSprite.bounds.size.y;
                
                // Calculate screen position
                Vector2 screenPos = HandleUtility.WorldToGUIPoint(mousePosition);
                
                // Draw sprite at screen position
                Handles.BeginGUI();
                // Calculate scale to match exactly the real world size
                float pixelsPerUnit = EditorGUIUtility.pixelsPerPoint * 100; // Conversion from world to screen
                float widthScaled = spriteWidth * scale * pixelsPerUnit;
                float heightScaled = spriteHeight * scale * pixelsPerUnit;
                
                // Draw using GUI instead of Graphics for more reliable positioning
                GUI.DrawTexture(
                    new Rect(
                        screenPos.x - (widthScaled / 2),  // Center on screen position
                        screenPos.y - (heightScaled / 2),
                        widthScaled,
                        heightScaled
                    ),
                    previewSprite.texture,
                    ScaleMode.ScaleToFit,
                    true,
                    0,
                    previewColor,
                    Vector4.zero,
                    0
                );
                
                // Handle flipping using a second pass with a matrix if needed
                if (selectedTile.FlipX || selectedTile.FlipY)
                {
                    GUIUtility.RotateAroundPivot(
                        0, 
                        new Vector2(screenPos.x, screenPos.y)
                    );
                    
                    Matrix4x4 oldMatrix = GUI.matrix;
                    Matrix4x4 newMatrix = GUI.matrix;
                    
                    // Apply flipping by scaling the matrix
                    if (selectedTile.FlipX)
                        newMatrix *= Matrix4x4.Scale(new Vector3(-1, 1, 1));
                    if (selectedTile.FlipY)
                        newMatrix *= Matrix4x4.Scale(new Vector3(1, -1, 1));
                    
                    // Apply the transformation
                    GUI.matrix = newMatrix;
                    
                    // Draw the flipped texture
                    GUI.DrawTexture(
                        new Rect(
                            screenPos.x - (widthScaled / 2),
                            screenPos.y - (heightScaled / 2),
                            widthScaled,
                            heightScaled
                        ),
                        previewSprite.texture,
                        ScaleMode.ScaleToFit,
                        true,
                        0,
                        previewColor,
                        Vector4.zero,
                        0
                    );
                    
                    // Restore the original matrix
                    GUI.matrix = oldMatrix;
                }
                
                Handles.EndGUI();
                
                // Draw a placement indicator that's more visible
                float indicatorSize = 0.2f * scale;
                
                // Draw placement grid snap indicator
                if (useClipping)
                {
                    // Draw a crosshair at the placement position - more visible now
                    // float lineLength = 0.25f * scale;
                    // Handles.color = new Color(1f, 1f, 1f, 1.0f); // Fully opaque white
                    // Handles.DrawAAPolyLine(3.0f, // Thicker line 
                    //     mousePosition + new Vector3(-lineLength, 0, 0),
                    //     mousePosition + new Vector3(lineLength, 0, 0)
                    // );
                    // Handles.DrawAAPolyLine(3.0f, // Thicker line
                    //     mousePosition + new Vector3(0, -lineLength, 0),
                    //     mousePosition + new Vector3(0, lineLength, 0)
                    // );
                    
                    // If we're over a valid cell, highlight the cell
                    if (hoverCell != null)
                    {
                        // Draw cell highlight
                        var lineRenderer = hoverCell.GetComponent<LineRenderer>();
                        if (lineRenderer != null && lineRenderer.positionCount >= 4)
                        {
                            Vector3[] cellPoints = new Vector3[lineRenderer.positionCount];
                            for (int i = 0; i < lineRenderer.positionCount; i++)
                            {
                                cellPoints[i] = lineRenderer.GetPosition(i);
                            }
                            
                            // Draw the cell outline with a glow effect
                            Handles.color = new Color(0f, 1f, 1f, 0.7f); // Brighter cyan with higher opacity
                            
                            // Draw thicker lines for cell outline
                            for (int i = 0; i < cellPoints.Length - 1; i++)
                            {
                                Handles.DrawAAPolyLine(3.0f, cellPoints[i], cellPoints[i + 1]);
                            }
                            
                            // Close the loop with thicker line
                            if (cellPoints.Length > 3)
                            {
                                Handles.DrawAAPolyLine(3.0f, cellPoints[cellPoints.Length - 1], cellPoints[0]);
                            }
                            
                            // Draw a central point to indicate the exact placement spot
                            // Handles.color = new Color(1f, 1f, 0f, 1.0f); // Yellow, fully opaque
                            // Handles.DrawSolidDisc(mousePosition, Vector3.forward, 0.07f * scale);
                        }
                    }
                }
                else
                {
                    // Free placement indicator - more visible now
                    Handles.color = new Color(0f, 1f, 0f, 1.0f); // Bright green, fully opaque
                    
                    // Draw double circle for better visibility
                    Handles.DrawWireDisc(mousePosition, Vector3.forward, indicatorSize);
                    Handles.DrawWireDisc(mousePosition, Vector3.forward, indicatorSize * 0.8f);
                    
                    // Draw a center point
                    // Handles.color = new Color(1f, 1f, 0f, 1.0f); // Yellow, fully opaque
                    // Handles.DrawSolidDisc(mousePosition, Vector3.forward, 0.07f * scale);
                }
                
                // Add text label to show more info
                Handles.BeginGUI();
                GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
                labelStyle.normal.textColor = Color.white;
                labelStyle.fontSize = 12;
                labelStyle.fontStyle = FontStyle.Bold;
                labelStyle.alignment = TextAnchor.UpperLeft;
                labelStyle.normal.background = MakeTexture(1, 1, new Color(0, 0, 0, 0.8f)); // Darker background
                labelStyle.padding = new RectOffset(5, 5, 5, 5); // Add padding for better readability
                
                // Convert world position to screen position
                Vector2 labelPos = HandleUtility.WorldToGUIPoint(mousePosition);
                labelPos.y += 30; // Offset below the cursor
                
                // Show tile ID and placement mode
                string isFixture = window.IsFixtureTile() ? "Fixture" : "Tile";
                string clipMode = useClipping ? "Grid Snap" : "Free Placement";
                GUI.Label(new Rect(labelPos.x, labelPos.y, 180, 50), 
                    $"ID: {numericId}\n{isFixture} - {clipMode}", 
                    labelStyle);
                
                Handles.EndGUI();
            }
            else
            {
                // Draw a placeholder if sprite can't be loaded - more noticeable now
                float size = 0.5f * selectedTile.Scale;
                
                // Draw a more noticeable placeholder
                // First, draw a filled background
                Handles.color = new Color(0.8f, 0.2f, 0.8f, 0.5f); // Semi-transparent fill
                Vector3[] fillPoints = new Vector3[] {
                    mousePosition + new Vector3(-size, -size, 0),
                    mousePosition + new Vector3(-size, size, 0),
                    mousePosition + new Vector3(size, size, 0),
                    mousePosition + new Vector3(size, -size, 0)
                };
                Handles.DrawSolidRectangleWithOutline(
                    fillPoints, 
                    new Color(0.8f, 0.2f, 0.8f, 0.5f), // Fill color
                    new Color(1f, 0.3f, 1f, 1.0f) // Outline color - fully opaque
                );
                
                // Draw an X inside with thicker lines
                Handles.color = new Color(1f, 1f, 1f, 1.0f); // White, fully opaque
                Handles.DrawAAPolyLine(3.0f, // Thicker line
                    mousePosition + new Vector3(-size * 0.7f, -size * 0.7f, 0),
                    mousePosition + new Vector3(size * 0.7f, size * 0.7f, 0)
                );
                Handles.DrawAAPolyLine(3.0f, // Thicker line
                    mousePosition + new Vector3(-size * 0.7f, size * 0.7f, 0),
                    mousePosition + new Vector3(size * 0.7f, -size * 0.7f, 0)
                );
                
                // Show warning about missing sprite with improved visibility
                Handles.BeginGUI();
                GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
                labelStyle.normal.textColor = Color.yellow;
                labelStyle.fontSize = 12;
                labelStyle.fontStyle = FontStyle.Bold;
                labelStyle.alignment = TextAnchor.UpperLeft;
                labelStyle.normal.background = MakeTexture(1, 1, new Color(0.5f, 0, 0.5f, 0.9f)); // Purple background
                labelStyle.padding = new RectOffset(5, 5, 5, 5); // Add padding
                labelStyle.border = new RectOffset(2, 2, 2, 2); // Add border
                
                Vector2 screenPos = HandleUtility.WorldToGUIPoint(mousePosition);
                screenPos.y += 30;
                
                GUI.Label(new Rect(screenPos.x, screenPos.y, 200, 40), 
                    $"Missing sprite for ID: {numericId}", 
                    labelStyle);
                
                // Add additional instructions
                GUIStyle helpStyle = new GUIStyle(labelStyle);
                helpStyle.normal.textColor = Color.white;
                helpStyle.fontSize = 10;
                helpStyle.normal.background = MakeTexture(1, 1, new Color(0, 0, 0, 0.8f));
                
                GUI.Label(new Rect(screenPos.x, screenPos.y + 45, 250, 40), 
                    "Check asset paths or import missing sprite", 
                    helpStyle);
                
                Handles.EndGUI();
                
                // Log more detailed information for debugging
                Debug.LogWarning($"Preview: Could not load sprite for tile ID: {numericId}, tried path: {path}");
            }
            
            // Request repaint every frame to keep the preview smooth
            SceneView.RepaintAll();
            HandleUtility.Repaint();
        }

        private static void DrawCellPropertyIndicators()
        {
            // Vérifier si les indicateurs doivent être affichés
            var window = NewMapCreatorWindowType.Instance;
            if (window == null || !window.ShowPropertyIndicators())
                return;
                
            // Récupérer toutes les cellules dans la scène
            CellComponent[] allCells = GameObject.FindObjectsOfType<CellComponent>();
            if (allCells == null || allCells.Length == 0) return;
            
            foreach (var cell in allCells)
            {
                if (cell == null || cell.Cell == null) continue;
                
                // Obtenir le centre de la cellule
                Vector3 cellCenter = GetCellCenterPosition(cell);
                
                // Taille des indicateurs
                float indicatorSize = 0.12f;
                
                // Vérifier chaque propriété et dessiner un indicateur approprié
                
                // Walkable/Non-Walkable (base)
                if (!cell.Cell.IsWalkable)
                {
                    // Indicateur rouge pour "non-walkable"
                    Handles.color = new Color(0.8f, 0.2f, 0.2f, 0.8f);
                    Handles.DrawSolidDisc(cellCenter, Vector3.forward, indicatorSize);
                    
                    // Symbole X
                    Handles.color = Color.white;
                    Handles.DrawLine(
                        cellCenter + new Vector3(-indicatorSize*0.5f, -indicatorSize*0.5f, 0), 
                        cellCenter + new Vector3(indicatorSize*0.5f, indicatorSize*0.5f, 0)
                    );
                    Handles.DrawLine(
                        cellCenter + new Vector3(-indicatorSize*0.5f, indicatorSize*0.5f, 0), 
                        cellCenter + new Vector3(indicatorSize*0.5f, -indicatorSize*0.5f, 0)
                    );
                }
                
                // Non-Walkable pendant les combats
                if ((cell.Cell.Data & 0x0002) != 0) // CELL_NON_WALKABLE_FIGHT
                {
                    // Indicateur orange pour "non-walkable combat" centré
                    Handles.color = new Color(1.0f, 0.6f, 0.1f, 0.8f);
                    Handles.DrawSolidDisc(cellCenter, Vector3.forward, indicatorSize);
                    
                    // Symbole épée
                    Handles.color = Color.white;
                    Handles.DrawLine(
                        cellCenter + new Vector3(-indicatorSize*0.4f, -indicatorSize*0.4f, 0), 
                        cellCenter + new Vector3(indicatorSize*0.4f, indicatorSize*0.4f, 0)
                    );
                    Handles.DrawLine(
                        cellCenter, 
                        cellCenter + new Vector3(0, -indicatorSize*0.6f, 0)
                    );
                }
                
                // Non-Walkable pendant le RP
                if ((cell.Cell.Data & 0x0004) != 0) // CELL_NON_WALKABLE_RP
                {
                    // Indicateur bleu pour "non-walkable RP" centré
                    Handles.color = new Color(0.3f, 0.5f, 0.9f, 0.8f);
                    Handles.DrawSolidDisc(cellCenter, Vector3.forward, indicatorSize);
                    
                    // Symbole bulle de dialogue
                    Handles.color = Color.white;
                    Handles.DrawWireDisc(cellCenter, Vector3.forward, indicatorSize * 0.6f);
                    Vector3 trianglePos = cellCenter + new Vector3(-indicatorSize*0.3f, -indicatorSize*0.6f, 0);
                    Handles.DrawLine(
                        cellCenter + new Vector3(-indicatorSize*0.3f, -indicatorSize*0.3f, 0),
                        trianglePos
                    );
                    Handles.DrawLine(
                        trianglePos,
                        cellCenter + new Vector3(-indicatorSize*0.1f, -indicatorSize*0.3f, 0)
                    );
                }
                
                // Cellule Bleue
                if ((cell.Cell.Data & 0x0010) != 0) // CELL_BLUE
                {
                    // Indicateur bleu centré
                    Handles.color = new Color(0.2f, 0.4f, 1.0f, 0.8f);
                    Handles.DrawSolidDisc(cellCenter, Vector3.forward, indicatorSize);
                }
                
                // Cellule Rouge
                if ((cell.Cell.Data & 0x0020) != 0) // CELL_RED
                {
                    // Indicateur rouge centré
                    Handles.color = new Color(1.0f, 0.2f, 0.2f, 0.8f);
                    Handles.DrawSolidDisc(cellCenter, Vector3.forward, indicatorSize);
                }
                
                // Cellule Visible
                if ((cell.Cell.Data & 0x0040) != 0) // CELL_VISIBLE
                {
                    // Indicateur jaune clair pour "visible" centré
                    Handles.color = new Color(1.0f, 1.0f, 0.3f, 0.8f);
                    Handles.DrawSolidDisc(cellCenter, Vector3.forward, indicatorSize);
                    
                    // Symbole V
                    Handles.color = Color.black;
                    Handles.DrawLine(
                        cellCenter + new Vector3(-indicatorSize*0.4f, 0, 0), 
                        cellCenter + new Vector3(-indicatorSize*0.1f, -indicatorSize*0.4f, 0)
                    );
                    Handles.DrawLine(
                        cellCenter + new Vector3(-indicatorSize*0.1f, -indicatorSize*0.4f, 0),
                        cellCenter + new Vector3(indicatorSize*0.4f, indicatorSize*0.4f, 0)
                    );
                }
                
                // Cellule de ferme
                if ((cell.Cell.Data & 0x0080) != 0) // CELL_FARM
                {
                    // Afficher l'icône de blé au centre de la cellule si la texture est chargée
                    if (s_BleTexture != null)
                    {
                        // Taille de l'icône - un peu plus grande que les indicateurs standard
                        float iconSize = 0.35f;
                        
                        // Dessiner l'icône au centre de la cellule
                        Handles.BeginGUI();
                        Vector2 screenPoint = HandleUtility.WorldToGUIPoint(cellCenter);
                        float halfSize = iconSize * 25; // Conversion en pixels pour GUI
                        
                        // Dessiner la texture centrée sur la cellule
                        GUI.DrawTexture(
                            new Rect(screenPoint.x - halfSize, screenPoint.y - halfSize, halfSize * 2, halfSize * 2),
                            s_BleTexture,
                            ScaleMode.ScaleToFit
                        );
                        Handles.EndGUI();
                    }
                    else
                    {
                        // Fallback si la texture n'est pas disponible - utiliser un indicateur centré
                        // Indicateur vert pour "ferme"
                        Handles.color = new Color(0.2f, 0.8f, 0.2f, 0.8f);
                        Handles.DrawSolidDisc(cellCenter, Vector3.forward, indicatorSize);
                        
                        // Symbole feuille
                        Handles.color = Color.white;
                        Vector3[] leafPoints = new Vector3[] {
                            cellCenter + new Vector3(-indicatorSize*0.3f, -indicatorSize*0.3f, 0),
                            cellCenter + new Vector3(0, indicatorSize*0.4f, 0),
                            cellCenter + new Vector3(indicatorSize*0.3f, -indicatorSize*0.3f, 0),
                        };
                        Handles.DrawAAPolyLine(leafPoints);
                    }
                }
                
                // Cellule de havre-sac
                if ((cell.Cell.Data & 0x0100) != 0) // CELL_HAVENBAG
                {
                    // Indicateur violet pour "havre-sac" centré
                    Handles.color = new Color(0.7f, 0.3f, 0.9f, 0.8f);
                    Handles.DrawSolidDisc(cellCenter, Vector3.forward, indicatorSize);
                    
                    // Symbole sac
                    Handles.color = Color.white;
                    Handles.DrawWireArc(cellCenter, Vector3.forward, new Vector3(0, 1, 0), 180, indicatorSize * 0.4f);
                    Handles.DrawLine(
                        cellCenter + new Vector3(-indicatorSize*0.4f, 0, 0),
                        cellCenter + new Vector3(-indicatorSize*0.4f, -indicatorSize*0.4f, 0)
                    );
                    Handles.DrawLine(
                        cellCenter + new Vector3(indicatorSize*0.4f, 0, 0),
                        cellCenter + new Vector3(indicatorSize*0.4f, -indicatorSize*0.4f, 0)
                    );
                    Handles.DrawLine(
                        cellCenter + new Vector3(-indicatorSize*0.4f, -indicatorSize*0.4f, 0),
                        cellCenter + new Vector3(indicatorSize*0.4f, -indicatorSize*0.4f, 0)
                    );
                }
                
                // Line of Sight
                if ((cell.Cell.Data & 0x0008) != 0) // CELL_LINE_OF_SIGHT
                {
                    // Afficher l'icône de l'œil fermé au centre de la cellule si la texture est chargée
                    if (s_ClosedEyeTexture != null)
                    {
                        // Taille de l'icône - un peu plus grande que les indicateurs standard
                        float iconSize = 0.35f;
                        
                        // Dessiner l'icône au centre de la cellule
                        Handles.BeginGUI();
                        Vector2 screenPoint = HandleUtility.WorldToGUIPoint(cellCenter);
                        float halfSize = iconSize * 25; // Conversion en pixels pour GUI
                        
                        // Dessiner la texture centrée sur la cellule
                        GUI.DrawTexture(
                            new Rect(screenPoint.x - halfSize, screenPoint.y - halfSize, halfSize * 2, halfSize * 2),
                            s_ClosedEyeTexture,
                            ScaleMode.ScaleToFit
                        );
                        Handles.EndGUI();
                    }
                    else
                    {
                        // Fallback si la texture n'est pas disponible - utiliser un indicateur centré
                        // Indicateur blanc pour "line of sight"
                        Handles.color = new Color(0.9f, 0.9f, 0.9f, 0.8f);
                        Handles.DrawSolidDisc(cellCenter, Vector3.forward, indicatorSize);
                        
                        // Symbole œil
                        Handles.color = Color.black;
                        Handles.DrawWireDisc(cellCenter, Vector3.forward, indicatorSize * 0.5f);
                        Handles.DrawSolidDisc(cellCenter, Vector3.forward, indicatorSize * 0.2f);
                    }
                }
            }
        }

        private static void DrawIndicatorLegend(SceneView sceneView)
        {
            // Créer un style pour la boîte de légende
            var boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.normal.background = MakeTexture(1, 1, new Color(0, 0, 0, 0.7f));
            boxStyle.normal.textColor = Color.white;
            
            // Créer un style pour les titres
            var titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.normal.textColor = Color.white;
            
            // Créer un style pour les éléments de légende
            var itemStyle = new GUIStyle(GUI.skin.label);
            itemStyle.normal.textColor = Color.white;
            itemStyle.fontSize = 10;
            
            // Taille et position de la légende
            float legendWidth = 180;
            float legendHeight = 300;
            float padding = 10;
            
            // Positionner la légende dans le coin inférieur droit de la vue
            Rect position = new Rect(
                sceneView.position.width - legendWidth - padding,
                sceneView.position.height - legendHeight - padding - 20, // Ajustement pour la barre d'état
                legendWidth,
                legendHeight
            );
            
            // Dessiner la boîte de fond
            GUI.Box(position, "", boxStyle);
            
            // Dessiner le titre
            GUI.Label(new Rect(position.x + 10, position.y + 10, position.width - 20, 20), "Légende des indicateurs", titleStyle);
            
            // Dessiner les éléments de légende
            float itemHeight = 20;
            float currentY = position.y + 40;
            float circleSize = 8;
            
            // Non-Walkable
            DrawLegendItem(position.x + 10, currentY, circleSize, new Color(0.8f, 0.2f, 0.2f, 0.8f), "Non-Walkable", itemStyle);
            currentY += itemHeight;
            
            // Non-Walkable Fight
            DrawLegendItem(position.x + 10, currentY, circleSize, new Color(1.0f, 0.6f, 0.1f, 0.8f), "Non-Walkable Fight", itemStyle);
            currentY += itemHeight;
            
            // Non-Walkable RP
            DrawLegendItem(position.x + 10, currentY, circleSize, new Color(0.3f, 0.5f, 0.9f, 0.8f), "Non-Walkable RP", itemStyle);
            currentY += itemHeight;
            
            // Line of Sight
            if (s_ClosedEyeTexture != null)
            {
                // Utiliser l'icône de l'œil fermé pour la légende
                GUI.DrawTexture(
                    new Rect(position.x + 10, currentY, circleSize * 2, circleSize * 2),
                    s_ClosedEyeTexture,
                    ScaleMode.ScaleToFit
                );
                GUI.Label(new Rect(position.x + 10 + (circleSize * 2) + 5, currentY, 150, 20), "Block Line of Sight", itemStyle);
            }
            else
            {
                DrawLegendItem(position.x + 10, currentY, circleSize, new Color(0.9f, 0.9f, 0.9f, 0.8f), "Block Line of Sight", itemStyle);
            }
            currentY += itemHeight;
            
            // Blue Cell
            DrawLegendItem(position.x + 10, currentY, circleSize, new Color(0.2f, 0.4f, 1.0f, 0.8f), "Blue Cell", itemStyle);
            currentY += itemHeight;
            
            // Cellule Rouge
            DrawLegendItem(position.x + 10, currentY, circleSize, new Color(1.0f, 0.2f, 0.2f, 0.8f), "Red Cell", itemStyle);
            currentY += itemHeight;
            
            // Cellule Visible
            DrawLegendItem(position.x + 10, currentY, circleSize, new Color(1.0f, 1.0f, 0.3f, 0.8f), "Visible Cell", itemStyle);
            currentY += itemHeight;
            
            // Cellule de ferme
            DrawLegendItem(position.x + 10, currentY, circleSize, new Color(0.2f, 0.8f, 0.2f, 0.8f), "Farm Cell", itemStyle);
            currentY += itemHeight;
            
            // Cellule de havre-sac
            DrawLegendItem(position.x + 10, currentY, circleSize, new Color(0.7f, 0.3f, 0.9f, 0.8f), "Havenbag Cell", itemStyle);
        }
        
        private static void DrawLegendItem(float x, float y, float size, Color color, string text, GUIStyle style)
        {
            // Dessiner le cercle de couleur
            EditorGUI.DrawRect(new Rect(x, y + 2, size, size), color);
            
            // Dessiner le texte
            GUI.Label(new Rect(x + size + 5, y, 150, 20), text, style);
        }

        // Add this method after the PlaceTileAtPosition method
        private static void EraseTileAtPosition(Vector3 position)
        {
            // Find all tile sprites in the scene
            var tilesInScene = GameObject.FindObjectsOfType<CreatorMap.Scripts.TileSprite>();
            if (tilesInScene == null || tilesInScene.Length == 0) return;
            
            // Distance threshold for erasing tiles (radius around click position)
            // Reduce the radius for more precision
            float eraseRadius = 0.25f; // Smaller radius for more precise erasing
            bool erasedAny = false;
            
            // Find map component
            var mapComponent = GameObject.FindObjectOfType<MapComponent>();
            if (mapComponent == null || mapComponent.mapInformation == null || 
                mapComponent.mapInformation.SpriteData == null) return;
            
            // Find the closest tile to the click position
            CreatorMap.Scripts.TileSprite closestTile = null;
            float closestDistance = float.MaxValue;
            
            // Loop through tiles to find the closest one
            foreach (var tileSprite in tilesInScene)
            {
                if (tileSprite == null) continue;
                
                // Calculate distance from click position to tile
                float distance = Vector2.Distance(
                    new Vector2(position.x, position.y),
                    new Vector2(tileSprite.transform.position.x, tileSprite.transform.position.y)
                );
                
                // Check if this tile is within range and closer than any previous tile
                if (distance <= eraseRadius && distance < closestDistance)
                {
                    closestDistance = distance;
                    closestTile = tileSprite;
                }
            }
            
            // If a close tile was found, remove it
            if (closestTile != null)
            {
                // Get the tile ID to find it in the mapInformation.SpriteData
                string tileId = closestTile.id;
                Vector3 tilePosition = closestTile.transform.position;
                
                // Remove the tile from the scene
                UnityEngine.Object.DestroyImmediate(closestTile.gameObject);
                
                // Also remove it from the data structure
                TileSpriteData tileToRemove = null;
                foreach (var tile in mapComponent.mapInformation.SpriteData.Tiles)
                {
                    if (tile.Id == tileId && 
                        Vector2.Distance(tile.Position, new Vector2(tilePosition.x, tilePosition.y)) < 0.1f)
                    {
                        tileToRemove = tile;
                        break;
                    }
                }
                
                // Remove the tile from the data
                if (tileToRemove != null)
                {
                    mapComponent.mapInformation.SpriteData.Tiles.Remove(tileToRemove);
                    erasedAny = true;
                }
            }
            
            if (erasedAny)
            {
                // Mark scene as dirty for saving
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                    UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
                Debug.Log("Erased tile at position: " + position);
            }
        }
    }
}
#endif 