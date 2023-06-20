## AssetStudioCLI
CLI version of AssetStudio Mod.
- Supported asset types: `Texture2D`, `Sprite`, `TextAsset`, `MonoBehaviour`, `Font`, `Shader`, `MovieTexture`, `AudioClip`, `VideoClip`, `Mesh`
- *There are no plans to add support for `AnimationClip`/`Animator` for now*

### Usage
```
AssetStudioCLI <input path to asset file/folder> [-m, --mode <value>]
                      [-t, --asset-type <value(s)>] [-g, --group-option <value>]
                      [-o, --output <path>] [-h, --help]
                      [--log-level <value>] [--log-output <value>]
                      [--image-format <value>] [--audio-format <value>]
                      [--export-asset-list <value>] [--filter-by-name <text>]
                      [--filter-by-container <text>] [--filter-by-pathid <text>]
                      [--filter-by-text <text>] [--assembly-folder <path>]
                      [--unity-version <text>] [--not-restore-extension]



General Options:
  -m, --mode <value>            Specify working mode
                                <Value: export(default) | exportRaw | dump | info>
                                Export - Exports converted assets
                                ExportRaw - Exports raw data
                                Dump - Makes asset dumps
                                Info - Loads file(s), shows the number of supported for export assets and exits
                                Example: "-m info"

  -t, --asset-type <value(s)>   Specify asset type(s) to export
                                <Value(s): tex2d, sprite, textAsset, monoBehaviour, font, shader, movieTexture,
                                audio, video, mesh | all(default)>
                                All - export all asset types, which are listed in the values
                                *To specify multiple asset types, write them separated by ',' or ';' without spaces
                                Examples: "-t sprite" or "-t all" or "-t tex2d,sprite,audio" or "-t tex2d;sprite;font"

  -g, --group-option <value>    Specify the way in which exported assets should be grouped
                                <Value: none | type | container(default) | containerFull | filename>
                                None - Do not group exported assets
                                Type - Group exported assets by type name
                                Container - Group exported assets by container path
                                ContainerFull - Group exported assets by full container path (e.g. with prefab name)
                                Filename - Group exported assets by source file name
                                Example: "-g container"

  -o, --output <path>           Specify path to the output folder
                                If path isn't specifyed, 'ASExport' folder will be created in the program's work folder

  -h, --help                    Display help and exit

Logger Options:
  --log-level <value>           Specify the log level
                                <Value: verbose | debug | info(default) | warning | error>
                                Example: "--log-level warning"

  --log-output <value>          Specify the log output
                                <Value: console(default) | file | both>
                                Example: "--log-output both"

Convert Options:
  --image-format <value>        Specify the format for converting image assets
                                <Value: none | jpg | png(default) | bmp | tga | webp>
                                None - Do not convert images and export them as texture data (.tex)
                                Example: "--image-format jpg"

  --audio-format <value>        Specify the format for converting audio assets
                                <Value: none | wav(default)>
                                None - Do not convert audios and export them in their own format
                                Example: "--audio-format wav"

Advanced Options:
  --export-asset-list <value>   Specify the format in which you want to export asset list
                                <Value: none(default) | xml>
                                None - Do not export asset list
                                Example: "--export-asset-list xml"

  --filter-by-name <text>       Specify the name by which assets should be filtered
                                *To specify multiple names write them separated by ',' or ';' without spaces
                                Example: "--filter-by-name char" or "--filter-by-name char,bg"

  --filter-by-container <text>  Specify the container by which assets should be filtered
                                *To specify multiple containers write them separated by ',' or ';' without spaces
                                Example: "--filter-by-container arts" or "--filter-by-container arts,icons"

  --filter-by-pathid <text>     Specify the PathID by which assets should be filtered
                                *To specify multiple PathIDs write them separated by ',' or ';' without spaces
                                Example: "--filter-by-pathid 7238605633795851352,-2430306240205277265"

  --filter-by-text <text>       Specify the text by which assets should be filtered
                                Looks for assets that contain the specified text in their names or containers
                                *To specify multiple values write them separated by ',' or ';' without spaces
                                Example: "--filter-by-text portrait" or "--filter-by-text portrait,art"

  --assembly-folder <path>      Specify the path to the assembly folder
  --unity-version <text>        Specify Unity version. Example: "--unity-version 2017.4.39f1"
  --not-restore-extension       (Flag) If specified, AssetStudio will not try to use/restore original TextAsset
                                extension name, and will just export all TextAssets with the ".txt" extension
```
