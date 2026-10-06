# Changelog

## 1.3.2
- The sail and wind display you see while steering a ship now moves down when a bigger or moved
  map would cover it. New setting `Move sailing display below map` (section `4 - Layout`), on by
  default.
- Status effect icons no longer jump back and forth between row lengths when a long name such as
  Boon of the Lox is showing.
- `Offset X` for the status effect icons now always applies. Before, it was ignored while the icons
  were next to the map and the offset moved them towards it.
- Status effect icons stay in their normal place when no minimap is shown, for example in a world
  without a map.
- A new status effect no longer flickers in the wrong place for a moment when it appears.
- Compass letters placed outside the circle are now kept clear of the status effect icons too.
- Pins and other mods' icons drawn smaller than normal, such as with TheGreatestMap's marker sizes,
  are no longer hidden too early at the round edge.
- If the status effect or sailing display layout ever fails after a game update, only that part
  switches off instead of the whole mod.
- The debug section is now `9 - Debug`. If you changed the hierarchy dump key, set it again.

## 1.3.1
- Fixed: pins you hid with a map filter mod such as TheGreatestMap showed up on the minimap anyway,
  and opening the large map brought them all back. Filters are now respected on both maps.
- Pins at the round edge are still hidden as before, and keep the marker size set by other mods
  when they come back into view.

## 1.3.0
- The status effect icons next to the minimap (Rested, Wet, Cold and so on) now move aside when a
  bigger or moved map would cover them, keeping the same gap as in vanilla. They only move while the
  map actually reaches them, and go back when it no longer does.
- If moving them aside would push the icons off the screen, into the hotbar (or the quick slot bar
  from EquipmentAndQuickSlots) or into the ValheimPlus clock, they wrap into shorter rows and the list
  grows downwards instead.
- Long status effect names such as Boon of the Lox, which the game draws in a smaller font, now sit
  on the same baseline as the other names instead of higher.
- New section `8 - Status effects` to control all of this: row length, icon and row spacing, name
  alignment, what to keep clear of, and a manual offset for HUD layouts the mod does not know about.

## 1.2.2
- New icon. No changes to the mod itself.

## 1.2.1
- Icons that other mods draw on the minimap themselves now turn with the map and are hidden past the
  round edge, like the game's own pins. This fixes HUDCompass's cart, ship and portal markers, which
  stayed where a north-up map would have them and showed outside the circle. New setting
  `Turn icons from other mods` (section `2 - Rotation`), on by default.

## 1.2.0
- Added `Map offset X` and `Map offset Y` (section `4 - Layout`) to move the whole minimap anywhere on
  the screen, in pixels. The map, markers, compass, border and biome name all move together.
- A bigger minimap now grows towards the middle of the screen instead of equally in every direction,
  so at large sizes it no longer ends up pushed into the corner or partly off the screen. This is the
  new `Grow towards screen centre` setting (section `3 - Shape`), on by default. Turn it off to get
  the old behaviour.
- Added an optional box behind the biome name, with its own colour and opacity, padding, rounded
  corners and border (new section `7 - Biome label`). Off by default; give it a background colour to
  turn it on. The box fits the biome name as it changes and pulses along with it.
- Turning the mod off now also puts the map back to its original size. Before, it kept whatever size
  the multiplier had set.
- `Round minimap` off now always gives you the vanilla square map. Before, it did nothing while
  `Rotate minimap` was on, because rotation quietly kept the circle. The square map now stays north up
  and at vanilla size, and the descriptions of settings that only apply to the round map say so.
- Removed `Rotation method`. Its `Transform` option was a fallback that could hide map icons, and
  nothing needs it any more. If you had it set, the map simply uses the normal method.

## 1.1.3
- Fixed the version the mod reports about itself, which still said 1.0.0 no matter what the package
  version was. Mod managers and anything else that reads the plugin version now see the correct one.
  The version is now taken straight from the package manifest at build time, so the two cannot
  disagree again. No other changes.

## 1.1.2
- Rewrote the description and the mod page so they explain what the mod does for you, rather than how
  it works internally. No changes to the mod itself.

## 1.1.1
- Fixed the square backing panel reappearing behind the map when `Map size multiplier` was set above
  1x. Whether something counts as backing is now measured against the map's original size instead of
  the scaled circle, so it stays hidden at any multiplier.

## 1.1.0
- Added `Map size multiplier` (section `3 - Shape`): scales the whole small map, 0.5x–3x. This
  magnifies the map; use zoom to reveal more of the world.
- Added zoom, in a new `6 - Zoom` section: `Keypad +` / `Keypad -` by default with a configurable
  step. The level is remembered across sessions, including when changed with the game's own
  MapZoomIn / MapZoomOut bindings, which keep working.
- Settings reset to defaults on this update, because the config file is named after the plugin id
  and that id changed. Your old file is still in `BepInEx/config` under its previous name. Rename
  it to `ithilias.roundminimap.cfg` before launching to keep your settings.

## 1.0.0
- Initial release: round minimap that rotates with the direction you are looking, with a compass,
  configurable border, and tidied biome label and wind marker.
