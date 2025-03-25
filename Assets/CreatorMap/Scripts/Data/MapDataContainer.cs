using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using CreatorMap.Scripts.Core.Grid;
using CreatorMap.Scripts.Data;
using Components.Maps;

namespace CreatorMap.Scripts.Data
{
    /// <summary>
    /// Container class for storing multiple map data in one file, matching the main project's approach.
    /// </summary>
    [Serializable]
    public class MapDataContainer
    {
        /// <summary>
        /// Dictionary mapping map IDs to their serialized data
        /// </summary>
        public Dictionary<long, MapEntry> Maps { get; private set; } = new Dictionary<long, MapEntry>();

        /// <summary>
        /// Updates a map in the container with data from a MapComponent
        /// </summary>
        public void UpdateMap(MapComponent mapComponent)
        {
            if (mapComponent == null || mapComponent.mapInformation == null)
            {
                Debug.LogError("[MapDataContainer] Cannot update from null MapComponent");
                return;
            }
            
            long mapId = mapComponent.mapInformation.id;
            
            // Create entry if it doesn't exist
            if (!Maps.TryGetValue(mapId, out var entry))
            {
                entry = new MapEntry { Id = mapId };
                Maps[mapId] = entry;
            }
            
            // Clear existing cells and update from MapComponent
            entry.Cells.Clear();
            if (mapComponent.mapInformation.cells != null && mapComponent.mapInformation.cells.dictionary != null)
            {
                foreach (var pair in mapComponent.mapInformation.cells.dictionary)
                {
                    entry.Cells[pair.Key] = pair.Value;
                }
            }
            
            // Update sprite data if available
            if (mapComponent.mapInformation.SpriteData != null)
            {
                entry.SpriteData = new MapSpriteData();
                
                // Copy tiles
                if (mapComponent.mapInformation.SpriteData.tiles != null)
                {
                    foreach (var tile in mapComponent.mapInformation.SpriteData.tiles)
                    {
                        entry.SpriteData.tiles.Add(tile);
                    }
                }
                
                // Copy fixtures
                if (mapComponent.mapInformation.SpriteData.fixtures != null)
                {
                    foreach (var fixture in mapComponent.mapInformation.SpriteData.fixtures)
                    {
                        entry.SpriteData.fixtures.Add(fixture);
                    }
                }
            }
            
            Debug.Log($"[MapDataContainer] Updated map {mapId} with {entry.Cells.Count} cells and {(entry.SpriteData?.tiles?.Count ?? 0) + (entry.SpriteData?.fixtures?.Count ?? 0)} sprites");
        }
        
        /// <summary>
        /// Legacy method to maintain compatibility
        /// </summary>
        public void UpdateMap(MapCreatorGridManager.GridData gridData)
        {
            Debug.LogWarning("[MapDataContainer] Using deprecated UpdateMap(GridData) method - please update to use MapComponent");
            
            // Find MapComponent to update container from
            var mapComponent = UnityEngine.Object.FindObjectOfType<MapComponent>();
            if (mapComponent != null)
            {
                UpdateMap(mapComponent);
                return;
            }
            
            // Fallback to old method if MapComponent not found
            long mapId = gridData.id;
            
            if (!Maps.TryGetValue(mapId, out var entry))
            {
                entry = new MapEntry { Id = mapId };
                Maps[mapId] = entry;
            }
            
            // Clear existing cells and update from grid data
            entry.Cells.Clear();
            if (gridData.cells != null)
            {
                foreach (var cell in gridData.cells)
                {
                    entry.Cells[cell.cellId] = cell.flags;
                }
            }
            
            Debug.Log($"[MapDataContainer] Updated map {mapId} with {entry.Cells.Count} cells (legacy method)");
        }

        /// <summary>
        /// Serialize the container to a binary writer
        /// </summary>
        public void Serialize(BinaryWriter writer)
        {
            // Write the number of maps
            writer.Write(Maps.Count);
            
            // Write each map
            foreach (var map in Maps)
            {
                // Write map ID
                writer.Write(map.Key);
                
                // Write the number of cells
                writer.Write(map.Value.Cells.Count);
                
                // Write each cell
                foreach (var cell in map.Value.Cells)
                {
                    writer.Write(cell.Key);
                    writer.Write(cell.Value);
                }
                
                // Write sprite data
                WriteSpriteData(writer, map.Value.SpriteData);
            }
        }
        
        /// <summary>
        /// Write sprite data to the binary writer
        /// </summary>
        private void WriteSpriteData(BinaryWriter writer, MapSpriteData spriteData)
        {
            if (spriteData == null || spriteData.tiles == null)
            {
                writer.Write(0); // No tiles
            }
            else
            {
                // Write number of tiles
                writer.Write(spriteData.tiles.Count);
                
                // Write each tile
                foreach (var tile in spriteData.tiles)
                {
                    writer.Write(tile.Id);
                    writer.Write(tile.Position.x);
                    writer.Write(tile.Position.y);
                    writer.Write(tile.FlipX);
                    writer.Write(tile.FlipY);
                    writer.Write(tile.Order);
                    writer.Write(tile.Scale);
                    
                    // Write color data
                    writer.Write(tile.Color.Red);
                    writer.Write(tile.Color.Green);
                    writer.Write(tile.Color.Blue);
                    writer.Write(tile.Color.Alpha);
                }
            }
            
            if (spriteData == null || spriteData.fixtures == null)
            {
                writer.Write(0); // No fixtures
            }
            else
            {
                // Write number of fixtures
                writer.Write(spriteData.fixtures.Count);
                
                // Write each fixture
                foreach (var fixture in spriteData.fixtures)
                {
                    writer.Write(fixture.Id);
                    writer.Write(fixture.Position.x);
                    writer.Write(fixture.Position.y);
                    writer.Write(fixture.FlipX);
                    writer.Write(fixture.FlipY);
                    writer.Write(fixture.Order);
                    writer.Write(fixture.Scale.x);
                    writer.Write(fixture.Scale.y);
                    
                    // Write color data
                    writer.Write(fixture.Color.Red);
                    writer.Write(fixture.Color.Green);
                    writer.Write(fixture.Color.Blue);
                    writer.Write(fixture.Color.Alpha);
                }
            }
        }
        
        /// <summary>
        /// Deserialize the container from a binary reader
        /// </summary>
        public void Deserialize(BinaryReader reader)
        {
            // Clear existing maps
            Maps.Clear();
            
            // Read the number of maps
            int mapCount = reader.ReadInt32();
            
            // Read each map
            for (int i = 0; i < mapCount; i++)
            {
                // Read map ID
                long mapId = reader.ReadInt64();
                
                // Create a new MapEntry
                var entry = new MapEntry { Id = mapId };
                
                // Read the number of cells
                int cellCount = reader.ReadInt32();
                
                // Read each cell
                for (int j = 0; j < cellCount; j++)
                {
                    ushort cellId = reader.ReadUInt16();
                    uint flags = reader.ReadUInt32();
                    
                    // Add cell to the entry
                    entry.Cells[cellId] = flags;
                }
                
                // Read sprite data
                entry.SpriteData = ReadSpriteData(reader);
                
                // Add the entry to the container
                Maps[mapId] = entry;
            }
        }
        
        /// <summary>
        /// Read sprite data from the binary reader
        /// </summary>
        private MapSpriteData ReadSpriteData(BinaryReader reader)
        {
            var spriteData = new MapSpriteData();
            
            // Read tiles
            int tileCount = reader.ReadInt32();
            for (int i = 0; i < tileCount; i++)
            {
                var tile = new TileSpriteData
                {
                    Id = reader.ReadString(),
                    Position = new Vector2(reader.ReadSingle(), reader.ReadSingle()),
                    FlipX = reader.ReadBoolean(),
                    FlipY = reader.ReadBoolean(),
                    Order = reader.ReadInt32(),
                    Scale = reader.ReadSingle(),
                    Color = new TileColorData
                    {
                        Red = reader.ReadSingle(),
                        Green = reader.ReadSingle(),
                        Blue = reader.ReadSingle(),
                        Alpha = reader.ReadSingle()
                    }
                };
                
                spriteData.tiles.Add(tile);
            }
            
            // Read fixtures
            int fixtureCount = reader.ReadInt32();
            for (int i = 0; i < fixtureCount; i++)
            {
                var fixture = new FixtureSpriteData
                {
                    Id = reader.ReadString(),
                    Position = new Vector2(reader.ReadSingle(), reader.ReadSingle()),
                    FlipX = reader.ReadBoolean(),
                    FlipY = reader.ReadBoolean(),
                    Order = reader.ReadInt32(),
                    Scale = new Vector2(reader.ReadSingle(), reader.ReadSingle()),
                    Color = new TileColorData
                    {
                        Red = reader.ReadSingle(),
                        Green = reader.ReadSingle(),
                        Blue = reader.ReadSingle(),
                        Alpha = reader.ReadSingle()
                    }
                };
                
                spriteData.fixtures.Add(fixture);
            }
            
            return spriteData;
        }
        
        /// <summary>
        /// Get a map entry by ID, or null if not found
        /// </summary>
        public MapEntry GetMap(long mapId)
        {
            return Maps.TryGetValue(mapId, out var entry) ? entry : null;
        }
        
        /// <summary>
        /// Apply map data to a MapComponent
        /// </summary>
        public void ApplyToMapComponent(long mapId, MapComponent mapComponent)
        {
            if (mapComponent == null)
            {
                Debug.LogError("[MapDataContainer] Cannot apply to null MapComponent");
                return;
            }
            
            var entry = GetMap(mapId);
            if (entry == null)
            {
                Debug.LogWarning($"[MapDataContainer] No map data found for ID {mapId}");
                return;
            }
            
            // Create mapInformation if needed
            if (mapComponent.mapInformation == null)
            {
                mapComponent.mapInformation = new MapBasicInformation();
            }
            
            // Set the map ID
            mapComponent.mapInformation.id = (int)entry.Id;
            
            // Initialize cells dictionary if needed
            if (mapComponent.mapInformation.cells == null)
            {
                mapComponent.mapInformation.cells = new SerializableDictionary<ushort, uint>();
            }
            
            // Clear existing cells and copy from entry
            mapComponent.mapInformation.cells.dictionary.Clear();
            foreach (var pair in entry.Cells)
            {
                mapComponent.mapInformation.cells.dictionary[pair.Key] = pair.Value;
            }
            
            // Update sprite data
            if (entry.SpriteData != null)
            {
                // Create SpriteData if needed
                if (mapComponent.mapInformation.SpriteData == null)
                {
                    mapComponent.mapInformation.SpriteData = new MapSpriteData();
                }
                
                // Clear existing sprites
                mapComponent.mapInformation.SpriteData.tiles.Clear();
                mapComponent.mapInformation.SpriteData.fixtures.Clear();
                
                // Copy tiles
                if (entry.SpriteData.tiles != null)
                {
                    foreach (var tile in entry.SpriteData.tiles)
                    {
                        mapComponent.mapInformation.SpriteData.tiles.Add(tile);
                    }
                }
                
                // Copy fixtures
                if (entry.SpriteData.fixtures != null)
                {
                    foreach (var fixture in entry.SpriteData.fixtures)
                    {
                        mapComponent.mapInformation.SpriteData.fixtures.Add(fixture);
                    }
                }
            }
            
            // Force serialization
            mapComponent.mapInformation.cells.OnBeforeSerialize();
            
            // Mark as dirty in editor
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(mapComponent);
            #endif
            
            Debug.Log($"[MapDataContainer] Applied map {mapId} with {entry.Cells.Count} cells to MapComponent");
        }
        
        /// <summary>
        /// Legacy method to get map data as GridData
        /// </summary>
        public MapCreatorGridManager.GridData GetMapAsGridData(long mapId)
        {
            Debug.LogWarning("[MapDataContainer] Using deprecated GetMapAsGridData method - consider updating to use ApplyToMapComponent");
            
            if (!Maps.TryGetValue(mapId, out var entry))
            {
                Debug.LogWarning($"No map data found for ID {mapId}");
                return new MapCreatorGridManager.GridData { id = (int)mapId };
            }
            
            var gridData = new MapCreatorGridManager.GridData
            {
                id = (int)mapId,
                cells = new List<MapCreatorGridManager.CellData>()
            };
            
            // Copy cell data from entry to gridData
            foreach (var cellPair in entry.Cells)
            {
                gridData.cells.Add(new MapCreatorGridManager.CellData(cellPair.Key, cellPair.Value));
                gridData.cellsDict[cellPair.Key] = cellPair.Value;
            }
            
            return gridData;
        }
    }

    /// <summary>
    /// Entry for a single map in the container
    /// </summary>
    [Serializable]
    public class MapEntry
    {
        public long Id { get; set; }
        public Dictionary<ushort, uint> Cells { get; set; } = new Dictionary<ushort, uint>();
        public MapSpriteData SpriteData { get; set; } = new MapSpriteData();
    }
} 