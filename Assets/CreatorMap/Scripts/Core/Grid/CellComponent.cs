using UnityEngine;
using MapCreator.Data.Models;

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
        public short CellId { get; set; }
        
        /// <summary>
        /// Reference to the cell data
        /// </summary>
        public Cell Cell { get; set; }
        
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
        
        private void Awake()
        {
            _lineRenderer = GetComponent<LineRenderer>();
            if (_lineRenderer != null)
            {
                _originalColor = _lineRenderer.material.color;
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