---
name: add-placeholder-asset
description: Add or change a placeholder sound, mesh, material, model or HUD icon in the VNP Godot project, the way the project requires: made by the placeholder baker or drawn as a file in res://placeholder/, registered in audio.tres and Sounds.cs for sounds, rebaked without disturbing the existing assets, and listed in PLACEHOLDERS.md. Use this whenever new content needs a look or a sound, such as a new enemy's mesh, a weapon's firing sound, a death sound or an icon, or when the designer asks to change how a placeholder looks or sounds, even if they don't say "placeholder".
---

# Add a placeholder asset

Every visual and audio asset in VNP is a temporary placeholder that final art will replace one file at a time. So each one lives in `res://placeholder/` as its own file and is listed in `res://placeholder/PLACEHOLDERS.md`. Most are not drawn by hand: `placeholder/code/PlaceholderBaker.cs` builds them from code and saves them, so a change means editing the recipe and baking again, never editing the generated file.

## Sounds

Sounds are synthesised from tones and noise by `placeholder/code/PlaceholderSounds.cs`, which the baker runs.

1. **Write the recipe** with `Make`, `Tone` and `Noise`, like its neighbours. Add it at the end of `Bake()`, under a heading for the phase or stage. All the noise in every sound comes from one shared random generator with a fixed seed, so a recipe inserted earlier would shift the noise of every sound after it, and rebaking would quietly change sounds already approved. Describe the sound in a comment, such as "a wet gulp: a low tone that drops", so the designer can picture it.
2. **Bake** (see Baking below).
3. **Register it** in `audio.tres` and `Sounds.cs` with the helper, from the vnp folder:
   ```bash
   python <this skill>/scripts/add_sound.py goo_absorb --category Deaths --volume 0.55 --max-copies 4 --vary-pitch
   ```
   - **Category:** one of the `SoundCategory` names in `audio/SoundDefinition.cs`. Their meanings are documented there.
   - **Volume:** start near similar sounds.
   - **Max copies:** limit it for frequent sounds.
   - **Vary pitch:** use it for anything repeated, but not for tunes or alarms.
   - **Loop:** only for sounds held while something lasts, and `Sounds.Hold` must keep asking for it.

   The helper adds the stream, the sound entry, the list entry, `load_steps`, and a `Sound.GooAbsorb` constant, and it refuses duplicates.
4. **Play it** from game code with `Fight.Sounds.Play(Sound.GooAbsorb)`.

## Meshes, materials and models

1. **Meshes:** add a line to the Meshes section of `PlaceholderBaker.Bake()`:
   - `PlaceholderShapes.ExtrudedShape(outline, thickness)` for flat-topped unit silhouettes. Outlines are points on the plane, with -y as forward.
   - `PlaceholderShapes.Blob` for smooth lumpy shapes.
   - Godot's primitive meshes.
   - A new shape builder in `PlaceholderShapes.cs`, if nothing else fits.
2. **Materials:** units drawn through an `InstanceBatch` take their colour per copy, so their material must use vertex colour (start from `InstanceMaterial(...)` in the baker). Use `instanced_lit` unless the asset needs its own look, as the Goo's glossy rim does.
3. **In code:** load assets with `Placeholders.Mesh("name")` and `Placeholders.Material("name")`.
4. **Readability:** the whole fight is seen from high above against a dark grid with grey asteroids. A new unit needs a silhouette that differs from the others, not only a colour, and must not be mistaken for an asteroid. Check with a screenshot (the check-stage skill's step 2).

## Icons

HUD icons are hand-written SVG files in `placeholder/textures/icons/`. They're 64×64, white shapes on transparent, and the game tints them with the weapon's colour. A weapon's icon is named after its ID (`<id>.svg`). Copy an existing icon's style: a few bold shapes that read at small size. Godot imports a new file on the next editor rescan.

## Baking

```bash
godot --headless --path . res://placeholder/code/Bake.tscn
```

The project's Godot binary is noted in CLAUDE.md and memory. The bake rewrites every generated asset, not only the new one, so check what changed:

- **Existing meshes, materials and sounds** should come out byte-identical. If one changed that you didn't mean to change, a recipe moved or the random sequence shifted. Fix that rather than accepting it.
- **`placeholder/models/*.tscn`:** these always change, because every bake gives their nodes new random IDs. If those `unique_id` lines are the only change, revert them with `git checkout -- placeholder/models/`.
- **New files:** they appear with their `.import` files. Commit both.

## List it

Add a row to the right table in `placeholder/PLACEHOLDERS.md`:

- **What it is:** described so someone could replace it without looking.
- **What uses it:** the script or feature.
- **How it's made:** baked (with the builder function, if it's special), or drawn by hand. Nothing in this project is downloaded. If that ever changes, list the source and licence.

A placeholder made in code each fight rather than saved as a file, like the asteroid rocks, still gets a line, naming the script that makes it.

## Finish

Run the check-stage skill. Its data check confirms every sound has its file, and its build catches a missing `Sound` constant.
