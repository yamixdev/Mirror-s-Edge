# Parkour checks

[English](README.md) · [Русский](README.ru.md)

Run these from the repository root in PowerShell. Git LFS assets must be downloaded first.

```powershell
& ./Tools/CollisionProbe/Invoke-CollisionProbe.ps1
& ./Tools/CollisionProbe/Invoke-CollisionProbe.ps1 -EntryPoint FullBodyProbe.Run -ReportName fullbody
```

The script creates a separate project in `Documents/MirrorEdgeParkourData/CollisionProbeProject`, copies source, assembly definitions, packages and required assets, and runs Unity in batch mode. It does not open or modify your map. The default editor path follows `ProjectSettings/ProjectVersion.txt`; pass `-UnityExe` for another installation. Pass `-ProbeProject` to choose a separate directory. Existing directories need the `.collision-probe-owned` marker before the script will use them. The import cache is retained for later runs.

| Entry point | What it checks | Graphics required |
| --- | --- | --- |
| CollisionProbe.Run | Wall-blocked steps, low steps, initial overlaps and mesh collision | No |
| ReviewProbe.Run | Volume deletion, callback removal, end-pose sampling, animation graph cleanup and crowded hit buffers | No |
| GameplayHandsProbe.Run | Hands remain visible in six camera poses at a ledge, compared with an independently baked mesh | Yes |
| PullUpProbe.Run | Ledge pull-up path, completion, crouched route and blocked clearance | Yes |
| VaultJumpProbe.Run | Held/released jump input after speed vault, single boost, velocity and ceiling collision | Yes |
| ParkourObjectsProbe.Run | Prefab geometry, interaction zones, dimensions and world registration | Yes |
| JumpSurvivalProbe.Run | Legacy jump callback does not kill the character after a delay | Yes |
| SwingProbe.Run | Grabbing, finite grip, momentum, bar-to-bar catches, sound and input recovery | Yes |
| ShimmyProbe.Run | Hand contact, release/replant phases, reverse sound notifies and teardown | Yes |
| FullBodyProbe.Run | Materials, native scale, full-body rendering, owner-camera visibility, shadows and nested cameras | Yes |

Run a different check by setting `-EntryPoint` and giving it a distinct `-ReportName`. Rendering checks run Unity with a hidden window and need a working graphics adapter.

Results are JSON files under `Evidence` in the separate project; rendering checks also save images. The script fails on a failed check, a compilation error, a missing report or a five-minute timeout. Full Unity logs remain local because they can contain licensing arguments.

Checks use the actual converted world, animation resources and Unity physics. Test fixtures create their own colliders, cameras and Spawn; some checks use a temporary material or controlled input. They cover specific bugs and do not replace playing complete maps or checking a standalone build.

The script copies current source into its marked test directory. If you delete or rename production scripts, use a fresh `-ProbeProject` directory to avoid old copied files affecting a result.

For the portable test map, use `PublishedSceneProbe.Run`: it checks Spawn, camera/light, scene scripts, basic material dependencies, live player startup and the included body textures. It opens only the copy of `New Scene` in the marked test project. The published scene uses basic materials; a locally edited scene may require additional assets.
