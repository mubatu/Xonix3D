# 03 - Core Mechanics

## Coordinate Model

The game appears in 3D, but the logic is 2D.

Unity axes:

```text
Grid X -> Unity X
Grid Y -> Unity Z
Height -> Unity Y
```

The arena is represented as a 2D grid:

```csharp
CellState[,] grid;
```

Each grid cell corresponds to a square region on the ground.

## Grid Example

Example initial level:

```text
C C C C C C C C
C U U U U U U C
C U U U U U U C
C U U U U U U C
C C C C C C C C
```

Legend:

```text
C = Claimed
U = Unclaimed
T = Temporary Path
B = Burning Path
```

The border starts as claimed. The center starts as unclaimed.

## Player Movement

The player moves in four directions:

- Up
- Down
- Left
- Right

Recommended controls:

| Input | Direction |
|---|---|
| W / Up Arrow | Forward |
| S / Down Arrow | Backward |
| A / Left Arrow | Left |
| D / Right Arrow | Right |

The player should move smoothly along grid lines or cell centers to make path drawing easier.

Recommended first implementation:

- Use grid-based logical movement for cell entry and path drawing.
- Smoothly move the player's visible object while snapping logical movement to grid lines/cell centers.
- Do not allow diagonal movement.

### Claimed Territory Movement

When the player is on `Claimed` territory:

- Movement happens only while a direction key is held.
- Releasing the key stops the player after reaching the current target cell.
- The player can freely choose any valid four-direction movement inside the arena bounds.

### Drawing Movement

When the player enters `Unclaimed` territory and starts drawing:

- The player continues moving in the active direction even if the key is released.
- Direction changes are accepted only at grid cell boundaries.
- A valid direction change must be perpendicular to the active direction.
- Same-direction inputs are ignored.
- Opposite-direction inputs are ignored to prevent immediate backtracking over the path.
- Diagonal movement is never allowed.

## Drawing Temporary Path

When the player leaves claimed territory and enters unclaimed territory:

```text
isDrawing = true
```

Every unclaimed cell the player enters becomes temporary path:

```text
Unclaimed -> TemporaryPath
```

The path remains active until:

1. The player returns to claimed territory, or
2. The player reaches the outer arena boundary while drawing, or
3. The player dies.

## Burning Path Danger

When a ball touches an active `TemporaryPath` cell, that cell becomes `BurningPath` instead of killing the player instantly:

```text
TemporaryPath -> BurningPath
```

The burning path then spreads along the ordered temporary path list in both directions. It does not spread into arbitrary neighboring unclaimed cells; it follows only the path the player has drawn.

Recommended first implementation:

- Store the active path as an ordered list of cells.
- Store burning path indices in that list.
- When a ball hits the path, ignite the closest temporary path index.
- On each spread tick, ignite `index - 1` and `index + 1` for every currently burning index.
- If a burning index reaches the player's current path cell before the player reaches claimed territory, the player dies.
- If the player reaches claimed territory first, the red cells break away and the surviving orange cells become claimed wall.

## Invalid Movement Cases

The player should not be allowed to:

- Leave the arena bounds.
- Move diagonally.
- Cross their own temporary path.
- Reverse direction while drawing a temporary path.

If the player is drawing and attempts to move beyond the grid into the outer arena boundary, the player remains inside the arena and the current path completes.

## Capture Algorithm

The recommended algorithm is **flood fill from balls**.

### Goal

When the player closes a path, determine which unclaimed cells are still reachable by balls. Any unclaimed cells not reachable by balls are captured.

### Steps

1. Treat `Claimed`, `TemporaryPath`, and `BurningPath` cells as blocked.
2. Start flood fill from each ball's current grid cell.
3. Mark every reachable `Unclaimed` cell as safe-from-capture.
4. Any `Unclaimed` cell not reached by flood fill becomes `Claimed`.
5. All `TemporaryPath` cells become `Claimed`.
6. Clear the temporary path list.
7. Recalculate captured percentage.

If the path contains `BurningPath` cells at completion, use damaged path completion instead of flood-fill capture:

1. Convert `BurningPath` cells back to `Unclaimed`.
2. Convert remaining `TemporaryPath` cells to `Claimed`.
3. Clear the temporary path and burning path lists.
4. Recalculate captured percentage.

### Pseudocode

```text
function CompletePath():
    if burningPath is not empty:
        CompleteDamagedPath()
        return

    reachable = empty boolean grid

    for each ball:
        ballCell = WorldToGrid(ball.position)
        FloodFill(ballCell, reachable)

    for each cell in grid:
        if grid[cell] == Unclaimed and reachable[cell] == false:
            grid[cell] = Claimed

    for each cell in temporaryPath:
        grid[cell] = Claimed

    temporaryPath.Clear()
    isDrawing = false
```

### Flood Fill Rules

Flood fill can move only through `Unclaimed` cells.

It cannot pass through:

- Claimed cells
- Temporary path cells
- Burning path cells
- Arena boundary outside the grid

## Ball Movement

Balls should move with a scripted velocity.

Each ball has:

```csharp
Vector2 gridDirection;
float speed;
```

The visible ball object moves in Unity X/Z according to its logical 2D direction.

Recommended first version:

- Ball position can be continuous, not cell-by-cell.
- Each frame, calculate the next position.
- Convert next position to grid coordinate.
- If the next cell is blocked, reflect the direction.

## Ball Bounce Logic

A ball should bounce when its next position would enter:

- Claimed territory
- Outside arena bounds
- Temporary path territory, after igniting the touched path cell
- Burning path territory
- Another ball

Recommended simple reflection:

```text
If blocked horizontally, reverse x direction.
If blocked vertically, reverse z direction.
If blocked in both, reverse both.
If colliding with another ball, reflect both balls away from the collision normal.
```

Balls do not get faster as captured percentage increases. Difficulty increases should come from level data, such as initial ball speed or ball count, not dynamic capture progress.

## EaterBall Claimed-Territory Damage

EaterBalls are a special ball type configured in level JSON.

When an EaterBall probes a `Claimed` cell:

1. The contacted claimed cell is marked for destruction.
2. One additional claimed cell in the bite direction is also marked when available.
3. Up to two claimed cells are converted back to `Unclaimed`.
4. Captured percentage is recalculated.
5. The contact still counts as blocked, so the EaterBall bounces away.

EaterBalls do not eat temporary path or burning path cells. Those contacts follow the normal path-hit and bounce rules.

## Path Hit Detection And Burning Spread

Each frame:

1. Probe the ball's next movement against blocked grid states.
2. If the probe touches `TemporaryPath`, notify `TerritoryManager`.
3. `TerritoryManager` ignites the related path cell as `BurningPath`.
4. If the probe is from an EaterBall against claimed territory, destroy up to two claimed cells and recalculate captured percentage.
5. The ball bounces away from the path or claimed-territory contact.
6. A timed spread coroutine expands burning path indices in both directions.
7. If burning path reaches the player's current cell, trigger player death.

This is more reliable than relying on Unity physics collisions.

## Player Hit Detection

For the prototype, player hit detection can be distance-based.

Example:

```text
if distance(player.position, ball.position) < hitRadius:
    player dies
```

Suggested rule:

- If the player is on claimed territory, the player is safe.
- If the player is drawing, a ball-player collision causes death.

## Captured Percentage

Captured percentage can be calculated as:

```text
claimed cells / total grid cells * 100
```

Recommended:

- Count the initial claimed border toward captured percentage.
- Count captured unclaimed cells and completed temporary path cells after an undamaged path closes.
- Count surviving claimed path cells after a damaged path closes, but do not award flood-fill area capture for damaged paths.
- Round the displayed percentage for the UI, even if the internal value remains a float.

Example:

```text
capturedPercentage = claimedCells / totalCells * 100
displayedCapturedPercentage = round(capturedPercentage)
```
