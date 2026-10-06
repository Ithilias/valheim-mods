# Changelog

## 1.1.3
- New icon. No changes to the mod itself.

## 1.1.2
- Fixed the version the mod reports about itself, which still said 1.0.0 no matter what the package
  version was. Mod managers and anything else that reads the plugin version now see the correct one.
  The version is now taken straight from the package manifest at build time, so the two cannot
  disagree again. No other changes.

## 1.1.1
- Rewrote the description and the mod page so they explain what the mod does for you, rather than how
  it works internally. Added a plain explanation of which buffs are kept and which are deliberately
  not. No changes to the mod itself.

## 1.1.0
- Settings reset to defaults on this update, because the config file is named after the plugin id
  and that id changed. Your old file is still in `BepInEx/config` under its previous name. Rename
  it to `ithilias.statuskeeper.cfg` before launching to keep your settings.
- Saved effects in your character file moved to a new key for the same reason. Entries from earlier
  builds are cleaned up automatically on the next save, so nothing is left behind; one logout's
  worth of buffs may not restore the first time you load after updating.

## 1.0.0
- Initial release: carries timed status effects such as Rested across a logout.
