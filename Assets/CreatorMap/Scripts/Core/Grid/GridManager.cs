using System;
using System.Collections.Generic;
using Components.Maps;
using Managers.Cameras;
using Managers.Scene;
using UnityEngine;
using Managers.Maps;
using CreatorMap.Scripts.Core;
using CreatorMap.Scripts.Data;
using UnityEngine.SceneManagement;
using MapCreator.Data.Models;
using Cell = CreatorMap.Scripts.Data.Cell;

namespace CreatorMap.Scripts.Core.Grid
{
    public class MapCreatorGridManager : MonoBehaviour
    {
        public static MapCreatorGridManager Instance;
        
        private readonly List<GameObject> _cells = new(560);
        
        // Nouvelle structure de données pour stocker les informations de la grille
        [System.Serializable]
        public class GridData
        {
            public int id;
            public List<CellData> cells = new List<CellData>();
            
            // Dictionnaire temporaire pour la migration - changement du type de valeur de ushort à uint
            [System.NonSerialized]
            public Dictionary<ushort, uint> cellsDict = new Dictionary<ushort, uint>();
        }
        
        [System.Serializable]
        public class CellData
        {
            public ushort cellId;
            public uint flags;
            
            public CellData(ushort cellId, uint flags)
            {
                this.cellId = cellId;
                this.flags = flags;
            }
        }
        
        // Données de la grille - now hidden from inspector as it's only for compatibility
        [HideInInspector]
        public GridData gridData = new GridData();
        
        // Collection pour stocker les données des tiles placés
        private readonly Dictionary<Vector2, TileSpriteData> _placedTiles = new Dictionary<Vector2, TileSpriteData>();
        private readonly Dictionary<Vector2, FixtureSpriteData> _placedFixtures = new Dictionary<Vector2, FixtureSpriteData>();

        // Delegate pour l'événement de modification de cellule
        public delegate void CellModifiedHandler(ushort cellId, uint flags);

        // Ajouter un event utilisant ce delegate près des autres champs
        public event CellModifiedHandler OnCellModified;

        // Start is called before the first frame update
        void Start()
        {
            CreateGrid();
        }

        private void Awake()
        {
            Instance = this;
            
            // We don't initialize gridData.cellsDict anymore as we will read from MapComponent
            
            Debug.Log($"[DATA_DEBUG] Awake: GridManager initialized");
            
            // Ajuster la position Y de la caméra à 2.7
            var camera = Camera.main;
            if (camera != null)
            {
                Vector3 position = camera.transform.position;
                camera.transform.position = new Vector3(position.x, 2.7f, position.z);
            }
            
            // We'll let GridDataSync handle data loading from MapComponent
        }

        public void CreateGrid()
        {
            Debug.Log("[DATA_DEBUG] Début de CreateGrid");
            
            // En mode jeu, ne PAS recréer la grille, juste mettre à jour l'état visuel
            if (Application.isPlaying)
            {
                Debug.Log("[DATA_DEBUG] CreateGrid: Mode jeu détecté - préservation de la grille existante");
                RefreshGridVisualsInPlayMode();
                return;
            }
            
            // Destroy all existing cell GameObjects
            foreach (var cell in _cells)
            {
                if (cell != null)
                {
                    DestroyImmediate(cell);
                }
            }
            
            _cells.Clear(); // Clear the list to avoid stale references
            Debug.Log("[DATA_DEBUG] CreateGrid: Toutes les cellules existantes ont été supprimées");
            
            // Find MapComponent to read data from
            var mapComponent = UnityEngine.Object.FindObjectOfType<Components.Maps.MapComponent>();
            if (mapComponent == null || mapComponent.mapInformation == null || mapComponent.mapInformation.cells == null)
            {
                Debug.LogError("[DATA_DEBUG] CreateGrid: Cannot find MapComponent or its data is null!");
                return;
            }
            
            // If in play mode, ensure preserveDataInPlayMode is still true (Unity might reset it)
            if (Application.isPlaying)
            {
                mapComponent.preserveDataInPlayMode = true;
                Debug.Log("[DATA_DEBUG] CreateGrid: Ensuring preserveDataInPlayMode is set to true in Play mode");
                
                // Log cell count to confirm data preservation
                Debug.Log($"[DATA_DEBUG] CreateGrid: MapComponent has {mapComponent.mapInformation.cells.dictionary.Count} cells in dictionary");
            }
            
            // Create all cells using the improved UpdateCellVisual method
            foreach (var cellId in MapTools.EveryCellId)
            {
                // Use UpdateCellVisual which now reads data from MapComponent
                CreateCellVisual((ushort)cellId);
            }
            
            Debug.Log($"[DATA_DEBUG] CreateGrid: {_cells.Count} cellules ont été créées");
            
            Debug.Log("[DATA_DEBUG] Fin de CreateGrid");
        }
        
        /// <summary>
        /// Creates the visual representation of a cell (sans recreating existing cells)
        /// </summary>
        private void CreateCellVisual(ushort cellId)
        {
            // Find MapComponent to read data from
            var mapComponent = UnityEngine.Object.FindObjectOfType<Components.Maps.MapComponent>();
            if (mapComponent == null || mapComponent.mapInformation == null || mapComponent.mapInformation.cells == null)
            {
                Debug.LogError("[DATA_DEBUG] CreateCellVisual: Cannot find MapComponent or its data is null!");
                return;
            }
            
            // Get the cell flags from MapComponent
            uint cellFlags = 0x0040; // Default to IsVisible only
            if (mapComponent.mapInformation.cells.dictionary.TryGetValue(cellId, out uint storedFlags))
            {
                cellFlags = storedFlags;
            }
            
            // Check if cell is walkable (bit 0 = 0 means walkable)
            bool isWalkable = (cellFlags & 0x0001) == 0;
            
            // Now create the cell properly
            var point = SceneConverter.GetSceneCoordByCellId(cellId)!;
            var posX = point.X;
            var posY = point.Y;

            var cell = new GameObject();
            cell.transform.SetParent(gameObject.transform);
            cell.transform.position = new Vector3(posX, posY, 0);

            var lr = cell.AddComponent<LineRenderer>();
            lr.sortingLayerName = "UI";
            lr.sortingOrder = 32700;
            lr.startWidth = 0.01f;
            lr.endWidth = 0.01f;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.name = $"Cell ({cellId}, {point.X}, {point.Y})";
            
            // Standard material and color for all cells as requested
            var standardMaterial = new Material(Shader.Find("Sprites/Default"));
            standardMaterial.color = new Color(1f, 1f, 1f, 31f/255f); // White with opacity of 31/255
            lr.material = standardMaterial;
            lr.positionCount = 5;
            
            // Set the positions for diamond shape for all cells
            lr.SetPosition(0, new Vector3(posX, posY, 0)); // Bottom left corner
            lr.SetPosition(1, new Vector3(posX + GridCameraController.CellWidth / 2f, posY + GridCameraController.CellHeight / 2f, 0)); // Top left corner
            lr.SetPosition(2, new Vector3(posX + GridCameraController.CellWidth, posY, 0)); // Top right corner
            lr.SetPosition(3, new Vector3(posX + GridCameraController.CellWidth / 2f, posY - GridCameraController.CellHeight / 2f, 0)); // Bottom right corner
            lr.SetPosition(4, new Vector3(posX, posY, 0)); // Back to bottom left to close the shape
            
            lr.startColor = new Color(1f, 1f, 1f, 1f);
            lr.endColor = new Color(1f, 1f, 1f, 1f);
            
            // Only add collider for walkable cells
            if (isWalkable)
            {
                var hitbox = cell.AddComponent<PolygonCollider2D>();
                var points = new Vector2[4];
                points[0] = new Vector2(0, 0); // Bottom left corner
                points[1] = new Vector2(GridCameraController.CellWidth / 2, GridCameraController.CellHeight / 2); // Top left corner
                points[2] = new Vector2(GridCameraController.CellWidth, 0); // Top right corner
                points[3] = new Vector2(GridCameraController.CellWidth / 2, -GridCameraController.CellHeight / 2); // Bottom right corner
                hitbox.SetPath(0, points);
            }

            var textObject = new GameObject();
            textObject.transform.SetParent(cell.transform);

            var cellComponent = cell.AddComponent<CellComponent>();
            cellComponent.CellId = cellId;
            
            // Create cell data with the appropriate walkability state
            short shortCellId = (short)cellId; // Explicit cast to short for Cell constructor
            var cellData = new MapCreator.Data.Models.Cell(shortCellId, (short)cellFlags); // Cast explicite de uint à short
            cellComponent.Cell = cellData;

            // Add the new cell to our list
            _cells.Add(cell);
        }
        
        /// <summary>
        /// Updates the visual representation of a cell
        /// </summary>
        private void UpdateCellVisual(ushort cellId, uint flags = 0)
        {
            Debug.Log($"[DATA_DEBUG] UpdateCellVisual: Mise à jour visuelle de la cellule {cellId}");
            
            // Find MapComponent to read data from
            var mapComponent = UnityEngine.Object.FindObjectOfType<Components.Maps.MapComponent>();
            if (mapComponent == null || mapComponent.mapInformation == null || mapComponent.mapInformation.cells == null)
            {
                Debug.LogError("[DATA_DEBUG] UpdateCellVisual: Cannot find MapComponent or its data is null!");
                return;
            }
            
            // Find the cell object - be explicit about type matching
            var cellObj = _cells.Find(c => {
                var component = c.GetComponent<CellComponent>();
                if (component == null) return false;
                return component.CellId == cellId;
            });
            
            // Get the cell flags from parameter or from MapComponent
            // Priority: 1. provided flags parameter, 2. MapComponent's data
            uint cellFlags = flags;
            if (cellFlags == 0) 
            {
                if (mapComponent.mapInformation.cells.dictionary.TryGetValue(cellId, out uint storedFlags))
                {
                    cellFlags = storedFlags;
                    Debug.Log($"[DATA_DEBUG] UpdateCellVisual: Cellule {cellId} - Found stored flags: {cellFlags} in MapComponent");
                }
                else
                {
                    // Not found in MapComponent, set default value with IsVisible flag (bit 6)
                    cellFlags = 0x0040; // Default to IsVisible only
                    Debug.Log($"[DATA_DEBUG] UpdateCellVisual: Cellule {cellId} - No stored flags found, using default: {cellFlags}");
                }
            }
            
            // Check if cell is walkable (bit 0 = 0 means walkable)
            bool isWalkable = (cellFlags & 0x0001) == 0;
            
            Debug.Log($"[DATA_DEBUG] UpdateCellVisual: Cellule {cellId} - isWalkable: {isWalkable}, flags: {cellFlags}");
            
            // If we found the cell object, destroy it and remove it from the list
            if (cellObj != null)
            {
                // Remove the cell from our list before destroying it
                _cells.Remove(cellObj);
                DestroyImmediate(cellObj);
                Debug.Log($"[DATA_DEBUG] UpdateCellVisual: Cellule {cellId} existante détruite");
            }
            
            // Now create a new cell with the appropriate properties
            CreateCellVisual(cellId);
            
            // Log success
            Debug.Log($"[DATA_DEBUG] Cell {cellId} updated successfully, walkable: {isWalkable}");
        }
        
        /// <summary>
        /// Synchronisation simple avec le MapComponent
        /// </summary>
        public void SyncWithMapComponent()
        {
            Debug.Log("[GridManager] SyncWithMapComponent is deprecated - GridManager now only reads from MapComponent");
            
            // This method shouldn't be used anymore, but keeping for compatibility
            // Might be needed for editor tools that expect this method to exist
        }
        
        /// <summary>
        /// Toggle the walkability of a cell by ID
        /// </summary>
        public void ToggleCellWalkability(ushort cellId)
        {
            Debug.Log($"[DATA_DEBUG] GridManager.ToggleCellWalkability - Toggle walkability pour cellule {cellId}");
            
            // Find MapComponent to modify its data
            var mapComponent = UnityEngine.Object.FindObjectOfType<Components.Maps.MapComponent>();
            if (mapComponent == null || mapComponent.mapInformation == null || mapComponent.mapInformation.cells == null)
            {
                Debug.LogError("[DATA_DEBUG] ToggleCellWalkability: Cannot find MapComponent or its data is null!");
                return;
            }
            
            // Get current cell flags from the MapComponent
            uint currentFlags = 0;
            if (mapComponent.mapInformation.cells.dictionary.TryGetValue(cellId, out uint flags))
            {
                currentFlags = flags;
            }
            
            // Toggle walkability
            bool isCurrentlyWalkable = (currentFlags & 0x0001) == 0;
            uint newFlags = isCurrentlyWalkable ? 
                (currentFlags | 0x0001) : // Rendre non-walkable
                (currentFlags & ~0x0001U); // Rendre walkable
            
            // Update the MapComponent directly
            if (mapComponent.mapInformation.cells.dictionary.ContainsKey(cellId))
            {
                mapComponent.mapInformation.cells.dictionary[cellId] = newFlags;
            }
            else
            {
                mapComponent.mapInformation.cells.dictionary.Add(cellId, newFlags);
            }
            
            Debug.Log($"[DATA_DEBUG] GridManager.ToggleCellWalkability - Cell {cellId} walkability toggled from {isCurrentlyWalkable} to {!isCurrentlyWalkable}, new flags: {newFlags}");
            
            // Forcer la recréation physique de la cellule avec UpdateCellVisual
            UpdateCellVisual(cellId, newFlags);
            
            // Notifier les écouteurs du changement
            OnCellModified?.Invoke(cellId, newFlags);
            
            Debug.Log($"[DATA_DEBUG] GridManager.ToggleCellWalkability - Cellule {cellId} mise à jour et notification envoyée");
            
            // Mark MapComponent as dirty in editor
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(mapComponent);
            #endif
        }
        
        /// <summary>
        /// Met à jour les flags d'une cellule
        /// </summary>
        /// <param name="cellId">ID de la cellule</param>
        /// <param name="flagsData">Nouveaux flags</param>
        public void UpdateCellFlags(ushort cellId, uint flagsData)
        {
            Debug.Log($"[DATA_DEBUG] GridManager.UpdateCellFlags - Mise à jour cellule {cellId} avec flags {flagsData}");

            // Find MapComponent to modify its data
            var mapComponent = UnityEngine.Object.FindObjectOfType<Components.Maps.MapComponent>();
            if (mapComponent == null || mapComponent.mapInformation == null || mapComponent.mapInformation.cells == null)
            {
                Debug.LogError("[DATA_DEBUG] UpdateCellFlags: Cannot find MapComponent or its data is null!");
                return;
            }

            // Mettre à jour les données directement dans le MapComponent
            if (mapComponent.mapInformation.cells.dictionary.ContainsKey(cellId))
            {
                mapComponent.mapInformation.cells.dictionary[cellId] = flagsData;
            }
            else
            {
                mapComponent.mapInformation.cells.dictionary.Add(cellId, flagsData);
            }
            
            // Mettre à jour la représentation visuelle
            UpdateCellVisual(cellId, flagsData);
            
            // Notifier les écouteurs du changement
            OnCellModified?.Invoke(cellId, flagsData);
            
            Debug.Log($"[DATA_DEBUG] GridManager.UpdateCellFlags - Cellule {cellId} mise à jour et notification envoyée");
            
            // Mark MapComponent as dirty in editor
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(mapComponent);
            #endif
        }
        
        /// <summary>
        /// Exporte les données de la grille vers un objet MapBasicInformation
        /// </summary>
        public MapBasicInformation ExportToMapBasicInformation()
        {
            Debug.Log("[GridManager] ExportToMapBasicInformation - Getting data from MapComponent");
            
            // Find MapComponent to read data from
            var mapComponent = UnityEngine.Object.FindObjectOfType<Components.Maps.MapComponent>();
            if (mapComponent == null || mapComponent.mapInformation == null)
            {
                Debug.LogError("[GridManager] ExportToMapBasicInformation - Cannot find MapComponent or its data");
                return new MapBasicInformation();
            }
            
            // Create a new MapBasicInformation but copy from MapComponent
            var mapInfo = new MapBasicInformation
            {
                id = mapComponent.mapInformation.id,
                leftNeighbourId = mapComponent.mapInformation.leftNeighbourId,
                rightNeighbourId = mapComponent.mapInformation.rightNeighbourId,
                topNeighbourId = mapComponent.mapInformation.topNeighbourId,
                bottomNeighbourId = mapComponent.mapInformation.bottomNeighbourId
            };
            
            // Initialize cells data structures if they don't exist
            if (mapInfo.cellsList == null)
            {
                mapInfo.cellsList = new List<Cell>();
            }
            
            if (mapInfo.cells == null)
            {
                mapInfo.cells = new CreatorMap.Scripts.Data.SerializableDictionary<ushort, uint>();
            }
            
            // Copy cell data from MapComponent
            if (mapComponent.mapInformation.cells != null && mapComponent.mapInformation.cells.dictionary != null)
            {
                foreach (var pair in mapComponent.mapInformation.cells.dictionary)
                {
                    // Add to the editor list
                    mapInfo.cellsList.Add(new Cell
                    {
                        id = pair.Key,
                        flags = (int)pair.Value // Cast from uint to int
                    });
                    
                    // Add to the dictionary
                    mapInfo.UpdateCellData(pair.Key, pair.Value);
                }
            }
            
            // Copy sprite data from gridManager
            if (mapInfo.SpriteData == null)
            {
                mapInfo.SpriteData = new MapSpriteData();
            }
            
            // Copy tiles data
            foreach (var kvp in _placedTiles)
            {
                mapInfo.SpriteData.tiles.Add(kvp.Value);
            }
            
            // Copy fixtures data
            foreach (var kvp in _placedFixtures)
            {
                mapInfo.SpriteData.fixtures.Add(kvp.Value);
            }
            
            Debug.Log($"[GridManager] ExportToMapBasicInformation - Exported {mapInfo.cellsList.Count} cells and {mapInfo.SpriteData.tiles.Count + mapInfo.SpriteData.fixtures.Count} sprites");
            
            return mapInfo;
        }
        
        /// <summary>
        /// Importe les données d'un objet MapBasicInformation vers la grille
        /// </summary>
        public void ImportFromMapBasicInformation(MapBasicInformation mapInfo)
        {
            Debug.Log("[GridManager] ImportFromMapBasicInformation - Importing to MapComponent");
            
            // Vérifier que les données sont valides
            if (mapInfo == null)
            {
                Debug.LogError("[GridManager] ImportFromMapBasicInformation - MapBasicInformation is null");
                return;
            }
            
            // Find MapComponent to write data to
            var mapComponent = UnityEngine.Object.FindObjectOfType<Components.Maps.MapComponent>();
            if (mapComponent == null)
            {
                Debug.LogError("[GridManager] ImportFromMapBasicInformation - Cannot find MapComponent");
                return;
            }
            
            // Create mapInformation if it doesn't exist
            if (mapComponent.mapInformation == null)
            {
                mapComponent.mapInformation = new MapBasicInformation();
            }
            
            // Update the ID and neighbors
            mapComponent.mapInformation.id = mapInfo.id;
            mapComponent.mapInformation.leftNeighbourId = mapInfo.leftNeighbourId;
            mapComponent.mapInformation.rightNeighbourId = mapInfo.rightNeighbourId;
            mapComponent.mapInformation.topNeighbourId = mapInfo.topNeighbourId;
            mapComponent.mapInformation.bottomNeighbourId = mapInfo.bottomNeighbourId;
            
            // For compatibility, also update gridData id
            gridData.id = mapInfo.id;
            
            // Initialize cells dictionary if needed
            if (mapComponent.mapInformation.cells == null)
            {
                mapComponent.mapInformation.cells = new CreatorMap.Scripts.Data.SerializableDictionary<ushort, uint>();
            }
            
            // Clear existing cells and sprites
            mapComponent.mapInformation.cells.dictionary.Clear();
            _placedTiles.Clear();
            _placedFixtures.Clear();
            
            // Import cells data
            if (mapInfo.cellsList != null && mapInfo.cellsList.Count > 0)
            {
                // Import from editor list
                foreach (var cell in mapInfo.cellsList)
                {
                    uint flagsUint = (uint)cell.flags;
                    mapComponent.mapInformation.cells.dictionary[(ushort)cell.id] = flagsUint;
                }
                
                Debug.Log($"[GridManager] Imported {mapInfo.cellsList.Count} cells from cellsList");
            }
            else if (mapInfo.cells != null && mapInfo.cells.dictionary.Count > 0)
            {
                // Import from dictionary
                foreach (var pair in mapInfo.cells.dictionary)
                {
                    uint flagsValue;
                    
                    // Convert value safely
                    if (pair.Value is uint uintValue)
                    {
                        flagsValue = uintValue;
                    }
                    else
                    {
                        flagsValue = Convert.ToUInt32(pair.Value);
                    }
                    
                    mapComponent.mapInformation.cells.dictionary[pair.Key] = flagsValue;
                }
                
                Debug.Log($"[GridManager] Imported {mapInfo.cells.dictionary.Count} cells from cells dictionary");
            }
            
            // Import sprites data
            if (mapInfo.SpriteData != null)
            {
                // Import tiles
                if (mapInfo.SpriteData.tiles != null)
                {
                    foreach (var tileData in mapInfo.SpriteData.tiles)
                    {
                        _placedTiles[tileData.Position] = tileData;
                        UpdateTileVisual(tileData);
                    }
                    
                    Debug.Log($"[GridManager] Imported {mapInfo.SpriteData.tiles.Count} tiles");
                }
                
                // Import fixtures
                if (mapInfo.SpriteData.fixtures != null)
                {
                    foreach (var fixtureData in mapInfo.SpriteData.fixtures)
                    {
                        _placedFixtures[fixtureData.Position] = fixtureData;
                        UpdateFixtureVisual(fixtureData);
                    }
                    
                    Debug.Log($"[GridManager] Imported {mapInfo.SpriteData.fixtures.Count} fixtures");
                }
            }
            
            // Force serialization
            if (mapComponent.mapInformation.cells != null)
            {
                mapComponent.mapInformation.cells.OnBeforeSerialize();
            }
            
            // Mark MapComponent as dirty
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(mapComponent);
            #endif
            
            // Recreate the grid
            CreateGrid();
            
            Debug.Log("[GridManager] ImportFromMapBasicInformation completed");
        }
        
        /// <summary>
        /// Ajoute un tile au dictionnaire des tiles placés
        /// </summary>
        public void AddTile(Vector2 position, TileSpriteData tileData)
        {
            _placedTiles[position] = tileData;
        }
        
        /// <summary>
        /// Ajoute un fixture au dictionnaire des fixtures placés
        /// </summary>
        public void AddFixture(Vector2 position, FixtureSpriteData fixtureData)
        {
            _placedFixtures[position] = fixtureData;
        }
        
        /// <summary>
        /// Supprime un tile de la position spécifiée
        /// </summary>
        public void RemoveTile(Vector2 position)
        {
            if (_placedTiles.ContainsKey(position))
            {
                _placedTiles.Remove(position);
            }
        }
        
        /// <summary>
        /// Supprime un fixture de la position spécifiée
        /// </summary>
        public void RemoveFixture(Vector2 position)
        {
            if (_placedFixtures.ContainsKey(position))
            {
                _placedFixtures.Remove(position);
            }
        }
        
        /// <summary>
        /// Met à jour la visualisation d'un tile
        /// </summary>
        private void UpdateTileVisual(TileSpriteData tileData)
        {
            // Trouver le GameObject correspondant au tile
            var tileObject = GameObject.Find(tileData.Id);
            if (tileObject != null)
            {
                var spriteRenderer = tileObject.GetComponent<SpriteRenderer>();
                if (spriteRenderer != null)
                {
                    // Utiliser Custom/ColorMatrixShader au lieu d'utiliser Addressables
                    var shader = Shader.Find("Custom/ColorMatrixShader");
                    if (shader != null)
                    {
                        var material = new Material(shader);
                        spriteRenderer.material = material;
                    }
                    else
                    {
                        // Fallback si le shader n'est pas trouvé
                        var defaultShader = Shader.Find("Sprites/Default");
                        spriteRenderer.material = new Material(defaultShader);
                    }
                }
            }
        }
        
        /// <summary>
        /// Met à jour la visualisation d'un fixture
        /// </summary>
        private void UpdateFixtureVisual(FixtureSpriteData fixtureData)
        {
            // Trouver le GameObject correspondant au fixture
            var fixtureObject = GameObject.Find(fixtureData.Id);
            if (fixtureObject != null)
            {
                var spriteRenderer = fixtureObject.GetComponent<SpriteRenderer>();
                if (spriteRenderer != null)
                {
                    // Utiliser Custom/ColorMatrixShader au lieu d'utiliser Addressables
                    var shader = Shader.Find("Custom/ColorMatrixShader");
                    if (shader != null)
                    {
                        var material = new Material(shader);
                        spriteRenderer.material = material;
                    }
                    else
                    {
                        // Fallback si le shader n'est pas trouvé
                        var defaultShader = Shader.Find("Sprites/Default");
                        spriteRenderer.material = new Material(defaultShader);
                    }
                }
            }
        }
        
        /// <summary>
        /// Actualise l'affichage complet de la grille
        /// </summary>
        private void RefreshGridDisplay()
        {
            // Mettre à jour visuellement l'ensemble de la grille
            foreach (var cellId in MapTools.EveryCellId)
            {
                UpdateCellVisual((ushort)cellId);
            }
        }

        // Methods for saving/loading grid data are no longer relevant as we're not storing data
        // Keep the method signatures for compatibility but log warnings

        // Méthode pour sauvegarder les données de la grille (à implémenter plus tard)
        public void SaveGridData()
        {
            Debug.LogWarning("[GridManager] SaveGridData is deprecated - All data is stored in MapComponent");
        }
        
        // Méthode pour charger les données de la grille (à implémenter plus tard)
        public void LoadGridData(int mapId)
        {
            Debug.LogWarning("[GridManager] LoadGridData is deprecated - All data is read from MapComponent");
            // Just update the local ID for compatibility
            gridData.id = mapId;
        }
        
        /// <summary>
        /// RefreshAfterPlayMode - Rebuilds the grid after exiting play mode using MapComponent data
        /// This method is called by GridManagerEditorHelper
        /// </summary>
        public void RefreshAfterPlayMode()
        {
            Debug.Log("[GridManager] RefreshAfterPlayMode - PRÉSERVATION EXACTE de l'état visuel après sortie du mode jeu");
            
            // Utiliser la même approche que pour RefreshGridVisualsInPlayMode
            // Ne jamais recréer complètement la grille
            
            // Find MapComponent to read data from
            var mapComponent = UnityEngine.Object.FindObjectOfType<Components.Maps.MapComponent>();
            if (mapComponent == null || mapComponent.mapInformation == null || mapComponent.mapInformation.cells == null)
            {
                Debug.LogError("[GridManager] RefreshAfterPlayMode - Cannot find MapComponent or its data is null!");
                return;
            }
            
            // NE PAS RECRÉER la grille - seulement mettre à jour les états visuels
            // Pour chaque cellule existante, vérifier qu'elle reflète correctement les données
            var allCells = GetComponentsInChildren<CellComponent>();
            Debug.Log($"[GridManager] RefreshAfterPlayMode - Mise à jour de {allCells.Length} cellules existantes");
            
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
                
                // Vérifier si nous devons ajouter/supprimer le collider pour refléter l'état walkable
                bool hasCollider = cellComponent.GetComponent<PolygonCollider2D>() != null;
                
                if (isWalkable && !hasCollider)
                {
                    // Ajouter le collider si la cellule doit être walkable
                    var hitbox = cellComponent.gameObject.AddComponent<PolygonCollider2D>();
                    var points = new Vector2[4];
                    points[0] = new Vector2(0, 0);
                    points[1] = new Vector2(GridCameraController.CellWidth / 2, GridCameraController.CellHeight / 2);
                    points[2] = new Vector2(GridCameraController.CellWidth, 0);
                    points[3] = new Vector2(GridCameraController.CellWidth / 2, -GridCameraController.CellHeight / 2);
                    hitbox.SetPath(0, points);
                    
                    cellsUpdated++;
                }
                else if (!isWalkable && hasCollider)
                {
                    // Supprimer le collider si la cellule ne doit pas être walkable
                    var collider = cellComponent.GetComponent<PolygonCollider2D>();
                    if (collider != null)
                    {
                        DestroyImmediate(collider);
                        cellsUpdated++;
                    }
                }
            }
            
            Debug.Log($"[GridManager] RefreshAfterPlayMode - {cellsUpdated} cellules mises à jour, état visuel exactement préservé");
        }
        
        /// <summary>
        /// Rebuilds the visual representation of the grid in play mode without resetting cell states
        /// Maintains the current walkability state of all cells
        /// </summary>
        public void RefreshGridVisualsInPlayMode()
        {
            Debug.Log("[GridManager] RefreshGridVisualsInPlayMode - PRÉSERVATION EXACTE de l'état visuel de la grille");
            
            // Find MapComponent to read data from
            var mapComponent = UnityEngine.Object.FindObjectOfType<Components.Maps.MapComponent>();
            if (mapComponent == null || mapComponent.mapInformation == null || mapComponent.mapInformation.cells == null)
            {
                Debug.LogError("[GridManager] RefreshGridVisualsInPlayMode - Cannot find MapComponent or its data is null!");
                return;
            }
            
            // NE PAS RECRÉER la grille - seulement mettre à jour les états visuels
            // Pour chaque cellule existante, vérifier qu'elle reflète correctement les données
            var allCells = GetComponentsInChildren<CellComponent>();
            Debug.Log($"[GridManager] RefreshGridVisualsInPlayMode - Mise à jour de {allCells.Length} cellules existantes");
            
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
                
                // Vérifier si nous devons ajouter/supprimer le collider pour refléter l'état walkable
                bool hasCollider = cellComponent.GetComponent<PolygonCollider2D>() != null;
                
                if (isWalkable && !hasCollider)
                {
                    // Ajouter le collider si la cellule doit être walkable
                    var hitbox = cellComponent.gameObject.AddComponent<PolygonCollider2D>();
                    var points = new Vector2[4];
                    points[0] = new Vector2(0, 0);
                    points[1] = new Vector2(GridCameraController.CellWidth / 2, GridCameraController.CellHeight / 2);
                    points[2] = new Vector2(GridCameraController.CellWidth, 0);
                    points[3] = new Vector2(GridCameraController.CellWidth / 2, -GridCameraController.CellHeight / 2);
                    hitbox.SetPath(0, points);
                    
                    Debug.Log($"[GridManager] RefreshGridVisualsInPlayMode - Cell {cellId}: Ajout du collider (walkable)");
                }
                else if (!isWalkable && hasCollider)
                {
                    // Supprimer le collider si la cellule ne doit pas être walkable
                    var collider = cellComponent.GetComponent<PolygonCollider2D>();
                    DestroyImmediate(collider);
                    
                    Debug.Log($"[GridManager] RefreshGridVisualsInPlayMode - Cell {cellId}: Suppression du collider (non-walkable)");
                }
            }
            
            Debug.Log("[GridManager] RefreshGridVisualsInPlayMode - État visuel de la grille préservé avec succès");
        }
    }
}

