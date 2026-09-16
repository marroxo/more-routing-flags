# More Routing Flags
![Code size](https://img.shields.io/github/languages/code-size/marroxo/more-routing-flags?color=5c85d6)
![Open issues](https://img.shields.io/github/issues/marroxo/more-routing-flags?color=d65c5c)
![License](https://img.shields.io/github/license/marroxo/more-routing-flags?color=a35cd6)

A
[Peaks of Yore](https://store.steampowered.com/app/2236070/)
mod which lets you place several routing flags per peak and switch
between them.

# Overview
- [Features](#features)
- [Installing](#installing)
    - [BepInEx](#bepinex)
- [Keybinds](#keybinds)
- [Building from source](#building-from-source)
    - [Dotnet](#dotnet-build)
    - [Visual Studio](#visual-studio-build)
    - [Custom game locations](#custom-game-locations)
- [Credits](#credits)

# Features
- Place up to a configurable number of routing flags per peak
- Cycle between flags, or open a full-screen picker
- Previous flags stay visible as the same routing flag as the main one

# Installing
## BepInEx
If you haven't installed BepInEx yet, follow the install instructions here:
- [Windows](https://github.com/Kaden5480/modloader-instructions#bepinex-windows)
- [Linux](https://github.com/Kaden5480/modloader-instructions#bepinex-linux)

### More Routing Flags
- Download the latest release
[here](https://github.com/marroxo/more-routing-flags/releases).
- The compressed zip will contain a `plugins` directory.
- Copy the files in `plugins` to `BepInEx/plugins` in your game directory.
- Requires [UILib](https://github.com/Kaden5480/poy-ui-lib), [Mod Menu](https://github.com/Kaden5480/poy-mod-menu)  is optional.

# Keybinds
Default keybinds, all rebindable via Mod Menu if you have it installed.
Otherwise edit `BepInEx/config/com.github.marroxo.more-routing-flags.cfg`
after running the game once.

| Action | Key |
| --- | --- |
| Next flag | `]` |
| Previous flag | `[` |
| Delete flag | `Delete` |
| Open picker | `Tab` |
| Replace active flag (hold while placing) | `Left Ctrl` |

# Building from source
The resulting plugin can be found in `bin/`.

## Dotnet build
```sh
dotnet build -c <Debug|Release>
```

## Visual Studio build
Open `MoreRoutingFlags.sln` and build with ctrl + shift + b, or
Build -> Build Solution.

## Custom game locations
If Peaks of Yore is installed somewhere other than the default Steam
location, add a `Config.props` file to the root of this repository:

```xml
<Project>
  <PropertyGroup>
    <GamePath>E:\SteamLibrary\steamapps\common\Peaks of Yore</GamePath>
    <InstallAfterBuild>true</InstallAfterBuild>
  </PropertyGroup>
</Project>
```

`InstallAfterBuild` copies the built dll straight into
`BepInEx/plugins` after every build.

# Credits
Forked/referenced from a few of [Kaden's](github.com/Kaden548) mods, credit to him:

- [poy-template-dotnet](https://github.com/Kaden5480/poy-template-dotnet)
- Requires [UILib](https://github.com/Kaden5480/poy-ui-lib), optionally
  [Mod Menu](https://github.com/Kaden5480/poy-mod-menu)
- GameObject/AudioListener/PostProcessLayer, the post processing reflection copy
  [poy-freecam](https://github.com/Kaden5480/poy-freecam)
- The teleport guard list in `FlagTeleport.cs` is copied directly from his
  [poy-better-routing-flag](https://github.com/Kaden5480/poy-better-routing-flag),
