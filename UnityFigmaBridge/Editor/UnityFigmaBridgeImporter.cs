using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityFigmaBridge.Editor.FigmaApi;
using UnityFigmaBridge.Editor.Fonts;
using UnityFigmaBridge.Editor.Nodes;
using UnityFigmaBridge.Editor.PrototypeFlow;
using UnityFigmaBridge.Editor.Settings;
using UnityFigmaBridge.Editor.Utils;
using UnityFigmaBridge.Runtime.UI;
using Object = UnityEngine.Object;

namespace UnityFigmaBridge.Editor
{
    /// <summary>
    ///  Manages Figma importing and document creation
    /// </summary>
    public static class UnityFigmaBridgeImporter
    {
        
        /// <summary>
        /// The settings asset, containing preferences for importing
        /// </summary>
        private static UnityFigmaBridgeSettings s_UnityFigmaBridgeSettings;
        
        /// <summary>
        /// We'll cache the access token in editor Player prefs
        /// </summary>
        private const string FIGMA_PERSONAL_ACCESS_TOKEN_PREF_KEY = "FIGMA_PERSONAL_ACCESS_TOKEN";

        public const string PROGRESS_BOX_TITLE = "Importing Figma Document";

        /// <summary>
        /// Figma imposes a limit on the number of images in a single batch. This is batch size
        /// (This is a bit of a guess - 650 is rejected)
        /// </summary>
        private const int MAX_SERVER_RENDER_IMAGE_BATCH_SIZE = 300;

        /// <summary>
        /// Cached personal access token, retrieved from PlayerPrefs
        /// </summary>
        private static string s_PersonalAccessToken;
        
        /// <summary>
        /// Active canvas used for construction
        /// </summary>
        private static Canvas s_SceneCanvas;

        /// <summary>
        /// The flowScreen controller to mange prototype functionality
        /// </summary>
        private static PrototypeFlowController s_PrototypeFlowController;

        [MenuItem("Figma Bridge/Sync ALL", priority = 0, secondaryPriority = 0)]
        static void SyncAll()
        {
            SyncAsync();
        }

        [MenuItem("Figma Bridge/Reprocess Cached Document", priority = 2, secondaryPriority = 0)]
        static void ReprocessCachedDocument()
        {
            ReprocessDocumentAsync();
        }


        [MenuItem("Figma Bridge/Sync Document (No Image)", priority = 3, secondaryPriority = 0)]
        static void SyncDocument()
        {
            SyncDocumentAsync();
        }
        
        [MenuItem("Figma Bridge/Download Server Rendered Images", priority = 15)]
        static void DownloadServerRenderedImages()
        {
            SyncServerRenderedImagesAsync(null);
        }
        
        [MenuItem("Figma Bridge/Download Image Fills", priority = 16)]
        static void DownloadImageFills()
        {
            SyncImageFillsAsync(null);
        }
        

        private static async void SyncAsync()
        {
            await SyncDocumentAsync();
            var figmaFile = FigmaApiUtils.LoadFigmaDocumentFromCache();
            await SyncServerRenderedImagesAsync(figmaFile);
            await SyncImageFillsAsync(figmaFile);
        }

        private static async void ReprocessDocumentAsync()
        {
            var figmaFile = FigmaApiUtils.LoadFigmaDocumentFromCache();
            await ProcessDocument(figmaFile);
            await SyncServerRenderedImagesAsync(figmaFile);
            await SyncImageFillsAsync(figmaFile);
        }

        private static async Task SyncDocumentAsync()
        {
            var requirementsMet = CheckRequirements();
            if (!requirementsMet) return;

            var figmaFile = await DownloadFigmaDocument(s_UnityFigmaBridgeSettings.FileId);
            if (figmaFile == null) return;

            await ProcessDocument(figmaFile);
        }
        
        private static async Task ProcessDocument(FigmaFile figmaFile)
        {
            var requirementsMet = CheckRequirements();
            if (!requirementsMet) return;

            var pageNodeList = FigmaDataUtils.GetPageNodes(figmaFile);

            if (s_UnityFigmaBridgeSettings.OnlyImportSelectedPages)
            {
                var downloadPageNodeIdList = pageNodeList.Select(p => p.id).ToList();
                downloadPageNodeIdList.Sort();

                var settingsPageDataIdList = s_UnityFigmaBridgeSettings.PageDataList.Select(p => p.NodeId).ToList();
                settingsPageDataIdList.Sort();

                if (!settingsPageDataIdList.SequenceEqual(downloadPageNodeIdList))
                {
                    ReportError("The pages found in the Figma document have changed - check your settings file and Sync again when ready", "");
                    
                    // Apply the new page list to serialized data and select to allow the user to change
                    s_UnityFigmaBridgeSettings.RefreshForUpdatedPages(figmaFile);
                    Selection.activeObject = s_UnityFigmaBridgeSettings;
                    EditorUtility.SetDirty(s_UnityFigmaBridgeSettings);
                    AssetDatabase.SaveAssetIfDirty(s_UnityFigmaBridgeSettings);
                    AssetDatabase.Refresh();
                    
                    return;
                }
                
                var enabledPageIdList = s_UnityFigmaBridgeSettings.PageDataList.Where(p => p.Selected).Select(p => p.NodeId).ToList();

                if (enabledPageIdList.Count <= 0)
                {
                    ReportError("'Import Selected Pages' is selected, but no pages are selected for import", "");
                    SelectSettings();
                    return;
                }

                pageNodeList = pageNodeList.Where(p => enabledPageIdList.Contains(p.id)).ToList();
            }

            await ImportDocument(s_UnityFigmaBridgeSettings.FileId, figmaFile, pageNodeList);
        }

        /// <summary>
        /// Download server rendered images separately with rate limiting to avoid 429 errors
        /// </summary>
        private static async Task SyncServerRenderedImagesAsync(FigmaFile figmaFile)
        {
            var requirementsMet = CheckRequirements(checkPrototypeFlow: false);
            if (!requirementsMet) return;

            EditorUtility.DisplayProgressBar(PROGRESS_BOX_TITLE, "Loading cached Figma document...", 0);
            
            // Load from cache instead of downloading
            if (figmaFile == null)
            {
                figmaFile = FigmaApiUtils.LoadFigmaDocumentFromCache();
            }
            if (figmaFile == null)
            {
                EditorUtility.ClearProgressBar();
                ReportError("No cached Figma document found", "Please run 'Sync Document' first to download the Figma file before syncing server rendered images.");
                return;
            }
            
            var pageNodeList = FigmaDataUtils.GetPageNodes(figmaFile);
            
            if (s_UnityFigmaBridgeSettings.OnlyImportSelectedPages)
            {
                var enabledPageIdList = s_UnityFigmaBridgeSettings.PageDataList.Where(p => p.Selected).Select(p => p.NodeId).ToList();
                pageNodeList = pageNodeList.Where(p => enabledPageIdList.Contains(p.id)).ToList();
            }
            
            var downloadPageIdList = pageNodeList.Select(p => p.id).ToList();
            var externalComponentList = FigmaDataUtils.FindMissingComponentDefinitions(figmaFile);
            var serverRenderNodes = FigmaDataUtils.FindAllServerRenderNodesInFile(figmaFile, externalComponentList, downloadPageIdList);
            
            if (serverRenderNodes.Count == 0)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("No Server Rendered Images", "No complex shapes that require server rendering were found in the document.", "OK");
                return;
            }
            
            EditorUtility.DisplayProgressBar(PROGRESS_BOX_TITLE, "Downloading server rendered images...", 0);
            
            try
            {
                // Request a render of these nodes on the server if required
                var serverRenderData=new List<FigmaServerRenderData>();
                if (serverRenderNodes.Count > 0)
                {
                    var allNodeIds = serverRenderNodes.Select(serverRenderNode => serverRenderNode.SourceNode.id).ToList();
                    // As the API has an upper limit of images that can be rendered in a single request, we'll need to batch
                    var batchCount = Mathf.CeilToInt((float)allNodeIds.Count / MAX_SERVER_RENDER_IMAGE_BATCH_SIZE);
                    for (var i = 0; i < batchCount; i++)
                    {
                        var startIndex = i * MAX_SERVER_RENDER_IMAGE_BATCH_SIZE;
                        var nodeBatch = allNodeIds.GetRange(startIndex,
                            Mathf.Min(MAX_SERVER_RENDER_IMAGE_BATCH_SIZE, allNodeIds.Count - startIndex));
                        var serverNodeCsvList = string.Join(",", nodeBatch);
                        EditorUtility.DisplayProgressBar(PROGRESS_BOX_TITLE, $"Downloading server-rendered image data {i+1}/{batchCount}",(float)i/(float)batchCount);
                        try
                        {
                            var figmaTask = FigmaApiUtils.GetFigmaServerRenderData(s_UnityFigmaBridgeSettings.FileId, s_PersonalAccessToken,
                                serverNodeCsvList, s_UnityFigmaBridgeSettings.ServerRenderImageScale);
                            await figmaTask;
                            serverRenderData.Add(figmaTask.Result);
                        }
                        catch (Exception e)
                        {
                            EditorUtility.ClearProgressBar();
                            ReportError("Error downloading Figma Server Render Image Data", e.ToString());
                            return;
                        }
                    }
                }

                // Process server rendered images in batches to avoid rate limiting (429 errors)
                var nodeIds = serverRenderNodes.Select(n => n.SourceNode.id).ToList();
                var successCount = 0;
                var failureCount = 0;
                    EditorUtility.DisplayProgressBar(
                        PROGRESS_BOX_TITLE,
                        $"Downloading server rendered images", 0);
                    
                try
                {
                    if (serverRenderData != null)
                    {
                        // Generate download list from the rendered images
                        var downloadList = FigmaApiUtils.GenerateDownloadQueue(new FigmaImageFillData(), 
                            serverRenderData, 
                            serverRenderNodes);
                        
                        if (downloadList.Count > 0)
                        {
                            // Download all files in this batch
                            await FigmaApiUtils.DownloadFiles(downloadList, s_UnityFigmaBridgeSettings);
                            successCount += downloadList.Count;
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error downloading: {e}");
                    
                    // Create placeholder images for failed batch
                    nodeIds = nodeIds.Select(i => FigmaDataUtils.ReplaceUnsafeFileCharactersForNodeId(i)).ToList();
                    var placeholderCount = FigmaApiUtils.CreatePlaceholderImagesForBatch(FigmaPaths.FigmaServerRenderedImagesFolder, nodeIds);
                }
                
                // Small delay between batches to respect rate limits
                await Task.Delay(1000);
                
                EditorUtility.ClearProgressBar();
                AssetDatabase.Refresh();
                Debug.Log($"Server rendered images sync completed. {successCount} images downloaded successfully, {failureCount} failed (placeholders created).");
            }
            catch (Exception e)
            {
                EditorUtility.ClearProgressBar();
                ReportError("Error downloading server rendered images", e.ToString());
            }
        }
        
        /// <summary>
        /// Download image fills from Figma document
        /// </summary>
        private static async Task SyncImageFillsAsync(FigmaFile figmaFile)
        {
            var requirementsMet = CheckRequirements(checkPrototypeFlow: false);
            if (!requirementsMet) return;

            EditorUtility.DisplayProgressBar(PROGRESS_BOX_TITLE, "Loading cached Figma document...", 0);
            
            
            // Load from cache instead of downloading
            if(figmaFile == null)
            {
                figmaFile = FigmaApiUtils.LoadFigmaDocumentFromCache();
            }
            if (figmaFile == null)
            {
                EditorUtility.ClearProgressBar();
                ReportError("No cached Figma document found", "Please run 'Sync Document' first to download the Figma file before syncing image fills.");
                return;
            }
            
            var pageNodeList = FigmaDataUtils.GetPageNodes(figmaFile);
            
            if (s_UnityFigmaBridgeSettings.OnlyImportSelectedPages)
            {
                var enabledPageIdList = s_UnityFigmaBridgeSettings.PageDataList.Where(p => p.Selected).Select(p => p.NodeId).ToList();
                pageNodeList = pageNodeList.Where(p => enabledPageIdList.Contains(p.id)).ToList();
            }
            
            var downloadPageIdList = pageNodeList.Select(p => p.id).ToList();
            var foundImageFills = FigmaDataUtils.GetAllImageFillIdsFromFile(figmaFile, downloadPageIdList);
            
            if (foundImageFills.Count == 0)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("No Image Fills", "No image fills found in the document.", "OK");
                return;
            }
            
            EditorUtility.DisplayProgressBar(PROGRESS_BOX_TITLE, "Downloading image fill data...", 0);
            
            try
            {
                // Get image fill data
                var figmaTask = FigmaApiUtils.GetDocumentImageFillData(s_UnityFigmaBridgeSettings.FileId, s_PersonalAccessToken);
                await figmaTask;
                var activeFigmaImageFillData = figmaTask.Result;
                
                // Generate download list for image fills only
                var downloadList = FigmaApiUtils.GenerateDownloadQueue(activeFigmaImageFillData, new List<FigmaServerRenderData>(), new List<ServerRenderNodeData>());
                
                if (downloadList.Count == 0)
                {
                    EditorUtility.ClearProgressBar();
                    EditorUtility.DisplayDialog("No Images to Download", "All image fills are already up to date.", "OK");
                    return;
                }
                
                // Download all image files
                await FigmaApiUtils.DownloadFiles(downloadList, s_UnityFigmaBridgeSettings);
                
                EditorUtility.ClearProgressBar();
                AssetDatabase.Refresh();
                Debug.Log($"Image fills sync completed. {downloadList.Count} images processed.");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error syncing image fills: {e}");
                EditorUtility.ClearProgressBar();
                ReportError("Error downloading image fills", e.ToString());
            }
        }

        /// <summary>
        /// Check to make sure all requirements are met before syncing
        /// </summary>
        /// <param name="checkPrototypeFlow">If true, checks and sets up prototype flow requirements. Set to false for reprocessing or server image sync only.</param>
        /// <returns></returns>
        public static bool CheckRequirements(bool checkPrototypeFlow = true) {
            
            // Find the settings asset if it exists
            if (s_UnityFigmaBridgeSettings == null)
                s_UnityFigmaBridgeSettings = UnityFigmaBridgeSettingsProvider.FindUnityBridgeSettingsAsset();
            
            if (s_UnityFigmaBridgeSettings == null)
            {
                if (
                    EditorUtility.DisplayDialog("No Unity Figma Bridge Settings File",
                        "Create a new Unity Figma bridge settings file? ", "Create", "Cancel"))
                {
                    s_UnityFigmaBridgeSettings =
                        UnityFigmaBridgeSettingsProvider.GenerateUnityFigmaBridgeSettingsAsset();
                }
                else
                {
                    return false;
                }
            }

            // Initialize FigmaPaths with configured assets root folder
            FigmaPaths.FigmaAssetsRootFolder = s_UnityFigmaBridgeSettings.FigmaAssetsRootFolder;
            
            if (Shader.Find("TextMeshPro/Mobile/Distance Field")==null)
            {
                EditorUtility.DisplayDialog("Text Mesh Pro" ,"You need to install TestMeshPro Essentials. Use Window->Text Mesh Pro->Import TMP Essential Resources","OK");
                return false;
            }
            
            if (s_UnityFigmaBridgeSettings.FileId.Length == 0)
            {
                EditorUtility.DisplayDialog("Missing Figma Document" ,"Figma Document Url is not valid, please enter valid URL","OK");
                return false;
            }
            
            // Get stored personal access key
            s_PersonalAccessToken = PlayerPrefs.GetString(FIGMA_PERSONAL_ACCESS_TOKEN_PREF_KEY);

            if (string.IsNullOrEmpty(s_PersonalAccessToken))
            {
                var setToken = RequestPersonalAccessToken();
                if (!setToken) return false;
            }
            
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Figma Unity Bridge Importer","Please exit play mode before importing", "OK");
                return false;
            }
            
            // Check all requirements for run time if required
            if (checkPrototypeFlow && s_UnityFigmaBridgeSettings.BuildPrototypeFlow)
            {
                if (!CheckRunTimeRequirements())
                    return false;
            }
            
            return true;
            
        }


        private static bool CheckRunTimeRequirements()
        {
            if (string.IsNullOrEmpty(s_UnityFigmaBridgeSettings.RunTimeAssetsScenePath))
            {
                if (
                    EditorUtility.DisplayDialog("No Figma Bridge Scene set",
                        "Use current scene for generating prototype flow? ", "OK", "Cancel"))
                {
                    var currentScene = SceneManager.GetActiveScene();
                    s_UnityFigmaBridgeSettings.RunTimeAssetsScenePath = currentScene.path;
                    EditorUtility.SetDirty(s_UnityFigmaBridgeSettings);
                    AssetDatabase.SaveAssetIfDirty(s_UnityFigmaBridgeSettings);
                }
                else
                {
                    return false;
                }
            }
            
            // If current scene doesnt match, switch
            if (SceneManager.GetActiveScene().path != s_UnityFigmaBridgeSettings.RunTimeAssetsScenePath)
            {
                if (EditorUtility.DisplayDialog("Figma Bridge Scene",
                        "Current Scene doesnt match Runtime asset scene - switch scenes?", "OK", "Cancel"))
                {
                    EditorSceneManager.OpenScene(s_UnityFigmaBridgeSettings.RunTimeAssetsScenePath);
                }
                else
                {
                    return false;
                }
            }
            
            // If we are building a prototype and settings allow, ensure we have a UI Controller component
            if (s_UnityFigmaBridgeSettings.BuildPrototypeFlow)
            {
                s_PrototypeFlowController = FigmaAssetGenerator.InitPrototypeFlowControllerOnScene();
            }
            else
            {
                // Skip creating PrototypeFlowController
                s_PrototypeFlowController = null;
            }
            
            return true;
        }

        [MenuItem("Figma Bridge/Select Settings File")]
        static void SelectSettings()
        {
            var bridgeSettings=UnityFigmaBridgeSettingsProvider.FindUnityBridgeSettingsAsset();
            Selection.activeObject = bridgeSettings;
        }

        [MenuItem("Figma Bridge/Set Personal Access Token")]
        static void SetPersonalAccessToken()
        {
            RequestPersonalAccessToken();
        }
        
        /// <summary>
        /// Launch window to request personal access token
        /// </summary>
        /// <returns></returns>
        static bool RequestPersonalAccessToken()
        {
            s_PersonalAccessToken = PlayerPrefs.GetString(FIGMA_PERSONAL_ACCESS_TOKEN_PREF_KEY);
            var newAccessToken = EditorInputDialog.Show( "Personal Access Token", "Please enter your Figma Personal Access Token (you can create in the 'Developer settings' page)",s_PersonalAccessToken);
            if (!string.IsNullOrEmpty(newAccessToken))
            {
                s_PersonalAccessToken = newAccessToken;
                Debug.Log( $"New access token set {s_PersonalAccessToken}");
                PlayerPrefs.SetString(FIGMA_PERSONAL_ACCESS_TOKEN_PREF_KEY,s_PersonalAccessToken);
                PlayerPrefs.Save();
                return true;
            }

            return false;
        }

        

        private static void ReportError(string message,string error)
        {
            EditorUtility.DisplayDialog("Unity Figma Bridge Error",message,"Ok");
            Debug.LogWarning($"{message}\n {error}\n");
        }

        public static async Task<FigmaFile> DownloadFigmaDocument(string fileId)
        {
            // Download figma document
            EditorUtility.DisplayProgressBar(PROGRESS_BOX_TITLE, $"Downloading file", 0);
            try
            {
                var figmaTask = FigmaApiUtils.GetFigmaDocument(fileId, s_PersonalAccessToken, true);
                await figmaTask;
                return figmaTask.Result;
            }
            catch (Exception e)
            {
                ReportError(
                    "Error downloading Figma document - Check your personal access key and document url are correct",
                    e.ToString());
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            return null;
        }

        private static async Task ImportDocument(string fileId, FigmaFile figmaFile, List<Node> downloadPageNodeList)
        {

            // Build a list of page IDs to download
            var downloadPageIdList = downloadPageNodeList.Select(p => p.id).ToList();
            
            // Store for old prefabs before directory creation (for orphan cleanup)
            var figmaBridgeProcessData = new FigmaImportProcessData
            {
                Settings = s_UnityFigmaBridgeSettings,
                SourceFile = figmaFile
            };
            
            // Ensure we have all required directories, and capture old paths for cleanup
            FigmaPaths.CreateRequiredDirectories(figmaBridgeProcessData);
            
            // Next build a list of all externally referenced components not included in the document (eg
            // from external libraries) and download
            var externalComponentList = FigmaDataUtils.FindMissingComponentDefinitions(figmaFile);
            
            // TODO - Implement external components
            // This is currently not working as only returns a depth of 1 of returned nodes. Need to get original files too
            /*
            FigmaFileNodes activeExternalComponentsData=null;
            if (externalComponentList.Count > 0)
            {
                EditorUtility.DisplayProgressBar(PROGRESS_BOX_TITLE, $"Getting external component data", 0);
                try
                {
                    var figmaTask = FigmaApiUtils.GetFigmaFileNodes(fileId, s_PersonalAccessToken,externalComponentList);
                    await figmaTask;
                    activeExternalComponentsData = figmaTask.Result;
                }
                catch (Exception e)
                {
                    EditorUtility.ClearProgressBar();
                    ReportError("Error downloading external component Data",e.ToString());
                    return;
                }
            }
            */

            // For any missing component definitions, we are going to find the first instance and switch it to be
            // The source component. This has to be done early to ensure download of server images
            //FigmaFileUtils.ReplaceMissingComponents(figmaFile,externalComponentList);
            
            // Some of the nodes, we'll want to identify to use Figma server side rendering (eg vector shapes, SVGs)
            // First up create a list of nodes we'll substitute with rendered images
            var serverRenderNodes = FigmaDataUtils.FindAllServerRenderNodesInFile(figmaFile,externalComponentList,downloadPageIdList);
            
            // For now, skip downloading server-rendered images during normal sync to avoid rate limiting
            // Users can use "Sync Server Rendered Images" menu item to download them separately with rate limiting
            if (serverRenderNodes.Count > 0)
            {
                var allNodeIds = serverRenderNodes.Select(serverRenderNode =>  FigmaDataUtils.ReplaceUnsafeFileCharactersForNodeId(serverRenderNode.SourceNode.id)).ToList();
                var placeholderCount = FigmaApiUtils.CreatePlaceholderImagesForBatch(FigmaPaths.FigmaServerRenderedImagesFolder, allNodeIds);
                Debug.Log($"Created {placeholderCount} placeholder images for server rendered nodes. Use 'Sync Server Rendered Images' to download actual images with rate limiting.");
            }

            // Make sure that existing downloaded assets are in the correct format
            FigmaApiUtils.CheckExistingAssetProperties();
            
            // Track fills that are actually used. This is needed as FIGMA has a way of listing any bitmap used rather than active 
            var foundImageFills = FigmaDataUtils.GetAllImageFillIdsFromFile(figmaFile,downloadPageIdList);
            
            // Get image fill data for the document (list of urls to download any bitmap data used)
            FigmaImageFillData activeFigmaImageFillData; 
            EditorUtility.DisplayProgressBar(PROGRESS_BOX_TITLE, $"Downloading image fill data", 0);
            try
            {
                var figmaTask = FigmaApiUtils.GetDocumentImageFillData(fileId, s_PersonalAccessToken);
                await figmaTask;
                activeFigmaImageFillData = figmaTask.Result;
            }
            catch (Exception e)
            {
                EditorUtility.ClearProgressBar();
                ReportError("Error downloading Figma Image Fill Data",e.ToString());
                return;
            }
            
            // Create placeholders for image fills
            if (foundImageFills.Count > 0)
            {
                var imageFillIds = foundImageFills.Select(id => FigmaDataUtils.ReplaceUnsafeFileCharactersForNodeId(id)).ToList();
                var placeholderCount = FigmaApiUtils.CreatePlaceholderImagesForBatch(FigmaPaths.FigmaImageFillFolder, imageFillIds);
                Debug.Log($"Created {placeholderCount} placeholder images for image fills.");
            }

            // Generate font mapping data
            var figmaFontMapTask = FontManager.GenerateFontMapForDocument(figmaFile,
                s_UnityFigmaBridgeSettings.EnableGoogleFontsDownloads);
            await figmaFontMapTask;
            var fontMap = figmaFontMapTask.Result;


            var componentData = new FigmaBridgeComponentData
            { 
                MissingComponentDefinitionsList = externalComponentList, 
            };
            
            // Update the process data with remaining info
            figmaBridgeProcessData.ComponentData = componentData;
            figmaBridgeProcessData.ServerRenderNodes = serverRenderNodes;
            figmaBridgeProcessData.PrototypeFlowController = s_PrototypeFlowController;
            figmaBridgeProcessData.FontMap = fontMap;
            figmaBridgeProcessData.PrototypeFlowStartPoints = FigmaDataUtils.GetAllPrototypeFlowStartingPoints(figmaFile);
            figmaBridgeProcessData.SelectedPagesForImport = downloadPageNodeList;
            figmaBridgeProcessData.NodeLookupDictionary = FigmaDataUtils.BuildNodeLookupDictionary(figmaFile);
            
            // Clear the existing screens on the flowScreen controller
            if (s_UnityFigmaBridgeSettings.BuildPrototypeFlow)
            {
                if (figmaBridgeProcessData.PrototypeFlowController)
                    figmaBridgeProcessData.PrototypeFlowController.ClearFigmaScreens();
            }

            GameObject root = null;
            try
            {
                root = FigmaAssetGenerator.BuildFigmaFile(figmaBridgeProcessData);
            }
            catch (Exception e)
            {
                ReportError("Error generating Figma document. Check log for details", e.ToString());
                EditorUtility.ClearProgressBar();
                CleanUpPostGeneration(root);
                return;
            }
           
            
            // Lastly, for prototype mode, instantiate the default flowScreen and set the scaler up appropriately
            if (s_UnityFigmaBridgeSettings.BuildPrototypeFlow)
            {
                // Make sure all required default elements are present
                var screenController = figmaBridgeProcessData.PrototypeFlowController;
                
                // Find default flow start position
                screenController.PrototypeFlowInitialScreenId =  FigmaDataUtils.FindPrototypeFlowStartScreenId(figmaBridgeProcessData.SourceFile);;

                if (screenController.ScreenParentTransform == null)
                    screenController.ScreenParentTransform=UnityUiUtils.CreateRectTransform("ScreenParentTransform",
                        figmaBridgeProcessData.PrototypeFlowController.transform as RectTransform);

                if (screenController.TransitionEffect == null)
                {
                    // Instantiate and apply the default transition effect (loaded from package assets folder)
                    var defaultTransitionAnimationEffect = AssetDatabase.LoadAssetAtPath("Packages/com.simonoliver.unityfigma/UnityFigmaBridge/Assets/TransitionFadeToBlack.prefab", typeof(GameObject)) as GameObject;
                    var transitionObject = (GameObject) PrefabUtility.InstantiatePrefab(defaultTransitionAnimationEffect,
                        screenController.transform.transform);
                    screenController.TransitionEffect =
                        transitionObject.GetComponent<TransitionEffect>();
                    
                    UnityUiUtils.SetTransformFullStretch(transitionObject.transform as RectTransform);
                }

                // Set start flowScreen on stage by default                
                var defaultScreenData = figmaBridgeProcessData.PrototypeFlowController.StartFlowScreen;
                if (defaultScreenData != null)
                {
                    var defaultScreenTransform = defaultScreenData.FigmaScreenPrefab.transform as RectTransform;
                    if (defaultScreenTransform != null)
                    {
                        var defaultSize = defaultScreenTransform.sizeDelta;
                        var canvasScaler = s_SceneCanvas.GetComponent<CanvasScaler>();
                        if (canvasScaler == null) canvasScaler = s_SceneCanvas.gameObject.AddComponent<CanvasScaler>();
                        canvasScaler.referenceResolution = defaultSize;
                        // If we are a vertical template, drive by width
                        canvasScaler.matchWidthOrHeight = (defaultSize.x>defaultSize.y) ? 1f : 0f; // Use height as driver
                        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    }

                    var screenInstance=(GameObject)PrefabUtility.InstantiatePrefab(defaultScreenData.FigmaScreenPrefab, figmaBridgeProcessData.PrototypeFlowController.ScreenParentTransform);
                    figmaBridgeProcessData.PrototypeFlowController.SetCurrentScreen(screenInstance,defaultScreenData.FigmaNodeId,true);
                }
            // Write CS file with references to flowScreen name
            if (s_UnityFigmaBridgeSettings.CreateScreenNameCSharpFile) ScreenNameCodeGenerator.WriteScreenNamesCodeFile(figmaBridgeProcessData.ScreenPrefabs);
            }
            
            // Clean up orphaned prefabs (those that existed before but don't exist in the new import)
            FigmaPaths.CleanupOrphanedPrefabs(figmaBridgeProcessData);
            
            CleanUpPostGeneration(root);
            EditorUtility.ClearProgressBar();
            AssetDatabase.Refresh();
        }

        /// <summary>
        ///  Clean up any leftover assets post-generation
        /// </summary>
        private static void CleanUpPostGeneration(GameObject root)
        {
            if (!s_UnityFigmaBridgeSettings.BuildPrototypeFlow && s_SceneCanvas != null)
            {
                // Destroy temporary canvas
                Object.DestroyImmediate(s_SceneCanvas.gameObject);
            }
            if(root != null)
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
