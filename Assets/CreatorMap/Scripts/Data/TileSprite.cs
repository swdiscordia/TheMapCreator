using System;
using System.Collections.Generic;
using UnityEngine;
// Comment out the DofusCoube dependency
// using DofusCoube.FileProtocol.Dlm;

namespace Models.Maps
{
    // Added TileColorMultiplicator class to replace external dependency
    [Serializable]
    public class TileColorMultiplicator
    {
        public bool IsOne { get; set; }
        public float Red { get; set; }
        public float Green { get; set; }
        public float Blue { get; set; }

        public TileColorMultiplicator()
        {
            IsOne = true;
            Red = 1f;
            Green = 1f;
            Blue = 1f;
        }
    }

    [Serializable]
    public class TileSprite
    {
        public int Id { get; private set; }
        public int Level { get; set; }
        public List<MapPoint> Cells { get; set; }
        public TileColorMultiplicator ColorMultiplicator { get; set; }
        public Sprite Sprite { get; set; }
        public bool FlipX { get; set; }
        public bool FlipY { get; set; }

        public TileSprite()
        {
            Cells = new List<MapPoint>();
            ColorMultiplicator = new TileColorMultiplicator();
        }

        public TileSprite(int id, int level)
        {
            Id = id;
            Level = level;
            Cells = new List<MapPoint>();
            ColorMultiplicator = new TileColorMultiplicator();
        }

        public void AddCell(MapPoint cell)
        {
            if (!Cells.Contains(cell))
                Cells.Add(cell);
        }

        public void RemoveCell(MapPoint cell)
        {
            Cells.Remove(cell);
        }
    }
} 