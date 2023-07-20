using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityFigmaBridge.Editor.FigmaApi;
using UnityFigmaBridge.Editor.Utils;

namespace UnityFigmaBridge.Editor.Settings
{
    public class UnityFigmaBridgeSettings : ScriptableObject
    {
       
        [Tooltip("The FIGMA Document URL to import")]
        public string DocumentUrl;
        
        [Tooltip("Generate logic and linking of screens based on FIGMA's 'Prototype' settings")]
        public bool BuildPrototypeFlow=true;
        
        [Space(10)]
        [Tooltip("Scene used for prototype assets, including canvas")]
        public string RunTimeAssetsScenePath;
        
        [Tooltip("Enable Auto layout components (Horizontal/Vertical layout) (EXPERIMENTAL)")]
        public bool EnableAutoLayout = false;
        
        [Tooltip("C# Namespace filter for binding MonoBehaviours for screens. Use this to ensure it will only bind to MonoBehaviours in that namespace (eg specify 'MyGame.UI' to only bind MyGame.UI.PlayScreen node to 'PlayScreen')")]
        public string ScreenBindingNamespace="";
        
        [Tooltip("Scale for rendering server images")]
        public int ServerRenderImageScale=3;

        [Tooltip("Tick this to enable downloading missing fonts from Google Fonts")]
        public bool EnableGoogleFontsDownloads = true;

        [Tooltip("Generate a C# file containing all found screens")]
        public bool CreateScreenNameCSharpFile = false;
        
        [Tooltip("If false, the generator will not attempt to build any nodes marked for export")]
        public bool GenerateNodesMarkedForExport = true;
        
        [Tooltip("If true, download only selected pages and screens")]
        public bool OnlyImportSelectedPages = false;

        [Tooltip("Version in which the Sync process was executed. (Do not edit manually)")]
        public string LastImportVersion = "";

        [HideInInspector]
        public List<FigmaPageData> PageDataList = new ();

        public string FileId {
            get
            {
                var (isValid, fileId) = FigmaApiUtils.GetFigmaDocumentIdFromUrl(DocumentUrl);
                return isValid ? fileId : "";
            }
        }
        
        /// <summary>
        /// Return true if the migrate process have to be performed
        /// </summary>
        /// <returns></returns>
        bool NeedsMigrate()
        {
            return string.IsNullOrEmpty(LastImportVersion) || new Version(LastImportVersion) < FigmaVersion.Version;
        }

        /// <summary>
        /// Check update and perform migrate process if needed
        /// </summary>
        public void CheckUpdate()
        {
            if (NeedsMigrate())
            {
                MigrateFrom(LastImportVersion);
            }
            LastImportVersion = FigmaVersion.Version.ToString();
        }

        /// <summary>
        /// Migrate process from a specific version
        /// </summary>
        /// <param name="fromVersion"></param>
        void MigrateFrom(string fromVersion)
        {
            // from 1.0.7 or earlier
            if (string.IsNullOrEmpty(fromVersion))
            {
                foreach (var filePath in Directory.GetFiles(FigmaPaths.FigmaImageFillFolder))
                {
                    var textureImporter = AssetImporter.GetAtPath(filePath) as TextureImporter;
                    if (textureImporter != null)
                    {
                        textureImporter.sRGBTexture = true;
                        textureImporter.SaveAndReimport();
                    }
                }
            }
        }

        public void RefreshForUpdatedPages(FigmaFile file)
        {
            // Get all pages from Figma Doc
            var pageNodeList = FigmaDataUtils.GetPageNodes(file);
            var downloadPageNodeIdList = pageNodeList.Select(p => p.id).ToList();

            // Get a list of all pages in the settings file
            var settingsPageDataIdList = PageDataList.Select(p => p.NodeId).ToList();

            // Build a list of all new pages to add
            var addPageIdList = downloadPageNodeIdList.Except(settingsPageDataIdList);
            foreach (var addPageId in addPageIdList)
            {
                var addNode = pageNodeList.FirstOrDefault(p => p.id == addPageId);
                PageDataList.Add(new FigmaPageData(addNode.name, addNode.id));
            }
            
            // Build a list of removed pages to remove from list
            var deletePageIdList = settingsPageDataIdList.Except(downloadPageNodeIdList);
            foreach (var deletePageId in deletePageIdList)
            {
                var index = PageDataList.FindIndex(p => p.NodeId == deletePageId);
                PageDataList.RemoveAt(index);
            }
            PageDataList.OrderBy(p => p.NodeId);
        }
    }

    [Serializable]
    public class FigmaPageData
    {
        public string Name;
        public string NodeId;
        public bool Selected;

        public FigmaPageData(){}

        public FigmaPageData(string name, string nodeId)
        {
            Name = name;
            NodeId = nodeId;
            Selected = true; // default is true
        }
    }
    
}