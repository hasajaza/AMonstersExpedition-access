# Test plan

Do every test **with the monitor off, or without looking**, using only the keyboard and the
screen reader.

Before starting, set `LogSpeech = true` in `BepInEx\config\hassan.ameaccess.cfg`. Afterwards,
send `BepInEx\LogOutput.log` with a note of any step that failed.

A step fails if you hear nothing, the wrong thing, the same thing twice, or if you would need
the mouse.

## 1. Starting up

1. Start the game. You hear "Accessibility mod ready. Press F1 for the key list."
2. `F1` reads the key list a part at a time and wraps round at the end.
3. Arrow through the title screen. Each item is named, ending in "button".
4. Settings: each row reads label then value, e.g. "Language: English US", "Show Grid, checked".
5. Controls: each row reads the action and its key, e.g. "Undo: Z, button".
6. Load screen: arriving on a save reads its play time, date, islands and exhibits. Moving
   between Delete, Reset and Continue does **not** repeat that summary.

## 2. On an island

1. `K` gives position, facing and what you are standing on.
2. `L` names all four neighbours; a tree says "chop", a log says roll, slide or step up.
3. `F` scans into the distance and ends each direction at water.
4. `T` gives counts only, with no bearings.
5. `Page Down` reaches each category; `]` steps through its items and the count matches `T`.
6. `X` limits everything to this island, and `T` says "This island only".
7. Walking into a wall says what blocked you, not just "blocked".
8. Two trees on one island are "tree 1" and "tree 2", and the numbers do not change as you walk.

## 3. Review mode

1. `V` freezes the monster and says so.
2. Arrow keys move the cursor; each tile reads with its position relative to you.
3. `Backspace` returns the cursor to you.
4. `M` gives turn-by-turn directions that actually walk when you follow them.
5. `V` again returns control; the monster moves normally.

## 4. Puzzles

1. Chop a tree; you hear "Tree chopped" and a log appears with the length the tree promised.
2. Roll a log into water; you hear the splash.
3. `\` names the nearest unvisited island, its shore, and the water gap as a log length.
4. Cross the bridge; the new island is announced.

## 5. Exhibits

1. Walk into an exhibit; the plaque is read in full.
2. `F9` reads it again; `Ctrl+F9` steps back through earlier ones.
3. The plaques appear in `BepInEx\LogOutput.log`.
4. `T` finds the exhibit on an island that has one.

## 6. The warp map

1. Walk into a postbox; the map opens and says how many destinations there are.
2. `]` and `[` step through them, each with distance, direction and visited state.
3. `Enter` travels to the one you are on.
4. `T` lists them all.

## 7. Keys

1. `F2` enters rebinding and steps through the actions with `]` and `[`.
2. Enter, then a new key, changes it and says so.
3. Trying a key the game uses is refused with the reason.
4. Trying a key another action uses is refused and names that action.
5. `Backspace` restores the default. The change survives restarting the game.
