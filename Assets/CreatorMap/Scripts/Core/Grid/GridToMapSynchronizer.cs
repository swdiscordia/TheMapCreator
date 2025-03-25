using System;
using Components.Maps;
using CreatorMap.Scripts.Data;
using UnityEngine;

namespace CreatorMap.Scripts.Core.Grid
{
    /// <summary>
    /// Synchronise les données entre GridManager et MapComponent
    /// </summary>
    [RequireComponent(typeof(MapCreatorGridManager))]
    public class GridToMapSynchronizer : MonoBehaviour
    {
        private MapCreatorGridManager _gridManager;
        private MapComponent _mapComponent;
        private bool _initialized = false;
        private int _cellsSynchronized = 0;
        private int _syncAttempts = 0;
        
        void Awake()
        {
            Debug.Log("[DATA_DEBUG] GridToMapSynchronizer.Awake - Initialisation");
            _gridManager = GetComponent<MapCreatorGridManager>();
            
            if (_gridManager == null)
            {
                Debug.LogError("[DATA_DEBUG] GridToMapSynchronizer.Awake - GridManager introuvable!");
                return;
            }
            
            // Chercher le MapComponent dans la scène
            _mapComponent = FindAnyObjectByType<MapComponent>();
            
            if (_mapComponent == null)
            {
                Debug.LogError("[DATA_DEBUG] GridToMapSynchronizer.Awake - MapComponent introuvable dans la scène!");
                return;
            }
            
            Debug.Log($"[DATA_DEBUG] GridToMapSynchronizer.Awake - GridManager et MapComponent trouvés");
            
            // S'abonner à l'événement de modification de cellule
            _gridManager.OnCellModified += HandleCellModified;
            
            _initialized = true;
            Debug.Log("[DATA_DEBUG] GridToMapSynchronizer.Awake - Initialisation terminée, prêt pour la synchronisation");
        }

        void Start()
        {
            Debug.Log("[DATA_DEBUG] GridToMapSynchronizer.Start - Démarrage");
            if (!_initialized)
            {
                Debug.LogError("[DATA_DEBUG] GridToMapSynchronizer.Start - Non initialisé, abandon");
                return;
            }
            
            // Synchroniser l'état initial
            SynchronizeAll();
        }

        void OnDestroy()
        {
            Debug.Log("[DATA_DEBUG] GridToMapSynchronizer.OnDestroy - Nettoyage");
            if (_gridManager != null)
            {
                _gridManager.OnCellModified -= HandleCellModified;
            }
        }
        
        /// <summary>
        /// Gère la modification d'une cellule
        /// </summary>
        private void HandleCellModified(ushort cellId, uint flags)
        {
            Debug.Log($"[DATA_DEBUG] GridToMapSynchronizer.HandleCellModified - Cellule {cellId} modifiée avec flags {flags}");
            
            if (_mapComponent == null || _mapComponent.mapInformation == null)
            {
                Debug.LogError($"[DATA_DEBUG] GridToMapSynchronizer.HandleCellModified - MapComponent ou mapInformation est null!");
                return;
            }
            
            try
            {
                // Mettre à jour les données dans MapComponent - convertir ushort en uint pour compatibilité
                _mapComponent.mapInformation.UpdateCellData(cellId, flags);
                _cellsSynchronized++;
                Debug.Log($"[DATA_DEBUG] GridToMapSynchronizer.HandleCellModified - Synchronisation réussie de la cellule {cellId}. Total synchronisé: {_cellsSynchronized}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[DATA_DEBUG] GridToMapSynchronizer.HandleCellModified - Erreur lors de la mise à jour: {e.Message}");
            }
        }
        
        /// <summary>
        /// Synchronise toutes les cellules du GridManager vers le MapComponent
        /// </summary>
        public void SynchronizeAll()
        {
            _syncAttempts++;
            Debug.Log($"[DATA_DEBUG] GridToMapSynchronizer.SynchronizeAll - Tentative #{_syncAttempts} de synchronisation de toutes les cellules");
            
            if (_gridManager == null || _mapComponent == null)
            {
                Debug.LogError($"[DATA_DEBUG] GridToMapSynchronizer.SynchronizeAll - GridManager ou MapComponent est null!");
                return;
            }
            
            if (_mapComponent.mapInformation == null)
            {
                Debug.LogError($"[DATA_DEBUG] GridToMapSynchronizer.SynchronizeAll - mapInformation est null!");
                return;
            }
            
            Debug.Log($"[DATA_DEBUG] GridToMapSynchronizer.SynchronizeAll - Début de synchronisation:");
            Debug.Log($"[DATA_DEBUG] - GridManager cells count: {_gridManager.gridData.cellsDict.Count}");
            Debug.Log($"[DATA_DEBUG] - MapComponent cells count: {_mapComponent.mapInformation.cells?.dictionary?.Count ?? 0}");

            try
            {
                int syncCount = 0;
                int errorCount = 0;
                
                // Clear existing cells
                _mapComponent.mapInformation.ClearAllCells();
                
                // Copy all cells from GridManager to MapComponent
                foreach (var pair in _gridManager.gridData.cellsDict)
                {
                    try
                    {
                        ushort cellId = pair.Key;
                        uint flags = pair.Value; // Déjà en uint, pas besoin de conversion
                        
                        _mapComponent.mapInformation.UpdateCellData(cellId, flags);
                        syncCount++;
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[DATA_DEBUG] Erreur lors de la synchronisation de la cellule {pair.Key}: {e.Message}");
                        errorCount++;
                    }
                }
                
                _cellsSynchronized = syncCount;
                Debug.Log($"[DATA_DEBUG] GridToMapSynchronizer.SynchronizeAll - Synchronisation terminée: {syncCount} cellules synchronisées, {errorCount} erreurs");
                Debug.Log($"[DATA_DEBUG] - MapComponent cells count après sync: {_mapComponent.mapInformation.cells?.dictionary?.Count ?? 0}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[DATA_DEBUG] GridToMapSynchronizer.SynchronizeAll - Erreur globale: {e.Message}");
            }
        }
        
        /// <summary>
        /// Réinitialise la synchronisation
        /// </summary>
        public void Reset()
        {
            Debug.Log("[DATA_DEBUG] GridToMapSynchronizer.Reset - Réinitialisation de la synchronisation");
            _cellsSynchronized = 0;
            _syncAttempts = 0;
            
            if (_mapComponent != null && _mapComponent.mapInformation != null)
            {
                _mapComponent.mapInformation.ClearAllCells();
                Debug.Log("[DATA_DEBUG] GridToMapSynchronizer.Reset - Cellules du MapComponent effacées");
            }
            
            SynchronizeAll();
        }
    }
} 