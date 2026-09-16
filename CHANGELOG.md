# Changelog

## 1.0.0
First release.

- **Position and terrain**: position, facing, what you are standing on, all four neighbours with
  what walking each way would do, and a distance scan out to twenty tiles.
- **Island survey**: one line of counts, then a category cursor (`Page Down` / `Page Up`) and an
  item cursor (`Ctrl+Page Down` / `Ctrl+Page Up` or the bracket keys) to step through objects at
  your own pace. `X` limits everything to the island you are standing on.
- **Direction cluster**: nine keys laid out like the tiles they describe, held with `Ctrl`.
- **Review cursor** (`V`): move a cursor anywhere without moving your monster; `M` gives
  turn-by-turn walking directions taken from the game's own pathfinder.
- **Bookmarks**: nine slots, set at the cursor and recalled as walking directions.
- **Exhibits**: all 167 plaques read aloud, with `F9` to re-read the last and `Ctrl+F9` to step
  back through the session. Every plaque is written to the log.
- **Trees report the log they will become**, taken from the tree's own prefab length.
- **Push, roll and knock over** are named separately, because each sends the log somewhere
  different.
- **Blocked moves say what blocked them**, including the log's "wrong side" case.
- **Warp map**: postbox destinations with distance, direction, visited state, the game's
  suggestion and whether each is on the critical path.
- **Menus**: title screen, settings, key bindings and save slots, with save details read as you
  arrive on a slot.
- **Progress**: islands visited, exhibits discovered and time played.
- **Hints**: the game's own island hints, read where they point, behind the game's own setting.
- **Speech**: Universal Speech with JAWS, NVDA and SAPI routes, a per-route self test (`F12`) and
  a `SpeechEngine` setting to force one.
- **Keys**: `F1` reads the full list, generated from the live bindings; `F2` changes any key in
  game. Nothing clashes with the game's own keys.
