using System.Collections;
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
    [Tooltip("Optional model root. If empty, all renderers under the Player object are used.")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private bool overrideVisualColor = true;
    [SerializeField] private Color playerColor = new Color(0.12f, 0.44f, 1f);

    private Vector2Int currentCell;
    private Vector2Int targetCell;
    private Vector2Int activeDirection;
    private Vector2Int queuedDrawingDirection;
    private bool isMoving;
    private Renderer[] playerRenderers;
    private MaterialPropertyBlock propertyBlock;
    private Vector3 startingScale;
    private bool hasInitialized;

    public Vector2Int CurrentCell => currentCell;

    public void ConfigureSpawn(Vector2Int newSpawnCell)
    {
        spawnCell = newSpawnCell;

        if (hasInitialized && gridManager != null)
        {
            Respawn();
        }
    }

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

        CacheVisualRenderers();
        propertyBlock = new MaterialPropertyBlock();
        startingScale = transform.localScale;
        hasInitialized = true;
        ApplyColor();

        currentCell = spawnCell;
        targetCell = spawnCell;
        transform.position = GetPlayerWorldPosition(spawnCell);
        territoryManager?.HandlePlayerEnteredCell(currentCell);
    }

    private void Update()
    {
        if (gameManager != null && gameManager.IsPlayerControlLocked)
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
        transform.localScale = startingScale;
        SetVisible(true);
        ApplyColor();
    }

    public IEnumerator PlayDeathFeedback(float duration, int flashCount, float popScale)
    {
        isMoving = false;
        activeDirection = Vector2Int.zero;
        queuedDrawingDirection = Vector2Int.zero;

        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.05f, duration);
        int safeFlashCount = Mathf.Max(1, flashCount);
        float flashInterval = safeDuration / (safeFlashCount * 2f);

        transform.localScale = startingScale * Mathf.Max(1f, popScale);

        for (int i = 0; i < safeFlashCount; i++)
        {
            SetVisible(false);
            yield return new WaitForSeconds(flashInterval);
            elapsed += flashInterval;

            SetVisible(true);
            yield return new WaitForSeconds(flashInterval);
            elapsed += flashInterval;
        }

        if (elapsed < safeDuration)
        {
            yield return new WaitForSeconds(safeDuration - elapsed);
        }

        SetVisible(true);
        transform.localScale = startingScale;
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

        CellState nextState = gridManager.GetCellState(nextCell);
        if (nextState == CellState.TemporaryPath || nextState == CellState.BurningPath)
        {
            gameManager?.HandlePlayerDeath();
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
        if (playerRenderers == null || playerRenderers.Length == 0)
        {
            return;
        }

        foreach (Renderer playerRenderer in playerRenderers)
        {
            if (playerRenderer == null)
            {
                continue;
            }

            if (!overrideVisualColor)
            {
                playerRenderer.SetPropertyBlock(null);
                continue;
            }

            playerRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_Color", playerColor);
            propertyBlock.SetColor("_BaseColor", playerColor);
            playerRenderer.SetPropertyBlock(propertyBlock);
        }
    }

    private void SetVisible(bool visible)
    {
        if (playerRenderers == null)
        {
            return;
        }

        foreach (Renderer playerRenderer in playerRenderers)
        {
            if (playerRenderer != null)
            {
                playerRenderer.enabled = visible;
            }
        }
    }

    private void CacheVisualRenderers()
    {
        Transform rendererRoot = visualRoot != null ? visualRoot : transform;
        playerRenderers = rendererRoot.GetComponentsInChildren<Renderer>(true);
    }

    private static bool IsSameOrOppositeDirection(Vector2Int direction, Vector2Int otherDirection)
    {
        return otherDirection != Vector2Int.zero
            && Vector2.Dot((Vector2)direction, (Vector2)otherDirection) != 0f;
    }
}
