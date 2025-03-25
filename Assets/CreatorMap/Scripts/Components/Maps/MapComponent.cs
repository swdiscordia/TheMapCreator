using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using CreatorMap.Scripts.Data;
using CreatorMap.Scripts;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Components.Maps
{
    /// <summary>
    /// Composant représentant une carte dans le jeu
    /// </summary>
    public class MapComponent : MonoBehaviour
    {
        [SerializeField]
        public MapBasicInformation mapInformation = new MapBasicInformation();
        [SerializeField] public Color backgroundColor;
        private static readonly int Color1 = Shader.PropertyToID("_Color");
        
        // Make sure this is preserved during serialization with additional attributes
        [SerializeField]
        [HideInInspector] 
        public bool preserveDataInPlayMode = true;
        
        // Track if we've already initialized to prevent double-initialization
        private bool m_HasInitialized = false;

        private readonly HashSet<GameObject> _sprites = new();
        private AsyncOperationHandle<IList<Texture2D>> _handle;
        private AsyncOperationHandle<Shader> _shaderHandle;

        public bool IsLoaded
        {
            get;
            private set;
        }
        
        private void Awake()
        {
            Debug.Log($"[DATA_DEBUG] MapComponent.Awake - Initialisation du composant");
            // S'assurer que les informations de base sont initialisées
            if (mapInformation == null)
            {
                mapInformation = new MapBasicInformation();
                Debug.Log($"[DATA_DEBUG] MapComponent.Awake - Création d'un nouveau MapBasicInformation");
            }
            else
            {
                Debug.Log($"[DATA_DEBUG] MapComponent.Awake - MapBasicInformation existant. ID: {mapInformation.id}");
            }
            
            // Check if we've already initialized to prevent double-initialization which might reset data
            if (m_HasInitialized)
            {
                Debug.Log("[DATA_DEBUG] MapComponent.Awake - Already initialized, skipping initialization");
                return;
            }
            
            // Special handling for play mode to ensure we preserve designer's modifications
            if (Application.isPlaying && preserveDataInPlayMode)
            {
                Debug.Log("[DATA_DEBUG] MapComponent.Awake - Running in PLAY mode, preserving designer data");
                
                // Make sure we don't lose any cell data during the transition to play mode
                if (mapInformation.cells != null && mapInformation.cells.dictionary != null && mapInformation.cells.dictionary.Count > 0)
                {
                    Debug.Log($"[DATA_DEBUG] MapComponent.Awake - Preserving {mapInformation.cells.dictionary.Count} designer-configured cells");
                    
                    // We don't need to do anything special here since the data is already serialized
                    // Just make sure we don't reinitialize the cells
                }
                else
                {
                    // If no cells are defined yet, initialize them
                    Debug.Log($"[DATA_DEBUG] MapComponent.Awake - No cells defined, initializing");
                    mapInformation.InitializeAllCells();
                }
                
                // Force serialization to ensure data is preserved
                if (mapInformation.cells != null)
                {
                    mapInformation.cells.OnBeforeSerialize();
                }
            }
            else
            {
                // Original initialization logic for edit mode
                // Initialiser les cellules si nécessaire
                if (mapInformation.cells == null || 
                    mapInformation.cells.dictionary == null || 
                    mapInformation.cells.dictionary.Count == 0)
                {
                    Debug.Log($"[DATA_DEBUG] MapComponent.Awake - Initialisation des cellules car elles sont nulles ou vides");
                    mapInformation.InitializeAllCells();
                    Debug.Log($"[DATA_DEBUG] MapComponent.Awake - Cellules initialisées. Nombre: {mapInformation.cells.dictionary.Count}");
                }
                else
                {
                    Debug.Log($"[DATA_DEBUG] MapComponent.Awake - Les cellules sont déjà initialisées. Nombre: {mapInformation.cells.dictionary.Count}");
                }
            }
            
            // Mark as initialized to prevent double-initialization
            m_HasInitialized = true;
            
            // Afficher les informations sur mapInformation.cells
            if (mapInformation.cells != null && mapInformation.cells.dictionary != null)
            {
                Debug.Log($"[DATA_DEBUG] MapComponent.Awake - Contenu de cells.dictionary:");
                foreach (var pair in mapInformation.cells.dictionary.Take(10)) // Limite à 10 pour ne pas spammer
                {
                    Debug.Log($"[DATA_DEBUG] Cell {pair.Key}: flags = {pair.Value}, walkable = {(pair.Value & 0x0001) == 0}");
                }
                
                if (mapInformation.cells.dictionary.Count > 10)
                {
                    Debug.Log($"[DATA_DEBUG] ... et {mapInformation.cells.dictionary.Count - 10} autres cellules");
                }
            }
        }

        private async void Start()
        {
            try
            {
                Debug.Log($"[DATA_DEBUG] MapComponent.Start - Démarrage en préservant l'état visuel");
                
                // Si nous sommes en mode jeu, s'assurer que preserveDataInPlayMode est toujours à true
                if (Application.isPlaying)
                {
                    preserveDataInPlayMode = true;
                    
                    // Vérifier que les données sont bien préservées
                    if (mapInformation != null && mapInformation.cells != null && mapInformation.cells.dictionary != null)
                    {
                        int cellCount = mapInformation.cells.dictionary.Count;
                        Debug.Log($"[DATA_DEBUG] MapComponent.Start - {cellCount} cellules préservées en mode jeu");
                    }
                }
                
                // Utiliser shader par défaut plutôt que l'Addressable qui cause des erreurs
                var colorMatrixShader = Shader.Find("Custom/ColorMatrixShader");
                if (colorMatrixShader == null)
                {
                    Debug.LogWarning("[DATA_DEBUG] Custom/ColorMatrixShader non trouvé, utilisation du shader par défaut");
                    colorMatrixShader = Shader.Find("Sprites/Default");
                }

                var tiles = FindObjectsByType<CreatorMap.Scripts.TileSprite>(FindObjectsSortMode.None);
                Debug.Log($"[DATA_DEBUG] MapComponent.Start - {tiles.Length} tiles trouvés");
                
                // Traitement des tiles existants - sans recréer ni détruire la structure existante
                try
                {
                    // Éviter Addressables pour simplifier
                    foreach (var tile in tiles)
                    {
                        if (tile == null) continue;
                        
                        // Create a basic material with our shader if needed
                        var sr = tile.GetComponent<SpriteRenderer>();
                        if (sr != null && sr.sharedMaterial == null)
                        {
                            var material = new Material(colorMatrixShader);
                            sr.sharedMaterial = material;
                        }
                        
                        // Ajouter à la liste de suivi sans détruire l'existant
                        if (!_sprites.Contains(tile.gameObject))
                        {
                            _sprites.Add(tile.gameObject);
                        }
                    }

                    Debug.Log($"[DATA_DEBUG] MapComponent.Start - Traitement terminé en préservant l'état existant");
                    IsLoaded = true;
                    
                    if (Camera.main != null)
                    {
                        Camera.main.backgroundColor = backgroundColor;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[DATA_DEBUG] Erreur lors du traitement des tiles: {e.Message}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[DATA_DEBUG] Erreur dans Start: {e.Message}");
            }
        }

        private void OnDestroy()
        {
            Debug.Log($"[DATA_DEBUG] MapComponent.OnDestroy - Nettoyage des ressources");
            foreach (var sprite in _sprites)
            {
                if (sprite != null)
                {
                    Destroy(sprite); // This won't release the asset, but will destroy the GameObject
                }
            }
            
            _sprites.Clear();

            if (_handle.IsValid())
                Addressables.Release(_handle);
                
            if (_shaderHandle.IsValid())  
                Addressables.Release(_shaderHandle);
                
            Addressables.ReleaseInstance(gameObject);
        }
        
        private void OnApplicationQuit()
        {
            if (Application.isPlaying)
            {
                Debug.Log("[DATA_DEBUG] MapComponent.OnApplicationQuit - Saving map data before exiting play mode");
                
                // Force serialization of the map data
                if (mapInformation != null && mapInformation.cells != null)
                {
                    mapInformation.cells.OnBeforeSerialize();
                    Debug.Log($"[DATA_DEBUG] MapComponent.OnApplicationQuit - Serialized {mapInformation.cells.dictionary.Count} cells");
                }
            }
        }
        
        // Pour le débogage, afficher l'état actuel des cellules
        private void OnValidate()
        {
            Debug.Log($"[DATA_DEBUG] MapComponent.OnValidate - MapBasicInformation ID: {mapInformation.id}");
            
            if (mapInformation != null && mapInformation.cells != null && mapInformation.cells.dictionary != null)
            {
                Debug.Log($"[DATA_DEBUG] MapComponent.OnValidate - Nombre de cellules: {mapInformation.cells.dictionary.Count}");
            }
            else
            {
                Debug.Log("[DATA_DEBUG] MapComponent.OnValidate - Pas de données de cellules disponibles");
            }
        }
    }
} 