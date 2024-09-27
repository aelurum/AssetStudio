using AssetStudio;
using CubismLive2DExtractor;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
using static AssetStudioGUI.Exporter;
using Object = AssetStudio.Object;

namespace AssetStudioGUI
{
    internal enum ExportType
    {
        Convert,
        Raw,
        Dump
    }

    internal enum ExportFilter
    {
        All,
        Selected,
        Filtered
    }

    internal enum ExportL2DFilter
    {
        All,
        Selected,
        SelectedWithFadeList,
        SelectedWithFade,
        SelectedWithClips,
    }

    internal enum ExportListType
    {
        XML
    }

    internal enum AssetGroupOption
    {
        TypeName,
        ContainerPath,
        ContainerPathFull,
        SourceFileName,
        SceneHierarchy,
    }

    internal enum ListSearchFilterMode
    {
        Include,
        Exclude,
        RegexName,
        RegexContainer,
    }

    [Flags]
    internal enum SelectedAssetType
    {
        Animator = 0x01,
        AnimationClip = 0x02,
        MonoBehaviourMoc = 0x04,
        MonoBehaviourFade = 0x08,
        MonoBehaviourFadeLst = 0x10
    }

    internal static class Studio
    {
        public static AssetsManager assetsManager = new AssetsManager();
        public static AssemblyLoader assemblyLoader = new AssemblyLoader();
        public static List<AssetItem> exportableAssets = new List<AssetItem>();
        public static List<AssetItem> visibleAssets = new List<AssetItem>();
        public static List<MonoBehaviour> cubismMocList = new List<MonoBehaviour>();
        private static Dictionary<Object, string> l2dResourceContainers = new Dictionary<Object, string>();
        internal static Action<string> StatusStripUpdate = x => { };

        public static int ExtractFolder(string path, string savePath)
        {
            int extractedCount = 0;
            Progress.Reset();
            var files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                var file = files[i];
                var fileOriPath = Path.GetDirectoryName(file);
                var fileSavePath = fileOriPath.Replace(path, savePath);
                extractedCount += ExtractFile(file, fileSavePath);
                Progress.Report(i + 1, files.Length);
            }
            return extractedCount;
        }

        public static int ExtractFile(string[] fileNames, string savePath)
        {
            int extractedCount = 0;
            Progress.Reset();
            for (var i = 0; i < fileNames.Length; i++)
            {
                var fileName = fileNames[i];
                extractedCount += ExtractFile(fileName, savePath);
                Progress.Report(i + 1, fileNames.Length);
            }
            return extractedCount;
        }

        public static int ExtractFile(string fileName, string savePath)
        {
            int extractedCount = 0;
            var reader = new FileReader(fileName);
            if (reader.FileType == FileType.BundleFile)
                extractedCount += ExtractBundleFile(reader, savePath);
            else if (reader.FileType == FileType.WebFile)
                extractedCount += ExtractWebDataFile(reader, savePath);
            else
                reader.Dispose();
            return extractedCount;
        }

        private static int ExtractBundleFile(FileReader reader, string savePath)
        {
            Logger.Info($"Decompressing {reader.FileName} ...");
            var bundleFile = new BundleFile(reader, assetsManager.ZstdEnabled, assetsManager.SpecifyUnityVersion);
            reader.Dispose();
            if (bundleFile.fileList.Length > 0)
            {
                var extractPath = Path.Combine(savePath, reader.FileName + "_unpacked");
                return ExtractStreamFile(extractPath, bundleFile.fileList);
            }
            return 0;
        }

        private static int ExtractWebDataFile(FileReader reader, string savePath)
        {
            Logger.Info($"Decompressing {reader.FileName} ...");
            var webFile = new WebFile(reader);
            reader.Dispose();
            if (webFile.fileList.Length > 0)
            {
                var extractPath = Path.Combine(savePath, reader.FileName + "_unpacked");
                return ExtractStreamFile(extractPath, webFile.fileList);
            }
            return 0;
        }

        private static int ExtractStreamFile(string extractPath, StreamFile[] fileList)
        {
            int extractedCount = 0;
            foreach (var file in fileList)
            {
                var filePath = Path.Combine(extractPath, file.path);
                var fileDirectory = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(fileDirectory))
                {
                    Directory.CreateDirectory(fileDirectory);
                }
                if (!File.Exists(filePath))
                {
                    using (var fileStream = File.Create(filePath))
                    {
                        file.stream.CopyTo(fileStream);
                    }
                    extractedCount += 1;
                }
                file.stream.Dispose();
            }
            return extractedCount;
        }

        public static (string, List<TreeNode>) BuildAssetData()
        {
            Logger.Info("Building asset list...");

            string productName = null;
            var objectCount = assetsManager.assetsFileList.Sum(x => x.Objects.Count);
            var objectAssetItemDic = new Dictionary<Object, AssetItem>(objectCount);
            var containers = new List<(PPtr<Object>, string)>();
            var tex2dArrayAssetList = new List<AssetItem>();
            l2dResourceContainers.Clear();
            var i = 0;
            Progress.Reset();
            foreach (var assetsFile in assetsManager.assetsFileList)
            {
                var preloadTable = Array.Empty<PPtr<Object>>();

                foreach (var asset in assetsFile.Objects)
                {
                    var assetItem = new AssetItem(asset);
                    objectAssetItemDic.Add(asset, assetItem);
                    assetItem.UniqueID = " #" + i;
                    var exportable = false;
                    switch (asset)
                    {
                        case PreloadData m_PreloadData:
                            preloadTable = m_PreloadData.m_Assets;
                            break;
                        case GameObject m_GameObject:
                            assetItem.Text = m_GameObject.m_Name;
                            break;
                        case Texture2D m_Texture2D:
                            if (!string.IsNullOrEmpty(m_Texture2D.m_StreamData?.path))
                                assetItem.FullSize = asset.byteSize + m_Texture2D.m_StreamData.size;
                            assetItem.Text = m_Texture2D.m_Name;
                            exportable = true;
                            break;
                        case Texture2DArray m_Texture2DArray:
                            if (!string.IsNullOrEmpty(m_Texture2DArray.m_StreamData?.path))
                                assetItem.FullSize = asset.byteSize + m_Texture2DArray.m_StreamData.size;
                            assetItem.Text = m_Texture2DArray.m_Name;
                            tex2dArrayAssetList.Add(assetItem);
                            exportable = true;
                            break;
                        case AudioClip m_AudioClip:
                            if (!string.IsNullOrEmpty(m_AudioClip.m_Source))
                                assetItem.FullSize = asset.byteSize + m_AudioClip.m_Size;
                            assetItem.Text = m_AudioClip.m_Name;
                            exportable = true;
                            break;
                        case VideoClip m_VideoClip:
                            if (!string.IsNullOrEmpty(m_VideoClip.m_OriginalPath))
                                assetItem.FullSize = asset.byteSize + m_VideoClip.m_ExternalResources.m_Size;
                            assetItem.Text = m_VideoClip.m_Name;
                            exportable = true;
                            break;
                        case Shader m_Shader:
                            assetItem.Text = m_Shader.m_ParsedForm?.m_Name ?? m_Shader.m_Name;
                            exportable = true;
                            break;
                        case Mesh _:
                        case TextAsset _:
                        case AnimationClip _:
                        case Font _:
                        case MovieTexture _:
                        case Sprite _:
                            assetItem.Text = ((NamedObject)asset).m_Name;
                            exportable = true;
                            break;
                        case Animator m_Animator:
                            if (m_Animator.m_GameObject.TryGet(out var gameObject))
                            {
                                assetItem.Text = gameObject.m_Name;
                            }
                            exportable = true;
                            break;
                        case MonoBehaviour m_MonoBehaviour:
                            var assetName = m_MonoBehaviour.m_Name;
                            if (m_MonoBehaviour.m_Script.TryGet(out var m_Script))
                            {
                                assetName = assetName == "" ? m_Script.m_ClassName : assetName;
                                if (m_Script.m_ClassName == "CubismMoc")
                                {
                                    cubismMocList.Add(m_MonoBehaviour);
                                }
                            }
                            assetItem.Text = assetName;
                            exportable = true;
                            break;
                        case PlayerSettings m_PlayerSettings:
                            productName = m_PlayerSettings.productName;
                            break;
                        case AssetBundle m_AssetBundle:
                            var isStreamedSceneAssetBundle = m_AssetBundle.m_IsStreamedSceneAssetBundle;
                            if (!isStreamedSceneAssetBundle)
                            {
                                preloadTable = m_AssetBundle.m_PreloadTable;
                            }
                            assetItem.Text = string.IsNullOrEmpty(m_AssetBundle.m_AssetBundleName) ? m_AssetBundle.m_Name : m_AssetBundle.m_AssetBundleName;

                            foreach (var m_Container in m_AssetBundle.m_Container)
                            {
                                var preloadIndex = m_Container.Value.preloadIndex;
                                var preloadSize = isStreamedSceneAssetBundle ? preloadTable.Length : m_Container.Value.preloadSize;
                                var preloadEnd = preloadIndex + preloadSize;
                                for (var k = preloadIndex; k < preloadEnd; k++)
                                {
                                    containers.Add((preloadTable[k], m_Container.Key));
                                }
                            }
                            break;
                        case ResourceManager m_ResourceManager:
                            foreach (var m_Container in m_ResourceManager.m_Container)
                            {
                                containers.Add((m_Container.Value, m_Container.Key));
                            }
                            break;
                        case NamedObject m_NamedObject:
                            assetItem.Text = m_NamedObject.m_Name;
                            break;
                    }
                    if (assetItem.Text == "")
                    {
                        assetItem.Text = assetItem.TypeString + assetItem.UniqueID;
                    }
                    if (Properties.Settings.Default.displayAll || exportable)
                    {
                        exportableAssets.Add(assetItem);
                    }
                    Progress.Report(++i, objectCount);
                }
            }
            foreach (var (pptr, container) in containers)
            {
                if (pptr.TryGet(out var obj))
                {
                    objectAssetItemDic[obj].Container = container;
                    switch (obj)
                    {
                        case AnimationClip _:
                        case GameObject _:
                        case Texture2D _:
                        case MonoBehaviour _:
                            l2dResourceContainers[obj] = container;
                            break;
                    }
                }
            }
            foreach (var tex2dAssetItem in tex2dArrayAssetList)
            {
                var m_Texture2DArray = (Texture2DArray)tex2dAssetItem.Asset;
                for (var layer = 0; layer < m_Texture2DArray.m_Depth; layer++)
                {
                    var fakeObj = new Texture2D(m_Texture2DArray, layer);
                    m_Texture2DArray.TextureList.Add(fakeObj);

                    var fakeItem = new AssetItem(fakeObj)
                    {
                        Text = fakeObj.m_Name,
                        Container = tex2dAssetItem.Container
                    };
                    exportableAssets.Add(fakeItem);
                }
            }
            foreach (var tmp in exportableAssets)
            {
                tmp.SetSubItems();
            }
            containers.Clear();
            tex2dArrayAssetList.Clear();

            visibleAssets = exportableAssets;

            if (!Properties.Settings.Default.buildTreeStructure)
            {
                Logger.Info("Building tree structure step is skipped");
                objectAssetItemDic.Clear();
                return (productName, new List<TreeNode>());
            }

            Logger.Info("Building tree structure...");

            var treeNodeCollection = new List<TreeNode>();
            var treeNodeDictionary = new Dictionary<GameObject, GameObjectTreeNode>();
            var assetsFileCount = assetsManager.assetsFileList.Count;
            var j = 0;
            Progress.Reset();
            foreach (var assetsFile in assetsManager.assetsFileList)
            {
                var fileNode = new TreeNode(assetsFile.fileName); //RootNode

                foreach (var obj in assetsFile.Objects)
                {
                    if (obj is GameObject m_GameObject)
                    {
                        if (!treeNodeDictionary.TryGetValue(m_GameObject, out var currentNode))
                        {
                            currentNode = new GameObjectTreeNode(m_GameObject);
                            treeNodeDictionary.Add(m_GameObject, currentNode);
                        }

                        foreach (var pptr in m_GameObject.m_Components)
                        {
                            if (pptr.TryGet(out var m_Component))
                            {
                                objectAssetItemDic[m_Component].TreeNode = currentNode;
                                if (m_Component is MeshFilter m_MeshFilter)
                                {
                                    if (m_MeshFilter.m_Mesh.TryGet(out var m_Mesh))
                                    {
                                        objectAssetItemDic[m_Mesh].TreeNode = currentNode;
                                    }
                                }
                                else if (m_Component is SkinnedMeshRenderer m_SkinnedMeshRenderer)
                                {
                                    if (m_SkinnedMeshRenderer.m_Mesh.TryGet(out var m_Mesh))
                                    {
                                        objectAssetItemDic[m_Mesh].TreeNode = currentNode;
                                    }
                                }
                            }
                        }

                        var parentNode = fileNode;

                        if (m_GameObject.m_Transform != null)
                        {
                            if (m_GameObject.m_Transform.m_Father.TryGet(out var m_Father))
                            {
                                if (m_Father.m_GameObject.TryGet(out var parentGameObject))
                                {
                                    if (!treeNodeDictionary.TryGetValue(parentGameObject, out var parentGameObjectNode))
                                    {
                                        parentGameObjectNode = new GameObjectTreeNode(parentGameObject);
                                        treeNodeDictionary.Add(parentGameObject, parentGameObjectNode);
                                    }
                                    parentNode = parentGameObjectNode;
                                }
                            }
                        }
                        parentNode.Nodes.Add(currentNode);
                    }
                }

                if (fileNode.Nodes.Count > 0)
                {
                    treeNodeCollection.Add(fileNode);
                }

                Progress.Report(++j, assetsFileCount);
            }
            treeNodeDictionary.Clear();
            objectAssetItemDic.Clear();

            return (productName, treeNodeCollection);
        }

        public static Dictionary<UnityVersion, SortedDictionary<int, TypeTreeItem>> BuildClassStructure()
        {
            var typeMap = new Dictionary<UnityVersion, SortedDictionary<int, TypeTreeItem>>();
            foreach (var assetsFile in assetsManager.assetsFileList)
            {
                if (typeMap.TryGetValue(assetsFile.version, out var curVer))
                {
                    foreach (var type in assetsFile.m_Types.Where(x => x.m_Type != null))
                    {
                        var key = type.classID;
                        if (type.m_ScriptTypeIndex >= 0)
                        {
                            key = -1 - type.m_ScriptTypeIndex;
                        }
                        curVer[key] = new TypeTreeItem(key, type.m_Type);
                    }
                }
                else
                {
                    var items = new SortedDictionary<int, TypeTreeItem>();
                    foreach (var type in assetsFile.m_Types.Where(x => x.m_Type != null))
                    {
                        var key = type.classID;
                        if (type.m_ScriptTypeIndex >= 0)
                        {
                            key = -1 - type.m_ScriptTypeIndex;
                        }
                        items[key] = new TypeTreeItem(key, type.m_Type);
                    }
                    typeMap.Add(assetsFile.version, items);
                }
            }
            return typeMap;
        }

        public static void ExportAssets(string savePath, List<AssetItem> toExportAssets, ExportType exportType)
        {
            ThreadPool.QueueUserWorkItem(state =>
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");

                var groupOption = (AssetGroupOption)Properties.Settings.Default.assetGroupOption;
                var parallelExportCount = Properties.Settings.Default.parallelExportCount <= 0
                    ? Environment.ProcessorCount - 1
                    : Math.Min(Properties.Settings.Default.parallelExportCount, Environment.ProcessorCount - 1);
                parallelExportCount = Properties.Settings.Default.parallelExport ? parallelExportCount : 1;
                var toExportAssetDict = new ConcurrentDictionary<AssetItem, string>();
                var toParallelExportAssetDict = new ConcurrentDictionary<AssetItem, string>();
                var exceptionMsgs = new ConcurrentDictionary<Exception, string>();
                var mode = exportType == ExportType.Dump ? "Dump" : "Export";
                var toExportCount = toExportAssets.Count;
                var exportedCount = 0;
                var i = 0;
                Progress.Reset();

                Parallel.ForEach(toExportAssets, asset =>
                {
                    string exportPath;
                    switch (groupOption)
                    {
                        case AssetGroupOption.TypeName:
                            exportPath = Path.Combine(savePath, asset.TypeString);
                            break;
                        case AssetGroupOption.ContainerPath:
                        case AssetGroupOption.ContainerPathFull:
                            if (!string.IsNullOrEmpty(asset.Container))
                            {
                                exportPath = Path.Combine(savePath, Path.GetDirectoryName(asset.Container));
                                if (groupOption == AssetGroupOption.ContainerPathFull)
                                {
                                    exportPath = Path.Combine(exportPath, Path.GetFileNameWithoutExtension(asset.Container));
                                }
                            }
                            else
                            {
                                exportPath = savePath;
                            }
                            break;
                        case AssetGroupOption.SourceFileName:
                            if (string.IsNullOrEmpty(asset.SourceFile.originalPath))
                            {
                                exportPath = Path.Combine(savePath, asset.SourceFile.fileName + "_export");
                            }
                            else
                            {
                                exportPath = Path.Combine(savePath, Path.GetFileName(asset.SourceFile.originalPath) + "_export", asset.SourceFile.fileName);
                            }
                            break;
                        case AssetGroupOption.SceneHierarchy:
                            if (asset.TreeNode != null)
                            {
                                exportPath = Path.Combine(savePath, asset.TreeNode.FullPath);
                            }
                            else
                            {
                                exportPath = Path.Combine(savePath, "_sceneRoot", asset.TypeString);
                            }
                            break;
                        default:
                            exportPath = savePath;
                            break;
                    }
                    exportPath += Path.DirectorySeparatorChar;

                    if (exportType == ExportType.Convert)
                    {
                        switch (asset.Type)
                        {
                            case ClassIDType.Texture2D:
                            case ClassIDType.Texture2DArrayImage:
                            case ClassIDType.Sprite:
                            case ClassIDType.AudioClip:
                                toParallelExportAssetDict.TryAdd(asset, exportPath);
                                break;
                            case ClassIDType.Texture2DArray:
                                var m_Texture2DArray = (Texture2DArray)asset.Asset;
                                toExportCount += m_Texture2DArray.TextureList.Count - 1;
                                foreach (var texture in m_Texture2DArray.TextureList)
                                {
                                    var fakeItem = new AssetItem(texture)
                                    {
                                        Text = texture.m_Name,
                                        Container = asset.Container,
                                    };
                                    toParallelExportAssetDict.TryAdd(fakeItem, exportPath);
                                }
                                break;
                            default:
                                toExportAssetDict.TryAdd(asset, exportPath);
                                break;
                        }
                    }
                    else
                    {
                        toExportAssetDict.TryAdd(asset, exportPath);
                    }
                });

                foreach (var toExportAsset in toExportAssetDict)
                {
                    var asset = toExportAsset.Key;
                    var exportPath = toExportAsset.Value;
                    var isExported = false;
                    try
                    {
                        Logger.Info($"[{exportedCount + 1}/{toExportCount}] {mode}ing {asset.TypeString}: {asset.Text}");
                        switch (exportType)
                        {
                            case ExportType.Raw:
                                isExported = ExportRawFile(asset, exportPath);
                                break;
                            case ExportType.Dump:
                                isExported = ExportDumpFile(asset, exportPath);
                                break;
                            case ExportType.Convert:
                                isExported = ExportConvertFile(asset, exportPath);
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"{mode} {asset.TypeString}: {asset.Text} error", ex);
                    }

                    if (isExported)
                    {
                        exportedCount++;
                    }
                    else
                    {
                        Logger.Warning($"Unable to {mode.ToLower()} {asset.TypeString}: {asset.Text}");
                    }

                    Progress.Report(++i, toExportCount);
                }

                Parallel.ForEach(toParallelExportAssetDict, new ParallelOptions { MaxDegreeOfParallelism = parallelExportCount }, (toExportAsset, loopState) =>
                {
                    var asset = toExportAsset.Key;
                    var exportPath = toExportAsset.Value;
                    try
                    {
                        if (ParallelExporter.ParallelExportConvertFile(asset, exportPath, out var debugLog))
                        {
                            Interlocked.Increment(ref exportedCount);
                            if (GUILogger.ShowDebugMessage)
                            {
                                Logger.Debug(debugLog);
                                StatusStripUpdate($"[{exportedCount}/{toExportCount}] Exporting {asset.TypeString}: {asset.Text}");
                            }
                            else
                            {
                                Logger.Info($"[{exportedCount}/{toExportCount}] Exporting {asset.TypeString}: {asset.Text}");
                            }
                        }
                        Interlocked.Increment(ref i);
                        Progress.Report(i, toExportCount);
                    }
                    catch (Exception ex)
                    {
                        if (parallelExportCount == 1)
                        {
                            Logger.Error($"{mode} {asset.TypeString}: {asset.Text} error", ex);
                        }
                        else
                        {
                            loopState.Break();
                            exceptionMsgs.TryAdd(ex, $"Exception occurred when exporting {asset.TypeString}: {asset.Text}\n{ex}\n");
                        }
                    }
                });
                ParallelExporter.ClearHash();

                foreach (var ex in exceptionMsgs)
                {
                    Logger.Error(ex.Value);
                }

                var statusText = exportedCount == 0 ? "Nothing exported." : $"Finished {mode.ToLower()}ing [{exportedCount}/{toExportCount}] assets.";
                if (toExportCount > exportedCount)
                {
                    statusText += exceptionMsgs.IsEmpty
                        ? $" {toExportCount - exportedCount} assets skipped (not extractable or files already exist)."
                        : " Export process was stopped because one or more exceptions occurred.";
                    Progress.Report(toExportCount, toExportCount);
                }
                Logger.Info(statusText);
                exceptionMsgs.Clear();

                if (Properties.Settings.Default.openAfterExport && exportedCount > 0)
                {
                    OpenFolderInExplorer(savePath);
                }
            });
        }

        public static void ExportAssetsList(string savePath, List<AssetItem> toExportAssets, ExportListType exportListType)
        {
            ThreadPool.QueueUserWorkItem(state =>
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");

                Progress.Reset();

                switch (exportListType)
                {
                    case ExportListType.XML:
                        var filename = Path.Combine(savePath, "assets.xml");
                        var doc = new XDocument(
                            new XElement("Assets",
                                new XAttribute("filename", filename),
                                new XAttribute("createdAt", DateTime.UtcNow.ToString("s")),
                                toExportAssets.Select(
                                    asset => new XElement("Asset",
                                        new XElement("Name", asset.Text),
                                        new XElement("Container", asset.Container),
                                        new XElement("Type", new XAttribute("id", (int)asset.Type), asset.TypeString),
                                        new XElement("PathID", asset.m_PathID),
                                        new XElement("Source", asset.SourceFile.fullName),
                                        new XElement("TreeNode", asset.TreeNode != null ? asset.TreeNode.FullPath : ""),
                                        new XElement("Size", asset.FullSize)
                                    )
                                )
                            )
                        );

                        doc.Save(filename);

                        break;
                }

                var statusText = $"Finished exporting asset list with {toExportAssets.Count} items.";

                Logger.Info(statusText);

                if (Properties.Settings.Default.openAfterExport && toExportAssets.Count > 0)
                {
                    OpenFolderInExplorer(savePath);
                }
            });
        }

        public static void ExportSplitObjects(string savePath, TreeNodeCollection nodes)
        {
            ThreadPool.QueueUserWorkItem(state =>
            {
                var count = nodes.Cast<TreeNode>().Sum(x => x.Nodes.Count);
                int k = 0;
                Progress.Reset();
                foreach (TreeNode node in nodes)
                {
                    //遍历一级子节点
                    foreach (GameObjectTreeNode j in node.Nodes)
                    {
                        //收集所有子节点
                        var gameObjects = new List<GameObject>();
                        CollectNode(j, gameObjects);
                        //跳过一些不需要导出的object
                        if (gameObjects.All(x => x.m_SkinnedMeshRenderer == null && x.m_MeshFilter == null))
                        {
                            Progress.Report(++k, count);
                            continue;
                        }
                        //处理非法文件名
                        var filename = FixFileName(j.Text);
                        //每个文件存放在单独的文件夹
                        var targetPath = $"{savePath}{filename}{Path.DirectorySeparatorChar}";
                        //重名文件处理
                        for (int i = 1; ; i++)
                        {
                            if (Directory.Exists(targetPath))
                            {
                                targetPath = $"{savePath}{filename} ({i}){Path.DirectorySeparatorChar}";
                            }
                            else
                            {
                                break;
                            }
                        }
                        Directory.CreateDirectory(targetPath);
                        //导出FBX
                        Logger.Info($"Exporting {filename}.fbx");
                        try
                        {
                            ExportGameObject(j.gameObject, targetPath);
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"Export GameObject:{j.Text} error", ex);
                        }

                        Progress.Report(++k, count);
                        Logger.Info($"Finished exporting {filename}.fbx");
                    }
                }
                if (Properties.Settings.Default.openAfterExport)
                {
                    OpenFolderInExplorer(savePath);
                }
                Logger.Info("Finished");
            });
        }

        private static void CollectNode(GameObjectTreeNode node, List<GameObject> gameObjects)
        {
            gameObjects.Add(node.gameObject);
            foreach (GameObjectTreeNode i in node.Nodes)
            {
                CollectNode(i, gameObjects);
            }
        }

        public static void ExportAnimatorWithAnimationClip(AssetItem animator, List<AssetItem> animationList, string exportPath)
        {
            ThreadPool.QueueUserWorkItem(state =>
            {
                Progress.Reset();
                Logger.Info($"Exporting {animator.Text}");
                Logger.Debug($"Selected AnimationClip(s):\n\"{string.Join("\"\n\"", animationList.Select(x => x.Text))}\"");
                try
                {
                    ExportAnimator(animator, exportPath, animationList);
                    if (Properties.Settings.Default.openAfterExport)
                    {
                        OpenFolderInExplorer(exportPath);
                    }
                    Progress.Report(1, 1);
                    Logger.Info($"Finished exporting {animator.Text}");
                }
                catch (Exception ex)
                {
                    Logger.Error($"Export Animator:{animator.Text} error", ex);
                    Logger.Info("Error in export");
                }
            });
        }

        public static void ExportObjectsWithAnimationClip(string exportPath, TreeNodeCollection nodes, List<AssetItem> animationList = null)
        {
            ThreadPool.QueueUserWorkItem(state =>
            {
                var gameObjects = new List<GameObject>();
                GetSelectedParentNode(nodes, gameObjects);
                if (gameObjects.Count > 0)
                {
                    var count = gameObjects.Count;
                    int i = 0;
                    Progress.Reset();
                    foreach (var gameObject in gameObjects)
                    {
                        Logger.Info($"Exporting {gameObject.m_Name}");
                        try
                        {
                            ExportGameObject(gameObject, exportPath, animationList);
                            Logger.Info($"Finished exporting {gameObject.m_Name}");
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"Export GameObject:{gameObject.m_Name} error", ex);
                            Logger.Info("Error in export");
                        }

                        Progress.Report(++i, count);
                    }
                    if (Properties.Settings.Default.openAfterExport)
                    {
                        OpenFolderInExplorer(exportPath);
                    }
                }
                else
                {
                    Logger.Info("No Object selected for export.");
                }
            });
        }

        public static void ExportObjectsMergeWithAnimationClip(string exportPath, List<GameObject> gameObjects, List<AssetItem> animationList = null)
        {
            ThreadPool.QueueUserWorkItem(state =>
            {
                var name = Path.GetFileName(exportPath);
                Progress.Reset();
                Logger.Info($"Exporting {name}");
                try
                {
                    ExportGameObjectMerge(gameObjects, exportPath, animationList);
                    Progress.Report(1, 1);
                    Logger.Info($"Finished exporting {name}");
                }
                catch (Exception ex)
                {
                    Logger.Error($"Export Model:{name} error", ex);
                    Logger.Info("Error in export");
                }
                if (Properties.Settings.Default.openAfterExport)
                {
                    OpenFolderInExplorer(Path.GetDirectoryName(exportPath));
                }
            });
        }

        public static void GetSelectedParentNode(TreeNodeCollection nodes, List<GameObject> gameObjects)
        {
            foreach (TreeNode i in nodes)
            {
                if (i is GameObjectTreeNode gameObjectTreeNode && i.Checked)
                {
                    gameObjects.Add(gameObjectTreeNode.gameObject);
                }
                else
                {
                    GetSelectedParentNode(i.Nodes, gameObjects);
                }
            }
        }

        public static TypeTree MonoBehaviourToTypeTree(MonoBehaviour m_MonoBehaviour)
        {
            SelectAssemblyFolder();
            return m_MonoBehaviour.ConvertToTypeTree(assemblyLoader);
        }

        private static void SelectAssemblyFolder()
        {
            if (!assemblyLoader.Loaded)
            {
                var openFolderDialog = new OpenFolderDialog();
                openFolderDialog.Title = "Select Assembly Folder";
                if (openFolderDialog.ShowDialog() == DialogResult.OK)
                {
                    assemblyLoader.Load(openFolderDialog.Folder);
                }
                else
                {
                    assemblyLoader.Loaded = true;
                }
            }
        }

        public static string DumpAsset(Object obj)
        {
            var str = obj.Dump();
            if (str == null && obj is MonoBehaviour m_MonoBehaviour)
            {
                var type = MonoBehaviourToTypeTree(m_MonoBehaviour);
                str = m_MonoBehaviour.Dump(type);
            }
            if (string.IsNullOrEmpty(str))
            {
                str = obj.DumpObject();
            }
            return str;
        }

        public static void OpenFolderInExplorer(string path)
        {
            var info = new ProcessStartInfo(path);
            info.UseShellExecute = true;
            Process.Start(info);
        }

        public static void ExportLive2D(string exportPath, List<MonoBehaviour> selMocs = null, List<AnimationClip> selClipMotions = null, List<MonoBehaviour> selFadeMotions = null, MonoBehaviour selFadeLst = null)
        {
            var baseDestPath = Path.Combine(exportPath, "Live2DOutput");
            var forceBezier = Properties.Settings.Default.l2dForceBezier;
            var mocList = selMocs ?? cubismMocList;
            var motionMode = Properties.Settings.Default.l2dMotionMode;
            if (selClipMotions != null)
                motionMode = Live2DMotionMode.AnimationClipV2;
            else if (selFadeMotions != null || selFadeLst != null)
                motionMode = Live2DMotionMode.MonoBehaviour;

            ThreadPool.QueueUserWorkItem(state =>
            {
                Logger.Info($"Searching for Live2D files...");

                var mocPathDict = new Dictionary<MonoBehaviour, (string, string)>();
                var mocPathList = new List<string>();
                foreach (var mocMonoBehaviour in cubismMocList)
                {
                    if (!l2dResourceContainers.TryGetValue(mocMonoBehaviour, out var fullContainerPath))
                        continue;

                    var pathSepIndex = fullContainerPath.LastIndexOf('/');
                    var basePath = pathSepIndex > 0
                        ? fullContainerPath.Substring(0, pathSepIndex)
                        : fullContainerPath;
                    mocPathDict.Add(mocMonoBehaviour, (fullContainerPath, basePath));
                }
                if (mocPathDict.Count == 0)
                {
                    Logger.Error("Live2D Cubism export error\r\nCannot find any model related files");
                    StatusStripUpdate("Live2D export canceled");
                    Progress.Reset();
                    return;
                }

                var basePathSet = mocPathDict.Values.Select(x => x.Item2).ToHashSet();
                var useFullContainerPath = mocPathDict.Count != basePathSet.Count;
                foreach (var moc in mocList)
                {
                    var mocPath = useFullContainerPath 
                        ? mocPathDict[moc].Item1  //fullContainerPath
                        : mocPathDict[moc].Item2; //basePath
                    mocPathList.Add(mocPath);
                }
                mocPathDict.Clear();

                var lookup = l2dResourceContainers.AsParallel().ToLookup(
                    x => mocPathList.Find(b => x.Value.Contains(b) && x.Value.Split('/').Any(y => y == b.Substring(b.LastIndexOf("/") + 1))),
                    x => x.Key
                );

                if (mocList[0].serializedType?.m_Type == null && !assemblyLoader.Loaded)
                {
                    Logger.Warning("Specifying the assembly folder may be needed for proper extraction");
                    SelectAssemblyFolder();
                }

                var totalModelCount = lookup.LongCount(x => x.Key != null);
                var modelCounter = 0;
                var parallelExportCount = Properties.Settings.Default.parallelExportCount <= 0
                    ? Environment.ProcessorCount - 1
                    : Math.Min(Properties.Settings.Default.parallelExportCount, Environment.ProcessorCount - 1);
                parallelExportCount = Properties.Settings.Default.parallelExport ? parallelExportCount : 1;
                foreach (var assets in lookup)
                {
                    var srcContainer = assets.Key;
                    if (srcContainer == null)
                        continue;
                    var container = srcContainer;

                    Logger.Info($"[{modelCounter + 1}/{totalModelCount}] Exporting Live2D: \"{srcContainer}\"...");
                    try
                    {
                        var modelName = useFullContainerPath
                            ? Path.GetFileNameWithoutExtension(container)
                            : container.Substring(container.LastIndexOf('/') + 1);
                        container = Path.HasExtension(container)
                            ? container.Replace(Path.GetExtension(container), "")
                            : container;
                        var destPath = Path.Combine(baseDestPath, container) + Path.DirectorySeparatorChar;

                        var modelExtractor = new Live2DExtractor(assets, selClipMotions, selFadeMotions, selFadeLst);
                        modelExtractor.ExtractCubismModel(destPath, modelName, motionMode, assemblyLoader, forceBezier, parallelExportCount);
                        modelCounter++;
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Live2D model export error: \"{srcContainer}\"", ex);
                    }
                    Progress.Report(modelCounter, (int)totalModelCount);
                }

                Logger.Info($"Finished exporting [{modelCounter}/{totalModelCount}] Live2D model(s).");
                if (modelCounter < totalModelCount)
                {
                    var total = (int)totalModelCount;
                    Progress.Report(total, total);
                }
                if (Properties.Settings.Default.openAfterExport && modelCounter > 0)
                {
                    OpenFolderInExplorer(exportPath);
                }
            });
        }
    }
}
