using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Managers.Cameras
{
    public class GridCameraController : MonoBehaviour
    {      
        public const float CellWidth = 0.86f;
        public const float CellHeight = 0.43f;

        public const float MaxWidth = CellWidth * 14;
        public const float MaxHeight = CellHeight * 20;

        private int _lastScreenWidth;
        private int _lastScreenHeight;
        
        // Hard-coded camera X position to ensure consistency
        private const float FixedCameraX = 6.2f;
        private const float FixedCameraY = 2.7f;
        private const float FixedCameraZ = -10f;

        // Start is called before the first frame update
        private GameObject _grid;     // The grid object
        public float zoomFactor = 1f; // The amount to zoom in or out
        
        
        // Call AdjustCamera in Start to set the initial camera settings
        async void Start()
        {     
            while (_grid == null)
            {
                _grid = GameObject.Find("GridManager");  // Changed from "Map" to "GridManager"
                if (_grid != null)
                {
                    AdjustCamera();
                    AdjustZoom();
                    // Force position to fixed coordinates
                    transform.position = new Vector3(FixedCameraX, FixedCameraY, FixedCameraZ);
                }
                
                await UniTask.Yield();
            }
        }
        
        // Adjust the camera's position and zoom level based on the grid size
        public void AdjustCamera()
        {
            // Set the camera's position to fixed coordinates
            transform.position = new Vector3(FixedCameraX, FixedCameraY, FixedCameraZ);
        }

        private void AdjustZoom()
        {        
            const float gridWidth  = MaxWidth + 10;
            const float gridHeight = MaxHeight + 2;

            // Get the camera component
            var gridCamera = GetComponent<UnityEngine.Camera>();

            // Calculate the aspect ratio
            var         screenRatio = Screen.width / (float)Screen.height;
            const float targetRatio = gridWidth / gridHeight;

            // Adjust the zoom level factor as needed
            const float zoomLevelFactor = 2.5f;

            if (screenRatio >= targetRatio)
            {
                // Screen or window is wider than the target: letterbox on the sides
                gridCamera.orthographicSize = gridHeight / zoomLevelFactor;
            }
            else
            {
                // Screen or window is narrower than the target: letterbox on the top and bottom
                var differenceInSize = targetRatio / screenRatio;
                gridCamera.orthographicSize = gridHeight / zoomLevelFactor * differenceInSize;
            }
        }

        // Update is called once per frame
        void Update()
        {
            if (Screen.width == _lastScreenWidth && Screen.height == _lastScreenHeight)
            {
                return;
            }

            _lastScreenWidth  = Screen.width;
            _lastScreenHeight = Screen.height;
            AdjustZoom();
            
            // Always maintain the fixed position
            var pos = transform.position;
            if (Mathf.Abs(pos.x - FixedCameraX) > 0.01f || 
                Mathf.Abs(pos.y - FixedCameraY) > 0.01f || 
                Mathf.Abs(pos.z - FixedCameraZ) > 0.01f)
            {
                transform.position = new Vector3(FixedCameraX, FixedCameraY, FixedCameraZ);
            }
        }
    }
}

