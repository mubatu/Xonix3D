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

        RefreshBallReferencesIfNeeded();
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
        RefreshBallReferencesIfNeeded();
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
        RefreshBallReferencesIfNeeded();

        bool[,] reachableFromBalls = FindReachableUnclaimedCellsFromBalls();

        for (int x = 0; x < gridManager.Width; x++)
        {
            for (int y = 0; y < gridManager.Height; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (gridManager.GetCellState(cell) == CellState.Unclaimed && !reachableFromBalls[x, y])
                {
                    gridManager.SetCellState(cell, CellState.Claimed);
                }
            }
        }

        foreach (Vector2Int pathCell in temporaryPathCells)
        {
            gridManager.SetCellState(pathCell, CellState.Claimed);
        }

        temporaryPathCells.Clear();
        isDrawing = false;
        capturedPercentage = CalculateCapturedPercentage();
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

            Vector2Int ballCell = ball.CurrentCell;
            if (!gridManager.IsInsideGrid(ballCell) || gridManager.GetCellState(ballCell) != CellState.Unclaimed)
            {
                continue;
            }

            FloodFillUnclaimed(ballCell, reachable);
        }

        return reachable;
    }

    private void FloodFillUnclaimed(Vector2Int startCell, bool[,] reachable)
    {
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

    private void RefreshBallReferencesIfNeeded()
    {
        balls.RemoveAll(ball => ball == null);
        if (balls.Count > 0)
        {
            return;
        }

        balls.AddRange(FindObjectsByType<BallController>(FindObjectsSortMode.None));
    }
}
