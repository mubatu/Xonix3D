using UnityEngine;

public sealed class GameManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private TerritoryManager territoryManager;

    [Header("State")]
    [SerializeField] private int startingLives = 3;

    private int lives;
    private bool isGameOver;
    private int lastDeathFrame = -1;

    public int Lives => lives;
    public bool IsGameOver => isGameOver;

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

        lives = startingLives;
    }

    public void HandlePlayerDeath()
    {
        if (isGameOver || lastDeathFrame == Time.frameCount)
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
}
