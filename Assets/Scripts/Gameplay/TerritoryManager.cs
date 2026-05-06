using System.Collections.Generic;
using UnityEngine;

public sealed class TerritoryManager : MonoBehaviour
{
    private static readonly Vector2Int[] FloodFillDirections =
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };

    [Header("References")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private List<BallController> balls = new();

    private readonly List<Vector2Int> temporaryPathCells = new();
    private bool isDrawing;
    private float capturedPercentage;

    public bool IsDrawing => isDrawing;
    public IReadOnlyList<Vector2Int> TemporaryPathCells => temporaryPathCells;
    public float CapturedPercentage => capturedPercentage;

    private void Awake()
    {
        if (gridManager == null)
        {
            gridManager = FindFirstObjectByType<GridManager>();
        }

        if (gameManager == null)
        {
            gameManager = FindFirstObjectByType<GameManager>();
        }

        RefreshBallReferences();
        capturedPercentage = CalculateCapturedPercentage();
        gameManager?.HandleCaptureUpdated(capturedPercentage);
    }

    public void HandlePlayerEnteredCell(Vector2Int cell)
    {
        if (gridManager == null || !gridManager.IsInsideGrid(cell))
        {
            return;
        }

        CellState cellState = gridManager.GetCellState(cell);
        switch (cellState)
        {
            case CellState.Claimed when isDrawing:
                CompletePath();
                break;

            case CellState.Unclaimed when isDrawing:
                AddTemporaryPathCell(cell);
                break;

            case CellState.Unclaimed:
                StartDrawing(cell);
                break;
        }
    }

    public void CancelTemporaryPath()
    {
        foreach (Vector2Int pathCell in temporaryPathCells)
        {
            if (gridManager.GetCellState(pathCell) == CellState.TemporaryPath)
            {
                gridManager.SetCellState(pathCell, CellState.Unclaimed);
            }
        }

        temporaryPathCells.Clear();
        isDrawing = false;
        capturedPercentage = CalculateCapturedPercentage();
        gameManager?.HandleCaptureUpdated(capturedPercentage);
    }

    public void ResetTerritory()
    {
        temporaryPathCells.Clear();
        isDrawing = false;
        gridManager.ResetGrid();
        RefreshBallReferences();
        capturedPercentage = CalculateCapturedPercentage();
    }

    private void StartDrawing(Vector2Int cell)
    {
        isDrawing = true;
        AddTemporaryPathCell(cell);
    }

    private void AddTemporaryPathCell(Vector2Int cell)
    {
        if (temporaryPathCells.Contains(cell))
        {
            return;
        }

        temporaryPathCells.Add(cell);
        gridManager.SetCellState(cell, CellState.TemporaryPath);
    }

    private void CompletePath()
    {
        RefreshBallReferences();

        bool[,] reachableFromBalls = FindReachableUnclaimedCellsFromBalls();
        List<Vector2Int> newlyClaimedCells = new();

        for (int x = 0; x < gridManager.Width; x++)
        {
            for (int y = 0; y < gridManager.Height; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (gridManager.GetCellState(cell) == CellState.Unclaimed && !reachableFromBalls[x, y])
                {
                    gridManager.SetCellState(cell, CellState.Claimed);
                    AddNewlyClaimedCell(newlyClaimedCells, cell);
                }
            }
        }

        foreach (Vector2Int pathCell in temporaryPathCells)
        {
            gridManager.SetCellState(pathCell, CellState.Claimed);
            AddNewlyClaimedCell(newlyClaimedCells, pathCell);
        }

        temporaryPathCells.Clear();
        isDrawing = false;
        capturedPercentage = CalculateCapturedPercentage();
        gridManager.PlayCapturePulse(newlyClaimedCells);
        gameManager?.HandleCaptureUpdated(capturedPercentage);
        Debug.Log($"Captured: {Mathf.RoundToInt(capturedPercentage)}%");
    }

    private bool[,] FindReachableUnclaimedCellsFromBalls()
    {
        bool[,] reachable = new bool[gridManager.Width, gridManager.Height];

        foreach (BallController ball in balls)
        {
            if (ball == null)
            {
                continue;
            }

            FloodFillFromBall(ball, reachable);
        }

        return reachable;
    }

    private void FloodFillFromBall(BallController ball, bool[,] reachable)
    {
        Vector3 ballPosition = ball.transform.position;
        float radius = Mathf.Max(0f, ball.HitRadius);
        Vector2Int minCell = gridManager.WorldToGrid(new Vector3(ballPosition.x - radius, ballPosition.y, ballPosition.z - radius));
        Vector2Int maxCell = gridManager.WorldToGrid(new Vector3(ballPosition.x + radius, ballPosition.y, ballPosition.z + radius));

        bool foundSeedCell = false;
        for (int x = minCell.x; x <= maxCell.x; x++)
        {
            for (int y = minCell.y; y <= maxCell.y; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (!IsValidBallSeedCell(cell, ballPosition, radius))
                {
                    continue;
                }

                foundSeedCell = true;
                FloodFillUnclaimed(cell, reachable);
            }
        }

        if (foundSeedCell)
        {
            return;
        }

        Vector2Int fallbackCell = ball.CurrentCell;
        if (gridManager.IsInsideGrid(fallbackCell) && gridManager.GetCellState(fallbackCell) == CellState.Unclaimed)
        {
            FloodFillUnclaimed(fallbackCell, reachable);
        }
    }

    private bool IsValidBallSeedCell(Vector2Int cell, Vector3 ballPosition, float radius)
    {
        if (!gridManager.IsInsideGrid(cell) || gridManager.GetCellState(cell) != CellState.Unclaimed)
        {
            return false;
        }

        Vector3 cellCenter = gridManager.GridToWorld(cell);
        float halfCellSize = gridManager.CellSize * 0.5f;
        float closestX = Mathf.Clamp(ballPosition.x, cellCenter.x - halfCellSize, cellCenter.x + halfCellSize);
        float closestZ = Mathf.Clamp(ballPosition.z, cellCenter.z - halfCellSize, cellCenter.z + halfCellSize);
        float radiusWithPadding = radius + 0.01f;
        float dx = ballPosition.x - closestX;
        float dz = ballPosition.z - closestZ;

        return dx * dx + dz * dz <= radiusWithPadding * radiusWithPadding;
    }

    private void FloodFillUnclaimed(Vector2Int startCell, bool[,] reachable)
    {
        if (reachable[startCell.x, startCell.y])
        {
            return;
        }

        Queue<Vector2Int> openCells = new();
        reachable[startCell.x, startCell.y] = true;
        openCells.Enqueue(startCell);

        while (openCells.Count > 0)
        {
            Vector2Int currentCell = openCells.Dequeue();
            foreach (Vector2Int direction in FloodFillDirections)
            {
                Vector2Int neighbor = currentCell + direction;
                if (!gridManager.IsInsideGrid(neighbor)
                    || reachable[neighbor.x, neighbor.y]
                    || gridManager.GetCellState(neighbor) != CellState.Unclaimed)
                {
                    continue;
                }

                reachable[neighbor.x, neighbor.y] = true;
                openCells.Enqueue(neighbor);
            }
        }
    }

    private float CalculateCapturedPercentage()
    {
        if (gridManager == null || gridManager.Width <= 0 || gridManager.Height <= 0)
        {
            return 0f;
        }

        int claimedCells = 0;
        int totalCells = gridManager.Width * gridManager.Height;

        for (int x = 0; x < gridManager.Width; x++)
        {
            for (int y = 0; y < gridManager.Height; y++)
            {
                CellState cellState = gridManager.GetCellState(new Vector2Int(x, y));
                if (cellState == CellState.Claimed || cellState == CellState.TemporaryPath)
                {
                    claimedCells++;
                }
            }
        }

        return (float)claimedCells / totalCells * 100f;
    }

    private void RefreshBallReferences()
    {
        balls.Clear();
        BallController[] activeBalls = FindObjectsByType<BallController>(FindObjectsSortMode.None);
        foreach (BallController ball in activeBalls)
        {
            if (ball != null && ball.isActiveAndEnabled)
            {
                balls.Add(ball);
            }
        }
    }

    private static void AddNewlyClaimedCell(List<Vector2Int> cells, Vector2Int cell)
    {
        if (!cells.Contains(cell))
        {
            cells.Add(cell);
        }
    }
}
