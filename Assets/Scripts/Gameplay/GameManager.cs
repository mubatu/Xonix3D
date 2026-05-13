using System;
using System.Collections;
using UnityEngine;

public sealed class GameManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private TerritoryManager territoryManager;
    [SerializeField] private BallController ballPrefab;
    [SerializeField] private Transform ballsRoot;
    [SerializeField] private BallController[] balls;

    [Header("Level Data")]
    [SerializeField] private string levelsResourceFolder = "Levels";
    [SerializeField] private string playableLevelFilePrefix = "Level_";

    [Header("Camera")]
    [SerializeField] private bool autoFrameCameraToGrid = true;
    [SerializeField] private Camera gameplayCamera;
    [SerializeField] private float cameraHeightScale = 0.75f;
    [SerializeField] private float cameraBackOffsetScale = 0.575f;
    [SerializeField] private float minCameraHeight = 12f;

    [Header("State")]
    [SerializeField] private int startingLives = 3;
    [SerializeField] private int levelNumber = 1;

    [Header("Death Feedback")]
    [SerializeField] private float respawnDelay = 0.55f;
    [SerializeField] private int deathFlashCount = 4;
    [SerializeField] private float deathPopScale = 1.35f;

    [Header("Debug Shortcuts")]
    [SerializeField] private bool enableDebugShortcuts = true;
    [SerializeField] private KeyCode reloadLevelFromJsonKey = KeyCode.F8;

    [Header("FPS Logging")]
    [SerializeField] private bool enableFpsLogging = true;
    [SerializeField] private float fpsLogInterval = 10f;
    [SerializeField] private KeyCode logFpsNowKey = KeyCode.F7;

    private int lives;
    private bool hasStarted;
    private bool isGameOver;
    private bool isLevelComplete;
    private bool isCampaignComplete;
    private bool isRespawning;
    private float capturedPercentage;
    private float requiredCapturePercentage = 75f;
    private int lastDeathFrame = -1;
    private int currentLevelIndex;
    private TextAsset[] levelFiles;
    private int fpsIntervalFrames;
    private int fpsSessionFrames;
    private float fpsIntervalElapsed;
    private float fpsSessionElapsed;
    private bool hasLoggedFinalFpsSample;

    public int StartingLives => startingLives;
    public int LevelNumber => levelNumber;
    public int Lives => lives;
    public float CapturedPercentage => capturedPercentage;
    public float RequiredCapturePercentage => requiredCapturePercentage;
    public bool HasStarted => hasStarted;
    public bool IsGameOver => isGameOver;
    public bool IsLevelComplete => isLevelComplete;
    public bool IsCampaignComplete => isCampaignComplete;
    public bool IsMainMenuActive => !hasStarted && !isGameOver && !isLevelComplete && !isCampaignComplete;
    public bool IsGameplayStopped => !hasStarted || isGameOver || isLevelComplete || isCampaignComplete;
    public bool IsPlayerControlLocked => IsGameplayStopped || isRespawning;

    private void Awake()
    {
        if (gridManager == null)
        {
            gridManager = FindFirstObjectByType<GridManager>();
        }

        if (playerController == null)
        {
            playerController = FindFirstObjectByType<PlayerController>();
        }

        if (territoryManager == null)
        {
            territoryManager = FindFirstObjectByType<TerritoryManager>();
        }

        RefreshBallReferencesIfNeeded();
        LoadLevelFiles();
        ApplyCurrentLevelData();
        ResetGameState(true);
        EnsureHudExists();
    }

    private void Update()
    {
        UpdateFpsLogging();

        if (enableDebugShortcuts && Input.GetKeyDown(reloadLevelFromJsonKey))
        {
            ReloadCurrentLevelFromJson();
            return;
        }

        if (IsMainMenuActive && Input.GetKeyDown(KeyCode.Return))
        {
            StartGame();
        }

        if (isLevelComplete && Input.GetKeyDown(KeyCode.Return))
        {
            StartNextLevel();
        }
    }

    private void UpdateFpsLogging()
    {
        if (!enableFpsLogging)
        {
            return;
        }

        if (Input.GetKeyDown(logFpsNowKey))
        {
            LogFpsSample("manual");
            return;
        }

        if (IsGameplayStopped)
        {
            if (fpsSessionFrames > 0 && !hasLoggedFinalFpsSample)
            {
                LogFpsSample("final");
                hasLoggedFinalFpsSample = true;
            }

            return;
        }

        hasLoggedFinalFpsSample = false;
        float deltaTime = Time.unscaledDeltaTime;
        if (deltaTime <= 0f)
        {
            return;
        }

        fpsIntervalElapsed += deltaTime;
        fpsSessionElapsed += deltaTime;
        fpsIntervalFrames++;
        fpsSessionFrames++;

        if (fpsIntervalElapsed >= Mathf.Max(1f, fpsLogInterval))
        {
            LogFpsSample("interval");
            fpsIntervalElapsed = 0f;
            fpsIntervalFrames = 0;
        }
    }

    private void ResetFpsLogging()
    {
        fpsIntervalFrames = 0;
        fpsSessionFrames = 0;
        fpsIntervalElapsed = 0f;
        fpsSessionElapsed = 0f;
        hasLoggedFinalFpsSample = false;
    }

    private void LogFpsSample(string sampleType)
    {
        if (fpsSessionFrames == 0 || fpsSessionElapsed <= 0f)
        {
            Debug.Log("Xonix FPS: no gameplay samples recorded yet.");
            return;
        }

        float intervalAverage = fpsIntervalElapsed > 0f ? fpsIntervalFrames / fpsIntervalElapsed : 0f;
        float sessionAverage = fpsSessionFrames / fpsSessionElapsed;
        int activeBallCount = GetActiveBallCount();
        string intervalText = fpsIntervalFrames > 0 ? intervalAverage.ToString("F1") : "n/a";

        Debug.Log(
            "Xonix FPS: "
            + $"sample={sampleType}, "
            + $"level={levelNumber}, "
            + $"grid={gridManager.Width}x{gridManager.Height}, "
            + $"balls={activeBallCount}, "
            + $"intervalAvg={intervalText}, "
            + $"sessionAvg={sessionAverage:F1}, "
            + $"sessionSeconds={fpsSessionElapsed:F1}, "
            + $"sessionFrames={fpsSessionFrames}"
        );
    }

    private void ResetGameState(bool resetLives)
    {
        isGameOver = false;
        isLevelComplete = false;
        isCampaignComplete = false;
        isRespawning = false;
        lastDeathFrame = -1;
        if (resetLives)
        {
            lives = startingLives;
        }

        capturedPercentage = territoryManager != null ? territoryManager.CapturedPercentage : 0f;
    }

    private void LoadLevelFiles()
    {
        TextAsset[] allLevelTextAssets = Resources.LoadAll<TextAsset>(levelsResourceFolder);
        levelFiles = Array.FindAll(
            allLevelTextAssets,
            levelFile => levelFile != null && levelFile.name.StartsWith(playableLevelFilePrefix, StringComparison.OrdinalIgnoreCase)
        );
        Array.Sort(levelFiles, CompareLevelAssets);

        if (levelFiles.Length == 0)
        {
            Debug.LogWarning($"No level JSON files found in Resources/{levelsResourceFolder}.");
        }
    }

    private void ApplyCurrentLevelData()
    {
        LevelData currentLevelData = LoadLevelData();
        if (currentLevelData == null)
        {
            return;
        }

        levelNumber = Mathf.Max(1, currentLevelData.levelNumber);
        requiredCapturePercentage = Mathf.Clamp(currentLevelData.requiredCapturePercentage, 1f, 100f);
        gridManager?.ApplyLevelData(currentLevelData);
        FrameCameraToCurrentGrid();

        if (playerController != null && currentLevelData.playerSpawnCell != null)
        {
            playerController.ConfigureSpawn(currentLevelData.playerSpawnCell.ToVector2Int());
        }

        ConfigureBallsFromLevel(currentLevelData);
        territoryManager?.ResetTerritory();
    }

    private LevelData LoadLevelData()
    {
        if (levelFiles == null || levelFiles.Length == 0)
        {
            return null;
        }

        currentLevelIndex = Mathf.Clamp(currentLevelIndex, 0, levelFiles.Length - 1);
        TextAsset source = levelFiles[currentLevelIndex];
        if (source == null)
        {
            Debug.LogWarning($"Level file at index {currentLevelIndex} is missing. Using scene defaults.");
            return null;
        }

        LevelData loadedData = JsonUtility.FromJson<LevelData>(source.text);
        if (loadedData == null)
        {
            Debug.LogWarning($"Could not parse level JSON: {source.name}");
        }

        return loadedData;
    }

    private static int CompareLevelAssets(TextAsset left, TextAsset right)
    {
        int leftNumber = ExtractFirstNumber(left != null ? left.name : string.Empty);
        int rightNumber = ExtractFirstNumber(right != null ? right.name : string.Empty);
        if (leftNumber != rightNumber)
        {
            return leftNumber.CompareTo(rightNumber);
        }

        return string.Compare(left != null ? left.name : string.Empty, right != null ? right.name : string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static int ExtractFirstNumber(string text)
    {
        int value = 0;
        bool hasNumber = false;

        foreach (char character in text)
        {
            if (!char.IsDigit(character))
            {
                if (hasNumber)
                {
                    break;
                }

                continue;
            }

            hasNumber = true;
            value = value * 10 + character - '0';
        }

        return hasNumber ? value : int.MaxValue;
    }

    private void ConfigureBallsFromLevel(LevelData levelData)
    {
        if (levelData.balls == null || levelData.balls.Length == 0)
        {
            return;
        }

        RefreshBallReferencesIfNeeded();
        if ((balls == null || balls.Length == 0) && ballPrefab == null)
        {
            Debug.LogWarning("Level JSON contains balls, but GameManager has no ball prefab or scene ball template.");
            return;
        }

        BallController[] configuredBalls = EnsureBallCount(levelData.balls.Length);
        for (int i = 0; i < configuredBalls.Length; i++)
        {
            bool hasLevelBall = i < levelData.balls.Length;
            configuredBalls[i].gameObject.SetActive(hasLevelBall);
            if (hasLevelBall)
            {
                configuredBalls[i].ConfigureFromLevel(levelData.balls[i]);
            }
        }

        balls = configuredBalls;
    }

    private void FrameCameraToCurrentGrid()
    {
        if (!autoFrameCameraToGrid || gridManager == null)
        {
            return;
        }

        if (gameplayCamera == null)
        {
            gameplayCamera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
        }

        if (gameplayCamera == null)
        {
            return;
        }

        Vector3 arenaCenter = gridManager.GetArenaCenter();
        float maxDimension = gridManager.GetArenaMaxDimension();
        float cameraHeight = Mathf.Max(minCameraHeight, maxDimension * cameraHeightScale);
        float cameraBackOffset = Mathf.Max(6f, maxDimension * cameraBackOffsetScale);

        gameplayCamera.transform.SetPositionAndRotation(
            new Vector3(arenaCenter.x, cameraHeight, arenaCenter.z - cameraBackOffset),
            Quaternion.Euler(60f, 0f, 0f)
        );
    }

    private BallController[] EnsureBallCount(int requestedCount)
    {
        int currentCount = balls != null ? balls.Length : 0;
        if (currentCount >= requestedCount)
        {
            return balls;
        }

        BallController[] expandedBalls = new BallController[requestedCount];
        for (int i = 0; i < currentCount; i++)
        {
            expandedBalls[i] = balls[i];
        }

        BallController template = GetBallTemplate();
        if (template == null)
        {
            return expandedBalls;
        }

        Transform parent = GetBallsParent(template);
        for (int i = currentCount; i < requestedCount; i++)
        {
            BallController newBall = Instantiate(template, parent);
            newBall.name = $"Ball_{i + 1:00}";
            expandedBalls[i] = newBall;
        }

        return expandedBalls;
    }

    private BallController GetBallTemplate()
    {
        if (ballPrefab != null)
        {
            return ballPrefab;
        }

        if (balls != null && balls.Length > 0)
        {
            return balls[0];
        }

        return null;
    }

    private Transform GetBallsParent(BallController template)
    {
        if (ballsRoot != null)
        {
            return ballsRoot;
        }

        if (template != null && template.gameObject.scene.IsValid() && template.transform.parent != null)
        {
            ballsRoot = template.transform.parent;
            return ballsRoot;
        }

        GameObject rootObject = new GameObject("Balls");
        ballsRoot = rootObject.transform;
        return ballsRoot;
    }

    public void StartGame()
    {
        currentLevelIndex = 0;
        ApplyCurrentLevelData();
        RestartLevel();
    }

    public void ReloadCurrentLevelFromJson()
    {
        LoadLevelFiles();
        ApplyCurrentLevelData();
        RestartLevel(true);
        Debug.Log($"Reloaded Level {levelNumber} from JSON.");
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
        RestartLevel(true);
    }

    public void ReturnToMainMenu()
    {
        hasStarted = false;
        currentLevelIndex = 0;
        ApplyCurrentLevelData();
        RefreshBallReferencesIfNeeded();
        StopAllCoroutines();
        territoryManager?.ResetTerritory();
        playerController?.Respawn();

        foreach (BallController ball in balls)
        {
            ball?.ResetBall();
        }

        ResetGameState(true);
        Debug.Log("Returned to main menu");
    }

    public void StartNextLevel()
    {
        if (levelFiles == null || currentLevelIndex >= levelFiles.Length - 1)
        {
            isLevelComplete = false;
            isCampaignComplete = true;
            Debug.Log("Campaign Complete!");
            return;
        }

        currentLevelIndex++;
        ApplyCurrentLevelData();
        RestartLevel(false);
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

    private void RestartLevel(bool resetLives)
    {
        hasStarted = true;
        ResetFpsLogging();
        RefreshBallReferencesIfNeeded();
        StopAllCoroutines();
        territoryManager?.ResetTerritory();
        playerController?.Respawn();

        foreach (BallController ball in balls)
        {
            ball?.ResetBall();
        }

        ResetGameState(resetLives);
        Debug.Log(resetLives ? "Level restarted" : "Level advanced placeholder");
    }

    private int GetActiveBallCount()
    {
        if (balls == null)
        {
            return 0;
        }

        int activeBallCount = 0;
        foreach (BallController ball in balls)
        {
            if (ball != null && ball.isActiveAndEnabled)
            {
                activeBallCount++;
            }
        }

        return activeBallCount;
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

        isLevelComplete = levelFiles != null && currentLevelIndex < levelFiles.Length - 1;
        isCampaignComplete = !isLevelComplete;
        territoryManager?.CancelTemporaryPath();
        Debug.Log(isCampaignComplete ? "You Win!" : "Level Complete!");
    }

    private void RefreshBallReferencesIfNeeded()
    {
        if (balls != null && balls.Length > 0)
        {
            return;
        }

        balls = FindObjectsByType<BallController>(FindObjectsSortMode.None);
        Array.Sort(balls, (left, right) => string.Compare(left.name, right.name, StringComparison.Ordinal));
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
