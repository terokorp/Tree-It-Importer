# Tree It Importer for Unity

[![GitHub latest release](https://img.shields.io/github/v/release/terokorp/net.koodaa.treeit-importer?color=green)](https://github.com/terokorp/Tree-It-Importer/releases/latest)
[![GitHub license](https://img.shields.io/github/license/terokorp/net.koodaa.treeit-importer)](https://github.com/terokorp/Tree-It-Importer/blob/master/LICENSE.md)

[![GitHub package.json version](https://img.shields.io/github/package-json/v/terokorp/Tree-It-Importer)](https://github.com/terokorp/net.koodaa.treeit-importer/releases)

This tool creates Unity tree prefabs from Tree It .tre files easily.

This tool is made and tested with Unity 2022.3.35f1, but might work with older versions. Test and report how it goes.

*Warning:* Alpha version and poorly documented.

## Features


## Installation
Install using UPM Package Git URL:

- Go to the Package Manager.
- Click the plus icon in the top left corner.
- Select "Add package from git URL..."
- Add the following URL: https://github.com/terokorp/Tree-It-Importer.git#master

### Get Tree It
- Free version from the homepage: http://www.evolved-software.com/treeit/treeit
- Paid version on Steam: https://store.steampowered.com/app/2386460/Tree_It/

### Usage
- Create tree with Tree it
- Save .tre and export as .fbx to Unity project folder
- Now this tool creates tree asset, creates URP Lit materials and setup LODs

# Planned features
- Windzone is not supported yet.
- Conversion to prefab.
- Shaders.

# Known issues
- Billboard has issues when using the exported LOD5 mesh.
- Billboard with SpeedTree Billboard shader size doesn't match perfectly.
- Only URP support, but you can override with custom materials.

## Documentation

- https://github.com/terokorp/Tree-It-Importer/tree/master/Documentation~

## License

- [MIT License](./LICENSE.md)
