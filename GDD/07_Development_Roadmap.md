# 07 - Development Roadmap

## Phase 1 - Basic Scene Setup

Goal: Create a playable empty arena.

Tasks:

- Create Unity project.
- Create Level 1 scene.
- Add fixed camera.
- Add lighting.
- Add ground plane or temporary tiles.
- Create basic materials.

Deliverable:

- A visible rectangular arena in Unity.

## Phase 2 - Grid System

Goal: Create the logical grid.

Tasks:

- Implement `CellState` enum.
- Implement `GridManager`.
- Create grid dimensions.
- Initialize border as claimed.
- Initialize center as unclaimed.
- Display tiles based on cell state.

Deliverable:

- A grid where claimed and unclaimed cells are visually different.

## Phase 3 - Player Movement

Goal: Add the player and four-direction movement.

Tasks:

- Add player object.
- Implement `PlayerController`.
- Move only up/down/left/right.
- Move smoothly while snapping gameplay logic to grid lines/cell centers.
- On claimed territory, move only while a direction key is held.
- In unclaimed territory, continue moving in the active drawing direction after key release.
- While drawing, allow only perpendicular turns and ignore same-direction or opposite-direction input.
- Keep player inside arena bounds.
- Convert player world position to grid cell.
- Detect when player enters a new cell.

Deliverable:

- Player can move around claimed territory with held input and move through unclaimed territory using committed drawing movement.

## Phase 4 - Temporary Path Drawing

Goal: Allow player to draw paths.

Tasks:

- Implement `TerritoryManager`.
- Detect when player leaves claimed territory.
- Mark unclaimed cells as temporary path.
- Store temporary path cells.
- Show path cells visually.

Deliverable:

- Player can draw a visible path through unclaimed territory.

## Phase 5 - Ball Movement

Goal: Add two scripted balls.

Tasks:

- Add ball objects.
- Create a reusable `Ball` prefab for shared visuals and default movement settings.
- Implement `BallController`.
- Give each ball initial direction and speed.
- Move balls continuously.
- Bounce balls from claimed cells and arena bounds.
- Bounce balls from temporary path cells after notifying the path danger system.
- Bounce balls away from each other when they collide.
- Keep balls inside unclaimed territory.

Deliverable:

- Two balls move and bounce inside the unclaimed area, including ball-to-ball collisions.

## Phase 6 - Death Conditions

Goal: Implement losing lives.

Tasks:

- Detect ball hitting temporary path.
- Turn hit temporary path cells into red burning path cells.
- Spread burning path danger along the active path in both directions.
- Kill the player if burning path reaches them before path completion.
- Detect ball hitting player while drawing.
- Implement `GameManager.HandlePlayerDeath()`.
- Remove temporary path on death.
- Decrease lives.
- Respawn player at level spawn.
- Keep balls moving without reloading scene.

Deliverable:

- Player loses life correctly and respawns without scene reload.

## Phase 7 - Area Capture

Goal: Capture territory when path closes.

Tasks:

- Detect when player returns to claimed territory.
- Run flood fill from ball cells.
- Convert unreachable unclaimed cells to claimed.
- Convert temporary path to claimed.
- If the path is damaged by burning cells, convert only surviving orange path cells to claimed and break red cells back to unclaimed.
- Recalculate captured percentage, including the initial claimed border.
- Update visuals instantly for the first implementation.

Deliverable:

- Player can capture territory by completing paths.

## Phase 8 - Level Completion

Goal: Finish Level 1 when enough area is captured.

Tasks:

- Add required capture percentage to level data.
- Check level completion after every capture.
- Show rounded captured percentage and required capture percentage during gameplay.
- Show Level Complete UI.

Deliverable:

- Level ends when target percentage is reached.

## Phase 9 - Visual Improvement

Goal: Make the game look better after mechanics work.

Tasks:

- Replace full tile ground with smoother mesh if desired.
- Keep path tiles visible.
- Improve materials.
- Add simple effects.
- Improve camera framing.

Deliverable:

- Game starts to look closer to a polished 3D arcade game.

## Phase 10 - Polish and Extensions

Possible tasks:

- Add main menu.
- Add restart button.
- Add sound effects.
- Add more levels.
- Add claimed-territory enemies.
- Add power-ups.
- Add scoring.
- Add timer.
