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

        private void CreateGrid()
        {
            // Clear any existing cells
            foreach (var cell in _cells)
            {
                if (!cell.IsDestroyed())
                {
                    Destroy(cell);
                }
            }
            
            // Create all cells
            foreach (var cellId in MapTools.EveryCellId)
            {
                CreateCell(cellId);
            }
        }

        private void CreateCell(short cellId)
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

            // All cells are walkable by default
            var transparentMaterial = new Material(Shader.Find("Sprites/Default"))
            {
                color = new Color(1, 1, 1, 0.12f), // White with 50% opacity
            };

            lr.positionCount = 5;
            lr.material = transparentMaterial;

            // Set the positions
            lr.SetPosition(0, new Vector3(posX, posY, 0)); // Bottom left corner
            lr.SetPosition(1, new Vector3(posX + GridCameraController.CellWidth / 2f, posY + GridCameraController.CellHeight / 2f, 0)); // Top left corner
            lr.SetPosition(2, new Vector3(posX + GridCameraController.CellWidth, posY, 0)); // Top right corner
            lr.SetPosition(3, new Vector3(posX + GridCameraController.CellWidth / 2f, posY - GridCameraController.CellHeight / 2f, 0)); // Bottom right corner
            lr.SetPosition(4, new Vector3(posX, posY, 0)); // Back to bottom left to close the shape

            lr.startColor = new Color(1, 1, 1, 1f);
            lr.endColor = new Color(1, 1, 1, 1f);

            var hitbox = cell.AddComponent<PolygonCollider2D>();
            var points = new Vector2[4];
            points[0] = new Vector2(0, 0); // Bottom left corner
            points[1] = new Vector2(GridCameraController.CellWidth / 2, GridCameraController.CellHeight / 2); // Top left corner
            points[2] = new Vector2(GridCameraController.CellWidth, 0); // Top right corner
            points[3] = new Vector2(GridCameraController.CellWidth / 2, -GridCameraController.CellHeight / 2); // Bottom right corner
            hitbox.SetPath(0, points);

            var textObject = new GameObject();
            textObject.transform.SetParent(cell.transform);

            var cellComponent = cell.AddComponent<CellComponent>();
            cellComponent.CellId = cellId;
            // Create a basic cell data with walkable state (data = 0 means walkable)
            var cellData = new Cell(cellId, 0);
            cellComponent.Cell = cellData;

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
