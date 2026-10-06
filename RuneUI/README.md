# Rune UI

A new look for Valheim's HUD. Your health, stamina, hotbars and food sit together at the bottom
centre of the screen, drawn in one theme whose colours, font and shape you can change. Rune UI also
adds a second quick bar and a list of the players around you.

This is the first part of a full UI redesign. The inventory, crafting, build and menu screens will be
restyled in later versions.

## What it does

- **Bars at the bottom centre.** Health, stamina, eitr, adrenaline and stagger are drawn as shaded,
  themed bars. Eitr shares the stamina row once you have any. Adrenaline and stagger are thin bars
  above health that only show while they fill, so health and stamina never move.
- **Hotbar under the bars.** The hotbar moves to the bottom centre and its slots get the theme. Your
  forsaken power sits as a ninth slot on its right, with its key and cooldown.
- **Second quick bar.** Your second inventory row is shown as another hotbar under the first, with
  Alt shown once on its left. Hold Left Alt and press 1 to 8 to use its items. While Alt is held, 1
  to 8 do not use the normal hotbar.
- **Food slots.** Three extra slots that only take food, shown right of the quick bar and below your
  inventory, or right of it while a chest is open. Drag food into them in the inventory and eat it with Z, U and B (Z can be switched to Y
  for keyboards with Y and Z swapped). Food in them counts toward your weight and goes into your
  tombstone when you die.
- **Buffs in one place.** Eaten food and status effects such as Rested or Wet are shown together as
  hotbar sized icons in the bottom left, filling rows upwards. Rested and Resting show your comfort
  level in the corner. Like vanilla, a food icon pulses when
  you can eat it again and the timer blinks in the last minute.
- **Quality rings.** Upgradable items get a ring in their quality colour in the hotbars, inventory and
  chests: grey, green, blue and gold for quality 1 to 4, and red with the number for items upgraded
  past their normal maximum.
- **Skill toasts.** When a skill gains experience, a toast on the right shows its level and a bar
  with the progress to the next level. Further gains of that skill update the same toast and keep it
  up; a level up flashes the name.
- **Key hints in a column.** The key hints in the bottom right are stacked instead of running in a
  long row into the food slots.
- **Party list.** Players within 100 metres are listed in the top left with their health bar and
  numbers, closest first. The health bars floating over their heads are hidden; their names stay.
- **Your theme.** Panel, border, text and bar colours, the font, corner roundness and border width
  are all settings, and every block can be moved and scaled.

Everything can be switched off individually, and turning the mod off puts the vanilla HUD back.

## Installing

With a mod manager, just install it. Manually, drop `RuneUI.dll` into `BepInEx/plugins`.

Client side only. It changes nothing that other players or the server can see, so it works on any
server and nobody else needs it. The party list reads the health every client already receives.

The food slots are saved inside your character file. If you remove the mod, food in them stays in the
save and is back when you install it again, so empty the slots first if you want to keep that food
without the mod.

## Settings

Config file: `BepInEx/config/ithilias.runeui.cfg`, created the first time you run the game. Settings
apply immediately, without a restart.

Anchors are one of `TopLeft`, `Top`, `TopRight`, `Left`, `Center`, `Right`, `BottomLeft`, `Bottom`
and `BottomRight`. A block's matching corner or edge sits on that point of the screen, moved by its
offsets. Offsets go from -2000 to 2000; negative values move left and down.

### 1 - General

| Setting | Default | What it does |
| --- | --- | --- |
| Enabled | `true` | Master switch. Off puts the vanilla HUD back |
| Dump HUD key | `Ctrl+F8` | Logs the HUD's object tree to the BepInEx log, for bug reports |

### 2 - Colors

| Setting | Default | What it does |
| --- | --- | --- |
| Panel background | dark grey, 82% | Fill of panels, bar backgrounds and slots |
| Panel border | gold | Border of panels, bars and slots |
| Accent | light gold | Adrenaline bar |
| Text | off-white | Text drawn by this mod |
| Health bar | red | Health bar, also in the party list |
| Stamina bar | yellow | Stamina bar |
| Eitr bar | purple | Eitr bar |
| Stagger bar | light grey | Stagger bar |
| Quality 1 | grey | Ring around upgradable items at quality 1 |
| Quality 2 | green | Ring at quality 2 |
| Quality 3 | blue | Ring at quality 3 |
| Quality 4 | gold | Ring at quality 4 |
| Quality above max | red | Ring around items upgraded past their normal maximum, with the number |

### 3 - Font

| Setting | Default | What it does |
| --- | --- | --- |
| Font name | empty | A font loaded by the game, such as `Norse SDF`. Empty uses the vanilla HUD font |
| Font size scale | `1` | Size of text drawn by this mod, 0.5 to 2 |

### 4 - Shape

| Setting | Default | What it does |
| --- | --- | --- |
| Corner radius | `5` | Roundness of panels, bars and slots, 0 to 16 pixels |
| Border width | `1.5` | Border thickness, 0 to 6 pixels. 0 draws no border |
| Bar shading | `0.5` | Fades bars from light at the top to dark at the bottom for a 3D look, 0 to 1. 0 keeps them flat |
| Quality rings | `true` | Ring in the quality colour around upgradable items in the hotbars, inventory and chests |
| Quality ring width | `2.5` | Ring thickness, 1 to 8 pixels |

### 5 - HUD layout

| Setting | Default | What it does |
| --- | --- | --- |
| HUD scale | `1` | Size of the bars, hotbars, buffs and party list, 0.5 to 2 |
| Replace bars | `true` | Hide the vanilla health, food, stamina, eitr, adrenaline and stagger displays and show the bars instead. Eaten food then shows with the buffs |
| Stack bars | `true` | Stack the quick bar under the hotbar and the bars on top of it so they never overlap. The hotbar's anchor and offsets then move all three, and the quick bar and bars offsets only nudge each one from its stacked spot. Needs `Move hotbar` |
| Stack gap | `6` | Space between stacked blocks, 0 to 50 |
| Bars anchor | `Bottom` | Screen point for the bars |
| Bars offset X | `0` | Horizontal offset. With `Stack bars`, a nudge from the stacked spot |
| Bars offset Y | `0` | Vertical offset. With `Stack bars`, a nudge from the stacked spot |
| Bars width | `420` | Width of the bars, 150 to 1000 |
| Stack key hints | `true` | Show the key hints in the bottom right as a column instead of a long row |
| Key hints spacing | `4` | Space between stacked key hints, 0 to 40 |
| Move hotbar | `true` | Move the vanilla hotbar to the position below |
| Style hotbar | `true` | Draw hotbar and quick bar slots in the theme |
| Hotbar anchor | `Bottom` | Screen point for the hotbar |
| Hotbar offset X | `0` | Horizontal offset |
| Hotbar offset Y | `86` | Vertical offset |
| Forsaken power slot | `true` | Show your forsaken power as a slot right of the hotbar instead of the vanilla display |
| Unify buffs | `true` | Show eaten food and status effects together as hotbar sized icons instead of the vanilla displays |
| Buffs anchor | `BottomLeft` | Screen point for the buffs. Rows fill away from it |
| Buffs offset X | `20` | Horizontal offset |
| Buffs offset Y | `20` | Vertical offset |
| Buffs per row | `6` | Icons in a row before a new row starts, 1 to 20 |

### 6 - Quick bar 2

| Setting | Default | What it does |
| --- | --- | --- |
| Enabled | `true` | Show inventory row 2 as a quick bar and use it with the modifier key |
| Modifier key | `LeftAlt` | Hold this and press 1 to 8 to use that slot of row 2 |
| Anchor | `Bottom` | Screen point for the quick bar |
| Offset X | `0` | Horizontal offset. With `Stack bars`, a nudge from the stacked spot under the hotbar |
| Offset Y | `0` | Vertical offset. With `Stack bars`, a nudge from the stacked spot under the hotbar |

### 7 - Party list

| Setting | Default | What it does |
| --- | --- | --- |
| Enabled | `true` | List nearby players with their health |
| Range | `100` | Only players within this many metres, 10 to 500 |
| Max players | `10` | Most rows shown, closest first, 1 to 20 |
| Hide bars over players | `true` | Hide the health bar over other players. Their names stay |
| Anchor | `TopLeft` | Screen point for the list |
| Offset X | `20` | Horizontal offset |
| Offset Y | `-20` | Vertical offset |
| Width | `220` | Width of each row, 120 to 500 |

### 8 - Food slots

| Setting | Default | What it does |
| --- | --- | --- |
| Enabled | `true` | Show the three food slots. Off only hides them; food already in them is kept |
| First key | `Z` | Key for the first slot, next to U and B. Pick `Y` if your keyboard has Y and Z swapped |
| Inventory offset X | `0` | Horizontal position of the food slots under the inventory. With a chest open they sit right of the inventory instead |
| Inventory offset Y | `-12` | Vertical position of the food slots under the inventory. With a chest open they sit right of the inventory instead |

### 9 - Skill toasts

| Setting | Default | What it does |
| --- | --- | --- |
| Enabled | `true` | Show a toast with the skill's level and progress whenever a skill gains experience |
| Duration | `4` | Seconds a toast stays after the skill's last gain, 1 to 20 |
| Max toasts | `4` | Most toasts at once, the oldest goes first, 1 to 10 |
| Anchor | `Right` | Screen point for the toasts. Older toasts move away from it |
| Offset X | `-20` | Horizontal offset |
| Offset Y | `-60` | Vertical offset |
