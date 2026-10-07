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
- **Quick slots.** Up to six extra slots for any item but ammo, shown right of the quick bar and below
  your inventory, or right of it while a chest is open. Each has its own key, Z, V and B for the
  first three, and you can change the keys and labels. A key uses its item: eats food, drinks a mead,
  or equips a weapon or tool. While you use one, vanilla actions on the same key, such as V for auto
  pickup, do not trigger. Items in them count toward your weight, and workbenches repair and upgrade
  them there. They are not used as crafting or building material.
- **Gear slots.** Six slots for worn armour right of the inventory: head, chest, legs, cape, utility and
  trinket. An empty slot shows a faint icon of what goes there. What is in them is worn, so armour no
  longer takes up inventory space.
  Put armour on in any way and it moves into its slot; drag it out or right click it and it comes off.
  Workbenches repair and upgrade it in the slots, and it counts toward your weight. While you hold an
  item, the quick and gear slots it fits in light up. Hold Left Alt and drag either row to move it.
- **Balance options.** Off by default: wear up to three utility items at once, add inventory rows, and
  change the base carry weight.
- **Back in place after death.** Quick and gear slots go into your tombstone with everything else.
  When you pick your things up again, items go back into their quick slot and everything you wore is
  put on again, as long as you have not put something else in that place meanwhile. Settings let you
  keep the gear or quick slots on death instead, or skip re-equipping.
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
  long row into the quick slots.
- **Party list.** Players within 100 metres are listed in the top left with their health bar and
  numbers, closest first. The health bars floating over their heads are hidden; their names stay.
- **Your theme.** Panel, border, text and bar colours, the font, corner roundness and border width
  are all settings, and every block can be moved and scaled.

Everything can be switched off individually, and turning the mod off puts the vanilla HUD back.

## Installing

With a mod manager, just install it. Manually, drop `RuneUI.dll` into `BepInEx/plugins`.

Client side only. It changes nothing that other players or the server can see, so it works on any
server and nobody else needs it. The party list reads the health every client already receives.

The quick and gear slots are saved inside your character file. If you remove the mod, the items in them
stay in the save and are back when you install it again, so empty the slots first if you want to keep
those items without the mod.

## Coming from Equipment and Quick Slots

Rune UI's quick and gear slots replace Equipment and Quick Slots, so remove that mod. The first time
you log in with Rune UI, the items in its quick and equipment slots move into Rune UI's slots, and
anything else from its extra rows into free inventory cells. Anything that does not fit is dropped by
the game where you spawn, so make some room in your inventory before switching.

If you already played without either mod and the game dropped your slot items, the cheat command
`runeui_eaqs_restore` brings back the items in Equipment and Quick Slots' last backup. Only use it if
the items are really gone, since it creates them again.

## Console commands

| Command | What it does |
| --- | --- |
| `runeui_slots` | Lists what is in the quick and gear slots |
| `runeui_emptyslots` | Takes off and moves everything in the quick and gear slots into the inventory, for example before removing the mod |
| `runeui_eaqs_restore` | Cheat. Brings back the items in Equipment and Quick Slots' backup, see above |

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
| Show empty hotbar slots | `true` | Always show all eight hotbar slots, like the quick bar. Vanilla only shows them up to the last item, and none after a death empties your inventory |
| Hotbar anchor | `Bottom` | Screen point for the hotbar |
| Hotbar offset X | `0` | Horizontal offset |
| Hotbar offset Y | `86` | Vertical offset |
| Hotbar scale | `1` | Size of the hotbar and quick bar on top of `HUD scale`, 0.5 to 2. They shrink and grow around the hotbar anchor, so the slots stay together. The hotbar's size needs `Move hotbar` |
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

### 8 - Quick slots

| Setting | Default | What it does |
| --- | --- | --- |
| Enabled | `true` | Show the quick slots. Off only hides them; items already in them are kept |
| Slot count | `3` | Number of quick slots, 0 to 6. Items in slots you remove move to your inventory |
| Slot 1 key to Slot 6 key | `Z`, `V`, `B`, none, none, none | Key that uses the item in that slot, with modifiers if you like, such as `LeftAlt + Z`. While it is held with an item in the slot, vanilla actions on the same key do not trigger |
| Slot 1 label to Slot 6 label | empty | Text shown on the slot. Empty shows its key |
| Inventory offset X | `0` | Horizontal position of the quick slots under the inventory. With a chest open they sit right of the inventory instead |
| Inventory offset Y | `-12` | Vertical position of the quick slots under the inventory. With a chest open they sit right of the inventory instead |
| Chest offset X | `0` | Horizontal nudge of the quick slots from their spot right of the inventory while a chest is open |
| Chest offset Y | `0` | Vertical nudge of the quick slots from their spot right of the inventory while a chest is open |

### 9 - Skill toasts

| Setting | Default | What it does |
| --- | --- | --- |
| Enabled | `true` | Show a toast with the skill's level and progress whenever a skill gains experience |
| Duration | `4` | Seconds a toast stays after the skill's last gain, 1 to 20 |
| Max toasts | `4` | Most toasts at once, the oldest goes first, 1 to 10 |
| Anchor | `Right` | Screen point for the toasts. Older toasts move away from it |
| Offset X | `-20` | Horizontal offset |
| Offset Y | `-60` | Vertical offset |

### 10 - Gear slots

| Setting | Default | What it does |
| --- | --- | --- |
| Enabled | `true` | Show the six gear slots right of the inventory. What is in them is worn. Off only hides them; gear already in them stays worn |
| Offset X | `0` | Horizontal nudge of the gear slots from their spot right of the inventory |
| Offset Y | `0` | Vertical nudge of the gear slots from their spot right of the inventory |
| Drag key | `LeftAlt` | Hold this in the inventory and drag the quick or gear slots with the left mouse button to move them. The position is saved to their offset settings. `None` turns dragging off |

### 11 - Death

| Setting | Default | What it does |
| --- | --- | --- |
| Keep gear | `false` | Gear in the gear slots stays with you when you die, and you still wear it after respawning. Makes dying easier than vanilla |
| Keep quick slots | `false` | Items in the quick slots stay with you when you die. Makes dying easier than vanilla |
| Re-equip armour | `true` | When you pick up your tombstone, put the armour you wore back on |
| Re-equip weapons | `true` | When you pick up your tombstone, take the weapons, shield, tools and ammo you held back in hand |

### 12 - Balance

These change how the game plays, so agree on them with the people you play with.

| Setting | Default | What it does |
| --- | --- | --- |
| Utility items | `1` | Utility items, such as belts and the Wishbone, you can wear at once, 1 to 3. Each extra one adds a gear slot. You can never wear two of the same item, and the extra ones do not show on your character |
| Extra inventory rows | `0` | Rows added to your inventory, 0 to 5, up to the game's limit of 9 rows. Items in rows you remove move to free cells, or are dropped on the ground if there is no room. Remove items from the extra rows before removing the mod, or the game drops them |
| Base carry weight | `300` | How much you can carry before belts and other bonuses, 50 to 5000. 300 is vanilla and leaves the value to the game and other mods |
