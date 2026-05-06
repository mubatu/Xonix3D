using System.Collections;
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

    [Header("Death Feedback")]
    [SerializeField] private float respawnDelay = 0.55f;
    [SerializeField] private int deathFlashCount = 4;
    [SerializeField] private float deathPopScale = 1.35f;

    private int lives;
    private bool hasStarted;
    private bool isGameOver;
    private bool isLevelComplete;
    private bool isRespawning;
    private float capturedPercentage;
    private int lastDeathFrame = -1;

    public int StartingLives => startingLives;
    public int LevelNumber => levelNumber;
    public int Lives => lives;
    public float CapturedPercentage => capturedPercentage;
    public float RequiredCapturePercentage => requiredCapturePercentage;
    public bool HasStarted => hasStarted;
    public bool IsGameOver => isGameOver;
    public bool IsLevelComplete => isLevelComplete;
    public bool IsMainMenuActive => !hasStarted && !isGameOver && !isLevelComplete;
    public bool IsGameplayStopped => !hasStarted || isGameOver || isLevelComplete;
    public bool IsPlayerControlLocked => IsGameplayStopped || isRespawning;

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
        if (IsMainMenuActive && Input.GetKeyDown(KeyCode.Return))
        {
            StartGame();
        }

        if ((isGameOver || isLevelComplete) && Input.GetKeyDown(KeyCode.R))
        {
            RestartLevel();
        }
    }

    private void ResetGameState()
    {
        isGameOver = false;
        isLevelComplete = false;
        isRespawning = false;
        lastDeathFrame = -1;
        lives = startingLives;
        capturedPercentage = territoryManager != null ? territoryManager.CapturedPercentage : 0f;
    }

    public void StartGame()
    {
        RestartLevel();
    }

    public void HandlePlayerDeath()
    {
        if (IsGameplayStopped || isRespawning || lastDeathFrame == Time.frameCount)
        {
            return;
        }

        lastDeathFrame = Time.frameCount;
        StartCoroutine(HandlePlayerDeathRoutine());
    }

    public void RestartLevel()
    {
        hasStarted = true;
        RefreshBallReferencesIfNeeded();
        StopAllCoroutines();
        territoryManager?.ResetTerritory();
        playerController?.Respawn();

        foreach (BallController ball in balls)
        {
            ball?.ResetBall();
        }

        ResetGameState();
        Debug.Log("Level restarted");
    }

    public void ReturnToMainMenu()
    {
        hasStarted = false;
        RefreshBallReferencesIfNeeded();
        StopAllCoroutines();
        territoryManager?.ResetTerritory();
        playerController?.Respawn();

        foreach (BallController ball in balls)
        {
            ball?.ResetBall();
        }

        ResetGameState();
        Debug.Log("Returned to main menu");
    }

    public void StartNextLevelPlaceholder()
    {
        Debug.Log("Next level placeholder: restarting current level.");
        RestartLevel();
    }

    public void QuitGame()
    {
        Debug.Log("Quit requested");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private IEnumerator HandlePlayerDeathRoutine()
    {
        isRespawning = true;
        lives--;

        territoryManager?.CancelTemporaryPath();

        if (playerController != null)
        {
            yield return playerController.PlayDeathFeedback(respawnDelay, deathFlashCount, deathPopScale);
        }
        else
        {
            yield return new WaitForSeconds(respawnDelay);
        }

        if (lives <= 0)
        {
            isGameOver = true;
            isRespawning = false;
            Debug.Log("Game Over");
            yield break;
        }

        playerController?.Respawn();
        isRespawning = false;
        Debug.Log($"Player died. Lives remaining: {lives}");
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
