using UnityEngine;
using UnityEditor;
using CreatorMap.Scripts.Core.Grid;
using CreatorMap.Scripts.Core;
using Components.Maps;

namespace CreatorMap.Scripts.Editor
{
    /// <summary>
    /// Fenêtre d'éditeur Unity pour gérer les cartes
    /// </summary>
    public class MapEditorWindow : EditorWindow
    {
        private GameObject _mapObject;
        private MapComponent _mapComponent;
        private GameObject _gridManagerObject;
        private MapCreatorGridManager _gridManager;
        private MapEditorController _editorController;
        
        private int _mapId = 1;
        private Vector2 _scrollPosition;
        
        [MenuItem("Map Creator/Map Editor")]
        public static void ShowWindow()
        {
            var window = GetWindow<MapEditorWindow>("Map Editor");
            window.minSize = new Vector2(300, 400);
            window.Show();
        }
        
        private void OnEnable()
        {
            FindMapComponent();
        }
        
        private void FindMapComponent()
        {
            // Rechercher d'abord le MapComponent
            _mapObject = GameObject.Find("Map");
            if (_mapObject != null)
            {
                _mapComponent = _mapObject.GetComponent<MapComponent>();
                
                if (_mapComponent != null)
                {
                    // Trouver le GridManager
                    _gridManagerObject = FindObjectOfType<MapCreatorGridManager>()?.gameObject;
                    if (_gridManagerObject != null)
                    {
                        _gridManager = _gridManagerObject.GetComponent<MapCreatorGridManager>();
                        _editorController = _gridManagerObject.GetComponent<MapEditorController>();
                        if (_editorController == null)
                        {
                            _editorController = _gridManagerObject.AddComponent<MapEditorController>();
                        }
                        
                        _mapId = (int)_mapComponent.mapInformation.id;
                        return;
                    }
                }
            }
            
            // Si aucun MapComponent n'est trouvé, chercher directement le GridManager
            FindGridManager();
        }
        
        private void FindGridManager()
        {
            _gridManagerObject = GameObject.Find("GridManager");
            if (_gridManagerObject != null)
            {
                _gridManager = _gridManagerObject.GetComponent<MapCreatorGridManager>();
                
                // Trouver le contrôleur de l'éditeur
                _editorController = _gridManagerObject.GetComponent<MapEditorController>();
                if (_editorController == null)
                {
                    _editorController = _gridManagerObject.AddComponent<MapEditorController>();
                }
            }
        }
        
        private void OnGUI()
        {
            if (_gridManager == null)
            {
                DrawNoGridManagerUI();
                return;
            }
            
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            
            DrawSetupStatus();
            DrawMapIdSection();
            DrawGridDataSection();
            DrawImportExportSection();
            
            EditorGUILayout.EndScrollView();
        }
        
        private void DrawSetupStatus()
        {
            EditorGUILayout.Space(5);
            
            if (_mapComponent != null)
            {
                EditorGUILayout.HelpBox("Configuration complète détectée (MapComponent + GridManager)", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("Configuration partielle détectée (GridManager uniquement, pas de MapComponent)", MessageType.Warning);
                
                if (GUILayout.Button("Mettre à niveau vers la configuration complète"))
                {
                    UpgradeToFullSetup();
                }
            }
            
            EditorGUILayout.Space(5);
        }
        
        private void DrawNoGridManagerUI()
        {
            EditorGUILayout.HelpBox("Aucun GridManager trouvé dans la scène.", MessageType.Warning);
            
            if (GUILayout.Button("Créer une configuration de carte complète"))
            {
                MapMenu.CreateMapSetup();
                FindMapComponent();
            }
            
            if (GUILayout.Button("Rechercher les composants"))
            {
                FindMapComponent();
            }
        }
        
        private void UpgradeToFullSetup()
        {
            // Créer un objet Map parent
            var mapObject = new GameObject("Map");
            Undo.RegisterCreatedObjectUndo(mapObject, "Create Map Object");
            
            // Déplacer le GridManager comme enfant de Map si nécessaire
            if (_gridManagerObject != null && _gridManagerObject.transform.parent == null)
            {
                Undo.SetTransformParent(_gridManagerObject.transform, mapObject.transform, "Move GridManager under Map");
            }
            
            // Ajouter le MapComponent
            _mapComponent = mapObject.AddComponent<MapComponent>();
            Undo.RegisterCreatedObjectUndo(_mapComponent, "Add MapComponent");
            
            // Synchroniser les informations avec le GridManager
            if (_gridManager != null)
            {
                _mapComponent.mapInformation = _gridManager.ExportToMapBasicInformation();
            }
            
            // Rafraîchir les références
            FindMapComponent();
            
            // Sélectionner l'objet Map
            Selection.activeGameObject = mapObject;
            
            EditorUtility.SetDirty(mapObject);
            
            Debug.Log("Mise à niveau vers la configuration complète réussie !");
        }
        
        private void CreateGridManager()
        {
            _gridManagerObject = new GameObject("GridManager");
            _gridManager = _gridManagerObject.AddComponent<MapCreatorGridManager>();
            _editorController = _gridManagerObject.AddComponent<MapEditorController>();
            
            EditorUtility.SetDirty(_gridManagerObject);
            Undo.RegisterCreatedObjectUndo(_gridManagerObject, "Create Grid Manager");
        }
        
        private void DrawMapIdSection()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Identifiant de la carte", EditorStyles.boldLabel);
            
            EditorGUI.BeginChangeCheck();
            _mapId = EditorGUILayout.IntField("ID de la carte", _mapId);
            if (EditorGUI.EndChangeCheck())
            {
                if (_mapComponent != null)
                {
                    // Mettre à jour via le MapComponent
                    _mapComponent.mapInformation.id = _mapId;
                }
                else
                {
                    // Mise à jour directe du GridManager
                    _gridManager.gridData.id = _mapId;
                }
                
                EditorUtility.SetDirty(_mapComponent != null ? (Object)_mapComponent : _gridManager);
            }
            
            EditorGUILayout.Space(5);
            
            if (GUILayout.Button("Charger la carte avec cet ID"))
            {
                _gridManager.LoadGridData(_mapId);
                _gridManager.CreateGrid();
                SceneView.RepaintAll();
            }
        }
        
        private void DrawGridDataSection()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Informations de la grille", EditorStyles.boldLabel);
            
            EditorGUILayout.LabelField($"Nombre de cellules configurées: {_gridManager.gridData.cells.Count}");
            
            if (GUILayout.Button("Recréer la grille"))
            {
                _gridManager.CreateGrid();
                SceneView.RepaintAll();
            }
        }
        
        private void DrawImportExportSection()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Import/Export", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Exporter (JSON)"))
            {
                _editorController.ExportMapData(_mapId);
            }
            
            if (GUILayout.Button("Exporter (Binaire)"))
            {
                _editorController.ExportMapDataBinary(_mapId);
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Importer (JSON)"))
            {
                _editorController.ImportMapData();
                if (_mapComponent != null)
                {
                    _mapId = (int)_mapComponent.mapInformation.id;
                }
                else
                {
                    _mapId = _gridManager.gridData.id;
                }
            }
            
            if (GUILayout.Button("Importer (Binaire)"))
            {
                _editorController.ImportMapDataBinary();
                if (_mapComponent != null)
                {
                    _mapId = (int)_mapComponent.mapInformation.id;
                }
                else
                {
                    _mapId = _gridManager.gridData.id;
                }
            }
            EditorGUILayout.EndHorizontal();
        }
    }
} 