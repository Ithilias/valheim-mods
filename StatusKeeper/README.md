# Status Keeper

Keeps your Rested bonus, potions and other timed buffs when you log out and back in, instead of
losing them every time you rejoin.

## The problem

Valheim never saves your active buffs. Log out with eight minutes of Rested left and it is simply
gone when you come back, and Rested in particular means going and sitting by a fire again. Status
Keeper writes your remaining buffs into your character file and puts them back when you next spawn,
with whatever time was left on them.

Time spent logged out does not count against them. A buff is worth the time you actually play under
it, so you get back exactly what you left with.

## What is kept

Anything with a timer that is not on the exclusion list. In practice that means Rested, plus potion
and mead buffs.

Deliberately not kept:

- **Things the game works out for itself**, like Wet, Cold, Freezing, Shelter and Campfire. These are
  recalculated from your surroundings the moment you spawn, so saving them would achieve nothing.
- **Damage over time effects**, like Poison, Burning and Frost. The game keeps the remaining damage
  for these in a place that cannot be restored, so a restored Poison would look real but tick for
  nothing. If you would rather logging out stopped curing them, remove them from the `Denylist` in
  the config, but that catch still applies.

Food is already saved by the game itself, so it is not affected either way.

## Installing

With a mod manager, just install it. Manually, drop `StatusKeeper.dll` into `BepInEx/plugins`.

Client side only. Your buffs live in your character file, which is stored on your machine, so this
works on any server and nobody else needs it. Do not install it on a dedicated server.

## Settings

Config file: `BepInEx/config/ithilias.statuskeeper.cfg`, created the first time you run the game.

| Setting | Default | What it does |
| --- | --- | --- |
| Enabled | `true` | Master switch |
| Denylist | env and damage effects | Buffs never carried across. See above |
| Allowlist | `""` | Buffs always carried across, even if listed above. Wins over the denylist |
| Minimum remaining seconds | `1` | Below this, a buff is not worth keeping |
| Normalise rested effect | `true` | Restores rested from a fire as the plain Rested buff, so it is not lost as soon as you walk away from the fire |
| Rested source prefabs | `Resting,Rested` | The effects treated as rested for the setting above |
| Rested target prefab | `Rested` | The effect they are restored as |
| Dump status effects key | `Ctrl+F10` | Lists your active buffs in the log |

To change which buffs are kept, press `Ctrl+F10` while they are active and look in
`BepInEx/LogOutput.log`. Every active effect is listed with its real name and whether it would be
kept, which is what you put in the deny and allow lists.

## Source

Source code and build instructions: https://github.com/Ithilias/valheim-mods
