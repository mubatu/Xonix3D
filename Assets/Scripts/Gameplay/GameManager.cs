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
    private GUIStyle hudStyle;
    private GUIStyle messageStyle;

    public int Lives => lives;
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

    private void OnGUI()
    {
        EnsureGuiStyles();

        string hudText =
            $"Lives: {lives}\n"
            + $"Level: {levelNumber}\n"
            + $"Captured: {Mathf.RoundToInt(capturedPercentage)}% / {Mathf.RoundToInt(requiredCapturePercentage)}%";

        GUI.Label(new Rect(20f, 20f, 360f, 120f), hudText, hudStyle);

        if (isLevelComplete)
        {
            DrawCenteredMessage("Level Complete!\nPress R to Restart");
        }
        else if (isGameOver)
        {
            DrawCenteredMessage("Game Over\nPress R to Restart");
        }
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

    private void EnsureGuiStyles()
    {
        if (hudStyle != null && messageStyle != null)
        {
            return;
        }

        hudStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 24
        };
        hudStyle.normal.textColor = Color.white;

        messageStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 48,
            fontStyle = FontStyle.Bold
        };
        messageStyle.normal.textColor = Color.white;
    }

    private void DrawCenteredMessage(string message)
    {
        Rect rect = new Rect(0f, Screen.height * 0.36f, Screen.width, 160f);
        GUI.Label(rect, message, messageStyle);
    }

    private void RefreshBallReferencesIfNeeded()
    {
        if (balls != null && balls.Length > 0)
        {
            return;
        }

        balls = FindObjectsByType<BallController>(FindObjectsSortMode.None);
    }
}
