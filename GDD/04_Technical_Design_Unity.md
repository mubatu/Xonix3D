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
│   ├── Ball_01 (runtime instance)
│   ├── Ball_02 (runtime instance)
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
- Load level JSON files from `Assets/Resources/Levels`
- Spawn or reuse balls from a shared ball prefab according to each level JSON

Suggested fields:

```csharp
public int lives = 3;
public bool isGameOver;
public bool isLevelComplete;
public BallController ballPrefab;
public Transform ballsRoot;
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
- Track burning path cells
- Spread burning path danger along the active path
- Resolve damaged path completion
- Complete path
- Run flood fill
- Calculate captured percentage, including the initial claimed border and completed temporary path cells

Suggested fields:

```csharp
public bool isDrawing;
public List<Vector2Int> temporaryPathCells;
public HashSet<int> burningPathIndices;
public float capturedPercentage;
```

Important methods:

```csharp
void HandlePlayerEnteredCell(Vector2Int cell);
void StartDrawing(Vector2Int cell);
void AddTemporaryPathCell(Vector2Int cell);
void CompletePath();
void HandleBallTouchedPath(Vector2Int touchedCell);
void CancelTemporaryPath();
float CalculateCapturedPercentage();
```

### PlayerController

Responsibilities:

- Read input
- Move in four directions
- Move smoothly while snapping logical movement to grid lines/cell centers
- Use held-key movement while on claimed territory
- Use committed active-direction movement while drawing through unclaimed territory
- Buffer perpendicular turn input while drawing and apply it at the next grid cell boundary
- Ignore same-direction and opposite-direction input while drawing
- Notify TerritoryManager when entering a new grid cell
- Return to spawn on death

Suggested fields:

```csharp
public float moveSpeed = 5f;
public Vector3 targetWorldPosition;
public Vector2Int currentCell;
public Vector2Int spawnCell;
public Vector2Int activeDirection;
public Vector2Int queuedDrawingDirection;
```

Important methods:

```csharp
Vector2Int ReadHeldDirection();
Vector2Int ReadPressedDirection();
void QueueDrawingDirectionInput();
void MovePlayer();
void Respawn();
```

### BallController

Responsibilities:

- Move ball using scripted velocity
- Bounce from blocked cells
- Bounce from temporary path cells after notifying `TerritoryManager` to ignite the path
- Support normal ball and EaterBall behavior
- For EaterBalls, destroy up to two claimed cells on contact and then bounce away
- Use a forward wall/path probe distance to reduce visible overlap before bounce
- Bounce from other balls
- Notify GameManager if the vulnerable player is hit

Suggested fields:

```csharp
public float speed = 4f;
public Vector2 direction;
public float hitRadius = 0.5f;
public float wallProbeDistance = 0.45f;
public BallType ballType;
public Material normalBallMaterial;
public Material eaterBallMaterial;
```

Important methods:

```csharp
void MoveBall();
void CheckBounce();
void CheckBallCollision(BallController otherBall);
void NotifyPathHit(Vector2Int pathCell);
void DestroyClaimedContacts();
void CheckPlayerHit();
```

### LevelData

Levels are stored as JSON files under `Assets/Resources/Levels`.

Suggested fields:

```csharp
public class LevelData
{
    public int levelNumber;
    public int width;
    public int height;
    public float cellSize;
    public float requiredCapturePercentage;
    public LevelCell playerSpawnCell;
    public LevelClaimedArea[] initiallyClaimedAreas;
    public LevelCell[] initiallyClaimedCells;
    public LevelBallData[] balls;
}
```

Each `LevelBallData` entry also supports:

```csharp
public string ballType; // "Normal" or "Eater"
```

Shared ball visuals and default behavior should live in a `Ball` prefab. Level JSON controls each runtime ball's type, spawn cell, direction, speed, hit radius, player hit radius, and ground offset.

`BallController` should default missing or unknown `ballType` values to `Normal` so older level JSON files remain valid.

## Grid Cell State Enum

```csharp
public enum CellState
{
    Unclaimed,
    Claimed,
    TemporaryPath,
    BurningPath
}
```

## Recommended Script Communication

Use direct references for the first prototype.

Example:

```text
PlayerController -> TerritoryManager
BallController -> TerritoryManager
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
| Burning path | Red |
| Normal ball | `BallMaterial` |
| EaterBall | `EaterBallMaterial` |
| Player | Blue |
| Balls | Distinct colors |
| Arena wall | Dark teal |

## Collision Approach

Do not rely on Unity physics for core gameplay logic.

Use grid and distance checks instead:

- Ball vs wall: grid state check
- Ball vs temporary path: grid probe plus `TerritoryManager.HandleBallTouchedPath`
- Ball vs player: distance check
- Ball vs ball: distance check and scripted direction reflection

Unity colliders may still be used for visual debugging, but they should not be the source of truth.

## Camera

Initial camera:

- Fixed position
- Perspective
- Angled down toward arena
