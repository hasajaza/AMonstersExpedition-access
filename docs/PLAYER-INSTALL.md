# Installing A Monster's Expedition Access (players)

**Requirements**
- A Monster's Expedition on Windows (Steam or GOG).
- A screen reader (JAWS or NVDA), or the Windows voice.

## The complete zip (easiest)

1. Close the game. Extract the whole zip to any folder, for example Downloads.
2. Open the extracted folder and run `Install.cmd` (select it and press Enter).
   It finds your game folder and copies everything in. If it cannot find the game, it asks you
   to paste the folder.
3. Start the game. You should hear: "Accessibility mod ready. Press F1 for the key list."

To install by hand instead, copy everything inside the `files` folder into the game folder (the
one containing the `.exe`) and let Windows replace files.

## The mod-only zip

For players who already have **BepInEx 5.4.x win_x86** installed and have started the game once.

Copy into the game folder:

- `BepInEx\plugins\AMEAccess.dll`
- `UniversalSpeech.dll` next to the game's `.exe`
- `nvdaControllerClient32.dll` next to the game's `.exe`, if you use NVDA

## Which BepInEx

**BepInEx 5.4.x, win_x86.** The game is a 32-bit Mono build, so the x64 build will not load and
BepInEx 6 is for IL2CPP games and does not apply.

You can tell the mod is loaded even without speech: `BepInEx\LogOutput.log` will contain
"A Monster's Expedition Accessibility ... loaded".

## Screen readers

- **JAWS** works in-process; no helper program is needed, because the game is 32-bit.
  If JAWS is silent inside the game, open JAWS Settings Center, choose Unity Player, go to
  Miscellaneous and set Sleep Mode to disabled.
- **NVDA** needs `nvdaControllerClient32.dll` beside `UniversalSpeech.dll`. The name matters:
  Universal Speech looks for that exact filename.
- **No screen reader**: Windows SAPI is used automatically.

If you hear nothing, press `F12` in game and read `BepInEx\LogOutput.log`. It says which engines
were found and what each speech route returned.

## Uninstalling

Delete `BepInEx\plugins\AMEAccess.dll`. To remove everything, delete the `BepInEx` folder,
`winhttp.dll`, `doorstop_config.ini`, `UniversalSpeech.dll` and `nvdaControllerClient32.dll`
from the game folder. No game file is ever modified, so nothing else needs undoing.
