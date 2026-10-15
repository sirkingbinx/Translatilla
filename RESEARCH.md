## Research
This documents the startup processes of both Utilla and GorillaLibrary for future reference when writing Translatilla.

### Utilla startup process
- **Plugin::Plugin() static constructor called**:
    - Applies harmony patches (we don't know which constructor gets called first, so we undo these patches with Harmony)
    - Hooks the **OnGameInitialized** function to Events (we do this already)
- **Plugin::OnGameInitialized()**:
    - Creates a gameobject and attached UtillaNetworkController, GamemodeManager, and VersionCheckManager
- **GamemodeManager::Awake()**:
    - Hooks room join/leave events (we do this for them)
    - Creates gameobject `Utilla Custom Game Modes`
    - Loads your last selected gamemode from PlayerPrefs
    - Creates the modded gamemodes, a copy of in-game gamemodes prefixed with `MODDED_`
    - Searches for custom gamemodes
    - Adds all gamemodes (type `Gamemode`) to a `Gamemodes` list, and calls `AddGamemodeToPrefabPool` for each
- **GamemodeManager::AddGamemodeToPrefabPool()**:
    - Filters duplicate gamemodes
    - Adds gamemode to `GameMode.gameModeKeyByName` after misc validity checks