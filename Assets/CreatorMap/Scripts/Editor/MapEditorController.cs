using System.IO;
using System.Text;
using CreatorMap.Scripts.Core.Grid;
using CreatorMap.Scripts.Data;
using UnityEngine;
using System.Linq;
using UnityEditor;

namespace CreatorMap.Scripts.Editor
{
    /// <summary>
    /// Contrôleur pour gérer les opérations d'édition de carte
    /// </summary>
    public class MapEditorController : MonoBehaviour
    {
        [SerializeField] private MapCreatorGridManager gridManager;
        
        private void Reset()
        {
            // Auto-référencer le GridManager au moment de l'ajout du composant
            gridManager = FindObjectOfType<MapCreatorGridManager>();
        }
        
        /// <summary>
        /// Exporte les données de la carte actuelle dans un fichier JSON
        /// </summary>
        /// <param name="mapId">ID de la carte</param>
        public void ExportMapData(int mapId)
        {
            if (gridManager == null)
            {
                Debug.LogError("GridManager reference is missing");
                return;
            }
            
            // S'assurer que l'ID est correctement défini
            gridManager.gridData.id = mapId;
            
            // Obtenir les données au format du projet principal
            var mapInfo = gridManager.ExportToMapBasicInformation();
            
            // Créer un conteneur qui stocke plusieurs cartes
            var container = new MapDataContainer();
            container.UpdateMap(gridManager.gridData);
            
            // Sérialiser en JSON pour une visualisation facile (en développement)
            string json = JsonUtility.ToJson(mapInfo, true);
            
            // Déterminer le chemin du fichier
            string filePath = EditorUtility.SaveFilePanel(
                "Exporter les données de carte",
                Application.dataPath,
                $"Map_{mapId}.json",
                "json");
                
            if (!string.IsNullOrEmpty(filePath))
            {
                File.WriteAllText(filePath, json);
                Debug.Log($"Carte exportée avec succès vers {filePath}");
            }
        }
        
        /// <summary>
        /// Exporte les données de la carte au format binaire compatible avec le projet principal
        /// </summary>
        /// <param name="mapId">ID de la carte</param>
        public void ExportMapDataBinary(int mapId)
        {
            if (gridManager == null)
            {
                Debug.LogError("GridManager reference is missing");
                return;
            }
            
            // S'assurer que l'ID est correctement défini
            gridManager.gridData.id = mapId;
            
            // Créer un conteneur qui stocke plusieurs cartes
            var container = new MapDataContainer();
            container.UpdateMap(gridManager.gridData);
            
            // Déterminer le chemin du fichier
            string filePath = EditorUtility.SaveFilePanel(
                "Exporter les données de carte (binaire)",
                Application.dataPath,
                $"Map_{mapId}.dat",
                "dat");
                
            if (!string.IsNullOrEmpty(filePath))
            {
                using (var stream = new FileStream(filePath, FileMode.Create))
                using (var writer = new BinaryWriter(stream))
                {
                    container.Serialize(writer);
                }
                
                Debug.Log($"Carte exportée en format binaire vers {filePath}");
            }
        }
        
        /// <summary>
        /// Importe les données d'une carte à partir d'un fichier JSON
        /// </summary>
        public void ImportMapData()
        {
            if (gridManager == null)
            {
                Debug.LogError("GridManager reference is missing");
                return;
            }
            
            string filePath = EditorUtility.OpenFilePanel(
                "Importer les données de carte",
                Application.dataPath,
                "json");
                
            if (!string.IsNullOrEmpty(filePath))
            {
                string json = File.ReadAllText(filePath);
                var mapInfo = JsonUtility.FromJson<MapBasicInformation>(json);
                
                if (mapInfo != null)
                {
                    // Reconstruire le dictionnaire si nécessaire
                    mapInfo.cellsList.RebuildDictionary();
                    
                    // Importer les données
                    gridManager.ImportFromMapBasicInformation(mapInfo);
                    
                    Debug.Log($"Carte importée avec succès depuis {filePath}");
                }
                else
                {
                    Debug.LogError("Échec de l'importation : format de fichier invalide");
                }
            }
        }
        
        /// <summary>
        /// Importe les données d'une carte à partir d'un fichier binaire
        /// </summary>
        public void ImportMapDataBinary()
        {
            if (gridManager == null)
            {
                Debug.LogError("GridManager reference is missing");
                return;
            }
            
            string filePath = EditorUtility.OpenFilePanel(
                "Importer les données de carte (binaire)",
                Application.dataPath,
                "dat");
                
            if (!string.IsNullOrEmpty(filePath))
            {
                try
                {
                    using (var stream = new FileStream(filePath, FileMode.Open))
                    using (var reader = new BinaryReader(stream))
                    {
                        var container = MapDataContainer.Deserialize(reader);
                        
                        // Demander quel mapId importer si plusieurs cartes sont présentes
                        if (container.Maps.Count > 1)
                        {
                            var mapIds = container.Maps.Keys.ToArray();
                            // Idéalement, on devrait afficher une fenêtre de sélection ici
                            // Pour cet exemple, on prend simplement la première carte
                            var mapId = mapIds[0];
                            var gridData = container.ToGridData(mapId);
                            gridManager.gridData = gridData;
                            gridManager.CreateGrid();
                        }
                        else if (container.Maps.Count == 1)
                        {
                            var mapId = container.Maps.Keys.First();
                            var gridData = container.ToGridData(mapId);
                            gridManager.gridData = gridData;
                            gridManager.CreateGrid();
                        }
                        else
                        {
                            Debug.LogWarning("Le fichier ne contient aucune carte");
                        }
                    }
                    
                    Debug.Log($"Carte importée avec succès depuis {filePath}");
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Échec de l'importation : {ex.Message}");
                }
            }
        }
    }
} 