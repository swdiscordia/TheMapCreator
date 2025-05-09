﻿#nullable enable
using System;
using System.Collections.Generic;
using Components.Maps;
// using DofusCoube.FileProtocol.Datacenter.World;
using Managers.Maps;
using MapCreator.Data.Models;
using Models.Maps;
using CreatorMap.Scripts.Core.Grid;
using CreatorMap.Scripts.Data;
using UnityEngine; // Added for Debug class
// using Models.Actors;

namespace Models.Maps
{
    public class Map 
    {
        // Keep GridData for backward compatibility, but it's no longer the primary data source
        private readonly MapCreatorGridManager.GridData? m_GridData;
        private readonly MapPosition _mapPosition;
        private readonly MapComponent? m_MapComponent;

        public readonly List<MapCreator.Data.Models.Cell> Cells;
        // public readonly List<ActorSprite> Actors = new();
        
        public long Id => m_MapComponent != null && m_MapComponent.mapInformation != null ? 
                        m_MapComponent.mapInformation.id : 
                        (m_GridData != null ? m_GridData.id : 0);

        /// <summary>
        /// Constructor that takes a MapComponent as the primary data source (preferred)
        /// </summary>
        public Map(MapComponent mapComponent, MapPosition mapPosition)
        {
            m_MapComponent = mapComponent;
            _mapPosition = mapPosition;
            
            Cells = new List<MapCreator.Data.Models.Cell>();
            
            if (mapComponent != null && mapComponent.mapInformation != null && 
                mapComponent.mapInformation.cells != null && mapComponent.mapInformation.cells.dictionary != null)
            {
                foreach (var pair in mapComponent.mapInformation.cells.dictionary)
                {
                    Cells.Add(new MapCreator.Data.Models.Cell((short)pair.Key, (short)pair.Value));
                }
                
                Debug.Log($"[Map] Created map from MapComponent with {Cells.Count} cells");
            }
            else
            {
                Debug.LogWarning("[Map] MapComponent or its data is null, map will be empty");
            }
        }

        /// <summary>
        /// Legacy constructor that takes GridData (kept for compatibility)
        /// </summary>
        public Map(MapCreatorGridManager.GridData gridData, MapPosition mapPosition)
        {
            Debug.LogWarning("[Map] Using deprecated GridData constructor - consider updating to MapComponent version");
            
            m_GridData = gridData;
            _mapPosition = mapPosition;

            Cells = new List<MapCreator.Data.Models.Cell>();

            if (gridData.cells != null)
            {
                foreach (var cell in gridData.cells)
                {
                    Cells.Add(new MapCreator.Data.Models.Cell((short)cell.cellId, (short)cell.flags));
                }
                
                Debug.Log($"[Map] Created map from GridData with {Cells.Count} cells");
            }
        }

        public bool IsValidCellId(int cellId)
        {
            return cellId is >= 0 and < 561;
        }

        public MapCreator.Data.Models.Cell? GetCell(short cellId)
        {
            return !IsValidCellId(cellId) ? null : Cells[cellId];
        }

        public bool PointMov(int x, int y, int previousCell = -1, bool isInFight = false)
        {
            var startCell = previousCell > -1 && IsValidCellId(previousCell) ? (int) previousCell : -1;
            var endCell   = MapTools.GetCellIdByCoord(x, y);

            if (endCell == -1)
            {
                return false;
            }

            return startCell < 0 || CanMoveToCell(startCell, endCell);
        }

        public bool CanMoveToCell(int startCell, int endCell)
        {
            if (!IsValidCellId(startCell) || !IsValidCellId(endCell))
            {
                return false;
            }

            var startCellObj = GetCell((short) startCell);
            var endCellObj   = GetCell((short) endCell);

            if (startCellObj == null || endCellObj == null || !startCellObj.IsAllWalkable || !endCellObj.IsAllWalkable)
            {
                return false;
            }

            return true;
        }

        public double PointWeight(int x, int y, bool allowTroughEntity = true)
        {
            var cellId = MapTools.GetCellIdByCoord(x, y);

            if (!IsValidCellId(cellId))
            {
                return 0d;
            }

            var speed  = Cells[(short)cellId].Speed;
            var weight = 0d;

            if (allowTroughEntity)
            {
                if (speed >= 0)
                {
                    weight += 5 - speed;
                }
                else
                {
                    weight += 11 + Math.Abs(speed);
                }

                /*var entityOnCell = GetActorOnCell(cellId);

                if (entityOnCell != null)
                {
                    weight = 20;
                }*/
            }

            else
            {
                /* Comment out all Actor references
                if (GetActorOnCell(cellId) != null)
                {
                    weight += 0.3;
                }

                if (GetActorOnCell(MapTools.GetCellIdByCoord(x + 1, y)) != null)
                {
                    weight += 0.3;
                }

                if (GetActorOnCell(MapTools.GetCellIdByCoord(x, y + 1)) != null)
                {
                    weight += 0.3;
                }

                if (GetActorOnCell(MapTools.GetCellIdByCoord(x - 1, y)) != null)
                {
                    weight += 0.3;
                }

                if (GetActorOnCell(MapTools.GetCellIdByCoord(x, y - 1)) != null)
                {
                    weight += 0.3;
                }
                */
            }

            return weight;
        }

        /*
        private ActorSprite? GetActorOnCell(int cellId)
        {
            return Actors.Find(actor => actor.CellId == cellId);
        }
        */

        public bool IsChangeZone(int cellId1, int cellId2)
        {
            var cellData1 = Cells[(short)cellId1];
            var cellData2 = Cells[(short)cellId2];

            var dif = Math.Abs(Math.Abs(cellData1.Floor) - Math.Abs(cellData2.Floor));
            return (cellData1.MoveZone != cellData2.MoveZone && dif == 0);
        }
    }
}