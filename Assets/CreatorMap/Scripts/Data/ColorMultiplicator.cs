using UnityEngine;

namespace CreatorMap.Scripts.Data
{
    /// <summary>
    /// Defines a color multiplier structure for tile sprites
    /// Compatible with the ploup project's ColorMultiplicator class
    /// </summary>
    [System.Serializable]
    public class ColorMultiplicator
    {
        public bool IsOne { get; set; } = true;
        public float Red { get; set; } = 1f;
        public float Green { get; set; } = 1f;
        public float Blue { get; set; } = 1f;
        public float Alpha { get; set; } = 1f;

        public Color ToColor()
        {
            return new Color(Red, Green, Blue, Alpha);
        }
    }
} 