# VNP Combat Prototype Spec

VNP (Von Neumann Probe, a working title) is a top-down roguelite space conquest game. The full game has a turn-based galactic map and real-time fights. This prototype builds **one real-time fight and nothing else**.

This spec is taken from the game design document (GDD). Where the two disagree, the GDD is newer only if the designer says so; otherwise treat this file as the source for the prototype.

## What the prototype is for

The prototype exists to find out whether the core fight is fun, and to answer questions that can only be settled by playing:

- Is the 5-minute time limit the right length?
- What kill targets and difficulty ramp feel right, both during the fight and after the limit?
- How many units (basic probes and enemies) can be on screen at once while performance stays smooth?
- Do the flocking, engage, and disengage behaviors feel good to control?
- Do the fight upgrade choices lead to meaningfully different builds from one fight to the next?
- Does harvesting with beams feel good alongside fighting?

Because the goal is answering these questions, fast iteration matters more than polish. Every number that affects feel should be easy to change (see Tuning values).

## Technical setup

- **Engine:** Godot 4.
- **2.5D:** this is a 3D project, with all gameplay locked to a single flat plane viewed from above. Use 3D nodes, not 2D nodes. Movement, flocking, ranges, and distances are calculated in two dimensions on that plane, even though the objects are 3D.
- **Art:** simple 3D geometric shapes as placeholders. Each kind of unit should be recognizable by its silhouette, not only its color.
- **Performance:** the fight is expected to hold a large number of moving units at once. One of the prototype's questions is how many, so build with that in mind and make unit counts easy to raise.

## Terms

- **VNP:** the player's main ship. The one the player controls is the **primary VNP**.
- **Basic probe:** a small combat unit that flocks around the VNP.
- **Gateway:** the fixed point where the fight starts, and the only place the player can retreat from.
- **Debris:** what a destroyed enemy leaves behind. It can be harvested.
- **Harvest beam:** the beam the VNP and its basic probes use to extract fight experience from asteroids and debris.
- **Fight experience:** what harvesting produces. Enough of it levels the player up within the fight.
- **Fight upgrade:** a temporary upgrade chosen on each level up. It lasts only for this fight.
- **VNP Weapon:** a weapon in one of the VNP's weapon slots.
- **Basic Probe Weapon:** the attack the basic probes use. All of a VNP's probes use the same one.

## The fight

### Controls

The game is played with one hand, in the style of Vampire Survivors.

- Move: WASD or a single joystick.
- Select an option in a menu: Spacebar, or A/X on a controller.
- Pause: Esc or Start.

Weapons aim and fire automatically. There is no aim or fire input.

### Primary VNP

- Starts with the **Pulse Laser** and **two weapon slots**.
- Has health. The fight is lost if the primary VNP is destroyed.
- Has its own harvest beam.

### Camera

A perspective camera placed high above the plane, with a narrow field of view and a slight tilt so the 3D shapes show some depth. It follows the primary VNP. The camera type should be easy to switch, because orthographic will be tried as well.

### Arena and gateway

- The arena is infinite.
- The fight starts at the gateway.
- An indicator at the edge of the screen always points toward the gateway, showing where it is in relation to the player.
- If the VNP stays on the gateway for 10 seconds, the option to retreat appears.

### Asteroid fields

- Asteroids sit in asteroid fields at set places in the arena. The player travels to them. They do not spawn around the player.
- The number of fields varies.
- Fields come in two layouts: clusters and rings.
- A field can be stationary, moving, or rotating.

### Harvesting

- The VNP and every basic probe harvest asteroids and debris with harvest beams. There are no pickups to collect.
- Asteroids and debris can always be harvested once they are within harvest range.
- Enemy debris never disappears.
- Each asteroid and piece of debris has a **mass**. Mass sets the base harvest time: about 1 second for a small one, up to about 10 seconds for a large one.
- Larger asteroids and debris also give more fight experience.
- The base harvest time is shortened by harvesting upgrades and by the number of basic probes harvesting, so more probes means faster harvesting.
- Harvesting gives fight experience. It also builds temporary basic probes, up to the basic probe cap. Once the cap is reached, harvesting gives only fight experience.

### Basic probes

- The VNP starts the fight with **10** basic probes.
- Probes are smaller than the VNP and flock around it.
- When asteroids or debris come in range, the probes surround and harvest them.
- When enemies come in range, the probes surround and attack them.
- Probes harvest and fight at the same time. Harvesting stays their priority whenever harvestable material is in range.
- If the player moves away, the probes try to disengage and follow.
- Probes can be destroyed. A destroyed probe's wreckage disintegrates and cannot be harvested.
- Probes start with the **Projectile** weapon, which fires single projectiles.

### Enemies

One placeholder enemy type:

- It has its own flocking movement.
- It attacks with projectiles.
- It has an aggro range.
- It spawns endlessly.
- It grows stronger over the course of the fight.
- When destroyed, it leaves debris.

### Fight experience and level ups

- The prototype uses a single color of fight experience.
- Gathering enough fight experience levels the player up.
- Each level up pauses the action and offers a fight upgrade: a choice of three options from the upgrade pool.
- An offer can be rerolled. The player starts with one free reroll.

### Upgrade pool

Twelve upgrades. This list is expected to change through playtesting, so keep upgrades easy to add, remove, and adjust.

| Upgrade | Effect | Condition |
|---|---|---|
| Orbital Shards | New VNP Weapon for the second slot: shapes that circle the VNP and damage what they touch | Not yet equipped |
| Pulse Laser damage | Raises Pulse Laser damage | |
| Pulse Laser cooldown | Shortens Pulse Laser cooldown | |
| Orbital Shards damage | Raises Orbital Shards damage | Orbital Shards equipped |
| Orbital Shards area of effect | Raises Orbital Shards area of effect | Orbital Shards equipped |
| Beam | Switches the basic probes from Projectile to Beam, a short-range beam attack | Probes not already using Beam |
| Health | Raises the VNP's health | |
| Movement speed | Raises the VNP's movement speed | |
| Harvest rate | Fight experience is gathered faster | |
| Harvest range | The harvest beam reaches farther | |
| Basic probe cap | Raises the cap for the rest of the fight | |
| New basic probes | Adds temporary basic probes right away, up to the cap | |

The Pulse Laser is a straight beam that fires at the nearest enemy. Damage, cooldown, and area of effect belong to each weapon, not to the VNP.

### Winning, losing, and retreating

- **Win:** destroy a target number of enemies (the kill target).
- **Loss:** the primary VNP is destroyed.
- **Retreat:** the player chooses the retreat option at the gateway.
- **Time limit:** 5 minutes. The fight does not end at the limit. Instead, the edges of the screen flash red and an exclamation point briefly appears in the center. From then on, difficulty rises substantially every minute, with another flash and exclamation point each time. Past the limit, the player should be forced to retreat, die, or narrowly succeed.

### Screens

- **Pause menu:** pauses the fight and lists the player's current upgrades.
- **Result screen:** shows win, loss, or retreat, along with kills, time, and fight experience harvested. It should offer a quick way to start another fight.

## Tuning values

Keep all of these in one place so they can be adjusted quickly without hunting through code. Values marked "placeholder" have not been decided by the designer: choose a sensible starting number and expect it to change.

| Value | Starting point |
|---|---|
| Time limit | 5 minutes |
| Seconds on the gateway before retreat is offered | 10 |
| Starting basic probes | 10 |
| Weapon slots | 2 |
| Options per fight upgrade offer | 3 |
| Free rerolls | 1 |
| Base harvest time by mass | about 1 second (small) to about 10 seconds (large) |
| Kill target | placeholder |
| Enemy spawn rate | placeholder |
| Difficulty ramp during the fight | placeholder |
| Difficulty increase per minute past the limit | placeholder, but it should be large |
| Fight experience needed per level | placeholder |
| Basic probe cap | placeholder, above 10 |
| Harvest needed per temporary basic probe | placeholder |
| Fight experience by mass | placeholder |
| Number of asteroid fields | placeholder, and it should vary |
| Enemy aggro range, health, damage, and speed | placeholder |
| Weapon damage, cooldown, and range | placeholder |
| Upgrade amounts | placeholder |
| Camera field of view, height, and tilt | placeholder |

## Not yet decided

The designer has not settled these. Use the simplest reasonable behavior, say what you chose, and make it easy to change.

- What probes do when an enemy and harvestable material are both in range but far apart from each other.
- Whether the free reroll refreshes per offer, per fight, or not at all.
- Where asteroid fields sit in relation to the gateway.
- How enemies choose where to spawn in an infinite arena.

For design questions this file does not answer, ask the designer rather than inventing a rule.

## Suggested build order

Build one stage at a time, and stop after each so the designer can play it.

1. The plane, the camera, VNP movement, the gateway, and the gateway indicator.
2. The enemy and the Pulse Laser, with enemies leaving debris.
3. Basic probes: flocking, attacking, following, and being destroyed.
4. Asteroid fields and harvesting, including mass, fight experience, and temporary probes.
5. Level ups, the upgrade pool, and rerolls.
6. The kill target, the time limit and escalation, retreat, the pause menu, and the result screen.

## Out of scope

Do not build any of these. They belong to the full game.

The galactic map, turns, the Zone Conquer sequence, empire experience, permanent Level Up Rewards, weapon storage, planets, empire resources, factions, the codex, meta progression, tutorials, saving, the upgrade for harvesting probe wreckage, and final art and audio.

## Stretch goals

Add these only after the core fight feels good and the designer asks for them.

- A second enemy type whose behavior calls for a different upgrade choice, such as an enemy that absorbs basic probes.
- One weapon combo, where two weapons combine into a new one.
- One escort VNP with its own flocking basic probes.
- Three fights in a row, with one permanent upgrade chosen between each, to test whether starting from a growing loadout keeps repeated fights from feeling the same.

## Done when

A full 5-minute fight runs smoothly from start to result screen, all three outcomes (win, loss, and retreat) work, and the fight is fun enough to want to replay several times in a row.
