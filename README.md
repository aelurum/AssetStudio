# ArknightsStudio

![Latest release](https://img.shields.io/github/v/release/aelurum/AssetStudio?filter=ak*&label=Latest%20release) [![Download latest release](https://img.shields.io/github/v/release/aelurum/AssetStudio?filter=ak*&label=Download&labelColor=blue)](https://github.com/aelurum/AssetStudio/tags)

![Build status](https://ci.appveyor.com/api/projects/status/857ucvvp0cykv1ni?svg=true) [![Download latest build](https://img.shields.io/badge/Download_latest_build-brightgreen)](https://ci.appveyor.com/project/aelurum/arknightsstudio/build/artifacts)

**ArknightsStudio** is a modified version of AssetStudio designed for Arknights. Based on [AssetStudioMod](https://github.com/aelurum/AssetStudio).

**Neither the repository, nor the tool, nor the author of the tool, nor the author of the modification is affiliated with, sponsored, or authorized by Unity Technologies or its affiliates.**

## ArknightsStudio Features

- CLI version (for Windows, Linux, Mac)
   - `Animator` and `AnimationClip` assets are not supported in the CLI version
- Support of sprites with alpha texture
- Support of portrait sprites
- Correct support of avg character sprites
- Correct support of character art sprites

## Requirements

- ArknightsStudio-net472
   - GUI/CLI - [.NET Framework 4.7.2](https://dotnet.microsoft.com/download/dotnet-framework/net472)
- ArknightsStudio-net6
   - GUI/CLI (Windows) - [.NET Desktop Runtime 6.0](https://dotnet.microsoft.com/download/dotnet/6.0)
   - CLI (Linux/Mac) - [.NET Runtime 6.0](https://dotnet.microsoft.com/download/dotnet/6.0)
- ArknightsStudio-net7
   - GUI/CLI (Windows) - [.NET Desktop Runtime 7.0](https://dotnet.microsoft.com/download/dotnet/7.0)
   - CLI (Linux/Mac) - [.NET Runtime 7.0](https://dotnet.microsoft.com/download/dotnet/7.0)
- ArknightsStudio-net8
   - GUI/CLI (Windows) - [.NET Desktop Runtime 8.0](https://dotnet.microsoft.com/download/dotnet/8.0)
   - CLI (Linux/Mac) - [.NET Runtime 8.0](https://dotnet.microsoft.com/download/dotnet/8.0)

## CLI Usage

You can read CLI readme [here](https://github.com/aelurum/AssetStudio/blob/ArknightsStudio/AssetStudioCLI/ReadMe.md).

### Run

- Command-line: `ArknightsStudioCLI <asset folder path>`
- Command-line for Portable versions (.NET 6+): `dotnet ArknightsStudioCLI.dll <asset folder path>`

### Basic Samples

- Show a list with a number of assets of each type available for export
```
ArknightsStudioCLI <asset folder path> -m info
```
- Export assets of all supported for export types
```
ArknightsStudioCLI <asset folder path>
```
- Export assets of specific types
```
ArknightsStudioCLI <asset folder path> -t sprite
```
```
ArknightsStudioCLI <asset folder path> -t tex2d,sprite,audio
```
- Export portrait sprites
```
ArknightsStudioCLI <asset folder path> -t akPortrait
```
- Export assets grouped by type
```
ArknightsStudioCLI <asset folder path> -g type
```
- Export assets to a specified output folder
```
ArknightsStudioCLI <asset folder path> -o <output folder path>
```
- Dump assets to a specified output folder
```
ArknightsStudioCLI <asset folder path> -m dump -o <output folder path>
```
- Export assets and create a log file
```
ArknightsStudioCLI <asset folder path> --log-output both
```
- Export all FBX objects (similar to "Export all objects (split)" option in the GUI)
```
ArknightsStudioCLI <asset folder path> -m splitObjects
```
> When running in splitObjects mode you can only specify `-o`, `--log-level`, `--log-output`, `--export-asset-list`, `--image-format`, `--filter-by-name` and `--unity-version` options.
Any other options will be ignored.

### Advanced Samples
- Export image assets converted to webp format to a specified output folder
```
ArknightsStudioCLI <asset folder path> -o <output folder path> -t sprite,akPortrait,tex2d --image-format webp
```
- Export avg character sprites with aliases in their names
```
ArknightsStudioCLI <asset folder path> -t sprite --add-aliases
```
- Export character art sprites without brightness change of semi-transparent shadow for 2048x2048 images
```
ArknightsStudioCLI <asset folder path> -t sprite --shadow-gamma 0
```
- Show the number of audio assets that have "voice" in their containers
```
ArknightsStudioCLI <asset folder path> -m info -t audio --filter-by-container voice
```
- Export audio assets that have "voice" in their containers
```
ArknightsStudioCLI <asset folder path> -t audio --filter-by-container voice
```
- Export audio assets that have "music" or "voice" in their containers
```
ArknightsStudioCLI <asset folder path> -t audio --filter-by-container music,voice
```
```
ArknightsStudioCLI <asset folder path> -t audio --filter-by-container music --filter-by-container voice
```
- Export audio assets that have "char" in their names **or** containers
```
ArknightsStudioCLI <asset folder path> -t audio --filter-by-text char
```
- Export audio assets that have "loop" in their names **and** "music" in their containers
```
ArknightsStudioCLI <asset folder path> -t audio --filter-by-name loop --filter-by-container music
```
- Export FBX objects that have "model" or "scene" in their names and set the scale factor to 10
```
ArknightsStudioCLI <asset folder path> -m splitObjects --filter-by-name model,scene --fbx-scale-factor 10
```
- Load assets of all types and show them (similar to "Display all assets" option in the GUI)
```
ArknightsStudioCLI <asset folder path> -m info --load-all
```
- Load assets of all types and dump Material assets
```
ArknightsStudioCLI <asset folder path> -m dump -t material --load-all
```

## GUI Usage

### Load Assets/AssetBundles

Use **File->Load file** or **File->Load folder**.

When ArknightsStudio loads AssetBundles, it decompresses and reads it directly in memory, which may cause a large amount of memory to be used. You can use **File->Extract file** or **File->Extract folder** to extract AssetBundles to another folder, and then read.

### Extract/Decompress AssetBundles

Use **File->Extract file** or **File->Extract folder**.

### Export Assets

Use **Export** menu.

### Export Model

Export model from "Scene Hierarchy" using the **Model** menu.

Export Animator from "Asset List" using the **Export** menu.

#### With AnimationClip

Select model from "Scene Hierarchy" then select the AnimationClip from "Asset List", using **Model->Export selected objects with AnimationClip** to export.

Export Animator will export bound AnimationClip or use **Ctrl** to select Animator and AnimationClip from "Asset List", using **Export->Export Animator with selected AnimationClip** to export.

## Build

* Visual Studio 2022 or newer
* **AssetStudioFBXNative** uses [FBX SDK 2020.2.1](https://www.autodesk.com/developer-network/platform-technologies/fbx-sdk-2020-2-1), before building, you need to install the FBX SDK and modify the project file, change include directory and library directory to point to the FBX SDK directory

## Open source libraries used

### Texture2DDecoder
* [Ishotihadus/mikunyan](https://github.com/Ishotihadus/mikunyan)
* [BinomialLLC/crunch](https://github.com/BinomialLLC/crunch)
* [Unity-Technologies/crunch](https://github.com/Unity-Technologies/crunch/tree/unity)
