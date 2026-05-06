using UnityEngine;

public sealed class GridManager : MonoBehaviour
{
    [Header("Grid")]
    [SerializeField] private int width = 40;
    [SerializeField] private int height = 40;
    [SerializeField] private float cellSize = 1f;

    [Header("Visuals")]
    [SerializeField] private float tileHeight = 0.08f;
    [SerializeField] private float temporaryPathHeight = 0.18f;
    [SerializeField] private float tileGap = 0.04f;
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

    private CellState[,] grid;
    private Renderer[,] tileRenderers;
    private Renderer[] wallRenderers;
    private MaterialPropertyBlock tilePropertyBlock;
    private bool isInitialized;
    private bool visualsCreated;
    private bool wallsCreated;

    public int Width => width;
    public int Height => height;
    public float CellSize => cellSize;

    private void Awake()
    {
        InitializeIfNeeded();
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

        return cell.x >= 0 && cell.x < width && cell.y >= 0 && cell.y < height;
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
        RefreshTile(cell);
    }

    public bool IsBlockedForBall(Vector2Int cell)
    {
        InitializeIfNeeded();

        return !IsInsideGrid(cell) || GetCellState(cell) != CellState.Unclaimed;
    }

    public void ResetGrid()
    {
        InitializeIfNeeded();
        InitializeGrid();
        RefreshAllTiles();
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
        CreateTileVisuals();
        CreateArenaWallVisuals();
        RefreshAllTiles();
    }

    private void InitializeGrid()
    {
        grid = new CellState[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                bool isBorder = x == 0 || x == width - 1 || y == 0 || y == height - 1;
                grid[x, y] = isBorder ? CellState.Claimed : CellState.Unclaimed;
            }
        }
    }

    private void CreateTileVisuals()
    {
        if (visualsCreated)
        {
            return;
        }

        if (tileRoot == null)
        {
            GameObject rootObject = new GameObject("Tiles");
            rootObject.transform.SetParent(transform);
            tileRoot = rootObject.transform;
        }

        tileRenderers = new Renderer[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tile.name = $"Tile_{x}_{y}";
                tile.transform.SetParent(tileRoot);

                tileRenderers[x, y] = tile.GetComponent<Renderer>();
                ConfigureTileTransform(new Vector2Int(x, y));
            }
        }

        visualsCreated = true;
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

    private void RefreshAllTiles()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                RefreshTile(new Vector2Int(x, y));
            }
        }
    }

    private void RefreshTile(Vector2Int cell)
    {
        if (!IsInsideGrid(cell) || tileRenderers == null || tileRenderers[cell.x, cell.y] == null)
        {
            return;
        }

        Renderer tileRenderer = tileRenderers[cell.x, cell.y];
        CellState state = grid[cell.x, cell.y];
        ConfigureTileTransform(cell);
        ApplyRendererMaterialAndColor(tileRenderer, GetMaterialForState(state), GetColorForState(state));
    }

    private void ConfigureTileTransform(Vector2Int cell)
    {
        if (tileRenderers == null || tileRenderers[cell.x, cell.y] == null)
        {
            return;
        }

        CellState state = grid[cell.x, cell.y];
        float stateHeight = state == CellState.TemporaryPath ? temporaryPathHeight : tileHeight;
        float tileSize = Mathf.Max(0.05f, cellSize - tileGap);

        Transform tileTransform = tileRenderers[cell.x, cell.y].transform;
        Vector3 groundPosition = GridToWorld(cell);
        tileTransform.position = new Vector3(groundPosition.x, stateHeight * 0.5f, groundPosition.z);
        tileTransform.localScale = new Vector3(tileSize, stateHeight, tileSize);
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

        targetRenderer.GetPropertyBlock(tilePropertyBlock);
        tilePropertyBlock.SetColor("_Color", color);
        tilePropertyBlock.SetColor("_BaseColor", color);
        targetRenderer.SetPropertyBlock(tilePropertyBlock);
    }

    private Material GetMaterialForState(CellState state)
    {
        return state switch
        {
            CellState.Claimed => claimedMaterial,
            CellState.TemporaryPath => temporaryPathMaterial,
            _ => unclaimedMaterial
        };
    }

    private static Color GetColorForState(CellState state)
    {
        return state switch
        {
            CellState.Claimed => new Color(0.18f, 0.72f, 0.34f),
            CellState.TemporaryPath => new Color(1f, 0.54f, 0.16f),
            _ => new Color(0.34f, 0.36f, 0.38f)
        };
    }
}
