using System;
using UnityEngine;

namespace Models.Maps
{
    [Serializable]
    public class MapPosition
    {
        public int worldX;
        public int worldY;

        public MapPosition() { }

        public MapPosition(int worldX, int worldY)
        {
            this.worldX = worldX;
            this.worldY = worldY;
        }

        public MapPosition(MapPosition position)
        {
            worldX = position.worldX;
            worldY = position.worldY;
        }

        public bool Equals(MapPosition obj)
        {
            return obj != null && worldX == obj.worldX && worldY == obj.worldY;
        }

        public override bool Equals(object obj)
        {
            if (obj is MapPosition)
                return Equals((MapPosition)obj);
            return false;
        }

        public override int GetHashCode()
        {
            return worldX * 100000 + worldY;
        }

        public override string ToString()
        {
            return string.Format("({0}, {1})", worldX, worldY);
        }

        public static MapPosition operator +(MapPosition a, MapPosition b)
        {
            return new MapPosition(a.worldX + b.worldX, a.worldY + b.worldY);
        }

        public static MapPosition operator -(MapPosition a, MapPosition b)
        {
            return new MapPosition(a.worldX - b.worldX, a.worldY - b.worldY);
        }

        public static bool operator ==(MapPosition a, MapPosition b)
        {
            if (ReferenceEquals(a, null))
                return ReferenceEquals(b, null);

            return a.Equals(b);
        }

        public static bool operator !=(MapPosition a, MapPosition b)
        {
            return !(a == b);
        }
    }
} 