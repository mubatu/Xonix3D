# 3D Xonix / AirXonix-Inspired Unity Project GDD

This folder contains the initial Game Design Document package for a grid-based 3D Xonix-style game developed in Unity.

The game is visually 3D, but its core gameplay logic is based on a 2D grid projected onto Unity's X/Z plane.

## Document Set

1. [01_Game_Overview.md](01_Game_Overview.md)  
   High-level concept, target platform, goals, and core loop.

2. [02_Game_Rules.md](02_Game_Rules.md)  
   Player rules, enemy rules, death rules, level completion, and scoring/percentage logic.

3. [03_Core_Mechanics.md](03_Core_Mechanics.md)  
   Movement, drawing, temporary path behavior, capture logic, and flood-fill algorithm.

4. [04_Technical_Design_Unity.md](04_Technical_Design_Unity.md)  
   Unity architecture, scene hierarchy, scripts, data structures, and implementation notes.

5. [05_Level_Design.md](05_Level_Design.md)  
   Level 1 specification and future level expansion ideas.

6. [06_Art_UI_Audio.md](06_Art_UI_Audio.md)  
   Prototype visuals, camera, UI, materials, and optional polish phase.

7. [07_Development_Roadmap.md](07_Development_Roadmap.md)  
   Suggested implementation order for a student project.

## Current Design Decisions

- Player moves only in four directions.
- Player movement is smooth while snapping gameplay logic to grid lines/cell centers.
- Player is safe on claimed territory for now.
- Balls stay only in unclaimed territory.
- Balls bounce from temporary paths after triggering player death.
- Balls can collide with each other and bounce away from each other.
- Ball speed does not increase with captured percentage.
- When the player dies, only the temporary path disappears, health decreases by one, and the player respawns at the level spawn point.
- Level completion percentage depends on the level.
- The initial claimed border and completed temporary path cells count toward captured percentage.
- Captured percentage is displayed as a rounded value, with the required percentage visible during gameplay.
- Timer is skipped for now.
- Player can only draw horizontal and vertical paths.
- Ball movement and bounce are scripted, not physics-based.
- The game uses grid-based logic.
- Ground should eventually look like a smooth single mesh.
- Temporary path may use visible square tiles.
- Camera is fixed-position for now.
