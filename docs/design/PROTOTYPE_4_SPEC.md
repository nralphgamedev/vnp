# VNP Combat Prototype, Phase 4: Grey Goo

Phase 3 (`docs/design/PROTOTYPE_3_SPEC.md`) added a second enemy type, the Goo, mixed into the same fight as the basic enemy. Its main question was whether a second enemy type calls for different upgrade picks, and the answer was "somewhat": one Goo type was too few to tell. Phase 4 adds the GDD's other Grey Goo types, so the faction can be judged as a whole.

This phase focuses only on the Grey Goo faction. Fights spawn Grey Goo and nothing else, so the basic enemy is no longer used (see Spawning).

Everything in the phase 1, 2, and 3 specs still applies unless this file changes it. Where they disagree, the newest file wins.

## What this phase is for

- Does a fight against Grey Goo alone call for different builds than the fights in phases 1 to 3?
- Does each type pressure something different (the VNP, the drones, defenses, the economy, harvesting), and can the player tell the types apart at a glance?
- Do the existing weapons and upgrades give a good answer to each type, or is a counter missing?
- With splits producing other types, do fights against Grey Goo change as they go on?
- Does performance hold, within the requirement in CLAUDE.md, with every type in large numbers?

## The Grey Goo types

The phase 3 Goo becomes the **basic goo**, the most common type. Phase 4 adds six more. Each is its own enemy type, with its own ID, folder, and definition.

**Folders.** The Grey Goo types are related, so group them under a parent folder: `enemies/grey_goo/`, with code they share there and each type in its own folder below it (`basic_goo/`, `lancer/`, `quiller/`, `spiker/`, `burster/`, `devourer/`, `asteroid_mimic/`). This moves the phase 3 Goo from `enemies/goo/` and renames its ID to `basic_goo`. Follow CLAUDE.md's rules for moving files.

**Shared traits.** Unless its section says otherwise, every Grey Goo type:

- looks like Grey Goo: the pale, glossy grey with a soft light at its edges that phase 3 settled on, but with a silhouette of its own, so each type can be told apart from the others and from asteroids;
- grows stronger over the fight and with each escalation step, as the basic goo does;
- leaves debris when destroyed;
- counts as one kill toward the kill target;
- damages the VNP by touching it, once per touch with a cooldown, as the basic goo does. Only the basic goo, the Devourer and an awake asteroid mimic drain and absorb drones that touch them; the other types reach drones only through their attacks;
- declares its threats for pilots: its body, its traits, and the danger shapes of its attacks once their tells are visible (see Pilots);
- is steered clear of by drones that aren't attacking it, as every enemy is;
- shows in the damage breakdown as its own enemy type.

### Basic goo (existing)

Unchanged from phase 3, except for splitting:

- **Splitting into other types:** each time a basic goo splits, there is about a one in three chance that one of the two halves is one of the other six types instead of another basic goo. The type is picked completely at random, each equally likely. The one in three chance was set by the designer and is a tuning value.
- **The other type is fresh:** a half that becomes another type is a new one of that type, at the fight's current difficulty, rather than taking a share of what the basic goo had grown. The basic goo half keeps its share as usual.

### Lancer

- Takes a short time to form itself into a spike, then shoots toward the VNP. The forming is clearly visible, so the player has a moment to see it coming and get out of the way.
- It aims a little ahead of a moving VNP. How well it aims rises with the fight's difficulty, in the same way the basic enemy's accuracy rose with time spent in the fight: early on it leads poorly and strays, and later it leads well.
- It damages the VNP if it hits, and hits any drones in its path as well, damaging them without absorbing them. It carries on through what it hits, damaging each target once per launch.
- After each launch, it turns back into a blob, then forms a spike and launches again.
- Between launches, it drifts after the VNP.

### Quiller

- Forms spikes and fires them in a spread, like a shotgun, so one volley can hit the VNP and several drones at once. Its many spikes are shared out among several targets, the VNP and the drones within its range, rather than all aimed at one.
- Each spike then arcs back to the main blob and is reabsorbed. It can hit things on the way back as well.
- Destroying a spike before it returns weakens the blob: the Quiller loses a set amount of health for each spike it doesn't get back.
- A spike is destroyed by Orbital Shards blocking or by any damage that touches it, though weapons don't aim at spikes. That is: Orbital Shards, Shockwave rings, mine blasts, ramming drones, and the VNP's and drones' shots that fly into it. Beams only hit what they aim at, so they don't hit spikes.

### Spiker

- Attacks by pushing out many small spikes at once, each of which can impale a drone, so one attack can catch several drones. It then draws the impaled drones back into its mass and absorbs them. An absorbed drone counts as absorbed in the damage breakdown, as with the basic goo.
- An impaled drone stops fighting while it is drawn in.
- Each drone it absorbs makes it larger and tougher, as with the basic goo, but it never splits.
- Its spikes go out toward every drone within its reach, and toward the VNP if it is within reach too. They damage the VNP but cannot impale it.
- If the Spiker is destroyed before it draws its impaled drones in, they are freed.

### Burster

- It moves faster than the other Grey Goo types.
- When destroyed, it splits into tiny blobs that arc toward the VNP.
- The tiny blobs damage what they hit and can be destroyed on the way, by the same things that destroy a Quiller's spikes.

### Devourer

- Slowly consumes anything harvestable, both asteroids and debris, growing as it does. It never splits, so it can become a very large blob, and it competes with the player for harvestable material.
- Its growth has a limit, but a large one.
- Its surface ripples at random, changing its outer shape, so a drone near it can be caught by a ripple and damaged. Staying close to a Devourer is risky even when it isn't heading for you.
- The more it has consumed, the more its debris is worth, so destroying a grown Devourer wins back some of what it took.

### Asteroid mimic

- Looks like an asteroid and takes an asteroid's place in a field. It isn't spawned around the VNP like the other types; mimics are placed in fields instead.
- **Where mimics are:** mimics come in groups. Some asteroid fields, clusters and rings alike, hold several mimics among their asteroids, while every other field is normal and holds none. This applies both around the gateway and as the VNP explores, so a fight rich in asteroids, with more fields, has more chances of a field with mimics.
- It stays inactive until the VNP or a drone comes within its aggro range, then wakes. A mimic made by a basic goo's split appears already awake.
- If a drone or the VNP tries to harvest it first, it wakes, takes a little damage from the attempt, and gives no harvest material.
- Waking is clear and sudden, with its own effect and sound, so the player sees what happened.
- **Once awake:** it acts like a basic goo, draining and absorbing drones, but never splits. It can also fire a single projectile at the nearest of the VNP or a drone. As an enemy shot, Orbital Shards blocking can stop it.

## Spawning

- In this phase, fights spawn only Grey Goo. The basic enemy from phase 1 no longer spawns, but its files stay, so it can be brought back by giving it a spawn weight again.
- Each spawned group is one Grey Goo type, picked by weight. The basic goo has the highest weight, so it stays the most common type.
- The Lancer, Quiller, Spiker, Burster, and Devourer spawn in groups like the basic goo, each with its own weight and group size. Asteroid mimics are placed in asteroid fields instead, as described above.
- Waves can include every type that spawns in groups.

With the basic enemy gone, the only enemy shots are the Quiller's spikes and the mimic's projectile. Whether Orbital Shards blocking is still worth taking is part of what this phase tests.

## Look and feedback

Each type needs a clear tell for what it is about to do:

- **Lancer:** a visible charge-up as it forms its spike, so the launch can be dodged.
- **Quiller:** its spikes visibly arc back to the blob, so the player can see that destroying them matters.
- **Spiker:** the spikes shooting out, and impaled drones visibly drawn in.
- **Burster:** something about its look that warns it will burst, such as a swollen, bubbling body.
- **Devourer:** its size, material visibly drawn in from what it consumes, and its ripples, which should be easy to see coming.
- **Asteroid mimic:** no tell while asleep, since that is its point, but a clear wake effect.

New placeholder sounds for each type's attack, the Burster bursting, the Devourer consuming, and a mimic waking. As always, they go in `res://placeholder/` and are listed in `PLACEHOLDERS.md`.

## Pilots

Batches only tell something about a Grey Goo type if pilots react to it: dodging a forming Lancer, keeping off a Devourer's ripples, shooting down returning spikes. The full game will have dozens of enemy types, each with its own thematic attacks and behaviors, so pilots need a way to handle enemy types that grows with them rather than code for each one.

So pilots are rebuilt before the Lancer (stage 2), as `docs/dev/PILOT_SPEC.md` describes:

- **Declared threats:** every enemy type declares its body, its traits, and the danger shapes of its attacks once their tells are visible.
- **Short paths:** the pilot scores short paths through those threats, so a new type needs no pilot code.
- **Fair sight:** pilots still see only what a player sees, so a sleeping asteroid mimic is an asteroid to them.

**Two pilots for comparison:** batches can then compare two pilots, one trained to play like the designer and one trained to play as well as possible. The gap shows the difficulty headroom, and the builds that win far more often in the best hands show where the balance breaks.

## Not yet decided

The designer has not settled these. Use the behavior listed, say that you did, and keep it easy to change.

- **Drones and growth for every type:** whether drones a Grey Goo type destroys without absorbing them, such as with a Lancer hit or a Quiller spike, should add to its mass and health, and whether every type, not only the basic goo, should split once it reaches a threshold. For now, drones destroyed without being absorbed add nothing, and only the basic goo splits. This also decides how much each goo's debris is worth, since a goo's mass sets the value of its debris: the more a goo's mass can grow, the more destroying it gives back.
- **The Burster's tiny blobs:** they head for the VNP and pop on whatever they hit first, the VNP or a drone. They leave no debris and don't count as kills.
- **The Devourer and drones:** it drains and absorbs drones that touch it, like the basic goo, and grows from them as well as from what it consumes.
- **The Devourer's targets:** it heads for the nearest harvestable material and ignores the player unless drones or the VNP come close.

For design questions this file does not answer, ask the designer rather than inventing a rule.

## Suggested build order

Build one stage at a time, and stop after each so the designer can play it.

1. **The Grey Goo folder and Grey Goo only:** move the phase 3 Goo into `enemies/grey_goo/basic_goo/` as the basic goo, set up the shared Grey Goo code, and stop the basic enemy spawning.
2. **Pilots that plan:** the goals and path scoring of `docs/dev/PILOT_SPEC.md` (its stages 1 and 2), replacing the current pilot once it matches or beats it in batches.
3. **Lancer:** the first type whose attack is declared; pilots should dodge a forming Lancer with no code for it.
4. **Quiller.**
5. **Spiker.**
6. **Burster.**
7. **Devourer.**
8. **Asteroid mimic.**
9. **Splitting into other types.**

Each type comes with its look, effects, and sounds, its spawn weight, and its threat declaration for pilots.

## Performance

Several types multiply: basic goos split, Bursters burst, and Quillers fill the air with spikes. Check against the performance requirement in CLAUDE.md after each stage, in a late fight with every type present.

Track each new enemy type's own performance cost as it is built, so the cost of every type can be seen on its own rather than only as part of the whole fight. When a stage adds a type, measure it alone in large numbers as well as in the full mix, and record what it costs, so a slow type is found at the stage that added it.

## Out of scope

Everything out of scope in phases 1 to 3, plus: other factions, new VNP Weapons, weapon modifiers beyond those already built, playing several fights in a row, Level Up Rewards, weapon combos, escort VNPs, music, and final art and audio.

## Stretch goals

Add these only after this phase feels good and the designer asks for them.

- Any upgrades that counter Grey Goo directly, if playtesting shows the existing pool doesn't offer a good answer to one of the types.

Next phase, planned: Tesla Coil, Cannonball, and the weapon modifiers from the GDD. After that: three fights in a row, with Level Up Rewards between them.

## Done when

- Fights spawn only Grey Goo, and every type behaves as described and can be told apart from the others at a glance.
- Basic goo splits produce other types about one time in three.
- The damage breakdown shows each type separately.
- Pilots react to each type's attacks through its threat declaration, with no pilot code for the type.
- The performance requirement in CLAUDE.md holds in a late fight with every type present.
- Each type's own performance cost has been measured and recorded at the stage that added it.
- Every new placeholder asset is in `res://placeholder/` and listed in `PLACEHOLDERS.md`.
