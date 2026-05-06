using UnityEngine;

public sealed class GameManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private TerritoryManager territoryManager;
    [SerializeField] private BallController[] balls;

    [Header("State")]
    [SerializeField] private int startingLives = 3;
    [SerializeField] private int levelNumber = 1;
    [SerializeField] private float requiredCapturePercentage = 75f;

    private int lives;
    private bool isGameOver;
    private bool isLevelComplete;
    private float capturedPercentage;
    private int lastDeathFrame = -1;

    public int StartingLives => startingLives;
    public int LevelNumber => levelNumber;
    public int Lives => lives;
    public float CapturedPercentage => capturedPercentage;
    public float RequiredCapturePercentage => requiredCapturePercentage;
    public bool IsGameOver => isGameOver;
    public bool IsLevelComplete => isLevelComplete;
    public bool IsGameplayStopped => isGameOver || isLevelComplete;

    private void Awake()
    {
        if (playerController == null)
        {
            playerController = FindFirstObjectByType<PlayerController>();
        }

        if (territoryManager == null)
        {
            territoryManager = FindFirstObjectByType<TerritoryManager>();
        }

        RefreshBallReferencesIfNeeded();
        ResetGameState();
        EnsureHudExists();
    }

    private void Update()
    {
        if (IsGameplayStopped && Input.GetKeyDown(KeyCode.R))
        {
            RestartLevel();
        }
    }

    private void ResetGameState()
    {
        isGameOver = false;
        isLevelComplete = false;
        lastDeathFrame = -1;
        lives = startingLives;
        capturedPercentage = territoryManager != null ? territoryManager.CapturedPercentage : 0f;
    }

    public void HandlePlayerDeath()
    {
        if (IsGameplayStopped || lastDeathFrame == Time.frameCount)
        {
            return;
        }

        lastDeathFrame = Time.frameCount;
        lives--;

        territoryManager?.CancelTemporaryPath();
        playerController?.Respawn();

        if (lives <= 0)
        {
            isGameOver = true;
            Debug.Log("Game Over");
            return;
        }

        Debug.Log($"Player died. Lives remaining: {lives}");
    }

    public void RestartLevel()
    {
        RefreshBallReferencesIfNeeded();
        territoryManager?.ResetTerritory();
        playerController?.Respawn();

        foreach (BallController ball in balls)
        {
            ball?.ResetBall();
        }

        ResetGameState();
        Debug.Log("Level restarted");
    }

    public void HandleCaptureUpdated(float newCapturedPercentage)
    {
        if (IsGameplayStopped)
        {
            return;
        }

        capturedPercentage = newCapturedPercentage;
        if (capturedPercentage < requiredCapturePercentage)
        {
            return;
        }

        isLevelComplete = true;
        territoryManager?.CancelTemporaryPath();
        Debug.Log("Level Complete!");
    }

    private void RefreshBallReferencesIfNeeded()
    {
        if (balls != null && balls.Length > 0)
        {
            return;
        }

        balls = FindObjectsByType<BallController>(FindObjectsSortMode.None);
    }

    private void EnsureHudExists()
    {
        if (FindFirstObjectByType<GameHud>() != null)
        {
            return;
        }

        gameObject.AddComponent<GameHud>();
    }
}
