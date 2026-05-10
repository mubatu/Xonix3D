using System;
using UnityEngine;

[Serializable]
public sealed class LevelData
{
    public int levelNumber = 1;
    public int width = 40;
    public int height = 40;
    public float cellSize = 1f;
    public float requiredCapturePercentage = 75f;
    public LevelCell playerSpawnCell = new LevelCell { x = 20, y = 0 };
    public LevelClaimedArea[] initiallyClaimedAreas;
    public LevelCell[] initiallyClaimedCells;
    public LevelBallData[] balls;
}

[Serializable]
public sealed class LevelCell
{
    public int x;
    public int y;

    public Vector2Int ToVector2Int()
    {
        return new Vector2Int(x, y);
    }
}

[Serializable]
public sealed class LevelClaimedArea
{
    public int x;
    public int y;
    public int width = 1;
    public int height = 1;
}

[Serializable]
public sealed class LevelBallData
{
    public string ballType = "Normal";
    public LevelCell spawnCell = new LevelCell();
    public LevelVector2 direction = new LevelVector2 { x = 1f, y = 1f };
    public float speed = 4f;
    public float hitRadius = 0.45f;
    public float playerHitRadius = 0.45f;
    public float groundOffset = 1f;
}

[Serializable]
public sealed class LevelVector2
{
    public float x;
    public float y;

    public Vector2 ToVector2()
    {
        return new Vector2(x, y);
    }
}
