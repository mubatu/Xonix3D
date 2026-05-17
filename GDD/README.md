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
- On claimed territory, player moves only while a direction key is held.
- While drawing through unclaimed territory, player keeps moving in the active direction until a valid turn is made or claimed territory is reached.
- While drawing, player can turn only perpendicular to the active direction; same-direction and opposite-direction inputs are ignored.
- Player movement is smooth while snapping gameplay logic to grid lines/cell centers.
- Player is safe on claimed territory for now.
- Balls stay only in unclaimed territory.
- Balls bounce from temporary paths after igniting a red burning path danger.
- Burning path spreads along the active path in both directions and kills the player only if it reaches them before path completion.
- Balls can collide with each other and bounce away from each other.
- Ball speed does not increase with captured percentage.
- When the player dies, only the temporary path disappears, health decreases by one, and the player respawns at the level spawn point.
- Level completion percentage depends on the level.
- The initial claimed border and completed temporary path cells count toward captured percentage.
- Damaged paths do not award flood-fill capture; red cells break back to unclaimed and surviving orange path cells become claimed.
- Captured percentage is displayed as a rounded value, with the required percentage visible during gameplay.
- Levels can define a countdown timer; current playable levels use 60 seconds.
- Player can only draw horizontal and vertical paths.
- Ball movement and bounce use Unity Rigidbody physics.
- Blocked grid cells generate ball physics colliders; adjacent blocked cells in the same row are merged into larger horizontal BoxColliders for performance.
- Runtime balls should use a shared prefab, while level JSON controls spawn, direction, speed, and radius settings.
- The game uses grid-based logic.
- Ground should eventually look like a smooth single mesh.
- Temporary path may use visible square tiles.
- Camera is fixed-position for now.
