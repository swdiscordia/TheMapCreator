#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;

namespace MapCreator.Editor
{
    public class SpriteImporterWindow : EditorWindow
    {
        private Vector2 m_ScrollPosition;
        private string m_SourceFolder = "";
        private string m_DestinationFolder = "Assets/CreatorMap/Content/Sprites";
        private bool m_OrganizeByID = true;
        
        [MenuItem("Window/Map Creator/Sprite Importer")]
        public static void ShowWindow()
        {
            GetWindow<SpriteImporterWindow>("Sprite Importer");
        }
        
        private void OnGUI()
        {
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField("Sprite Importer", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("This window allows you to import sprites into the project.", MessageType.Info);
            
            EditorGUILayout.Space(10);
            
            // Source folder
            EditorGUILayout.BeginHorizontal();
            m_SourceFolder = EditorGUILayout.TextField("Source Folder", m_SourceFolder);
            if (GUILayout.Button("Browse...", GUILayout.Width(80)))
            {
                string folder = EditorUtility.OpenFolderPanel("Select Source Folder", "", "");
                if (!string.IsNullOrEmpty(folder))
                {
                    m_SourceFolder = folder;
                }
            }
            EditorGUILayout.EndHorizontal();
            
            // Destination folder
            EditorGUILayout.BeginHorizontal();
            m_DestinationFolder = EditorGUILayout.TextField("Destination Folder", m_DestinationFolder);
            if (GUILayout.Button("Browse...", GUILayout.Width(80)))
            {
                string folder = EditorUtility.OpenFolderPanel("Select Destination Folder", "Assets", "");
                if (!string.IsNullOrEmpty(folder))
                {
                    // Make the path relative to the project
                    if (folder.StartsWith(Application.dataPath))
                    {
                        folder = "Assets" + folder.Substring(Application.dataPath.Length);
                    }
                    m_DestinationFolder = folder;
                }
            }
            EditorGUILayout.EndHorizontal();
            
            // Options
            m_OrganizeByID = EditorGUILayout.Toggle("Organize by ID", m_OrganizeByID);
            
            EditorGUILayout.Space(20);
            
            // Import button
            GUI.enabled = !string.IsNullOrEmpty(m_SourceFolder) && !string.IsNullOrEmpty(m_DestinationFolder);
            if (GUILayout.Button("Import Sprites", GUILayout.Height(30)))
            {
                ImportSprites();
            }
            GUI.enabled = true;
            
            EditorGUILayout.EndVertical();
        }
        
        private void ImportSprites()
        {
            if (string.IsNullOrEmpty(m_SourceFolder) || !Directory.Exists(m_SourceFolder))
            {
                EditorUtility.DisplayDialog("Error", "Source folder is invalid or does not exist.", "OK");
                return;
            }
            
            if (string.IsNullOrEmpty(m_DestinationFolder))
            {
                EditorUtility.DisplayDialog("Error", "Destination folder is invalid.", "OK");
                return;
            }
            
            // Ensure destination folder exists
            if (!Directory.Exists(m_DestinationFolder))
            {
                Directory.CreateDirectory(m_DestinationFolder);
            }
            
            // Get all image files
            string[] files = Directory.GetFiles(m_SourceFolder, "*.png", SearchOption.AllDirectories);
            files = files.Concat(Directory.GetFiles(m_SourceFolder, "*.jpg", SearchOption.AllDirectories)).ToArray();
            files = files.Concat(Directory.GetFiles(m_SourceFolder, "*.jpeg", SearchOption.AllDirectories)).ToArray();
            
            if (files.Length == 0)
            {
                EditorUtility.DisplayDialog("No Files", "No image files found in the source folder.", "OK");
                return;
            }
            
            int imported = 0;
            foreach (string file in files)
            {
                string fileName = Path.GetFileNameWithoutExtension(file);
                string extension = Path.GetExtension(file);
                
                string destPath;
                if (m_OrganizeByID && fileName.Length >= 2)
                {
                    // Use first 2 characters of ID as subfolder
                    string subfolder = fileName.Substring(0, 2);
                    string subfolderPath = Path.Combine(m_DestinationFolder, subfolder);
                    
                    if (!Directory.Exists(subfolderPath))
                    {
                        Directory.CreateDirectory(subfolderPath);
                    }
                    
                    destPath = Path.Combine(subfolderPath, fileName + extension);
                }
                else
                {
                    destPath = Path.Combine(m_DestinationFolder, fileName + extension);
                }
                
                // Copy the file
                try
                {
                    File.Copy(file, destPath, true);
                    imported++;
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Error copying {file}: {ex.Message}");
                }
            }
            
            // Refresh the asset database
            AssetDatabase.Refresh();
            
            // Set sprites import settings
            SetSpriteImportSettings();
            
            EditorUtility.DisplayDialog("Import Complete", $"Successfully imported {imported} sprites.", "OK");
        }
        
        private void SetSpriteImportSettings()
        {
            string[] spriteGuids = AssetDatabase.FindAssets("t:Sprite", new[] { m_DestinationFolder });
            
            foreach (string guid in spriteGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.mipmapEnabled = false;
                    importer.filterMode = FilterMode.Point;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    
                    // Apply changes
                    importer.SaveAndReimport();
                }
            }
        }
    }
}
#endif 