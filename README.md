# Still Alive · Unity 6

[English](README.md) · [Русский](README.ru.md)

I wanted to play with Mirror's Edge parkour and build my own maps in an editor. I found [Still Alive by Eideren](https://github.com/Eideren/Mirror-s-Edge), tried it in Unity, and had a lot of fun with it. This fork is my attempt to keep working on that project.

**Eideren is the author of the original port.** He did the work that made this possible, and I'm grateful for it.

The project now runs in Unity 6. I want to focus on running, jumping, hanging, swinging, the camera and animations. Combat, AI, story events, menus and saves aren't part of my plans right now. This is a noncommercial experiment: it still has bugs, and I'll update it as I work on it.

## A bit of history

Eideren started the repository in 2021 and later moved it to Unity 2022.3. In [Mirror's Edge Decompiled](https://eideren.com/posts/mirrors-edge-decompiled.html), he explains how he studied the game's movement, converted UnrealScript to C# with Unreal Library, and reconstructed native behavior using a disassembler and debugger. Unity handles input, rendering, physics queries and low-level audio; the ported code handles the movement states, animation and game logic.

His focus was the player and traversal. He also wrote about unfinished audio and problems with steps and slopes. I'm building on that work and keeping the remaining tasks in [TODO.md](TODO.md). You can watch [his original demonstration here](https://www.youtube.com/watch?v=W6-_Lgdt0zg).

I started working on this fork in October 2026. So far, the main work has been fixing movement bugs and making it easier to test the parkour on a small map.

## Try it

You'll need **Unity 6000.6.4f1**, Git and **Git LFS**. That's the exact Unity 6 version I've tested; other versions haven't been checked. The project uses the Built-in Render Pipeline. Checks have been run on Windows.

Clone through Git. **Don't use Download ZIP**: it can contain LFS pointers instead of the models and textures.

```sh
git lfs install
git clone https://github.com/yamixdev/Mirror-s-Edge.git
cd Mirror-s-Edge
git lfs pull
```

Open the folder in Unity Hub with **6000.6.4f1**, wait for the import, then open **`Assets/New Scene.unity`** and press Play. Click inside Game view to control Faith.

I included this little test map so you can try things immediately. It has obstacles, a Spawn, a balance beam, swinging bar, ladder and zipline. Faith's full model and restored textures are included too. The larger original scene is still at `Assets/Scenes/Cranes_Off.unity`; I haven't fully retested it.

| Input | Action |
| --- | --- |
| W / A / S / D | Move; W / S also climb and swing |
| Mouse | Look |
| Space | Jump, pull up from a ledge, jump off a bar |
| Left Shift | Crouch / slide, or let go while hanging |
| Q | Quick turn / look behind |

To make a map, add a floor with colliders, a Main Camera, a Directional Light and **`Assets/Spawn.prefab`**. Leave enough room around the Spawn for the character. Place parkour objects from **`Assets/Prefabs/Parkour`**. [The object guide](docs/parkour-objects-en.md) explains placement and sizes.

Visual Studio and Unity MCP aren't needed to play in the editor.

## What's changed

- Fixed tested cases of wall jitter, hands disappearing at ledges, and backward movement during pull-ups.
- Removed delayed death after a jump and fixed stuck crouch requests.
- Improved swinging, bar-to-bar catches, sideways hand movement and its sound events.
- Added placeable parkour objects. Holding Space through a vault with one left hand now gives one upward jump while keeping forward speed.
- Enabled the complete body for Scene view, external cameras and shadows. The player camera keeps the original first-person models; this doesn't add a third-person game mode.

Details are in [CHANGELOG.md](CHANGELOG.md). [The checks](Tools/CollisionProbe/README.md) run in a separate Unity project.

## What still needs work

Ladder grabs can throw an exception. Some full-body hand positions still look wrong. Slopes, stuck movement, missing animation messages and behavior at different frame rates also need work. The full list, including Eideren's original tasks, is in [TODO.md](TODO.md).

A full standalone build, every map, URP/HDRP and other platforms haven't been checked. If you find a bug, open an issue with the Unity version, scene, inputs and steps to reproduce it. The relevant Console error and stack trace help too.

If models or textures are missing, check that `git lfs pull` succeeds. The original README also provided an [asset mirror](https://mega.nz/folder/48hnGBTY#jIfGg36zOBKYF1eVuT71sQ); it contains upstream assets, not this fork's new files.

## Thanks

**Eideren** for Still Alive and the original Unity port. **DICE / Electronic Arts** for Mirror's Edge itself. **EliotVU / Unreal Library** for the tools used in the original conversion, and **Gildor / UE Viewer** for texture export.

The original authorship and Git history are preserved.
