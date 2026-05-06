using UnityEngine;

public sealed class GridManager : MonoBehaviour
{
    [Header("Grid")]
    [SerializeField] private int width = 40;
    [SerializeField] private int height = 40;
    [SerializeField] private float cellSize = 1f;

    [Header("Visuals")]
    [SerializeField] private float tileHeight = 0.08f;
    [SerializeField] private Transform tileRoot;
    [SerializeField] private Material claimedMaterial;
    [SerializeField] private Material unclaimedMaterial;
    [SerializeField] private Material temporaryPathMaterial;

    private CellState[,] grid;
    private Renderer[,] tileRenderers;

    public int Width => width;
    public int Height => height;
    public float CellSize => cellSize;

    private void Awake()
    {
        EnsureMaterials();
        InitializeGrid();
        CreateTileVisuals();
        RefreshAllTiles();
    }

    public Vector2Int WorldToGrid(Vector3 worldPosition)
    {
        return new Vector2Int(
            Mathf.RoundToInt(worldPosition.x / cellSize),
            Mathf.RoundToInt(worldPosition.z / cellSize)
        );
    }

    public Vector3 GridToWorld(Vector2Int gridPosition)
    {
        return new Vector3(gridPosition.x * cellSize, 0f, gridPosition.y * cellSize);
    }

    public bool IsInsideGrid(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < width && cell.y >= 0 && cell.y < height;
    }

    public CellState GetCellState(Vector2Int cell)
    {
        if (!IsInsideGrid(cell))
        {
            return CellState.Claimed;
        }

        return grid[cell.x, cell.y];
    }

    public void SetCellState(Vector2Int cell, CellState state)
    {
        if (!IsInsideGrid(cell))
        {
            return;
        }

        grid[cell.x, cell.y] = state;
        RefreshTile(cell);
    }

    public bool IsBlockedForBall(Vector2Int cell)
    {
        return !IsInsideGrid(cell) || GetCellState(cell) != CellState.Unclaimed;
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
                tile.transform.position = GridToWorld(new Vector2Int(x, y));
                tile.transform.localScale = new Vector3(cellSize, tileHeight, cellSize);

                tileRenderers[x, y] = tile.GetComponent<Renderer>();
            }
        }
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

        tileRenderers[cell.x, cell.y].sharedMaterial = GetMaterialForState(grid[cell.x, cell.y]);
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

    private void EnsureMaterials()
    {
        claimedMaterial ??= CreateRuntimeMaterial("Runtime Claimed", new Color(0.18f, 0.72f, 0.34f));
        unclaimedMaterial ??= CreateRuntimeMaterial("Runtime Unclaimed", new Color(0.34f, 0.36f, 0.38f));
        temporaryPathMaterial ??= CreateRuntimeMaterial("Runtime Temporary Path", new Color(1f, 0.54f, 0.16f));
    }

    private static Material CreateRuntimeMaterial(string materialName, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        shader ??= Shader.Find("Standard");
        shader ??= Shader.Find("Unlit/Color");

        Material material = new Material(shader)
        {
            name = materialName
        };

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        return material;
    }
}
