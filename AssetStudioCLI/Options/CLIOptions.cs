using AssetStudio;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace AssetStudioCLI.Options
{
    internal enum HelpGroups
    {
        General,
        Convert,
        Logger,
        Advanced,
    }

    internal enum WorkMode
    {
        Export,
        ExportRaw,
        Dump,
        Info,
    }

    internal enum AssetGroupOption
    {
        None,
        TypeName,
        ContainerPath,
        ContainerPathFull,
        SourceFileName,
    }

    internal enum ExportListType
    {
        None,
        XML,
    }

    internal enum AudioFormat
    {
        None,
        Wav,
    }

    internal enum FilterBy
    {
        None,
        Name,
        Container,
        PathID,
        NameOrContainer,
        NameAndContainer,
    }

    internal class GroupedOption<T> : Option<T>
    {
        public GroupedOption(T optionDefaultValue, string optionName, string optionDescription, HelpGroups optionHelpGroup, bool isFlag = false) : base(optionDefaultValue, optionName, optionDescription, optionHelpGroup, isFlag)
        {
            CLIOptions.OptionGrouping(optionName, optionDescription, optionHelpGroup, isFlag);
        }
    }

    internal class CLIOptions
    {
        public bool isParsed;
        public bool showHelp;
        public string[] cliArgs;
        public string inputPath;
        public FilterBy filterBy;
        private static Dictionary<string, string> optionsDict;
        private static Dictionary<string, string> flagsDict;
        private static Dictionary<HelpGroups, Dictionary<string, string>> optionGroups;
        private List<ClassIDType> supportedAssetTypes;
        //general
        public Option<WorkMode> o_workMode;
        public Option<List<ClassIDType>> o_exportAssetTypes;
        public Option<AssetGroupOption> o_groupAssetsBy;
        public Option<string> o_outputFolder;
        public Option<bool> o_displayHelp;
        //logger
        public Option<LoggerEvent> o_logLevel;
        public Option<LogOutputMode> o_logOutput;
        //convert
        public bool convertTexture;
        public Option<ImageFormat> o_imageFormat;
        public Option<AudioFormat> o_audioFormat;
        //advanced
        public Option<ExportListType> o_exportAssetList;
        public Option<List<string>> o_filterByName;
        public Option<List<string>> o_filterByContainer;
        public Option<List<string>> o_filterByPathID;
        public Option<List<string>> o_filterByText;
        public Option<string> o_assemblyPath;
        public Option<string> o_unityVersion;
        public Option<bool> f_notRestoreExtensionName;

        public CLIOptions(string[] args)
        {
            cliArgs = args;
            InitOptions();
            ParseArgs(args);
        }

        private void InitOptions()
        {
            isParsed = false;
            showHelp = false;
            inputPath = "";
            filterBy = FilterBy.None;
            optionsDict = new Dictionary<string, string>();
            flagsDict = new Dictionary<string, string>();
            optionGroups = new Dictionary<HelpGroups, Dictionary<string, string>>();
            supportedAssetTypes = new List<ClassIDType>
            {
                ClassIDType.Texture2D,
                ClassIDType.Sprite,
                ClassIDType.TextAsset,
                ClassIDType.MonoBehaviour,
                ClassIDType.Font,
                ClassIDType.Shader,
                ClassIDType.AudioClip,
                ClassIDType.VideoClip,
                ClassIDType.MovieTexture,
                ClassIDType.Mesh,
            };

            #region Init General Options
            o_workMode = new GroupedOption<WorkMode>
            (
                optionDefaultValue: WorkMode.Export,
                optionName: "-m, --mode <value>",
                optionDescription: "Specify working mode\n" +
                    "<Value: export(default) | exportRaw | dump | info>\n" +
                    "Export - Exports converted assets\n" +
                    "ExportRaw - Exports raw data\n" +
                    "Dump - Makes asset dumps\n" +
                    "Info - Loads file(s), shows the number of supported for export assets and exits\n" +
                    "Example: \"-m info\"\n",
                optionHelpGroup: HelpGroups.General
            );
            o_exportAssetTypes = new GroupedOption<List<ClassIDType>>
            (
                optionDefaultValue: supportedAssetTypes,
                optionName: "-t, --asset-type <value(s)>",
                optionDescription: "Specify asset type(s) to export\n" +
                    "<Value(s): tex2d, sprite, textAsset, monoBehaviour, font, shader, movieTexture,\n" +
                    "audio, video, mesh | all(default)>\n" +
                    "All - export all asset types, which are listed in the values\n" +
                    "*To specify multiple asset types, write them separated by ',' or ';' without spaces\n" +
                    "Examples: \"-t sprite\" or \"-t all\" or \"-t tex2d,sprite,audio\" or \"-t tex2d;sprite;font\"\n",
                optionHelpGroup: HelpGroups.General
            );
            o_groupAssetsBy = new GroupedOption<AssetGroupOption>
            (
                optionDefaultValue: AssetGroupOption.ContainerPath,
                optionName: "-g, --group-option <value>",
                optionDescription: "Specify the way in which exported assets should be grouped\n" +
                    "<Value: none | type | container(default) | containerFull | filename>\n" +
                    "None - Do not group exported assets\n" +
                    "Type - Group exported assets by type name\n" +
                    "Container - Group exported assets by container path\n" +
                    "ContainerFull - Group exported assets by full container path (e.g. with prefab name)\n" +
                    "Filename - Group exported assets by source file name\n" +
                    "Example: \"-g container\"\n",
                optionHelpGroup: HelpGroups.General
            );
            o_outputFolder = new GroupedOption<string>
            (
                optionDefaultValue: "",
                optionName: "-o, --output <path>",
                optionDescription: "Specify path to the output folder\n" +
                    "If path isn't specifyed, 'ASExport' folder will be created in the program's work folder\n",
                optionHelpGroup: HelpGroups.General
            );
            o_displayHelp = new GroupedOption<bool>
            (
                optionDefaultValue: false,
                optionName: "-h, --help",
                optionDescription: "Display help and exit",
                optionHelpGroup: HelpGroups.General
            );
            #endregion

            #region Init Logger Options
            o_logLevel = new GroupedOption<LoggerEvent>
            (
                optionDefaultValue: LoggerEvent.Info,
                optionName: "--log-level <value>",
                optionDescription: "Specify the log level\n" +
                    "<Value: verbose | debug | info(default) | warning | error>\n" +
                    "Example: \"--log-level warning\"\n",
                optionHelpGroup: HelpGroups.Logger
            );
            o_logOutput = new GroupedOption<LogOutputMode> 
            (
                optionDefaultValue: LogOutputMode.Console,
                optionName: "--log-output <value>",
                optionDescription: "Specify the log output\n" +
                    "<Value: console(default) | file | both>\n" +
                    "Example: \"--log-output both\"",
                optionHelpGroup: HelpGroups.Logger
            );
            #endregion

            #region Init Convert Options
            convertTexture = true;
            o_imageFormat = new GroupedOption<ImageFormat>
            (
                optionDefaultValue: ImageFormat.Png,
                optionName: "--image-format <value>",
                optionDescription: "Specify the format for converting image assets\n" +
                    "<Value: none | jpg | png(default) | bmp | tga | webp>\n" +
                    "None - Do not convert images and export them as texture data (.tex)\n" +
                    "Example: \"--image-format jpg\"\n",
                optionHelpGroup: HelpGroups.Convert
            );
            o_audioFormat = new GroupedOption<AudioFormat>
            (
                optionDefaultValue: AudioFormat.Wav,
                optionName: "--audio-format <value>",
                optionDescription: "Specify the format for converting audio assets\n" +
                    "<Value: none | wav(default)>\n" +
                    "None - Do not convert audios and export them in their own format\n" +
                    "Example: \"--audio-format wav\"",
                optionHelpGroup: HelpGroups.Convert
            );
            #endregion

            #region Init Advanced Options
            o_exportAssetList = new GroupedOption<ExportListType>
            (
                optionDefaultValue: ExportListType.None,
                optionName: "--export-asset-list <value>",
                optionDescription: "Specify the format in which you want to export asset list\n" +
                    "<Value: none(default) | xml>\n" +
                    "None - Do not export asset list\n" +
                    "Example: \"--export-asset-list xml\"\n",
                optionHelpGroup: HelpGroups.Advanced
            );
            o_filterByName = new GroupedOption<List<string>>
            (
                optionDefaultValue: new List<string>(),
                optionName: "--filter-by-name <text>",
                optionDescription: "Specify the name by which assets should be filtered\n" +
                    "*To specify multiple names write them separated by ',' or ';' without spaces\n" +
                    "Example: \"--filter-by-name char\" or \"--filter-by-name char,bg\"\n",
                optionHelpGroup: HelpGroups.Advanced
            );
            o_filterByContainer = new GroupedOption<List<string>>
            (
                optionDefaultValue: new List<string>(),
                optionName: "--filter-by-container <text>",
                optionDescription: "Specify the container by which assets should be filtered\n" +
                    "*To specify multiple containers write them separated by ',' or ';' without spaces\n" +
                    "Example: \"--filter-by-container arts\" or \"--filter-by-container arts,icons\"\n",
                optionHelpGroup: HelpGroups.Advanced
            );
            o_filterByPathID = new GroupedOption<List<string>>
            (
                optionDefaultValue: new List<string>(),
                optionName: "--filter-by-pathid <text>",
                optionDescription: "Specify the PathID by which assets should be filtered\n" +
                    "*To specify multiple PathIDs write them separated by ',' or ';' without spaces\n" +
                    "Example: \"--filter-by-pathid 7238605633795851352,-2430306240205277265\"\n",
                optionHelpGroup: HelpGroups.Advanced
            );
            o_filterByText = new GroupedOption<List<string>>
            (
                optionDefaultValue: new List<string>(),
                optionName: "--filter-by-text <text>",
                optionDescription: "Specify the text by which assets should be filtered\n" +
                    "Looks for assets that contain the specified text in their names or containers\n" +
                    "*To specify multiple values write them separated by ',' or ';' without spaces\n" +
                    "Example: \"--filter-by-text portrait\" or \"--filter-by-text portrait,art\"\n",
                optionHelpGroup: HelpGroups.Advanced
            );
            o_assemblyPath = new GroupedOption<string>
            (
                optionDefaultValue: "",
                optionName: "--assembly-folder <path>",
                optionDescription: "Specify the path to the assembly folder",
                optionHelpGroup: HelpGroups.Advanced
            );
            o_unityVersion = new GroupedOption<string>
            (
                optionDefaultValue: "",
                optionName: "--unity-version <text>",
                optionDescription: "Specify Unity version. Example: \"--unity-version 2017.4.39f1\"",
                optionHelpGroup: HelpGroups.Advanced
            );
            f_notRestoreExtensionName = new GroupedOption<bool>
            (
                optionDefaultValue: false,
                optionName: "--not-restore-extension",
                optionDescription: "(Flag) If specified, AssetStudio will not try to use/restore original TextAsset\nextension name, and will just export all TextAssets with the \".txt\" extension",
                optionHelpGroup: HelpGroups.Advanced,
                isFlag: true
            );
            #endregion
        }

        internal static void OptionGrouping(string name, string desc, HelpGroups group, bool isFlag)
        {
            if (string.IsNullOrEmpty(name))
            {
                return;
            }

            var optionDict = new Dictionary<string, string>() { { name, desc } };
            if (!optionGroups.ContainsKey(group))
            {
                optionGroups.Add(group, optionDict);
            }
            else
            {
                optionGroups[group].Add(name, desc);
            }

            if (isFlag)
            {
                flagsDict.Add(name, desc);
            }
            else
            {
                optionsDict.Add(name, desc);
            }
        }

        private void ParseArgs(string[] args)
        {
            var brightYellow = CLIAnsiColors.BrightYellow;
            var brightRed = CLIAnsiColors.BrightRed;

            if (args.Length == 0 || args.Any(x => x == "-h" || x == "--help"))
            {
                showHelp = true;
                return;
            }

            if (!args[0].StartsWith("-"))
            {
                inputPath = Path.GetFullPath(args[0]).Replace("\"", "");
                if (!Directory.Exists(inputPath) && !File.Exists(inputPath))
                {
                    Console.WriteLine($"{"Error:".Color(brightRed)} Invalid input path \"{args[0].Color(brightRed)}\".\n" +
                        $"Specified file or folder was not found. The input path must be specified as the first argument.");
                    return;
                }
                o_outputFolder.Value = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ASExport");
            }
            else
            {
                Console.WriteLine($"{"Error:".Color(brightRed)} Input path was empty. Specify the input path as the first argument.");
                return;
            }

            var resplittedArgs = new List<string>();
            for (int i = 1; i < args.Length; i++)
            {
                string arg = args[i];

                if (arg.Contains('='))
                {
                    var splittedArgs = arg.Split('=');
                    resplittedArgs.Add(splittedArgs[0]);
                    resplittedArgs.Add(splittedArgs[1]);
                }
                else
                {
                    resplittedArgs.Add(arg);
                }    
            };

            #region Parse Flags
            for (int i = 0; i < resplittedArgs.Count; i++) 
            {
                string flag = resplittedArgs[i].ToLower();

                switch(flag)
                {
                    case "--not-restore-extension":
                        f_notRestoreExtensionName.Value = true;
                        resplittedArgs.RemoveAt(i);
                        break;
                }
            }            
            #endregion

            #region Parse Options
            for (int i = 0; i < resplittedArgs.Count; i++)
            {
                var option = resplittedArgs[i].ToLower();
                try
                {
                    var value = resplittedArgs[i + 1].Replace("\"", "");
                    switch (option)
                    {
                        case "-m":
                        case "--mode":
                            switch (value.ToLower())
                            {
                                case "export":
                                    o_workMode.Value = WorkMode.Export;
                                    break;
                                case "raw":
                                case "exportraw":
                                    o_workMode.Value = WorkMode.ExportRaw;
                                    break;
                                case "dump":
                                    o_workMode.Value = WorkMode.Dump;
                                    break;
                                case "info":
                                    o_workMode.Value = WorkMode.Info;
                                    break;
                                default:
                                    Console.WriteLine($"{"Error".Color(brightRed)} during parsing [{option}] option. Unsupported working mode: [{value.Color(brightRed)}].\n");
                                    Console.WriteLine(o_workMode.Description);
                                    return;
                            }
                            break;
                        case "-t":
                        case "--asset-type":
                            var splittedTypes = ValueSplitter(value);
                            o_exportAssetTypes.Value = new List<ClassIDType>();
                            foreach (var type in splittedTypes)
                            {
                                switch (type.ToLower())
                                {
                                    case "tex2d":
                                    case "texture2d":
                                        o_exportAssetTypes.Value.Add(ClassIDType.Texture2D);
                                        break;
                                    case "sprite":
                                        o_exportAssetTypes.Value.Add(ClassIDType.Sprite);
                                        break;
                                    case "textasset":
                                        o_exportAssetTypes.Value.Add(ClassIDType.TextAsset);
                                        break;
                                    case "monobehaviour":
                                        o_exportAssetTypes.Value.Add(ClassIDType.MonoBehaviour);
                                        break;
                                    case "font":
                                        o_exportAssetTypes.Value.Add(ClassIDType.Font);
                                        break;
                                    case "shader":
                                        o_exportAssetTypes.Value.Add(ClassIDType.Shader);
                                        break;
                                    case "audio":
                                    case "audioclip":
                                        o_exportAssetTypes.Value.Add(ClassIDType.AudioClip);
                                        break;
                                    case "video":
                                    case "videoclip":
                                        o_exportAssetTypes.Value.Add(ClassIDType.VideoClip);
                                        break;
                                    case "movietexture":
                                        o_exportAssetTypes.Value.Add(ClassIDType.MovieTexture);
                                        break;
                                    case "mesh":
                                        o_exportAssetTypes.Value.Add(ClassIDType.Mesh);
                                        break;
                                    case "all":
                                        o_exportAssetTypes.Value = supportedAssetTypes;
                                        break;
                                    default:
                                        Console.WriteLine($"{"Error".Color(brightRed)} during parsing [{option}] option. Unsupported asset type: [{value.Color(brightRed)}].\n");
                                        Console.WriteLine(o_exportAssetTypes.Description);
                                        return;
                                }
                            }
                            break;
                        case "-g":
                        case "--group-option":
                            switch (value.ToLower())
                            {
                                case "type":
                                    o_groupAssetsBy.Value = AssetGroupOption.TypeName;
                                    break;
                                case "container":
                                    o_groupAssetsBy.Value = AssetGroupOption.ContainerPath;
                                    break;
                                case "containerfull":
                                    o_groupAssetsBy.Value = AssetGroupOption.ContainerPathFull;
                                    break;
                                case "filename":
                                    o_groupAssetsBy.Value = AssetGroupOption.SourceFileName;
                                    break;
                                case "none":
                                    o_groupAssetsBy.Value = AssetGroupOption.None;
                                    break;
                                default:
                                    Console.WriteLine($"{"Error".Color(brightRed)} during parsing [{option}] option. Unsupported grouping option: [{value.Color(brightRed)}].\n");
                                    Console.WriteLine(o_groupAssetsBy.Description);
                                    return;
                            }
                            break;
                        case "-o":
                        case "--output":
                            try
                            {
                                value = Path.GetFullPath(value);
                                if (!Directory.Exists(value))
                                {
                                    Directory.CreateDirectory(value);
                                }
                                o_outputFolder.Value = value;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"{"Warning:".Color(brightYellow)} Invalid output folder \"{value.Color(brightYellow)}\".\n{ex.Message}");
                                Console.WriteLine($"Working folder \"{o_outputFolder.Value.Color(brightYellow)}\" will be used as the output folder.\n");
                                Console.WriteLine("Press ESC to exit or any other key to continue...\n");
                                switch (Console.ReadKey(intercept: true).Key)
                                {
                                    case ConsoleKey.Escape:
                                        return;
                                }
                            }
                            break;
                        case "--log-level":
                            switch (value.ToLower())
                            {
                                case "verbose":
                                    o_logLevel.Value = LoggerEvent.Verbose;
                                    break;
                                case "debug":
                                    o_logLevel.Value = LoggerEvent.Debug;
                                    break;
                                case "info":
                                    o_logLevel.Value = LoggerEvent.Info;
                                    break;
                                case "warning":
                                    o_logLevel.Value = LoggerEvent.Warning;
                                    break;
                                case "error":
                                    o_logLevel.Value = LoggerEvent.Error;
                                    break;
                                default:
                                    Console.WriteLine($"{"Error".Color(brightRed)} during parsing [{option}] option. Unsupported log level value: [{value.Color(brightRed)}].\n");
                                    Console.WriteLine(o_logLevel.Description);
                                    return;
                            }
                            break;
                        case "--log-output":
                            switch (value.ToLower())
                            {
                                case "console":
                                    o_logOutput.Value = LogOutputMode.Console;
                                    break;
                                case "file":
                                    o_logOutput.Value = LogOutputMode.File;
                                    break;
                                case "both":
                                    o_logOutput.Value = LogOutputMode.Both;
                                    break;
                                default:
                                    Console.WriteLine($"{"Error".Color(brightRed)} during parsing [{option}] option. Unsupported log output mode: [{value.Color(brightRed)}].\n");
                                    Console.WriteLine(o_logOutput.Description);
                                    return;
                            }
                            break;
                        case "--image-format":
                            switch (value.ToLower())
                            {
                                case "jpg":
                                case "jpeg":
                                    o_imageFormat.Value = ImageFormat.Jpeg;
                                    break;
                                case "png":
                                    o_imageFormat.Value = ImageFormat.Png;
                                    break;
                                case "bmp":
                                    o_imageFormat.Value = ImageFormat.Bmp;
                                    break;
                                case "tga":
                                    o_imageFormat.Value = ImageFormat.Tga;
                                    break;
                                case "webp":
                                    o_imageFormat.Value = ImageFormat.Webp;
                                    break;
                                case "none":
                                    convertTexture = false;
                                    break;
                                default:
                                    Console.WriteLine($"{"Error".Color(brightRed)} during parsing [{option}] option. Unsupported image format: [{value.Color(brightRed)}].\n");
                                    Console.WriteLine(o_imageFormat.Description);
                                    return;
                            }
                            break;
                        case "--audio-format":
                            switch (value.ToLower())
                            {
                                case "wav":
                                case "wave":
                                    o_audioFormat.Value = AudioFormat.Wav;
                                    break;
                                case "none":
                                    o_audioFormat.Value = AudioFormat.None;
                                    break;
                                default:
                                    Console.WriteLine($"{"Error".Color(brightRed)} during parsing [{option}] option. Unsupported audio format: [{value.Color(brightRed)}].\n");
                                    Console.WriteLine(o_audioFormat.Description);
                                    return;
                            }
                            break;
                        case "--export-asset-list":
                            switch (value.ToLower())
                            {
                                case "xml":
                                    o_exportAssetList.Value = ExportListType.XML;
                                    break;
                                case "none":
                                    o_exportAssetList.Value = ExportListType.None;
                                    break;
                                default:
                                    Console.WriteLine($"{"Error".Color(brightRed)} during parsing [{option}] option. Unsupported asset list export option: [{value.Color(brightRed)}].\n");
                                    Console.WriteLine(o_exportAssetList.Description);
                                    return;
                            }
                            break;
                        case "--filter-by-name":
                            o_filterByName.Value.AddRange(ValueSplitter(value));
                            filterBy = filterBy == FilterBy.None ? FilterBy.Name : filterBy == FilterBy.Container ? FilterBy.NameAndContainer : filterBy;
                            break;
                        case "--filter-by-container":
                            o_filterByContainer.Value.AddRange(ValueSplitter(value));
                            filterBy = filterBy == FilterBy.None ? FilterBy.Container : filterBy == FilterBy.Name ? FilterBy.NameAndContainer : filterBy;
                            break;
                        case "--filter-by-pathid":
                            o_filterByPathID.Value.AddRange(ValueSplitter(value));
                            filterBy = FilterBy.PathID;
                            break;
                        case "--filter-by-text":
                            o_filterByText.Value.AddRange(ValueSplitter(value));
                            filterBy = FilterBy.NameOrContainer;
                            break;
                        case "--assembly-folder":
                            if (Directory.Exists(value))
                            {
                                o_assemblyPath.Value = value;
                            }
                            else
                            {
                                Console.WriteLine($"{"Error".Color(brightRed)} during parsing [{option}] option. Assembly folder [{value.Color(brightRed)}] was not found.");
                                return;
                            }
                            break;
                        case "--unity-version":
                            o_unityVersion.Value = value;
                            break;
                        default:
                            Console.WriteLine($"{"Error:".Color(brightRed)} Unknown option [{option.Color(brightRed)}].\n");
                            if (!TryShowOptionDescription(option, optionsDict))
                            {
                                TryShowOptionDescription(option, flagsDict);
                            }
                            return;
                    }
                    i++;
                }
                catch (IndexOutOfRangeException)
                {
                    if (optionsDict.Any(x => x.Key.Contains(option)))
                    {
                        Console.WriteLine($"{"Error during parsing options:".Color(brightRed)} Value for [{option.Color(brightRed)}] option was not found.\n");
                        TryShowOptionDescription(option, optionsDict);
                    }
                    else if (flagsDict.Any(x => x.Key.Contains(option)))
                    {
                        Console.WriteLine($"{"Error:".Color(brightRed)} Unknown flag [{option.Color(brightRed)}].\n");
                        TryShowOptionDescription(option, flagsDict);
                    }
                    else
                    {
                        Console.WriteLine($"{"Error:".Color(brightRed)} Unknown option [{option.Color(brightRed)}].");
                    }
                    return;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Unknown Error.".Color(CLIAnsiColors.Red));
                    Console.WriteLine(ex);
                    return;
                }
            }
            isParsed = true;
            #endregion
        }

        private static string[] ValueSplitter(string value)
        {
            var separator = value.Contains(';') ? ';' : ',';
            return value.Split(separator);
        }

        private bool TryShowOptionDescription(string option, Dictionary<string, string> descDict)
        {
            var optionDesc = descDict.Where(x => x.Key.Contains(option));
            if (optionDesc.Any())
            {
                var rand = new Random();
                var rndOption = optionDesc.ElementAt(rand.Next(0, optionDesc.Count()));
                Console.WriteLine($"Did you mean [{ $"{rndOption.Key}".Color(CLIAnsiColors.BrightYellow) }] option?");
                Console.WriteLine($"Here's a description of it: \n\n{rndOption.Value}");

                return true;
            }
            return false;
        }

        public void ShowHelp(bool showUsageOnly = false)
        {
            const int indent = 22;
            var helpMessage = new StringBuilder();
            var usage = new StringBuilder();
            var appAssembly = typeof(Program).Assembly.GetName();
            usage.Append($"Usage: {appAssembly.Name} <input path to asset file/folder> ");

            var i = 0;
            foreach (var optionsGroup in optionGroups.Keys)
            {
                helpMessage.AppendLine($"{optionsGroup} Options:");
                foreach (var optionDict in optionGroups[optionsGroup])
                {
                    var optionName = $"{optionDict.Key,-indent - 8}";
                    var optionDesc = optionDict.Value.Replace("\n", $"{"\n",-indent - 11}");
                    helpMessage.AppendLine($"  {optionName}{optionDesc}");

                    usage.Append($"[{optionDict.Key}] ");
                    if (i++ % 2 == 0)
                    {
                        usage.Append($"\n{"",indent}");
                    }
                }
                helpMessage.AppendLine();
            }

            if (showUsageOnly)
            {
                Console.WriteLine(usage);
            }
            else
            {
                Console.WriteLine($"# {appAssembly.Name}\n# Based on AssetStudioMod v{appAssembly.Version}\n");
                Console.WriteLine($"{usage}\n\n{helpMessage}");
            }
        }

        private string ShowCurrentFilter()
        {
            switch (filterBy)
            {
                case FilterBy.Name:
                    return $"# Filter by {filterBy}(s): \"{string.Join("\", \"", o_filterByName.Value)}\"";
                case FilterBy.Container:
                    return $"# Filter by {filterBy}(s): \"{string.Join("\", \"", o_filterByContainer.Value)}\"";
                case FilterBy.PathID:
                    return $"# Filter by {filterBy}(s): \"{string.Join("\", \"", o_filterByPathID.Value)}\"";
                case FilterBy.NameOrContainer:
                    return $"# Filter by Text: \"{string.Join("\", \"", o_filterByText.Value)}\"";
                case FilterBy.NameAndContainer:
                    return $"# Filter by Name(s): \"{string.Join("\", \"", o_filterByName.Value)}\"\n# Filter by Container(s): \"{string.Join("\", \"", o_filterByContainer.Value)}\"";
                default:
                    return $"# Filter by: {filterBy}";
            }
        }

        public void ShowCurrentOptions()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[Current Options]");
            sb.AppendLine($"# Working Mode: {o_workMode}");
            sb.AppendLine($"# Input Path: \"{inputPath}\"");
            if (o_workMode.Value != WorkMode.Info)
            {
                sb.AppendLine($"# Output Path: \"{o_outputFolder}\"");
                sb.AppendLine($"# Export Asset Type(s): {string.Join(", ", o_exportAssetTypes.Value)}");
                sb.AppendLine($"# Asset Group Option: {o_groupAssetsBy}");
                sb.AppendLine($"# Export Image Format: {o_imageFormat}");
                sb.AppendLine($"# Export Audio Format: {o_audioFormat}");
                sb.AppendLine($"# Log Level: {o_logLevel}");
                sb.AppendLine($"# Log Output: {o_logOutput}");
                sb.AppendLine($"# Export Asset List: {o_exportAssetList}");
                sb.AppendLine(ShowCurrentFilter());
                sb.AppendLine($"# Assebmly Path: \"{o_assemblyPath}\"");
                sb.AppendLine($"# Unity Version: \"{o_unityVersion}\"");
                sb.AppendLine($"# Restore TextAsset extension: {!f_notRestoreExtensionName.Value}");
            }
            else
            {
                sb.AppendLine($"# Export Asset Type(s): {string.Join(", ", o_exportAssetTypes.Value)}");
                sb.AppendLine($"# Log Level: {o_logLevel}");
                sb.AppendLine($"# Log Output: {o_logOutput}");
                sb.AppendLine($"# Export Asset List: {o_exportAssetList}");
                sb.AppendLine(ShowCurrentFilter());
                sb.AppendLine($"# Unity Version: \"{o_unityVersion}\"");
            }
            sb.AppendLine("======");
            Logger.Info(sb.ToString());
        }
    }
}
