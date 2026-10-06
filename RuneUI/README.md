# Rune UI

A new look for Valheim's HUD. Your health, stamina, hotbars and food sit together at the bottom
centre of the screen, drawn in one theme whose colours, font and shape you can change. Rune UI also
adds a second quick bar and a list of the players around you.

This is the first part of a full UI redesign. The inventory, crafting, build and menu screens will be
restyled in later versions.

## What it does

- **Bars at the bottom centre.** Health, stamina, eitr and adrenaline are drawn as themed bars with
  their values. Eitr shares the stamina row, and adrenaline only shows when you have some.
- **Hotbar under the bars.** The hotbar moves to the bottom centre and its slots get the theme.
- **Second quick bar.** Your second inventory row is shown as another hotbar under the first. Hold
  Left Alt and press 1 to 8 to use its items. While Alt is held, 1 to 8 do not use the normal hotbar.
- **Food next to the hotbars.** Your three foods with their time left. Like vanilla, an icon pulses
  when you can eat that food again and the timer blinks in the last minute.
- **Party list.** Players within 100 metres are listed in the top left with their health, closest
  first. The health bars floating over their heads are hidden; their names stay.
- **Your theme.** Panel, border, text and bar colours, the font, corner roundness and border width
  are all settings, and every block can be moved and scaled.

Everything can be switched off individually, and turning the mod off puts the vanilla HUD back.

## Installing

With a mod manager, just install it. Manually, drop `RuneUI.dll` into `BepInEx/plugins`.

Client side only. It changes nothing that other players or the server can see, so it works on any
server and nobody else needs it. The party list reads the health every client already receives.

## Settings

Config file: `BepInEx/config/ithilias.runeui.cfg`, created the first time you run the game. Settings
apply immediately, without a restart.

Anchors are one of `TopLeft`, `Top`, `TopRight`, `Left`, `Center`, `Right`, `BottomLeft`, `Bottom`
and `BottomRight`. A block's matching corner or edge sits on that point of the screen, moved by its
offsets.

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

### 5 - HUD layout

| Setting | Default | What it does |
| --- | --- | --- |
| HUD scale | `1` | Size of the bars, hotbars, food and party list, 0.5 to 2 |
| Replace bars | `true` | Hide the vanilla health, food, stamina, eitr and adrenaline displays and show the bars and food row instead |
| Bars anchor | `Bottom` | Screen point for the bars |
| Bars offset X | `0` | Horizontal offset |
| Bars offset Y | `172` | Vertical offset |
| Bars width | `420` | Width of the bars, 150 to 1000 |
| Move hotbar | `true` | Move the vanilla hotbar to the position below |
| Style hotbar | `true` | Draw hotbar and quick bar slots in the theme |
| Hotbar anchor | `Bottom` | Screen point for the hotbar |
| Hotbar offset X | `0` | Horizontal offset |
| Hotbar offset Y | `92` | Vertical offset |
| Food anchor | `Bottom` | Screen point for the food row |
| Food offset X | `380` | Horizontal offset |
| Food offset Y | `16` | Vertical offset |

### 6 - Quick bar 2

| Setting | Default | What it does |
| --- | --- | --- |
| Enabled | `true` | Show inventory row 2 as a quick bar and use it with the modifier key |
| Modifier key | `LeftAlt` | Hold this and press 1 to 8 to use that slot of row 2 |
| Anchor | `Bottom` | Screen point for the quick bar |
| Offset X | `0` | Horizontal offset |
| Offset Y | `16` | Vertical offset |

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
