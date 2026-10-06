# Valheim mods

Client-side [BepInEx](https://github.com/BepInEx/BepInEx) mods for Valheim by Ithilias.

| Mod | What it does |
| --- | --- |
| [RoundMinimap](RoundMinimap) | A round minimap that turns with you, with compass letters, resizing and zoom. |
| [StatusKeeper](StatusKeeper) | Keeps Rested and other timed buffs when you log out and back in. |
| [SnappySync](SnappySync) | Creatures and other players catch up to where they really are faster, so hits and dodges show up sooner. |
| [RuneUI](RuneUI) | A themed HUD with bars and hotbars at the bottom centre, a second quick bar on Alt+1 to 8, and a party list. |

Each mod's folder has its own README with settings and details, and a CHANGELOG.

## Installing

Install through a mod manager such as Gale or r2modman, from Hexium or Thunderstore. To install by
hand, put the mod's DLL into `BepInEx/plugins`.

## Building

Needs the .NET 8 SDK (or newer), a Valheim install and BepInEx. `build.sh` also needs bash and
`zip`; on Windows without them, build with `dotnet build <Mod> -c Release` and zip the five files
listed below by hand.

1. Tell the build where the game and BepInEx are, in one of these ways:
   - copy `Directory.Build.local.props.example` to `Directory.Build.local.props` and fill it in,
   - set the environment variables `VALHEIM_MANAGED` and `BEPINEX_CORE`,
   - or pass `-p:GameManaged=... -p:BepInExCore=...` to `dotnet build`.

   `GameManaged` (`VALHEIM_MANAGED`) is the game's `valheim_Data/Managed` folder, `BepInExCore`
   (`BEPINEX_CORE`) a `BepInEx/core` folder, for example from your mod manager profile. The local
   props file wins over the environment variables.
2. Build:

   ```bash
   ./build.sh                # every mod
   ./build.sh RoundMinimap   # just one
   ```

   Each mod ends up in `dist/<Mod>.zip` with its DLL, `manifest.json`, `icon.png`, `README.md` and
   `CHANGELOG.md`, ready to upload. Options after `--` go to `dotnet build`.

To test quickly, add `-p:PluginTarget=/path/to/BepInEx/plugins/<Mod>` to `dotnet build` and the
DLL is copied there. If a mod manager has already installed the mod, remove that copy first, or
both load under the same plugin ID.

A mod's version lives only in its `manifest.json`; the build writes it into the DLL.

## Contributing

Issues and pull requests are welcome. Commit messages follow
[Conventional Commits](https://www.conventionalcommits.org), scoped to the mod where it applies,
for example `fix(roundminimap): respect map filters`. Pull requests run
`.github/scripts/checks.sh`, which checks version bumps, CHANGELOG entries and config names; run
it locally before pushing.

## License

[MIT](LICENSE)
