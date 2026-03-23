using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 扫雷游戏 UI 控制器：难度选择、雷数/计时显示、重新开始、动态生成格子网格。
/// </summary>
public class GameUI : MonoBehaviour
{
    [Header("难度（0=初级 1=中级 2=高级）")]
    [SerializeField] private int currentDifficultyIndex = 0;

    [Header("引用")]
    [SerializeField] private GameObject cellPrefab;
    [SerializeField] private RectTransform gridParent;
    [SerializeField] private GridLayoutGroup gridLayout;
    [SerializeField] private Text minesRemainingText;
    [SerializeField] private Text timerText;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button difficultyEasyButton;
    [SerializeField] private Button difficultyMediumButton;
    [SerializeField] private Button difficultyHardButton;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private Text gameOverMessageText;
    [SerializeField] private Button gameOverRestartButton;
    [Header("开始选难度")]
    [SerializeField] private GameObject difficultySelectPanel;
    [SerializeField] private GameObject gameContent;
    [SerializeField] private Button startDifficultyEasy;
    [SerializeField] private Button startDifficultyMedium;
    [SerializeField] private Button startDifficultyHard;
    [Header("自定义地形")]
    [SerializeField] private MinesweeperTerrainData customTerrainAsset;
    [SerializeField] private Button startTerrainButton;

    private MinesweeperTerrainData _currentTerrain;

    private MinesweeperGame _game;
    private List<CellView> _cells = new List<CellView>();
    private float _timer;
    private bool _timerRunning;

    private const float MaxCellSize = 32f;
    private const float MinCellSize = 12f;
    private const float GridSpacing = 1f;
    private const float TopBarReserve = 62f;
    private const float ContentPadding = 24f;

    private void Start()
    {
        _game = new MinesweeperGame();
        _game.OnMinesPlaced += StartTimer;
        _game.OnGameOver += OnGameOver;
        _game.OnFlagCountChanged += UpdateMinesRemaining;

        if (restartButton != null) restartButton.onClick.AddListener(Restart);
        if (difficultyEasyButton != null) difficultyEasyButton.onClick.AddListener(() => SetDifficultyAndRestart(0));
        if (difficultyMediumButton != null) difficultyMediumButton.onClick.AddListener(() => SetDifficultyAndRestart(1));
        if (difficultyHardButton != null) difficultyHardButton.onClick.AddListener(() => SetDifficultyAndRestart(2));
        if (gameOverRestartButton != null) gameOverRestartButton.onClick.AddListener(Restart);
        if (startDifficultyEasy != null) startDifficultyEasy.onClick.AddListener(() => OnDifficultySelected(0));
        if (startDifficultyMedium != null) startDifficultyMedium.onClick.AddListener(() => OnDifficultySelected(1));
        if (startDifficultyHard != null) startDifficultyHard.onClick.AddListener(() => OnDifficultySelected(2));
        if (startTerrainButton != null) startTerrainButton.onClick.AddListener(OnTerrainSelected);

        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (difficultySelectPanel != null) difficultySelectPanel.SetActive(true);
        if (gameContent != null) gameContent.SetActive(false);
        if (difficultySelectPanel == null || !difficultySelectPanel.activeSelf)
            Restart();
    }

    private void OnDestroy()
    {
        if (_game != null)
        {
            _game.OnMinesPlaced -= StartTimer;
            _game.OnGameOver -= OnGameOver;
            _game.OnFlagCountChanged -= UpdateMinesRemaining;
        }
    }

    private void Update()
    {
        if (_timerRunning)
        {
            _timer += Time.deltaTime;
            if (timerText != null) timerText.text = Mathf.FloorToInt(_timer).ToString();
        }
    }

    public void SetDifficultyAndRestart(int index)
    {
        currentDifficultyIndex = Mathf.Clamp(index, 0, MinesweeperGame.Difficulties.Length - 1);
        Restart();
    }

    private void OnDifficultySelected(int index)
    {
        if (difficultySelectPanel != null) difficultySelectPanel.SetActive(false);
        if (gameContent != null) gameContent.SetActive(true);
        SetDifficultyAndRestart(index);
    }

    /// <summary>失败后返回选择难度界面。</summary>
    public void BackToDifficultySelect()
    {
        _timerRunning = false;
        _currentTerrain = null;
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (gameContent != null) gameContent.SetActive(false);
        if (difficultySelectPanel != null) difficultySelectPanel.SetActive(true);
    }

    private void OnTerrainSelected()
    {
        if (customTerrainAsset == null) return;
        _currentTerrain = customTerrainAsset;
        if (difficultySelectPanel != null) difficultySelectPanel.SetActive(false);
        if (gameContent != null) gameContent.SetActive(true);
        Restart();
    }

    private static int TerrainMineCount(MinesweeperTerrainData terrain)
    {
        if (terrain == null) return 10;
        int valid = terrain.GetValidCellCount();
        return Mathf.Max(1, (int)(valid * 0.15f));
    }

    public void Restart()
    {
        _timerRunning = false;
        _timer = 0f;
        if (gameOverPanel != null) gameOverPanel.SetActive(false);

        if (_currentTerrain != null)
            _game.InitFromTerrain(_currentTerrain, TerrainMineCount(_currentTerrain));
        else
            _game.Init(currentDifficultyIndex);
        BuildGrid();
        UpdateMinesRemaining();
        if (timerText != null) timerText.text = "0";
    }

    private void BuildGrid()
    {
        foreach (var c in _cells)
        {
            if (c != null && c.gameObject != null)
                Destroy(c.gameObject);
        }
        _cells.Clear();

        if (cellPrefab == null || gridParent == null)
            return;

        int rows = _game.Rows;
        int cols = _game.Cols;
        int count = rows * cols;

        float availableWidth = 400f;
        float availableHeight = 400f;
        if (gameContent != null)
        {
            var contentRect = gameContent.GetComponent<RectTransform>();
            if (contentRect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
                availableWidth = contentRect.rect.width - ContentPadding;
                availableHeight = contentRect.rect.height - TopBarReserve - ContentPadding;
            }
        }
        availableWidth = Mathf.Max(availableWidth, 80f);
        availableHeight = Mathf.Max(availableHeight, 80f);

        float cellSize = MaxCellSize;
        if (gridLayout != null)
        {
            gridLayout.constraintCount = cols;
            float w = (availableWidth - (cols - 1) * GridSpacing) / cols;
            float h = (availableHeight - (rows - 1) * GridSpacing) / rows;
            cellSize = Mathf.Min(w, h);
            cellSize = Mathf.Clamp(cellSize, MinCellSize, MaxCellSize);
            gridLayout.cellSize = new Vector2(cellSize, cellSize);
            gridLayout.spacing = new Vector2(GridSpacing, GridSpacing);
        }

        for (int i = 0; i < count; i++)
        {
            int r = i / cols;
            int c = i % cols;
            var go = Instantiate(cellPrefab, gridParent);
            go.name = $"Cell_{r}_{c}";
            var cellView = go.GetComponent<CellView>();
            if (cellView != null)
            {
                cellView.Bind(_game, r, c);
                _cells.Add(cellView);
            }
        }
    }

    private void StartTimer()
    {
        _timerRunning = true;
    }

    private void UpdateMinesRemaining()
    {
        if (minesRemainingText != null)
            minesRemainingText.text = _game.RemainingMines.ToString();
    }

    private void OnGameOver(bool won)
    {
        _timerRunning = false;
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            gameOverPanel.transform.SetAsLastSibling();
            if (gameOverMessageText != null)
                gameOverMessageText.text = won ? "你赢了！" : "踩雷了！";
            if (gameOverRestartButton != null)
            {
                gameOverRestartButton.onClick.RemoveAllListeners();
                if (won)
                {
                    gameOverRestartButton.onClick.AddListener(Restart);
                    var btnText = gameOverRestartButton.GetComponentInChildren<Text>();
                    if (btnText != null) btnText.text = "再玩一局";
                }
                else
                {
                    gameOverRestartButton.onClick.AddListener(BackToDifficultySelect);
                    var btnText = gameOverRestartButton.GetComponentInChildren<Text>();
                    if (btnText != null) btnText.text = "选择难度";
                }
            }
        }
    }
}
