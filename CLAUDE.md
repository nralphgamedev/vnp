# VNP (working title)

VNP is a top-down roguelite space conquest game built in Godot. The current goal is a combat prototype: one real-time fight, with nothing from the rest of the game.

The current spec is the source for what to build: @docs/PROTOTYPE_2_SPEC.md

It builds on the phase 1 spec, which describes the fight that is already built: @docs/PROTOTYPE_SPEC.md

## Technical rules

- Godot 4.7.2. Update this line if a different version is installed.
- C#.
- This is a 3D project with all gameplay locked to a single flat plane viewed from above. Use 3D nodes, not 2D nodes. Calculate movement, flocking, ranges, and distances in two dimensions on that plane.
- Placeholder art is simple 3D geometric shapes. Beyond making units easy to tell apart, spend time on visuals only where the spec asks for it, such as feedback effects.
- Every placeholder visual and audio asset goes in `res://placeholder/`, saved as its own file rather than embedded in a gameplay scene, and is listed in `res://placeholder/PLACEHOLDERS.md` with what uses it and where it came from. Written text is not a placeholder asset and stays where it is.
- Keep every tuning value in one place, as the spec describes. The point of the prototype is to adjust these numbers by playing, so none of them should be buried in scripts.
- Tuning values and content definitions (weapons, upgrades, enemy types) are data-only custom Resources in `res://tuning/`, split by system. Modding may be supported later, so keep logic out of them.
- Playtest cheats go in `scripts/cheats/`, which `VNP.csproj` compiles only in the editor's Debug configuration. Wrap the cheat code and every line that uses it in `#if TOOLS`, so a cheat can never reach an exported game.

## How we work

- The designer is a programmer by trade and will playtest every stage. Explain what you built in plain terms and say how to try it.
- Build one stage from the spec's build order at a time, then stop so it can be played. Do not start the next stage until asked.
- You cannot judge how the game feels. Feedback will arrive as descriptions of what happened on screen, such as "drones lag too far behind." Turn that into a change, and say which tuning value or behavior you adjusted.
- Stay inside the spec's scope. If something would be useful but is listed as out of scope or as a stretch goal, mention it rather than building it.
- When the spec is silent on a design question, ask. Where the spec marks something as not yet decided, pick the simplest behavior, say what you picked, and keep it easy to change.
- Keep the current spec up to date. Whenever something changes during prototyping, whether from playtest feedback, a new design decision, or a choice made for something marked not yet decided, update the spec in the same step so it always describes what is built. Move items that get settled out of the spec's not-yet-decided list and record what was chosen.
- After a stage is approved, commit it to git so there is a working version to return to.
