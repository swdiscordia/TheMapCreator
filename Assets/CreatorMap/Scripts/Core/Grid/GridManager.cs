using System.Collections.Generic;
using Components.Maps;
using Managers.Cameras;
using Managers.Scene;
using Models.Maps;
using Unity.VisualScripting;
using UnityEngine;
using MapCreator.Data.Models;

namespace Managers.Maps.MapCreator
{
    public class MapCreatorGridManager : MonoBehaviour
    {
        public static MapCreatorGridManager Instance;
        
        private readonly List<GameObject> _cells = new(560);
    

        // Start is called before the first frame update
        void Start()
        {
            CreateGrid();
        }

        private void Awake()
        {
            Instance = this;
        }

        public void CreateGrid()
        {
            // Clear any existing cells
            foreach (var cell in _cells)
            {
                if (!cell.IsDestroyed())
                {
                    Destroy(cell);
                }
            }
            
            _cells.Clear(); // Clear the list to avoid stale references
            
            // Get map data if available
            MapComponent mapComponent = GetComponent<MapComponent>();
            MapBasicInformation mapInfo = null;
            if (mapComponent != null)
            {
                mapInfo = mapComponent.mapInformation;
                Debug.Log($"Found map component with ID: {mapInfo.id}, cells count: {mapInfo.cells.dictionary.Count}");
            }
            else
            {
                Debug.LogWarning("No map component found, using default cell data");
            }
            
            // Create all cells
            foreach (var cellId in MapTools.EveryCellId)
            {
                CreateCell(cellId, mapInfo);
            }
            
            Debug.Log($"Grid creation complete. Created {_cells.Count} cells.");
        }

        private void CreateCell(short cellId, MapBasicInformation mapInfo = null)
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
            ushort cellData = 0;
            
            // Check if we have saved data for this cell
            if (mapInfo != null && mapInfo.cells != null && mapInfo.cells.dictionary != null)
            {
                if (mapInfo.cells.dictionary.TryGetValue((ushort)cellId, out cellData))
                {
                    // If CELL_WALKABLE bit is set, cell is NOT walkable (backwards from what you'd expect)
                    const ushort CELL_WALKABLE = 0x0001;
                    isWalkable = (cellData & CELL_WALKABLE) == 0;
                    
                    // Add debug logging for problematic cells
                    if (!isWalkable)
                    {
                        Debug.Log($"Cell {cellId} has data {cellData}, CELL_WALKABLE bit is SET, so it is NON-walkable");
                    }
                }
            }

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
                
                Debug.Log($"Created non-walkable cell {cellId} with 2-point LineRenderer at {posX}, {posY}");
            }

            var textObject = new GameObject();
            textObject.transform.SetParent(cell.transform);

            var cellComponent = cell.AddComponent<CellComponent>();
            cellComponent.CellId = cellId;
            
            // Create cell data with the appropriate walkability state
            var cellObj = new Cell(cellId, (short)cellData);
            cellComponent.Cell = cellObj;

            _cells.Add(cell);
        }

        // Update is called once per frame
        public void UpdateCells(Map map)
        {
            if (map == null)
            {
                Debug.LogWarning("Attempted to update cells with null map");
                return;
            }

            CreateGrid();
        }
    }
}
