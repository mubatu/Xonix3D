using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class GameHud : MonoBehaviour
{
    private const float LivesPanelMinWidth = 250f;
    private const float LivesPanelHeight = 58f;
    private const float LivesLabelWidth = 86f;
    private const float LivesRootX = 112f;
    private const float LivesRightPadding = 20f;
    private const float LifeHeartSize = 28f;
    private const float LifeHeartSpacing = 34f;

    [Header("References")]
    [SerializeField] private GameManager gameManager;

    [Header("Colors")]
    [SerializeField] private Color panelColor = new Color(0.03f, 0.06f, 0.08f, 0.78f);
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private Color progressBackColor = new Color(0.12f, 0.14f, 0.15f, 0.95f);
    [SerializeField] private Color progressFillColor = new Color(0.25f, 0.82f, 0.38f, 0.95f);
    [SerializeField] private Color lifeOnColor = new Color(0.95f, 0.08f, 0.12f, 1f);
    [SerializeField] private Color lifeOffColor = new Color(0.28f, 0.08f, 0.1f, 0.9f);
    [SerializeField] private Color messagePanelColor = new Color(0.02f, 0.04f, 0.05f, 0.86f);
    [SerializeField] private Color buttonColor = new Color(0.16f, 0.38f, 0.72f, 0.96f);
    [SerializeField] private Color secondaryButtonColor = new Color(0.14f, 0.18f, 0.2f, 0.96f);
    [SerializeField] private Color buttonTextColor = Color.white;

    private Canvas canvas;
    private Font font;
    private GameObject statusPanel;
    private Text levelText;
    private Text timerText;
    private Text captureText;
    private Image captureFill;
    private Image[] lifeHearts;
    private Sprite heartSprite;
    private GameObject messagePanel;
    private Text messageTitleText;
    private Text messagePromptText;
    private Button startButton;
    private Button nextLevelButton;
    private Button mainMenuButton;
    private Button quitButton;

    private void Awake()
    {
        if (gameManager == null)
        {
            gameManager = FindFirstObjectByType<GameManager>();
        }

        font = LoadDefaultFont();
        BuildHud();
    }

    private void Update()
    {
        RefreshHud();
    }

    private void BuildHud()
    {
        canvas = CreateCanvas();
        EnsureEventSystemExists();

        RectTransform statusRoot = CreateRect("Status Root", canvas.transform);
        statusPanel = statusRoot.gameObject;
        statusRoot.anchorMin = Vector2.zero;
        statusRoot.anchorMax = Vector2.one;
        statusRoot.offsetMin = Vector2.zero;
        statusRoot.offsetMax = Vector2.zero;

        RectTransform levelPanel = CreatePanel("Level Panel", statusRoot, panelColor);
        levelPanel.anchorMin = new Vector2(0f, 1f);
        levelPanel.anchorMax = new Vector2(0f, 1f);
        levelPanel.pivot = new Vector2(0f, 1f);
        levelPanel.anchoredPosition = new Vector2(24f, -24f);
        levelPanel.sizeDelta = new Vector2(440f, 154f);

        levelText = CreateText("Level Text", levelPanel, 24, FontStyle.Bold, TextAnchor.MiddleLeft);
        SetRect(levelText.rectTransform, new Vector2(20f, -20f), new Vector2(150f, 34f), new Vector2(0f, 1f));

        timerText = CreateText("Timer Text", levelPanel, 24, FontStyle.Bold, TextAnchor.MiddleRight);
        SetRect(timerText.rectTransform, new Vector2(270f, -20f), new Vector2(150f, 34f), new Vector2(0f, 1f));

        captureText = CreateText("Capture Text", levelPanel, 20, FontStyle.Bold, TextAnchor.MiddleLeft);
        SetRect(captureText.rectTransform, new Vector2(20f, -72f), new Vector2(330f, 28f), new Vector2(0f, 1f));

        RectTransform progressBack = CreatePanel("Capture Progress Back", levelPanel, progressBackColor);
        SetRect(progressBack, new Vector2(20f, -106f), new Vector2(400f, 14f), new Vector2(0f, 1f));

        RectTransform livesPanel = CreatePanel("Lives Panel", statusRoot, panelColor);
        livesPanel.anchorMin = new Vector2(1f, 1f);
        livesPanel.anchorMax = new Vector2(1f, 1f);
        livesPanel.pivot = new Vector2(1f, 1f);
        livesPanel.anchoredPosition = new Vector2(-24f, -24f);
        livesPanel.sizeDelta = new Vector2(GetLivesPanelWidth(), LivesPanelHeight);

        Text livesLabel = CreateText("Lives Label", livesPanel, 20, FontStyle.Bold, TextAnchor.MiddleLeft);
        SetRect(livesLabel.rectTransform, new Vector2(20f, -20f), new Vector2(LivesLabelWidth, 28f), new Vector2(0f, 1f));
        livesLabel.text = "LIVES";

        RectTransform livesRoot = CreateRect("Lives", livesPanel);
        livesRoot.anchorMin = new Vector2(0f, 1f);
        livesRoot.anchorMax = new Vector2(0f, 1f);
        livesRoot.pivot = new Vector2(0f, 1f);
        livesRoot.anchoredPosition = new Vector2(LivesRootX, -22f);
        livesRoot.sizeDelta = new Vector2(GetLivesRootWidth(), LifeHeartSize);
        BuildLifeHearts(livesRoot);

        captureFill = CreateImage("Capture Progress Fill", progressBack, progressFillColor);
        captureFill.rectTransform.anchorMin = new Vector2(0f, 0f);
        captureFill.rectTransform.anchorMax = new Vector2(0f, 1f);
        captureFill.rectTransform.pivot = new Vector2(0f, 0.5f);
        captureFill.rectTransform.anchoredPosition = Vector2.zero;
        captureFill.rectTransform.sizeDelta = Vector2.zero;

        BuildMessagePanel();
        RefreshHud();
    }

    private void BuildLifeHearts(RectTransform parent)
    {
        int lifeCount = Mathf.Max(1, gameManager != null ? gameManager.StartingLives : 3);
        lifeHearts = new Image[lifeCount];
        heartSprite ??= CreateHeartSprite();

        for (int i = 0; i < lifeCount; i++)
        {
            Image heart = CreateImage($"Life_{i + 1}", parent, lifeOnColor);
            heart.sprite = heartSprite;
            heart.type = Image.Type.Simple;
            heart.preserveAspect = true;
            heart.color = lifeOnColor;
            heart.rectTransform.anchorMin = new Vector2(0f, 1f);
            heart.rectTransform.anchorMax = new Vector2(0f, 1f);
            heart.rectTransform.pivot = new Vector2(0f, 1f);
            heart.rectTransform.anchoredPosition = new Vector2(i * LifeHeartSpacing, 0f);
            heart.rectTransform.sizeDelta = new Vector2(LifeHeartSize, LifeHeartSize);
            lifeHearts[i] = heart;
        }
    }

    private float GetLivesPanelWidth()
    {
        return Mathf.Max(LivesPanelMinWidth, LivesRootX + GetLivesRootWidth() + LivesRightPadding);
    }

    private float GetLivesRootWidth()
    {
        int lifeCount = Mathf.Max(1, gameManager != null ? gameManager.StartingLives : 3);
        return LifeHeartSize + (lifeCount - 1) * LifeHeartSpacing;
    }

    private void BuildMessagePanel()
    {
        messagePanel = CreatePanel("State Panel", canvas.transform, messagePanelColor).gameObject;
        RectTransform panelRect = messagePanel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(600f, 330f);

        messageTitleText = CreateText("State Title", panelRect, 46, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(messageTitleText.rectTransform, new Vector2(0f, 102f), new Vector2(540f, 70f), new Vector2(0.5f, 0.5f));

        messagePromptText = CreateText("State Prompt", panelRect, 24, FontStyle.Normal, TextAnchor.MiddleCenter);
        SetRect(messagePromptText.rectTransform, new Vector2(0f, 34f), new Vector2(520f, 50f), new Vector2(0.5f, 0.5f));

        startButton = CreateButton("Start Button", "START", panelRect, new Vector2(0f, -44f), buttonColor);
        startButton.onClick.AddListener(gameManager.StartGame);

        nextLevelButton = CreateButton("Next Level Button", "NEXT LEVEL", panelRect, new Vector2(0f, -44f), buttonColor);
        nextLevelButton.onClick.AddListener(gameManager.StartNextLevel);

        mainMenuButton = CreateButton("Main Menu Button", "MAIN MENU", panelRect, new Vector2(0f, -112f), secondaryButtonColor);
        mainMenuButton.onClick.AddListener(gameManager.ReturnToMainMenu);

        quitButton = CreateButton("Quit Button", "QUIT", panelRect, new Vector2(0f, -112f), secondaryButtonColor);
        quitButton.onClick.AddListener(gameManager.QuitGame);
    }

    private void RefreshHud()
    {
        if (gameManager == null)
        {
            return;
        }

        int capturedRounded = Mathf.RoundToInt(gameManager.CapturedPercentage);
        int requiredRounded = Mathf.RoundToInt(gameManager.RequiredCapturePercentage);
        float required = Mathf.Max(1f, gameManager.RequiredCapturePercentage);
        float normalizedCapture = Mathf.Clamp01(gameManager.CapturedPercentage / required);

        levelText.text = $"LEVEL {gameManager.LevelNumber}";
        timerText.text = $"TIME {FormatTimer(gameManager.RemainingTimerSeconds)}";
        captureText.text = $"CAPTURED {capturedRounded}% / {requiredRounded}%";
        captureFill.rectTransform.anchorMax = new Vector2(normalizedCapture, 1f);

        for (int i = 0; i < lifeHearts.Length; i++)
        {
            lifeHearts[i].color = i < gameManager.Lives ? lifeOnColor : lifeOffColor;
        }

        bool showMessage = gameManager.IsMainMenuActive
            || gameManager.IsGameOver
            || gameManager.IsLevelComplete
            || gameManager.IsCampaignComplete;
        statusPanel.SetActive(gameManager.HasStarted);
        messagePanel.SetActive(showMessage);
        if (!showMessage)
        {
            return;
        }

        messageTitleText.text = GetMessageTitle();
        messagePromptText.text = GetMessagePrompt();
        startButton.gameObject.SetActive(gameManager.IsMainMenuActive);
        nextLevelButton.gameObject.SetActive(gameManager.IsLevelComplete);
        mainMenuButton.gameObject.SetActive(gameManager.IsGameOver || gameManager.IsLevelComplete || gameManager.IsCampaignComplete);
        quitButton.gameObject.SetActive(gameManager.IsMainMenuActive);
        mainMenuButton.GetComponent<RectTransform>().anchoredPosition = gameManager.IsLevelComplete
            ? new Vector2(0f, -112f)
            : new Vector2(0f, -44f);
    }

    private string GetMessageTitle()
    {
        if (gameManager.IsMainMenuActive)
        {
            return "XONIX 3D";
        }

        if (gameManager.IsCampaignComplete)
        {
            return "YOU WIN";
        }

        return gameManager.IsLevelComplete ? "LEVEL COMPLETE" : "GAME OVER";
    }

    private string GetMessagePrompt()
    {
        if (gameManager.IsMainMenuActive)
        {
            return "Press Enter or Start";
        }

        if (gameManager.IsCampaignComplete)
        {
            return "All levels complete";
        }

        return gameManager.IsLevelComplete ? "Press Enter or Next Level" : gameManager.GameOverPrompt;
    }

    private static string FormatTimer(float seconds)
    {
        int totalSeconds = Mathf.CeilToInt(Mathf.Max(0f, seconds));
        int minutes = totalSeconds / 60;
        int remainingSeconds = totalSeconds % 60;
        return $"{minutes:00}:{remainingSeconds:00}";
    }

    private Canvas CreateCanvas()
    {
        GameObject canvasObject = new GameObject("HUD Canvas", typeof(RectTransform));
        canvasObject.transform.SetParent(transform, false);

        Canvas newCanvas = canvasObject.AddComponent<Canvas>();
        newCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        newCanvas.sortingOrder = 50;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();
        return newCanvas;
    }

    private RectTransform CreatePanel(string objectName, Transform parent, Color color)
    {
        Image image = CreateImage(objectName, parent, color);
        return image.rectTransform;
    }

    private Image CreateImage(string objectName, Transform parent, Color color)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(parent, false);

        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private Text CreateText(string objectName, Transform parent, int fontSize, FontStyle fontStyle, TextAnchor anchor)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.transform.SetParent(parent, false);

        Text text = textObject.GetComponent<Text>();
        text.color = textColor;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = anchor;
        text.raycastTarget = false;

        if (font != null)
        {
            text.font = font;
        }

        return text;
    }

    private Button CreateButton(string objectName, string label, Transform parent, Vector2 anchoredPosition, Color color)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rectTransform = buttonObject.GetComponent<RectTransform>();
        SetRect(rectTransform, anchoredPosition, new Vector2(230f, 50f), new Vector2(0.5f, 0.5f));

        Image image = buttonObject.GetComponent<Image>();
        image.color = color;

        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = Brighten(color, 1.22f);
        colors.pressedColor = Brighten(color, 0.78f);
        colors.selectedColor = Brighten(color, 1.12f);
        colors.disabledColor = new Color(color.r, color.g, color.b, 0.45f);
        button.colors = colors;

        Text buttonText = CreateText($"{objectName} Text", rectTransform, 22, FontStyle.Bold, TextAnchor.MiddleCenter);
        buttonText.text = label;
        buttonText.color = buttonTextColor;
        SetRect(buttonText.rectTransform, Vector2.zero, rectTransform.sizeDelta, new Vector2(0.5f, 0.5f));
        return button;
    }

    private RectTransform CreateRect(string objectName, Transform parent)
    {
        GameObject rectObject = new GameObject(objectName, typeof(RectTransform));
        rectObject.transform.SetParent(parent, false);
        return rectObject.GetComponent<RectTransform>();
    }

    private static Sprite CreateHeartSprite()
    {
        string[] pattern =
        {
            "01100110",
            "11111111",
            "11111111",
            "11111111",
            "01111110",
            "00111100",
            "00011000",
            "00000000"
        };

        const int scale = 8;
        int textureWidth = pattern[0].Length * scale;
        int textureHeight = pattern.Length * scale;
        Texture2D texture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false)
        {
            name = "Generated Heart Sprite",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        Color[] pixels = new Color[textureWidth * textureHeight];
        for (int patternY = 0; patternY < pattern.Length; patternY++)
        {
            for (int patternX = 0; patternX < pattern[patternY].Length; patternX++)
            {
                Color pixelColor = pattern[patternY][patternX] == '1' ? Color.white : Color.clear;
                for (int offsetY = 0; offsetY < scale; offsetY++)
                {
                    for (int offsetX = 0; offsetX < scale; offsetX++)
                    {
                        int x = patternX * scale + offsetX;
                        int y = textureHeight - 1 - (patternY * scale + offsetY);
                        pixels[y * textureWidth + x] = pixelColor;
                    }
                }
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        return Sprite.Create(
            texture,
            new Rect(0f, 0f, textureWidth, textureHeight),
            new Vector2(0.5f, 0.5f),
            textureWidth
        );
    }

    private static void SetRect(RectTransform rectTransform, Vector2 anchoredPosition, Vector2 size, Vector2 centerAnchor)
    {
        rectTransform.anchorMin = centerAnchor;
        rectTransform.anchorMax = centerAnchor;
        rectTransform.pivot = centerAnchor;
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = size;
    }

    private static Font LoadDefaultFont()
    {
        Font loadedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (loadedFont != null)
        {
            return loadedFont;
        }

        loadedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (loadedFont != null)
        {
            return loadedFont;
        }

        return Font.CreateDynamicFontFromOSFont(new[] { "Arial", "Helvetica", "Verdana" }, 18);
    }

    private static void EnsureEventSystemExists()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
        {
            return;
        }

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private static Color Brighten(Color color, float multiplier)
    {
        return new Color(
            Mathf.Clamp01(color.r * multiplier),
            Mathf.Clamp01(color.g * multiplier),
            Mathf.Clamp01(color.b * multiplier),
            color.a
        );
    }
}
