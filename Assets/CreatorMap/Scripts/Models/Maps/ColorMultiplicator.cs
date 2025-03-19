using System;

namespace Models.Maps
{
    [Serializable]
    public class ColorMultiplicator
    {
        public bool IsOne { get; set; }
        public float Red { get; set; }
        public float Green { get; set; }
        public float Blue { get; set; }

        public ColorMultiplicator()
        {
            IsOne = true;
            Red = 1f;
            Green = 1f;
            Blue = 1f;
        }
    }
} 