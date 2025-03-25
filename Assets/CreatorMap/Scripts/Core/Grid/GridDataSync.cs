using UnityEngine;
using Components.Maps;
using System.Collections.Generic;
using Managers.Cameras;

namespace CreatorMap.Scripts.Core.Grid
{
    /// <summary>
    /// Ensures grid visualization is properly updated when MapComponent data changes
    /// </summary>
    [RequireComponent(typeof(MapCreatorGridManager))]
    public class GridDataSync : MonoBehaviour
    {
        private MapCreatorGridManager m_GridManager;
        private MapComponent m_MapComponent;
        private bool m_Initialized = false;
        
        void Awake()
        {
            Initialize();
        }
        
        void Start()
        {
            if (!m_Initialized)
            {
                Initialize();
            }
            
            // In all modes, we only need to ensure the grid reflects the MapComponent's data
            Debug.Log("[GridDataSync] Start: Ensuring grid is visualizing MapComponent data correctly");
            
            // Ensure preserveDataInPlayMode is set to true in Play mode
            if (Application.isPlaying && m_MapComponent != null)
            {
                m_MapComponent.preserveDataInPlayMode = true;
                Debug.Log("[GridDataSync] Start: EN MODE JEU - PRÉSERVATION EXACTE de l'apparence visuelle");
                
                // Log data to verify preservation
                if (m_MapComponent.mapInformation != null && m_MapComponent.mapInformation.cells != null)
                {
                    Debug.Log($"[GridDataSync] Start: MapComponent has {m_MapComponent.mapInformation.cells.dictionary.Count} cells");
                    
                    // JAMAIS RECRÉER la grille en mode jeu - uniquement mettre à jour l'état visuel
                    EnsureGridVisualsMatchData();
                }
            }
            else
            {
                // Force a grid rebuild to ensure it's using MapComponent data
                if (m_GridManager != null)
                {
                    m_GridManager.CreateGrid();
                }
            }
        }
        
        void OnEnable()
        {
            if (m_GridManager != null)
            {
                // Listen for cell modifications to update the MapComponent
                m_GridManager.OnCellModified += OnCellModified;
            }
        }
        
        void OnDisable()
        {
            if (m_GridManager != null)
            {
                m_GridManager.OnCellModified -= OnCellModified;
            }
        }
        
        void OnApplicationQuit()
        {
            // Force final serialization before exiting play mode
            if (Application.isPlaying && m_Initialized && m_MapComponent != null)
            {
                Debug.Log("[GridDataSync] Application is quitting, ensuring MapComponent data is serialized");
                
                // Ensure any changes made to MapComponent are properly serialized
                if (m_MapComponent.mapInformation != null && m_MapComponent.mapInformation.cells != null)
                {
                    m_MapComponent.mapInformation.cells.OnBeforeSerialize();
                    
                    #if UNITY_EDITOR
                    UnityEditor.EditorUtility.SetDirty(m_MapComponent);
                    #endif
                }
            }
        }
        
        private void Initialize()
        {
            // Get required components
            m_GridManager = GetComponent<MapCreatorGridManager>();
            m_MapComponent = FindObjectOfType<MapComponent>();
            
            if (m_GridManager == null)
            {
                Debug.LogError("[GridDataSync] Unable to find GridManager component!");
                return;
            }
            
            if (m_MapComponent == null)
            {
                Debug.LogError("[GridDataSync] Unable to find MapComponent!");
                return;
            }
            
            // No data needs to be loaded from MapComponent to GridManager here
            // The GridManager's CreateGrid method will read from MapComponent directly
            
            m_Initialized = true;
            Debug.Log("[GridDataSync] Initialized successfully");
        }
        
        private void OnCellModified(ushort cellId, uint flags)
        {
            if (!m_Initialized || m_MapComponent == null || m_MapComponent.mapInformation == null || m_MapComponent.mapInformation.cells == null)
            {
                Debug.LogError("[GridDataSync] OnCellModified: Not initialized or MapComponent is null!");
                return;
            }
            
            // Debug log for play mode
            if (Application.isPlaying)
            {
                Debug.Log($"[GridDataSync] OnCellModified: Cell {cellId} modified with flags {flags} in PLAY mode");
            }
            
            // Update the MapComponent with the new data directly
            if (m_MapComponent.mapInformation.cells.dictionary.ContainsKey(cellId))
            {
                // Log previous value for debugging
                uint previousFlags = m_MapComponent.mapInformation.cells.dictionary[cellId];
                Debug.Log($"[GridDataSync] OnCellModified: Updating cell {cellId}, old flags: {previousFlags}, new flags: {flags}");
                
                m_MapComponent.mapInformation.cells.dictionary[cellId] = flags;
            }
            else
            {
                Debug.Log($"[GridDataSync] OnCellModified: Adding new cell {cellId} with flags {flags}");
                m_MapComponent.mapInformation.cells.dictionary.Add(cellId, flags);
            }
            
            // Force serialization
            m_MapComponent.mapInformation.cells.OnBeforeSerialize();
            
            // Mark scene as dirty in editor
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(m_MapComponent);
            
            // Additional step to ensure the scene is marked dirty
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            #endif
            
            Debug.Log($"[GridDataSync] OnCellModified: MapComponent updated and marked dirty");
        }
        
        // Legacy method kept for compatibility
        public void SyncGridToMapComponent()
        {
            Debug.Log("[GridDataSync] SyncGridToMapComponent is deprecated as data now flows only from MapComponent to GridManager");
            
            // No implementation needed as GridManager no longer stores data
        }
        
        private void UpdateMapComponentCell(ushort cellId, uint flags)
        {
            if (m_MapComponent == null || m_MapComponent.mapInformation == null || m_MapComponent.mapInformation.cells == null)
                return;
                
            if (m_MapComponent.mapInformation.cells.dictionary.ContainsKey(cellId))
            {
                m_MapComponent.mapInformation.cells.dictionary[cellId] = flags;
            }
            else
            {
                m_MapComponent.mapInformation.cells.dictionary.Add(cellId, flags);
            }
            
            // Force serialization to make sure the changes are saved
            m_MapComponent.mapInformation.cells.OnBeforeSerialize();
        }
        
        /// <summary>
        /// Ensures that the visual representation of cells matches the data in MapComponent
        /// WITHOUT recreating any cells, just updating their visual state
        /// </summary>
        private void EnsureGridVisualsMatchData()
        {
            if (m_GridManager == null || m_MapComponent == null || 
                m_MapComponent.mapInformation == null || m_MapComponent.mapInformation.cells == null)
            {
                Debug.LogError("[GridDataSync] EnsureGridVisualsMatchData: Missing required components");
                return;
            }
            
            Debug.Log("[GridDataSync] EnsureGridVisualsMatchData: Mise à jour de l'état visuel sans recréation");
            
            // NE PAS RECRÉER LES CELLULES - uniquement mettre à jour leur état visuel
            // Pour chaque cellule, mettre à jour son état walkable en fonction des données du MapComponent
            var allCells = m_GridManager.GetComponentsInChildren<CellComponent>();
            int cellsUpdated = 0;
            
            foreach (var cellComponent in allCells)
            {
                ushort cellId = cellComponent.CellId;
                
                // Obtenir les flags depuis le MapComponent
                uint flags = 0x0040; // Par défaut: visible uniquement
                if (m_MapComponent.mapInformation.cells.dictionary.TryGetValue(cellId, out uint storedFlags))
                {
                    flags = storedFlags;
                }
                
                // Vérifier si la cellule est walkable (bit 0 = 0 signifie walkable)
                bool isWalkable = (flags & 0x0001) == 0;
                
                // Mettre à jour le modèle de données de la cellule
                cellComponent.Cell = new MapCreator.Data.Models.Cell((short)cellId, (short)flags);
                
                // Vérifier si le collider correspond à l'état walkable
                bool hasCollider = cellComponent.GetComponent<PolygonCollider2D>() != null;
                
                if (isWalkable && !hasCollider)
                {
                    // Si la cellule doit être walkable mais n'a pas de collider, ajouter un
                    var hitbox = cellComponent.gameObject.AddComponent<PolygonCollider2D>();
                    var points = new Vector2[4];
                    points[0] = new Vector2(0, 0);
                    points[1] = new Vector2(0.5f, 0.25f); // Valeurs fixes pour éviter la dépendance à GridCameraController
                    points[2] = new Vector2(1, 0);
                    points[3] = new Vector2(0.5f, -0.25f);
                    hitbox.SetPath(0, points);
                    cellsUpdated++;
                }
                else if (!isWalkable && hasCollider)
                {
                    // Si la cellule ne doit pas être walkable mais a un collider, le supprimer
                    var collider = cellComponent.GetComponent<PolygonCollider2D>();
                    if (collider != null)
                    {
                        GameObject.DestroyImmediate(collider);
                        cellsUpdated++;
                    }
                }
            }
            
            Debug.Log($"[GridDataSync] EnsureGridVisualsMatchData: {cellsUpdated} cellules mises à jour visuellement sur {allCells.Length} total");
        }
    }
} 