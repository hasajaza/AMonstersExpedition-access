# Keyboard reference

Press `F1` in game to hear these, **one key per press**. `Shift+F1` steps back. Either shift
key works, and so does either control or alt key.

`F2` changes any key without leaving the game.

## Three ways to look around

| | |
|---|---|
| **Hold `Alt`** and use the arrow keys | a quick glance. Movement is frozen only while you hold it |
| **Press `V`** | review mode for a longer look. Press again to play |
| **Press `F3`** | give the arrow keys to the cursor permanently, and walk with W A S D |

`F3` switches the last one on and off and says which you got, so there is no config file to
edit and no need to restart. It is saved straight away and survives quitting.

Holding `Alt` is the one to reach for most of the time: one press, nothing to leave, and the
arrow keys walk again the instant you let go. While held, `C` reads the column, `M` gives
walking directions and `Enter` inspects.

With `F3` on, the arrow keys no longer move your monster, which is what they do for every
other player, so guides and videos will not match. `V` still works, so you are never stuck.

The cursor comes with you as you walk, so it is always reading from where you are standing. Set
`CursorFollowsPlayer` to false in the config to leave it where you put it, which is useful for
keeping a spot marked while you walk towards it.

## The game's own keys

These belong to the game, not the mod, and the mod never takes them:

| Key | Does |
|---|---|
| `W` `A` `S` `D` and arrows | Move |
| `Left Shift` | Look around |
| `Z` | Undo |
| `R` | Reset island |
| `Space` | Select |
| `Esc` | Menu |
| `O` / `P` | Zoom in / out |
| `G` | Toggle grid, which also turns spoken coordinates on and off |
| `H` | Show or hide hints. The mod says which you got, and reads where they point |

## Keys

| Key | Action | What it does |
|---|---|---|
| `K` | Where am I | Speak your position and what you are standing on. |
| `L` | Look Around | Speak the four neighbouring tiles and what walking into each would do. |
| `I` | Island Info | Speak the current island's name and whether you have visited it before. |
| `E` | Facing | Describe the thing you are facing, and read its plaque if it has one. |
| `Q` | Repeat | Repeat the last thing spoken. |
| `Ctrl` | Silence | Stop speech immediately. Left Control on its own, which also means every Ctrl+letter in the direction cluster cuts off whatever is speaking before it reads the new tile - the same way a screen reader behaves. |
| `U` | Status | Speak move count and whether undo or reset is available. |
| `F` | Scan | Look into the distance in all four directions and report what is there. |
| `T` | Survey | Survey the whole island: counts of everything, then the nearest items. |
| `]` | Next Object | Step to the next nearby object and say where it is. |
| `[` | Prev Object | Step to the previous nearby object. |
| `Page Down` | Next Category | Move to the next category of things on the island - trees, logs, rocks and so on. |
| `Page Up` | Prev Category | Move to the previous category. |
| `Ctrl+Page Down` | Next Item | Next item inside the current category. Same as the right bracket key. |
| `Ctrl+Page Up` | Prev Item | Previous item inside the current category. Same as the left bracket key. |
| `X` | Island Only Toggle | Switch between listing only what is on the island you are standing on, and everything in sight including logs and rafts in the water around it. |
| `Enter` | Inspect | Full detail on the object you last stepped to, or the one you are facing. |
| `\` | Next Island | Where to go next: the nearest island you have not visited, how far it is, and whether you can already walk there. |
| `'` | List Bookmarks | List the bookmarks you have set. |
| `N` | Islands Nearby | List other islands in range, with bearing and whether you can walk there now. |
| `B` | Read Hints | Read where the island hints point. Only works when 'Enable Island Hints' is on in the game's settings, because that is when a sighted player sees them too. |
| `Shift+F1` | Help Previous | Go back one key in the key list. Either shift key works. |
| `F1` | Help | Read the next key in the mod's key list, one key per press. Also writes the whole list to the BepInEx log. |
| `F9` | Plaque Repeat | Read the last exhibit plaque again, in full. |
| `Ctrl+F9` | Plaque Previous | Step back through earlier plaques read this session. |
| `Y` | Stats | Speak overall progress: islands visited, exhibits discovered, time played. |
| `F10` | Read Panel | Read everything in the panel around the focused control - on a save slot that is the play time, date, islands visited and exhibits found. |
| `F11` | Read Focus | Re-read the menu item that currently has focus. |
| `F3` | Arrow Mode Toggle | Switch between the arrow keys moving your monster and the arrow keys moving the review cursor. Saved straight away, so it survives quitting. Same setting as ArrowsAlwaysReview, without opening this file. |
| `F2` | Change Keys | Change the mod's keys from inside the game, without editing this file. |
| `F8` | Dump Pieces | Write every piece near you to the BepInEx log, with the reason each was kept or dropped from the survey. Use this when something on screen is not listed. |
| `F12` | Speech Test | Re-run the speech self test and write the result to BepInEx\\LogOutput.log. Works anywhere, including menus. Use this when the mod is silent. |

## Direction cluster

| Key | Action | What it does |
|---|---|---|
| `Ctrl+U` | North West | Read the tile to the north west. |
| `Ctrl+I` | North | Read the tile to the north. |
| `Ctrl+O` | North East | Read the tile to the north east. |
| `Ctrl+J` | West | Read the tile to the west. |
| `Ctrl+K` | Here | Read where the cluster is centred: your monster, or the cursor in review mode. |
| `Ctrl+L` | East | Read the tile to the east. |
| `Ctrl+M` | South West | Read the tile to the south west. |
| `Ctrl+,` | South | Read the tile to the south. |
| `Ctrl+.` | South East | Read the tile to the south east. |

## Review cursor (review mode only)

| Key | Action | What it does |
|---|---|---|
| `V` | Review Toggle | Turn review mode on or off. While it is on your monster cannot move, so the arrow keys drive the review cursor instead. Press again to go back to playing. |
| `Up` | North | Cursor north (review mode). |
| `Down` | South | Cursor south (review mode). |
| `Right` | East | Cursor east (review mode). |
| `Left` | West | Cursor west (review mode). |
| `Backspace` | Home | Snap the cursor back to your monster. |
| `Home` | Home Alt | Second key for snapping the cursor back to your monster. |
| `Alt` | Peek Hold | HOLD this and use the arrow keys to move the review cursor WITHOUT entering review mode. Movement is frozen only while it is held, so let go and the arrow keys walk again. Either alt key works. This is the quick way to look around; the review toggle is for a longer look. |
| `End` | Jump To Item | Jump the review cursor to the object you have selected with the category and item keys. Turns review mode on if it is not already. |
| `C` | Column | Read everything stacked in the cursor's column, with heights. |
| `M` | Route | Say whether you could walk to the cursor from here, and in how many steps. This mirrors the path preview the game already draws for mouse players. |
| `Right Shift` | Jump Five Tiles | REVIEW MODE ONLY. Hold this with a cursor arrow key to move the review cursor five tiles at a time instead of one. It does nothing on its own. How far it jumps is set by CursorBigStep. |

## The direction cluster

Nine keys in the shape of the tiles they describe, held with `Ctrl`:

```
Ctrl+U   Ctrl+I   Ctrl+O          north-west   north   north-east
Ctrl+J   Ctrl+K   Ctrl+L     =        west         here    east
Ctrl+M   Ctrl+,   Ctrl+.          south-west   south   south-east
```

These read the eight tiles touching you, without moving anything.

## Bookmarks

Nine places you can mark and walk back to. It does not matter how you are looking around —
a modifier says which action you mean:

| Key | Does |
|---|---|
| `1` to `9` | Walking directions to that bookmark |
| `Shift` + `1` to `9` | Set that bookmark at the review cursor |
| `Ctrl` + `1` to `9` | Clear it |

Each bookmark remembers what was there when you set it. `'` lists the ones you have set.
