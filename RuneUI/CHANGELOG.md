# Changelog

## 0.10.1
- The inventory is no longer resized a second time right after you spawn.

## 0.10.0
- Works with Better Archery's quiver: Rune UI no longer puts items into the two rows it keeps below
  your inventory, and keeps them when it adds inventory rows or takes over from Equipment and Quick
  Slots.

## 0.9.0
- New Paperdoll setting: the gear slots sit on a body outline, head at the top and legs at the
  bottom, instead of in a row.

## 0.8.0
- Move the quick and gear slots in the inventory: hold Left Alt and drag them. Their position is also
  in the settings, separately for the quick slots with a chest open.

## 0.7.0
- New balance settings, all off by default: wear up to three utility items at once, add up to five
  inventory rows, and change your base carry weight.

## 0.6.0
- New death settings: keep your gear or quick slot items when you die, and choose whether armour and
  weapons are put back on when you pick up your tombstone. Keeping items is off by default.

## 0.5.0
- Switching from Equipment and Quick Slots: the items in its quick and equipment slots move into Rune
  UI's slots the first time you log in, instead of being dropped where you spawn.
- New console commands: `runeui_slots` lists the slots, `runeui_emptyslots` moves everything out of
  them, and the cheat `runeui_eaqs_restore` brings back Equipment and Quick Slots' backup.

## 0.4.0
- The food slots are now quick slots: they take any item but ammo, and their key uses it. Food is
  eaten, meads are drunk, and weapons and tools are equipped. Food already in them stays.
- Up to six quick slots, with a key and a label of your choice for each.
- While a quick slot key is used, vanilla actions on the same key no longer trigger, so V eats
  instead of also toggling auto pickup.
- Workbenches repair and upgrade items in the quick slots too.

## 0.3.1
- Gear in the gear slots now counts for other mods that look at what you wear, such as Epic Loot's
  magic effects and set bonuses.

## 0.3.0
- New gear slots right of the inventory: head, chest, legs, cape, utility and trinket. Armour you
  wear sits there instead of taking up inventory space. Workbenches repair and upgrade it there too.
  Empty slots show a faint icon of what goes there.
- While you hold an item, the food and gear slots it fits in light up.
- After dying, food goes back into the food slots and your gear is put on again when you pick up your
  tombstone.
- The second food slot is now eaten with V instead of U, so the three keys sit closer together. V also
  toggles auto pickup in vanilla, so rebind that in the game's controls.
- If your inventory is full when you die, the food slots now go into the tombstone instead of onto the
  ground.

## 0.2.1
- The hotbar no longer disappears after you die. All eight slots now always show, empty ones included, like the quick bar. Turn this off with Show empty hotbar slots.

## 0.2.0
- New setting Hotbar scale makes the hotbar and quick bar smaller or larger without changing the rest of the HUD. They stay centred on the hotbar anchor.

## 0.1.1
- Food in the food slots can be picked up with a left click again when no chest is open.

## 0.1.0
- First release. A new HUD: health, stamina and eitr as themed bars at the bottom centre with the
  hotbar under them, your forsaken power as a slot next to the hotbar, a second quick bar for
  inventory row 2 on Alt+1 to 8, three food slots eaten with Z, U and B, food and status effects
  together in the bottom left, quality rings around upgradable items, skill progress toasts, and a
  party list with the health of nearby players.
