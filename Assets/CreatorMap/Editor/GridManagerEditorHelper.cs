#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using CreatorMap.Scripts.Core.Grid;
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using Components.Maps;

namespace CreatorMap.Editor
{
    [InitializeOnLoad]
    public class GridManagerEditorHelper
    {
        // Flag pour suivre si nous venons de sortir du mode jeu
        private static bool s_JustExitedPlayMode = false;

        // Cette liste conserve les cellIds et flags modifiés pendant le mode jeu
        private static Dictionary<ushort, uint> s_ModifiedCellsInPlayMode = new Dictionary<ushort, uint>();

        // Ce constructeur statique est appelé quand l'éditeur démarre
        static GridManagerEditorHelper()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            
            // S'abonner aux événements de l'éditeur pour détecter des changements de scène
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorSceneManager.sceneSaved += OnSceneSaved;
        }
        
        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.EnteredPlayMode:
                    Debug.Log("[GridManagerEditorHelper] Entering Play Mode, saving initial cell states");
                    // Sauvegarde de l'état initial des cellules avant le mode jeu
                    CaptureInitialCellStates();
                    break;
                    
                case PlayModeStateChange.ExitingPlayMode:
                    Debug.Log("[GridManagerEditorHelper] Exiting Play Mode, capturing modified cells");
                    // Capture les cellules modifiées pendant le play mode
                    CaptureModifiedCellStates();
                    s_JustExitedPlayMode = true;
                    break;
                    
                case PlayModeStateChange.EnteredEditMode:
                    if (s_JustExitedPlayMode)
                    {
                        Debug.Log("[GridManagerEditorHelper] Returned to Edit Mode, refreshing grid...");
                        
                        // Retarder pour permettre à Unity de finir la transition
                        EditorApplication.delayCall += () => {
                            ForceGridRefresh();
                            s_JustExitedPlayMode = false;
                        };
                    }
                    break;
            }
        }

        // Capture l'état des cellules avant d'entrer en mode jeu
        private static void CaptureInitialCellStates()
        {
            s_ModifiedCellsInPlayMode.Clear();
            
            // Trouver le MapComponent pour obtenir l'état actuel
            var mapComponent = Object.FindObjectOfType<MapComponent>();
            if (mapComponent != null && mapComponent.mapInformation != null && mapComponent.mapInformation.cells != null)
            {
                // Enregistrer l'état actuel de toutes les cellules
                foreach (var pair in mapComponent.mapInformation.cells.dictionary)
                {
                    s_ModifiedCellsInPlayMode[pair.Key] = pair.Value;
                }
                Debug.Log($"[GridManagerEditorHelper] Captured state of {s_ModifiedCellsInPlayMode.Count} cells before play mode");
            }
        }

        // Capture les cellules modifiées pendant le mode jeu
        private static void CaptureModifiedCellStates()
        {
            // Trouver le MapComponent pour obtenir l'état actuel
            var mapComponent = Object.FindObjectOfType<MapComponent>();
            if (mapComponent != null && mapComponent.mapInformation != null && mapComponent.mapInformation.cells != null)
            {
                // Mettre à jour notre dictionnaire avec les cellules modifiées
                foreach (var pair in mapComponent.mapInformation.cells.dictionary)
                {
                    s_ModifiedCellsInPlayMode[pair.Key] = pair.Value;
                }
                Debug.Log($"[GridManagerEditorHelper] Captured modified state of {mapComponent.mapInformation.cells.dictionary.Count} cells during play mode");
            }
        }
        
        // Force une reconstruction complète de la grille
        private static void ForceGridRefresh()
        {
            Debug.Log("[GridManagerEditorHelper] PRÉSERVATION EXACTE DE LA GRILLE après sortie du mode jeu");
            
            // Trouver le MapComponent
            var mapComponent = Object.FindObjectOfType<MapComponent>();
            if (mapComponent == null)
            {
                Debug.LogWarning("[GridManagerEditorHelper] No MapComponent found to restore cell data");
                return;
            }
            
            // Trouver le GridManager
            var gridManager = Object.FindObjectOfType<MapCreatorGridManager>();
            if (gridManager == null)
            {
                Debug.LogWarning("[GridManagerEditorHelper] No GridManager found to refresh grid");
                return;
            }
            
            // Synchroniser les données des cellules modifiées vers le MapComponent
            if (s_ModifiedCellsInPlayMode.Count > 0)
            {
                if (mapComponent.mapInformation == null)
                {
                    mapComponent.mapInformation = new CreatorMap.Scripts.Data.MapBasicInformation();
                }
                
                if (mapComponent.mapInformation.cells == null)
                {
                    mapComponent.mapInformation.cells = new CreatorMap.Scripts.Data.SerializableDictionary<ushort, uint>();
                }
                
                // Appliquer les changements capturés au MapComponent
                foreach (var pair in s_ModifiedCellsInPlayMode)
                {
                    if (mapComponent.mapInformation.cells.dictionary.ContainsKey(pair.Key))
                    {
                        mapComponent.mapInformation.cells.dictionary[pair.Key] = pair.Value;
                    }
                    else
                    {
                        mapComponent.mapInformation.cells.dictionary.Add(pair.Key, pair.Value);
                    }
                }
                
                Debug.Log($"[GridManagerEditorHelper] Données des {s_ModifiedCellsInPlayMode.Count} cellules préservées dans MapComponent");
                
                // Marquer comme dirty pour sauvegarder ces changements
                EditorUtility.SetDirty(mapComponent);
            }
            
            // Utiliser la nouvelle méthode qui préserve l'état visuel exactement
            RefreshGridAfterPlayMode();
            
            Debug.Log("[GridManagerEditorHelper] Préservation exacte de la grille terminée avec succès");
        }
        
        // Gestionnaire d'événement quand une scène est ouverte
        private static void OnSceneOpened(UnityEngine.SceneManagement.Scene scene, OpenSceneMode mode)
        {
            // Force une mise à jour de la grille quand une scène est ouverte
            EditorApplication.delayCall += () => {
                ForceGridRefresh();
            };
        }
        
        // Gestionnaire d'événement quand une scène est sauvegardée
        private static void OnSceneSaved(UnityEngine.SceneManagement.Scene scene)
        {
            Debug.Log($"[GridManagerEditorHelper] Scene {scene.name} saved, ensuring grid and data are in sync");
            
            // S'assurer que les données de la grille et les visuels sont synchronisés
            var gridManager = Object.FindObjectOfType<MapCreatorGridManager>();
            if (gridManager != null)
            {
                // Force la synchronisation vers le MapComponent pour persister les changements
                gridManager.SyncWithMapComponent();
            }
        }

        public static void RefreshGridAfterPlayMode()
        {
            Debug.Log("[GridManagerEditorHelper] Refreshing grid after exiting play mode");
            
            // Find required components
            var gridManager = Object.FindObjectOfType<MapCreatorGridManager>();
            var mapComponent = Object.FindObjectOfType<Components.Maps.MapComponent>();
            
            if (gridManager == null)
            {
                Debug.LogError("[GridManagerEditorHelper] Cannot find GridManager!");
                return;
            }
            
            if (mapComponent == null)
            {
                Debug.LogError("[GridManagerEditorHelper] Cannot find MapComponent!");
                return;
            }
            
            // IMPORTANT: NE PAS recreer la grille complètement après sortie du mode jeu
            // Seulement appliquer les modifications qui ont été faites en mode jeu
            Debug.Log("[GridManagerEditorHelper] PRÉSERVATION EXACTE de l'état visuel après sortie du mode jeu");
            
            // Nous n'avons plus besoin de restaurer les cellules depuis s_ModifiedCellsInPlayMode,
            // car le MapComponent conserve déjà les données correctement.
            
            // Mettre à jour directement la visualisation sans supprimer les cellules existantes
            bool hasUpdates = false;
            
            // Mettre à jour chaque cellule visuellement sans recréation
            var allCells = gridManager.GetComponentsInChildren<Components.Maps.CellComponent>();
            
            int cellsUpdated = 0;
            foreach (var cellComponent in allCells)
            {
                ushort cellId = cellComponent.CellId;
                
                // Obtenir l'état de la cellule à partir du MapComponent
                uint flags = 0x0040; // Valeur par défaut (visible uniquement)
                if (mapComponent.mapInformation.cells.dictionary.TryGetValue(cellId, out uint cellFlags))
                {
                    flags = cellFlags;
                }
                
                // Vérifier si la cellule est walkable (bit 0 = 0 signifie walkable)
                bool isWalkable = (flags & 0x0001) == 0;
                
                // Mettre à jour le modèle de données de la cellule
                cellComponent.Cell = new MapCreator.Data.Models.Cell((short)cellId, (short)flags);
                
                // Vérifier si le collider correspond à l'état walkable
                bool hasCollider = cellComponent.GetComponent<PolygonCollider2D>() != null;
                
                if (isWalkable && !hasCollider)
                {
                    // Ajouter le collider si la cellule doit être walkable
                    var hitbox = cellComponent.gameObject.AddComponent<PolygonCollider2D>();
                    var points = new Vector2[4];
                    points[0] = new Vector2(0, 0);
                    points[1] = new Vector2(0.5f, 0.25f);
                    points[2] = new Vector2(1, 0);
                    points[3] = new Vector2(0.5f, -0.25f);
                    hitbox.SetPath(0, points);
                    cellsUpdated++;
                    hasUpdates = true;
                }
                else if (!isWalkable && hasCollider)
                {
                    // Supprimer le collider si la cellule ne doit pas être walkable
                    var collider = cellComponent.GetComponent<PolygonCollider2D>();
                    if (collider != null)
                    {
                        GameObject.DestroyImmediate(collider);
                        cellsUpdated++;
                        hasUpdates = true;
                    }
                }
            }
            
            // Force la mise à jour de la scène si nécessaire
            if (hasUpdates)
            {
                Debug.Log($"[GridManagerEditorHelper] {cellsUpdated} cellules ont été mises à jour après sortie du mode jeu");
                SceneView.RepaintAll();
                
                // Mark scene as dirty for saving
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                    UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            }
            else
            {
                Debug.Log("[GridManagerEditorHelper] Aucune mise à jour visuelle nécessaire après sortie du mode jeu");
            }
            
            Debug.Log("[GridManagerEditorHelper] Refresh after play mode completed with EXACT state preservation");
        }
    }
}
#endif 