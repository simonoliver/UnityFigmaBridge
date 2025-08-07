using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityFigmaBridge.Editor.Extension.ImportCache;
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

        [MenuItem("Figma Bridge/Sync Document")]
        static void Sync()
        {
            SyncAsync();
        }
        
        private static async void SyncAsync()
        {
            ImportSessionCache.CacheClear();//キャッシュの削除
            
            var requirementsMet = CheckRequirements();
            if (!requirementsMet) return;
            
            FigmaFile figmaFile;
            List<Node> pageNodeList;
            if (s_UnityFigmaBridgeSettings.OnlyImportSelectedPages)
            {
                // node指定でページを取得
                var basePageFile = await DownloadFigmaNodesDocument(s_UnityFigmaBridgeSettings.FileId, s_UnityFigmaBridgeSettings.ImportSelectPageIdList);
                if (basePageFile == null) return;

                figmaFile = basePageFile.CreateFigmaFile();
                pageNodeList = FigmaDataUtils.GetPageNodes(figmaFile);
                
                // var downloadPageNodeIdList = pageNodeList.Select(p => p.id).ToList();
                // downloadPageNodeIdList.Sort();
                //
                // var settingsPageDataIdList = s_UnityFigmaBridgeSettings.PageDataList.Select(p => p.NodeId).ToList();
                // settingsPageDataIdList.Sort();
                //
                // if (!settingsPageDataIdList.SequenceEqual(downloadPageNodeIdList))
                // {
                //     ReportError("The pages found in the Figma document have changed - check your settings file and Sync again when ready", "");
                //     
                //     // Apply the new page list to serialized data and select to allow the user to change
                //     s_UnityFigmaBridgeSettings.RefreshForUpdatedPages(figmaFile);
                //     Selection.activeObject = s_UnityFigmaBridgeSettings;
                //     EditorUtility.SetDirty(s_UnityFigmaBridgeSettings);
                //     AssetDatabase.SaveAssetIfDirty(s_UnityFigmaBridgeSettings);
                //     AssetDatabase.Refresh();
                //     
                //     return;
                // }
                
                // 構成のチェックを通した上で、コンポーネント用のページを生成
                var componentIds = figmaFile.GetComponentIds();
                // node指定でコンポーネント取得
                var componentPageFile = await DownloadFigmaNodesDocument(s_UnityFigmaBridgeSettings.FileId, componentIds);
                if (componentPageFile != null)
                {
                    var pages = figmaFile.document.children;
                    int len = pages.Length;
                    Node[] newPages = new Node[len + 1];
                    Array.Copy(pages, newPages, len);
                    newPages[len] = componentPageFile.CreateFigmaComponentPageNode();
                    figmaFile.document.children = newPages;
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
            else
            {
                figmaFile = await DownloadFigmaDocument(s_UnityFigmaBridgeSettings.FileId);
                if (figmaFile == null) return;
                pageNodeList = FigmaDataUtils.GetPageNodes(figmaFile);
            }
            
            // 画像キャッシュここで構築しておく
            FigmaAssetGuidMapManager.CreateMap(FigmaAssetGuidMapManager.AssetType.ImageFill);
            
            await ImportDocument(s_UnityFigmaBridgeSettings.FileId, figmaFile, pageNodeList);
            
            FigmaAssetGuidMapManager.SaveAllMap();
            ImportSessionCache.CacheClear();
        }

        /// <summary>
        /// Check to make sure all requirements are met before syncing
        /// </summary>
        /// <returns></returns>
        public static bool CheckRequirements() {
            
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
            if (s_UnityFigmaBridgeSettings.BuildPrototypeFlow)
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
            
            // Find a canvas in the active scene
            s_SceneCanvas = Object.FindObjectOfType<Canvas>();
            
            // If doesnt exist create new one
            if (s_SceneCanvas == null)
            {
                s_SceneCanvas = CreateCanvas(true);
            }
            
            // If we are building a prototype, ensure we have a UI Controller component
            s_PrototypeFlowController = s_SceneCanvas.GetComponent<PrototypeFlowController>();
            if (s_PrototypeFlowController== null)
                s_PrototypeFlowController = s_SceneCanvas.gameObject.AddComponent<PrototypeFlowController>();
            
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


        private static Canvas CreateCanvas(bool createEventSystem)
        {
            // Canvas
            var canvasGameObject = new GameObject("Canvas");
            var canvas=canvasGameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGameObject.AddComponent<GraphicRaycaster>();

            if (!createEventSystem) return canvas;

            var existingEventSystem = Object.FindObjectOfType<EventSystem>();
            if (existingEventSystem == null)
            {
                // Create new event system
                var eventSystemGameObject = new GameObject("EventSystem");
                existingEventSystem=eventSystemGameObject.AddComponent<EventSystem>();
            }

            var pointerInputModule = Object.FindObjectOfType<PointerInputModule>();
            if (pointerInputModule == null)
            {
                // TODO - Allow for new input system?
                existingEventSystem.gameObject.AddComponent<StandaloneInputModule>();
            }

            return canvas;
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
        
        public static async Task<FigmaFileNodes> DownloadFigmaNodesDocument(string fileId, List<string> nodeId)
        {
            // Download figma document
            EditorUtility.DisplayProgressBar(PROGRESS_BOX_TITLE, $"Downloading file (Nodes)", 0);
            try
            {
                var figmaTask = FigmaApiUtils.GetFigmaFileNodes(fileId, s_PersonalAccessToken, nodeId);
                await figmaTask;
                return figmaTask.Result;
            }
            catch (Exception e)
            {
                ReportError(
                    "Error downloading Figma Component node",
                    e.ToString());
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            return null;
        }

        /// <summary>
        /// Figmaドキュメントのインポート
        /// </summary>
        /// <param name="fileId">FigmaファイルのID</param>
        /// <param name="figmaFile">Figmaデータ</param>
        /// <param name="downloadPageNodeList">ダウンロード対象のページノード一覧</param>
        private static async Task ImportDocument(string fileId, FigmaFile figmaFile, List<Node> downloadPageNodeList)
        {
            // Build a list of page IDs to download
            var downloadPageIdList = downloadPageNodeList.Select(p => p.id).ToList();
            
            // Ensure we have all required directories, and remove existing files
            // TODO - Once we move to processing only differences, we won't remove existing files
            FigmaPaths.CreateRequiredDirectories();
            
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
                        var figmaTask = FigmaApiUtils.GetFigmaServerRenderData(fileId, s_PersonalAccessToken,
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
            
            // Make sure that existing downloaded assets are in the correct format
            FigmaApiUtils.CheckExistingAssetProperties();
            
            // 利用されているImageをピックして保持する
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
            
            // Generate a list of all items that need to be downloaded
            var downloadList =
                FigmaApiUtils.GenerateDownloadQueue(activeFigmaImageFillData,foundImageFills, serverRenderData, serverRenderNodes);

            // Download all required files
            await FigmaApiUtils.DownloadFiles(downloadList, s_UnityFigmaBridgeSettings);
            
            // Generate font mapping data
            var figmaFontMapTask = FontManager.GenerateFontMapForDocument(figmaFile,
                s_UnityFigmaBridgeSettings.EnableGoogleFontsDownloads);
            await figmaFontMapTask;
            var fontMap = figmaFontMapTask.Result;
            
            var componentData = new FigmaBridgeComponentData
            { 
                MissingComponentDefinitionsList = externalComponentList, 
            };
            
            // Stores necessary importer data needed for document generator.
            var figmaBridgeProcessData = new FigmaImportProcessData
            {
                Settings=s_UnityFigmaBridgeSettings,
                SourceFile = figmaFile,
                ComponentData = componentData,
                ServerRenderNodes = serverRenderNodes,
                PrototypeFlowController = s_PrototypeFlowController,
                FontMap = fontMap,
                PrototypeFlowStartPoints = FigmaDataUtils.GetAllPrototypeFlowStartingPoints(figmaFile),
                SelectedPagesForImport = downloadPageNodeList,
                NodeLookupDictionary = FigmaDataUtils.BuildNodeLookupDictionary(figmaFile)
            };
            
            
            // Clear the existing screens on the flowScreen controller
            if (s_UnityFigmaBridgeSettings.BuildPrototypeFlow)
            {
                if (figmaBridgeProcessData.PrototypeFlowController)
                    figmaBridgeProcessData.PrototypeFlowController.ClearFigmaScreens();
            }
            else
            {
                s_SceneCanvas = CreateCanvas(false);
            }

            try
            {
                FigmaAssetGenerator.BuildFigmaFile(s_SceneCanvas, figmaBridgeProcessData, s_UnityFigmaBridgeSettings.SaveFigmaPageAsPrefab);
            }
            catch (Exception e)
            {
                ReportError("Error generating Figma document. Check log for details", e.ToString());
                EditorUtility.ClearProgressBar();
                CleanUpPostGeneration();
                return;
            }
            
            // Lastly, for prototype mode, instantiate the default flowScreen and set the scaler up appropriately
            if (s_UnityFigmaBridgeSettings.BuildPrototypeFlow)
            {
                // Make sure all required default elements are present
                var screenController = figmaBridgeProcessData.PrototypeFlowController;
                
                // Find default flow start position
                screenController.PrototypeFlowInitialScreenId =  FigmaDataUtils.FindPrototypeFlowStartScreenId(figmaBridgeProcessData.SourceFile);

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
            CleanUpPostGeneration();
            EditorUtility.ClearProgressBar();
            AssetDatabase.Refresh();
        }

        /// <summary>
        ///  Clean up any leftover assets post-generation
        /// </summary>
        private static void CleanUpPostGeneration()
        {
            if (!s_UnityFigmaBridgeSettings.BuildPrototypeFlow)
            {
                // Destroy temporary canvas
                Object.DestroyImmediate(s_SceneCanvas.gameObject);
            }
        }
    }
}
