# Build and publish, step by step

Every command is typed at a prompt. Open **Command Prompt** (Windows key, type `cmd`, Enter).
Lines starting with `rem` are comments; you do not type them.

Your GitHub username is `hasajaza`, which is already filled in below.

---

## 1. Put the project somewhere permanent

Extract `AMEAccessWork.zip`, then:

```
cd /d C:\Users\%USERNAME%\source\repos\AMEAccessWork
dir
```

You should see `AMEAccess.sln`, `README.md` and the `AMEAccess`, `docs` and `scripts` folders.

---

## 2. Put your name in the licence

```
notepad LICENSE
```

Change `YOUR NAME` on the copyright line, save, close.

---

## 3. Build

The project finds the game by itself. If it cannot, it says so and you edit
`AMEAccess\GamePath.props`.

```
"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" AMEAccess.sln /p:Configuration=Release /v:minimal
```

If you have a different Visual Studio edition, change `Community` to `Professional` or
`Enterprise`. You can also just open `AMEAccess.sln` in Visual Studio and press Ctrl+Shift+B.

A successful build prints the game folder it found and copies `AMEAccess.dll` into
`BepInEx\plugins` for you.

---

## 4. Build the player downloads

This packages **the BepInEx you already have installed**, so the release is the exact version
you have been playing with:

```
powershell -ExecutionPolicy Bypass -File scripts\make-release.ps1 -UseInstalledBepInEx
```

It prints which game folder it took BepInEx from and which version that is. Two files appear in
`release\`:

- `AMEAccess-1.0.0-complete.zip` — BepInEx, the mod, the installer. For most players.
- `AMEAccess-1.0.0-mod-only.zip` — just the mod, for players who already have BepInEx.

If it cannot find your game folder, give it:

```
powershell -ExecutionPolicy Bypass -File scripts\make-release.ps1 -UseInstalledBepInEx -FromGameFolder "C:\Program Files (x86)\Steam\steamapps\common\A Monster's Expedition"
```

**Before you publish**, put the speech files in `native\` so they are included:

```
copy "C:\path\to\UniversalSpeech.dll" native\
copy "C:\path\to\nvdaControllerClient32.dll" native\
```

They are not committed to git (`.gitignore` excludes them), but the release script picks them up
from there. Also drop the licence texts into `release-kit\licenses\` — see the README there for
the list.

---

## 5. Create the GitHub repository

Once only. If `git` is not recognised, install Git for Windows first from git-scm.com.

```
git --version
git config --global user.name "Hasajaza"
git config --global user.email "hassan.jazairli@gmail.com"
```

The repository already has its first commit and a `v1.0.0` tag, made in your name to match
Suzerain Access. Check it:

```
git log --oneline
git status
```

Now make the empty repository on GitHub, in the browser: **New repository**, name it
`AMonstersExpedition-access`, **do not** add a README, licence or .gitignore, then:

```
git remote add origin https://github.com/hasajaza/AMonstersExpedition-access.git
git branch -M main
git push -u origin main
```

Git will ask you to sign in the first time.

---

## 6. Publish a release

The `v1.0.0` tag already exists, so just push it:

```
git push origin v1.0.0
```

Then on GitHub: **Releases > Draft a new release**, choose the tag `v1.0.0`, title
`A Monster's Expedition Access 1.0.0`, paste the top section of `CHANGELOG.md` as the notes, and
attach both zips from `release\`.

---

## 7. Later changes

```
git add -A
git commit -m "What changed"
git push
```

For a new version: raise `<Version>` in `AMEAccess\AMEAccess.csproj`, add a section at the top
of `CHANGELOG.md`, rebuild, re-run the release script, then tag and push as in step 6.

---

## If something goes wrong

**"cannot find the A Monster's Expedition game folder"** — open `AMEAccess\GamePath.props` and
put your folder between the `AmeDir` tags.

**"found the game in more than one place"** — you have two copies. Put the one you want in
`AmeDir` as above; setting it turns the automatic search off.

**"BepInEx is not installed"** — install BepInEx 5.4.x **win_x86** into the game folder and
start the game once so `BepInEx\core` exists. The x64 build will not work: the game is 32-bit.

**"Could not find an installed BepInEx"** from the release script — pass
`-FromGameFolder "<your game folder>"`.

**`git push` rejected** — the GitHub repository was created with a README. Either delete and
recreate it empty, or run `git pull --rebase origin main` and push again.
