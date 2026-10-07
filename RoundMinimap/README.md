# Round Minimap

Turns Valheim's square minimap into a round one that rotates with you, so whatever is ahead of you is
always at the top. No more mentally rotating the map every time you turn around.

## What it does

- **Rotates with you.** The map turns as you look around, so up on the map is always the way you are
  facing. Your arrow stays pointing up and the wind indicator turns with the map, so both still read
  correctly.
- **Round, not square.** The map is drawn as a circle with a soft rim, and the square panel behind it
  is tidied away.
- **Compass letters.** N, E, S and W sit around the edge and move as the map turns, so you can always
  tell which way north is.
- **Resize and move it.** Make the minimap bigger or smaller, up to 3x, and move it anywhere on the
  screen. A bigger map grows towards the middle of the screen, so it stays clear of the edge.
- **Status icons stay clear.** When a bigger map reaches the Rested, Wet, Cold and other status
  icons, they move aside, and wrap into shorter rows rather than run into the hotbar, the
  ValheimPlus clock or off the screen. Long names like Boon of the Lox line up with the
  others.
- **Sailing display stays clear.** While you steer a ship, the sail and wind display moves down
  below a bigger or moved map instead of being covered by it.
- **Readable biome name.** The biome name sits centred under the map, and can get a background box and
  border so it stands out over bright ground.
- **Zoom in and out.** Keypad + and Keypad - by default. The zoom level is remembered between
  sessions.
- **Pins stay readable.** Pin positions turn with the map but the icons themselves stay upright, and
  pins that would hang over the round edge are hidden instead of poking out.

Everything can be switched off individually, and turning the mod off puts the vanilla minimap back
exactly as it was.

The three looks, one switch each:

| You want | Set |
| --- | --- |
| Round, turns with you | the defaults |
| Round, north always up | `Rotate minimap` off |
| The vanilla square map | `Round minimap` off |

Rotation, resizing, the compass and most layout settings belong to the round map. The square map is
kept exactly vanilla, apart from `Map offset`, the biome label box, the status effect settings and
the sailing display, which work on both.

## Installing

With a mod manager, just install it. Manually, drop `RoundMinimap.dll` into `BepInEx/plugins`.

Client side only. It changes nothing that other players or the server can see, so it works on any
server and nobody else needs it.

## Settings

Config file: `BepInEx/config/ithilias.roundminimap.cfg`, created the first time you run the game.
Most settings apply immediately, without a restart.

### 1 - General

| Setting | Default | What it does |
| --- | --- | --- |
| Enabled | `true` | Master switch. Off puts the vanilla minimap back |

### 2 - Rotation

Round map only.

| Setting | Default | What it does |
| --- | --- | --- |
| Rotate minimap | `true` | Off keeps north at the top |
| Rotation follows | `PlayerLook` | Follow the camera, or your character's facing with `PlayerBody` |
| Smoothing | `0.08` | Seconds of easing. `0` snaps instantly, higher lags behind |
| Fix player arrow | `true` | Arrow points up and the map turns instead |
| Keep pin icons upright | `true` | |
| Turn icons from other mods | `true` | Also turns icons other mods draw on the minimap, like HUDCompass's cart, ship and portal markers |

### 3 - Shape

| Setting | Default | What it does |
| --- | --- | --- |
| Round minimap | `true` | Off gives the vanilla square map |
| Map size multiplier | `1.0` | Scales the round minimap, 0.5x–3x |
| Grow towards screen centre | `true` | A bigger map grows towards the middle of the screen instead of past its edge |
| Circle scale | `1.0` | Circle size as a fraction of the map's shortest side |
| Circle segments | `128` | Smoothness of the circle |
| Border thickness | `3` | Rim over the circle's edge. `0` for none |
| Border shadow | `5` | Soft shadow fading outwards. `0` for none |
| Border shadow strength | `0.5` | |
| Border color | `0D0A08E6` (dark) | Colour and opacity of the rim and shadow. While the colour is left at the default, the rim follows the vanilla map frame, so mods that recolour it (e.g. Seasons) carry over. Any other colour is used as set |

### 4 - Layout

| Setting | Default | What it does |
| --- | --- | --- |
| Map offset X | `0` | Moves the whole map sideways, in pixels. Negative is left |
| Map offset Y | `0` | Moves the whole map up or down, in pixels. Negative is down |
| Move sailing display below map | `true` | While steering a ship, moves the sail and wind display down when a bigger or moved map would cover it |
| Hide square background | `true` | Hides the square panel that would stick out past the circle |
| Center biome label below map | `true` | |
| Biome label gap | `8` | |
| Pull wind marker inside circle | `true` | |
| Wind marker distance | `0.8` | |
| Hidden elements | `""` | Names of extra HUD pieces to hide, if something is left over |

### 5 - Compass

| Setting | Default | What it does |
| --- | --- | --- |
| Compass | `Cardinal` | `Off`, `NorthOnly`, or `Cardinal` for all four |
| Compass letters | `N,E,S,W` | North, east, south, west, in that order |
| Compass distance | `0.86` | How far out the letters sit |
| Compass font size | `14` | |
| Compass color | warm white | |

### 6 - Zoom

| Setting | Default | What it does |
| --- | --- | --- |
| Zoom level | `0.01` | Remembered automatically. Smaller is closer in |
| Zoom step | `1.3` | How far one key press zooms |
| Zoom in key | `Keypad +` | |
| Zoom out key | `Keypad -` | |

### 7 - Biome label

A box behind the biome name, off by default. Give it a background colour to turn it on.

| Setting | Default | What it does |
| --- | --- | --- |
| Background color | none | Colour and opacity of the box. The last two hex digits are opacity, `00` draws nothing. `000000AA` is a dark see-through box |
| Padding horizontal | `8` | Space left and right of the name |
| Padding vertical | `3` | Space above and below the name |
| Corner radius | `4` | `0` for square corners |
| Border thickness | `0` | Border around the box. `0` for none. Also works without a background colour |
| Border color | warm white | |

### 8 - Status effects

The Rested, Wet, Cold and other status icons next to the map. These work on both the round and the
square map.

The mod keeps the icons clear of the map, the screen edge, hotbars and the ValheimPlus clock by
itself. Other mods' HUD pieces are not detected. If the icons collide with one, or you would rather
place them yourself, turn `Make room for status effects` off and use `Offset X`, `Offset Y`,
`Icons per row` and the spacing settings.

| Setting | Default | What it does |
| --- | --- | --- |
| Make room for status effects | `true` | Moves the icons aside when the map grows or moves into them, keeping the vanilla gap |
| Wrap rows to fit | `true` | Puts fewer icons in each row, so the list grows downwards, when moving them aside would push them off the screen or into something |
| Minimum icons per row | `3` | The shortest a row gets when wrapping |
| Icons per row | `0` | Row length by hand. `0` is the game's own |
| Icon spacing | `0` | Distance between icons. `0` is the game's own |
| Row spacing | `0` | Distance between rows. `0` is the same as `Icon spacing` |
| Line up shrunk names | `true` | Long names such as Boon of the Lox, which the game draws smaller, sit on the same baseline as the others instead of higher |
| Avoid hotbars | `true` | Keeps the icons out of the hotbar and extra bars from mods such as EquipmentAndQuickSlots |
| Avoid ValheimPlus clock | `true` | Keeps the icons out of the ValheimPlus day and time text |
| Edge margin | `8` | Free pixels kept to the screen edge, hotbars and the ValheimPlus clock |
| Offset X | `0` | Moves the icons sideways by hand, for HUD layouts the mod does not know about. Negative is left |
| Offset Y | `0` | Moves the icons up or down by hand. Negative is down |

### 9 - Debug

| Setting | Default | What it does |
| --- | --- | --- |
| Dump hierarchy key | `Ctrl+F9` | Logs the minimap's UI pieces to the BepInEx log, see below |

## If something looks wrong

**`Round minimap` is off but some setting does nothing.** Most settings only apply to the round map.
Their descriptions in the config say "Round map only".

**Something is still sticking out behind the circle.** Press the dump hierarchy key (`Ctrl+F9`),
then look in `BepInEx/LogOutput.log` for a block starting `RoundMinimap hierarchy dump`. Find the
name of the piece you want gone and add it to `Hidden elements`.

## How it works, briefly

The map is drawn as a circle by replacing the mesh of the game's own map image with a disc, and it is
rotated by sampling that image at a turned offset rather than by rotating anything in the interface.
That keeps the game's own per frame updates to the map working, and means nothing attached to the map
gets dragged around as it turns. Pin positions are recalculated every frame so they stay correct
while you turn on the spot.

## Source

Source code and build instructions: https://github.com/Ithilias/valheim-mods
