using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Profiling;
using UnityEngine;
using Debug = UnityEngine.Debug;

public sealed class TerritoryManager : MonoBehaviour
{
    private static readonly ProfilerMarker AddTemporaryPathCellMarker = new("Xonix.PathTiles.AddTemporaryPathCell");
    private static readonly ProfilerMarker CancelTemporaryPathMarker = new("Xonix.PathTiles.CancelTemporaryPath");
    private static readonly ProfilerMarker CompletePathMarker = new("Xonix.Capture.CompletePath");
    private static readonly ProfilerMarker CaptureResolutionMarker = new("Xonix.Capture.ResolveTerritory");
    private static readonly ProfilerMarker FindReachableCellsMarker = new("Xonix.Capture.FindReachableFromBalls");
    private static readonly ProfilerMarker FloodFillFromBallMarker = new("Xonix.Capture.FloodFillFromBall");
    private static readonly ProfilerMarker FloodFillUnclaimedMarker = new("Xonix.Capture.FloodFillUnclaimed");
    private static readonly ProfilerMarker ClaimUnreachableCellsMarker = new("Xonix.Capture.ClaimUnreachableCells");
    private static readonly ProfilerMarker ClaimTemporaryPathCellsMarker = new("Xonix.Capture.ClaimTemporaryPathCells");
    private static readonly ProfilerMarker CalculateCapturedPercentageMarker = new("Xonix.Capture.CalculateCapturedPercentage");

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

    [Header("Profiling Debug")]
    [SerializeField] private bool enableCaptureProfilingHotkeys = true;
    [SerializeField] private bool useReusableCaptureBuffers = true;
    [SerializeField] private KeyCode logCaptureProfileKey = KeyCode.F11;
    [SerializeField] private KeyCode toggleCaptureProfileModeKey = KeyCode.F12;
    [SerializeField] private bool logCaptureProfileAfterComplete = true;

    private readonly List<Vector2Int> temporaryPathCells = new();
    private readonly HashSet<int> burningPathIndices = new();
    private readonly Queue<Vector2Int> floodFillOpenCells = new();
    private readonly List<Vector2Int> newlyClaimedCellsBuffer = new();
    private bool isDrawing;
    private float capturedPercentage;
    private Coroutine pathBurnRoutine;
    private int[,] reachableFromBalls;
    private int reachableGeneration;
    private int captureProfileSamples;
    private int captureProfileLastGridCells;
    private int captureProfileLastBallCount;
    private int captureProfileLastPathCells;
    private int captureProfileLastCapturedCells;
    private int captureProfileTotalCapturedCells;
    private long captureProfileLastTicks;
    private long captureProfileTotalTicks;
    private long captureProfileMaxTicks;
    private long captureProfileLastAllocatedBytes;
    private long captureProfileTotalAllocatedBytes;
    private int floodFillProfileLastCalls;
    private int floodFillProfileTotalCalls;
    private int floodFillProfileLastVisitedCells;
    private int floodFillProfileTotalVisitedCells;
    private int floodFillProfileLastMaxQueue;
    private int floodFillProfileMaxQueue;
    private long floodFillProfileLastTicks;
    private long floodFillProfileTotalTicks;
    private long floodFillProfileMaxTicks;

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

    private void Update()
    {
        if (enableCaptureProfilingHotkeys && Input.GetKeyDown(logCaptureProfileKey))
        {
            LogAndResetCaptureProfileSamples();
        }

        if (enableCaptureProfilingHotkeys && Input.GetKeyDown(toggleCaptureProfileModeKey))
        {
            ToggleCaptureProfilingMode();
        }
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
        using (CancelTemporaryPathMarker.Auto())
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
        using (AddTemporaryPathCellMarker.Auto())
        {
            if (temporaryPathCells.Contains(cell))
            {
                return;
            }

            temporaryPathCells.Add(cell);
            gridManager.SetCellState(cell, CellState.TemporaryPath);
        }
    }

    private void CompletePath()
    {
        bool shouldLogCaptureSample = false;
        int roundedCapturedPercentage = 0;

        using (CompletePathMarker.Auto())
        {
            RefreshBallReferences();

            if (burningPathIndices.Count > 0)
            {
                CompleteDamagedPath();
                return;
            }

            ResetCurrentCaptureFloodFillProfile();

            int pathCellCount = temporaryPathCells.Count;
            int capturedCellCount;
            List<Vector2Int> newlyClaimedCells;
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            long ticksBefore = Stopwatch.GetTimestamp();

            using (CaptureResolutionMarker.Auto())
            {
                newlyClaimedCells = useReusableCaptureBuffers ? newlyClaimedCellsBuffer : new List<Vector2Int>();
                newlyClaimedCells.Clear();
                capturedCellCount = useReusableCaptureBuffers
                    ? ResolveTerritoryWithReusableBuffers(newlyClaimedCells)
                    : ResolveTerritoryWithNaiveAllocations(newlyClaimedCells);

                temporaryPathCells.Clear();
                isDrawing = false;
                capturedPercentage = CalculateCapturedPercentage();
            }

            long elapsedTicks = Stopwatch.GetTimestamp() - ticksBefore;
            long allocatedBytes = Math.Max(0L, GC.GetAllocatedBytesForCurrentThread() - allocatedBefore);

            RecordCaptureProfileSample(elapsedTicks, allocatedBytes, capturedCellCount, pathCellCount);

            gridManager.PlayCapturePulse(newlyClaimedCells);
            gameManager?.HandleCaptureUpdated(capturedPercentage);
            shouldLogCaptureSample = logCaptureProfileAfterComplete;
            roundedCapturedPercentage = Mathf.RoundToInt(capturedPercentage);
        }

        if (shouldLogCaptureSample)
        {
            // LogCaptureProfileLastSample();
        }

        // Debug.Log($"Captured: {roundedCapturedPercentage}%");
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

    public void ResetCaptureProfileSamples()
    {
        captureProfileSamples = 0;
        captureProfileLastGridCells = 0;
        captureProfileLastBallCount = 0;
        captureProfileLastPathCells = 0;
        captureProfileLastCapturedCells = 0;
        captureProfileTotalCapturedCells = 0;
        captureProfileLastTicks = 0;
        captureProfileTotalTicks = 0;
        captureProfileMaxTicks = 0;
        captureProfileLastAllocatedBytes = 0;
        captureProfileTotalAllocatedBytes = 0;
        floodFillProfileLastCalls = 0;
        floodFillProfileTotalCalls = 0;
        floodFillProfileLastVisitedCells = 0;
        floodFillProfileTotalVisitedCells = 0;
        floodFillProfileLastMaxQueue = 0;
        floodFillProfileMaxQueue = 0;
        floodFillProfileLastTicks = 0;
        floodFillProfileTotalTicks = 0;
        floodFillProfileMaxTicks = 0;
    }

    public void LogAndResetCaptureProfileSamples()
    {
        if (captureProfileSamples == 0)
        {
            Debug.Log("Xonix capture profile: no completed capture samples yet.");
            return;
        }

        double completeLastMs = TicksToMilliseconds(captureProfileLastTicks);
        double completeAvgMs = TicksToMilliseconds(captureProfileTotalTicks) / captureProfileSamples;
        double completeMaxMs = TicksToMilliseconds(captureProfileMaxTicks);
        double floodLastMs = TicksToMilliseconds(floodFillProfileLastTicks);
        double floodAvgMs = TicksToMilliseconds(floodFillProfileTotalTicks) / captureProfileSamples;
        double floodMaxCallMs = TicksToMilliseconds(floodFillProfileMaxTicks);
        double gcLastKb = captureProfileLastAllocatedBytes / 1024.0;
        double gcAvgKb = captureProfileTotalAllocatedBytes / 1024.0 / captureProfileSamples;
        double capturedAvg = (double)captureProfileTotalCapturedCells / captureProfileSamples;

        Debug.Log(
            "Xonix capture profile: "
            + $"mode={GetCaptureProfilingModeName()}, "
            + $"samples={captureProfileSamples}, "
            + $"gridCells={captureProfileLastGridCells}, "
            + $"balls={captureProfileLastBallCount}, "
            + $"pathCellsLast={captureProfileLastPathCells}, "
            + $"capturedCellsLast={captureProfileLastCapturedCells}, "
            + $"capturedCellsAvg={capturedAvg:F1}, "
            + $"completePathCpuMs(last/avg/max)={completeLastMs:F4}/{completeAvgMs:F4}/{completeMaxMs:F4}, "
            + $"floodFillCpuMs(last/avgPerCapture/maxCall)={floodLastMs:F4}/{floodAvgMs:F4}/{floodMaxCallMs:F4}, "
            + $"floodFillCallsLast={floodFillProfileLastCalls}, "
            + $"floodFillCellsLast={floodFillProfileLastVisitedCells}, "
            + $"floodFillCallsTotal={floodFillProfileTotalCalls}, "
            + $"maxQueueLast={floodFillProfileLastMaxQueue}, "
            + $"maxQueueOverall={floodFillProfileMaxQueue}, "
            + $"gcKB(last/avg)={gcLastKb:F3}/{gcAvgKb:F3}"
        );

        ResetCaptureProfileSamples();
    }

    private void ToggleCaptureProfilingMode()
    {
        useReusableCaptureBuffers = !useReusableCaptureBuffers;
        ResetCaptureProfileSamples();
        Debug.Log($"Xonix capture profiling mode: mode={GetCaptureProfilingModeName()}. Capture samples reset.");
    }

    private int ResolveTerritoryWithReusableBuffers(List<Vector2Int> newlyClaimedCells)
    {
        PrepareReachabilityMap();
        FindReachableUnclaimedCellsFromBalls();
        int capturedCellCount = ClaimUnreachableUnclaimedCells(newlyClaimedCells);
        capturedCellCount += ClaimTemporaryPathCells(newlyClaimedCells);
        return capturedCellCount;
    }

    private int ResolveTerritoryWithNaiveAllocations(List<Vector2Int> newlyClaimedCells)
    {
        bool[,] reachable = FindReachableUnclaimedCellsFromBallsNaive();
        int capturedCellCount = ClaimUnreachableUnclaimedCellsNaive(newlyClaimedCells, reachable);
        capturedCellCount += ClaimTemporaryPathCells(newlyClaimedCells);
        return capturedCellCount;
    }

    private void FindReachableUnclaimedCellsFromBalls()
    {
        using (FindReachableCellsMarker.Auto())
        {
            foreach (BallController ball in balls)
            {
                if (ball == null)
                {
                    continue;
                }

                FloodFillFromBall(ball);
            }
        }
    }

    private bool[,] FindReachableUnclaimedCellsFromBallsNaive()
    {
        using (FindReachableCellsMarker.Auto())
        {
            bool[,] reachable = new bool[gridManager.Width, gridManager.Height];

            foreach (BallController ball in balls)
            {
                if (ball == null)
                {
                    continue;
                }

                FloodFillFromBallNaive(ball, reachable);
            }

            return reachable;
        }
    }

    private void FloodFillFromBall(BallController ball)
    {
        using (FloodFillFromBallMarker.Auto())
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
                    FloodFillUnclaimed(cell);
                }
            }

            if (foundSeedCell)
            {
                return;
            }

            Vector2Int fallbackCell = ball.CurrentCell;
            if (gridManager.IsInsideGrid(fallbackCell) && gridManager.GetCellState(fallbackCell) == CellState.Unclaimed)
            {
                FloodFillUnclaimed(fallbackCell);
            }
        }
    }

    private void FloodFillFromBallNaive(BallController ball, bool[,] reachable)
    {
        using (FloodFillFromBallMarker.Auto())
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
                    FloodFillUnclaimedNaive(cell, reachable);
                }
            }

            if (foundSeedCell)
            {
                return;
            }

            Vector2Int fallbackCell = ball.CurrentCell;
            if (gridManager.IsInsideGrid(fallbackCell) && gridManager.GetCellState(fallbackCell) == CellState.Unclaimed)
            {
                FloodFillUnclaimedNaive(fallbackCell, reachable);
            }
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

    private void FloodFillUnclaimedNaive(Vector2Int startCell, bool[,] reachable)
    {
        using (FloodFillUnclaimedMarker.Auto())
        {
            long ticksBefore = Stopwatch.GetTimestamp();
            int visitedCells = 0;
            int maxQueueCount = 0;

            if (reachable[startCell.x, startCell.y])
            {
                RecordFloodFillProfileSample(Stopwatch.GetTimestamp() - ticksBefore, 0, 0);
                return;
            }

            Queue<Vector2Int> openCells = new();
            reachable[startCell.x, startCell.y] = true;
            openCells.Enqueue(startCell);

            while (openCells.Count > 0)
            {
                maxQueueCount = Mathf.Max(maxQueueCount, openCells.Count);
                Vector2Int currentCell = openCells.Dequeue();
                visitedCells++;

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

            RecordFloodFillProfileSample(Stopwatch.GetTimestamp() - ticksBefore, visitedCells, maxQueueCount);
        }
    }

    private void FloodFillUnclaimed(Vector2Int startCell)
    {
        using (FloodFillUnclaimedMarker.Auto())
        {
            long ticksBefore = Stopwatch.GetTimestamp();
            int visitedCells = 0;
            int maxQueueCount = 0;

            if (IsMarkedReachable(startCell.x, startCell.y))
            {
                RecordFloodFillProfileSample(Stopwatch.GetTimestamp() - ticksBefore, 0, 0);
                return;
            }

            floodFillOpenCells.Clear();
            MarkReachable(startCell.x, startCell.y);
            floodFillOpenCells.Enqueue(startCell);

            while (floodFillOpenCells.Count > 0)
            {
                maxQueueCount = Mathf.Max(maxQueueCount, floodFillOpenCells.Count);
                Vector2Int currentCell = floodFillOpenCells.Dequeue();
                visitedCells++;

                foreach (Vector2Int direction in FloodFillDirections)
                {
                    Vector2Int neighbor = currentCell + direction;
                    if (!gridManager.IsInsideGrid(neighbor)
                        || IsMarkedReachable(neighbor.x, neighbor.y)
                        || gridManager.GetCellState(neighbor) != CellState.Unclaimed)
                    {
                        continue;
                    }

                    MarkReachable(neighbor.x, neighbor.y);
                    floodFillOpenCells.Enqueue(neighbor);
                }
            }

            RecordFloodFillProfileSample(Stopwatch.GetTimestamp() - ticksBefore, visitedCells, maxQueueCount);
        }
    }

    private float CalculateCapturedPercentage()
    {
        using (CalculateCapturedPercentageMarker.Auto())
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

    private void PrepareReachabilityMap()
    {
        int width = gridManager.Width;
        int height = gridManager.Height;
        if (reachableFromBalls == null || reachableFromBalls.GetLength(0) != width || reachableFromBalls.GetLength(1) != height)
        {
            reachableFromBalls = new int[width, height];
            reachableGeneration = 0;
        }

        reachableGeneration++;
        if (reachableGeneration == int.MaxValue)
        {
            Array.Clear(reachableFromBalls, 0, reachableFromBalls.Length);
            reachableGeneration = 1;
        }
    }

    private int ClaimUnreachableUnclaimedCells(List<Vector2Int> newlyClaimedCells)
    {
        using (ClaimUnreachableCellsMarker.Auto())
        {
            int capturedCellCount = 0;
            for (int x = 0; x < gridManager.Width; x++)
            {
                for (int y = 0; y < gridManager.Height; y++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    if (gridManager.GetCellState(cell) == CellState.Unclaimed && !IsMarkedReachable(x, y))
                    {
                        newlyClaimedCells.Add(cell);
                        capturedCellCount++;
                    }
                }
            }

            gridManager.SetCellStatesBulk(newlyClaimedCells, CellState.Claimed);
            return capturedCellCount;
        }
    }

    private int ClaimTemporaryPathCells(List<Vector2Int> newlyClaimedCells)
    {
        using (ClaimTemporaryPathCellsMarker.Auto())
        {
            int capturedCellCount = 0;
            foreach (Vector2Int pathCell in temporaryPathCells)
            {
                newlyClaimedCells.Add(pathCell);
                capturedCellCount++;
            }

            gridManager.SetCellStatesBulk(temporaryPathCells, CellState.Claimed);
            return capturedCellCount;
        }
    }

    private int ClaimUnreachableUnclaimedCellsNaive(List<Vector2Int> newlyClaimedCells, bool[,] reachable)
    {
        using (ClaimUnreachableCellsMarker.Auto())
        {
            int capturedCellCount = 0;
            for (int x = 0; x < gridManager.Width; x++)
            {
                for (int y = 0; y < gridManager.Height; y++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    if (gridManager.GetCellState(cell) == CellState.Unclaimed && !reachable[x, y])
                    {
                        gridManager.SetCellState(cell, CellState.Claimed);
                        if (AddNewlyClaimedCell(newlyClaimedCells, cell))
                        {
                            capturedCellCount++;
                        }
                    }
                }
            }

            return capturedCellCount;
        }
    }

    private bool IsMarkedReachable(int x, int y)
    {
        return reachableFromBalls != null && reachableFromBalls[x, y] == reachableGeneration;
    }

    private void MarkReachable(int x, int y)
    {
        reachableFromBalls[x, y] = reachableGeneration;
    }

    private void ResetCurrentCaptureFloodFillProfile()
    {
        floodFillProfileLastCalls = 0;
        floodFillProfileLastVisitedCells = 0;
        floodFillProfileLastMaxQueue = 0;
        floodFillProfileLastTicks = 0;
    }

    private void RecordFloodFillProfileSample(long elapsedTicks, int visitedCells, int maxQueueCount)
    {
        floodFillProfileLastCalls++;
        floodFillProfileTotalCalls++;
        floodFillProfileLastVisitedCells += visitedCells;
        floodFillProfileTotalVisitedCells += visitedCells;
        floodFillProfileLastMaxQueue = Mathf.Max(floodFillProfileLastMaxQueue, maxQueueCount);
        floodFillProfileMaxQueue = Mathf.Max(floodFillProfileMaxQueue, maxQueueCount);
        floodFillProfileLastTicks += elapsedTicks;
        floodFillProfileTotalTicks += elapsedTicks;
        floodFillProfileMaxTicks = Math.Max(floodFillProfileMaxTicks, elapsedTicks);
    }

    private void RecordCaptureProfileSample(long elapsedTicks, long allocatedBytes, int capturedCellCount, int pathCellCount)
    {
        captureProfileSamples++;
        captureProfileLastGridCells = gridManager.Width * gridManager.Height;
        captureProfileLastBallCount = balls.Count;
        captureProfileLastPathCells = pathCellCount;
        captureProfileLastCapturedCells = capturedCellCount;
        captureProfileTotalCapturedCells += capturedCellCount;
        captureProfileLastTicks = elapsedTicks;
        captureProfileTotalTicks += elapsedTicks;
        captureProfileMaxTicks = Math.Max(captureProfileMaxTicks, elapsedTicks);
        captureProfileLastAllocatedBytes = allocatedBytes;
        captureProfileTotalAllocatedBytes += allocatedBytes;
    }

    private void LogCaptureProfileLastSample()
    {
        Debug.Log(
            "Xonix capture sample: "
            + $"mode={GetCaptureProfilingModeName()}, "
            + $"gridCells={captureProfileLastGridCells}, "
            + $"balls={captureProfileLastBallCount}, "
            + $"pathCells={captureProfileLastPathCells}, "
            + $"capturedCells={captureProfileLastCapturedCells}, "
            + $"completePathCpuMs={TicksToMilliseconds(captureProfileLastTicks):F4}, "
            + $"floodFillCpuMs={TicksToMilliseconds(floodFillProfileLastTicks):F4}, "
            + $"floodFillCalls={floodFillProfileLastCalls}, "
            + $"floodFillCells={floodFillProfileLastVisitedCells}, "
            + $"maxQueue={floodFillProfileLastMaxQueue}, "
            + $"gcKB={captureProfileLastAllocatedBytes / 1024.0:F3}"
        );
    }

    private string GetCaptureProfilingModeName()
    {
        return useReusableCaptureBuffers ? "optimized" : "naive";
    }

    private static double TicksToMilliseconds(long ticks)
    {
        return ticks * 1000.0 / Stopwatch.Frequency;
    }

    private static bool AddNewlyClaimedCell(List<Vector2Int> cells, Vector2Int cell)
    {
        if (cells.Contains(cell))
        {
            return false;
        }

        cells.Add(cell);
        return true;
    }
}
