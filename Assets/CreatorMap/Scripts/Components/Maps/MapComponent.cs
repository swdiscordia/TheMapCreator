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
            
            // Afficher les informations sur mapInformation.cells
            if (mapInformation.cells != null && mapInformation.cells.dictionary != null)
            {
                Debug.Log($"[DATA_DEBUG] MapComponent.Awake - Contenu de cells.dictionary:");
                foreach (var pair in mapInformation.cells.dictionary.Take(10)) // Limite à 10 pour ne pas spammer
                {
                    Debug.Log($"[DATA_DEBUG] Cell {pair.Key}: flags = {pair.Value}");
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
                Debug.Log($"[DATA_DEBUG] MapComponent.Start - Chargement des ressources");
                // Utiliser shader par défaut plutôt que l'Addressable qui cause des erreurs
                var colorMatrixShader = Shader.Find("Custom/ColorMatrixShader");
                if (colorMatrixShader == null)
                {
                    Debug.LogWarning("[DATA_DEBUG] Custom/ColorMatrixShader non trouvé, utilisation du shader par défaut");
                    colorMatrixShader = Shader.Find("Sprites/Default");
                }

                var tiles = FindObjectsByType<CreatorMap.Scripts.TileSprite>(FindObjectsSortMode.None);
                Debug.Log($"[DATA_DEBUG] MapComponent.Start - {tiles.Length} tiles trouvés");
                
                // Le reste du code de Start reste le même mais avec les try/catch pour éviter les crashs
                try
                {
                    var keys = tiles.Select(x => x.key).Distinct();
                    
                    // Éviter Addressables pour simplifier
                    Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
                    foreach (var tile in tiles)
                    {
                        if (tile == null) continue;
                        
                        // Create a basic material with our shader
                        var material = new Material(colorMatrixShader);
                        var sr = tile.GetComponent<SpriteRenderer>();
                        if (sr != null)
                        {
                            sr.sharedMaterial = material;
                        }
                        
                        _sprites.Add(tile.gameObject);
                    }

                    Debug.Log($"[DATA_DEBUG] MapComponent.Start - Chargement terminé");
                    IsLoaded = true;
                    
                    if (Camera.main != null)
                    {
                        Camera.main.backgroundColor = backgroundColor;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[DATA_DEBUG] Erreur lors du chargement des tiles: {e.Message}");
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