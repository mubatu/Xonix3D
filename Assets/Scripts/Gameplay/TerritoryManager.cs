using System.Collections;
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
    [SerializeField] private PlayerController playerController;
    [SerializeField] private List<BallController> balls = new();

    [Header("Path Danger")]
    [SerializeField] private float pathBurnSpreadInterval = 0.18f;

    private readonly List<Vector2Int> temporaryPathCells = new();
    private readonly HashSet<int> burningPathIndices = new();
    private bool isDrawing;
    private float capturedPercentage;
    private Coroutine pathBurnRoutine;

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

        if (playerController == null)
        {
            playerController = FindFirstObjectByType<PlayerController>();
        }

        RefreshBallReferences();
        capturedPercentage = CalculateCapturedPercentage();
        gameManager?.HandleCaptureUpdated(capturedPercentage);
    }

    public void HandlePlayerEnteredCell(Vector2Int cell)
    {
        if (gridManager == null)
        {
            return;
        }

        if (!gridManager.IsInsideGrid(cell))
        {
            if (isDrawing)
            {
                CompletePath();
            }

            return;
        }

        CellState cellState = gridManager.GetCellState(cell);
        switch (cellState)
        {
            case CellState.Claimed when isDrawing:
                CompletePath();
                break;

            case CellState.BurningPath:
                gameManager?.HandlePlayerDeath();
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
        StopPathBurn();

        foreach (Vector2Int pathCell in temporaryPathCells)
        {
            CellState cellState = gridManager.GetCellState(pathCell);
            if (cellState == CellState.TemporaryPath || cellState == CellState.BurningPath)
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
        StopPathBurn();
        temporaryPathCells.Clear();
        isDrawing = false;
        gridManager.ResetGrid();
        RefreshBallReferences();
        capturedPercentage = CalculateCapturedPercentage();
    }

    public void HandleBallTouchedPath(Vector2Int touchedCell)
    {
        if (!isDrawing || gameManager == null || gameManager.IsGameplayStopped)
        {
            return;
        }

        int pathIndex = temporaryPathCells.IndexOf(touchedCell);
        if (pathIndex < 0)
        {
            pathIndex = FindClosestTemporaryPathIndex(touchedCell);
        }

        if (pathIndex < 0)
        {
            return;
        }

        IgnitePathIndex(pathIndex);
        CheckBurnReachedPlayer();

        if (pathBurnRoutine == null)
        {
            pathBurnRoutine = StartCoroutine(SpreadPathBurnRoutine());
        }
    }

    public void HandleClaimedCellsDestroyed(IReadOnlyList<Vector2Int> destroyedCells)
    {
        if (destroyedCells == null || destroyedCells.Count == 0)
        {
            return;
        }

        capturedPercentage = CalculateCapturedPercentage();
        gameManager?.HandleCaptureUpdated(capturedPercentage);
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

        if (burningPathIndices.Count > 0)
        {
            CompleteDamagedPath();
            return;
        }

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

    private void CompleteDamagedPath()
    {
        if (pathBurnRoutine != null)
        {
            StopCoroutine(pathBurnRoutine);
            pathBurnRoutine = null;
        }

        List<Vector2Int> newlyClaimedCells = new();
        for (int i = 0; i < temporaryPathCells.Count; i++)
        {
            Vector2Int pathCell = temporaryPathCells[i];
            if (burningPathIndices.Contains(i))
            {
                gridManager.SetCellState(pathCell, CellState.Unclaimed);
                continue;
            }

            gridManager.SetCellState(pathCell, CellState.Claimed);
            AddNewlyClaimedCell(newlyClaimedCells, pathCell);
        }

        temporaryPathCells.Clear();
        burningPathIndices.Clear();
        isDrawing = false;
        capturedPercentage = CalculateCapturedPercentage();
        gridManager.PlayCapturePulse(newlyClaimedCells);
        gameManager?.HandleCaptureUpdated(capturedPercentage);
        Debug.Log("Damaged path closed: safe path cells became claimed, red cells broke away.");
    }

    private IEnumerator SpreadPathBurnRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(Mathf.Max(0.03f, pathBurnSpreadInterval));

        while (isDrawing && burningPathIndices.Count > 0)
        {
            yield return wait;

            List<int> indicesToIgnite = new();
            foreach (int pathIndex in burningPathIndices)
            {
                AddPathIndexIfValid(indicesToIgnite, pathIndex - 1);
                AddPathIndexIfValid(indicesToIgnite, pathIndex + 1);
            }

            if (indicesToIgnite.Count == 0)
            {
                pathBurnRoutine = null;
                yield break;
            }

            foreach (int pathIndex in indicesToIgnite)
            {
                IgnitePathIndex(pathIndex);
            }

            CheckBurnReachedPlayer();
        }

        pathBurnRoutine = null;
    }

    private void IgnitePathIndex(int pathIndex)
    {
        if (pathIndex < 0 || pathIndex >= temporaryPathCells.Count || !burningPathIndices.Add(pathIndex))
        {
            return;
        }

        Vector2Int pathCell = temporaryPathCells[pathIndex];
        if (gridManager.GetCellState(pathCell) == CellState.TemporaryPath)
        {
            gridManager.SetCellState(pathCell, CellState.BurningPath);
        }
    }

    private void CheckBurnReachedPlayer()
    {
        if (playerController == null)
        {
            playerController = FindFirstObjectByType<PlayerController>();
        }

        if (playerController == null)
        {
            return;
        }

        Vector2Int playerCell = playerController.CurrentCell;
        Vector2Int playerWorldCell = gridManager.WorldToGrid(playerController.transform.position);
        if (IsBurningPathCell(playerCell) || IsBurningPathCell(playerWorldCell))
        {
            gameManager?.HandlePlayerDeath();
        }
    }

    private bool IsBurningPathCell(Vector2Int cell)
    {
        int pathIndex = temporaryPathCells.IndexOf(cell);
        return pathIndex >= 0 && burningPathIndices.Contains(pathIndex);
    }

    private int FindClosestTemporaryPathIndex(Vector2Int touchedCell)
    {
        int closestIndex = -1;
        int closestDistance = int.MaxValue;

        for (int i = 0; i < temporaryPathCells.Count; i++)
        {
            Vector2Int pathCell = temporaryPathCells[i];
            CellState cellState = gridManager.GetCellState(pathCell);
            if (cellState != CellState.TemporaryPath && cellState != CellState.BurningPath)
            {
                continue;
            }

            int distance = Mathf.Abs(pathCell.x - touchedCell.x) + Mathf.Abs(pathCell.y - touchedCell.y);
            if (distance >= closestDistance)
            {
                continue;
            }

            closestDistance = distance;
            closestIndex = i;
        }

        return closestDistance <= 2 ? closestIndex : -1;
    }

    private void AddPathIndexIfValid(List<int> indices, int pathIndex)
    {
        if (pathIndex < 0 || pathIndex >= temporaryPathCells.Count || burningPathIndices.Contains(pathIndex) || indices.Contains(pathIndex))
        {
            return;
        }

        CellState cellState = gridManager.GetCellState(temporaryPathCells[pathIndex]);
        if (cellState == CellState.TemporaryPath)
        {
            indices.Add(pathIndex);
        }
    }

    private void StopPathBurn()
    {
        if (pathBurnRoutine != null)
        {
            StopCoroutine(pathBurnRoutine);
            pathBurnRoutine = null;
        }

        burningPathIndices.Clear();
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
                if (cellState == CellState.Claimed || cellState == CellState.TemporaryPath || cellState == CellState.BurningPath)
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
