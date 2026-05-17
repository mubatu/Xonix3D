using System.Collections.Generic;
using UnityEngine;

public enum BallType
{
    Normal,
    Eater
}

public sealed class BallController : MonoBehaviour
{
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
    [SerializeField] private BallType ballType = BallType.Normal;

    [Header("Physics")]
    [SerializeField] private float mass = 1f;
    [SerializeField] private float bounciness = 1f;
    [SerializeField] private CollisionDetectionMode collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

    [Header("Visuals")]
    [SerializeField] private Material normalBallMaterial;
    [SerializeField] private Material eaterBallMaterial;
    [SerializeField] private bool overrideBallColor;
    [SerializeField] private Color ballColor = new Color(0.92f, 0.18f, 0.18f);
    [SerializeField] private bool createTrail = true;
    [SerializeField] private float trailTime = 0.35f;
    [SerializeField] private float trailStartWidth = 0.28f;
    [SerializeField] private float trailEndWidth = 0.02f;

    private Renderer ballRenderer;
    private Rigidbody ballRigidbody;
    private SphereCollider ballCollider;
    private PhysicMaterial runtimePhysicsMaterial;
    private MaterialPropertyBlock propertyBlock;
    private Vector2 initialDirection;
    private TrailRenderer trailRenderer;
    private bool wasGameplayStopped = true;

    public Vector2 Direction => direction;
    public float HitRadius => hitRadius;
    public Vector2Int CurrentCell => gridManager != null ? gridManager.WorldToGrid(transform.position) : spawnCell;
    public BallType BallType => ballType;
    public bool IsEater => ballType == BallType.Eater;

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
        EnsurePhysicsComponents();
        ApplyColliderRadius();
        ApplyVisualStyle();
        EnsureTrail();
    }

    private void Start()
    {
        transform.position = GetBallWorldPosition(ResolveSpawnCell(spawnCell));
        ResetPhysicsMotion();
    }

    private void Update()
    {
        if (gameManager != null && gameManager.IsGameplayStopped)
        {
            return;
        }

        CheckPlayerHit();
    }

    private void FixedUpdate()
    {
        EnsurePhysicsComponents();

        if (gameManager != null && gameManager.IsGameplayStopped)
        {
            StopPhysicsMotion();
            wasGameplayStopped = true;
            return;
        }

        if (wasGameplayStopped)
        {
            ResetPhysicsMotion();
            wasGameplayStopped = false;
        }

        MaintainFixedPhysicsSpeed();
        ApplyRollingAngularVelocity();
        SyncDirectionFromVelocity();
    }

    private void OnValidate()
    {
        NormalizeDirection();
        ApplyColliderRadius();
    }

    public void ResetBall()
    {
        direction = initialDirection;
        NormalizeDirection();
        transform.position = GetBallWorldPosition(ResolveSpawnCell(spawnCell));
        ResetPhysicsMotion();
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
            spawnCell = ResolveSpawnCell(ballData.spawnCell.ToVector2Int());
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
        ApplyColliderRadius();

        if (gridManager != null)
        {
            transform.position = GetBallWorldPosition(ResolveSpawnCell(spawnCell));
        }

        ResetPhysicsMotion();
        ApplyVisualStyle();
        trailRenderer?.Clear();
    }

    private void EnsurePhysicsComponents()
    {
        if (ballRigidbody == null)
        {
            ballRigidbody = GetComponent<Rigidbody>();
            if (ballRigidbody == null)
            {
                ballRigidbody = gameObject.AddComponent<Rigidbody>();
            }
        }

        if (ballCollider == null)
        {
            ballCollider = GetComponent<SphereCollider>();
            if (ballCollider == null)
            {
                ballCollider = gameObject.AddComponent<SphereCollider>();
            }
        }

        runtimePhysicsMaterial ??= CreatePhysicsMaterial();
        ballCollider.isTrigger = false;
        ballCollider.material = runtimePhysicsMaterial;

        ballRigidbody.useGravity = false;
        ballRigidbody.mass = Mathf.Max(0.01f, mass);
        ballRigidbody.drag = 0f;
        ballRigidbody.angularDrag = 0f;
        ballRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        ballRigidbody.collisionDetectionMode = collisionDetectionMode;
        ballRigidbody.constraints = RigidbodyConstraints.FreezePositionY;
        ballRigidbody.freezeRotation = false;
        ballRigidbody.maxAngularVelocity = Mathf.Max(7f, speed / Mathf.Max(0.01f, hitRadius) * 2f);
        ballRigidbody.sleepThreshold = 0f;
    }

    private void ApplyColliderRadius()
    {
        if (ballCollider == null)
        {
            return;
        }

        float maxScale = Mathf.Max(
            Mathf.Abs(transform.lossyScale.x),
            Mathf.Abs(transform.lossyScale.y),
            Mathf.Abs(transform.lossyScale.z)
        );
        ballCollider.radius = Mathf.Max(0.01f, hitRadius / Mathf.Max(0.01f, maxScale));
        ballCollider.center = Vector3.zero;
    }

    private void ResetPhysicsMotion()
    {
        EnsurePhysicsComponents();
        ApplyColliderRadius();

        Vector3 position = transform.position;
        position.y = groundOffset;
        transform.position = position;
        ballRigidbody.position = position;
        ballRigidbody.rotation = transform.rotation;

        if (gameManager != null && gameManager.IsGameplayStopped)
        {
            StopPhysicsMotion();
            wasGameplayStopped = true;
            return;
        }

        wasGameplayStopped = false;
        LaunchPhysicsMotion();
    }

    private void LaunchPhysicsMotion()
    {
        NormalizeDirection();
        ballRigidbody.velocity = Vector3.zero;
        ballRigidbody.angularVelocity = Vector3.zero;
        ballRigidbody.WakeUp();
        ballRigidbody.AddForce(GetLaunchVelocity(), ForceMode.VelocityChange);
        ApplyRollingAngularVelocity();
    }

    private void StopPhysicsMotion()
    {
        if (ballRigidbody == null)
        {
            return;
        }

        ballRigidbody.velocity = Vector3.zero;
        ballRigidbody.angularVelocity = Vector3.zero;
        ballRigidbody.Sleep();
    }

    private Vector3 GetLaunchVelocity()
    {
        return new Vector3(direction.x, 0f, direction.y) * speed;
    }

    private void MaintainFixedPhysicsSpeed()
    {
        Vector3 planarVelocity = GetPlanarRigidbodyVelocity();
        if (planarVelocity.sqrMagnitude < 0.0001f)
        {
            planarVelocity = GetLaunchVelocity();
        }

        ballRigidbody.velocity = planarVelocity.normalized * speed;
    }

    private void ApplyRollingAngularVelocity()
    {
        Vector3 planarVelocity = GetPlanarRigidbodyVelocity();
        if (planarVelocity.sqrMagnitude < 0.0001f)
        {
            ballRigidbody.angularVelocity = Vector3.zero;
            return;
        }

        float rollingRadius = Mathf.Max(0.01f, hitRadius);
        Vector3 rollingAxis = Vector3.Cross(Vector3.up, planarVelocity.normalized);
        ballRigidbody.angularVelocity = rollingAxis * (planarVelocity.magnitude / rollingRadius);
    }

    private Vector3 GetPlanarRigidbodyVelocity()
    {
        Vector3 velocity = ballRigidbody.velocity;
        return new Vector3(velocity.x, 0f, velocity.z);
    }

    private void SyncDirectionFromVelocity()
    {
        if (ballRigidbody == null)
        {
            return;
        }

        Vector2 planarVelocity = new Vector2(ballRigidbody.velocity.x, ballRigidbody.velocity.z);
        if (planarVelocity.sqrMagnitude > 0.0001f)
        {
            direction = planarVelocity.normalized;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        HandlePhysicsCollision(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        HandlePhysicsCollision(collision);
    }

    private void HandlePhysicsCollision(Collision collision)
    {
        List<Vector2Int> handledCells = null;

        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint contact = collision.GetContact(i);
            Collider otherCollider = contact.otherCollider;
            if (otherCollider == null || otherCollider == ballCollider)
            {
                otherCollider = collision.collider;
            }

            GridCellPhysicsCollider cellCollider = otherCollider != null ? otherCollider.GetComponent<GridCellPhysicsCollider>() : null;
            GridManager contactGridManager = cellCollider != null ? cellCollider.GridManager : null;
            if (contactGridManager == null)
            {
                continue;
            }

            Vector2Int cell = contactGridManager.GetBallPhysicsContactCell(contact.point, contact.normal);
            handledCells ??= new List<Vector2Int>();
            if (handledCells.Contains(cell))
            {
                continue;
            }

            handledCells.Add(cell);
            HandleGridCellPhysicsCollision(cell);
        }

        MaintainFixedPhysicsSpeed();
        ApplyRollingAngularVelocity();
        SyncDirectionFromVelocity();
    }

    private void HandleGridCellPhysicsCollision(Vector2Int cell)
    {
        if (gridManager == null || !gridManager.IsInsideGrid(cell))
        {
            return;
        }

        CellState cellState = gridManager.GetCellState(cell);
        if (cellState == CellState.TemporaryPath || cellState == CellState.BurningPath)
        {
            territoryManager?.HandleBallTouchedPath(cell);
            return;
        }

        if (cellState == CellState.Claimed && IsEater)
        {
            DestroyClaimedContactCells(cell);
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

    private void DestroyClaimedContactCells(Vector2Int contactCell)
    {
        if (!IsEater || gridManager == null)
        {
            return;
        }

        List<Vector2Int> contactCells = new() { contactCell };
        Vector2Int biteDirection = GetPrimaryBiteDirection();
        Vector2Int nextCell = contactCell + biteDirection;
        if (biteDirection != Vector2Int.zero && !contactCells.Contains(nextCell))
        {
            contactCells.Add(nextCell);
        }

        List<Vector2Int> destroyedCells = new();
        foreach (Vector2Int cell in contactCells)
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

    private Vector2Int GetPrimaryBiteDirection()
    {
        if (Mathf.Abs(direction.x) >= Mathf.Abs(direction.y))
        {
            return new Vector2Int(GetDirectionSign(direction.x), 0);
        }

        return new Vector2Int(0, GetDirectionSign(direction.y));
    }

    private Vector3 GetBallWorldPosition(Vector2Int cell)
    {
        Vector3 groundPosition = gridManager.GridToWorld(cell);
        return groundPosition + Vector3.up * groundOffset;
    }

    private Vector2Int ResolveSpawnCell(Vector2Int requestedCell)
    {
        if (gridManager == null)
        {
            return requestedCell;
        }

        return gridManager.FindNearestCellWithState(requestedCell, CellState.Unclaimed, true);
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

    private static Material CreateTrailMaterial()
    {
        Shader shader = Shader.Find("Sprites/Default");
        shader ??= Shader.Find("Particles/Standard Unlit");
        shader ??= Shader.Find("Unlit/Color");
        shader ??= Shader.Find("Standard");

        return shader != null ? new Material(shader) : null;
    }

    private PhysicMaterial CreatePhysicsMaterial()
    {
        return new PhysicMaterial("Ball Physics Material")
        {
            dynamicFriction = 0f,
            staticFriction = 0f,
            bounciness = Mathf.Clamp01(bounciness),
            frictionCombine = PhysicMaterialCombine.Minimum,
            bounceCombine = PhysicMaterialCombine.Maximum
        };
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
