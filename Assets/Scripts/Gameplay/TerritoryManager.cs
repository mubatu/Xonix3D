using System.Collections.Generic;
using UnityEngine;

public sealed class TerritoryManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GridManager gridManager;

    private readonly List<Vector2Int> temporaryPathCells = new();
    private bool isDrawing;

    public bool IsDrawing => isDrawing;
    public IReadOnlyList<Vector2Int> TemporaryPathCells => temporaryPathCells;

    private void Awake()
    {
        if (gridManager == null)
        {
            gridManager = FindFirstObjectByType<GridManager>();
        }
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
                CompletePathPrototype();
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

    private void CompletePathPrototype()
    {
        foreach (Vector2Int pathCell in temporaryPathCells)
        {
            gridManager.SetCellState(pathCell, CellState.Claimed);
        }

        temporaryPathCells.Clear();
        isDrawing = false;
    }
}
