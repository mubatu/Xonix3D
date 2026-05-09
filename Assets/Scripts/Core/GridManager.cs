using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class GridManager : MonoBehaviour
{
    [Header("Grid")]
    [SerializeField] private int width = 40;
    [SerializeField] private int height = 40;
    [SerializeField] private float cellSize = 1f;

    [Header("Visuals")]
    [SerializeField] private float tileHeight = 0.08f;
    [SerializeField] private float tileGap = 0.04f;
    [SerializeField] private Transform groundRoot;
    [SerializeField] private Transform tileRoot;
    [SerializeField] private Material claimedMaterial;
    [SerializeField] private Material unclaimedMaterial;
    [SerializeField] private Material temporaryPathMaterial;

    [Header("Arena Walls")]
    [SerializeField] private bool createArenaWalls = true;
    [SerializeField] private float wallHeight = 1.2f;
    [SerializeField] private float wallThickness = 0.5f;
    [SerializeField] private Transform wallRoot;
    [SerializeField] private Material wallMaterial;

    [Header("Capture Feedback")]
    [SerializeField] private Color capturePulseColor = new Color(0.64f, 1f, 0.34f);
    [SerializeField] private float capturePulseDuration = 0.45f;
    [SerializeField] private int capturePulseCount = 2;

    private CellState[,] grid;
    private Renderer[,] pathTileRenderers;
    private Renderer[] wallRenderers;
    private MaterialPropertyBlock tilePropertyBlock;
    private Material fallbackMaterial;
    private MeshFilter claimedGroundMeshFilter;
    private MeshFilter unclaimedGroundMeshFilter;
    private MeshFilter pulseMeshFilter;
    private Renderer pulseRenderer;
    private LevelClaimedArea[] initialClaimedAreas;
    private LevelCell[] initialClaimedCells;
    private bool isInitialized;
    private bool groundVisualsCreated;
    private bool pathTilePoolCreated;
    private bool wallsCreated;
    private bool groundMeshesDirty;

    public int Width => width;
    public int Height => height;
    public float CellSize => cellSize;

    private void Awake()
    {
        InitializeIfNeeded();
    }

    private void LateUpdate()
    {
        if (!isInitialized || !groundMeshesDirty)
        {
            return;
        }

        RebuildGroundMeshes();
        groundMeshesDirty = false;
    }

    public Vector2Int WorldToGrid(Vector3 worldPosition)
    {
        InitializeIfNeeded();

        return new Vector2Int(
            Mathf.RoundToInt(worldPosition.x / cellSize),
            Mathf.RoundToInt(worldPosition.z / cellSize)
        );
    }

    public Vector3 GridToWorld(Vector2Int gridPosition)
    {
        InitializeIfNeeded();

        return new Vector3(gridPosition.x * cellSize, 0f, gridPosition.y * cellSize);
    }

    public bool IsInsideGrid(Vector2Int cell)
    {
        InitializeIfNeeded();

        return IsInsideGridBounds(cell.x, cell.y);
    }

    public CellState GetCellState(Vector2Int cell)
    {
        InitializeIfNeeded();

        if (!IsInsideGrid(cell))
        {
            return CellState.Claimed;
        }

        return grid[cell.x, cell.y];
    }

    public void SetCellState(Vector2Int cell, CellState state)
    {
        InitializeIfNeeded();

        if (!IsInsideGrid(cell))
        {
            return;
        }

        grid[cell.x, cell.y] = state;
        RefreshCellVisual(cell);
    }

    public bool IsBlockedForBall(Vector2Int cell)
    {
        InitializeIfNeeded();

        return !IsInsideGrid(cell) || GetCellState(cell) != CellState.Unclaimed;
    }

    public void ApplyLevelData(LevelData levelData)
    {
        if (levelData == null)
        {
            return;
        }

        bool dimensionsChanged = width != levelData.width
            || height != levelData.height
            || !Mathf.Approximately(cellSize, levelData.cellSize);

        width = Mathf.Max(2, levelData.width);
        height = Mathf.Max(2, levelData.height);
        cellSize = Mathf.Max(0.1f, levelData.cellSize);
        initialClaimedAreas = levelData.initiallyClaimedAreas;
        initialClaimedCells = levelData.initiallyClaimedCells;

        if (isInitialized && dimensionsChanged)
        {
            ResetRuntimeVisuals();
            CreateGroundVisuals();
            CreatePathTilePool();
            CreateArenaWallVisuals();
        }

        ResetGrid();
    }

    public void ResetGrid()
    {
        InitializeIfNeeded();
        StopAllCoroutines();
        HideCapturePulse();
        InitializeGrid();
        RefreshAllVisuals();
    }

    public void PlayCapturePulse(IReadOnlyList<Vector2Int> cells)
    {
        InitializeIfNeeded();

        if (cells == null || cells.Count == 0)
        {
            return;
        }

        if (groundMeshesDirty)
        {
            RebuildGroundMeshes();
            groundMeshesDirty = false;
        }

        StartCoroutine(PlayCapturePulseRoutine(cells));
    }

    private void InitializeIfNeeded()
    {
        if (isInitialized)
        {
            return;
        }

        tilePropertyBlock ??= new MaterialPropertyBlock();
        InitializeGrid();
        isInitialized = true;
        CreateGroundVisuals();
        CreatePathTilePool();
        CreateArenaWallVisuals();
        RefreshAllVisuals();
    }

    private void InitializeGrid()
    {
        grid = new CellState[width, height];
        bool hasCustomInitialClaims = (initialClaimedAreas != null && initialClaimedAreas.Length > 0)
            || (initialClaimedCells != null && initialClaimedCells.Length > 0);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                bool isBorder = x == 0 || x == width - 1 || y == 0 || y == height - 1;
                grid[x, y] = !hasCustomInitialClaims && isBorder ? CellState.Claimed : CellState.Unclaimed;
            }
        }

        ApplyInitialClaims();
    }

    private void ApplyInitialClaims()
    {
        if (initialClaimedAreas != null)
        {
            foreach (LevelClaimedArea area in initialClaimedAreas)
            {
                ApplyInitialClaimedArea(area);
            }
        }

        if (initialClaimedCells == null)
        {
            return;
        }

        foreach (LevelCell cell in initialClaimedCells)
        {
            if (cell != null && IsInsideGridBounds(cell.x, cell.y))
            {
                grid[cell.x, cell.y] = CellState.Claimed;
            }
        }
    }

    private void ApplyInitialClaimedArea(LevelClaimedArea area)
    {
        if (area == null)
        {
            return;
        }

        int startX = Mathf.Clamp(area.x, 0, width - 1);
        int startY = Mathf.Clamp(area.y, 0, height - 1);
        int endX = Mathf.Clamp(area.x + Mathf.Max(1, area.width), 0, width);
        int endY = Mathf.Clamp(area.y + Mathf.Max(1, area.height), 0, height);

        for (int x = startX; x < endX; x++)
        {
            for (int y = startY; y < endY; y++)
            {
                grid[x, y] = CellState.Claimed;
            }
        }
    }

    private void CreateGroundVisuals()
    {
        if (groundVisualsCreated)
        {
            return;
        }

        if (groundRoot == null)
        {
            GameObject rootObject = new GameObject("Ground Meshes");
            rootObject.transform.SetParent(transform);
            groundRoot = rootObject.transform;
        }

        claimedGroundMeshFilter = CreateMeshVisual(
            "Claimed Ground Mesh",
            groundRoot,
            claimedMaterial,
            GetColorForState(CellState.Claimed),
            out _
        );
        unclaimedGroundMeshFilter = CreateMeshVisual(
            "Unclaimed Ground Mesh",
            groundRoot,
            unclaimedMaterial,
            GetColorForState(CellState.Unclaimed),
            out _
        );
        pulseMeshFilter = CreateMeshVisual(
            "Capture Pulse Mesh",
            groundRoot,
            null,
            capturePulseColor,
            out pulseRenderer
        );
        HideCapturePulse();

        groundVisualsCreated = true;
    }

    private void ResetRuntimeVisuals()
    {
        HideCapturePulse();
        DestroyChildren(groundRoot);
        DestroyChildren(tileRoot);
        DestroyChildren(wallRoot);

        pathTileRenderers = null;
        wallRenderers = null;
        claimedGroundMeshFilter = null;
        unclaimedGroundMeshFilter = null;
        pulseMeshFilter = null;
        pulseRenderer = null;

        groundVisualsCreated = false;
        pathTilePoolCreated = false;
        wallsCreated = false;
        groundMeshesDirty = false;
    }

    private void CreatePathTilePool()
    {
        if (pathTilePoolCreated)
        {
            return;
        }

        if (tileRoot == null)
        {
            GameObject rootObject = new GameObject("Temporary Path Tiles");
            rootObject.transform.SetParent(transform);
            tileRoot = rootObject.transform;
        }

        pathTileRenderers = new Renderer[width, height];
        pathTilePoolCreated = true;
    }

    private MeshFilter CreateMeshVisual(string objectName, Transform parent, Material material, Color color, out Renderer meshRenderer)
    {
        GameObject meshObject = new GameObject(objectName, typeof(MeshFilter), typeof(MeshRenderer));
        meshObject.transform.SetParent(parent);

        MeshFilter meshFilter = meshObject.GetComponent<MeshFilter>();
        meshFilter.sharedMesh = new Mesh { name = objectName };

        meshRenderer = meshObject.GetComponent<Renderer>();
        ApplyRendererMaterialAndColor(meshRenderer, material, color);
        return meshFilter;
    }

    private void CreateArenaWallVisuals()
    {
        if (wallsCreated || !createArenaWalls)
        {
            return;
        }

        if (wallRoot == null)
        {
            GameObject rootObject = new GameObject("Arena Walls");
            rootObject.transform.SetParent(transform);
            wallRoot = rootObject.transform;
        }

        wallRenderers = new Renderer[4];

        float arenaWidth = width * cellSize;
        float arenaHeight = height * cellSize;
        float centerX = (width - 1) * cellSize * 0.5f;
        float centerZ = (height - 1) * cellSize * 0.5f;
        float wallY = wallHeight * 0.5f;
        float minX = -cellSize * 0.5f - wallThickness * 0.5f;
        float maxX = (width - 1) * cellSize + cellSize * 0.5f + wallThickness * 0.5f;
        float minZ = -cellSize * 0.5f - wallThickness * 0.5f;
        float maxZ = (height - 1) * cellSize + cellSize * 0.5f + wallThickness * 0.5f;

        wallRenderers[0] = CreateWall(
            "Wall_North",
            new Vector3(centerX, wallY, maxZ),
            new Vector3(arenaWidth + wallThickness * 2f, wallHeight, wallThickness)
        );
        wallRenderers[1] = CreateWall(
            "Wall_South",
            new Vector3(centerX, wallY, minZ),
            new Vector3(arenaWidth + wallThickness * 2f, wallHeight, wallThickness)
        );
        wallRenderers[2] = CreateWall(
            "Wall_East",
            new Vector3(maxX, wallY, centerZ),
            new Vector3(wallThickness, wallHeight, arenaHeight)
        );
        wallRenderers[3] = CreateWall(
            "Wall_West",
            new Vector3(minX, wallY, centerZ),
            new Vector3(wallThickness, wallHeight, arenaHeight)
        );

        foreach (Renderer wallRenderer in wallRenderers)
        {
            ApplyRendererMaterialAndColor(wallRenderer, wallMaterial, new Color(0.04f, 0.24f, 0.26f));
        }

        wallsCreated = true;
    }

    private Renderer CreateWall(string wallName, Vector3 position, Vector3 scale)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = wallName;
        wall.transform.SetParent(wallRoot);
        wall.transform.position = position;
        wall.transform.localScale = scale;
        return wall.GetComponent<Renderer>();
    }

    private void RefreshAllVisuals()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                RefreshCellVisual(new Vector2Int(x, y));
            }
        }

        RebuildGroundMeshes();
        groundMeshesDirty = false;
    }

    private void RefreshCellVisual(Vector2Int cell)
    {
        if (!IsInsideGrid(cell) || pathTileRenderers == null)
        {
            return;
        }

        CellState state = grid[cell.x, cell.y];
        Renderer pathTileRenderer = pathTileRenderers[cell.x, cell.y];

        if (state == CellState.TemporaryPath || state == CellState.BurningPath)
        {
            pathTileRenderer ??= CreatePathTileRenderer(cell);
            ConfigurePathTileTransform(cell);
            ApplyRendererMaterialAndColor(pathTileRenderer, temporaryPathMaterial, GetColorForState(state));
            pathTileRenderer.gameObject.SetActive(true);
        }
        else if (pathTileRenderer != null)
        {
            pathTileRenderer.gameObject.SetActive(false);
        }

        groundMeshesDirty = true;
    }

    private Renderer CreatePathTileRenderer(Vector2Int cell)
    {
        GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tile.name = $"TemporaryPathTile_{cell.x}_{cell.y}";
        tile.transform.SetParent(tileRoot);

        Collider tileCollider = tile.GetComponent<Collider>();
        if (tileCollider != null)
        {
            Destroy(tileCollider);
        }

        Renderer pathTileRenderer = tile.GetComponent<Renderer>();
        pathTileRenderers[cell.x, cell.y] = pathTileRenderer;
        return pathTileRenderer;
    }

    private void ConfigurePathTileTransform(Vector2Int cell)
    {
        if (pathTileRenderers == null || pathTileRenderers[cell.x, cell.y] == null)
        {
            return;
        }

        float stateHeight = wallHeight;
        float tileSize = Mathf.Max(0.05f, cellSize - tileGap);

        Transform tileTransform = pathTileRenderers[cell.x, cell.y].transform;
        Vector3 groundPosition = GridToWorld(cell);
        tileTransform.position = new Vector3(groundPosition.x, stateHeight * 0.5f, groundPosition.z);
        tileTransform.localScale = new Vector3(tileSize, stateHeight, tileSize);
    }

    private float GetHeightForState(CellState state)
    {
        return state switch
        {
            CellState.Claimed => wallHeight,
            _ => tileHeight
        };
    }

    private void RebuildGroundMeshes()
    {
        BuildGroundMesh(claimedGroundMeshFilter, CellState.Claimed);
        BuildGroundMesh(unclaimedGroundMeshFilter, CellState.Unclaimed);
    }

    private void BuildGroundMesh(MeshFilter meshFilter, CellState meshState)
    {
        if (meshFilter == null)
        {
            return;
        }

        Mesh mesh = meshFilter.sharedMesh;
        if (mesh == null)
        {
            mesh = new Mesh { name = meshFilter.gameObject.name };
            meshFilter.sharedMesh = mesh;
        }

        List<Vector3> vertices = new();
        List<int> triangles = new();
        List<Vector2> uvs = new();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                CellState state = grid[x, y];
                if (!ShouldRenderInGroundMesh(state, meshState))
                {
                    continue;
                }

                Vector2Int cell = new Vector2Int(x, y);
                float cellHeight = GetHeightForState(state);
                AddTopFace(vertices, triangles, uvs, cell, cellHeight);
                AddVisibleSideFaces(vertices, triangles, uvs, cell, cellHeight, meshState);
            }
        }

        mesh.Clear();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    private void AddTopFace(List<Vector3> vertices, List<int> triangles, List<Vector2> uvs, Vector2Int cell, float topY)
    {
        GetCellBounds(cell, out float minX, out float maxX, out float minZ, out float maxZ);

        AddQuad(
            vertices,
            triangles,
            uvs,
            new Vector3(minX, topY, minZ),
            new Vector3(minX, topY, maxZ),
            new Vector3(maxX, topY, maxZ),
            new Vector3(maxX, topY, minZ)
        );
    }

    private void AddVisibleSideFaces(
        List<Vector3> vertices,
        List<int> triangles,
        List<Vector2> uvs,
        Vector2Int cell,
        float topY,
        CellState meshState
    )
    {
        GetCellBounds(cell, out float minX, out float maxX, out float minZ, out float maxZ);

        AddSideFaceIfVisible(vertices, triangles, uvs, cell + Vector2Int.up, meshState, topY, new[]
        {
            new Vector3(minX, 0f, maxZ),
            new Vector3(maxX, 0f, maxZ),
            new Vector3(maxX, topY, maxZ),
            new Vector3(minX, topY, maxZ)
        });

        AddSideFaceIfVisible(vertices, triangles, uvs, cell + Vector2Int.down, meshState, topY, new[]
        {
            new Vector3(minX, 0f, minZ),
            new Vector3(minX, topY, minZ),
            new Vector3(maxX, topY, minZ),
            new Vector3(maxX, 0f, minZ)
        });

        AddSideFaceIfVisible(vertices, triangles, uvs, cell + Vector2Int.right, meshState, topY, new[]
        {
            new Vector3(maxX, 0f, minZ),
            new Vector3(maxX, topY, minZ),
            new Vector3(maxX, topY, maxZ),
            new Vector3(maxX, 0f, maxZ)
        });

        AddSideFaceIfVisible(vertices, triangles, uvs, cell + Vector2Int.left, meshState, topY, new[]
        {
            new Vector3(minX, 0f, minZ),
            new Vector3(minX, 0f, maxZ),
            new Vector3(minX, topY, maxZ),
            new Vector3(minX, topY, minZ)
        });
    }

    private void AddSideFaceIfVisible(
        List<Vector3> vertices,
        List<int> triangles,
        List<Vector2> uvs,
        Vector2Int neighbor,
        CellState meshState,
        float topY,
        Vector3[] corners
    )
    {
        if (IsInsideGrid(neighbor) && ShouldRenderInGroundMesh(grid[neighbor.x, neighbor.y], meshState))
        {
            return;
        }

        float bottomY = GetNeighborSurfaceHeight(neighbor);
        if (topY <= bottomY + 0.001f)
        {
            return;
        }

        for (int i = 0; i < corners.Length; i++)
        {
            if (Mathf.Approximately(corners[i].y, 0f))
            {
                corners[i].y = bottomY;
            }
        }

        AddQuad(vertices, triangles, uvs, corners[0], corners[1], corners[2], corners[3]);
    }

    private void BuildPulseMesh(IReadOnlyList<Vector2Int> cells)
    {
        if (pulseMeshFilter == null)
        {
            return;
        }

        Mesh mesh = pulseMeshFilter.sharedMesh;
        if (mesh == null)
        {
            mesh = new Mesh { name = "Capture Pulse Mesh" };
            pulseMeshFilter.sharedMesh = mesh;
        }

        List<Vector3> vertices = new();
        List<int> triangles = new();
        List<Vector2> uvs = new();
        float pulseY = wallHeight + 0.01f;

        foreach (Vector2Int cell in cells)
        {
            if (IsInsideGrid(cell))
            {
                AddTopFace(vertices, triangles, uvs, cell, pulseY);
            }
        }

        mesh.Clear();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    private void AddQuad(
        List<Vector3> vertices,
        List<int> triangles,
        List<Vector2> uvs,
        Vector3 bottomLeft,
        Vector3 topLeft,
        Vector3 topRight,
        Vector3 bottomRight
    )
    {
        int startIndex = vertices.Count;
        vertices.Add(bottomLeft);
        vertices.Add(topLeft);
        vertices.Add(topRight);
        vertices.Add(bottomRight);

        triangles.Add(startIndex);
        triangles.Add(startIndex + 1);
        triangles.Add(startIndex + 2);
        triangles.Add(startIndex);
        triangles.Add(startIndex + 2);
        triangles.Add(startIndex + 3);

        uvs.Add(new Vector2(0f, 0f));
        uvs.Add(new Vector2(0f, 1f));
        uvs.Add(new Vector2(1f, 1f));
        uvs.Add(new Vector2(1f, 0f));
    }

    private void GetCellBounds(Vector2Int cell, out float minX, out float maxX, out float minZ, out float maxZ)
    {
        Vector3 center = GridToWorld(cell);
        float halfSize = cellSize * 0.5f;
        minX = center.x - halfSize;
        maxX = center.x + halfSize;
        minZ = center.z - halfSize;
        maxZ = center.z + halfSize;
    }

    private float GetNeighborSurfaceHeight(Vector2Int neighbor)
    {
        if (!IsInsideGrid(neighbor))
        {
            return 0f;
        }

        return GetHeightForState(grid[neighbor.x, neighbor.y]);
    }

    private static bool ShouldRenderInGroundMesh(CellState state, CellState meshState)
    {
        return meshState switch
        {
            CellState.Claimed => state == CellState.Claimed,
            CellState.Unclaimed => state == CellState.Unclaimed || state == CellState.TemporaryPath || state == CellState.BurningPath,
            _ => false
        };
    }

    private bool IsInsideGridBounds(int x, int y)
    {
        return x >= 0 && x < width && y >= 0 && y < height;
    }

    private static void DestroyChildren(Transform root)
    {
        if (root == null)
        {
            return;
        }

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Destroy(root.GetChild(i).gameObject);
        }
    }

    private void ApplyRendererMaterialAndColor(Renderer targetRenderer, Material stateMaterial, Color color)
    {
        if (targetRenderer == null)
        {
            return;
        }

        if (stateMaterial != null)
        {
            targetRenderer.sharedMaterial = stateMaterial;
            targetRenderer.SetPropertyBlock(null);
            return;
        }

        if (targetRenderer.sharedMaterial == null)
        {
            targetRenderer.sharedMaterial = GetFallbackMaterial();
        }

        targetRenderer.GetPropertyBlock(tilePropertyBlock);
        tilePropertyBlock.SetColor("_Color", color);
        tilePropertyBlock.SetColor("_BaseColor", color);
        targetRenderer.SetPropertyBlock(tilePropertyBlock);
    }

    private Material GetFallbackMaterial()
    {
        if (fallbackMaterial != null)
        {
            return fallbackMaterial;
        }

        Shader shader = Shader.Find("Standard");
        shader ??= Shader.Find("Universal Render Pipeline/Lit");
        shader ??= Shader.Find("Unlit/Color");

        fallbackMaterial = shader != null ? new Material(shader) : null;
        if (fallbackMaterial != null)
        {
            fallbackMaterial.name = "Generated Ground Fallback Material";
        }

        return fallbackMaterial;
    }

    private IEnumerator PlayCapturePulseRoutine(IReadOnlyList<Vector2Int> cells)
    {
        List<Vector2Int> pulseCells = new(cells);
        int safePulseCount = Mathf.Max(1, capturePulseCount);
        float halfPulseDuration = Mathf.Max(0.04f, capturePulseDuration / (safePulseCount * 2f));
        Color claimedColor = GetColorForState(CellState.Claimed);

        BuildPulseMesh(pulseCells);
        SetCapturePulseVisible(true);

        for (int pulseIndex = 0; pulseIndex < safePulseCount; pulseIndex++)
        {
            SetCapturePulseColor(capturePulseColor);
            yield return new WaitForSeconds(halfPulseDuration);

            SetCapturePulseColor(claimedColor);
            yield return new WaitForSeconds(halfPulseDuration);
        }

        HideCapturePulse();
    }

    private void SetCapturePulseColor(Color color)
    {
        if (pulseRenderer == null)
        {
            return;
        }

        ApplyRendererMaterialAndColor(pulseRenderer, null, color);
    }

    private void SetCapturePulseVisible(bool visible)
    {
        if (pulseRenderer != null)
        {
            pulseRenderer.enabled = visible;
        }
    }

    private void HideCapturePulse()
    {
        SetCapturePulseVisible(false);
    }

    private static Color GetColorForState(CellState state)
    {
        return state switch
        {
            CellState.Claimed => new Color(0.18f, 0.72f, 0.34f),
            CellState.TemporaryPath => new Color(1f, 0.54f, 0.16f),
            CellState.BurningPath => new Color(1f, 0.04f, 0.03f),
            _ => new Color(0.34f, 0.36f, 0.38f)
        };
    }
}
