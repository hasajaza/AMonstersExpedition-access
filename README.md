# A Monster's Expedition Access

A screen-reader accessibility mod for **A Monster's Expedition**. It is built for keyboard and
screen reader use, with no mouse needed.

It is a BepInEx 5 plugin for the Mono, 32-bit build of the game. All speech goes through
**Universal Speech**, which talks to JAWS, NVDA or other supported screen readers, and falls
back to Windows SAPI when no screen reader is running.

The mod reads the game's **internal state**. There is no OCR and no image recognition anywhere
in it: positions, terrain, logs, exhibits and menus all come from the game's own data.

## What the game is

You are a monster touring human exhibits across hundreds of small islands. There is no timer,
no enemies and no way to lose. Every island is a small puzzle about crossing water, and there
are only two actions, both of which are just walking into things:

- **Walk into a tree** and you chop it down; it becomes a log.
- **Walk into a lying log along its length** and you climb onto it. Logs are paths as well as
  obstacles.
- **Walk into a lying log from the side** and it rolls one tile sideways.
- **A log that ends up in water becomes a bridge.** A three-tile log spans three tiles of water.

## What you get

- **Position and terrain.** Where you are, what you are standing on, all four neighbours, and a
  distance scan along each direction.
- **A survey of the island.** Counts of everything, then a category and item cursor to step
  through each object at your own pace.
- **A review cursor.** Move a cursor anywhere on the map without moving your monster, and get
  turn-by-turn walking directions to it.
- **Nine bookmarks.** Mark a spot, come back to it later from anywhere.
- **Every exhibit read aloud.** All 167 museum plaques, with a session transcript you can
  re-read.
- **The warp map.** Postbox destinations with distance, direction, visited state and whether
  they are on the game's main route.
- **Menus.** Title screen, settings, save slots and key bindings.

## Requirements

1. A Monster's Expedition, Windows. The mod was built against the Steam build; the GOG build
   ships a byte-identical `Assembly-CSharp.dll` and works the same way.
2. **BepInEx 5.4.x, win_x86** installed in the game folder, and the game started once so that
   `BepInEx\core` and `BepInEx\plugins` exist. The game is a **32-bit Mono** build, so the x64
   build of BepInEx and BepInEx 6 will not work.
3. **Visual Studio 2022** with the ".NET desktop development" workload, if you are building from
   source.
4. A screen reader, or the Windows voice.

The mod has no NuGet packages and no Harmony patches. Every reference comes from your game
folder.

## Building from source

Open `AMEAccess.sln` and build. The project finds the game automatically in the usual Steam and
GOG locations; if it cannot, it says so and you put your folder in `AMEAccess\GamePath.props`.

After a successful build, `AMEAccess.dll` is copied into `BepInEx\plugins`, and anything in
`native\` is copied next to the game's `.exe`. Set `DeployToGame` to `false` in `GamePath.props`
to turn the copying off. `scripts\make-release.ps1` builds the player packages.

## Speech setup

Both files go **next to the game's .exe**, not in `plugins`:

- `UniversalSpeech.dll` — the **32-bit** build, from https://github.com/qtnc/UniversalSpeech
  (its `bin` folder). The 64-bit build used for 64-bit games will not load here.
- `nvdaControllerClient32.dll` — for NVDA users. Universal Speech looks for that exact name, so
  a copy called `nvdaControllerClient.dll` without the `32` will not be found by it. The mod
  itself tries both names for its own direct NVDA fallback.

Because the game is a 32-bit process, the JAWS API can be called in-process. No bridge helper
is needed, unlike 64-bit games.

If the mod is silent, press **`F12`**. It speaks a test line through every route separately and
logs what each returned, then set `SpeechEngine` in the config to whichever you heard. `sapi`
is the reliable escape hatch, since it bypasses the screen reader entirely.

## Keyboard

The full reference is in [`docs/KEYBOARD.md`](docs/KEYBOARD.md), and **`F1`** reads it aloud in
game. **`F2`** changes any key without leaving the game.

| Key | Action |
|---|---|
| Arrow keys | Move (the game's own keys) |
| `K` | Position, facing, what you are standing on |
| `L` | All four neighbours and what walking each way would do |
| `F` | Distance scan in all four directions |
| `T` | Survey the island: counts |
| `Page Down` / `Page Up` | Category |
| `Ctrl+Page Down` / `Ctrl+Page Up`, or `]` / `[` | Item in that category |
| `Enter` | Full detail on it |
| `V` | Review mode: arrow keys move a cursor, your monster stays put |
| `M` (review mode) | Turn-by-turn directions to the cursor |
| `\` | Nearest island you have not visited |
| `1`–`9` | Set a bookmark (review mode) or walk back to it |
| `Ctrl` + `U I O / J K L / M , .` | Read the eight surrounding tiles |

## Publishing

[`BUILD-AND-PUBLISH.md`](BUILD-AND-PUBLISH.md) is the step-by-step command list: build, package
the player downloads, create the GitHub repository and tag a release.

`scripts\make-release.ps1 -UseInstalledBepInEx` packages the BepInEx already installed in your
game folder, so a release ships the exact version you tested with. Only the loader is taken;
your config, your logs and any other plugins stay out.

## Documentation

- [`docs/PLAYER-INSTALL.md`](docs/PLAYER-INSTALL.md) — installing, for players
- [`docs/KEYBOARD.md`](docs/KEYBOARD.md) — every key
- [`docs/TROUBLESHOOTING.md`](docs/TROUBLESHOOTING.md) — when something does not work
- [`docs/TECHNICAL_NOTES.md`](docs/TECHNICAL_NOTES.md) — how the game was analysed and how the mod hooks in
- [`docs/TESTING.md`](docs/TESTING.md) — the test plan
- [`BUILD-AND-PUBLISH.md`](BUILD-AND-PUBLISH.md) — build, package and publish commands

## Licence

MIT, see [`LICENSE`](LICENSE). Third-party components are listed in
[`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md).

This is an unofficial fan-made accessibility mod. It is not affiliated with or endorsed by
Draknek and Friends.
