using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class GameHud : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameManager gameManager;

    [Header("Colors")]
    [SerializeField] private Color panelColor = new Color(0.03f, 0.06f, 0.08f, 0.78f);
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private Color progressBackColor = new Color(0.12f, 0.14f, 0.15f, 0.95f);
    [SerializeField] private Color progressFillColor = new Color(0.25f, 0.82f, 0.38f, 0.95f);
    [SerializeField] private Color lifeOnColor = new Color(0.2f, 0.58f, 1f, 1f);
    [SerializeField] private Color lifeOffColor = new Color(0.16f, 0.18f, 0.2f, 0.9f);
    [SerializeField] private Color messagePanelColor = new Color(0.02f, 0.04f, 0.05f, 0.86f);
    [SerializeField] private Color buttonColor = new Color(0.16f, 0.38f, 0.72f, 0.96f);
    [SerializeField] private Color secondaryButtonColor = new Color(0.14f, 0.18f, 0.2f, 0.96f);
    [SerializeField] private Color buttonTextColor = Color.white;

    private Canvas canvas;
    private Font font;
    private GameObject statusPanel;
    private Text levelText;
    private Text captureText;
    private Image captureFill;
    private Image[] lifePips;
    private GameObject messagePanel;
    private Text messageTitleText;
    private Text messagePromptText;
    private Button startButton;
    private Button retryButton;
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

        RectTransform topPanel = CreatePanel("Status Panel", canvas.transform, panelColor);
        statusPanel = topPanel.gameObject;
        topPanel.anchorMin = new Vector2(0f, 1f);
        topPanel.anchorMax = new Vector2(0f, 1f);
        topPanel.pivot = new Vector2(0f, 1f);
        topPanel.anchoredPosition = new Vector2(24f, -24f);
        topPanel.sizeDelta = new Vector2(440f, 158f);

        levelText = CreateText("Level Text", topPanel, 24, FontStyle.Bold, TextAnchor.MiddleLeft);
        SetRect(levelText.rectTransform, new Vector2(20f, -18f), new Vector2(180f, 34f), new Vector2(0f, 1f));

        RectTransform livesRoot = CreateRect("Lives", topPanel);
        livesRoot.anchorMin = new Vector2(0f, 1f);
        livesRoot.anchorMax = new Vector2(0f, 1f);
        livesRoot.pivot = new Vector2(0f, 1f);
        livesRoot.anchoredPosition = new Vector2(20f, -62f);
        livesRoot.sizeDelta = new Vector2(220f, 28f);
        BuildLifePips(livesRoot);

        captureText = CreateText("Capture Text", topPanel, 20, FontStyle.Bold, TextAnchor.MiddleLeft);
        SetRect(captureText.rectTransform, new Vector2(20f, -96f), new Vector2(300f, 28f), new Vector2(0f, 1f));

        RectTransform progressBack = CreatePanel("Capture Progress Back", topPanel, progressBackColor);
        SetRect(progressBack, new Vector2(20f, -130f), new Vector2(400f, 14f), new Vector2(0f, 1f));

        captureFill = CreateImage("Capture Progress Fill", progressBack, progressFillColor);
        captureFill.rectTransform.anchorMin = new Vector2(0f, 0f);
        captureFill.rectTransform.anchorMax = new Vector2(0f, 1f);
        captureFill.rectTransform.pivot = new Vector2(0f, 0.5f);
        captureFill.rectTransform.anchoredPosition = Vector2.zero;
        captureFill.rectTransform.sizeDelta = Vector2.zero;

        BuildMessagePanel();
        RefreshHud();
    }

    private void BuildLifePips(RectTransform parent)
    {
        int lifeCount = Mathf.Max(1, gameManager != null ? gameManager.StartingLives : 3);
        lifePips = new Image[lifeCount];

        for (int i = 0; i < lifeCount; i++)
        {
            Image pip = CreateImage($"Life_{i + 1}", parent, lifeOnColor);
            pip.rectTransform.anchorMin = new Vector2(0f, 1f);
            pip.rectTransform.anchorMax = new Vector2(0f, 1f);
            pip.rectTransform.pivot = new Vector2(0f, 1f);
            pip.rectTransform.anchoredPosition = new Vector2(i * 34f, 0f);
            pip.rectTransform.sizeDelta = new Vector2(24f, 24f);
            lifePips[i] = pip;
        }
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

        retryButton = CreateButton("Retry Button", "RETRY", panelRect, new Vector2(-138f, -44f), buttonColor);
        retryButton.onClick.AddListener(gameManager.RestartLevel);

        nextLevelButton = CreateButton("Next Level Button", "NEXT LEVEL", panelRect, new Vector2(-138f, -44f), buttonColor);
        nextLevelButton.onClick.AddListener(gameManager.StartNextLevelPlaceholder);

        mainMenuButton = CreateButton("Main Menu Button", "MAIN MENU", panelRect, new Vector2(0f, -112f), secondaryButtonColor);
        mainMenuButton.onClick.AddListener(gameManager.ReturnToMainMenu);

        quitButton = CreateButton("Quit Button", "QUIT", panelRect, new Vector2(138f, -44f), secondaryButtonColor);
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
        captureText.text = $"CAPTURED {capturedRounded}% / {requiredRounded}%";
        captureFill.rectTransform.anchorMax = new Vector2(normalizedCapture, 1f);

        for (int i = 0; i < lifePips.Length; i++)
        {
            lifePips[i].color = i < gameManager.Lives ? lifeOnColor : lifeOffColor;
        }

        bool showMessage = gameManager.IsMainMenuActive || gameManager.IsGameOver || gameManager.IsLevelComplete;
        statusPanel.SetActive(gameManager.HasStarted);
        messagePanel.SetActive(showMessage);
        if (!showMessage)
        {
            return;
        }

        messageTitleText.text = GetMessageTitle();
        messagePromptText.text = GetMessagePrompt();
        startButton.gameObject.SetActive(gameManager.IsMainMenuActive);
        retryButton.gameObject.SetActive(gameManager.IsGameOver);
        nextLevelButton.gameObject.SetActive(gameManager.IsLevelComplete);
        mainMenuButton.gameObject.SetActive(gameManager.IsGameOver || gameManager.IsLevelComplete);
        quitButton.gameObject.SetActive(true);
        quitButton.GetComponent<RectTransform>().anchoredPosition = gameManager.IsMainMenuActive
            ? new Vector2(0f, -112f)
            : new Vector2(138f, -44f);
    }

    private string GetMessageTitle()
    {
        if (gameManager.IsMainMenuActive)
        {
            return "XONIX 3D";
        }

        return gameManager.IsLevelComplete ? "LEVEL COMPLETE" : "GAME OVER";
    }

    private string GetMessagePrompt()
    {
        if (gameManager.IsMainMenuActive)
        {
            return "Press Enter or Start";
        }

        return gameManager.IsLevelComplete ? "Area secured" : "Press R or Retry";
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
