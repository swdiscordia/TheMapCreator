#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Components.Maps;
using Managers.Maps.MapCreator;
using System.Collections.Generic;
using System;

namespace MapCreator.Editor
{
    public class MapCreatorSceneGUI
    {
        private static Color s_HighlightColor = new Color(1f, 0.92f, 0.016f, 0.8f); // Bright yellow with 80% opacity
        private static Color s_NonWalkableColor = new Color(0.8f, 0.2f, 0.2f, 0.5f); // Red with 50% opacity
        private static Color s_WalkableColor = new Color(1f, 1f, 1f, 0.12f); // Default walkable color
        
        private static CellComponent s_LastHighlightedCell;
        private static Dictionary<CellComponent, Material> s_OriginalMaterials = new Dictionary<CellComponent, Material>();
        
        // Dictionnaire pour stocker les points d'origine du LineRenderer de chaque cellule
        private static Dictionary<CellComponent, Vector3[]> s_OriginalPoints = new Dictionary<CellComponent, Vector3[]>();

        [InitializeOnLoadMethod]
        static void Initialize()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            if (Application.isPlaying) return;

            Event e = Event.current;
            if (e == null) return;

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
                    e.Use();
                    break;
                    
                case EventType.MouseDown:
                    if (e.button == 0) // Left mouse button
                    {
                        HandleMouseClick(e);
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
            
            // Premier essai : raycast normal pour trouver les cellules avec collider
            var hits = Physics2D.RaycastAll(ray.origin, ray.direction);
            
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
            
            // Si aucune cellule n'a été trouvée par raycast, essayons une approche différente pour les cellules non-walkable
            if (foundCell == null)
            {
                // Chercher toutes les cellules dans la scène
                CellComponent[] allCells = GameObject.FindObjectsOfType<CellComponent>();
                
                // Distance minimale pour considérer que la souris est sur une cellule
                float minDistance = 0.5f;
                float closestDistance = float.MaxValue;
                
                foreach (var cell in allCells)
                {
                    if (cell == null || cell.Cell == null || !IsValidCell(cell)) continue;
                    
                    // Pour les cellules non-walkable sans collider
                    if (!cell.Cell.IsWalkable)
                    {
                        // Vérifier si le point de la souris est proche de la position de la cellule
                        Vector3 cellPosition = cell.transform.position;
                        Vector3 mouseWorldPos = ray.GetPoint(10f); // Prendre un point à une certaine distance sur le rayon
                        
                        // Calculer la distance dans le plan XY (ignorer Z)
                        float distance = Vector2.Distance(
                            new Vector2(cellPosition.x, cellPosition.y),
                            new Vector2(mouseWorldPos.x, mouseWorldPos.y)
                        );
                        
                        // Si cette cellule est plus proche que la précédente et en dessous du seuil
                        if (distance < minDistance && distance < closestDistance)
                        {
                            closestDistance = distance;
                            foundCell = cell;
                        }
                    }
                }
            }
            
            // Si une cellule a été trouvée (avec ou sans collider), la mettre en évidence
            if (foundCell != null)
            {
                HighlightCell(foundCell);
            }
            
            // Force repaint to update visuals
            SceneView.RepaintAll();
        }

        private static void HandleMouseClick(Event e)
        {
            if (s_LastHighlightedCell == null || s_LastHighlightedCell.Cell == null) return;

            var currentMode = MapCreatorWindow.CurrentDrawMode;
            if (currentMode == MapCreatorWindow.DrawMode.NonWalkable || currentMode == MapCreatorWindow.DrawMode.Walkable)
            {
                bool shouldBeWalkable = currentMode == MapCreatorWindow.DrawMode.Walkable;
                Debug.Log($"Changing cell to {(shouldBeWalkable ? "walkable" : "non-walkable")} mode");
                SetCellWalkable(s_LastHighlightedCell, shouldBeWalkable);
            }
        }

        private static void SetCellWalkable(CellComponent cell, bool walkable)
        {
            if (cell == null || cell.Cell == null) return;

            Undo.RecordObject(cell.gameObject, "Change Cell Walkability");

            // Update cell data
            cell.Cell.Data = (short)(walkable ? 0 : 1); // 0 for walkable, 1 for non-walkable
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
                cell.Cell.Data = 0; // 0 = walkable
                Debug.Log($"RestoreWalkableCell: Parent cell data changed from {(wasWalkable ? "walkable" : "non-walkable")} to walkable");
            }
            
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
            var lineRenderer = cell.GetComponent<LineRenderer>();
            if (lineRenderer != null)
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
                    s_OriginalPoints.Remove(cell);
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
                cell.Cell.Data = 1; // 1 = non-walkable
                Debug.Log($"SetupNonWalkableCell: Parent cell data set to non-walkable");
            }
            
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
                
                // Pour les cellules non-walkable, configurer le LineRenderer avec exactement 2 points spécifiques
                lineRenderer.positionCount = 2;
                lineRenderer.SetPosition(0, new Vector3(0, 0, 0)); // Premier point à (0,0,0)
                lineRenderer.SetPosition(1, new Vector3(0, 0, 1)); // Deuxième point à (0,0,1)
                
                Debug.Log($"SetupNonWalkableCell: Set LineRenderer to have exactly two points: (0,0,0) and (0,0,1)");
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
            // This method is no longer needed
            // Non-walkable cells just have their collider disabled
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
                        // Create a default material based on walkability
                        bool isWalkable = s_LastHighlightedCell.Cell != null && s_LastHighlightedCell.Cell.IsWalkable;
                        Material newMaterial = new Material(Shader.Find("Sprites/Default"));
                        newMaterial.color = isWalkable ? s_WalkableColor : s_NonWalkableColor;
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
    }
}
#endif 