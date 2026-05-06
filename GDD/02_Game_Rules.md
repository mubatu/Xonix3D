# 02 - Game Rules

## Territory Types

The arena contains three main territory states.

| State | Name | Meaning |
|---|---|---|
| `Claimed` | Safe territory | Player can move safely here. Balls cannot enter this area for now. |
| `Unclaimed` | Dangerous territory | Balls move here. Player can enter this area to draw a path. |
| `TemporaryPath` | Drawing path | Created when the player moves through unclaimed territory. If hit by a ball, the player loses a life. |

Internally, these states can be represented using an enum:

```csharp
public enum CellState
{
    Unclaimed,
    Claimed,
    TemporaryPath
}
```

## Player Rules

- The player moves only in four directions.
- The player moves on the X/Z plane.
- Movement is smooth, but it snaps to grid lines/cell centers for gameplay logic.
- The player starts from a level-defined spawn point.
- The player is safe while standing on claimed territory.
- The player becomes vulnerable while drawing a path through unclaimed territory.
- The player can only draw horizontal and vertical paths.
- Diagonal drawing is not allowed.

## Ball Rules

- Balls move only inside unclaimed territory.
- Balls use scripted movement, not Rigidbody physics.
- Balls bounce when they hit claimed territory, arena boundaries, or temporary path cells.
- If a ball hits a temporary path, it first triggers player death and then bounces away from the path.
- Balls can collide with each other and bounce away from each other.
- Balls should keep a constant speed unless level design says otherwise.
- Ball speed does not increase based on captured percentage.
- Level 1 contains two balls.
- Note that new ball types can be added later.

## Death Rules

The player loses one life when:

1. A ball touches the player while the player is vulnerable.
2. A ball touches the temporary path before the player closes it.
3. The player touches its own path.

When the player dies:

- Temporary path disappears.
- Player health decreases by one.
- Player respawns at the level spawn point.
- Balls continue moving.
- The scene does not reload.
- Already claimed territory remains claimed.
- Game continues if the player still has remaining lives.

If lives reach zero:

- Game over state is triggered.

## Path Completion Rule

A path is completed when:

- The player started from claimed territory,
- moved through unclaimed territory while drawing temporary path,
- and returned to claimed territory.

When this happens:

1. The game runs the capture algorithm.
2. Captured cells become claimed.
3. Temporary path cells become claimed.
4. Captured percentage is recalculated.

Capture resolution can be instant for the first implementation. Gradual capture animation is optional polish.

## Level Completion Rule

Each level defines a required capture percentage.

For Level 1, recommended initial value:

```text
Required capture percentage: 75%
```

The level is completed when:

```text
claimed percentage >= required capture percentage
```

The initial claimed border counts toward claimed percentage.
