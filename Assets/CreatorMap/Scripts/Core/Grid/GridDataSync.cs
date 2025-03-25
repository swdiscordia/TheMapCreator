using UnityEngine;
using Components.Maps;
using System.Collections.Generic;

namespace CreatorMap.Scripts.Core.Grid
{
    /// <summary>
    /// Ensures grid data is properly synchronized with MapComponent
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
            
            // Sync on start to make sure we have the right data
            SyncGridToMapComponent();
        }
        
        void OnEnable()
        {
            if (m_GridManager != null)
            {
                // Listen for cell modifications to sync immediately
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
            
            // Load data from MapComponent to GridManager on initialization
            if (m_MapComponent.mapInformation != null && m_MapComponent.mapInformation.cells != null)
            {
                m_GridManager.gridData.cells.Clear();
                m_GridManager.gridData.cellsDict.Clear();
                
                foreach (var cellPair in m_MapComponent.mapInformation.cells.dictionary)
                {
                    m_GridManager.gridData.cells.Add(new MapCreatorGridManager.CellData(cellPair.Key, cellPair.Value));
                    m_GridManager.gridData.cellsDict[cellPair.Key] = cellPair.Value;
                }
                
                Debug.Log($"[GridDataSync] Loaded {m_GridManager.gridData.cells.Count} cells from MapComponent");
            }
            
            m_Initialized = true;
            Debug.Log("[GridDataSync] Initialized successfully");
        }
        
        private void OnCellModified(ushort cellId, uint flags)
        {
            // When a cell is modified in the grid, update the MapComponent immediately
            UpdateMapComponentCell(cellId, flags);
            
            // Ensure the scene is marked as dirty (in Editor)
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(m_MapComponent);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            #endif
        }
        
        public void SyncGridToMapComponent()
        {
            if (!m_Initialized)
            {
                Initialize();
                if (!m_Initialized) return;
            }
            
            // Make sure mapInformation exists
            if (m_MapComponent.mapInformation == null)
            {
                m_MapComponent.mapInformation = new Data.MapBasicInformation();
            }
            
            // Make sure cells dictionary exists
            if (m_MapComponent.mapInformation.cells == null)
            {
                m_MapComponent.mapInformation.cells = new Data.SerializableDictionary<ushort, uint>();
            }
            
            // Copy grid manager's cell data to map component
            foreach (var cellData in m_GridManager.gridData.cells)
            {
                UpdateMapComponentCell(cellData.cellId, cellData.flags);
            }
            
            Debug.Log($"[GridDataSync] Synced {m_GridManager.gridData.cells.Count} cells to MapComponent");
            
            // Ensure the scene is marked as dirty (in Editor)
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(m_MapComponent);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            #endif
        }
        
        private void UpdateMapComponentCell(ushort cellId, uint flags)
        {
            if (m_MapComponent.mapInformation == null || m_MapComponent.mapInformation.cells == null)
                return;
                
            if (m_MapComponent.mapInformation.cells.dictionary.ContainsKey(cellId))
            {
                m_MapComponent.mapInformation.cells.dictionary[cellId] = flags;
            }
            else
            {
                m_MapComponent.mapInformation.cells.dictionary.Add(cellId, flags);
            }
        }
    }
} 