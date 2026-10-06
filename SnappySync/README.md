# SnappySync

Makes creatures and other players move on your screen closer to where they really are. Knockbacks,
dodges and sudden turns show up sooner instead of trailing behind.

## The problem

On a server, the creatures around you are simulated somewhere else. Your game only receives their
positions and slides them toward each new one. Vanilla does that slowly: it takes roughly 90 ms
for a creature to catch up to where the game has already been told it is. On top of that, the
catch-up only runs 50 times a second, so a higher frame rate does not help at all.

SnappySync closes that gap faster, about 40 ms by default, and does it every frame you render. With
it, a higher frame rate genuinely makes things look more responsive.

## What it does not do

It only changes how quickly your screen catches up to information your game already has. It never
guesses ahead, so it cannot make things rubber-band or show a hit that did not happen. It has no
effect on damage, hit registration, or network speed.

It also does not change animations. If an enemy finishes its swing before staggering, that is the
enemy's animation and not something this mod touches.

## Installing

With a mod manager, just install it. Manually, drop `SnappySync.dll` into `BepInEx/plugins`.

Client side only. Nothing is needed on the server and there is no version check, so you can install
it on your own and everyone else keeps playing exactly as before.

## Settings

Config file: `BepInEx/config/ithilias.snappysync.cfg`, created the first time you run the game. All
settings apply immediately, without a restart.

| Setting | Default | What it does |
| --- | --- | --- |
| Enabled | `true` | Master switch. Off is exactly vanilla |
| Render Rate Smoothing | `true` | Catch up once per rendered frame instead of 50 times a second. Costs a little CPU |
| Catch-up Rate | `25` | How fast things catch up, roughly `1/rate` seconds: 25 is about 40 ms, 11 is about vanilla. Higher is snappier but shows network jitter more |
| Snap Distance | `5` | Metres above which something is teleported instead of slid. The game already teleports at 5, so only values **below** 5 change anything |
| Log Stats | `false` | Writes a line every 30 seconds with how many position updates were smoothed or snapped, and your frame rate |

## Checking that it works

If `BepInEx/config/ithilias.snappysync.cfg` is missing after starting the game, the mod did not
start, whatever the log says about loading it.

The log should also say `Patched 1 position-smoothing call(s) in ClientSync` and `Patched 2
position-smoothing call(s) in SyncPosition`. Fewer means a game update changed something and that
part is running vanilla. The log says so when that happens.

## For the curious

The smoothing lives in the game's `ZSyncTransform`. Objects with a kinematic rigidbody are eased
inline and moved with `Rigidbody.MovePosition`, which belongs on the physics tick, so they stay on
it. Everything else, including any creature that is awake, is eased through `SyncPosition`, and
that is what moves to the render loop.

The vanilla catch-up looks like it runs once per frame, but that check is only an upper limit. It
is actually driven by the 50 Hz physics tick, so above 50 fps every player converges at the same
speed.

Rotation is left alone. It already catches up much faster than position does, so speeding up
position brings the two into line.

When you stand on a moving ship, other players' positions are eased relative to the ship. That path
gets the faster catch-up but never teleports, because vanilla never teleports there either.

## Source

Source code and build instructions: https://github.com/Ithilias/valheim-mods
