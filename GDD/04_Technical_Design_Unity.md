# 04 - Technical Design for Unity

## Unity Version

- Unity 2022.3.62f3

## Scene Structure

Recommended scene hierarchy:

```text
LevelScene
├── GameManager
├── GridManager
├── TerritoryManager
├── Player
├── Balls
│   ├── Ball_01
│   └── Ball_02
│   └── ...
├── Main Camera (default for 3D Unity projects)
├── Directional Light (default for 3D Unity projects)
├── UI
└── Visuals
```

## Main Scripts

### GameManager

Responsibilities:

- Store current game state
- Store lives
- Handle death
- Handle game over
- Handle level complete
- Coordinate reset after death

Suggested fields:

```csharp
public int lives = 3;
public bool isGameOver;
public bool isLevelComplete;
```

### GridManager

Responsibilities:

- Create and store grid data
- Convert world position to grid coordinate
- Convert grid coordinate to world position
- Provide cell state information
- Update visual representation of cells

Suggested fields:

```csharp
public int width = 40;
public int height = 30;
public float cellSize = 1f;
public CellState[,] grid;
```

Important methods:

```csharp
Vector2Int WorldToGrid(Vector3 worldPosition);
Vector3 GridToWorld(Vector2Int gridPosition);
bool IsInsideGrid(Vector2Int cell);
CellState GetCellState(Vector2Int cell);
void SetCellState(Vector2Int cell, CellState state);
```

### TerritoryManager

Responsibilities:

- Track whether the player is drawing
- Store temporary path cells
- Complete path
- Run flood fill
- Calculate captured percentage, including the initial claimed border and completed temporary path cells

Suggested fields:

```csharp
public bool isDrawing;
public List<Vector2Int> temporaryPathCells;
public float capturedPercentage;
```

Important methods:

```csharp
void HandlePlayerEnteredCell(Vector2Int cell);
void StartDrawing(Vector2Int cell);
void AddTemporaryPathCell(Vector2Int cell);
void CompletePath();
void CancelTemporaryPath();
float CalculateCapturedPercentage();
```

### PlayerController

Responsibilities:

- Read input
- Move in four directions
- Move smoothly while snapping logical movement to grid lines/cell centers
- Notify TerritoryManager when entering a new grid cell
- Return to spawn on death

Suggested fields:

```csharp
public float moveSpeed = 5f;
public Vector3 targetWorldPosition;
public Vector2Int currentCell;
public Vector2Int spawnCell;
public Vector2Int currentDirection;
```

Important methods:

```csharp
void ReadInput();
void MovePlayer();
void Respawn();
```

### BallController

Responsibilities:

- Move ball using scripted velocity
- Bounce from blocked cells
- Bounce from temporary path cells after triggering player death
- Bounce from other balls
- Notify GameManager if temporary path or player is hit

Suggested fields:

```csharp
public float speed = 4f;
public Vector2 direction;
public float hitRadius = 0.5f;
```

Important methods:

```csharp
void MoveBall();
void CheckBounce();
void CheckBallCollision(BallController otherBall);
void CheckTemporaryPathHit();
void CheckPlayerHit();
```

### LevelData

A ScriptableObject is recommended for level settings.

Suggested fields:

```csharp
[CreateAssetMenu(menuName = "Xonix/Level Data")]
public class LevelData : ScriptableObject
{
    public int width;
    public int height;
    public float requiredCapturePercentage;
    public Vector2Int playerSpawnCell;
    public List<Vector2Int> ballSpawnCells;
    public List<Vector2> ballInitialDirections;
    public float ballSpeed;
    public bool scaleBallSpeedWithCapturedPercentage = false;
}
```

## Grid Cell State Enum

```csharp
public enum CellState
{
    Unclaimed,
    Claimed,
    TemporaryPath
}
```

## Recommended Script Communication

Use direct references for the first prototype.

Example:

```text
PlayerController -> TerritoryManager
BallController -> GameManager
BallController -> GridManager
TerritoryManager -> GridManager
GameManager -> PlayerController
```

Avoid building a complex event system at the start.

## Visual Ground Strategy

The logic is grid-based, but the ground can be displayed as a smooth single mesh.

Recommended development plan:

### Phase 1

Use visible square tiles for all cells.

This helps debugging.

### Phase 2

Use a smooth mesh for claimed/unclaimed regions.

Keep temporary path cells visible as square tiles.

### Phase 3

Generate clean procedural meshes for territory regions.

## Suggested Materials

| Element | Prototype Material |
|---|---|
| Claimed territory | Green |
| Unclaimed territory | Gray |
| Temporary path | Orange |
| Player | Blue |
| Balls | Distinct colors |
| Arena wall | Dark teal |

## Collision Approach

Do not rely on Unity physics for core gameplay logic.

Use grid and distance checks instead:

- Ball vs wall: grid state check
- Ball vs temporary path: grid state check
- Ball vs player: distance check
- Ball vs ball: distance check and scripted direction reflection

Unity colliders may still be used for visual debugging, but they should not be the source of truth.

## Camera

Initial camera:

- Fixed position
- Orthographic or perspective
- Angled down toward arena
