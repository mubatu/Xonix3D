using System.Collections.Generic;
using UnityEngine;

public sealed class BallController : MonoBehaviour
{
    private static readonly List<BallController> ActiveBalls = new();

    [Header("References")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] private TerritoryManager territoryManager;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private GameManager gameManager;

    [Header("Spawn")]
    [SerializeField] private Vector2Int spawnCell = new Vector2Int(12, 24);
    [SerializeField] private float groundOffset = 0.65f;

    [Header("Movement")]
    [SerializeField] private float speed = 4f;
    [SerializeField] private Vector2 direction = new Vector2(1f, 1f);
    [SerializeField] private float hitRadius = 0.45f;
    [SerializeField] private float playerHitRadius = 0.45f;

    [Header("Visuals")]
    [SerializeField] private Color ballColor = new Color(0.92f, 0.18f, 0.18f);

    private Renderer ballRenderer;
    private MaterialPropertyBlock propertyBlock;

    public Vector2 Direction => direction;
    public float HitRadius => hitRadius;

    private void Awake()
    {
        if (gridManager == null)
        {
            gridManager = FindFirstObjectByType<GridManager>();
        }

        if (territoryManager == null)
        {
            territoryManager = FindFirstObjectByType<TerritoryManager>();
        }

        if (playerController == null)
        {
            playerController = FindFirstObjectByType<PlayerController>();
        }

        if (gameManager == null)
        {
            gameManager = FindFirstObjectByType<GameManager>();
        }

        ballRenderer = GetComponentInChildren<Renderer>();
        propertyBlock = new MaterialPropertyBlock();
        NormalizeDirection();
        ApplyColor();
    }

    private void OnEnable()
    {
        if (!ActiveBalls.Contains(this))
        {
            ActiveBalls.Add(this);
        }
    }

    private void Start()
    {
        transform.position = GetBallWorldPosition(spawnCell);
    }

    private void Update()
    {
        if (gameManager != null && gameManager.IsGameOver)
        {
            return;
        }

        MoveBall();
        CheckBallCollisions();
        CheckPlayerHit();
    }

    private void OnDisable()
    {
        ActiveBalls.Remove(this);
    }

    private void OnValidate()
    {
        NormalizeDirection();
    }

    private void MoveBall()
    {
        if (gridManager == null)
        {
            return;
        }

        Vector3 currentPosition = transform.position;
        Vector3 frameMovement = new Vector3(direction.x, 0f, direction.y) * (speed * Time.deltaTime);
        Vector3 nextPosition = currentPosition + frameMovement;

        CellState stateX = GetCellStateAt(new Vector3(nextPosition.x, currentPosition.y, currentPosition.z));
        CellState stateZ = GetCellStateAt(new Vector3(currentPosition.x, currentPosition.y, nextPosition.z));
        CellState stateDiagonal = GetCellStateAt(nextPosition);

        bool blockedX = stateX != CellState.Unclaimed;
        bool blockedZ = stateZ != CellState.Unclaimed;
        bool blockedDiagonal = stateDiagonal != CellState.Unclaimed;

        if (stateX == CellState.TemporaryPath
            || stateZ == CellState.TemporaryPath
            || stateDiagonal == CellState.TemporaryPath)
        {
            gameManager?.HandlePlayerDeath();
        }

        if (blockedX)
        {
            direction.x *= -1f;
        }

        if (blockedZ)
        {
            direction.y *= -1f;
        }

        if (!blockedX && !blockedZ && blockedDiagonal)
        {
            direction *= -1f;
        }

        NormalizeDirection();

        Vector3 correctedMovement = new Vector3(direction.x, 0f, direction.y) * (speed * Time.deltaTime);
        Vector3 correctedPosition = currentPosition + correctedMovement;
        if (GetCellStateAt(correctedPosition) == CellState.Unclaimed)
        {
            transform.position = correctedPosition;
        }
    }

    private void CheckBallCollisions()
    {
        foreach (BallController otherBall in ActiveBalls)
        {
            if (otherBall == this)
            {
                continue;
            }

            Vector2 toOther = GetXZ(otherBall.transform.position) - GetXZ(transform.position);
            float minDistance = hitRadius + otherBall.hitRadius;
            if (toOther.sqrMagnitude > minDistance * minDistance)
            {
                continue;
            }

            Vector2 normal = toOther.sqrMagnitude > 0.0001f ? toOther.normalized : Vector2.right;
            direction = Vector2.Reflect(direction, -normal).normalized;
            otherBall.direction = Vector2.Reflect(otherBall.direction, normal).normalized;

            float overlap = minDistance - toOther.magnitude;
            Vector3 separation = new Vector3(normal.x, 0f, normal.y) * (overlap * 0.5f);
            transform.position -= separation;
            otherBall.transform.position += separation;
        }
    }

    private void CheckPlayerHit()
    {
        if (territoryManager == null || playerController == null || gameManager == null)
        {
            return;
        }

        if (!territoryManager.IsDrawing)
        {
            return;
        }

        float hitDistance = hitRadius + playerHitRadius;
        Vector2 toPlayer = GetXZ(playerController.transform.position) - GetXZ(transform.position);
        if (toPlayer.sqrMagnitude <= hitDistance * hitDistance)
        {
            gameManager.HandlePlayerDeath();
        }
    }

    private CellState GetCellStateAt(Vector3 worldPosition)
    {
        Vector2Int cell = gridManager.WorldToGrid(worldPosition);
        return gridManager.GetCellState(cell);
    }

    private Vector3 GetBallWorldPosition(Vector2Int cell)
    {
        Vector3 groundPosition = gridManager.GridToWorld(cell);
        return groundPosition + Vector3.up * groundOffset;
    }

    private void NormalizeDirection()
    {
        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = new Vector2(1f, 1f);
        }

        direction.Normalize();
    }

    private void ApplyColor()
    {
        if (ballRenderer == null)
        {
            return;
        }

        ballRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor("_Color", ballColor);
        propertyBlock.SetColor("_BaseColor", ballColor);
        ballRenderer.SetPropertyBlock(propertyBlock);
    }

    private static Vector2 GetXZ(Vector3 position)
    {
        return new Vector2(position.x, position.z);
    }
}
