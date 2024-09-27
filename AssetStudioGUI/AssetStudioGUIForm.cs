using AssetStudio;
using Newtonsoft.Json;
using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Forms;
using static AssetStudioGUI.Studio;
using Font = AssetStudio.Font;
using Microsoft.WindowsAPICodePack.Taskbar;
#if NET472
using OpenTK;
using Vector3 = OpenTK.Vector3;
using Vector4 = OpenTK.Vector4;
#else
using Vector3 = OpenTK.Mathematics.Vector3;
using Vector4 = OpenTK.Mathematics.Vector4;
using Matrix4 = OpenTK.Mathematics.Matrix4;
#endif

namespace AssetStudioGUI
{
    partial class AssetStudioGUIForm : Form
    {
        private AssetItem lastSelectedItem;
        private DirectBitmap imageTexture;
        private string tempClipboard;

        private FMOD.System system;
        private FMOD.Sound sound;
        private FMOD.Channel channel;
        private FMOD.SoundGroup masterSoundGroup;
        private FMOD.MODE loopMode = FMOD.MODE.LOOP_OFF;
        private uint FMODlenms;
        private uint FMODloopstartms;
        private uint FMODloopendms;
        private float FMODVolume = 0.8f;

        #region SpriteControl
        private SpriteMaskMode spriteMaskVisibleMode = SpriteMaskMode.On;
        #endregion

        #region TexControl
        private static char[] textureChannelNames = new[] { 'B', 'G', 'R', 'A' };
        private bool[] textureChannels = new[] { true, true, true, true };
        #endregion

        #region GLControl
        private bool glControlLoaded;
        private int mdx, mdy;
        private bool lmdown, rmdown;
        private int pgmID, pgmColorID, pgmBlackID;
        private int attributeVertexPosition;
        private int attributeNormalDirection;
        private int attributeVertexColor;
        private int uniformModelMatrix;
        private int uniformViewMatrix;
        private int uniformProjMatrix;
        private int vao;
        private Vector3[] vertexData;
        private Vector3[] normalData;
        private Vector3[] normal2Data;
        private Vector4[] colorData;
        private Matrix4 modelMatrixData;
        private Matrix4 viewMatrixData;
        private Matrix4 projMatrixData;
        private int[] indiceData;
        private int wireFrameMode;
        private int shadeMode;
        private int normalMode;
        #endregion

        //asset list sorting
        private int sortColumn = -1;
        private bool reverseSort;

#if NET6_0_OR_GREATER
        private AlphanumComparatorFastNet alphanumComparator = new AlphanumComparatorFastNet();
#else
        private AlphanumComparatorFast alphanumComparator = new AlphanumComparatorFast();
#endif

        //asset list selection
        private List<int> selectedIndicesPrevList = new List<int>();
        private List<AssetItem> selectedAnimationAssetsList = new List<AssetItem>();

        //asset list filter
        private System.Timers.Timer delayTimer;
        private bool enableFiltering;

        //tree search
        private int nextGObject;
        private List<TreeNode> treeSrcResults = new List<TreeNode>();

        //tree selection
        private List<TreeNode> treeNodeSelectedList = new List<TreeNode>();
        private bool treeRecursionEnabled = true;
        private bool isRecursionEvent = false;

        private string openDirectoryBackup = string.Empty;
        private string saveDirectoryBackup = string.Empty;

        private GUILogger logger;

        private TaskbarManager taskbar = TaskbarManager.Instance;

        [DllImport("gdi32.dll")]
        private static extern IntPtr AddFontMemResourceEx(IntPtr pbFont, uint cbFont, IntPtr pdv, [In] ref uint pcFonts);

        private string guiTitle = string.Empty;

        public AssetStudioGUIForm()
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
            ConsoleWindow.RunConsole(Properties.Settings.Default.showConsole);
            InitializeComponent();

            var appAssembly = typeof(Program).Assembly.GetName();
            guiTitle = $"{appAssembly.Name} v{appAssembly.Version}";
            Text = guiTitle;

            delayTimer = new System.Timers.Timer(800);
            delayTimer.Elapsed += delayTimer_Elapsed;
            displayAll.Checked = Properties.Settings.Default.displayAll;
            displayInfo.Checked = Properties.Settings.Default.displayInfo;
            enablePreview.Checked = Properties.Settings.Default.enablePreview;
            showConsoleToolStripMenuItem.Checked = Properties.Settings.Default.showConsole;
            buildTreeStructureToolStripMenuItem.Checked = Properties.Settings.Default.buildTreeStructure;
            useAssetLoadingViaTypetreeToolStripMenuItem.Checked = Properties.Settings.Default.useTypetreeLoading;
            FMODinit();
            listSearchFilterMode.SelectedIndex = 0;

            logger = new GUILogger(StatusStripUpdate);
            Logger.Default = logger;
            writeLogToFileToolStripMenuItem.Checked = Properties.Settings.Default.useFileLogger;

            Progress.Default = new Progress<int>(SetProgressBarValue);
            Studio.StatusStripUpdate = StatusStripUpdate;
        }

        private void AssetStudioGUIForm_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
        }

        private async void AssetStudioGUIForm_DragDrop(object sender, DragEventArgs e)
        {
            var paths = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (paths.Length == 0)
                return;

            ResetForm();
            for (var i = 0; i < paths.Length; i++)
            {
                if (paths[i].ToLower().EndsWith(".lnk"))
                {
                    var targetPath = LnkReader.GetLnkTarget(paths[i]);
                    if (!string.IsNullOrEmpty(targetPath))
                    {
                        paths[i] = targetPath;
                    }
                }
            }
            await Task.Run(() => assetsManager.LoadFilesAndFolders(out openDirectoryBackup, paths));
            saveDirectoryBackup = openDirectoryBackup;
            BuildAssetStructures();
        }

        private async void loadFile_Click(object sender, EventArgs e)
        {
            openFileDialog1.InitialDirectory = openDirectoryBackup;
            if (openFileDialog1.ShowDialog(this) == DialogResult.OK)
            {
                ResetForm();
                await Task.Run(() => assetsManager.LoadFilesAndFolders(out openDirectoryBackup, openFileDialog1.FileNames));
                BuildAssetStructures();
            }
        }

        private async void loadFolder_Click(object sender, EventArgs e)
        {
            var openFolderDialog = new OpenFolderDialog();
            openFolderDialog.InitialFolder = openDirectoryBackup;
            if (openFolderDialog.ShowDialog(this) == DialogResult.OK)
            {
                ResetForm();
                await Task.Run(() => assetsManager.LoadFilesAndFolders(out openDirectoryBackup, openFolderDialog.Folder));
                BuildAssetStructures();
            }
        }

        private void specifyUnityVersion_Close(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(specifyUnityVersion.Text))
            {
                assetsManager.SpecifyUnityVersion = null;
                return;
            }
            
            try
            {
                assetsManager.SpecifyUnityVersion = new UnityVersion(specifyUnityVersion.Text);
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
            }
        }

        private async void extractFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog(this) == DialogResult.OK)
            {
                var saveFolderDialog = new OpenFolderDialog();
                saveFolderDialog.Title = "Select the save folder";
                if (saveFolderDialog.ShowDialog(this) == DialogResult.OK)
                {
                    var fileNames = openFileDialog1.FileNames;
                    var savePath = saveFolderDialog.Folder;
                    var extractedCount = await Task.Run(() => ExtractFile(fileNames, savePath));
                    Logger.Info($"Finished extracting {extractedCount} files.");
                }
            }
        }

        private async void extractFolderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var openFolderDialog = new OpenFolderDialog();
            if (openFolderDialog.ShowDialog(this) == DialogResult.OK)
            {
                var saveFolderDialog = new OpenFolderDialog();
                saveFolderDialog.Title = "Select the save folder";
                if (saveFolderDialog.ShowDialog(this) == DialogResult.OK)
                {
                    var path = openFolderDialog.Folder;
                    var savePath = saveFolderDialog.Folder;
                    var extractedCount = await Task.Run(() => ExtractFolder(path, savePath));
                    Logger.Info($"Finished extracting {extractedCount} files.");
                }
            }
        }

        private async void BuildAssetStructures()
        {
            if (assetsManager.assetsFileList.Count == 0)
            {
                Logger.Info("No Unity file can be loaded.");
                return;
            }

            var (productName, treeNodeCollection) = await Task.Run(() => BuildAssetData());
            var typeMap = await Task.Run(() => BuildClassStructure());
            productName = string.IsNullOrEmpty(productName) ? "no productName" : productName;

            Text = $"{guiTitle} - {productName} - {assetsManager.assetsFileList[0].version} - {assetsManager.assetsFileList[0].m_TargetPlatform}";

            assetListView.VirtualListSize = visibleAssets.Count;

            sceneTreeView.BeginUpdate();
            sceneTreeView.Nodes.AddRange(treeNodeCollection.ToArray());
            sceneTreeView.EndUpdate();
            treeNodeCollection.Clear();

            classesListView.BeginUpdate();
            foreach (var version in typeMap)
            {
                var versionGroup = new ListViewGroup(version.Key.FullVersion);
                classesListView.Groups.Add(versionGroup);

                foreach (var uclass in version.Value)
                {
                    uclass.Value.Group = versionGroup;
                    classesListView.Items.Add(uclass.Value);
                }
            }
            typeMap.Clear();
            classesListView.EndUpdate();

            var types = new SortedSet<string>();
            types.UnionWith(exportableAssets.Select(x => x.TypeString));
            if (Studio.cubismMocList.Count > 0)
            {
                types.Add("MonoBehaviour (Live2D Model)");
            }
            foreach (var typeString in types)
            {
                var typeItem = new ToolStripMenuItem
                {
                    CheckOnClick = true,
                    Name = typeString,
                    Size = new Size(180, 22),
                    Text = typeString
                };
                typeItem.Click += typeToolStripMenuItem_Click;
                filterTypeToolStripMenuItem.DropDownItems.Add(typeItem);
            }
            allToolStripMenuItem.Checked = true;
            var log = $"Finished loading {assetsManager.assetsFileList.Count} file(s) with {assetListView.Items.Count} exportable assets";
            var unityVer = assetsManager.assetsFileList[0].version;
            var m_ObjectsCount = unityVer > 2020 ?
                assetsManager.assetsFileList.Sum(x => x.m_Objects.LongCount(y => y.classID != (int)ClassIDType.Shader)) :
                assetsManager.assetsFileList.Sum(x => x.m_Objects.Count);
            var objectsCount = assetsManager.assetsFileList.Sum(x => x.Objects.Count);
            if (m_ObjectsCount != objectsCount)
            {
                log += $" and {m_ObjectsCount - objectsCount} assets failed to read";
            }
            Logger.Info(log);
        }

        private void typeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var typeItem = (ToolStripMenuItem)sender;
            if (typeItem != allToolStripMenuItem)
            {
                allToolStripMenuItem.Checked = false;

                var monoBehaviourItemArray = filterTypeToolStripMenuItem.DropDownItems.Find("MonoBehaviour", false);
                var monoBehaviourMocItemArray = filterTypeToolStripMenuItem.DropDownItems.Find("MonoBehaviour (Live2D Model)", false);
                if (monoBehaviourItemArray.Length > 0 && monoBehaviourMocItemArray.Length > 0)
                {
                    var monoBehaviourItem = (ToolStripMenuItem)monoBehaviourItemArray[0];
                    var monoBehaviourMocItem = (ToolStripMenuItem)monoBehaviourMocItemArray[0];
                    if (typeItem == monoBehaviourItem && monoBehaviourItem.Checked)
                    {
                        monoBehaviourMocItem.Checked = false;
                    }
                    else if (typeItem == monoBehaviourMocItem && monoBehaviourMocItem.Checked)
                    {
                        monoBehaviourItem.Checked = false;
                    }
                }
            }
            else if (allToolStripMenuItem.Checked)
            {
                for (var i = 1; i < filterTypeToolStripMenuItem.DropDownItems.Count; i++)
                {
                    var item = (ToolStripMenuItem)filterTypeToolStripMenuItem.DropDownItems[i];
                    item.Checked = false;
                }
            }
            FilterAssetList();
        }

        private void AssetStudioForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (glControl1.Visible)
            {
                if (e.Control)
                {
                    switch (e.KeyCode)
                    {
                        case Keys.W:
                            //Toggle WireFrame
                            wireFrameMode = (wireFrameMode + 1) % 3;
                            glControl1.Invalidate();
                            break;
                        case Keys.S:
                            //Toggle Shade
                            shadeMode = (shadeMode + 1) % 2;
                            glControl1.Invalidate();
                            break;
                        case Keys.N:
                            //Normal mode
                            normalMode = (normalMode + 1) % 2;
                            CreateVAO();
                            glControl1.Invalidate();
                            break;
                    }
                }
            }
            else if (previewPanel.Visible)
            {
                if (e.Control)
                {
                    var need = false;
                    if (lastSelectedItem?.Type == ClassIDType.Texture2D || lastSelectedItem?.Type == ClassIDType.Texture2DArrayImage)
                    {
                        switch (e.KeyCode)
                        {
                            case Keys.B:
                                textureChannels[0] = !textureChannels[0];
                                need = true;
                                break;
                            case Keys.G:
                                textureChannels[1] = !textureChannels[1];
                                need = true;
                                break;
                            case Keys.R:
                                textureChannels[2] = !textureChannels[2];
                                need = true;
                                break;
                            case Keys.A:
                                textureChannels[3] = !textureChannels[3];
                                need = true;
                                break;
                        }
                    }
                    else if (lastSelectedItem?.Type == ClassIDType.Sprite && !((Sprite)lastSelectedItem.Asset).m_RD.alphaTexture.IsNull)
                    {
                        switch (e.KeyCode)
                        {
                            case Keys.A:
                                spriteMaskVisibleMode = spriteMaskVisibleMode == SpriteMaskMode.On ? SpriteMaskMode.Off : SpriteMaskMode.On;
                                need = true;
                                break;
                            case Keys.M:
                                spriteMaskVisibleMode = spriteMaskVisibleMode == SpriteMaskMode.MaskOnly ? SpriteMaskMode.On : SpriteMaskMode.MaskOnly;
                                need = true;
                                break;
                        }
                    }
                    if (need)
                    {
                        if (lastSelectedItem != null)
                        {
                            PreviewAsset(lastSelectedItem);
                            assetInfoLabel.Text = lastSelectedItem.InfoText;
                        }
                    }
                }
            }
        }

        private void exportClassStructuresMenuItem_Click(object sender, EventArgs e)
        {
            if (classesListView.Items.Count > 0)
            {
                var saveFolderDialog = new OpenFolderDialog();
                if (saveFolderDialog.ShowDialog(this) == DialogResult.OK)
                {
                    var savePath = saveFolderDialog.Folder;
                    var count = classesListView.Items.Count;
                    int i = 0;
                    Progress.Reset();
                    foreach (TypeTreeItem item in classesListView.Items)
                    {
                        var versionPath = Path.Combine(savePath, item.Group.Header);
                        Directory.CreateDirectory(versionPath);

                        var saveFile = $"{versionPath}{Path.DirectorySeparatorChar}{item.SubItems[1].Text} {item.Text}.txt";
                        File.WriteAllText(saveFile, item.ToString());

                        Progress.Report(++i, count);
                    }

                    Logger.Info("Finished exporting class structures");
                }
            }
        }

        private void displayAll_CheckedChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.displayAll = displayAll.Checked;
            Properties.Settings.Default.Save();
        }

        private void enablePreview_Check(object sender, EventArgs e)
        {
            if (lastSelectedItem != null)
            {
                switch (lastSelectedItem.Type)
                {
                    case ClassIDType.Texture2D:
                    case ClassIDType.Sprite:
                        {
                            if (enablePreview.Checked && imageTexture != null)
                            {
                                previewPanel.Image = imageTexture.Bitmap;
                            }
                            else
                            {
                                previewPanel.Image = Properties.Resources.preview;
                                previewPanel.SizeMode = PictureBoxSizeMode.CenterImage;
                            }
                        }
                        break;
                    case ClassIDType.Shader:
                    case ClassIDType.TextAsset:
                    case ClassIDType.MonoBehaviour:
                        textPreviewBox.Visible = !textPreviewBox.Visible;
                        break;
                    case ClassIDType.Font:
                        fontPreviewBox.Visible = !fontPreviewBox.Visible;
                        break;
                    case ClassIDType.AudioClip:
                        {
                            FMODpanel.Visible = !FMODpanel.Visible;

                            if (sound.hasHandle() && channel.hasHandle())
                            {
                                var result = channel.isPlaying(out var playing);
                                if (result == FMOD.RESULT.OK && playing)
                                {
                                    channel.stop();
                                    FMODreset();
                                }
                            }
                            else if (FMODpanel.Visible)
                            {
                                PreviewAsset(lastSelectedItem);
                            }

                            break;
                        }

                }

            }
            else if (lastSelectedItem != null && enablePreview.Checked)
            {
                PreviewAsset(lastSelectedItem);
            }

            Properties.Settings.Default.enablePreview = enablePreview.Checked;
            Properties.Settings.Default.Save();
        }

        private void displayAssetInfo_Check(object sender, EventArgs e)
        {
            if (displayInfo.Checked && assetInfoLabel.Text != null)
            {
                assetInfoLabel.Visible = true;
            }
            else
            {
                assetInfoLabel.Visible = false;
            }

            Properties.Settings.Default.displayInfo = displayInfo.Checked;
            Properties.Settings.Default.Save();
        }

        private void showExpOpt_Click(object sender, EventArgs e)
        {
            var exportOpt = new ExportOptions();
            exportOpt.ShowDialog(this);
        }

        private void assetListView_RetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
        {
            e.Item = visibleAssets[e.ItemIndex];
        }

        private void tabPageSelected(object sender, TabControlEventArgs e)
        {
            switch (e.TabPageIndex)
            {
                case 0:
                    sceneTreeView.Select();
                    break;
                case 1:
                    assetListView.Select();
                    break;
            }
        }

        private void treeSearch_Enter(object sender, EventArgs e)
        {
            if (treeSearch.Text == " Search ")
            {
                treeSearch.Text = "";
                treeSearch.ForeColor = SystemColors.WindowText;
            }
        }

        private void treeSearch_Leave(object sender, EventArgs e)
        {
            if (treeSearch.Text == "")
            {
                treeSearch.Text = " Search ";
                treeSearch.ForeColor = SystemColors.GrayText;
            }
        }

        private void treeSearch_TextChanged(object sender, EventArgs e)
        {
            treeSrcResults.Clear();
            nextGObject = 0;
        }

        private void treeSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                if (treeSrcResults.Count == 0)
                {
                    foreach (TreeNode node in sceneTreeView.Nodes)
                    {
                        TreeNodeSearch(node);
                    }
                }
                if (treeSrcResults.Count > 0)
                {
                    if (nextGObject >= treeSrcResults.Count)
                    {
                        nextGObject = 0;
                    }
                    treeSrcResults[nextGObject].EnsureVisible();
                    sceneTreeView.SelectedNode = treeSrcResults[nextGObject];
                    nextGObject++;
                }
            }
        }

        private void TreeNodeSearch(TreeNode treeNode)
        {
            if (treeNode.Text.IndexOf(treeSearch.Text, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                treeSrcResults.Add(treeNode);
            }

            foreach (TreeNode node in treeNode.Nodes)
            {
                TreeNodeSearch(node);
            }
        }

        private void sceneTreeView_AfterCheck(object sender, TreeViewEventArgs e)
        {
            if (!treeRecursionEnabled)
                return;

            if (!isRecursionEvent)
            {
                if (e.Node.Checked)
                {
                    treeNodeSelectedList.Add(e.Node);
                }
                else
                {
                    treeNodeSelectedList.Remove(e.Node);
                }
            }

            foreach (TreeNode childNode in e.Node.Nodes)
            {
                isRecursionEvent = true;
                bool wasChecked = childNode.Checked;
                childNode.Checked = e.Node.Checked;
                if (!wasChecked && childNode.Checked)
                {
                    treeNodeSelectedList.Add(childNode);
                }
                else if (!childNode.Checked)
                {
                    treeNodeSelectedList.Remove(childNode);
                }
            }
            isRecursionEvent = false;

            StatusStripUpdate($"Selected {treeNodeSelectedList.Count} object(s).");
        }

        private void listSearch_Enter(object sender, EventArgs e)
        {
            if (listSearch.Text == " Filter ")
            {
                listSearch.Text = "";
                listSearch.ForeColor = SystemColors.WindowText;
                BeginInvoke(new Action(() => { enableFiltering = true; }));
            }
        }

        private void listSearch_Leave(object sender, EventArgs e)
        {
            if (listSearch.Text == "")
            {
                enableFiltering = false;
                listSearch.Text = " Filter ";
                listSearch.ForeColor = SystemColors.GrayText;
                listSearch.BackColor = SystemColors.Window;
            }
        }

        private void ListSearchTextChanged(object sender, EventArgs e)
        {
            if (enableFiltering)
            {
                if (delayTimer.Enabled)
                {
                    delayTimer.Stop();
                    delayTimer.Start();
                }
                else
                {
                    delayTimer.Start();
                }
            }
        }

        private void delayTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            delayTimer.Stop();
            ListSearchHistoryAdd();
            Invoke(new Action(FilterAssetList));
        }

        private void ListSearchHistoryAdd()
        {
            BeginInvoke(new Action(() =>
            {
                if (listSearch.Text != "" && listSearch.Text != " Filter ")
                {
                    if (listSearchHistory.Items.Count == listSearchHistory.MaxDropDownItems)
                    {
                        listSearchHistory.Items.RemoveAt(listSearchHistory.MaxDropDownItems - 1);
                    }
                    listSearchHistory.Items.Insert(0, listSearch.Text);
                }
            }));
        }

        private void assetListView_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            if (sortColumn != e.Column)
            {
                reverseSort = false;
            }
            else
            {
                reverseSort = !reverseSort;
            }
            sortColumn = e.Column;
            assetListView.BeginUpdate();
            assetListView.SelectedIndices.Clear();
            selectedIndicesPrevList.Clear();
            selectedAnimationAssetsList.Clear();
            if (sortColumn == 4) //FullSize
            {
                visibleAssets.Sort((a, b) =>
                {
                    var asf = a.FullSize;
                    var bsf = b.FullSize;
                    return reverseSort ? bsf.CompareTo(asf) : asf.CompareTo(bsf);
                });
            }
            else if (sortColumn == 3) // PathID
            {
                visibleAssets.Sort((x, y) =>
                {
                    long pathID_X = x.m_PathID;
                    long pathID_Y = y.m_PathID;
                    return reverseSort ? pathID_Y.CompareTo(pathID_X) : pathID_X.CompareTo(pathID_Y);
                });
            }
            else if (sortColumn == 0) // Name
            {
                visibleAssets.Sort((a, b) =>
                {
                    var at = a.SubItems[sortColumn].Text;
                    var bt = b.SubItems[sortColumn].Text;
                    return reverseSort ? alphanumComparator.Compare(bt, at) : alphanumComparator.Compare(at, bt);
                });
            }
            else
            {
                visibleAssets.Sort((a, b) =>
                {
                    var at = a.SubItems[sortColumn].Text.AsSpan();
                    var bt = b.SubItems[sortColumn].Text.AsSpan();

                    return reverseSort ? MemoryExtensions.CompareTo(bt, at, StringComparison.OrdinalIgnoreCase) : MemoryExtensions.CompareTo(at, bt, StringComparison.OrdinalIgnoreCase);
                });
            }
            assetListView.EndUpdate();
        }

        private void selectAsset(object sender, ListViewItemSelectionChangedEventArgs e)
        {
            previewPanel.Image = Properties.Resources.preview;
            previewPanel.SizeMode = PictureBoxSizeMode.CenterImage;
            classTextBox.Visible = false;
            assetInfoLabel.Visible = false;
            assetInfoLabel.Text = null;
            textPreviewBox.Visible = false;
            fontPreviewBox.Visible = false;
            FMODpanel.Visible = false;
            glControl1.Visible = false;
            StatusStripUpdate("");

            FMODreset();

            lastSelectedItem = (AssetItem)e.Item;

            if (e.IsSelected)
            {
                if (tabControl2.SelectedIndex == 1)
                {
                    dumpTextBox.Text = DumpAsset(lastSelectedItem.Asset);
                }
                if (enablePreview.Checked)
                {
                    PreviewAsset(lastSelectedItem);
                    if (displayInfo.Checked && lastSelectedItem.InfoText != null)
                    {
                        assetInfoLabel.Text = lastSelectedItem.InfoText;
                        assetInfoLabel.Visible = true;
                    }
                }
            }
        }

        private void classesListView_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
        {
            classTextBox.Visible = true;
            assetInfoLabel.Visible = false;
            assetInfoLabel.Text = null;
            textPreviewBox.Visible = false;
            fontPreviewBox.Visible = false;
            FMODpanel.Visible = false;
            glControl1.Visible = false;
            StatusStripUpdate("");
            if (e.IsSelected)
            {
                classTextBox.Text = ((TypeTreeItem)classesListView.SelectedItems[0]).ToString();
            }
        }

        private void preview_Resize(object sender, EventArgs e)
        {
            if (glControlLoaded && glControl1.Visible)
            {
                ChangeGLSize(glControl1.Size);
                glControl1.Invalidate();
            }
        }

        private void PreviewAsset(AssetItem assetItem)
        {
            if (assetItem == null)
                return;
            try
            {
                switch (assetItem.Type)
                {
                    case ClassIDType.Texture2D:
                    case ClassIDType.Texture2DArrayImage:
                        PreviewTexture2D(assetItem, assetItem.Asset as Texture2D);
                        break;
                    case ClassIDType.Texture2DArray:
                        PreviewTexture2DArray(assetItem, assetItem.Asset as Texture2DArray);
                        break;
                    case ClassIDType.AudioClip:
                        PreviewAudioClip(assetItem, assetItem.Asset as AudioClip);
                        break;
                    case ClassIDType.Shader:
                        PreviewShader(assetItem.Asset as Shader);
                        break;
                    case ClassIDType.TextAsset:
                        PreviewTextAsset(assetItem.Asset as TextAsset);
                        break;
                    case ClassIDType.MonoBehaviour:
                        var m_MonoBehaviour = (MonoBehaviour)assetItem.Asset;
                        if (m_MonoBehaviour.m_Script.TryGet(out var m_Script))
                        {
                            if (m_Script.m_ClassName == "CubismMoc")
                            {
                                PreviewMoc(assetItem, m_MonoBehaviour);
                                break;
                            }
                        }
                        PreviewMonoBehaviour(m_MonoBehaviour);
                        break;
                    case ClassIDType.Font:
                        PreviewFont(assetItem.Asset as Font);
                        break;
                    case ClassIDType.Mesh:
                        PreviewMesh(assetItem.Asset as Mesh);
                        break;
                    case ClassIDType.VideoClip:
                        PreviewVideoClip(assetItem, assetItem.Asset as VideoClip);
                        break;
                    case ClassIDType.MovieTexture:
                        StatusStripUpdate("Only supported export.");
                        break;
                    case ClassIDType.Sprite:
                        PreviewSprite(assetItem, assetItem.Asset as Sprite);
                        break;
                    case ClassIDType.Animator:
                        StatusStripUpdate("Can be exported to FBX file.");
                        break;
                    case ClassIDType.AnimationClip:
                        StatusStripUpdate("Can be exported with Animator or Objects");
                        break;
                    default:
                        var str = assetItem.Asset.Dump();
                        if (str != null)
                        {
                            textPreviewBox.Text = str;
                            textPreviewBox.Visible = true;
                        }
                        break;
                }
            }
            catch (Exception e)
            {
                MessageBox.Show($"Preview {assetItem.Type}:{assetItem.Text} error\r\n{e.Message}\r\n{e.StackTrace}");
            }
        }

        private void PreviewTexture2DArray(AssetItem assetItem, Texture2DArray m_Texture2DArray)
        {
            assetItem.InfoText = 
                $"Width: {m_Texture2DArray.m_Width}\n" +
                $"Height: {m_Texture2DArray.m_Height}\n" +
                $"Graphics format: {m_Texture2DArray.m_Format}\n" +
                $"Texture format: {m_Texture2DArray.m_Format.ToTextureFormat()}\n" +
                $"Texture count: {m_Texture2DArray.m_Depth}";
        }

        private void PreviewTexture2D(AssetItem assetItem, Texture2D m_Texture2D)
        {
            var image = m_Texture2D.ConvertToImage(true);
            if (image != null)
            {
                var bitmap = new DirectBitmap(image);
                image.Dispose();
                assetItem.InfoText = $"Width: {m_Texture2D.m_Width}\nHeight: {m_Texture2D.m_Height}\nFormat: {m_Texture2D.m_TextureFormat}";
                switch (m_Texture2D.m_TextureSettings.m_FilterMode)
                {
                    case 0: assetItem.InfoText += "\nFilter mode: Point "; break;
                    case 1: assetItem.InfoText += "\nFilter mode: Bilinear "; break;
                    case 2: assetItem.InfoText += "\nFilter mode: Trilinear "; break;
                }
                assetItem.InfoText += $"\nAnisotropic level: {m_Texture2D.m_TextureSettings.m_Aniso}\nMip map bias: {m_Texture2D.m_TextureSettings.m_MipBias}";
                switch (m_Texture2D.m_TextureSettings.m_WrapMode)
                {
                    case 0: assetItem.InfoText += "\nWrap mode: Repeat"; break;
                    case 1: assetItem.InfoText += "\nWrap mode: Clamp"; break;
                }
                assetItem.InfoText += "\nChannels: ";
                int validChannel = 0;
                for (int i = 0; i < 4; i++)
                {
                    if (textureChannels[i])
                    {
                        assetItem.InfoText += textureChannelNames[i];
                        validChannel++;
                    }
                }
                if (validChannel == 0)
                    assetItem.InfoText += "None";
                if (validChannel != 4)
                {
                    var bytes = bitmap.Bits;
                    for (int i = 0; i < bitmap.Height; i++)
                    {
                        int offset = Math.Abs(bitmap.Stride) * i;
                        for (int j = 0; j < bitmap.Width; j++)
                        {
                            bytes[offset] = textureChannels[0] ? bytes[offset] : validChannel == 1 && textureChannels[3] ? byte.MaxValue : byte.MinValue;
                            bytes[offset + 1] = textureChannels[1] ? bytes[offset + 1] : validChannel == 1 && textureChannels[3] ? byte.MaxValue : byte.MinValue;
                            bytes[offset + 2] = textureChannels[2] ? bytes[offset + 2] : validChannel == 1 && textureChannels[3] ? byte.MaxValue : byte.MinValue;
                            bytes[offset + 3] = textureChannels[3] ? bytes[offset + 3] : byte.MaxValue;
                            offset += 4;
                        }
                    }
                }
                var switchSwizzled = m_Texture2D.m_PlatformBlob.Length != 0;
                assetItem.InfoText += assetItem.Asset.platform == BuildTarget.Switch ? $"\nUses texture swizzling: {switchSwizzled}" : "";
                PreviewTexture(bitmap);

                StatusStripUpdate("'Ctrl'+'R'/'G'/'B'/'A' for Channel Toggle");
            }
            else
            {
                StatusStripUpdate("Unsupported image for preview");
            }
        }

        private void PreviewAudioClip(AssetItem assetItem, AudioClip m_AudioClip)
        {
            //Info
            assetItem.InfoText = "Compression format: ";
            if (m_AudioClip.version < 5)
            {
                switch (m_AudioClip.m_Type)
                {
                    case FMODSoundType.AAC:
                        assetItem.InfoText += "AAC";
                        break;
                    case FMODSoundType.AIFF:
                        assetItem.InfoText += "AIFF";
                        break;
                    case FMODSoundType.IT:
                        assetItem.InfoText += "Impulse tracker";
                        break;
                    case FMODSoundType.MOD:
                        assetItem.InfoText += "Protracker / Fasttracker MOD";
                        break;
                    case FMODSoundType.MPEG:
                        assetItem.InfoText += "MP2/MP3 MPEG";
                        break;
                    case FMODSoundType.OGGVORBIS:
                        assetItem.InfoText += "Ogg vorbis";
                        break;
                    case FMODSoundType.S3M:
                        assetItem.InfoText += "ScreamTracker 3";
                        break;
                    case FMODSoundType.WAV:
                        assetItem.InfoText += "Microsoft WAV";
                        break;
                    case FMODSoundType.XM:
                        assetItem.InfoText += "FastTracker 2 XM";
                        break;
                    case FMODSoundType.XMA:
                        assetItem.InfoText += "Xbox360 XMA";
                        break;
                    case FMODSoundType.VAG:
                        assetItem.InfoText += "PlayStation Portable ADPCM";
                        break;
                    case FMODSoundType.AUDIOQUEUE:
                        assetItem.InfoText += "iPhone";
                        break;
                    default:
                        assetItem.InfoText += $"Unknown ({m_AudioClip.m_Type})";
                        break;
                }
            }
            else
            {
                switch (m_AudioClip.m_CompressionFormat)
                {
                    case AudioCompressionFormat.PCM:
                        assetItem.InfoText += "PCM";
                        break;
                    case AudioCompressionFormat.Vorbis:
                        assetItem.InfoText += "Vorbis";
                        break;
                    case AudioCompressionFormat.ADPCM:
                        assetItem.InfoText += "ADPCM";
                        break;
                    case AudioCompressionFormat.MP3:
                        assetItem.InfoText += "MP3";
                        break;
                    case AudioCompressionFormat.PSMVAG:
                        assetItem.InfoText += "PlayStation Portable ADPCM";
                        break;
                    case AudioCompressionFormat.HEVAG:
                        assetItem.InfoText += "PSVita ADPCM";
                        break;
                    case AudioCompressionFormat.XMA:
                        assetItem.InfoText += "Xbox360 XMA";
                        break;
                    case AudioCompressionFormat.AAC:
                        assetItem.InfoText += "AAC";
                        break;
                    case AudioCompressionFormat.GCADPCM:
                        assetItem.InfoText += "Nintendo 3DS/Wii DSP";
                        break;
                    case AudioCompressionFormat.ATRAC9:
                        assetItem.InfoText += "PSVita ATRAC9";
                        break;
                    default:
                        assetItem.InfoText += "Unknown";
                        break;
                }
            }

            var m_AudioData = m_AudioClip.m_AudioData.GetData();
            if (m_AudioData == null || m_AudioData.Length == 0)
                return;

            if (!m_AudioClip.IsConvertSupport())
            {
                if (m_AudioClip.version >= 5)
                {
                    assetItem.InfoText += 
                        $"\nLength: {m_AudioClip.m_Length:0.0##}\n" +
                        $"Channel count: {m_AudioClip.m_Channels}\n" +
                        $"Sample rate: {m_AudioClip.m_Frequency}\n" +
                        $"Bit depth: {m_AudioClip.m_BitsPerSample}";
                }
                StatusStripUpdate("Preview is not available for non-fmod sounds. Try to export instead.");
                return;
            }

            var exinfo = new FMOD.CREATESOUNDEXINFO();

            exinfo.cbsize = Marshal.SizeOf(exinfo);
            exinfo.length = (uint)m_AudioClip.m_Size;

            var result = system.createSound(m_AudioData, FMOD.MODE.OPENMEMORY | loopMode, ref exinfo, out sound);
            if (ERRCHECK(result)) return;

            sound.getNumSubSounds(out var numsubsounds);

            if (numsubsounds > 0)
            {
                result = sound.getSubSound(0, out var subsound);
                if (result == FMOD.RESULT.OK)
                {
                    sound = subsound;
                }
            }

            result = sound.getLength(out FMODlenms, FMOD.TIMEUNIT.MS);
            if (ERRCHECK(result)) return;

            result = sound.getLoopPoints(out FMODloopstartms, FMOD.TIMEUNIT.MS, out FMODloopendms, FMOD.TIMEUNIT.MS);
            if (result == FMOD.RESULT.OK)
            {
                assetItem.InfoText += $"\nLoop Start: {(FMODloopstartms / 1000 / 60):00}:{(FMODloopstartms / 1000 % 60):00}.{(FMODloopstartms / 10 % 100):00}";
                assetItem.InfoText += $"\nLoop End: {(FMODloopendms / 1000 / 60):00}:{(FMODloopendms / 1000 % 60):00}.{(FMODloopendms / 10 % 100):00}";
            }
            
            _ = system.getMasterChannelGroup(out var channelGroup);
            result = system.playSound(sound, channelGroup, true, out channel);
            if (ERRCHECK(result)) return;

            FMODpanel.Visible = true;

            result = channel.getFrequency(out var frequency);
            if (ERRCHECK(result)) return;

            FMODinfoLabel.Text = frequency + " Hz";
            FMODtimerLabel.Text = $"00:00.00 / {(FMODlenms / 1000 / 60):00}:{(FMODlenms / 1000 % 60):00}.{(FMODlenms / 10 % 100):00}";
        }

        private void PreviewVideoClip(AssetItem assetItem, VideoClip m_VideoClip)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Width: {m_VideoClip.Width}");
            sb.AppendLine($"Height: {m_VideoClip.Height}");
            sb.AppendLine($"Frame rate: {m_VideoClip.m_FrameRate:.0##}");
            sb.AppendLine($"Split alpha: {m_VideoClip.m_HasSplitAlpha}");
            assetItem.InfoText = sb.ToString();
            
            StatusStripUpdate("Only supported export.");
        }

        private void PreviewShader(Shader m_Shader)
        {
            var str = ShaderConverter.Convert(m_Shader);
            PreviewText(str == null ? "Serialized Shader can't be read" : str.Replace("\n", "\r\n"));
        }

        private void PreviewTextAsset(TextAsset m_TextAsset)
        {
            var text = Encoding.UTF8.GetString(m_TextAsset.m_Script);
            text = text.Replace("\n", "\r\n").Replace("\0", "");
            PreviewText(text);
        }

        private void PreviewMonoBehaviour(MonoBehaviour m_MonoBehaviour)
        {
            var obj = m_MonoBehaviour.ToType();
            if (obj == null)
            {
                var type = MonoBehaviourToTypeTree(m_MonoBehaviour);
                obj = m_MonoBehaviour.ToType(type);
            }
            var str = JsonConvert.SerializeObject(obj, Formatting.Indented);
            PreviewText(str);
        }

        private void PreviewMoc(AssetItem assetItem, MonoBehaviour m_MonoBehaviour)
        {
            using (var cubismModel = new CubismModel(m_MonoBehaviour))
            {
                var sb = new StringBuilder();
                sb.AppendLine($"SDK Version: {cubismModel.VersionDescription}");
                if (cubismModel.Version > 0)
                {
                    sb.AppendLine($"Canvas Width: {cubismModel.CanvasWidth}");
                    sb.AppendLine($"Canvas Height: {cubismModel.CanvasHeight}");
                    sb.AppendLine($"Center X: {cubismModel.CentralPosX}");
                    sb.AppendLine($"Center Y: {cubismModel.CentralPosY}");
                    sb.AppendLine($"Pixel Per Unit: {cubismModel.PixelPerUnit}");
                    sb.AppendLine($"Parameter Count: {cubismModel.ParamCount}");
                    sb.AppendLine($"Part Count: {cubismModel.PartCount}");
                }
                assetItem.InfoText = sb.ToString();
            }
            StatusStripUpdate("Can be exported as Live2D Cubism model.");
        }

        private void PreviewFont(Font m_Font)
        {
            if (m_Font.m_FontData != null)
            {
                var data = Marshal.AllocCoTaskMem(m_Font.m_FontData.Length);
                Marshal.Copy(m_Font.m_FontData, 0, data, m_Font.m_FontData.Length);

                uint cFonts = 0;
                var re = AddFontMemResourceEx(data, (uint)m_Font.m_FontData.Length, IntPtr.Zero, ref cFonts);
                if (re != IntPtr.Zero)
                {
                    using (var pfc = new PrivateFontCollection())
                    {
                        pfc.AddMemoryFont(data, m_Font.m_FontData.Length);
                        Marshal.FreeCoTaskMem(data);
                        if (pfc.Families.Length > 0)
                        {
                            fontPreviewBox.SelectionStart = 0;
                            fontPreviewBox.SelectionLength = 80;
                            fontPreviewBox.SelectionFont = new System.Drawing.Font(pfc.Families[0], 16, FontStyle.Regular);
                            fontPreviewBox.SelectionStart = 81;
                            fontPreviewBox.SelectionLength = 56;
                            fontPreviewBox.SelectionFont = new System.Drawing.Font(pfc.Families[0], 12, FontStyle.Regular);
                            fontPreviewBox.SelectionStart = 138;
                            fontPreviewBox.SelectionLength = 56;
                            fontPreviewBox.SelectionFont = new System.Drawing.Font(pfc.Families[0], 18, FontStyle.Regular);
                            fontPreviewBox.SelectionStart = 195;
                            fontPreviewBox.SelectionLength = 56;
                            fontPreviewBox.SelectionFont = new System.Drawing.Font(pfc.Families[0], 24, FontStyle.Regular);
                            fontPreviewBox.SelectionStart = 252;
                            fontPreviewBox.SelectionLength = 56;
                            fontPreviewBox.SelectionFont = new System.Drawing.Font(pfc.Families[0], 36, FontStyle.Regular);
                            fontPreviewBox.SelectionStart = 309;
                            fontPreviewBox.SelectionLength = 56;
                            fontPreviewBox.SelectionFont = new System.Drawing.Font(pfc.Families[0], 48, FontStyle.Regular);
                            fontPreviewBox.SelectionStart = 366;
                            fontPreviewBox.SelectionLength = 56;
                            fontPreviewBox.SelectionFont = new System.Drawing.Font(pfc.Families[0], 60, FontStyle.Regular);
                            fontPreviewBox.SelectionStart = 423;
                            fontPreviewBox.SelectionLength = 55;
                            fontPreviewBox.SelectionFont = new System.Drawing.Font(pfc.Families[0], 72, FontStyle.Regular);
                            fontPreviewBox.Visible = true;
                        }
                    }
                    return;
                }
            }
            StatusStripUpdate("Unsupported font for preview. Try to export.");
        }

        private void PreviewMesh(Mesh m_Mesh)
        {
            if (m_Mesh.m_VertexCount > 0)
            {
                viewMatrixData = Matrix4.CreateRotationY(-(float)Math.PI / 4) * Matrix4.CreateRotationX(-(float)Math.PI / 6);
                #region Vertices
                if (m_Mesh.m_Vertices == null || m_Mesh.m_Vertices.Length == 0)
                {
                    StatusStripUpdate("Mesh can't be previewed.");
                    return;
                }
                int count = 3;
                if (m_Mesh.m_Vertices.Length == m_Mesh.m_VertexCount * 4)
                {
                    count = 4;
                }
                vertexData = new Vector3[m_Mesh.m_VertexCount];
                // Calculate Bounding
                float[] min = new float[3];
                float[] max = new float[3];
                for (int i = 0; i < 3; i++)
                {
                    min[i] = m_Mesh.m_Vertices[i];
                    max[i] = m_Mesh.m_Vertices[i];
                }
                for (int v = 0; v < m_Mesh.m_VertexCount; v++)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        min[i] = Math.Min(min[i], m_Mesh.m_Vertices[v * count + i]);
                        max[i] = Math.Max(max[i], m_Mesh.m_Vertices[v * count + i]);
                    }
                    vertexData[v] = new Vector3(
                        m_Mesh.m_Vertices[v * count],
                        m_Mesh.m_Vertices[v * count + 1],
                        m_Mesh.m_Vertices[v * count + 2]);
                }

                // Calculate modelMatrix
                Vector3 dist = Vector3.One, offset = Vector3.Zero;
                for (int i = 0; i < 3; i++)
                {
                    dist[i] = max[i] - min[i];
                    offset[i] = (max[i] + min[i]) / 2;
                }
                float d = Math.Max(1e-5f, dist.Length);
                modelMatrixData = Matrix4.CreateTranslation(-offset) * Matrix4.CreateScale(2f / d);
                #endregion
                #region Indicies
                indiceData = new int[m_Mesh.m_Indices.Count];
                for (int i = 0; i < m_Mesh.m_Indices.Count; i = i + 3)
                {
                    indiceData[i] = (int)m_Mesh.m_Indices[i];
                    indiceData[i + 1] = (int)m_Mesh.m_Indices[i + 1];
                    indiceData[i + 2] = (int)m_Mesh.m_Indices[i + 2];
                }
                #endregion
                #region Normals
                if (m_Mesh.m_Normals != null && m_Mesh.m_Normals.Length > 0)
                {
                    if (m_Mesh.m_Normals.Length == m_Mesh.m_VertexCount * 3)
                        count = 3;
                    else if (m_Mesh.m_Normals.Length == m_Mesh.m_VertexCount * 4)
                        count = 4;
                    normalData = new Vector3[m_Mesh.m_VertexCount];
                    for (int n = 0; n < m_Mesh.m_VertexCount; n++)
                    {
                        normalData[n] = new Vector3(
                            m_Mesh.m_Normals[n * count],
                            m_Mesh.m_Normals[n * count + 1],
                            m_Mesh.m_Normals[n * count + 2]);
                    }
                }
                else
                    normalData = null;
                // calculate normal by ourself
                normal2Data = new Vector3[m_Mesh.m_VertexCount];
                int[] normalCalculatedCount = new int[m_Mesh.m_VertexCount];
                for (int i = 0; i < m_Mesh.m_VertexCount; i++)
                {
                    normal2Data[i] = Vector3.Zero;
                    normalCalculatedCount[i] = 0;
                }
                for (int i = 0; i < m_Mesh.m_Indices.Count; i = i + 3)
                {
                    Vector3 dir1 = vertexData[indiceData[i + 1]] - vertexData[indiceData[i]];
                    Vector3 dir2 = vertexData[indiceData[i + 2]] - vertexData[indiceData[i]];
                    Vector3 normal = Vector3.Cross(dir1, dir2);
                    normal.Normalize();
                    for (int j = 0; j < 3; j++)
                    {
                        normal2Data[indiceData[i + j]] += normal;
                        normalCalculatedCount[indiceData[i + j]]++;
                    }
                }
                for (int i = 0; i < m_Mesh.m_VertexCount; i++)
                {
                    if (normalCalculatedCount[i] == 0)
                        normal2Data[i] = new Vector3(0, 1, 0);
                    else
                        normal2Data[i] /= normalCalculatedCount[i];
                }
                #endregion
                #region Colors
                if (m_Mesh.m_Colors != null && m_Mesh.m_Colors.Length == m_Mesh.m_VertexCount * 3)
                {
                    colorData = new Vector4[m_Mesh.m_VertexCount];
                    for (int c = 0; c < m_Mesh.m_VertexCount; c++)
                    {
                        colorData[c] = new Vector4(
                            m_Mesh.m_Colors[c * 3],
                            m_Mesh.m_Colors[c * 3 + 1],
                            m_Mesh.m_Colors[c * 3 + 2],
                            1.0f);
                    }
                }
                else if (m_Mesh.m_Colors != null && m_Mesh.m_Colors.Length == m_Mesh.m_VertexCount * 4)
                {
                    colorData = new Vector4[m_Mesh.m_VertexCount];
                    for (int c = 0; c < m_Mesh.m_VertexCount; c++)
                    {
                        colorData[c] = new Vector4(
                        m_Mesh.m_Colors[c * 4],
                        m_Mesh.m_Colors[c * 4 + 1],
                        m_Mesh.m_Colors[c * 4 + 2],
                        m_Mesh.m_Colors[c * 4 + 3]);
                    }
                }
                else
                {
                    colorData = new Vector4[m_Mesh.m_VertexCount];
                    for (int c = 0; c < m_Mesh.m_VertexCount; c++)
                    {
                        colorData[c] = new Vector4(0.5f, 0.5f, 0.5f, 1.0f);
                    }
                }
                #endregion
                glControl1.Visible = true;
                CreateVAO();
                StatusStripUpdate("Using OpenGL Version: " + GL.GetString(StringName.Version) + "\n"
                                  + "'Mouse Left'=Rotate | 'Mouse Right'=Move | 'Mouse Wheel'=Zoom \n"
                                  + "'Ctrl W'=Wireframe | 'Ctrl S'=Shade | 'Ctrl N'=ReNormal ");
            }
            else
            {
                StatusStripUpdate("Unable to preview this mesh");
            }
        }

        private void PreviewSprite(AssetItem assetItem, Sprite m_Sprite)
        {
            var image = m_Sprite.GetImage(spriteMaskMode: spriteMaskVisibleMode);
            if (image != null)
            {
                var bitmap = new DirectBitmap(image);
                image.Dispose();
                assetItem.InfoText = $"Width: {bitmap.Width}\nHeight: {bitmap.Height}\n";
                PreviewTexture(bitmap);

                if (!m_Sprite.m_RD.alphaTexture.IsNull)
                {
                    assetItem.InfoText += $"Alpha Mask: {spriteMaskVisibleMode}\n";
                    StatusStripUpdate("'Ctrl'+'A' - Enable/Disable alpha mask usage. 'Ctrl'+'M' - Show alpha mask only.");
                }
            }
            else
            {
                StatusStripUpdate("Unsupported sprite for preview.");
            }
        }

        private void PreviewTexture(DirectBitmap bitmap)
        {
            imageTexture?.Dispose();
            imageTexture = bitmap;
            previewPanel.Image = imageTexture.Bitmap;
            if (imageTexture.Width > previewPanel.Width || imageTexture.Height > previewPanel.Height)
                previewPanel.SizeMode = PictureBoxSizeMode.Zoom;
            else
                previewPanel.SizeMode = PictureBoxSizeMode.CenterImage;
        }

        private void PreviewText(string text)
        {
            textPreviewBox.Text = text;
            textPreviewBox.Visible = true;
        }

        private void SetProgressBarValue(int value)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => 
                {
                    progressBar1.Value = value;
                    progressBar1.Style = ProgressBarStyle.Continuous;
                }));
            }
            else
            {
                progressBar1.Style = ProgressBarStyle.Continuous;
                progressBar1.Value = value;
            }

            BeginInvoke(new Action(() => 
            {
                var max = progressBar1.Maximum;
                taskbar.SetProgressValue(value, max);
                if (value == max)
                    taskbar.SetProgressState(TaskbarProgressBarState.NoProgress);
                else
                    taskbar.SetProgressState(TaskbarProgressBarState.Normal);
            }));
        }

        private void StatusStripUpdate(string statusText)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => { toolStripStatusLabel1.Text = statusText; }));
            }
            else
            {
                toolStripStatusLabel1.Text = statusText;
            }
        }

        private void ResetForm()
        {
            Text = guiTitle;
            assetsManager.Clear();
            assemblyLoader.Clear();
            exportableAssets.Clear();
            visibleAssets.Clear();
            sceneTreeView.Nodes.Clear();
            assetListView.VirtualListSize = 0;
            assetListView.Items.Clear();
            classesListView.Items.Clear();
            classesListView.Groups.Clear();
            selectedAnimationAssetsList.Clear();
            selectedIndicesPrevList.Clear();
            cubismMocList.Clear();
            previewPanel.Image = Properties.Resources.preview;
            previewPanel.SizeMode = PictureBoxSizeMode.CenterImage;
            imageTexture?.Dispose();
            imageTexture = null;
            assetInfoLabel.Visible = false;
            assetInfoLabel.Text = null;
            textPreviewBox.Visible = false;
            fontPreviewBox.Visible = false;
            glControl1.Visible = false;
            lastSelectedItem = null;
            sortColumn = -1;
            reverseSort = false;
            enableFiltering = false;
            listSearch.Text = " Filter ";
            listSearch.ForeColor = SystemColors.GrayText;
            listSearch.BackColor = SystemColors.Window;
            if (tabControl1.SelectedIndex == 1)
                assetListView.Select();

            var count = filterTypeToolStripMenuItem.DropDownItems.Count;
            for (var i = 1; i < count; i++)
            {
                filterTypeToolStripMenuItem.DropDownItems.RemoveAt(1);
            }

            taskbar.SetProgressState(TaskbarProgressBarState.NoProgress);
            FMODreset();
        }

        private void tabControl2_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (tabControl2.SelectedIndex == 1 && lastSelectedItem != null)
            {
                dumpTextBox.Text = DumpAsset(lastSelectedItem.Asset);
            }
        }

        private void assetListView_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right && assetListView.SelectedIndices.Count > 0)
            {
                goToSceneHierarchyToolStripMenuItem.Visible = false;
                showOriginalFileToolStripMenuItem.Visible = false;
                exportAnimatorWithSelectedAnimationClipMenuItem.Visible = false;
                exportAsLive2DModelToolStripMenuItem.Visible = false;
                exportL2DWithFadeLstToolStripMenuItem.Visible = false;
                exportL2DWithFadeToolStripMenuItem.Visible = false;
                exportL2DWithClipsToolStripMenuItem.Visible = false;

                if (assetListView.SelectedIndices.Count == 1)
                {
                    goToSceneHierarchyToolStripMenuItem.Visible = true;
                    showOriginalFileToolStripMenuItem.Visible = true;
                }
                if (assetListView.SelectedIndices.Count >= 1)
                {
                    var selectedAssets = GetSelectedAssets();

                    var selectedTypes = (SelectedAssetType)0;
                    foreach (var asset in selectedAssets)
                    {
                        switch (asset.Asset)
                        {
                            case MonoBehaviour m_MonoBehaviour:
                                if (Studio.cubismMocList.Count > 0 && m_MonoBehaviour.m_Script.TryGet(out var m_Script))
                                {
                                    if (m_Script.m_ClassName == "CubismMoc")
                                    {
                                        selectedTypes |= SelectedAssetType.MonoBehaviourMoc;
                                    }
                                    else if (m_Script.m_ClassName == "CubismFadeMotionData")
                                    {
                                        selectedTypes |= SelectedAssetType.MonoBehaviourFade;
                                    }
                                    else if (m_Script.m_ClassName == "CubismFadeMotionList")
                                    {
                                        selectedTypes |= SelectedAssetType.MonoBehaviourFadeLst;
                                    }
                                }
                                break;
                            case AnimationClip _:
                                selectedTypes |= SelectedAssetType.AnimationClip;
                                break;
                            case Animator _:
                                selectedTypes |= SelectedAssetType.Animator;
                                break;
                        }
                    }
                    exportAnimatorWithSelectedAnimationClipMenuItem.Visible = (selectedTypes & SelectedAssetType.Animator) !=0 && (selectedTypes & SelectedAssetType.AnimationClip) != 0;
                    exportAsLive2DModelToolStripMenuItem.Visible = (selectedTypes & SelectedAssetType.MonoBehaviourMoc) != 0;
                    exportL2DWithFadeLstToolStripMenuItem.Visible = (selectedTypes & SelectedAssetType.MonoBehaviourMoc) !=0 && (selectedTypes & SelectedAssetType.MonoBehaviourFadeLst) != 0;
                    exportL2DWithFadeToolStripMenuItem.Visible = (selectedTypes & SelectedAssetType.MonoBehaviourMoc) != 0 && (selectedTypes & SelectedAssetType.MonoBehaviourFade) !=0;
                    exportL2DWithClipsToolStripMenuItem.Visible = (selectedTypes & SelectedAssetType.MonoBehaviourMoc) !=0 && (selectedTypes & SelectedAssetType.AnimationClip) != 0;
                }

                var selectedElement = assetListView.HitTest(new Point(e.X, e.Y));
                var subItemIndex = selectedElement.Item.SubItems.IndexOf(selectedElement.SubItem);
                tempClipboard = selectedElement.SubItem.Text;
                copyToolStripMenuItem.Text = $"Copy {assetListView.Columns[subItemIndex].Text}";
                contextMenuStrip1.Show(assetListView, e.X, e.Y);
            }
        }

        private void copyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Clipboard.SetDataObject(tempClipboard);
        }

        private void exportSelectedAssetsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ExportAssets(ExportFilter.Selected, ExportType.Convert);
        }

        private void dumpSelectedAssetsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ExportAssets(ExportFilter.Selected, ExportType.Dump);
        }

        private void showOriginalFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var selectAsset = (AssetItem)assetListView.Items[assetListView.SelectedIndices[0]];
            var args = $"/select, \"{selectAsset.SourceFile.originalPath ?? selectAsset.SourceFile.fullName}\"";
            var pfi = new ProcessStartInfo("explorer.exe", args);
            Process.Start(pfi);
        }

        private void exportAnimatorWithAnimationClipMenuItem_Click(object sender, EventArgs e)
        {
            AssetItem animator = null;
            var selectedAssets = GetSelectedAssets();
            foreach (var assetPreloadData in selectedAssets)
            {
                if (assetPreloadData.Type == ClassIDType.Animator)
                {
                    animator = assetPreloadData;
                }
            }

            if (animator != null)
            {
                var saveFolderDialog = new OpenFolderDialog();
                saveFolderDialog.InitialFolder = saveDirectoryBackup;
                if (saveFolderDialog.ShowDialog(this) == DialogResult.OK)
                {
                    saveDirectoryBackup = saveFolderDialog.Folder;
                    var exportPath = Path.Combine(saveFolderDialog.Folder, "Animator") + Path.DirectorySeparatorChar;
                    ExportAnimatorWithAnimationClip(animator, selectedAnimationAssetsList, exportPath);
                }
            }
        }

        private void exportSelectedObjectsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ExportObjects(false);
        }

        private void exportObjectsWithAnimationClipMenuItem_Click(object sender, EventArgs e)
        {
            ExportObjects(true);
        }

        private void ExportObjects(bool animation)
        {
            if (sceneTreeView.Nodes.Count > 0)
            {
                var saveFolderDialog = new OpenFolderDialog();
                saveFolderDialog.InitialFolder = saveDirectoryBackup;
                if (saveFolderDialog.ShowDialog(this) == DialogResult.OK)
                {
                    saveDirectoryBackup = saveFolderDialog.Folder;
                    var exportPath = Path.Combine(saveFolderDialog.Folder, "GameObject") + Path.DirectorySeparatorChar;
                    List<AssetItem> animationList = null;
                    if (animation)
                    {
                        animationList = GetSelectedAssets().Where(x => x.Type == ClassIDType.AnimationClip).ToList();
                        if (animationList.Count == 0)
                        {
                            animationList = null;
                        }
                    }
                    ExportObjectsWithAnimationClip(exportPath, sceneTreeView.Nodes, animationList);
                }
            }
            else
            {
                StatusStripUpdate("No Objects available for export");
            }
        }

        private void exportSelectedObjectsMergeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ExportMergeObjects(false);
        }

        private void exportSelectedObjectsMergeWithAnimationClipToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ExportMergeObjects(true);
        }

        private void ExportMergeObjects(bool animation)
        {
            if (sceneTreeView.Nodes.Count > 0)
            {
                var gameObjects = new List<GameObject>();
                GetSelectedParentNode(sceneTreeView.Nodes, gameObjects);
                if (gameObjects.Count > 0)
                {
                    var saveFileDialog = new SaveFileDialog();
                    saveFileDialog.FileName = gameObjects[0].m_Name + " (merge).fbx";
                    saveFileDialog.AddExtension = false;
                    saveFileDialog.Filter = "Fbx file (*.fbx)|*.fbx";
                    saveFileDialog.InitialDirectory = saveDirectoryBackup;
                    if (saveFileDialog.ShowDialog(this) == DialogResult.OK)
                    {
                        saveDirectoryBackup = Path.GetDirectoryName(saveFileDialog.FileName);
                        var exportPath = saveFileDialog.FileName;
                        List<AssetItem> animationList = null;
                        if (animation)
                        {
                            animationList = GetSelectedAssets().Where(x => x.Type == ClassIDType.AnimationClip).ToList();
                            if (animationList.Count == 0)
                            {
                                animationList = null;
                            }
                        }
                        ExportObjectsMergeWithAnimationClip(exportPath, gameObjects, animationList);
                    }
                }
                else
                {
                    StatusStripUpdate("No Object selected for export.");
                }
            }
        }

        private void goToSceneHierarchyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var selectAsset = (AssetItem)assetListView.Items[assetListView.SelectedIndices[0]];
            if (selectAsset.TreeNode != null)
            {
                sceneTreeView.SelectedNode = selectAsset.TreeNode;
                tabControl1.SelectedTab = tabPage1;
            }
        }

        private void exportAllAssetsMenuItem_Click(object sender, EventArgs e)
        {
            ExportAssets(ExportFilter.All, ExportType.Convert);
        }

        private void exportSelectedAssetsMenuItem_Click(object sender, EventArgs e)
        {
            ExportAssets(ExportFilter.Selected, ExportType.Convert);
        }

        private void exportFilteredAssetsMenuItem_Click(object sender, EventArgs e)
        {
            ExportAssets(ExportFilter.Filtered, ExportType.Convert);
        }

        private void toolStripMenuItem4_Click(object sender, EventArgs e)
        {
            ExportAssets(ExportFilter.All, ExportType.Raw);
        }

        private void toolStripMenuItem5_Click(object sender, EventArgs e)
        {
            ExportAssets(ExportFilter.Selected, ExportType.Raw);
        }

        private void toolStripMenuItem6_Click(object sender, EventArgs e)
        {
            ExportAssets(ExportFilter.Filtered, ExportType.Raw);
        }

        private void toolStripMenuItem7_Click(object sender, EventArgs e)
        {
            ExportAssets(ExportFilter.All, ExportType.Dump);
        }

        private void toolStripMenuItem8_Click(object sender, EventArgs e)
        {
            ExportAssets(ExportFilter.Selected, ExportType.Dump);
        }

        private void toolStripMenuItem9_Click(object sender, EventArgs e)
        {
            ExportAssets(ExportFilter.Filtered, ExportType.Dump);
        }

        private void toolStripMenuItem11_Click(object sender, EventArgs e)
        {
            ExportAssetsList(ExportFilter.All);
        }

        private void toolStripMenuItem12_Click(object sender, EventArgs e)
        {
            ExportAssetsList(ExportFilter.Selected);
        }

        private void toolStripMenuItem13_Click(object sender, EventArgs e)
        {
            ExportAssetsList(ExportFilter.Filtered);
        }

        private void exportAllObjectsSplitToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            if (sceneTreeView.Nodes.Count > 0)
            {
                var saveFolderDialog = new OpenFolderDialog();
                saveFolderDialog.InitialFolder = saveDirectoryBackup;
                if (saveFolderDialog.ShowDialog(this) == DialogResult.OK)
                {
                    saveDirectoryBackup = saveFolderDialog.Folder;
                    var savePath = saveFolderDialog.Folder + Path.DirectorySeparatorChar;
                    ExportSplitObjects(savePath, sceneTreeView.Nodes);
                }
            }
            else
            {
                StatusStripUpdate("No Objects available for export");
            }
        }

        private void assetListView_SelectedIndexChanged(object sender, EventArgs e)
        {
            ProcessSelectedItems();
        }

        private void assetListView_VirtualItemsSelectionRangeChanged(object sender, ListViewVirtualItemsSelectionRangeChangedEventArgs e)
        {
            ProcessSelectedItems();
        }

        private void ProcessSelectedItems()
        {
            if (assetListView.SelectedIndices.Count > 1)
            {
                StatusStripUpdate($"Selected {assetListView.SelectedIndices.Count} assets.");
            }

            var selectedIndicesList = assetListView.SelectedIndices.Cast<int>().ToList();

            var addedIndices = selectedIndicesList.Except(selectedIndicesPrevList).ToArray();
            foreach (var itemIndex in addedIndices)
            {
                selectedIndicesPrevList.Add(itemIndex);
                var selectedItem = (AssetItem)assetListView.Items[itemIndex];
                if (selectedItem.Type == ClassIDType.AnimationClip)
                {
                    selectedAnimationAssetsList.Add(selectedItem);
                }
            }

            var removedIndices = selectedIndicesPrevList.Except(selectedIndicesList).ToArray();
            foreach (var itemIndex in removedIndices)
            {
                selectedIndicesPrevList.Remove(itemIndex);
                var unselectedItem = (AssetItem)assetListView.Items[itemIndex];
                if (unselectedItem.Type == ClassIDType.AnimationClip)
                {
                    selectedAnimationAssetsList.Remove(unselectedItem);
                }
            }
        }

        private List<AssetItem> GetSelectedAssets()
        {
            var selectedAssets = new List<AssetItem>(assetListView.SelectedIndices.Count);
            foreach (int index in assetListView.SelectedIndices)
            {
                selectedAssets.Add((AssetItem)assetListView.Items[index]);
            }

            return selectedAssets;
        }

        private void FilterAssetList()
        {
            if (exportableAssets.Count < 1)
                return;

            assetListView.BeginUpdate();
            assetListView.SelectedIndices.Clear();
            var show = new List<ClassIDType>();
            var filterMoc = false;
            if (!allToolStripMenuItem.Checked)
            {
                for (var i = 1; i < filterTypeToolStripMenuItem.DropDownItems.Count; i++)
                {
                    var item = (ToolStripMenuItem)filterTypeToolStripMenuItem.DropDownItems[i];
                    if (item.Checked)
                    {
                        if (item.Name == "MonoBehaviour (Live2D Model)")
                            filterMoc = true;
                        else
                            show.Add((ClassIDType)Enum.Parse(typeof(ClassIDType), item.Text));
                    }
                }
                visibleAssets = filterMoc
                    ? exportableAssets.FindAll(x => cubismMocList.Contains(x.Asset) || show.Contains(x.Type))
                    : exportableAssets.FindAll(x => show.Contains(x.Type));
            }
            else
            {
                visibleAssets = exportableAssets;
            }
            if (listSearch.Text != " Filter ")
            {
                var mode = (ListSearchFilterMode)listSearchFilterMode.SelectedIndex;
                switch (mode)
                {
                    case ListSearchFilterMode.Include:
                        visibleAssets = visibleAssets.FindAll(x =>
                            x.Text.IndexOf(listSearch.Text, StringComparison.OrdinalIgnoreCase) >= 0
                            || x.SubItems[1].Text.IndexOf(listSearch.Text, StringComparison.OrdinalIgnoreCase) >= 0
                            || x.SubItems[3].Text.IndexOf(listSearch.Text, StringComparison.OrdinalIgnoreCase) >= 0);
                        break;
                    case ListSearchFilterMode.Exclude:
                        visibleAssets = visibleAssets.FindAll(x =>
                            x.Text.IndexOf(listSearch.Text, StringComparison.OrdinalIgnoreCase) <= 0
                            && x.SubItems[1].Text.IndexOf(listSearch.Text, StringComparison.OrdinalIgnoreCase) <= 0
                            && x.SubItems[3].Text.IndexOf(listSearch.Text, StringComparison.OrdinalIgnoreCase) <= 0);
                        break;
                    case ListSearchFilterMode.RegexName:
                    case ListSearchFilterMode.RegexContainer:
                        StatusStripUpdate("");
                        var pattern = listSearch.Text;
                        var regexOptions = RegexOptions.IgnoreCase | RegexOptions.Singleline;
                        try
                        {
                            if (mode == ListSearchFilterMode.RegexName)
                            {
                                visibleAssets = visibleAssets.FindAll(x => Regex.IsMatch(x.Text, pattern, regexOptions));
                            }
                            else
                            {
                                visibleAssets = visibleAssets.FindAll(x => Regex.IsMatch(x.SubItems[1].Text, pattern, regexOptions));
                            }
                            listSearch.BackColor = SystemInformation.HighContrast ? listSearch.BackColor : System.Drawing.Color.PaleGreen;
                        }
                        catch (ArgumentException e)
                        {
                            listSearch.BackColor = SystemInformation.HighContrast ? listSearch.BackColor : System.Drawing.Color.FromArgb(255, 160, 160);
                            StatusStripUpdate($"Regex error: {e.Message}");
                        }
                        catch (RegexMatchTimeoutException)
                        {
                            listSearch.BackColor = SystemInformation.HighContrast ? listSearch.BackColor : System.Drawing.Color.FromArgb(255, 160, 160);
                            StatusStripUpdate($"Timeout error");
                        }
                        break;
                }
            }
            assetListView.VirtualListSize = visibleAssets.Count;
            assetListView.EndUpdate();
        }

        private void ExportAssets(ExportFilter type, ExportType exportType)
        {
            if (exportableAssets.Count > 0)
            {
                var saveFolderDialog = new OpenFolderDialog();
                saveFolderDialog.InitialFolder = saveDirectoryBackup;
                if (saveFolderDialog.ShowDialog(this) == DialogResult.OK)
                {
                    timer.Stop();
                    saveDirectoryBackup = saveFolderDialog.Folder;
                    List<AssetItem> toExportAssets = null;
                    switch (type)
                    {
                        case ExportFilter.All:
                            toExportAssets = exportableAssets;
                            break;
                        case ExportFilter.Selected:
                            toExportAssets = GetSelectedAssets();
                            break;
                        case ExportFilter.Filtered:
                            toExportAssets = visibleAssets;
                            break;
                    }

                    if (toExportAssets != null && filterTypeToolStripMenuItem.DropDownItems.ContainsKey("Texture2DArray"))
                    {
                        var tex2dArrayImgPathIdSet = toExportAssets.FindAll(x => x.Type == ClassIDType.Texture2DArrayImage).Select(x => x.m_PathID).ToHashSet();
                        foreach (var pathId in tex2dArrayImgPathIdSet)
                        {
                            toExportAssets = toExportAssets.Where(x => 
                                x.Type != ClassIDType.Texture2DArray 
                                || (x.Type == ClassIDType.Texture2DArray && x.m_PathID != pathId))
                                .ToList();
                        }
                    }
                    Studio.ExportAssets(saveFolderDialog.Folder, toExportAssets, exportType);
                }
            }
            else
            {
                StatusStripUpdate("No exportable assets loaded");
            }
        }

        private void ExportAssetsList(ExportFilter type)
        {
            // XXX: Only exporting as XML for now, but would JSON(/CSV/other) be useful too?

            if (exportableAssets.Count > 0)
            {
                var saveFolderDialog = new OpenFolderDialog();
                saveFolderDialog.InitialFolder = saveDirectoryBackup;
                if (saveFolderDialog.ShowDialog(this) == DialogResult.OK)
                {
                    timer.Stop();
                    saveDirectoryBackup = saveFolderDialog.Folder;
                    List<AssetItem> toExportAssets = null;
                    switch (type)
                    {
                        case ExportFilter.All:
                            toExportAssets = exportableAssets;
                            break;
                        case ExportFilter.Selected:
                            toExportAssets = GetSelectedAssets();
                            break;
                        case ExportFilter.Filtered:
                            toExportAssets = visibleAssets;
                            break;
                    }
                    Studio.ExportAssetsList(saveFolderDialog.Folder, toExportAssets, ExportListType.XML);
                }
            }
            else
            {
                StatusStripUpdate("No exportable assets loaded");
            }
        }

        private void toolStripMenuItem15_Click(object sender, EventArgs e)
        {
            GUILogger.ShowDebugMessage = toolStripMenuItem15.Checked;
        }

        private void sceneTreeView_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                sceneTreeView.SelectedNode = e.Node;
                contextMenuStrip2.Show(sceneTreeView, e.Location.X, e.Location.Y);
            }
        }

        private void selectAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (TreeNode node in sceneTreeView.Nodes)
            {
                node.Checked = true;
            }
        }

        private void clearSelectionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            treeRecursionEnabled = false;
            for (var i = 0; i < treeNodeSelectedList.Count; i++)
            {
                treeNodeSelectedList[i].Checked = false;
            }
            treeRecursionEnabled = true;
            treeNodeSelectedList.Clear();
            StatusStripUpdate($"Selected {treeNodeSelectedList.Count} object(s).");
        }

        private void expandAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (sceneTreeView.Nodes.Count > 500)
            {
                MessageBox.Show("Too many elements.");
                return;
            }

            sceneTreeView.BeginUpdate();
            foreach (TreeNode node in sceneTreeView.Nodes)
            {
                node.ExpandAll();
            }
            sceneTreeView.EndUpdate();
        }

        private void collapseAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            sceneTreeView.BeginUpdate();
            foreach (TreeNode node in sceneTreeView.Nodes)
            {
                node.Collapse(ignoreChildren: false);
            }
            sceneTreeView.EndUpdate();
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var aboutForm = new AboutForm();
            aboutForm.ShowDialog(this);
        }

        private void listSearchFilterMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            listSearch.BackColor = SystemColors.Window;
            if (listSearch.Text != " Filter ")
            {
                FilterAssetList();
            }
        }

        private void listSearchHistory_SelectedIndexChanged(object sender, EventArgs e)
        {
            listSearch.Text = listSearchHistory.Text;
            listSearch.Focus();
            listSearch.SelectionStart = listSearch.Text.Length;
        }

        private void selectRelatedAsset(object sender, EventArgs e)
        {
            var selectedItem = (ToolStripMenuItem)sender;
            var index = int.Parse(selectedItem.Name.Split('_')[0]);

            assetListView.SelectedIndices.Clear();
            tabControl1.SelectedTab = tabPage2;
            var assetItem = assetListView.Items[index];
            assetItem.Selected = true;
            assetItem.EnsureVisible();
        }

        private void selectAllRelatedAssets(object sender, EventArgs e)
        {
            var selectedNode = sceneTreeView.SelectedNode;
            var relatedAssets = visibleAssets.FindAll(x => x.TreeNode == selectedNode);
            if (relatedAssets.Count > 0)
            {
                assetListView.SelectedIndices.Clear();
                tabControl1.SelectedTab = tabPage2;
                foreach (var asset in relatedAssets)
                {
                    var assetItem = assetListView.Items[assetListView.Items.IndexOf(asset)];
                    assetItem.Selected = true;
                }
                assetListView.Items[assetListView.Items.IndexOf(relatedAssets[0])].EnsureVisible();
            }
        }

        private void showRelatedAssetsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var selectedNode = sceneTreeView.SelectedNode;
            var relatedAssets = visibleAssets.FindAll(x => x.TreeNode == selectedNode);
            if (relatedAssets.Count == 0)
            {
                StatusStripUpdate("No related assets were found among the visible assets.");
            }
        }

        private void contextMenuStrip2_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            var selectedNode = sceneTreeView.SelectedNode;
            var relatedAssets = visibleAssets.FindAll(x => x.TreeNode == selectedNode);
            showRelatedAssetsToolStripMenuItem.DropDownItems.Clear();
            if (relatedAssets.Count > 1)
            {
                var assetItem = new ToolStripMenuItem
                {
                    CheckOnClick = false,
                    Name = "selectAllRelatedAssetsToolStripMenuItem",
                    Size = new Size(180, 22),
                    Text = "Select all"
                };
                assetItem.Click += selectAllRelatedAssets;
                showRelatedAssetsToolStripMenuItem.DropDownItems.Add(assetItem);
            }
            foreach (var asset in relatedAssets)
            {
                var index = assetListView.Items.IndexOf(asset);
                var assetItem = new ToolStripMenuItem
                {
                    CheckOnClick = false,
                    Name = $"{index}_{asset.TypeString}",
                    Size = new Size(180, 22),
                    Text = $"({asset.TypeString}) {asset.Text}"
                };
                assetItem.Click += selectRelatedAsset;
                showRelatedAssetsToolStripMenuItem.DropDownItems.Add(assetItem);
            }
        }

        private void showConsoleToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var showConsole = showConsoleToolStripMenuItem.Checked;
            if (showConsole)
                ConsoleWindow.ShowConsoleWindow();
            else
                ConsoleWindow.HideConsoleWindow();

            Properties.Settings.Default.showConsole = showConsole;
            Properties.Settings.Default.Save();
        }

        private void writeLogToFileToolStripMenuItem_CheckedChanged(object sender, EventArgs e)
        {
            var useFileLogger = writeLogToFileToolStripMenuItem.Checked;
            logger.UseFileLogger = useFileLogger;

            Properties.Settings.Default.useFileLogger = useFileLogger;
            Properties.Settings.Default.Save();
        }

        private void AssetStudioGUIForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            Logger.Verbose("Closing AssetStudio");
        }

        private void buildTreeStructureToolStripMenuItem_CheckedChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.buildTreeStructure = buildTreeStructureToolStripMenuItem.Checked;
            Properties.Settings.Default.Save();
        }

        private void exportAllL2D_Click(object sender, EventArgs e)
        {
            if (exportableAssets.Count > 0)
            {
                if (Studio.cubismMocList.Count == 0)
                {
                    Logger.Info("Live2D Cubism models were not found.");
                    return;
                }
                Live2DExporter();
            }
            else
            {
                Logger.Info("No exportable assets loaded");
            }
        }

        private void exportSelectedL2D_Click(object sender, EventArgs e)
        {
            ExportSelectedL2DModels(ExportL2DFilter.Selected);
        }

        private void exportSelectedL2DWithClips_Click(object sender, EventArgs e)
        {
            ExportSelectedL2DModels(ExportL2DFilter.SelectedWithClips);
        }

        private void exportSelectedL2DWithFadeMotions_Click(object sender, EventArgs e)
        {
            ExportSelectedL2DModels(ExportL2DFilter.SelectedWithFade);
        }

        private void exportSelectedL2DWithFadeList_Click(object sender, EventArgs e)
        {
            ExportSelectedL2DModels(ExportL2DFilter.SelectedWithFadeList);
        }

        private void ExportSelectedL2DModels(ExportL2DFilter l2dExportMode)
        {
            if (exportableAssets.Count == 0)
            {
                Logger.Info("No exportable assets loaded");
                return;
            }
            if (Studio.cubismMocList.Count == 0)
            {
                Logger.Info("Live2D Cubism models were not found.");
                return;
            }
            var selectedAssets = GetSelectedAssets();
            if (selectedAssets.Count == 0)
                return;

            MonoBehaviour selectedFadeLst = null;
            var selectedMocs = new List<MonoBehaviour>();
            var selectedFadeMotions = new List<MonoBehaviour>();
            var selectedClips = new List<AnimationClip>();
            foreach (var assetItem in selectedAssets)
            {
                if (assetItem.Asset is MonoBehaviour m_MonoBehaviour && m_MonoBehaviour.m_Script.TryGet(out var m_Script))
                {
                    if (m_Script.m_ClassName == "CubismMoc")
                    {
                        selectedMocs.Add(m_MonoBehaviour);
                    }
                    else if (m_Script.m_ClassName == "CubismFadeMotionData")
                    {
                        selectedFadeMotions.Add(m_MonoBehaviour);
                    }
                    else if (m_Script.m_ClassName == "CubismFadeMotionList")
                    {
                        selectedFadeLst = m_MonoBehaviour;
                    }
                }
                else if (assetItem.Asset is AnimationClip m_AnimationClip)
                {
                    selectedClips.Add(m_AnimationClip);
                }
            }
            if (selectedMocs.Count == 0)
            {
                Logger.Info("Live2D Cubism models were not selected.");
                return;
            }

            switch (l2dExportMode)
            {
                case ExportL2DFilter.Selected:
                    Live2DExporter(selectedMocs);
                    break;
                case ExportL2DFilter.SelectedWithFadeList:
                    if (selectedFadeLst == null)
                    {
                        Logger.Info("Fade Motion List was not selected.");
                        return;
                    }
                    Live2DExporter(selectedMocs, selFadeLst: selectedFadeLst);
                    break;
                case ExportL2DFilter.SelectedWithFade:
                    if (selectedFadeMotions.Count == 0)
                    {
                        Logger.Info("No Fade motions were selected.");
                        return;
                    }
                    Live2DExporter(selectedMocs, selFadeMotions: selectedFadeMotions);
                    break;
                case ExportL2DFilter.SelectedWithClips:
                    if (selectedClips.Count == 0)
                    {
                        Logger.Info("No AnimationClips were selected.");
                        return;
                    }
                    Live2DExporter(selectedMocs, selectedClips);
                    break;
            }
        }

        private void Live2DExporter(List<MonoBehaviour> selMocs = null, List<AnimationClip> selClipMotions = null, List<MonoBehaviour> selFadeMotions = null, MonoBehaviour selFadeLst = null)
        {
            var saveFolderDialog = new OpenFolderDialog();
            saveFolderDialog.InitialFolder = saveDirectoryBackup;
            if (saveFolderDialog.ShowDialog(this) == DialogResult.OK)
            {
                timer.Stop();
                saveDirectoryBackup = saveFolderDialog.Folder;
                Progress.Reset();
                BeginInvoke(new Action(() => { progressBar1.Style = ProgressBarStyle.Marquee; }));

                Studio.ExportLive2D(saveFolderDialog.Folder, selMocs, selClipMotions, selFadeMotions, selFadeLst);
            }
        }

        private void customCompressionZstd_CheckedChanged(object sender, EventArgs e)
        {
            customCompressionLZ4ToolStripMenuItem.Checked = !customCompressionZstdToolStripMenuItem.Checked;
            assetsManager.ZstdEnabled = customCompressionZstdToolStripMenuItem.Checked;
        }

        private void customCompressionLZ4_CheckedChanged(object sender, EventArgs e)
        {
            customCompressionZstdToolStripMenuItem.Checked = !customCompressionLZ4ToolStripMenuItem.Checked;
            assetsManager.ZstdEnabled = customCompressionZstdToolStripMenuItem.Checked;
        }

        private void useAssetLoadingViaTypetreeToolStripMenuItem_CheckedChanged(object sender, EventArgs e)
        {
            var isEnabled = useAssetLoadingViaTypetreeToolStripMenuItem.Checked;
            assetsManager.LoadingViaTypeTreeEnabled = isEnabled;
            Properties.Settings.Default.useTypetreeLoading = isEnabled;
            Properties.Settings.Default.Save();
        }

        #region FMOD
        private void FMODinit()
        {
            FMODreset();

            var result = FMOD.Factory.System_Create(out system);
            if (ERRCHECK(result)) { return; }

            result = system.getVersion(out var version);
            ERRCHECK(result);
            if (version < FMOD.VERSION.number)
            {
                Logger.Error($"Error! You are using an old version of FMOD {version:X}. This program requires {FMOD.VERSION.number:X}.");
                Application.Exit();
            }

            result = system.init(2, FMOD.INITFLAGS.NORMAL, IntPtr.Zero);
            if (ERRCHECK(result)) { return; }

            result = system.getMasterSoundGroup(out masterSoundGroup);
            if (ERRCHECK(result)) { return; }

            result = masterSoundGroup.setVolume(FMODVolume);
            if (ERRCHECK(result)) { return; }
        }

        private void FMODreset()
        {
            timer.Stop();
            FMODprogressBar.Value = 0;
            FMODtimerLabel.Text = "00:00.00 / 00:00.00";
            FMODstatusLabel.Text = "Stopped";
            FMODinfoLabel.Text = "";

            if (sound.hasHandle())
            {
                var result = sound.release();
                ERRCHECK(result);
                sound.clearHandle();
            }
        }

        private void FMODplayButton_Click(object sender, EventArgs e)
        {
            if (sound.hasHandle() && channel.hasHandle())
            {
                _ = system.getMasterChannelGroup(out var channelGroup);
                timer.Start();
                var result = channel.isPlaying(out var playing);
                if ((result != FMOD.RESULT.OK) && (result != FMOD.RESULT.ERR_INVALID_HANDLE))
                {
                    if (ERRCHECK(result)) { return; }
                }

                if (playing)
                {
                    result = channel.stop();
                    if (ERRCHECK(result)) { return; }

                    result = system.playSound(sound, channelGroup, false, out channel);
                    if (ERRCHECK(result)) { return; }

                    FMODpauseButton.Text = "Pause";
                }
                else
                {
                    result = system.playSound(sound, channelGroup, false, out channel);
                    if (ERRCHECK(result)) { return; }
                    FMODstatusLabel.Text = "Playing";

                    if (FMODprogressBar.Value > 0)
                    {
                        uint newms = FMODlenms / 1000 * (uint)FMODprogressBar.Value;

                        result = channel.setPosition(newms, FMOD.TIMEUNIT.MS);
                        if ((result != FMOD.RESULT.OK) && (result != FMOD.RESULT.ERR_INVALID_HANDLE))
                        {
                            if (ERRCHECK(result)) { return; }
                        }

                    }
                }
            }
        }

        private void FMODpauseButton_Click(object sender, EventArgs e)
        {
            if (sound.hasHandle() && channel.hasHandle())
            {
                var result = channel.isPlaying(out var playing);
                if ((result != FMOD.RESULT.OK) && (result != FMOD.RESULT.ERR_INVALID_HANDLE))
                {
                    if (ERRCHECK(result)) { return; }
                }

                if (playing)
                {
                    result = channel.getPaused(out var paused);
                    if (ERRCHECK(result)) { return; }
                    result = channel.setPaused(!paused);
                    if (ERRCHECK(result)) { return; }

                    if (paused)
                    {
                        FMODstatusLabel.Text = "Playing";
                        FMODpauseButton.Text = "Pause";
                        timer.Start();
                    }
                    else
                    {
                        FMODstatusLabel.Text = "Paused";
                        FMODpauseButton.Text = "Resume";
                        timer.Stop();
                    }
                }
            }
        }

        private void FMODstopButton_Click(object sender, EventArgs e)
        {
            if (channel.hasHandle())
            {
                var result = channel.isPlaying(out var playing);
                if ((result != FMOD.RESULT.OK) && (result != FMOD.RESULT.ERR_INVALID_HANDLE))
                {
                    if (ERRCHECK(result)) { return; }
                }

                if (playing)
                {
                    result = channel.stop();
                    if (ERRCHECK(result)) { return; }
                    //channel = null;
                    //don't FMODreset, it will nullify the sound
                    timer.Stop();
                    FMODprogressBar.Value = 0;
                    FMODtimerLabel.Text = "00:00.00 / 00:00.00";
                    FMODstatusLabel.Text = "Stopped";
                    FMODpauseButton.Text = "Pause";
                }
            }
        }

        private void FMODloopButton_CheckedChanged(object sender, EventArgs e)
        {
            FMOD.RESULT result;

            loopMode = FMODloopButton.Checked ? FMOD.MODE.LOOP_NORMAL : FMOD.MODE.LOOP_OFF;

            if (sound.hasHandle())
            {
                result = sound.setMode(loopMode);
                if (ERRCHECK(result)) { return; }
            }

            if (channel.hasHandle())
            {
                result = channel.isPlaying(out var playing);
                if ((result != FMOD.RESULT.OK) && (result != FMOD.RESULT.ERR_INVALID_HANDLE))
                {
                    if (ERRCHECK(result)) { return; }
                }

                result = channel.getPaused(out var paused);
                if ((result != FMOD.RESULT.OK) && (result != FMOD.RESULT.ERR_INVALID_HANDLE))
                {
                    if (ERRCHECK(result)) { return; }
                }

                if (playing || paused)
                {
                    result = channel.setMode(loopMode);
                    if (ERRCHECK(result)) { return; }
                }
            }
        }

        private void FMODvolumeBar_ValueChanged(object sender, EventArgs e)
        {
            FMODVolume = Convert.ToSingle(FMODvolumeBar.Value) / 10;

            var result = masterSoundGroup.setVolume(FMODVolume);
            if (ERRCHECK(result)) { return; }
        }

        private void FMODprogressBar_Scroll(object sender, EventArgs e)
        {
            if (channel.hasHandle())
            {
                uint newms = FMODlenms / 1000 * (uint)FMODprogressBar.Value;
                FMODtimerLabel.Text = $"{newms / 1000 / 60:00}:{newms / 1000 % 60:00}.{newms / 10 % 100:00} / {FMODlenms / 1000 / 60:00}:{FMODlenms / 1000 % 60:00}.{FMODlenms / 10 % 100:00}";
            }
        }

        private void FMODprogressBar_MouseDown(object sender, MouseEventArgs e)
        {
            timer.Stop();
        }

        private void FMODprogressBar_MouseUp(object sender, MouseEventArgs e)
        {
            if (channel.hasHandle())
            {
                uint newms = FMODlenms / 1000 * (uint)FMODprogressBar.Value;

                var result = channel.setPosition(newms, FMOD.TIMEUNIT.MS);
                if ((result != FMOD.RESULT.OK) && (result != FMOD.RESULT.ERR_INVALID_HANDLE))
                {
                    if (ERRCHECK(result)) { return; }
                }


                result = channel.isPlaying(out var playing);
                if ((result != FMOD.RESULT.OK) && (result != FMOD.RESULT.ERR_INVALID_HANDLE))
                {
                    if (ERRCHECK(result)) { return; }
                }

                if (playing) { timer.Start(); }
            }
        }

        private void timer_Tick(object sender, EventArgs e)
        {
            uint ms = 0;
            bool playing = false;
            bool paused = false;

            if (channel.hasHandle())
            {
                var result = channel.getPosition(out ms, FMOD.TIMEUNIT.MS);
                if ((result != FMOD.RESULT.OK) && (result != FMOD.RESULT.ERR_INVALID_HANDLE))
                {
                    ERRCHECK(result);
                }

                result = channel.isPlaying(out playing);
                if ((result != FMOD.RESULT.OK) && (result != FMOD.RESULT.ERR_INVALID_HANDLE))
                {
                    ERRCHECK(result);
                }

                result = channel.getPaused(out paused);
                if ((result != FMOD.RESULT.OK) && (result != FMOD.RESULT.ERR_INVALID_HANDLE))
                {
                    ERRCHECK(result);
                }
            }

            FMODtimerLabel.Text = $"{ms / 1000 / 60:00}:{ms / 1000 % 60:00}.{ms / 10 % 100:00} / {FMODlenms / 1000 / 60:00}:{FMODlenms / 1000 % 60:00}.{FMODlenms / 10 % 100:00}";
#if NETFRAMEWORK
            FMODprogressBar.Value = (int)Math.Max(0, Math.Min(ms * 1000f / FMODlenms, 1000));
#else
            FMODprogressBar.Value = (int)Math.Clamp(ms * 1000f / FMODlenms, 0, 1000);
#endif
            FMODstatusLabel.Text = paused ? "Paused " : playing ? "Playing" : "Stopped";

            if (system.hasHandle() && channel.hasHandle())
            {
                system.update();
            }
        }

        private bool ERRCHECK(FMOD.RESULT result)
        {
            if (result != FMOD.RESULT.OK)
            {
                FMODreset();
                Logger.Warning($"FMOD error! {result} - {FMOD.Error.String(result)}");
                return true;
            }
            return false;
        }
        #endregion

        #region GLControl
        private void InitOpenTK()
        {
            ChangeGLSize(glControl1.Size);
            GL.ClearColor(System.Drawing.Color.CadetBlue);
            pgmID = GL.CreateProgram();
            LoadShader("vs", ShaderType.VertexShader, pgmID, out int vsID);
            LoadShader("fs", ShaderType.FragmentShader, pgmID, out int fsID);
            GL.LinkProgram(pgmID);

            pgmColorID = GL.CreateProgram();
            LoadShader("vs", ShaderType.VertexShader, pgmColorID, out vsID);
            LoadShader("fsColor", ShaderType.FragmentShader, pgmColorID, out fsID);
            GL.LinkProgram(pgmColorID);

            pgmBlackID = GL.CreateProgram();
            LoadShader("vs", ShaderType.VertexShader, pgmBlackID, out vsID);
            LoadShader("fsBlack", ShaderType.FragmentShader, pgmBlackID, out fsID);
            GL.LinkProgram(pgmBlackID);

            attributeVertexPosition = GL.GetAttribLocation(pgmID, "vertexPosition");
            attributeNormalDirection = GL.GetAttribLocation(pgmID, "normalDirection");
            attributeVertexColor = GL.GetAttribLocation(pgmColorID, "vertexColor");
            uniformModelMatrix = GL.GetUniformLocation(pgmID, "modelMatrix");
            uniformViewMatrix = GL.GetUniformLocation(pgmID, "viewMatrix");
            uniformProjMatrix = GL.GetUniformLocation(pgmID, "projMatrix");
        }

        private static void LoadShader(string filename, ShaderType type, int program, out int address)
        {
            address = GL.CreateShader(type);
            var str = (string)Properties.Resources.ResourceManager.GetObject(filename);
            GL.ShaderSource(address, str);
            GL.CompileShader(address);
            GL.AttachShader(program, address);
            GL.DeleteShader(address);
        }

        private static void CreateVBO(out int vboAddress, Vector3[] data, int address)
        {
            GL.GenBuffers(1, out vboAddress);
            GL.BindBuffer(BufferTarget.ArrayBuffer, vboAddress);
            GL.BufferData(BufferTarget.ArrayBuffer,
                                    (IntPtr)(data.Length * Vector3.SizeInBytes),
                                    data,
                                    BufferUsageHint.StaticDraw);
            GL.VertexAttribPointer(address, 3, VertexAttribPointerType.Float, false, 0, 0);
            GL.EnableVertexAttribArray(address);
        }

        private static void CreateVBO(out int vboAddress, Vector4[] data, int address)
        {
            GL.GenBuffers(1, out vboAddress);
            GL.BindBuffer(BufferTarget.ArrayBuffer, vboAddress);
            GL.BufferData(BufferTarget.ArrayBuffer,
                                    (IntPtr)(data.Length * Vector4.SizeInBytes),
                                    data,
                                    BufferUsageHint.StaticDraw);
            GL.VertexAttribPointer(address, 4, VertexAttribPointerType.Float, false, 0, 0);
            GL.EnableVertexAttribArray(address);
        }

        private static void CreateVBO(out int vboAddress, Matrix4 data, int address)
        {
            GL.GenBuffers(1, out vboAddress);
            GL.UniformMatrix4(address, false, ref data);
        }

        private static void CreateEBO(out int address, int[] data)
        {
            GL.GenBuffers(1, out address);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, address);
            GL.BufferData(BufferTarget.ElementArrayBuffer,
                            (IntPtr)(data.Length * sizeof(int)),
                            data,
                            BufferUsageHint.StaticDraw);
        }

        private void CreateVAO()
        {
            GL.DeleteVertexArray(vao);
            GL.GenVertexArrays(1, out vao);
            GL.BindVertexArray(vao);
            CreateVBO(out var vboPositions, vertexData, attributeVertexPosition);
            if (normalMode == 1)
            {
                CreateVBO(out var vboNormals, normal2Data, attributeNormalDirection);
            }
            else
            {
                if (normalData != null)
                    CreateVBO(out var vboNormals, normalData, attributeNormalDirection);
            }
            CreateVBO(out var vboColors, colorData, attributeVertexColor);
            CreateVBO(out var vboModelMatrix, modelMatrixData, uniformModelMatrix);
            CreateVBO(out var vboViewMatrix, viewMatrixData, uniformViewMatrix);
            CreateVBO(out var vboProjMatrix, projMatrixData, uniformProjMatrix);
            CreateEBO(out var eboElements, indiceData);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindVertexArray(0);
        }

        private void ChangeGLSize(Size size)
        {
            GL.Viewport(0, 0, size.Width, size.Height);

            if (size.Width <= size.Height)
            {
                float k = 1.0f * size.Width / size.Height;
                projMatrixData = Matrix4.CreateScale(1, k, 1);
            }
            else
            {
                float k = 1.0f * size.Height / size.Width;
                projMatrixData = Matrix4.CreateScale(k, 1, 1);
            }
        }

        private void glControl1_Load(object sender, EventArgs e)
        {
            InitOpenTK();
            glControlLoaded = true;
        }

        private void glControl1_Paint(object sender, PaintEventArgs e)
        {
            glControl1.MakeCurrent();
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Lequal);
            GL.BindVertexArray(vao);
            if (wireFrameMode == 0 || wireFrameMode == 2)
            {
                GL.UseProgram(shadeMode == 0 ? pgmID : pgmColorID);
                GL.UniformMatrix4(uniformModelMatrix, false, ref modelMatrixData);
                GL.UniformMatrix4(uniformViewMatrix, false, ref viewMatrixData);
                GL.UniformMatrix4(uniformProjMatrix, false, ref projMatrixData);
                GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
                GL.DrawElements(BeginMode.Triangles, indiceData.Length, DrawElementsType.UnsignedInt, 0);
            }
            //Wireframe
            if (wireFrameMode == 1 || wireFrameMode == 2)
            {
                GL.Enable(EnableCap.PolygonOffsetLine);
                GL.PolygonOffset(-1, -1);
                GL.UseProgram(pgmBlackID);
                GL.UniformMatrix4(uniformModelMatrix, false, ref modelMatrixData);
                GL.UniformMatrix4(uniformViewMatrix, false, ref viewMatrixData);
                GL.UniformMatrix4(uniformProjMatrix, false, ref projMatrixData);
                GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
                GL.DrawElements(BeginMode.Triangles, indiceData.Length, DrawElementsType.UnsignedInt, 0);
                GL.Disable(EnableCap.PolygonOffsetLine);
            }
            GL.BindVertexArray(0);
            GL.Flush();
            glControl1.SwapBuffers();
        }

        private void glControl1_MouseWheel(object sender, MouseEventArgs e)
        {
            if (glControl1.Visible)
            {
                viewMatrixData *= Matrix4.CreateScale(1 + e.Delta / 1000f);
                glControl1.Invalidate();
            }
        }

        private void glControl1_MouseDown(object sender, MouseEventArgs e)
        {
            mdx = e.X;
            mdy = e.Y;
            if (e.Button == MouseButtons.Left)
            {
                lmdown = true;
            }
            if (e.Button == MouseButtons.Right)
            {
                rmdown = true;
            }
        }

        private void glControl1_MouseMove(object sender, MouseEventArgs e)
        {
            if (lmdown || rmdown)
            {
                float dx = mdx - e.X;
                float dy = mdy - e.Y;
                mdx = e.X;
                mdy = e.Y;
                if (lmdown)
                {
                    dx *= 0.01f;
                    dy *= 0.01f;
                    viewMatrixData *= Matrix4.CreateRotationX(dy);
                    viewMatrixData *= Matrix4.CreateRotationY(dx);
                }
                if (rmdown)
                {
                    dx *= 0.003f;
                    dy *= 0.003f;
                    viewMatrixData *= Matrix4.CreateTranslation(-dx, dy, 0);
                }
                glControl1.Invalidate();
            }
        }

        private void glControl1_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                lmdown = false;
            }
            if (e.Button == MouseButtons.Right)
            {
                rmdown = false;
            }
        }
        #endregion
    }
}
