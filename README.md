# NeuroWar Commander
**Engine:** Godot 4.4.1
**Language:** C#

## Overview

**NeuroWar Commander** is a real-time tactical simulation built to explore and compare two prominent AI paradigms—**Behaviour Tree (BT)** and **Belief-Desire-Intention (BDI)**—within a constrained RTS environment. Developed in C# using the Godot engine, the game pits two AI-controlled squads against each other under fog-of-war, unit FSMs, and camp control mechanics. The goal is to assess how each AI architecture performs tactically in dynamic and partially observable settings.

## Key Features

* **Dual AI Frameworks:** Fully implemented Behaviour Tree and BDI systems.
* **Modular Unit Architecture:** FSM-based agent control with perception, pathfinding (A\*), and steering layers.
* **Dynamic Tactical Maps:** Influence Map, Vision Map, Terrain Map, and Location Map support AI decision-making.
* **Physics-Based Combat:** Each unit type uses unique projectile mechanics and range behaviors.
* **Fog-of-War System:** Units only act based on what they can see and remember.
* **Debugging and Evaluation Tools:** Toggleable tactical overlays, event bus logging, and automated match reports.

## Units & Entities

Includes multiple unit classes like **Rifleman**, **Sniper**, **Tanker**, **Scout**, and **Siege Machine**, each with distinct stats, field-of-view mechanics, and projectile behaviors. **Camps** function as capturable objectives offering healing and strategic control.

## Tactical AI Comparison

* **BT AI**: Hierarchical, reactive logic with selectors and sequences.
* **BDI AI**: Goal-driven, deliberative agents with persistent intentions and utility-based desire selection.
* Both AIs share a common blackboard but diverge in execution and adaptability.

## Game AI Architecture
![AI Architecture](https://github.com/user-attachments/assets/7e790291-de0a-420d-8d5e-db66bab4983a)


## Project Structure

* `Scripts/`: Core game and AI logic
* `Scenes/`: Godot game scenes and UI
* `MatchLogs/`: Match results and statistics
* `Assets/`: Sprites and fonts (open-source, non-commercial)

## Demonstration

A full demo video is available showcasing comparative behaviour between BT and BDI on symmetric and asymmetric maps. See the documentation for match highlights and observed tactical differences.

## Acknowledgements

Uses freely licensed art assets from OpenGameArt contributors:

* Colony Sim Environment by Buch
* Armored Soldiers by Ragewortt

---

Let me know if you'd like this README to include badges, setup instructions, or installation notes.
