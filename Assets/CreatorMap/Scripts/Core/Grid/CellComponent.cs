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
        public bool IsHighlighted { get; set; }
        
        /// <summary>
        /// The sprite renderer component
        /// </summary>
        private SpriteRenderer _renderer;
        
        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
        }
        
        /// <summary>
        /// Highlight the cell with the specified color
        /// </summary>
        public void Highlight(Color? color = null)
        {
            if (_renderer != null)
            {
                IsHighlighted = true;
                _renderer.color = color ?? HighlightColor;
            }
        }
        
        /// <summary>
        /// Reset the cell highlight
        /// </summary>
        public void ResetHighlight()
        {
            if (_renderer != null)
            {
                IsHighlighted = false;
                _renderer.color = Color.white;
            }
        }
    }
} 