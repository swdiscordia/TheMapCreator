using System.Collections.Generic;
using System.Linq;
using CreatorMap.Scripts.Core.Grid;
using CreatorMap.Scripts.Data;
using UnityEngine;

namespace CreatorMap.Scripts.Core
{
    /// <summary>
    /// Adaptateur pour convertir les données du GridManager en format MapBasicInformation compatible avec le projet principal
    /// </summary>
    public static class MapDataAdapter
    {
        /// <summary>
        /// Convertit les données de la grille en MapBasicInformation pour le projet principal
        /// </summary>
        /// <param name="gridData">Données de la grille</param>
        /// <returns>Données formatées pour le projet principal</returns>
        public static MapBasicInformation ToMapBasicInformation(MapCreatorGridManager.GridData gridData)
        {
            // Créer une nouvelle instance de MapBasicInformation
            var mapInfo = new MapBasicInformation
            {
                id = gridData.id,
                // On garde les valeurs existantes ou on initialise à 0
                leftNeighbourId = 0,
                rightNeighbourId = 0,
                topNeighbourId = 0,
                bottomNeighbourId = 0,
                identifiedElements = new SerializableDictionary<uint, uint>()
            };

            // Initialiser la liste cellsList
            mapInfo.cellsList.Clear();

            // Copier les données des cellules
            foreach (var cell in gridData.cells)
            {
                // Ajouter à la liste pour l'éditeur
                mapInfo.cellsList.Add(new Cell 
                { 
                    id = cell.cellId, 
                    flags = (int)cell.flags 
                });
                
                // Ajouter au dictionnaire cells, déjà au bon format uint
                mapInfo.UpdateCellData(cell.cellId, cell.flags);
            }

            return mapInfo;
        }

        /// <summary>
        /// Convertit les données MapBasicInformation en format GridData pour l'éditeur
        /// </summary>
        /// <param name="mapInfo">Données venant du projet principal</param>
        /// <returns>Données formatées pour l'éditeur</returns>
        public static MapCreatorGridManager.GridData FromMapBasicInformation(MapBasicInformation mapInfo)
        {
            var gridData = new MapCreatorGridManager.GridData
            {
                id = mapInfo.id
            };
            
            // Vérifier si nous avons la liste des cellules
            if (mapInfo.cellsList != null && mapInfo.cellsList.Count > 0)
            {
                foreach (var cell in mapInfo.cellsList)
                {
                    gridData.cells.Add(new MapCreatorGridManager.CellData((ushort)cell.id, (uint)cell.flags));
                }
            }
            // Sinon, utiliser le dictionnaire cells
            else if (mapInfo.cells != null && mapInfo.cells.dictionary.Count > 0)
            {
                // Extraire les données de cellules, les valeurs sont déjà en uint
                foreach (var entry in mapInfo.cells.dictionary)
                {
                    gridData.cells.Add(new MapCreatorGridManager.CellData(entry.Key, entry.Value));
                }
            }
            
            // Maintenir la compatibilité avec le dictionnaire cellsDict
            gridData.cellsDict = gridData.cells.ToDictionary(c => c.cellId, c => c.flags);
            
            return gridData;
        }
    }
} 