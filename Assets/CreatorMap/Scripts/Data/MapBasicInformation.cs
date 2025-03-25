using System;
using System.Collections.Generic;
using CreatorMap.Scripts.Data;
using UnityEngine;

namespace CreatorMap.Scripts.Data
{
    /// <summary>
    /// Structure de données pour stocker les informations de base d'une carte, compatible avec le projet principal
    /// </summary>
    [Serializable]
    public class MapBasicInformation
    {
        // ONLY KEEP FIELDS WE WANT

        [SerializeField] public int id;
        
        [SerializeField] public long leftNeighbourId = -1;
        [SerializeField] public long rightNeighbourId = -1;
        [SerializeField] public long topNeighbourId = -1;
        [SerializeField] public long bottomNeighbourId = -1;
        
        [SerializeField] public SerializableDictionary<ushort, uint> cells = new();
        
        [SerializeField] public SerializableDictionary<uint, uint> identifiedElements = new();
        
        // MAKE ALL UNWANTED FIELDS NON-SERIALIZED
        
        [NonSerialized] public string version;
        [NonSerialized] public string updatedAt;
        [NonSerialized] public long subAreaId = -1;
        [NonSerialized] public bool encrypted;
        [NonSerialized] public bool createWorldMapElements;
        [NonSerialized] public bool encryptionVersion;
        [NonSerialized] public int backgroundsCount;
        [NonSerialized] public bool isUsingNewMovementSystem;
        [NonSerialized] public bool useExternalFileToSave;
        [NonSerialized] public bool backgroundRasterization;
        [NonSerialized] public int width = 14;
        [NonSerialized] public int height = 20;
        
        // EDITOR ONLY - Ces champs sont pour notre éditeur uniquement
        [NonSerialized] 
        public List<Cell> cellsList = new List<Cell>();
        
        // EDITOR ONLY - Données de sprites pour l'éditeur
        [NonSerialized]
        public MapSpriteData SpriteData = new MapSpriteData();
        
        // Propriété pour la rétrocompatibilité avec le code existant
        public SerializableDictionary<ushort, ushort> cellsDict 
        { 
            get { return new SerializableDictionary<ushort, ushort>(); } 
        }
        
        public void UpdateCellData(ushort cellId, uint flags) 
        {
            Debug.Log($"[DATA_DEBUG] MapBasicInformation.UpdateCellData - Mise à jour de la cellule {cellId} avec flags {flags}");
            
            if (cells == null)
            {
                Debug.LogWarning($"[DATA_DEBUG] MapBasicInformation.UpdateCellData - Le dictionnaire cells est null!");
                cells = new SerializableDictionary<ushort, uint>();
            }
            
            if (cells.dictionary.ContainsKey(cellId))
            {
                Debug.Log($"[DATA_DEBUG] MapBasicInformation.UpdateCellData - Cellule {cellId} présente, mise à jour des flags de {cells.dictionary[cellId]} à {flags}");
                cells.dictionary[cellId] = flags;
            }
            else
            {
                Debug.Log($"[DATA_DEBUG] MapBasicInformation.UpdateCellData - Cellule {cellId} non trouvée, ajout avec flags {flags}");
                cells.dictionary.Add(cellId, flags);
            }
            
            // Mettre également à jour cellsList pour l'éditeur
            bool found = false;
            for (int i = 0; i < cellsList.Count; i++)
            {
                if (cellsList[i].id == cellId)
                {
                    cellsList[i].flags = (int)flags;
                    found = true;
                    break;
                }
            }
            
            if (!found)
            {
                cellsList.Add(new Cell { id = cellId, flags = (int)flags });
            }
        }
        
        public void InitializeAllCells()
        {
            Debug.Log($"[DATA_DEBUG] MapBasicInformation.InitializeAllCells - Initialisation de toutes les cellules (14x20)");
            
            if (cells == null)
            {
                Debug.Log($"[DATA_DEBUG] MapBasicInformation.InitializeAllCells - Création du dictionnaire cells");
                cells = new SerializableDictionary<ushort, uint>();
            }
            
            if (cells.dictionary == null)
            {
                Debug.Log($"[DATA_DEBUG] MapBasicInformation.InitializeAllCells - Initialisation du dictionnaire interne");
                cells.dictionary = new Dictionary<ushort, uint>();
            }
            
            int totalCells = 14 * 20; // 560 cells (standard map size)
            int initialCount = cells.dictionary.Count;
            
            Debug.Log($"[DATA_DEBUG] MapBasicInformation.InitializeAllCells - Nombre initial de cellules: {initialCount}, objectif: {totalCells}");
            
            for (ushort i = 0; i < 560; i++)
            {
                if (!cells.dictionary.ContainsKey(i))
                {
                    // Default value: 64 (bit 6 = IsVisible) + Walkable (bit 0 = 0)
                    cells.dictionary.Add(i, 0x0040); // Par défaut, IsVisible + walkable
                }
            }
            
            Debug.Log($"[DATA_DEBUG] MapBasicInformation.InitializeAllCells - Initialisation terminée. Nombre de cellules: {cells.dictionary.Count}");
            Debug.Log("[DATA_DEBUG] Toutes les cellules sont initialisées comme walkable (bit 0 = 0) pour permettre le placement des tiles");
        }
        
        public void ClearAllCells()
        {
            Debug.Log($"[DATA_DEBUG] MapBasicInformation.ClearAllCells - Suppression de toutes les cellules");
            
            if (cells != null && cells.dictionary != null)
            {
                int count = cells.dictionary.Count;
                cells.dictionary.Clear();
                Debug.Log($"[DATA_DEBUG] MapBasicInformation.ClearAllCells - {count} cellules supprimées");
            }
            else
            {
                Debug.Log($"[DATA_DEBUG] MapBasicInformation.ClearAllCells - Pas de cellules à supprimer");
            }
            
            cellsList.Clear();
        }
    }
    
    /// <summary>
    /// Représente une cellule sur la carte (compatible avec le projet principal)
    /// </summary>
    [Serializable]
    public class Cell
    {
        public long id;
        public int flags;
        
        public bool IsWalkable => (flags & 0x0001) == 0;
        public bool IsNonWalkableFight => (flags & 0x0002) != 0;
        public bool IsNonWalkableRP => (flags & 0x0004) != 0;
        public bool IsLineOfSight => (flags & 0x0008) != 0;
        public bool IsBlue => (flags & 0x0010) != 0;
        public bool IsRed => (flags & 0x0020) != 0;
        public bool IsVisible => (flags & 0x0040) != 0;
        public bool IsFarm => (flags & 0x0080) != 0;
        public bool IsHavenbag => (flags & 0x0100) != 0;
        
        // Helper methods for Cell flags
        public static bool CalculateCellFlag_NonWalkable(uint flags) => (flags & 0x0001) != 0;
        public static uint SetCellFlag_NonWalkable(uint flags) => flags | 0x0001;
        public static uint UnsetCellFlag_NonWalkable(uint flags) => flags & ~0x0001U;
    }
} 