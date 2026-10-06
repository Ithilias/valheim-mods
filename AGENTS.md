# Notes for coding agents

Client-side BepInEx mods for Valheim, one folder per mod. Read the root README for the build setup.

## Build and test

- `./build.sh [Mod]` builds and packages into `dist/<Mod>.zip`. Game paths come from
  `Directory.Build.local.props` (gitignored), never from tracked files.
- Shared build logic is in `Directory.Build.props` and `Directory.Build.targets`. A mod's `.csproj`
  only names the game assemblies it uses (`GameAssembly` items).
- There are no automated tests; changes are checked in game. Say so when a change is untested.
- Do not replace a DLL while the game runs (it is locked). Mod managers such as Gale hardlink
  installed files to their cache: delete the installed DLL before copying a new one, or the
  cached package is changed too.

## Rules

- The version lives only in `manifest.json`. A published version can never be uploaded again, so
  every released change, even an icon, needs a version bump and a CHANGELOG entry.
- CHANGELOG entries are written for players: what changed for them, not how the code works.
- BepInEx config sections and keys must not contain `= \n \t \ " ' [ ]`. One of these in a key
  throws in `Awake` and the whole mod silently does nothing.
- Renaming a config section or key resets that setting for every player; avoid it, or note it in
  the CHANGELOG.
- Keep READMEs in sync with the `Config.Bind` calls: names, defaults, sections.
- Plain, concise English. No em-dashes.

## Working with the game

- Decompile the game with `ilspycmd` on `valheim_Data/Managed/assembly_valheim.dll` to check how
  vanilla does something before patching it.
- Do not fight the game or other mods for state they own. Prefer changing only what this mod
  added, and undo exactly that when a setting is turned off. Example: RoundMinimap hides pins by
  scale, not `SetActive`, because map filter mods switch pins off themselves.
- Unity's `anchoredPosition` does not read back exactly what was written; compare with a
  tolerance.
- `Hud`, `Minimap` and their children are destroyed on logout and rebuilt on rejoin; drop cached
  references when the instance changes.

## Git

- Conventional Commits, scoped to the mod: `fix(roundminimap): ...`, `docs(statuskeeper): ...`.
- One branch and pull request per topic, squash merged. Never commit to `main` directly.
