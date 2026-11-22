---
name: add-content
description: Add a new VNP Weapon, fight upgrade, Drone Weapon, or enemy type to the VNP Godot project, wired into everything that has to know about it, such as the code factory, the upgrade pool in levels.tres, the playtest cheat menus, spawn weights, HUD icon, sounds, and the spec's tables. Use this whenever a stage or request adds or changes content: "add a weapon", "new upgrade", "a third enemy", "Orbital Shards harvesting", a new Drone Weapon, or a new upgrade effect or stat, even if the request only names the thing to add.
---

# Add content

VNP's content is data: each weapon, upgrade, Drone Weapon and enemy type is a definition (`*Definition`, a `.tres` file) in its feature's folder, read by code. Adding one is quick, but it has to be registered in several places that nothing checks at compile time. A missed one fails quietly: an upgrade never offered, a weapon missing from the cheat menu, a card with the wrong colour. This skill is the list of those places.

First read the stage in the current spec (named in CLAUDE.md) for what to build. Where it's silent on a design question, ask rather than invent. Then find the closest existing example of the same kind and copy its pattern. The project is consistent, so the nearest sibling is the best template.

## Rules that apply to every kind

- **IDs** are snake_case and match the folder name. A weapon's icon file is named after its ID.
- **Enums are stored as numbers** in `.tres` files (`Category = 0`, `Effect = 1`), in declaration order. Read the enum in code for the number, never guess it. Only add new values at the end of an enum: inserting or reordering would silently change every existing file.
- **Numbers that affect play** go in the definition's `.tres`, with an `[Export]` field and a doc comment saying what it does and how it feels to raise or lower it. Pick a sensible starting value and say what you picked.
- **Lists in `.tres` files:** use the helper rather than editing them by hand. It adds the `ext_resource` line, raises `load_steps`, appends the entry, and refuses duplicates:
  ```bash
  python <this skill>/scripts/tres_add.py tuning/levels.tres Upgrades res://weapons/<id>/upgrades/damage.tres
  ```
  Run it from the vnp folder. For a weighted dictionary, pass `--weight`. Use `--dry-run` to see the result first.
- **New .uid files:** a new C# class needs an editor rescan to get its `.uid` file. The check-stage skill does this.
- **New placeholder assets** (icons, meshes, materials, sounds): use the add-placeholder-asset skill if it exists. Otherwise put each in `res://placeholder/`, list it in `PLACEHOLDERS.md`, and for sounds follow the existing ones in `PlaceholderSounds.cs` and `audio.tres`.

## A VNP Weapon

1. **Folder:** `weapons/<id>/` holding:
   - `<Name>Definition.cs`: `[GlobalClass] public partial class <Name>Definition : WeaponDefinition`, with the weapon's own exported numbers.
   - `<Name>.cs`: the behavior, a `VnpWeapon` subclass.
   - `<id>.tres`.
   - `upgrades/`.

   ScatterShot and Shockwave are compact templates. Use the weapon's own `Damage`, `Cooldown`, `Area`, `Speed`, `Range` and `ExtraCount` so the standard upgrades work on it.
2. **Factory:** add a case to the `switch` in `VnpWeapon.Create` (`weapons/VnpWeapon.cs`). Without it, equipping the weapon logs an error and does nothing.
3. **Upgrades** in `upgrades/`:
   - `equip.tres` (`EquipWeapon`, category VnpWeapon);
   - one file per stat upgrade the spec lists (`ScaleWeaponStat` for percentages, `AddWeaponStat` for counts).

   Copy a sibling weapon's files. Add every one to the pool with the helper (`tuning/levels.tres`, `Upgrades`).
4. **Count:** if the weapon has a count that `{count}` card text should show, add it to the `count` switch in `Upgrades.Describe` (`scripts/fight/Upgrades.cs`).
5. **Cheat menu:** add the weapon's `.tres` to `StartingWeaponChoices` in `scripts/cheats/in_game_cheats.tres` with the helper.
6. **Look and sound:**
   - its own colour, distinct from the other weapons';
   - firing and impact effects (`scripts/effects/Effects.cs` has the building blocks);
   - sounds;
   - an icon at `placeholder/textures/icons/<id>.svg`: 64×64, white shapes on transparent, tinted in game. Copy an existing icon's style.
7. **Recorded for the run review:** the designer's fights are recorded so any moment can be drawn later, and a weapon must show up in that recording. `CollectPlaced` is required in the editor's build (it fails without it), and goes inside `#if RECORDING`, as every weapon's does, so exports leave it out: add everything the weapon has out in the world as `PlacedObject.Point` (a shard, mine or missile, with the radius it acts over), `Ring` (an outline, such as Shockwave's ring) or `Line` (a beam). Shots fired through `Fight.Projectiles` are recorded already, so a weapon with nothing else writes an empty body with a comment saying so, as Scatter Shot does. Then check it on a strip: `tools\scenario.cmd pilot=expert weapon=<id> spawn=all:2 invulnerable frames=900 record=yes`, then `tools\maps.cmd <its log> strip=10`. The stage check's recording test plays every starting weapon and fails if one leaves nothing in the recording.

## A fight upgrade

1. **Where the file goes:** in the `upgrades/` folder of what it belongs to:
   - a weapon's: `weapons/<id>/upgrades/`;
   - a Drone Weapon switch: `drone_weapons/<id>/upgrades/`;
   - for every Drone Weapon: `drone_weapons/upgrades/`;
   - otherwise `tuning/upgrades/` by what it affects (`vnp_stats/`, `drones/`, `harvesting/`).
2. **Fields:** fill them as `UpgradeDefinition.cs` documents: `Effect`, the target it needs (`Weapon`, `WeaponStat`, `Feature`, `DroneWeapon`, or `Stat`), `Amount` (a fraction for Scale effects), `Limit` (4 for limited upgrades, per the spec), `Category`, and card text with `{percent}`, `{amount}`, `{count}` or `{drones}`.
3. **Category:** upgrades to a weapon take that weapon's kind. Upgrades for whichever Drone Weapon is in use are DroneWeapon. Stats and harvesting are Stats; drone numbers are Drones. The rule is under "Offer card colours" in `docs/PROTOTYPE_2_SPEC.md`.
4. **New behavior, beyond a new file:**
   - **A new player stat:** add it at the end of `PlayerStat`, and handle it in both `ScaleStat` and `AddStat` in `Upgrades.cs`.
   - **A new weapon stat:** add it at the end of `WeaponStat`, handle it in `VnpWeapon.AddStat` and `ScaleStat`, and use it in the weapon.
   - **A new weapon feature:** add it at the end of `WeaponFeature`, then in the weapon check `HasFeature(...)`, as Orbital Shards does for `Blocking`. Give the weapon a visible change once it's taken. Features are mods on cards: one per weapon (`OneFeaturePerWeapon`), and recordings list each weapon's mods by themselves. If the mod puts something new in the world, add it to the weapon's `CollectPlaced`.
   - **A new effect:** check `CanOffer`, `Apply` and `TotalBonus` in `Upgrades.cs`, and the upgrade list's grouping in `scripts/ui/UpgradeList.cs`.
5. **Pool:** add the upgrade with the helper.

## A Drone Weapon

1. **Folder:** `drone_weapons/<id>/` with `<id>.tres` (a `DroneWeaponDefinition`) and `upgrades/equip.tres` (`SwitchDroneWeapon`, category DroneWeapon, Limit 1). A weapon that reuses an existing `Kind` needs no code.
2. **A new kind** needs code:
   - a value at the end of `DroneWeaponKind`;
   - a `DroneAttack` subclass beside the `.tres` (Projectile and Beam are short templates, and Ram shows an attack that steers the drone itself);
   - a case in `DroneAttack.Create`.

   `Range` and `AttackDistance` are measured from the enemy's edge: use `EdgeDistance` and `RadiusOf`, never the enemy's center. Decide whether the attack should keep back from enemies that ask for it (`KeepsBack`).
3. **Register it:**
   - add the equip upgrade to the pool;
   - add the `.tres` to `StartingDroneWeaponChoices` in `in_game_cheats.tres`;
   - add an icon named after its ID.
4. **Recorded for the run review:** a new `DroneAttack` must implement `CollectPlaced`, as VNP Weapons do (step 7 above): beams as lines, anything else it leaves as points; an empty body with a comment if its shots go through `Fight.Projectiles` or it is only the drones themselves, as Ram is. A Drone Weapon that never shows up in a recording must be listed, with why, in `NOTHING_OF_ITS_OWN` in the check-stage skill's `check_recording.py`.

## An enemy type

1. **Folder:** `enemies/<id>/` holding:
   - `<Name>Definition.cs`: `: EnemyDefinition`, adding only the fields its behavior needs;
   - `<Name>.cs`: an `EnemyBehavior` subclass;
   - `<id>.tres`.

   BasicEnemy is the simple template. Goo shows growth, splitting, absorbing and a custom look.
2. **Factory:** add a case to `EnemyBehavior.Create` (`enemies/EnemyBehavior.cs`). Put subclasses before their base classes in the switch.
3. **Spawning:** add the `.tres` to both `GroupWeights` and `WaveGroupWeights` in `tuning/enemy_spawning.tres` with the helper and `--weight`. Set `GroupSize`, `TargetsDronesFirst` and `DroneKeepBack` in its own file.
4. **Difficulty:** if it needs its own ramp, follow the Goo:
   - add fields to `DifficultyTuning.cs` and `difficulty.tres`;
   - add a scale to `Fight.cs`;
   - read it in the behavior's `Spawned`.
5. **Look:** a silhouette that differs from every other enemy and from asteroids, not only its colour, plus its mesh, possibly a material, and death, attack and other sounds. Check it with a screenshot (check-stage step 2).
6. **Recorded for the run review:** enemies are recorded and drawn from their traits (`AbsorbsDronesOf`, `DamagesByTouchOf`), name and colour, so a new type appears with no code of its own. If it has a new trait that matters for reviews, record it in `ReviewRecorder.Snapshot` and mark it in `MapDrawer`, as touch damage is striped. If it leaves things in the world (spikes, blobs), they must be recorded too.

## Finish

1. **The spec**, in the same step (CLAUDE.md requires it):
   - the behavior, described without tuning numbers;
   - any new tuning values in its tuning table, by name and file;
   - the upgrade pool's new size;
   - the upgrade's row in the "Offer card colours" table in the phase 2 spec;
   - choices made for "not yet decided" items;
   - folder lists that name every feature folder, such as "Tuning files" in the phase 2 spec.
2. **PLACEHOLDERS.md** for every new asset.
3. **Checks:** run the check-stage skill. Its data check reports the pool size and spawn weights, so compare them with what the spec now says.
4. **Report:** tell the designer what was added, how to try it, and which values are guesses to tune. The starting weapon cheat lets them pick a new weapon straight away.
