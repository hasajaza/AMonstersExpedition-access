# Technical notes

## 1. How the game was analysed

The supplied files were the only source of truth.

| File | Use |
|---|---|
| `Assembly-CSharp.dll` | all gameplay classes; disassembled to IL |
| `UnityPlayer.dll` | Unity version and process architecture |
| `globalgamemanagers.assets` | Unity version confirmation |
| `level0` | scene contents, to find which GameObjects carry which components |
| `sharedassets0.assets` | localisation, exhibit descriptions, island hints, biomes |
| `resources.assets` | fonts and sprites; nothing gameplay-relevant |
| `UnityEngine.CoreModule.dll`, `UnityEngine.UIModule.dll`, `UnityEngine.InputLegacyModule.dll` | resolving which assembly provides which type |

No decompiler was available that could resolve the Unity references, so an ECMA-335 IL
disassembler and a Unity `.assets` parser were written for the job. Every claim below was read
out of the binaries rather than assumed.

## 2. The environment

| | |
|---|---|
| Unity | 2020.3.8f1 (rev `507919d4fff5`) |
| Scripting backend | Mono |
| Architecture | **x86 (32-bit)**, PE machine `0x014c` |
| Loader | BepInEx 5.4.x, win_x86 |
| Game files modified | none |

The Steam and GOG builds ship a **byte-identical** `Assembly-CSharp.dll` (MD5
`ca644d28bb4ad2b5a31c7a55cf3c34c7`), so the mod is the same for both.

Being a 32-bit process means the JAWS API can be called in-process, so no external bridge
helper is needed.

## 3. The game's model

The game is a discrete, deterministic puzzle game with a real internal state model, so almost
nothing has to be inferred from rendering.

- **`Vector3i`** — integer coordinates. **X and Z are the horizontal plane; Y is height.** The
  static directions are `north = (0,0,+1)`, `south = (0,0,-1)`, `east = (+1,0,0)`,
  `west = (-1,0,0)`.
- **`Board`** — no tile array. Pieces are held in `physicalPieces` with a parent/child board
  tree, and cell queries are bounds intersections.
- **`Piece`** — `position`, `direction`, `top`, `bounds`, `type`, plus `IsStandable()`,
  `IsPhysical()`, `CanWalkOntoFrom(Vector3i)`.
- **`PieceType` is a BIT FLAG enum**, not a sequence: `Land=1, Log=2, Player=4, Raft=8, Rock=16,
  Tree=64, TreeStump=128, ShallowWater=256, Ramp=512, WarpPoint=16384, Campfire=65536,
  Obstacle=131072, Monument=262144, Villager=524288, Landmark=8388608, Caption=16777216`.
- **There is no Water piece.** Open water is the *absence* of a physical piece, which is why
  `Board.GetPhysicalPieceBelow` returning null means water.

## 4. Rules worth knowing

- **Moving beats interacting.** `GridMovement.SetQueuedMove` calls `GetPositionAfterMove` first
  and only treats the input as an interaction when the position would not change.
- **A lying log is a walkway along its length.** `Log.CanWalkOntoFrom` returns
  `IsAlongAxis(direction)`, so approaching end-on steps onto it and approaching across it rolls
  it.
- **A tree carries the log it becomes.** `TreeLog.height` is the stump height plus four times
  `logPrefab.length`, so `logPrefab.length` is exactly the log you get.
- **`Obstacle.IsStandable()` is a height test** (`position.y + height > 2`), not a flat no.
- **Exhibits have `Piece.island == null`** and are often not physical pieces, so they must be
  found through `Piece.all` and judged by position.

## 5. How the mod hooks in

**No Harmony patches.** The game exposes a public static `Events` class holding about a hundred
public static mutable delegate fields — `playerMoved`, `playerChopped`, `undo`, `captionShown`,
`islandDiscovered` and so on. The mod subscribes with plain `+=`, which is far more robust than
patching methods.

State is read from `Simulator.current`, a plain public static field assigned in
`Simulator.Awake()`. The whole gameplay stack lives in one scene (`level0`), on GameObjects
named `Gameplay`, `Player`, `Resetter`, `HUD`, `Island Names`, `Island Hinter` and
`Warp Mode` — so there are no scene transitions to handle.

A few members are private and are worked around rather than reflected into where possible:

| Member | Approach |
|---|---|
| `IslandNameDisplay.CalculateIslandName` | reimplemented exactly; origin is `(200, 0, 200)` |
| `Simulator.GetIslandBeneathPlayer` | `Simulator.island` is public |
| `IslandHinter.currentIsland` | reflection, to tell whether hints are on screen |
| `WarpMode` internals | reflection, cached and guarded; travel goes through the button's public `onClick` |

## 6. Things that are deliberately not done

- The **`Solver`** class that ships in the build is never run. It solves the puzzle.
- Nothing routes you through a puzzle or suggests which log to push.
- Hints are read only when the game's own "Enable Island Hints" setting is on, which is when a
  sighted player sees the ghost logs.
- `PreviewInteraction` is called only on an explicit key press, never per frame: it sets
  `RaftGroup.includesPlayer` as a side effect.

## 7. Name collisions

`UnityEngine` is one namespace spread over many module assemblies, and the mod has collided with
`UnityEngine.Cursor` (CoreModule) and `UnityEngine.Compass` (InputLegacyModule). Auditing names
against one module is not enough.

`Plugin.cs` therefore declares an explicit `using X = AMEAccess.Game.X;` alias for each of the
mod's own types instead of importing the namespace. An alias binds unconditionally, so no type
added to any Unity module can reintroduce the ambiguity.
