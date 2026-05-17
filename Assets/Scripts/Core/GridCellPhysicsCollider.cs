using UnityEngine;

public sealed class GridCellPhysicsCollider : MonoBehaviour
{
    public GridManager GridManager { get; private set; }

    public void Configure(GridManager gridManager)
    {
        GridManager = gridManager;
    }
}
