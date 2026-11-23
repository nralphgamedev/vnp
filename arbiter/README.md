# Arbiter

A small library for agents that perceive what is around them and choose what to do. It knows nothing of the game
that uses it, or of the engine: it uses plain .NET types (`System.Numerics` for vectors), and takes its random
numbers from the game, so a game's fights stay repeatable.

It was built for the computer pilots that fly VNP's ships (`docs/dev/PILOT_SPEC.md` in VNP), but it is meant for
other behavior and other games too: anything that has to weigh options, such as where to move, what to attack or
what to pick, in a world with things that can hurt it.

## What belongs here

- **Perception:** a common description of the world around an agent, which the game fills in each decision:
  - **bodies:** the things around it, with their position, velocity, size and tags whose meaning the game decides;
  - **hazards:** areas that will hurt, with their shape (circle, capsule, cone or ring), how they move, when they
    land, how long they last, how much they hurt and whom (the agent, its allies, or both);
  - **zones:** where the agent's own effects work: its reach, or traps it has left.
- **Decisions** (planned): options scored by weighted terms, with skill settings, a record of each choice and an
  explanation.
- **Motion planning** (planned): candidate paths on a plane, scored against the hazards along them.

## What doesn't

Anything about one game: its units, weapons, resources or rules, and any word for them. The game describes its
world to Arbiter in Arbiter's terms, and keeps its own goals and scoring terms in its own code. If a change here
only makes sense for one game, it belongs in that game.

## Building

`Arbiter.csproj` is an ordinary .NET 8 class library. In VNP, `VNP.csproj` references it, and a `.gdignore` keeps
Godot from importing this folder.
