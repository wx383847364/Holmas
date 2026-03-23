using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// 单个格子视图：显示状态，处理左键翻开、右键插旗。
/// 支持两种预制体结构：
/// 1) 根下直接：NumberText、FlagImage、MineImage、WrongFlagImage
/// 2) 根下仅 NumberText，其子节点为 FlagImage、MineImage、WrongFlagImage
/// </summary>
[RequireComponent(typeof(Image))]
public class CellView : MonoBehaviour, IPointerClickHandler
{
    [Header("显示节点（可空则按名称查找）")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Text numberText;
    [SerializeField] private Image flagImage;
    [SerializeField] private Image mineImage;
    [SerializeField] private Image wrongFlagImage;

    [Header("颜色（无贴图时使用）")]
    [SerializeField] private Color coveredColor = new Color(0.75f, 0.75f, 0.75f);
    [SerializeField] private Color revealedColor = Color.white;
    [SerializeField] private Color mineColor = Color.red;
    [SerializeField] private Color wrongFlagColor = new Color(1f, 0.3f, 0.3f);
    [SerializeField] private Color[] numberColors = new Color[0]; // 1~8 可选配色，空则用默认

    private MinesweeperGame _game;
    private int _row;
    private int _col;

    private Image _bg;
    private Text _numText;
    private Image _flagImg;
    private Image _mineImg;
    private Image _wrongImg;

    private void Awake()
    {
        _bg = backgroundImage != null ? backgroundImage : GetComponent<Image>();
        if (_bg == null) _bg = gameObject.AddComponent<Image>();

        _numText = numberText;
        if (_numText == null)
        {
            var numberGo = transform.Find("NumberText");
            if (numberGo != null) _numText = numberGo.GetComponent<Text>();
            if (_numText == null) _numText = GetComponentInChildren<Text>();
        }

        _flagImg = flagImage;
        if (_flagImg == null) _flagImg = ResolveImage("FlagImage");

        _mineImg = mineImage;
        if (_mineImg == null) _mineImg = ResolveImage("MineImage");

        _wrongImg = wrongFlagImage;
        if (_wrongImg == null) _wrongImg = ResolveImage("WrongFlagImage");

        StretchChildrenToFillCell();
    }

    /// <summary>让 NumberText 与各 Image 随格子尺寸拉伸填满，避免换关卡后子节点尺寸不变。</summary>
    private void StretchChildrenToFillCell()
    {
        var cellRect = GetComponent<RectTransform>();
        if (cellRect == null) return;

        if (_numText != null)
        {
            StretchRect(_numText.rectTransform);
            _numText.alignment = TextAnchor.MiddleCenter;
            _numText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _numText.verticalOverflow = VerticalWrapMode.Overflow;
            float cellW = cellRect.rect.width;
            int fontSize = Mathf.Max(8, Mathf.RoundToInt(cellW * 0.65f));
            _numText.fontSize = fontSize;
        }
        if (_flagImg != null)
            StretchRect(_flagImg.rectTransform);
        if (_mineImg != null)
            StretchRect(_mineImg.rectTransform);
        if (_wrongImg != null)
            StretchRect(_wrongImg.rectTransform);
    }

    private static void StretchRect(RectTransform rect)
    {
        if (rect == null) return;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private Image ResolveImage(string name)
    {
        var t = transform.Find(name);
        if (t == null) t = transform.Find("NumberText/" + name);
        return t != null ? t.GetComponent<Image>() : null;
    }

    public void Bind(MinesweeperGame game, int row, int col)
    {
        _game = game;
        _row = row;
        _col = col;
        if (_game != null)
        {
            _game.OnCellChanged += OnCellChanged;
            _game.OnGameOver += OnGameOver;
        }
        Refresh();
    }

    private void OnDestroy()
    {
        if (_game != null)
        {
            _game.OnCellChanged -= OnCellChanged;
            _game.OnGameOver -= OnGameOver;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_game == null) return;
        var state = _game.GetCellState(_row, _col);
        if (!state.IsValid) return; // 无效格不响应点击
        if (eventData.button == PointerEventData.InputButton.Left)
            _game.Reveal(_row, _col);
        else if (eventData.button == PointerEventData.InputButton.Right)
            _game.ToggleFlag(_row, _col);
    }

    private void OnCellChanged(int row, int col)
    {
        if (row == _row && col == _col)
            Refresh();
    }

    private void OnGameOver(bool won)
    {
        Refresh();
    }

    public void Refresh()
    {
        if (_game == null) return;
        var state = _game.GetCellState(_row, _col);

        if (_bg != null)
        {
            if (!state.IsValid)
                _bg.color = new Color(0.35f, 0.35f, 0.35f, 0.9f); // 无效格固定灰色
            else if (state.IsRevealed)
                _bg.color = revealedColor; // 翻开后白色底
            else
                _bg.color = new Color(state.BlockColor.r / 255f, state.BlockColor.g / 255f, state.BlockColor.b / 255f, 1f);
        }
        var image = GetComponent<UnityEngine.UI.Image>();
        if (image != null) image.raycastTarget = state.IsValid;

        if (_numText != null)
        {
            _numText.gameObject.SetActive(state.IsValid && state.IsRevealed && !state.IsMine);
            if (state.IsRevealed && !state.IsMine)
            {
                _numText.text = state.AdjacentCount > 0 ? state.AdjacentCount.ToString() : "";
                _numText.color = GetNumberColor(state.AdjacentCount);
                var cellRect = GetComponent<RectTransform>();
                if (cellRect != null && cellRect.rect.width > 0)
                    _numText.fontSize = Mathf.Max(8, Mathf.RoundToInt(cellRect.rect.width * 0.65f));
            }
        }

        if (_flagImg != null)
            _flagImg.gameObject.SetActive(state.IsValid && state.IsFlagged && !state.IsWrongFlag);

        if (_mineImg != null)
        {
            _mineImg.gameObject.SetActive(state.IsValid && state.IsRevealed && state.IsMine);
            _mineImg.color = mineColor;
        }

        if (_wrongImg != null)
        {
            _wrongImg.gameObject.SetActive(state.IsValid && state.IsWrongFlag);
            _wrongImg.color = wrongFlagColor;
        }
    }

    private Color GetNumberColor(int adjacent)
    {
        if (adjacent <= 0 || adjacent > 8) return Color.black;
        if (numberColors != null && numberColors.Length >= adjacent && numberColors[adjacent - 1] != default)
            return numberColors[adjacent - 1];
        // 默认配色（类似 Windows 扫雷）
        switch (adjacent)
        {
            case 1: return new Color(0.1f, 0.1f, 0.9f);   // 蓝
            case 2: return new Color(0f, 0.5f, 0f);        // 绿
            case 3: return new Color(0.9f, 0f, 0f);        // 红
            case 4: return new Color(0f, 0f, 0.5f);        // 深蓝
            case 5: return new Color(0.5f, 0f, 0f);        // 棕红
            case 6: return new Color(0f, 0.5f, 0.5f);      // 青
            case 7: return Color.black;
            case 8: return Color.gray;
            default: return Color.black;
        }
    }
}
