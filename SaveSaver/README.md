# SaveSaver / Another SL MOD

Creates checkpoints before expedition battles so you can retry after quitting or terminating the game process. It does not restore mid-battle turns, cards, or health. Terminating the process can only recover the last completed save.

## Changes in 1.0.3

- Protect checkpoints from repeated battle requests, quitting during battle transitions, and late autosaves during chained encounters. Quit protection no longer truncates the original `QuitSave` IL.
- Save before the Trial of Strength charges its 500 gold entry fee. Preserve the Sword Sanctuary flag so its special boss can be retried.
- Save before Shiranui and Casino events disable their interactions or grant entry gold. Keep these checkpoints through dialogue and delayed battle loading.
- Keep the checkpoint before the first Bloody Mist boss when a second battle starts immediately. Continuing after quitting requires replaying the entire pair.
- Skip extra battle checkpoints in story mode, while loading, or without a valid expedition. Preserve normal field saves, run completion, and the game's defeat handling, including Hope Mode recovery.
- Remove the dependency on Harmony file logging, report save exceptions, and undo partially installed patches if initialization fails.

Story-mode battles use the game's own save flow. Enable the mod before starting a battle or event; it cannot recover encounters consumed before it was enabled.

## Build

Requires a .NET SDK, the installed Chrono Ark assemblies, and HarmonyX 2.10.2. The default game directory is `C:\Program Files (x86)\Steam\steamapps\common\Chrono Ark`; override it with `GameFolder` if needed.

```powershell
dotnet build SaveSaver/SaveSaver.csproj -c Release
# For a different game directory:
dotnet build SaveSaver/SaveSaver.csproj -c Release '-p:GameFolder=D:\SteamLibrary\steamapps\common\Chrono Ark'
```

The output is `SaveSaver/bin/Release/net40/SaveSaver.dll`. Building this repository does not modify the game installation or player saves.

## Verification

```powershell
dotnet build SaveSaver.Tests/SaveSaver.Tests.csproj -c Release
./SaveSaver.Tests/bin/Release/net48/SaveSaver.Tests.exe
```

The regression executable installs the real HarmonyX patches against in-memory game substitutes. It checks checkpoint contents, transitions, delayed events, and patch removal without accessing player saves. These checks do not replace testing inside Unity.

For manual verification, use a test save and quit during a normal encounter's loading animation, combat, Shiranui/Casino dialogue, and the second Bloody Mist battle. Continue and check that encounters remain accessible, Trial of Strength does not charge twice, and Casino entry gold is not duplicated. Also check normal field and post-battle saving, completed runs, abandoning a run, and defeat recovery.
