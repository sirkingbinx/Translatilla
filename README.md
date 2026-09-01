<div align="center">
    <img src=".github/icon.png" width=250>
    <h1>Translatilla</h1>
</div>

Translatilla is a (experimental) Gorilla Tag mod that heavily modifies [Utilla](https://github.com/sirkingbinx/Utilla) and [GorillaLibrary](https://github.com/GorillaTagModdingHub/GorillaLibrary) at runtime to get them to work together to implement modded lobby support.

Utilla or GorillaLibrary is declared as a "master library". The master library is the library that handles modded lobby functionality, and calls from the other library are translated to your master library and back in order to force compatability between both libraries without any compromises.

Translatilla requires little setup, just use the [Installation](#installation) steps and you get support for all of these features without any modification:
- custom gamemodes
- modded lobbies
- miscellanous utilities from both mod loaders

<img src=".github/stop_fighting.png" width="90%">

## Installation
- Go to the [latest release](https://github.com/sirkingbinx/Translatilla/releases/latest) and download `Translatilla.zip`
- Extract the .zip file into your Gorilla Tag folder (that holds `Gorilla Tag.exe`).
- Launch the game with both Utilla and GorillaLibrary installed

## Implementation & History
### Why
In early 2026, [dev](https://github.com/developer9998), [Lapis](https://github.com/lapisgit), and several other notable modders formed the [Gorilla Tag Modding Hub](https://discord.gg/5rTmMjtECf) (GTMH) after splitting away from [Gorilla Tag Modding Group](https://discord.gg/monkemod) (GTMG) due to mishandling of allegations about a Verified Modder at that time. They built their own independent modding libraries (GorillaLibrary) which broke compatability with most mods that dependended on Utilla.

It left many modders and creators at a stopping point, forced to choose between a large community with official AA recommendation, and a smaller community but with many of the most popular mods in Gorilla Tag at that time.

To avoid modifying Utilla or GorillaLibrary to keep all groups of the modding community happy, Translatilla steps in and does heavy modifications at runtime with Harmony and BepInEx patchers to force the two to work together.

### How
In Translatilla's `bingus.translatilla.cfg` file, a "master library" is set (either Utilla or GorillaLibrary). The master library is in charge of providing modded lobby services (gamemode selectors, code of conduct board info, and lobby management). Calls to the non-master library are translated into methods for the master library, and then those return values are translated to the non-master library types required by a mod, hence the name **transla**tilla.

The perks of modifying modded lobby management means that any misc calls (eg. for GorillaLibrary's cosmetic utilities) are able to run as normal without breaking mods that rely on those methods.

## For Developers
### Support Translatilla
You don't need to change anything in order to make Translatilla work. Translatilla does nothing to modify regular mods, it only changes GorillaLibrary and Utilla. Just use your chosen library like normal and Translatilla will do the rest.

### How to tell if Translatilla is active
Only check this for debugging purposes. Disabling your mod just because you know Translatilla is there isn't very friendly, meanie.
```cs
// GorillaLibrary mods
// Note: Do not reference ConductBoardManager, there's a 50/50 chance it's disabled. Use one of these two if referencing by Behaviours: NetworkController, GameModeManager
//       This also requires you to publicize GorillaLibrary.
bool translatillaRunning = NetworkController.Instance.gameObject.name.Contains("GorillaLibrary/Translatilla");

// Utilla mods
// Note: This requires you to publicize Utilla.
bool translatillaRunning = UtillaNetworkController.Instance.gameObject.name.Contains("Utilla/Translatilla");
```
