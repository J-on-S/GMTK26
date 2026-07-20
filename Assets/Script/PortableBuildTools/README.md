# Portable Build Tools
## Install

Copy the `Editor/` folder into your project's `Assets/` (any depth, as long as the
folder is named `Editor` or sits under one). All types are in the `BuildTools` namespace.



## Setup

1. `Assets > Create > Build Tools > Build Config` — put the asset in an `Editor/` folder
   (it's an editor-only type; Unity can't deserialize it elsewhere).
2. Fill it in:
   - **Output** – build folder (Browse…), optional product-name override, dev-build toggle.
   - **Scripting Flags** – rows of `define` (+ optional `PlayerPref` key) toggled per build.
   - **PlayerPrefs Seeds** – arbitrary int prefs written before building (save/progress seeding).
   - **Zip** – toggle + zip output folder (Browse…; blank = a `zips` folder next to the build).
   - **itch.io** – toggle, paste the game URL (`https://user.itch.io/game`), butler path
     (Browse…; blank = `butler` on PATH), upload timeout.

## Use

- **Default Build button** (File > Build Settings/Profiles > Build) is intercepted: it opens
  the Build Configurator window → pick a config → Build. Target = whatever's active in Unity.
- **Tools > Build > Dated Build (active target)** — quick dated build to a folder you pick.
  Auto-zips; never uploads.

## Files

| File | Role |
|------|------|
| `BuildConfig.cs` | The settings asset (data-driven, no game specifics) |
| `BuildConfigEditor.cs` | Inspector with Browse buttons + itch URL parse preview |
| `BuildConfigWindow.cs` | Modal shown when the Build button is clicked |
| `BuildConfigurator.cs` | Intercepts Build, applies config, stashes deploy settings |
| `BuildPostprocess.cs` | `ZipPostprocess` + `ItchUploader` post-build hooks |
| `DatedBuild.cs` | Standalone dated-build menu command |
| `ItchUrl.cs` | Parses `user.itch.io/game` URLs |
| `SceneBootstrapConfig.cs` | Scene-bootstrap settings asset (`SceneTools` namespace) |
| `SceneBootstrapInjector.cs` | Auto-adds listed prefabs to any opened/created scene |
