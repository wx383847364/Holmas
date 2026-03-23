using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// 一键搭建扫雷场景：创建 Canvas、TopBar、GridParent、GameOverPanel，挂 GameUI 并绑定所有引用。
/// 菜单：Minesweeper -> 搭建扫雷场景
/// </summary>
public static class MinesweeperSceneBuilder
{
    private const string CellPrefabPath = "Assets/Minesweeper/Prefabs/Cell.prefab";

    [MenuItem("Minesweeper/搭建扫雷场景")]
    public static void BuildScene()
    {
        BuildSceneInternal();
    }

    [MenuItem("Minesweeper/新建并搭建扫雷场景")]
    public static void NewSceneAndBuild()
    {
        if (EditorSceneManager.GetActiveScene().isDirty &&
            !EditorUtility.DisplayDialog("未保存", "当前场景有未保存修改，是否继续？", "继续", "取消"))
            return;
        EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.DefaultGameObjects);
        BuildSceneInternal();
    }

    private static void BuildSceneInternal()
    {
        // 确保有 Canvas 和 EventSystem
        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            var canvasGo = new GameObject("Canvas");
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.AddComponent<GraphicRaycaster>();
            var es = Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>();
            if (es == null)
            {
                var esGo = new GameObject("EventSystem");
                esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }
        }

        Transform canvasRoot = canvas.transform;

        // 若已存在 GameUI 根则删除重建（避免重复）
        Transform existing = canvasRoot.Find("GameUI");
        if (existing != null)
        {
            Object.DestroyImmediate(existing.gameObject);
        }

        GameObject gameUIRoot = new GameObject("GameUI");
        gameUIRoot.transform.SetParent(canvasRoot, false);
        RectTransform gameUIRect = gameUIRoot.AddComponent<RectTransform>();
        gameUIRect.anchorMin = Vector2.zero;
        gameUIRect.anchorMax = Vector2.one;
        gameUIRect.offsetMin = Vector2.zero;
        gameUIRect.offsetMax = Vector2.zero;

        // --- GameContent（选完难度后显示：TopBar + GridParent，垂直布局居中）---
        GameObject gameContent = new GameObject("GameContent");
        gameContent.transform.SetParent(gameUIRoot.transform, false);
        RectTransform gameContentRect = gameContent.AddComponent<RectTransform>();
        gameContentRect.anchorMin = Vector2.zero;
        gameContentRect.anchorMax = Vector2.one;
        gameContentRect.offsetMin = Vector2.zero;
        gameContentRect.offsetMax = Vector2.zero;
        var gameContentLayout = gameContent.AddComponent<VerticalLayoutGroup>();
        gameContentLayout.spacing = 8;
        gameContentLayout.childAlignment = TextAnchor.UpperCenter;
        gameContentLayout.childControlWidth = true;
        gameContentLayout.childControlHeight = true;
        gameContentLayout.childForceExpandWidth = false;
        gameContentLayout.childForceExpandHeight = false;
        gameContentLayout.padding = new RectOffset(12, 12, 12, 12);

        // --- TopBar ---
        GameObject topBar = new GameObject("TopBar");
        topBar.transform.SetParent(gameContent.transform, false);
        RectTransform topBarRect = topBar.AddComponent<RectTransform>();
        var topBarLE = topBar.AddComponent<LayoutElement>();
        topBarLE.preferredHeight = 52;
        topBarLE.minHeight = 48;
        topBarLE.flexibleWidth = 1;
        var topBarLayout = topBar.AddComponent<HorizontalLayoutGroup>();
        topBarLayout.spacing = 10;
        topBarLayout.childAlignment = TextAnchor.MiddleCenter;
        topBarLayout.childControlWidth = true;
        topBarLayout.childControlHeight = true;
        topBarLayout.childForceExpandWidth = false;
        topBarLayout.childForceExpandHeight = true;

        Text minesRemainingText = CreateText(topBar.transform, "MinesRemainingText", "10", 24);
        Text timerText = CreateText(topBar.transform, "TimerText", "0", 24);
        Button restartButton = CreateButton(topBar.transform, "重新开始", 100);
        Button easyButton = CreateButton(topBar.transform, "初级", 64);
        Button mediumButton = CreateButton(topBar.transform, "中级", 64);
        Button hardButton = CreateButton(topBar.transform, "高级", 64);

        // --- GridParent（由 ContentSizeFitter 随格子数量自适应，布局居中）---
        GameObject gridParent = new GameObject("GridParent");
        gridParent.transform.SetParent(gameContent.transform, false);
        RectTransform gridParentRect = gridParent.AddComponent<RectTransform>();
        var gridParentLE = gridParent.AddComponent<LayoutElement>();
        gridParentLE.flexibleWidth = 0;
        gridParentLE.flexibleHeight = 0;
        var gridContentFitter = gridParent.AddComponent<ContentSizeFitter>();
        gridContentFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        gridContentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var gridLayout = gridParent.AddComponent<GridLayoutGroup>();
        gridLayout.cellSize = new Vector2(28, 28);
        gridLayout.spacing = new Vector2(1, 1);
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = 9;
        gridLayout.childAlignment = TextAnchor.MiddleCenter;

        // --- GameOverPanel ---
        GameObject gameOverPanel = new GameObject("GameOverPanel");
        gameOverPanel.transform.SetParent(gameUIRoot.transform, false);
        gameOverPanel.SetActive(false);
        RectTransform panelRect = gameOverPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(300, 120);
        Image panelBg = gameOverPanel.AddComponent<Image>();
        panelBg.color = new Color(0.2f, 0.2f, 0.2f, 0.95f);

        GameObject msgGo = new GameObject("GameOverMessageText");
        msgGo.transform.SetParent(gameOverPanel.transform, false);
        RectTransform msgRect = msgGo.AddComponent<RectTransform>();
        msgRect.anchorMin = new Vector2(0.5f, 0.55f);
        msgRect.anchorMax = new Vector2(0.5f, 0.55f);
        msgRect.anchoredPosition = Vector2.zero;
        msgRect.sizeDelta = new Vector2(280, 50);
        Text gameOverMessageText = msgGo.AddComponent<Text>();
        gameOverMessageText.text = "你赢了！";
        gameOverMessageText.fontSize = 28;
        gameOverMessageText.alignment = TextAnchor.MiddleCenter;
        gameOverMessageText.color = Color.white;

        Button gameOverRestartButton = CreateButton(gameOverPanel.transform, "再玩一局");
        var goRestartRect = gameOverRestartButton.GetComponent<RectTransform>();
        goRestartRect.anchorMin = new Vector2(0.5f, 0.2f);
        goRestartRect.anchorMax = new Vector2(0.5f, 0.2f);
        goRestartRect.pivot = new Vector2(0.5f, 0.5f);
        goRestartRect.anchoredPosition = Vector2.zero;
        goRestartRect.sizeDelta = new Vector2(120, 36);

        // --- 开始选难度面板 ---
        GameObject difficultySelectPanel = new GameObject("DifficultySelectPanel");
        difficultySelectPanel.transform.SetParent(gameUIRoot.transform, false);
        RectTransform diffPanelRect = difficultySelectPanel.AddComponent<RectTransform>();
        diffPanelRect.anchorMin = Vector2.zero;
        diffPanelRect.anchorMax = Vector2.one;
        diffPanelRect.offsetMin = Vector2.zero;
        diffPanelRect.offsetMax = Vector2.zero;
        Image diffPanelBg = difficultySelectPanel.AddComponent<Image>();
        diffPanelBg.color = new Color(0.15f, 0.2f, 0.25f, 0.98f);

        GameObject diffTitleGo = new GameObject("Title");
        diffTitleGo.transform.SetParent(difficultySelectPanel.transform, false);
        RectTransform diffTitleRect = diffTitleGo.AddComponent<RectTransform>();
        diffTitleRect.anchorMin = new Vector2(0.5f, 0.7f);
        diffTitleRect.anchorMax = new Vector2(0.5f, 0.7f);
        diffTitleRect.pivot = new Vector2(0.5f, 0.5f);
        diffTitleRect.anchoredPosition = Vector2.zero;
        diffTitleRect.sizeDelta = new Vector2(300, 50);
        Text diffTitleText = diffTitleGo.AddComponent<Text>();
        diffTitleText.text = "选择难度";
        diffTitleText.fontSize = 32;
        diffTitleText.alignment = TextAnchor.MiddleCenter;
        diffTitleText.color = Color.white;

        Button startDifficultyEasy = CreateButton(difficultySelectPanel.transform, "初级 9×9 10雷");
        Button startDifficultyMedium = CreateButton(difficultySelectPanel.transform, "中级 16×16 40雷");
        Button startDifficultyHard = CreateButton(difficultySelectPanel.transform, "高级 30×30 99雷");
        Button startTerrainButton = CreateButton(difficultySelectPanel.transform, "自定义地形");
        var rectEasy = startDifficultyEasy.GetComponent<RectTransform>();
        var rectMed = startDifficultyMedium.GetComponent<RectTransform>();
        var rectHard = startDifficultyHard.GetComponent<RectTransform>();
        var rectTerrain = startTerrainButton.GetComponent<RectTransform>();
        rectEasy.anchorMin = new Vector2(0.5f, 0.5f);
        rectEasy.anchorMax = new Vector2(0.5f, 0.5f);
        rectEasy.anchoredPosition = new Vector2(0, 65);
        rectEasy.sizeDelta = new Vector2(180, 40);
        rectMed.anchorMin = new Vector2(0.5f, 0.5f);
        rectMed.anchorMax = new Vector2(0.5f, 0.5f);
        rectMed.anchoredPosition = new Vector2(0, 15);
        rectMed.sizeDelta = new Vector2(180, 40);
        rectHard.anchorMin = new Vector2(0.5f, 0.5f);
        rectHard.anchorMax = new Vector2(0.5f, 0.5f);
        rectHard.anchoredPosition = new Vector2(0, -35);
        rectHard.sizeDelta = new Vector2(180, 40);
        rectTerrain.anchorMin = new Vector2(0.5f, 0.5f);
        rectTerrain.anchorMax = new Vector2(0.5f, 0.5f);
        rectTerrain.anchoredPosition = new Vector2(0, -85);
        rectTerrain.sizeDelta = new Vector2(180, 40);

        // --- 挂 GameUI 并绑定引用 ---
        GameUI gameUI = gameUIRoot.AddComponent<GameUI>();
        SerializedObject so = new SerializedObject(gameUI);
        so.FindProperty("cellPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(CellPrefabPath);
        so.FindProperty("gridParent").objectReferenceValue = gridParentRect;
        so.FindProperty("gridLayout").objectReferenceValue = gridLayout;
        so.FindProperty("minesRemainingText").objectReferenceValue = minesRemainingText;
        so.FindProperty("timerText").objectReferenceValue = timerText;
        so.FindProperty("restartButton").objectReferenceValue = restartButton;
        so.FindProperty("difficultyEasyButton").objectReferenceValue = easyButton;
        so.FindProperty("difficultyMediumButton").objectReferenceValue = mediumButton;
        so.FindProperty("difficultyHardButton").objectReferenceValue = hardButton;
        so.FindProperty("gameOverPanel").objectReferenceValue = gameOverPanel;
        so.FindProperty("gameOverMessageText").objectReferenceValue = gameOverMessageText;
        so.FindProperty("gameOverRestartButton").objectReferenceValue = gameOverRestartButton;
        so.FindProperty("difficultySelectPanel").objectReferenceValue = difficultySelectPanel;
        so.FindProperty("gameContent").objectReferenceValue = gameContent;
        so.FindProperty("startDifficultyEasy").objectReferenceValue = startDifficultyEasy;
        so.FindProperty("startDifficultyMedium").objectReferenceValue = startDifficultyMedium;
        so.FindProperty("startDifficultyHard").objectReferenceValue = startDifficultyHard;
        so.FindProperty("startTerrainButton").objectReferenceValue = startTerrainButton;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(gameUIRoot);
        EditorSceneManager.MarkSceneDirty(gameUIRoot.scene);
        Debug.Log("[Minesweeper] 场景已搭建完成。若 Cell 预制体路径不是 " + CellPrefabPath + "，请在 GameUI 上手动指定 Cell Prefab。");
    }

    private static Text CreateText(Transform parent, string name, string content, int fontSize)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(60, 36);
        var text = go.AddComponent<Text>();
        text.text = content;
        text.fontSize = fontSize;
        text.alignment = TextAnchor.MiddleCenter;
        return text;
    }

    private static Button CreateButton(Transform parent, string label, float preferredWidth = 80f)
    {
        var go = new GameObject(label);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(preferredWidth, 38);
        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = preferredWidth;
        le.minWidth = Mathf.Min(56f, preferredWidth * 0.7f);
        le.preferredHeight = 38;
        le.minHeight = 32;
        var image = go.AddComponent<Image>();
        image.color = new Color(0.3f, 0.5f, 0.8f);
        var button = go.AddComponent<Button>();
        var textGo = new GameObject("Text");
        textGo.transform.SetParent(go.transform, false);
        var textRect = textGo.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(2, 2);
        textRect.offsetMax = new Vector2(-2, -2);
        var text = textGo.AddComponent<Text>();
        text.text = label;
        text.fontSize = 16;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return button;
    }
}
