# Native speech files

Nothing is committed to this folder, because these are third-party binaries. Put them here and
the build copies them next to the game's `.exe` for you.

## UniversalSpeech.dll

The **32-bit** build. A Monster's Expedition is a 32-bit process, so the 64-bit build used for
64-bit games cannot load.

Source and binaries: https://github.com/qtnc/UniversalSpeech — the repository's `bin` folder
holds the prebuilt Windows DLL. No changes are needed: unlike 64-bit games, which require a
custom x64 build, the standard build is exactly what this game needs.

MIT licence, © Quentin Cosendey.

## nvdaControllerClient32.dll

From NV Access's NVDA Controller Client package, for NVDA users. LGPL 2.1.

**The filename matters.** Universal Speech loads `nvdaControllerClient32.dll` by that exact
name, relative to its own folder. A copy called `nvdaControllerClient.dll`, without the `32`,
will not be found by it — though the mod's own direct NVDA fallback tries both names.

## JAWS

Nothing to install. The game is a 32-bit process, so the JAWS API is callable in-process and no
bridge helper is required.
