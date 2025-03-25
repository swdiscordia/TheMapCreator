using System.Collections.Generic;
using System.Linq;
using CreatorMap.Scripts.Core.Grid;
using CreatorMap.Scripts.Data;
using UnityEngine;
using Components.Maps;

namespace CreatorMap.Scripts.Core
{
    /// <summary>
    /// Adaptateur pour convertir les données du MapComponent en format MapBasicInformation compatible avec le projet principal
    /// </summary>
    public static class MapDataAdapter
    {
        /// <summary>
        /// Convertit les données du MapComponent en MapBasicInformation pour le projet principal
        /// </summary>
        /// <param name="mapComponent">Component MapComponent contenant les données</param>
        /// <returns>Données formatées pour le projet principal</returns>
        public static MapBasicInformation ToMapBasicInformation(Components.Maps.MapComponent mapComponent)
        {
            if (mapComponent == null || mapComponent.mapInformation == null)
            {
                Debug.LogError("[MapDataAdapter] Cannot convert null MapComponent");
                return new MapBasicInformation();
            }
            
            // Create a deep copy of the MapBasicInformation to avoid reference issues
            var mapInfo = new MapBasicInformation
            {
                id = mapComponent.mapInformation.id,
                leftNeighbourId = mapComponent.mapInformation.leftNeighbourId,
                rightNeighbourId = mapComponent.mapInformation.rightNeighbourId,
                topNeighbourId = mapComponent.mapInformation.topNeighbourId,
                bottomNeighbourId = mapComponent.mapInformation.bottomNeighbourId
            };
            
            // Initialize collections
            if (mapInfo.cellsList == null)
            {
                mapInfo.cellsList = new List<Cell>();
            }
            else
            {
                mapInfo.cellsList.Clear();
            }
            
            if (mapInfo.cells == null)
            {
                mapInfo.cells = new SerializableDictionary<ushort, uint>();
            }
            else if (mapInfo.cells.dictionary != null)
            {
                mapInfo.cells.dictionary.Clear();
            }
            
            if (mapInfo.identifiedElements == null)
            {
                mapInfo.identifiedElements = new SerializableDictionary<uint, uint>();
            }
            
            // Copy cell data
            if (mapComponent.mapInformation.cells != null && mapComponent.mapInformation.cells.dictionary != null)
            {
                foreach (var pair in mapComponent.mapInformation.cells.dictionary)
                {
                    // Add to the editor's cell list
                    mapInfo.cellsList.Add(new Cell
                    {
                        id = pair.Key,
                        flags = (int)pair.Value
                    });
                    
                    // Add to the dictionary for the main project
                    mapInfo.UpdateCellData(pair.Key, pair.Value);
                }
            }
            
            // Copy sprite data if available
            if (mapComponent.mapInformation.SpriteData != null)
            {
                mapInfo.SpriteData = new MapSpriteData();
                
                // Copy tiles
                if (mapComponent.mapInformation.SpriteData.tiles != null)
                {
                    foreach (var tile in mapComponent.mapInformation.SpriteData.tiles)
                    {
                        mapInfo.SpriteData.tiles.Add(tile);
                    }
                }
                
                // Copy fixtures
                if (mapComponent.mapInformation.SpriteData.fixtures != null)
                {
                    foreach (var fixture in mapComponent.mapInformation.SpriteData.fixtures)
                    {
                        mapInfo.SpriteData.fixtures.Add(fixture);
                    }
                }
            }
            
            return mapInfo;
        }
        
        /// <summary>
        /// Updates a MapComponent with data from MapBasicInformation
        /// </summary>
        /// <param name="mapInfo">Source data</param>
        /// <param name="mapComponent">Target MapComponent to update</param>
        public static void UpdateMapComponent(MapBasicInformation mapInfo, Components.Maps.MapComponent mapComponent)
        {
            if (mapInfo == null)
            {
                Debug.LogError("[MapDataAdapter] Cannot update from null MapBasicInformation");
                return;
            }
            
            if (mapComponent == null)
            {
                Debug.LogError("[MapDataAdapter] Cannot update null MapComponent");
                return;
            }
            
            // Create mapInformation if needed
            if (mapComponent.mapInformation == null)
            {
                mapComponent.mapInformation = new MapBasicInformation();
            }
            
            // Update basic properties
            mapComponent.mapInformation.id = mapInfo.id;
            mapComponent.mapInformation.leftNeighbourId = mapInfo.leftNeighbourId;
            mapComponent.mapInformation.rightNeighbourId = mapInfo.rightNeighbourId;
            mapComponent.mapInformation.topNeighbourId = mapInfo.topNeighbourId;
            mapComponent.mapInformation.bottomNeighbourId = mapInfo.bottomNeighbourId;
            
            // Initialize cells collection if needed
            if (mapComponent.mapInformation.cells == null)
            {
                mapComponent.mapInformation.cells = new SerializableDictionary<ushort, uint>();
            }
            else if (mapComponent.mapInformation.cells.dictionary != null)
            {
                mapComponent.mapInformation.cells.dictionary.Clear();
            }
            
            // Copy cells data
            if (mapInfo.cellsList != null && mapInfo.cellsList.Count > 0)
            {
                foreach (var cell in mapInfo.cellsList)
                {
                    mapComponent.mapInformation.cells.dictionary[(ushort)cell.id] = (uint)cell.flags;
                }
            }
            else if (mapInfo.cells != null && mapInfo.cells.dictionary != null && mapInfo.cells.dictionary.Count > 0)
            {
                foreach (var pair in mapInfo.cells.dictionary)
                {
                    mapComponent.mapInformation.cells.dictionary[pair.Key] = pair.Value;
                }
            }
            
            // Copy sprite data
            if (mapInfo.SpriteData != null)
            {
                if (mapComponent.mapInformation.SpriteData == null)
                {
                    mapComponent.mapInformation.SpriteData = new MapSpriteData();
                }
                
                // Clear existing sprite data
                mapComponent.mapInformation.SpriteData.tiles.Clear();
                mapComponent.mapInformation.SpriteData.fixtures.Clear();
                
                // Copy tiles
                if (mapInfo.SpriteData.tiles != null)
                {
                    foreach (var tile in mapInfo.SpriteData.tiles)
                    {
                        mapComponent.mapInformation.SpriteData.tiles.Add(tile);
                    }
                }
                
                // Copy fixtures
                if (mapInfo.SpriteData.fixtures != null)
                {
                    foreach (var fixture in mapInfo.SpriteData.fixtures)
                    {
                        mapComponent.mapInformation.SpriteData.fixtures.Add(fixture);
                    }
                }
            }
            
            // Force serialization of the data
            if (mapComponent.mapInformation.cells != null)
            {
                mapComponent.mapInformation.cells.OnBeforeSerialize();
            }
            
            // Mark as dirty in editor
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(mapComponent);
            #endif
        }
        
        // Legacy methods kept for compatibility - will redirect to the MapComponent-based methods
        
        /// <summary>
        /// Convertit les données de la grille en MapBasicInformation pour le projet principal
        /// </summary>
        /// <param name="gridData">Données de la grille</param>
        /// <returns>Données formatées pour le projet principal</returns>
        public static MapBasicInformation ToMapBasicInformation(MapCreatorGridManager.GridData gridData)
        {
            Debug.LogWarning("[MapDataAdapter] Using deprecated GridData method, please update code to use MapComponent version");
            
            // Find MapComponent to read data from
            var mapComponent = UnityEngine.Object.FindObjectOfType<Components.Maps.MapComponent>();
            if (mapComponent == null)
            {
                // Fallback to old method with limited data
                var mapInfo = new MapBasicInformation
                {
                    id = gridData.id,
                    leftNeighbourId = 0,
                    rightNeighbourId = 0,
                    topNeighbourId = 0,
                    bottomNeighbourId = 0,
                    identifiedElements = new SerializableDictionary<uint, uint>()
                };
                
                // Copy cell data
                if (gridData.cells != null)
                {
                    foreach (var cell in gridData.cells)
                    {
                        mapInfo.cellsList.Add(new Cell { id = cell.cellId, flags = (int)cell.flags });
                        mapInfo.UpdateCellData(cell.cellId, cell.flags);
                    }
                }
                
                return mapInfo;
            }
            
            // Use the modern method that reads from MapComponent
            return ToMapBasicInformation(mapComponent);
        }
        
        /// <summary>
        /// Convertit les données MapBasicInformation en format GridData pour l'éditeur
        /// </summary>
        /// <param name="mapInfo">Données venant du projet principal</param>
        /// <returns>Données formatées pour l'éditeur</returns>
        public static MapCreatorGridManager.GridData FromMapBasicInformation(MapBasicInformation mapInfo)
        {
            Debug.LogWarning("[MapDataAdapter] Using deprecated FromMapBasicInformation method, GridManager no longer stores data");
            
            // Find MapComponent to update
            var mapComponent = UnityEngine.Object.FindObjectOfType<Components.Maps.MapComponent>();
            if (mapComponent != null)
            {
                // Update the MapComponent with the provided data
                UpdateMapComponent(mapInfo, mapComponent);
            }
            
            // Create a minimal GridData for backward compatibility
            var gridData = new MapCreatorGridManager.GridData
            {
                id = mapInfo.id,
                cells = new List<MapCreatorGridManager.CellData>()
            };
            
            // Return empty GridData since we don't use it anymore
            return gridData;
        }
    }
} 