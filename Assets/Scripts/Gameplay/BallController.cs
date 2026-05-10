using System.Collections.Generic;
using UnityEngine;

public enum BallType
{
    Normal,
    Eater
}

public sealed class BallController : MonoBehaviour
{
    private static readonly List<BallController> ActiveBalls = new();
    private const int EaterDestroyedCellCount = 2;

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
    [SerializeField] private float wallProbeDistance = 0.45f;
    [SerializeField] private BallType ballType = BallType.Normal;

    [Header("Visuals")]
    [SerializeField] private Material normalBallMaterial;
    [SerializeField] private Material eaterBallMaterial;
    [SerializeField] private bool overrideBallColor;
    [SerializeField] private Color ballColor = new Color(0.92f, 0.18f, 0.18f);
    [SerializeField] private bool createTrail = true;
    [SerializeField] private float trailTime = 0.35f;
    [SerializeField] private float trailStartWidth = 0.28f;
    [SerializeField] private float trailEndWidth = 0.02f;
    [SerializeField] private float visualRadius = 0.5f;

    private Renderer ballRenderer;
    private MaterialPropertyBlock propertyBlock;
    private Vector2 initialDirection;
    private TrailRenderer trailRenderer;

    public Vector2 Direction => direction;
    public float HitRadius => hitRadius;
    public Vector2Int CurrentCell => gridManager != null ? gridManager.WorldToGrid(transform.position) : spawnCell;
    public BallType BallType => ballType;
    public bool IsEater => ballType == BallType.Eater;

    private readonly struct ProbeCollisionInfo
    {
        public ProbeCollisionInfo(bool isBlocked, bool touchedPath, Vector2Int pathCell, List<Vector2Int> claimedContacts)
        {
            IsBlocked = isBlocked;
            TouchedPath = touchedPath;
            PathCell = pathCell;
            ClaimedContacts = claimedContacts;
        }

        public bool IsBlocked { get; }
        public bool TouchedPath { get; }
        public Vector2Int PathCell { get; }
        public List<Vector2Int> ClaimedContacts { get; }
    }

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
        normalBallMaterial ??= ballRenderer != null ? ballRenderer.sharedMaterial : null;
        propertyBlock = new MaterialPropertyBlock();
        NormalizeDirection();
        initialDirection = direction;
        ApplyVisualStyle();
        EnsureTrail();
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
        if (gameManager != null && gameManager.IsGameplayStopped)
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

    public void ResetBall()
    {
        direction = initialDirection;
        NormalizeDirection();
        transform.position = GetBallWorldPosition(spawnCell);
        trailRenderer?.Clear();
    }

    public void ConfigureFromLevel(LevelBallData ballData)
    {
        if (ballData == null)
        {
            return;
        }

        if (ballData.spawnCell != null)
        {
            spawnCell = ballData.spawnCell.ToVector2Int();
        }

        if (ballData.direction != null)
        {
            direction = ballData.direction.ToVector2();
        }

        ballType = ParseBallType(ballData.ballType);
        speed = Mathf.Max(0.1f, ballData.speed);
        hitRadius = Mathf.Max(0.01f, ballData.hitRadius);
        playerHitRadius = Mathf.Max(0.01f, ballData.playerHitRadius);
        groundOffset = ballData.groundOffset;
        NormalizeDirection();
        initialDirection = direction;

        if (gridManager != null)
        {
            transform.position = GetBallWorldPosition(spawnCell);
        }

        ApplyVisualStyle();
        trailRenderer?.Clear();
    }

    private void MoveBall()
    {
        if (gridManager == null)
        {
            return;
        }

        float frameDistance = speed * Time.deltaTime;
        if (frameDistance <= 0f)
        {
            return;
        }

        float maxStepDistance = Mathf.Max(0.05f, gridManager.CellSize * 0.25f);
        int stepCount = Mathf.Max(1, Mathf.CeilToInt(frameDistance / maxStepDistance));
        float stepDistance = frameDistance / stepCount;

        for (int i = 0; i < stepCount; i++)
        {
            Vector3 stepMovement = new Vector3(direction.x, 0f, direction.y) * stepDistance;
            MoveBallStep(stepMovement);
        }
    }

    private void MoveBallStep(Vector3 movement)
    {
        if (movement.sqrMagnitude < 0.000001f)
        {
            return;
        }

        Vector3 currentPosition = transform.position;
        ProbeCollisionInfo currentCollision = GetProbeCollisionAt(currentPosition);
        if (currentCollision.TouchedPath)
        {
            territoryManager?.HandleBallTouchedPath(currentCollision.PathCell);
        }
        DestroyClaimedContacts(currentCollision);

        Vector3 nextPosition = currentPosition + movement;
        ProbeCollisionInfo nextCollision = GetProbeCollisionAt(nextPosition);
        if (!nextCollision.IsBlocked)
        {
            transform.position = nextPosition;
            RollVisual(movement);
            return;
        }

        if (nextCollision.TouchedPath)
        {
            territoryManager?.HandleBallTouchedPath(nextCollision.PathCell);
        }

        Vector3 lastSafePosition = FindLastSafePosition(currentPosition, movement);
        Vector3 actualMovement = lastSafePosition - currentPosition;
        if (actualMovement.sqrMagnitude > 0.000001f)
        {
            transform.position = lastSafePosition;
            RollVisual(actualMovement);
        }

        ReflectFromCollision(currentPosition, movement);
        DestroyClaimedContacts(nextCollision);
    }

    private void ReflectFromCollision(Vector3 currentPosition, Vector3 movement)
    {
        ProbeCollisionInfo xCollision = GetProbeCollisionAt(currentPosition + new Vector3(movement.x, 0f, 0f), true, false);
        ProbeCollisionInfo zCollision = GetProbeCollisionAt(currentPosition + new Vector3(0f, 0f, movement.z), false, true);

        if (xCollision.TouchedPath)
        {
            territoryManager?.HandleBallTouchedPath(xCollision.PathCell);
        }

        if (zCollision.TouchedPath)
        {
            territoryManager?.HandleBallTouchedPath(zCollision.PathCell);
        }

        bool blockedX = xCollision.IsBlocked;
        bool blockedZ = zCollision.IsBlocked;

        if (blockedX)
        {
            direction.x *= -1f;
        }

        if (blockedZ)
        {
            direction.y *= -1f;
        }

        if (!blockedX && !blockedZ)
        {
            direction *= -1f;
        }

        NormalizeDirection();
    }

    private Vector3 FindLastSafePosition(Vector3 startPosition, Vector3 movement)
    {
        float safeT = 0f;
        float blockedT = 1f;

        for (int i = 0; i < 8; i++)
        {
            float testT = (safeT + blockedT) * 0.5f;
            Vector3 testPosition = startPosition + movement * testT;
            if (GetProbeCollisionAt(testPosition).IsBlocked)
            {
                blockedT = testT;
            }
            else
            {
                safeT = testT;
            }
        }

        return startPosition + movement * safeT;
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

    private ProbeCollisionInfo GetProbeCollisionAt(Vector3 worldPosition, bool checkX = true, bool checkZ = true)
    {
        bool isBlocked = false;
        bool touchedPath = false;
        Vector2Int pathCell = Vector2Int.zero;
        List<Vector2Int> claimedContacts = null;
        float probeDistance = GetWallProbeDistance();

        if (checkX && Mathf.Abs(direction.x) > 0.0001f)
        {
            Vector2Int xCell = GetCellAt(worldPosition + new Vector3(Mathf.Sign(direction.x) * probeDistance, 0f, 0f));
            CellState xState = gridManager.GetCellState(xCell);
            Vector2Int biteDirection = new Vector2Int(GetDirectionSign(direction.x), 0);
            CaptureProbeContact(xState, xCell, biteDirection, ref touchedPath, ref pathCell, ref claimedContacts, ref isBlocked);
        }

        if (checkZ && Mathf.Abs(direction.y) > 0.0001f)
        {
            Vector2Int zCell = GetCellAt(worldPosition + new Vector3(0f, 0f, Mathf.Sign(direction.y) * probeDistance));
            CellState zState = gridManager.GetCellState(zCell);
            Vector2Int biteDirection = new Vector2Int(0, GetDirectionSign(direction.y));
            CaptureProbeContact(zState, zCell, biteDirection, ref touchedPath, ref pathCell, ref claimedContacts, ref isBlocked);
        }

        if (checkX && checkZ && Mathf.Abs(direction.x) > 0.0001f && Mathf.Abs(direction.y) > 0.0001f)
        {
            Vector3 diagonalProbe = worldPosition + new Vector3(
                Mathf.Sign(direction.x) * probeDistance,
                0f,
                Mathf.Sign(direction.y) * probeDistance
            );
            Vector2Int diagonalCell = GetCellAt(diagonalProbe);
            CellState diagonalState = gridManager.GetCellState(diagonalCell);
            Vector2Int biteDirection = new Vector2Int(GetDirectionSign(direction.x), GetDirectionSign(direction.y));
            CaptureProbeContact(diagonalState, diagonalCell, biteDirection, ref touchedPath, ref pathCell, ref claimedContacts, ref isBlocked);
        }

        return new ProbeCollisionInfo(isBlocked, touchedPath, pathCell, claimedContacts);
    }

    private Vector2Int GetCellAt(Vector3 worldPosition)
    {
        return gridManager.WorldToGrid(worldPosition);
    }

    private void CaptureProbeContact(
        CellState cellState,
        Vector2Int cell,
        Vector2Int biteDirection,
        ref bool touchedPath,
        ref Vector2Int pathCell,
        ref List<Vector2Int> claimedContacts,
        ref bool isBlocked)
    {
        if (cellState == CellState.Unclaimed)
        {
            return;
        }

        if (cellState == CellState.Claimed && IsEater && gridManager.IsInsideGrid(cell))
        {
            AddClaimedContact(cell, ref claimedContacts);
            AddClaimedContact(cell + biteDirection, ref claimedContacts);
            isBlocked = true;
            return;
        }

        isBlocked = true;

        if (cellState == CellState.TemporaryPath || cellState == CellState.BurningPath)
        {
            if (!touchedPath)
            {
                pathCell = cell;
            }

            touchedPath = true;
        }
    }

    private void DestroyClaimedContacts(ProbeCollisionInfo collisionInfo)
    {
        if (!IsEater || collisionInfo.ClaimedContacts == null || collisionInfo.ClaimedContacts.Count == 0 || gridManager == null)
        {
            return;
        }

        List<Vector2Int> destroyedCells = new();
        foreach (Vector2Int cell in collisionInfo.ClaimedContacts)
        {
            if (destroyedCells.Count >= EaterDestroyedCellCount)
            {
                break;
            }

            if (!gridManager.IsInsideGrid(cell) || gridManager.GetCellState(cell) != CellState.Claimed)
            {
                continue;
            }

            gridManager.SetCellState(cell, CellState.Unclaimed);
            destroyedCells.Add(cell);
        }

        territoryManager?.HandleClaimedCellsDestroyed(destroyedCells);
    }

    private static void AddClaimedContact(Vector2Int cell, ref List<Vector2Int> claimedContacts)
    {
        claimedContacts ??= new List<Vector2Int>();
        if (!claimedContacts.Contains(cell))
        {
            claimedContacts.Add(cell);
        }
    }

    private float GetWallProbeDistance()
    {
        return Mathf.Clamp(wallProbeDistance, 0.01f, gridManager.CellSize * 0.49f);
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

    private void ApplyVisualStyle()
    {
        if (ballRenderer == null)
        {
            return;
        }

        Material selectedMaterial = IsEater ? eaterBallMaterial : normalBallMaterial;
        if (selectedMaterial != null)
        {
            ballRenderer.sharedMaterial = selectedMaterial;
        }

        if (!overrideBallColor)
        {
            ballRenderer.SetPropertyBlock(null);
            return;
        }

        ballRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor("_Color", ballColor);
        propertyBlock.SetColor("_BaseColor", ballColor);
        ballRenderer.SetPropertyBlock(propertyBlock);
    }

    private static BallType ParseBallType(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return BallType.Normal;
        }

        return System.Enum.TryParse(typeName, true, out BallType parsedType) ? parsedType : BallType.Normal;
    }

    private void EnsureTrail()
    {
        if (!createTrail)
        {
            return;
        }

        trailRenderer = GetComponent<TrailRenderer>();
        if (trailRenderer == null)
        {
            trailRenderer = gameObject.AddComponent<TrailRenderer>();
        }

        trailRenderer.time = trailTime;
        trailRenderer.startWidth = trailStartWidth;
        trailRenderer.endWidth = trailEndWidth;
        trailRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trailRenderer.receiveShadows = false;

        Material trailMaterial = CreateTrailMaterial();
        if (trailMaterial != null)
        {
            trailRenderer.material = trailMaterial;
        }

        Color startColor = ballColor;
        startColor.a = 0.65f;
        Color endColor = ballColor;
        endColor.a = 0f;
        trailRenderer.startColor = startColor;
        trailRenderer.endColor = endColor;
    }

    private void RollVisual(Vector3 movement)
    {
        if (ballRenderer == null || movement.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Vector3 axis = Vector3.Cross(Vector3.up, movement.normalized);
        float degrees = movement.magnitude / Mathf.Max(0.01f, visualRadius) * Mathf.Rad2Deg;
        ballRenderer.transform.Rotate(axis, degrees, Space.World);
    }

    private static Material CreateTrailMaterial()
    {
        Shader shader = Shader.Find("Sprites/Default");
        shader ??= Shader.Find("Particles/Standard Unlit");
        shader ??= Shader.Find("Unlit/Color");
        shader ??= Shader.Find("Standard");

        return shader != null ? new Material(shader) : null;
    }

    private static Vector2 GetXZ(Vector3 position)
    {
        return new Vector2(position.x, position.z);
    }

    private static int GetDirectionSign(float value)
    {
        return value >= 0f ? 1 : -1;
    }
}
