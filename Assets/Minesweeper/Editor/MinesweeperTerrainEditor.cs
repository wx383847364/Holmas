using UnityEngine;
using UnityEditor;

/// <summary>
/// 扫雷地形编辑器：编辑行列数、有效地块、每格颜色。
/// 菜单：Minesweeper -> 地形编辑器
/// </summary>
public class MinesweeperTerrainEditor : EditorWindow
{
    private MinesweeperTerrainData _terrain;
    private int _editRows = 9;
    private int _editCols = 9;
    private Color _paintColor = new Color(0.7f, 0.7f, 0.7f, 1f);
    private const float Padding = 2f;
    private const float MinCellSize = 4f;
    private const float MaxCellSize = 48f;

    private Texture2D _importTexture;
    private bool _useImageSize = true;
    private bool _transparentAsInvalid = true;
    private float _alphaThreshold = 0.5f;

    [MenuItem("Minesweeper/地形编辑器")]
    public static void Open()
    {
        var w = GetWindow<MinesweeperTerrainEditor>("扫雷地形");
        w.minSize = new Vector2(360, 320);
    }

    private void OnEnable()
    {
        if (_terrain != null)
        {
            _editRows = _terrain.Rows;
            _editCols = _terrain.Cols;
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(4);
        DrawToolbar();
        EditorGUILayout.Space(4);
        DrawObjectField();
        DrawImportFromImage();
        DrawGrid();
    }

    private void DrawObjectField()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("地形数据");
        var newTerrain = (MinesweeperTerrainData)EditorGUILayout.ObjectField(_terrain, typeof(MinesweeperTerrainData), false);
        if (newTerrain != _terrain)
        {
            _terrain = newTerrain;
            if (_terrain != null) { _editRows = _terrain.Rows; _editCols = _terrain.Cols; }
            Repaint();
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("行", GUILayout.Width(16));
        _editRows = Mathf.Clamp(EditorGUILayout.IntField(_editRows, GUILayout.Width(40)), 1, 64);
        EditorGUILayout.LabelField("列", GUILayout.Width(16));
        _editCols = Mathf.Clamp(EditorGUILayout.IntField(_editCols, GUILayout.Width(40)), 1, 64);
        if (GUILayout.Button("应用", GUILayout.Width(44)))
            ApplyResize();
        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField("绘制颜色", GUILayout.Width(56));
        _paintColor = EditorGUILayout.ColorField(_paintColor, GUILayout.Width(60));
        EditorGUILayout.Space(8);
        if (GUILayout.Button("新建", GUILayout.Width(40)))
            NewTerrain();
        if (GUILayout.Button("保存", GUILayout.Width(40)))
            SaveTerrain();
        if (GUILayout.Button("加载", GUILayout.Width(40)))
            LoadTerrain();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.HelpBox("左键点击格子：设为当前颜色。右键点击格子：切换有效/无效（有效=可玩，无效=不参与）。", MessageType.None);
    }

    private void DrawImportFromImage()
    {
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("从图片生成地形", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("图片");
        _importTexture = (Texture2D)EditorGUILayout.ObjectField(_importTexture, typeof(Texture2D), false);
        EditorGUILayout.EndHorizontal();
        _useImageSize = EditorGUILayout.Toggle("使用图片尺寸（行=高 列=宽）", _useImageSize);
        if (!_useImageSize)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("生成行列");
            _editRows = Mathf.Clamp(EditorGUILayout.IntField(_editRows, GUILayout.Width(40)), 1, 64);
            EditorGUILayout.LabelField("×", GUILayout.Width(12));
            _editCols = Mathf.Clamp(EditorGUILayout.IntField(_editCols, GUILayout.Width(40)), 1, 64);
            EditorGUILayout.EndHorizontal();
        }
        _transparentAsInvalid = EditorGUILayout.Toggle("透明为无效格", _transparentAsInvalid);
        if (_transparentAsInvalid)
            _alphaThreshold = EditorGUILayout.Slider("透明度阈值", _alphaThreshold, 0f, 1f);
        if (GUILayout.Button("从图片生成地形", GUILayout.Height(24)))
            GenerateFromImage();
    }

    private void GenerateFromImage()
    {
        if (_importTexture == null)
        {
            EditorUtility.DisplayDialog("提示", "请先指定一张图片。", "确定");
            return;
        }
        int w = _importTexture.width;
        int h = _importTexture.height;
        if (w <= 0 || h <= 0)
        {
            EditorUtility.DisplayDialog("提示", "图片尺寸无效。", "确定");
            return;
        }
        int rows = _useImageSize ? h : _editRows;
        int cols = _useImageSize ? w : _editCols;
        rows = Mathf.Clamp(rows, 1, 64);
        cols = Mathf.Clamp(cols, 1, 64);

        Texture2D readable = GetReadableTexture(_importTexture);
        if (readable == null)
        {
            EditorUtility.DisplayDialog("无法读取", "请勾选该纹理的 Read/Write 后重试（选中图片 → Inspector → Read/Write Enable）。", "确定");
            return;
        }

        if (_terrain == null)
            _terrain = CreateInstance<MinesweeperTerrainData>();
        Undo.RecordObject(_terrain, "Terrain From Image");
        _terrain.Resize(rows, cols);
        _editRows = rows;
        _editCols = cols;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                float u = cols <= 1 ? 0.5f : (c + 0.5f) / cols;
                float v = rows <= 1 ? 0.5f : (rows - 1 - r + 0.5f) / rows;
                int px = Mathf.Clamp((int)(u * readable.width), 0, readable.width - 1);
                int py = Mathf.Clamp((int)(v * readable.height), 0, readable.height - 1);
                Color pixel = readable.GetPixel(px, py);
                bool valid = !_transparentAsInvalid || pixel.a >= _alphaThreshold;
                _terrain.SetValid(r, c, valid);
                _terrain.SetColor(r, c, new Color32((byte)(pixel.r * 255), (byte)(pixel.g * 255), (byte)(pixel.b * 255), 255));
            }
        }
        if (readable != _importTexture)
            DestroyImmediate(readable);
        EditorUtility.SetDirty(_terrain);
        Repaint();
    }

    private static Texture2D GetReadableTexture(Texture2D source)
    {
        if (source == null) return null;
        if (source.isReadable) return source;
        RenderTexture rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
        RenderTexture.active = rt;
        Graphics.Blit(source, rt);
        Texture2D copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
        copy.Apply();
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);
        return copy;
    }

    private void ApplyResize()
    {
        if (_terrain == null)
        {
            _terrain = CreateInstance<MinesweeperTerrainData>();
            _terrain.Resize(_editRows, _editCols);
            return;
        }
        Undo.RecordObject(_terrain, "Terrain Resize");
        _terrain.Resize(_editRows, _editCols);
        EditorUtility.SetDirty(_terrain);
        Repaint();
    }

    private void NewTerrain()
    {
        _terrain = CreateInstance<MinesweeperTerrainData>();
        _terrain.Resize(_editRows, _editCols);
        _editRows = _terrain.Rows;
        _editCols = _terrain.Cols;
        Repaint();
    }

    private void SaveTerrain()
    {
        if (_terrain == null) return;
        string path = AssetDatabase.GetAssetPath(_terrain);
        if (!string.IsNullOrEmpty(path))
        {
            EditorUtility.SetDirty(_terrain);
            AssetDatabase.SaveAssets();
            Repaint();
            return;
        }
        path = EditorUtility.SaveFilePanelInProject("保存地形", "Terrain", "asset", "选择保存位置");
        if (string.IsNullOrEmpty(path)) return;
        AssetDatabase.CreateAsset(_terrain, path);
        Repaint();
    }

    private void LoadTerrain()
    {
        string path = EditorUtility.OpenFilePanel("加载地形", Application.dataPath, "asset");
        if (string.IsNullOrEmpty(path)) return;
        if (!path.StartsWith(Application.dataPath))
            path = path.Replace("\\", "/");
        else
            path = "Assets" + path.Substring(Application.dataPath.Length).Replace("\\", "/");
        var loaded = AssetDatabase.LoadAssetAtPath<MinesweeperTerrainData>(path);
        if (loaded != null)
        {
            _terrain = loaded;
            _editRows = _terrain.Rows;
            _editCols = _terrain.Cols;
        }
        Repaint();
    }

    private void DrawGrid()
    {
        if (_terrain == null)
        {
            EditorGUILayout.HelpBox("请点击「新建」或「加载」创建/打开地形数据。", MessageType.Info);
            return;
        }
        int rows = _terrain.Rows;
        int cols = _terrain.Cols;
        if (rows <= 0 || cols <= 0) return;

        Rect gridArea = GUILayoutUtility.GetRect(64, 64, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        float availableW = gridArea.width - Padding * 2;
        float availableH = gridArea.height - Padding * 2;
        if (availableW <= 0 || availableH <= 0) return;

        float cellW = (availableW - (cols - 1) * Padding) / cols;
        float cellH = (availableH - (rows - 1) * Padding) / rows;
        float cellSize = Mathf.Min(cellW, cellH);
        cellSize = Mathf.Clamp(cellSize, MinCellSize, MaxCellSize);

        float totalW = cols * cellSize + (cols - 1) * Padding + Padding * 2;
        float totalH = rows * cellSize + (rows - 1) * Padding + Padding * 2;
        float offsetX = gridArea.x + (gridArea.width - totalW) * 0.5f;
        float offsetY = gridArea.y + (gridArea.height - totalH) * 0.5f;

        Event e = Event.current;
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                float x = offsetX + Padding + c * (cellSize + Padding);
                float y = offsetY + Padding + r * (cellSize + Padding);
                Rect cellRect = new Rect(x, y, cellSize, cellSize);
                var cell = _terrain.GetCell(r, c);
                Color drawColor = cell.isValid ? (Color)cell.blockColor : new Color(0.4f, 0.4f, 0.4f, 0.8f);
                EditorGUI.DrawRect(cellRect, drawColor);
                if (cell.isValid && cellSize > 4)
                    EditorGUI.DrawRect(new Rect(cellRect.x + 1, cellRect.y + 1, cellRect.width - 2, cellRect.height - 2), new Color(1, 1, 1, 0.15f));
                if (e.type == EventType.MouseDown && e.button < 2 && cellRect.Contains(e.mousePosition))
                {
                    if (e.button == 0)
                    {
                        Undo.RecordObject(_terrain, "Terrain Paint");
                        _terrain.SetColor(r, c, _paintColor);
                        EditorUtility.SetDirty(_terrain);
                    }
                    else
                    {
                        Undo.RecordObject(_terrain, "Terrain Toggle Valid");
                        _terrain.SetValid(r, c, !cell.isValid);
                        EditorUtility.SetDirty(_terrain);
                    }
                    e.Use();
                    Repaint();
                }
            }
        }
    }
}
