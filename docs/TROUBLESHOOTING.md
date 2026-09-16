# Troubleshooting

The log is `<game folder>\BepInEx\LogOutput.log`. Turn on `LogSpeech` in
`BepInEx\config\hassan.ameaccess.cfg` for a line per utterance.

## The mod says nothing at all

Press **`F12`**. It speaks a test line through every route separately and writes what each one
returned:

- `speechSay` returning **1** means an engine accepted the text; **0** means no engine was found.
- Returning 1 and still hearing nothing almost always means **JAWS sleep mode** for the Unity
  Player window. JAWS accepts speech and stays silent.

Whichever test line you actually hear, set `SpeechEngine` in the config to `jaws`, `nvda` or
`sapi`. `sapi` bypasses the screen reader entirely and works even with one asleep.

Other causes, in order:

- `UniversalSpeech.dll` is the **64-bit** build. The game is 32-bit; it will not load.
- `nvdaControllerClient32.dll` is missing or named `nvdaControllerClient.dll`. Universal Speech
  looks for the name with the `32`.
- The speech DLLs are in `BepInEx\plugins`. They belong next to the game's `.exe`.

## Something on screen is not in the survey

Stand next to it and press **`F8`**. Every piece within twenty-five tiles goes to the log with
its type, position, which island the game thinks it belongs to, whether it is hidden or fogged,
and **which filter dropped it**.

This cannot be worked out from the game files, because it depends on that object's live state.
The log answers it directly.

## A menu item reads oddly

Press **`F11`** on it. The log records the focused object, its components, every sibling with
its text and visibility, and which label candidates were considered.

## A key does nothing

- Press `F1` and listen for that action's current key. The list is read from your live bindings,
  so it is always right.
- The mod avoids every key the game uses. If you rebound something in the game's Controls
  screen, it may now clash; `F2` moves the mod's key out of the way.
- After an update the mod may reset its keys to new defaults. It says how many changed at
  startup and writes a line to the log.

## The build cannot find the game

Open `AMEAccess\GamePath.props` and put the folder containing the game's `.exe` between the
`AmeDir` tags. If the build instead reports BepInEx missing, install BepInEx 5.4.x win_x86 and
start the game once so that `BepInEx\core` exists.

## The game updated and the mod broke

Send `BepInEx\LogOutput.log` and the new `Assembly-CSharp.dll` from `<game>\*_Data\Managed\`.
The mod reads the game's own classes by name, so a rename in a game update is the usual cause.
