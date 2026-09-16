# Keyboard reference

All mod keys can be changed:

- **in game with `F2`**: step through the actions with `]` and `[`, press Enter on the one you
  want, then press the new key. `Backspace` puts it back to its default, `Escape` finishes.
- **in `BepInEx\config\hassan.ameaccess.cfg`**, sections `[Keys]`, `[Direction cluster]` and
  `[Review cursor]`.

Press `F1` in game to hear the whole list. It is read from your live bindings, so it stays
correct after you change anything.

None of these clash with the game's own keys, which are `W` `A` `S` `D` for movement,
`Left Shift` look around, `Z` undo, `R` reset, `Space` select, `Esc` menu, `O` zoom in,
`P` zoom out, `G` toggle grid and `H` show hint. The rebinder refuses those.

## Keys

| Key | Action | What it does |
|---|---|---|
| `K` | Where am I | Speak your position and what you are standing on. |
| `L` | Look Around | Speak the four neighbouring tiles and what walking into each would do. |
| `I` | Island Info | Speak the current island's name and whether you have visited it before. |
| `E` | Facing | Describe the thing you are facing, and read its plaque if it has one. |
| `J` | Hint | Toggle the game's own island hints. Only works when 'Enable Island Hints' is turned on in the game's settings, exactly like the in-game hint button. |
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
| `F1` | Help | Speak the mod's key list, one section per press. Also writes the whole list to the BepInEx log. |
| `F9` | Plaque Repeat | Read the last exhibit plaque again, in full. |
| `Ctrl+F9` | Plaque Previous | Step back through earlier plaques read this session. |
| `Y` | Stats | Speak overall progress: islands visited, exhibits discovered, time played. |
| `F10` | Read Panel | Read everything in the panel around the focused control - on a save slot that is the play time, date, islands visited and exhibits found. |
| `F11` | Read Focus | Re-read the menu item that currently has focus. |
| `F2` | Change Keys | Change the mod's keys from inside the game, without editing this file. |
| `F8` | Dump Pieces | Write every piece near you to the BepInEx log, with the reason each was kept or dropped from the survey. Use this when something on screen is not listed. |
| `F12` | Speech Test | Re-run the speech self test and write the result to BepInEx\LogOutput.log. Works anywhere, including menus. Use this when the mod is silent. |

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

## Review cursor

| Key | Action | What it does |
|---|---|---|
| `V` | Review Toggle | Turn review mode on or off. While it is on your monster cannot move, so the arrow keys drive the review cursor instead. Press again to go back to playing. |
| `Up` | North | Cursor north (review mode). |
| `Down` | South | Cursor south (review mode). |
| `Right` | East | Cursor east (review mode). |
| `Left` | West | Cursor west (review mode). |
| `Backspace` | Home | Snap the cursor back to your monster. |
| `Home` | Home Alt | Second key for snapping the cursor back to your monster. |
| `End` | Jump To Item | Jump the review cursor to the object you have selected with the category and item keys. Turns review mode on if it is not already. |
| `C` | Column | Read everything stacked in the cursor's column, with heights. |
| `M` | Route | Say whether you could walk to the cursor from here, and in how many steps. This mirrors the path preview the game already draws for mouse players. |
| `Right Shift` | Big Step Modifier | Hold with a cursor direction to move several tiles at once. See CursorBigStep. |

## The direction cluster

Nine keys in the shape of the tiles they describe, all held with `Ctrl`:

```
Ctrl+U   Ctrl+I   Ctrl+O          north-west   north   north-east
Ctrl+J   Ctrl+K   Ctrl+L     =        west         here    east
Ctrl+M   Ctrl+,   Ctrl+.          south-west   south   south-east
```

In review mode the cluster reads around the cursor instead of around your monster.

## Bookmarks

`1` to `9` set a bookmark at the review cursor, and give walking directions back to it while
playing. `Ctrl` plus a number clears one. `'` lists them.
