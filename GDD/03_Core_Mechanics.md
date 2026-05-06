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
2. The player dies.

## Invalid Movement Cases

The player should not be allowed to:

- Leave the arena bounds.
- Move diagonally.
- Cross their own temporary path.

## Capture Algorithm

The recommended algorithm is **flood fill from balls**.

### Goal

When the player closes a path, determine which unclaimed cells are still reachable by balls. Any unclaimed cells not reachable by balls are captured.

### Steps

1. Treat `Claimed` cells and `TemporaryPath` cells as blocked.
2. Start flood fill from each ball's current grid cell.
3. Mark every reachable `Unclaimed` cell as safe-from-capture.
4. Any `Unclaimed` cell not reached by flood fill becomes `Claimed`.
5. All `TemporaryPath` cells become `Claimed`.
6. Clear the temporary path list.
7. Recalculate captured percentage.

### Pseudocode

```text
function CompletePath():
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
- Temporary path territory, after triggering player death
- Another ball

Recommended simple reflection:

```text
If blocked horizontally, reverse x direction.
If blocked vertically, reverse z direction.
If blocked in both, reverse both.
If colliding with another ball, reflect both balls away from the collision normal.
```

Balls do not get faster as captured percentage increases. Difficulty increases should come from level data, such as initial ball speed or ball count, not dynamic capture progress.

## Path Hit Detection

Each frame:

1. Convert each ball position to grid cell.
2. Check that cell's state.
3. If the state is `TemporaryPath`, trigger player death.
4. Bounce the ball away from the temporary path.

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
- Count captured unclaimed cells and completed temporary path cells after a path closes.
- Round the displayed percentage for the UI, even if the internal value remains a float.

Example:

```text
capturedPercentage = claimedCells / totalCells * 100
displayedCapturedPercentage = round(capturedPercentage)
```
