A screen-reader accessibility mod for A Monster's Expedition. Keyboard only, no mouse needed.
Works with JAWS and NVDA, and falls back to the Windows voice if no screen reader is running.

## Install

Download **AMEAccess-1.1.0-complete.zip** below. It contains everything, including BepInEx.

1. Close the game. Extract the whole zip to any folder, for example Downloads.
2. Open the extracted folder and run **Install.cmd** (select it and press Enter).
   It finds your game folder and copies everything in. If it cannot find the game, it asks you
   to paste the folder.
3. Start the game. You should hear: "Accessibility mod ready. Press F1 for the key list."

Press **F1** in game to hear the keys, one per press. Press **F2** to change any of them.

If you already have BepInEx 5 win_x86 installed, use **AMEAccess-1.1.0-mod-only.zip** instead
and extract it into the game folder, the one containing the game's .exe.

JAWS users: if JAWS is silent inside the game, open JAWS Settings Center, choose Unity Player,
go to Miscellaneous and set Sleep Mode to disabled.

If the mod is silent, press **F12** in game and send BepInEx\LogOutput.log.

## Important if you downloaded 1.0.0 or 1.0.1

Those zips were packaged wrongly: the mod itself was stored in a way many zip tools could not
see, so the download looked as if it held only the speech files. Please download 1.1.0.

## What's new in 1.1.0

- The downloads are fixed, and every zip is now checked before it is published
- Three ways to look around: hold **Alt** with the arrows for a quick glance, **V** for review
  mode, or **F3** to give the arrows to the cursor permanently and walk with W A S D
- Bookmarks work however you are looking: **Shift** and a number sets one, a number walks you
  there, **Ctrl** and a number clears it
- The cursor follows you when you walk, reset or undo
- Exhibits are only called exhibits: benches, huts, friends and props are named for what they are
- Settings sliders read their value, and speak it again as you change it
- F1 reads one key per press, Shift+F1 steps back
- Right-hand Ctrl and Shift keys work everywhere
- Trees and logs no longer run two numbers together: "tree 1, log 3 long"

Full details in CHANGELOG.md. Unofficial fan-made mod, not affiliated with Draknek and Friends.
