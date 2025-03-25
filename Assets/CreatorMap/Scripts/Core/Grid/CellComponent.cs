using UnityEngine;
using MapCreator.Data.Models;
using CreatorMap.Scripts.Core.Grid;

namespace Components.Maps
{
    /// <summary>
    /// Component attached to each cell GameObject on the grid
    /// </summary>
    public class CellComponent : MonoBehaviour
    {
        /// <summary>
        /// The cell's ID
        /// </summary>
        public ushort CellId { get; set; }
        
        /// <summary>
        /// Reference to the cell data
        /// </summary>
        private Cell _cell;
        public Cell Cell 
        { 
            get { return _cell; }
            set 
            { 
                if (_cell != value)
                {
                    Debug.Log($"[DATA_DEBUG] Cell data changed for cell {CellId}. Old data: {(_cell != null ? _cell.Data : "null")}, New data: {(value != null ? value.Data : "null")}");
                    _cell = value;
                }
            }
        }
        
        /// <summary>
        /// Color of the cell when highlighted
        /// </summary>
        public Color HighlightColor { get; set; } = Color.yellow;
        
        /// <summary>
        /// Is the cell currently highlighted
        /// </summary>
        public bool IsHighlighted { get; private set; }
        
        /// <summary>
        /// The line renderer component
        /// </summary>
        private LineRenderer _lineRenderer;
        
        /// <summary>
        /// The original material color before highlighting
        /// </summary>
        private Color _originalColor;
        
        /// <summary>
        /// Is the cell walkable
        /// </summary>
        public bool IsWalkable
        {
            get
            {
                if (Cell == null) return true; // Default to walkable
                // If CELL_WALKABLE bit is set, cell is NOT walkable (backwards from what you'd expect)
                const ushort CELL_WALKABLE = 0x0001;
                return (Cell.Data & CELL_WALKABLE) == 0;
            }
        }
        
        private void Awake()
        {
            _lineRenderer = GetComponent<LineRenderer>();
            if (_lineRenderer != null)
            {
                _originalColor = _lineRenderer.material.color;
            }
            Debug.Log($"[DATA_DEBUG] CellComponent {CellId} initialized. IsWalkable: {IsWalkable}");
        }
        
        private void OnMouseDown()
        {
            Debug.Log($"[DATA_DEBUG] Mouse down on cell {CellId}");
            // When clicked, toggle walkability of this cell
            ToggleWalkability();
        }
        
        /// <summary>
        /// Toggle the walkability of this cell
        /// </summary>
        public void ToggleWalkability()
        {
            Debug.Log($"[DATA_DEBUG] CellComponent.ToggleWalkability called for cell {CellId}. Current IsWalkable: {IsWalkable}");
            // Find the grid manager
            var gridManager = FindObjectOfType<MapCreatorGridManager>();
            if (gridManager != null)
            {
                // Toggle the cell walkability
                gridManager.ToggleCellWalkability(CellId);
                Debug.Log($"[DATA_DEBUG] Called ToggleCellWalkability on GridManager for cell {CellId}");
            }
            else
            {
                Debug.LogError("[DATA_DEBUG] Could not find MapCreatorGridManager to toggle cell walkability");
            }
        }
        
        /// <summary>
        /// Highlight the cell with the specified color
        /// </summary>
        public void Highlight(Color? color = null)
        {
            if (_lineRenderer != null)
            {
                IsHighlighted = true;
                var highlightColor = color ?? HighlightColor;
                _lineRenderer.material.color = highlightColor;
                _lineRenderer.startColor = highlightColor;
                _lineRenderer.endColor = highlightColor;
            }
        }
        
        /// <summary>
        /// Reset the cell highlight
        /// </summary>
        public void ResetHighlight()
        {
            if (_lineRenderer != null)
            {
                IsHighlighted = false;
                _lineRenderer.material.color = _originalColor;
                _lineRenderer.startColor = Color.white;
                _lineRenderer.endColor = Color.white;
            }
        }
    }
} 