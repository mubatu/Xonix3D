using UnityEngine;

public sealed class PlayerController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] private TerritoryManager territoryManager;
    [SerializeField] private GameManager gameManager;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 7f;
    [SerializeField] private Vector2Int spawnCell = new Vector2Int(20, 0);
    [SerializeField] private float groundOffset = 1f;
    [SerializeField] private float turnSpeed = 16f;

    [Header("Visuals")]
    [SerializeField] private Color playerColor = new Color(0.12f, 0.44f, 1f);

    private Vector2Int currentCell;
    private Vector2Int targetCell;
    private Vector2Int activeDirection;
    private Vector2Int queuedDrawingDirection;
    private bool isMoving;
    private Renderer playerRenderer;
    private MaterialPropertyBlock propertyBlock;

    public Vector2Int CurrentCell => currentCell;

    private void Start()
    {
        if (gridManager == null)
        {
            gridManager = FindFirstObjectByType<GridManager>();
        }

        if (territoryManager == null)
        {
            territoryManager = FindFirstObjectByType<TerritoryManager>();
        }

        if (gameManager == null)
        {
            gameManager = FindFirstObjectByType<GameManager>();
        }

        playerRenderer = GetComponentInChildren<Renderer>();
        propertyBlock = new MaterialPropertyBlock();
        ApplyColor();

        currentCell = spawnCell;
        targetCell = spawnCell;
        transform.position = GetPlayerWorldPosition(spawnCell);
        territoryManager?.HandlePlayerEnteredCell(currentCell);
    }

    private void Update()
    {
        if (gameManager != null && gameManager.IsGameplayStopped)
        {
            return;
        }

        QueueDrawingDirectionInput();

        if (isMoving)
        {
            MoveTowardTarget();
            return;
        }

        TryStartMove(GetMoveDirection());
    }

    public void Respawn()
    {
        isMoving = false;
        activeDirection = Vector2Int.zero;
        queuedDrawingDirection = Vector2Int.zero;
        currentCell = spawnCell;
        targetCell = spawnCell;
        transform.position = GetPlayerWorldPosition(spawnCell);
    }

    private Vector2Int GetMoveDirection()
    {
        CellState currentState = gridManager.GetCellState(currentCell);
        if (currentState == CellState.Claimed)
        {
            queuedDrawingDirection = Vector2Int.zero;
            activeDirection = ReadHeldDirection();
            return activeDirection;
        }

        if (queuedDrawingDirection != Vector2Int.zero)
        {
            activeDirection = queuedDrawingDirection;
            queuedDrawingDirection = Vector2Int.zero;
        }

        return activeDirection;
    }

    private void QueueDrawingDirectionInput()
    {
        if (gridManager == null || gridManager.GetCellState(currentCell) == CellState.Claimed)
        {
            return;
        }

        Vector2Int pressedDirection = ReadPressedDirection();
        if (pressedDirection == Vector2Int.zero || IsSameOrOppositeDirection(pressedDirection, activeDirection))
        {
            return;
        }

        queuedDrawingDirection = pressedDirection;
    }

    private Vector2Int ReadHeldDirection()
    {
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
        {
            return Vector2Int.up;
        }

        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
        {
            return Vector2Int.down;
        }

        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
        {
            return Vector2Int.left;
        }

        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
        {
            return Vector2Int.right;
        }

        return Vector2Int.zero;
    }

    private Vector2Int ReadPressedDirection()
    {
        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
        {
            return Vector2Int.up;
        }

        if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
        {
            return Vector2Int.down;
        }

        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
        {
            return Vector2Int.left;
        }

        if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
        {
            return Vector2Int.right;
        }

        return Vector2Int.zero;
    }

    private void TryStartMove(Vector2Int direction)
    {
        if (direction == Vector2Int.zero || gridManager == null)
        {
            return;
        }

        Vector2Int nextCell = currentCell + direction;
        if (!gridManager.IsInsideGrid(nextCell))
        {
            return;
        }

        if (gridManager.GetCellState(nextCell) == CellState.TemporaryPath)
        {
            return;
        }

        targetCell = nextCell;
        activeDirection = direction;
        FaceDirection(direction, true);
        isMoving = true;
    }

    private void MoveTowardTarget()
    {
        Vector3 targetPosition = GetPlayerWorldPosition(targetCell);
        FaceDirection(activeDirection, false);

        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPosition,
            moveSpeed * Time.deltaTime
        );

        if (Vector3.Distance(transform.position, targetPosition) > 0.001f)
        {
            return;
        }

        transform.position = targetPosition;
        currentCell = targetCell;
        isMoving = false;
        territoryManager?.HandlePlayerEnteredCell(currentCell);
    }

    private Vector3 GetPlayerWorldPosition(Vector2Int cell)
    {
        Vector3 groundPosition = gridManager.GridToWorld(cell);
        return groundPosition + Vector3.up * groundOffset;
    }

    private void FaceDirection(Vector2Int direction, bool snap)
    {
        if (direction == Vector2Int.zero)
        {
            return;
        }

        Vector3 lookDirection = new Vector3(direction.x, 0f, direction.y);
        Quaternion targetRotation = Quaternion.LookRotation(lookDirection, Vector3.up);
        transform.rotation = snap
            ? targetRotation
            : Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
    }

    private void ApplyColor()
    {
        if (playerRenderer == null)
        {
            return;
        }

        playerRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor("_Color", playerColor);
        propertyBlock.SetColor("_BaseColor", playerColor);
        playerRenderer.SetPropertyBlock(propertyBlock);
    }

    private static bool IsSameOrOppositeDirection(Vector2Int direction, Vector2Int otherDirection)
    {
        return otherDirection != Vector2Int.zero
            && Vector2.Dot((Vector2)direction, (Vector2)otherDirection) != 0f;
    }
}
