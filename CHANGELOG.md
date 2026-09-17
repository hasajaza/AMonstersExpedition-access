# Changelog

## 1.0.1

- **Two numbers no longer run together on a tree.** A tree read as "tree, log 3 1" — the log
  length and the tree's own number side by side with nothing to say which was which. The number
  now attaches to the noun it counts: "tree 1, log 3 long". Logs likewise read as
  "log 2, lying east to west, 3 long".
- **Sliders in whole steps read as steps**, e.g. "music volume, 10 of 15", because you change
  them one arrow press at a time and the step count tells you what a press will do. Continuous
  sliders still read as a percentage. `SliderAsPercent` forces percentages everywhere.

- **Jumping five tiles in review mode now works.** Holding the jump modifier stopped the arrow
  keys matching at all, because their binding has no modifiers and the match was exact, so the
  cursor sat still. The jump modifier is now ignored when testing those four keys.
- **The mod's own hint toggle is gone.** It did the same thing as the game's `H`, which the mod
  already announces, so two keys did one job. `B` still re-reads where the hints point.
- **`B` only reads hints that are actually showing.** Having the setting on is not the same as
  having the hints on screen: the game's hint key toggles them, and a sighted player with them
  hidden sees nothing. Reading them anyway gave away a puzzle you had chosen not to be shown.

- **Right-hand modifier keys now work.** `Shift+F1` did nothing with the right shift key, and
  the same went for right control with the direction cluster: a binding stores the LEFT
  modifier, and the mod was matching it exactly. Left and right of the same modifier are now
  treated as the same key, which they are to a person. A binding with no modifiers still does
  not fire while one is held, so `Ctrl+K` cannot also trigger plain `K`.
- **"Big step modifier" renamed to "jump five tiles"** and marked as review-mode only. The old
  name described the mechanism rather than what it does, and gave no hint that it only affects
  the review cursor. The key-list section is now "Review cursor (review mode only)".

- **Settings sliders now read their value**, as a percentage: "Music volume, 50 percent". They
  were silent because the game's sliders are `SliderWithLabelHighlight`, a subclass of
  `UnityEngine.UI.Slider`, and the mod tested for the exact type name. Sliders are now found by
  capability instead, so any subclass works.
- **A slider says its new value as you change it.** Moving a slider does not move focus, so the
  row used to be read once and then stay silent while you were adjusting it.
- **`F1` reads one key per press** instead of a whole section at a time, with `Shift+F1` to step
  back. A section is named only when it changes. Forty words in one utterance could not be
  acted on or returned to.

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
