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
        
        // Données de la grille
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
            
            // Initialize or restore the dictionary
            if (gridData.cellsDict == null)
            {
                gridData.cellsDict = new Dictionary<ushort, uint>();
            }
            
            // Only initialize from cells if dictionary is empty
            if (gridData.cellsDict.Count == 0)
            {
                foreach (var cell in gridData.cells)
                {
                    gridData.cellsDict[cell.cellId] = cell.flags;
                }
            }
            
            Debug.Log($"[DATA_DEBUG] Awake: Dictionnaire initialisé avec {gridData.cells.Count} cellules");
            
            // Ajuster la position Y de la caméra à 2.7
            var camera = Camera.main;
            if (camera != null)
            {
                Vector3 position = camera.transform.position;
                camera.transform.position = new Vector3(position.x, 2.7f, position.z);
            }
            
            // Don't sync in Awake, let GridDataSync handle it
        }

        public void CreateGrid()
        {
            Debug.Log("[DATA_DEBUG] Début de CreateGrid");
            
            // Clear any existing cells
            foreach (var cell in _cells)
            {
                if (cell != null)
                {
                    DestroyImmediate(cell);
                }
            }
            
            _cells.Clear(); // Clear the list to avoid stale references
            Debug.Log("[DATA_DEBUG] CreateGrid: Toutes les cellules existantes ont été supprimées");
            
            // Create all cells
            foreach (var cellId in MapTools.EveryCellId)
            {
                CreateCell((ushort)cellId);
            }
            
            Debug.Log($"[DATA_DEBUG] CreateGrid: {_cells.Count} cellules ont été créées");
            
            // Synchroniser avec MapComponent
            Debug.Log("[DATA_DEBUG] CreateGrid: Appel à SyncWithMapComponent");
            SyncWithMapComponent();
            
            Debug.Log("[DATA_DEBUG] Fin de CreateGrid");
        }

        private void CreateCell(ushort cellId)
        {
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

            // All cells are walkable by default unless data says otherwise
            bool isWalkable = true;
            uint cellData = 0;
            
            // Check if we have saved data for this cell
            if (gridData != null && gridData.cellsDict.Count > 0)
            {
                if (gridData.cellsDict.TryGetValue(cellId, out cellData))
                {
                    // Force all cells to be walkable by ensuring bit 0 is 0
                    cellData &= 0xFFFFFFFE; // Clear bit 0 using AND with all 1s except bit 0
                    // Update the data in the dictionary
                    gridData.cellsDict[cellId] = cellData;
                }
            }
            Debug.Log($"[GRID] Creating walkable cell {cellId} for proper tile placement");

            // Apply different visualization for walkable vs non-walkable cells
            if (isWalkable)
            {
                // Walkable cells have a diamond shape with 5 points
                var transparentMaterial = new Material(Shader.Find("Sprites/Default"))
                {
                    color = new Color(1, 1, 1, 0.12f) // Walkable: White with low opacity
                };
                
                lr.material = transparentMaterial;
                lr.positionCount = 5;
                
                // Set the positions for diamond shape
                lr.SetPosition(0, new Vector3(posX, posY, 0)); // Bottom left corner
                lr.SetPosition(1, new Vector3(posX + GridCameraController.CellWidth / 2f, posY + GridCameraController.CellHeight / 2f, 0)); // Top left corner
                lr.SetPosition(2, new Vector3(posX + GridCameraController.CellWidth, posY, 0)); // Top right corner
                lr.SetPosition(3, new Vector3(posX + GridCameraController.CellWidth / 2f, posY - GridCameraController.CellHeight / 2f, 0)); // Bottom right corner
                lr.SetPosition(4, new Vector3(posX, posY, 0)); // Back to bottom left to close the shape
                
                lr.startColor = new Color(1, 1, 1, 1f);
                lr.endColor = new Color(1, 1, 1, 1f);
                
                // Only add collider for walkable cells
                var hitbox = cell.AddComponent<PolygonCollider2D>();
                var points = new Vector2[4];
                points[0] = new Vector2(0, 0); // Bottom left corner
                points[1] = new Vector2(GridCameraController.CellWidth / 2, GridCameraController.CellHeight / 2); // Top left corner
                points[2] = new Vector2(GridCameraController.CellWidth, 0); // Top right corner
                points[3] = new Vector2(GridCameraController.CellWidth / 2, -GridCameraController.CellHeight / 2); // Bottom right corner
                hitbox.SetPath(0, points);
            }
            else
            {
                // Non-walkable cells have just 2 points
                var transparentMaterial = new Material(Shader.Find("Sprites/Default"))
                {
                    color = new Color(1, 1, 1, 0.12f) // Keep the same color as walkable cells
                };
                
                lr.material = transparentMaterial;
                lr.positionCount = 2;
                
                // Set points relative to cell position, not global coordinates
                // This is critical for the non-walkable cells to display correctly
                lr.SetPosition(0, new Vector3(0, 0, 0)); // Local origin
                lr.SetPosition(1, new Vector3(0, 0, 1)); // 1 unit up in Z direction locally
                
                lr.startColor = new Color(1, 1, 1, 1f);
                lr.endColor = new Color(1, 1, 1, 1f);
                
                // No collider for non-walkable cells
            }

            var textObject = new GameObject();
            textObject.transform.SetParent(cell.transform);

            var cellComponent = cell.AddComponent<CellComponent>();
            cellComponent.CellId = cellId;
            
            // Create cell data with the appropriate walkability state
            short shortCellId = (short)cellId; // Explicit cast to short for Cell constructor
            var cellObj = new MapCreator.Data.Models.Cell(shortCellId, (short)cellData); // Cast explicite de uint à short
            cellComponent.Cell = cellObj;

            _cells.Add(cell);
        }
        
        /// <summary>
        /// Synchronisation simple avec le MapComponent
        /// </summary>
        private void SyncWithMapComponent()
        {
            Debug.Log($"[DATA_DEBUG] Début de synchronisation avec MapComponent");
            
            var mapComponent = UnityEngine.Object.FindFirstObjectByType<Components.Maps.MapComponent>();
            if (mapComponent == null || mapComponent.mapInformation == null)
            {
                Debug.LogError("[DATA_DEBUG] MapComponent ou mapInformation est null");
                return;
            }
                
            // Synchroniser l'ID
            mapComponent.mapInformation.id = gridData.id;
            Debug.Log($"[DATA_DEBUG] ID de la map synchronisé: {gridData.id}");
            
            // Méthode 1 : Utilisation directe du dictionary interne
            try {
                if (mapComponent.mapInformation.cells == null)
                {
                    mapComponent.mapInformation.cells = new SerializableDictionary<ushort, uint>();
                    Debug.Log("[DATA_DEBUG] Nouveau SerializableDictionary créé");
                }
                
                // Vider complètement le dictionnaire existant
                mapComponent.mapInformation.cells.dictionary.Clear();
                Debug.Log($"[DATA_DEBUG] Dictionary vidé, taille actuelle: {mapComponent.mapInformation.cells.dictionary.Count}");
                
                // Ajouter directement les cellules dans le dictionary interne
                foreach (var cell in gridData.cells)
                {
                    mapComponent.mapInformation.cells.dictionary[cell.cellId] = cell.flags;
                }
                
                Debug.Log($"[DATA_DEBUG] Cellules ajoutées au dictionary: {mapComponent.mapInformation.cells.dictionary.Count}");
                
                // Forcer la sérialisation pour mettre à jour les listes internes
                mapComponent.mapInformation.cells.OnBeforeSerialize();
                Debug.Log("[DATA_DEBUG] OnBeforeSerialize appelé");
                
                // Vérifier que les listes internes sont bien remplies
                var serializableDictType = mapComponent.mapInformation.cells.GetType();
                var keysField = serializableDictType.GetField("keys", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var valuesField = serializableDictType.GetField("values", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (keysField != null && valuesField != null)
                {
                    var keys = keysField.GetValue(mapComponent.mapInformation.cells) as List<ushort>;
                    var values = valuesField.GetValue(mapComponent.mapInformation.cells) as List<uint>;
                    
                    if (keys != null && values != null)
                    {
                        Debug.Log($"[DATA_DEBUG] Taille des listes internes - keys: {keys.Count}, values: {values.Count}");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[DATA_DEBUG] Erreur lors de la synchronisation du dictionnaire: {e.Message}\n{e.StackTrace}");
            }
            
            // Recréer la liste d'édition
            try {
                mapComponent.mapInformation.cellsList = new List<CreatorMap.Scripts.Data.Cell>();
                foreach (var cell in gridData.cells)
                {
                    mapComponent.mapInformation.cellsList.Add(new CreatorMap.Scripts.Data.Cell
                    {
                        id = cell.cellId,
                        flags = (int)cell.flags
                    });
                }
                
                Debug.Log($"[DATA_DEBUG] Taille de cellsList après synchronisation: {mapComponent.mapInformation.cellsList.Count}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[DATA_DEBUG] Erreur lors de la synchronisation de cellsList: {e.Message}");
            }
            
            // Marquer comme modifié
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(mapComponent);
            Debug.Log("[DATA_DEBUG] MapComponent marqué comme dirty");
            #endif
            
            Debug.Log("[DATA_DEBUG] Fin de synchronisation avec MapComponent");
        }
        
        /// <summary>
        /// Toggle the walkability of a cell by ID
        /// </summary>
        public void ToggleCellWalkability(ushort cellId)
        {
            Debug.Log($"[DATA_DEBUG] GridManager.ToggleCellWalkability - Toggle walkability pour cellule {cellId}");
            
            // Trouver la cellule
            var cellObj = _cells.Find(c => {
                var component = c.GetComponent<CellComponent>();
                if (component == null) return false;
                return component.CellId == cellId;
            });
            
            if (cellObj == null)
            {
                Debug.LogError($"[DATA_DEBUG] GridManager.ToggleCellWalkability - Cellule {cellId} non trouvée!");
                return;
            }
            
            CellComponent cellComponent = cellObj.GetComponent<CellComponent>();
            
            if (cellComponent == null)
            {
                Debug.LogError($"[DATA_DEBUG] GridManager.ToggleCellWalkability - CellComponent non trouvé pour cellule {cellId}!");
                return;
            }
            
            // Basculer l'état walkable en fonction des données actuelles
            uint currentFlags = gridData.cellsDict.ContainsKey(cellId) ? gridData.cellsDict[cellId] : 0;
            bool isCurrentlyWalkable = (currentFlags & 0x0001) == 0;
            uint newFlags = isCurrentlyWalkable ? 
                (currentFlags | 0x0001) : // Rendre non-walkable
                (currentFlags & ~0x0001U); // Rendre walkable
            
            // Mettre à jour les flags et déclencher les événements
            UpdateCellFlags(cellId, newFlags);
        }
        
        /// <summary>
        /// Met à jour les flags d'une cellule
        /// </summary>
        /// <param name="cellId">ID de la cellule</param>
        /// <param name="flagsData">Nouveaux flags</param>
        public void UpdateCellFlags(ushort cellId, uint flagsData)
        {
            Debug.Log($"[DATA_DEBUG] GridManager.UpdateCellFlags - Mise à jour cellule {cellId} avec flags {flagsData}");

            if (gridData == null || gridData.cellsDict == null)
            {
                Debug.LogError("[DATA_DEBUG] GridManager.UpdateCellFlags - gridData ou cellsDict est null!");
                return;
            }

            // Mettre à jour les données dans le dictionnaire
            gridData.cellsDict[(ushort)cellId] = flagsData;
            
            // Mettre à jour la représentation visuelle
            UpdateCellVisual(cellId, flagsData);
            
            // Notifier les écouteurs du changement
            OnCellModified?.Invoke(cellId, flagsData);
            
            Debug.Log($"[DATA_DEBUG] GridManager.UpdateCellFlags - Cellule {cellId} mise à jour et notification envoyée");
        }
        
        /// <summary>
        /// Exporte les données de la grille vers un objet MapBasicInformation
        /// </summary>
        public MapBasicInformation ExportToMapBasicInformation()
        {
            var mapInfo = new MapBasicInformation
            {
                id = gridData.id
            };
            
            // Convertir les cellData en format compatible
            foreach (var cellData in gridData.cells)
            {
                // Ajouter à la liste des cellules pour l'éditeur
                mapInfo.cellsList.Add(new Cell
                {
                    id = cellData.cellId,
                    flags = (int)cellData.flags // Cast explicite de uint à int
                });
                
                // Ajouter au dictionnaire pour le projet principal (déjà en uint)
                mapInfo.UpdateCellData(cellData.cellId, cellData.flags); // No cast needed as UpdateCellData expects uint
            }
            
            // Convertir les données des sprites
            if (mapInfo.SpriteData == null)
            {
                mapInfo.SpriteData = new MapSpriteData();
            }
            
            // Ajouter les tiles normaux
            foreach (var kvp in _placedTiles)
            {
                mapInfo.SpriteData.tiles.Add(kvp.Value);
            }
            
            // Ajouter les fixtures
            foreach (var kvp in _placedFixtures)
            {
                mapInfo.SpriteData.fixtures.Add(kvp.Value);
            }
            
            return mapInfo;
        }
        
        /// <summary>
        /// Importe les données d'un objet MapBasicInformation vers la grille
        /// </summary>
        public void ImportFromMapBasicInformation(MapBasicInformation mapInfo)
        {
            // Vérifier que les données sont valides
            if (mapInfo == null)
            {
                Debug.LogError("MapBasicInformation is null, cannot import data");
                return;
            }
            
            // Mettre à jour l'ID de la grille
            gridData.id = mapInfo.id;
            
            // Vider les données actuelles
            gridData.cells.Clear();
            gridData.cellsDict.Clear();
            _placedTiles.Clear();
            _placedFixtures.Clear();
            
            // Importer les données des cellules depuis les deux sources possibles
            
            // 1. D'abord, essayer d'utiliser la liste pour l'éditeur
            if (mapInfo.cellsList != null && mapInfo.cellsList.Count > 0)
            {
                foreach (var cell in mapInfo.cellsList)
                {
                    // Ajouter chaque cellule aux données de la grille
                    uint flagsUint = (uint)cell.flags;
                    var cellData = new CellData((ushort)cell.id, flagsUint);
                    gridData.cells.Add(cellData);
                    gridData.cellsDict[(ushort)cell.id] = flagsUint;
                    
                    // Mettre à jour la visualisation de la cellule
                    UpdateCellVisual((ushort)cell.id);
                }
            }
            // 2. Sinon, utiliser le dictionnaire compatible avec le projet principal
            else if (mapInfo.cells != null && mapInfo.cells.dictionary.Count > 0)
            {
                foreach (var pair in mapInfo.cells.dictionary)
                {
                    // Convertir les valeurs de manière sûre
                    ushort cellId = pair.Key;
                    uint flagsValue;
                    
                    // Gestion du type de pair.Value qui pourrait être int, uint ou autre
                    if (pair.Value is uint uintValue)
                    {
                        flagsValue = uintValue;
                    }
                    else
                    {
                        // Conversion générique avec Convert
                        flagsValue = Convert.ToUInt32(pair.Value);
                    }
                    
                    // Ajouter chaque cellule aux données de la grille
                    var cellData = new CellData(cellId, flagsValue);
                    gridData.cells.Add(cellData);
                    gridData.cellsDict[cellId] = flagsValue;
                    
                    // Mettre à jour la visualisation de la cellule
                    UpdateCellVisual(cellId);
                }
            }
            
            // Importer les données des sprites si disponibles
            if (mapInfo.SpriteData != null)
            {
                // Importer les tiles
                if (mapInfo.SpriteData.tiles != null)
                {
                    foreach (var tileData in mapInfo.SpriteData.tiles)
                    {
                        _placedTiles[tileData.Position] = tileData;
                        // Mettre à jour la visualisation des tiles
                        UpdateTileVisual(tileData);
                    }
                }
                
                // Importer les fixtures
                if (mapInfo.SpriteData.fixtures != null)
                {
                    foreach (var fixtureData in mapInfo.SpriteData.fixtures)
                    {
                        _placedFixtures[fixtureData.Position] = fixtureData;
                        // Mettre à jour la visualisation des fixtures
                        UpdateFixtureVisual(fixtureData);
                    }
                }
            }
            
            // Recréer la grille pour refléter les changements
            CreateGrid();
            
            // Synchroniser avec MapComponent après l'import
            SyncWithMapComponent();
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

        /// <summary>
        /// Updates the visual representation of a cell
        /// </summary>
        private void UpdateCellVisual(ushort cellId, uint flags = 0)
        {
            Debug.Log($"[DATA_DEBUG] Mise à jour visuelle de la cellule {cellId}");
            
            // Find the cell object - be explicit about type matching
            var cellObj = _cells.Find(c => {
                var component = c.GetComponent<CellComponent>();
                if (component == null) return false;
                return component.CellId == cellId;
            });
            
            if (cellObj != null)
            {
                var cellComponent = cellObj.GetComponent<CellComponent>();
                if (cellComponent != null)
                {
                    // Get the cell flags from parameter or dictionary
                    uint cellFlags = flags > 0 ? flags : (gridData.cellsDict.TryGetValue(cellId, out uint storedFlags) ? storedFlags : 0);
                    
                    // Update the cell's appearance based on flags
                    bool isWalkable = (cellFlags & 0x0001) == 0;
                    
                    Debug.Log($"[DATA_DEBUG] Cellule {cellId} - isWalkable: {isWalkable}, flags: {cellFlags}");
                    
                    // Get the LineRenderer
                    var lineRenderer = cellObj.GetComponent<LineRenderer>();
                    if (lineRenderer != null)
                    {
                        // Update visibility or color based on flags
                        lineRenderer.startColor = isWalkable ? 
                            new Color(1, 1, 1, 0.12f) : 
                            new Color(1, 0, 0, 0.25f);
                        
                        lineRenderer.endColor = lineRenderer.startColor;
                        Debug.Log($"[DATA_DEBUG] LineRenderer de la cellule {cellId} mis à jour");
                    }
                    
                    // Recreate this cell to apply visual changes
                    CreateCell(cellId);
                    Debug.Log($"[DATA_DEBUG] Cellule {cellId} recréée");
                }
                else
                {
                    Debug.LogWarning($"[DATA_DEBUG] CellComponent non trouvé pour la cellule {cellId}");
                }
            }
            else
            {
                Debug.LogWarning($"[DATA_DEBUG] Objet de cellule non trouvé pour la cellule {cellId}");
            }
        }

        // Méthode pour sauvegarder les données de la grille (à implémenter plus tard)
        public void SaveGridData()
        {
            // TODO: Implémenter la sauvegarde des données
        }
        
        // Méthode pour charger les données de la grille (à implémenter plus tard)
        public void LoadGridData(int mapId)
        {
            // TODO: Implémenter le chargement des données
            gridData.id = mapId;
        }
    }
}
