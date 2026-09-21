# Changelog

## 1.1.0

- **The downloads are fixed.** The 1.0.0 and 1.0.1 zips were built with Windows PowerShell's
  `Compress-Archive`, which writes paths with backslashes and no folder entries. The zip format
  requires forward slashes, so viewers that follow it showed `BepInEx\plugins\AMEAccess.dll` as
  one oddly named file or not at all, and the mod-only zip appeared to hold nothing but the
  speech DLLs. The release script now writes zips through .NET directly, then reopens each one
  and refuses to finish unless every path uses forward slashes and every required file is in it.

- **Reset brings the review cursor back to you.** A reset puts your monster back where the island
  started, and a cursor left anywhere else was pointing at a place that no longer meant
  anything. Undo and redo do the same when `CursorFollowsPlayer` is on. An island reset plays a
  transition and only puts your monster back partway through it, so the cursor waits for you to
  actually move, for up to two seconds, rather than snapping on the event and catching your old
  position.

- **Not everything is an exhibit.** The game uses one piece type for exhibits, benches, huts,
  trophies, friends, the ferry and plain props, and the mod called all of them exhibits — so a
  post in the water and an empty patch of sea turned up in the exhibit count.

  An exhibit is now precisely a landmark with plaque text. The rest are named for what they do:
  friend, bench, coffee hut, popcorn hut, trophy, ferry. A prop that does nothing but is solid
  — the kind that stops a raft — is a "fixed object", named from what the game calls it. A prop
  that does nothing and blocks nothing is left out of the survey, like scenery.

  Walking into a prop no longer promises a plaque; it says it is in the way. `F8` now shows each
  landmark's kind, behaviour and prefab name, so a misclassified one is visible at once.

- **The cursor comes with you when you walk**, in arrow-cursor mode. It stayed where it was, so
  after a step every reading was relative to a spot you had walked away from. Set
  `CursorFollowsPlayer` to false to leave it behind, which is useful for keeping a spot marked
  while you walk towards it.

- **The rebinder no longer refuses the arrow keys for cursor directions.** It treats the game's
  keys as off limits, which is right in general but wrong for the arrows: they are the review
  cursor's own directions, and in arrow-cursor mode they belong to the mod outright. You could
  move a cursor direction off the arrows and then be unable to put it back.

- **An empty bookmark says how to set one.** The message still read "set it in review mode"
  after setting had moved to shift and a number.
- **The direction cluster follows the cursor in all three ways of looking around**, not only in
  review mode. Every place that asked "is review mode on?" now asks whether the cursor is the
  thing you are moving, which is the question that was actually meant.

- **`End` no longer drags you into review mode.** With the arrows already driving the cursor, or
  while the peek key is held, the cursor is live already — switching review mode on as well
  froze movement for no reason and left you in a mode you had not asked for. It now only does
  that when it is the only way to have a live cursor.
- **The column, route and inspect keys work in arrow-cursor mode.** They were only ever reached
  through the review-mode handler, so they were dead when the arrows drove the cursor without it.

- **`F3` switches what the arrow keys do**, so there is no config file to edit and no restart.
  It says which mode you landed in, and is saved straight away so it survives quitting. Any look
  in progress is ended first, so the two ways of moving the cursor can never be half-on at once.

- **Bookmarks work however you are looking around.** A number key set a bookmark in review mode
  and recalled one otherwise, which broke as soon as there was more than one way to move the
  cursor: holding the peek key and arrows-always-review both move it without review mode being
  on, so a number could only ever recall and there was no way left to set one.

  A modifier now says which action you mean: `1` to `9` walks you to a bookmark, `Shift` and a
  number sets one at the cursor, `Ctrl` and a number clears it. The mode no longer matters.

- **New setting: `ArrowsAlwaysReview`.** With it on, the arrow keys drive the review cursor
  permanently and you walk with W A S D, so there are no modes to switch between at all. Off by
  default, and `V` still works either way, so there is always a way back if it misbehaves.

  W A S D movement stays the game's own code. The arrows could not simply be reassigned,
  because the game reads movement from Rewired's axes and both the arrows and W A S D feed
  them — by the time the game sees a value, which key produced it is gone. So a single Harmony
  patch vetoes the game's input method on frames where an arrow is down and no W A S D key is.
  Every other frame runs untouched: input buffering, sitting, the undo hold and the first-move
  case are all still the game's, not reimplemented.

  This is the mod's only Harmony patch. Everything else reads state or subscribes to the game's
  own events.

- **Hold `Alt` and use the arrow keys to look around**, without entering review mode. Movement
  is frozen only while the key is held, so letting go gives the arrows straight back to walking.
  Toggling review mode cost two presses around every glance, and glancing is the commonest thing
  you do.

  While held, `C` reads the column, `M` gives walking directions and `Enter` inspects, the same
  as in review mode. Each look starts from your monster rather than wherever the cursor was
  left, so a glance is always about where you are.

  `V` still toggles review mode for a longer look; nothing about it has changed.

  The arrow keys could not simply be reassigned: the game reads movement from Rewired's axes,
  which both the arrows and W A S D feed, so holding a modifier does not stop the game seeing an
  arrow press. Switching movement off for exactly as long as the key is held is what makes this
  work.

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
