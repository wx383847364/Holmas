# 扫雷场景与预制体搭建说明

## 快速搭建（推荐：步骤 2、3 一键完成）

1. 在 Unity 菜单栏点击 **Minesweeper → 新建并搭建扫雷场景**（会新建场景并自动创建完整 UI 与引用）。
2. 或打开任意场景后点击 **Minesweeper → 搭建扫雷场景**（在当前场景中创建/重建 GameUI 及所有引用）。
3. 确认 `Assets/Minesweeper/Prefabs/Cell.prefab` 存在，保存场景后运行即可玩扫雷。

以下为手动搭建时的参考结构。

---

## 一、场景结构

在 `Scenes` 下新建场景（或复制现有场景），按下面层级搭建 UI。

```
Canvas (Screen Space - Overlay)
├── GameUI (挂 GameUI.cs)
│   ├── TopBar (水平布局)
│   │   ├── MinesRemainingText (Text)      — 剩余雷数，如 "10"
│   │   ├── TimerText (Text)               — 计时，如 "0"
│   │   ├── RestartButton (Button)         — 重新开始
│   │   ├── EasyButton (Button)            — 初级
│   │   ├── MediumButton (Button)          — 中级
│   │   └── HardButton (Button)            — 高级
│   ├── GridParent (空物体，挂 GridLayoutGroup)
│   │   — 这里由 GameUI 动态生成格子，子物体为 Cell 预制体实例
│   └── GameOverPanel (Panel，默认隐藏)
│       └── GameOverMessageText (Text)     — "你赢了！" / "踩雷了！"
```

- **Canvas**：Render Mode 用 Screen Space - Overlay 即可。
- **GridParent**：建议给一个固定宽高（如 400×400），并添加 **Grid Layout Group**：
  - Constraint: Fixed Column Count，后面由代码设置列数。
  - Cell Size、Spacing 可由 GameUI 在运行时设置（也可在编辑器先设一个默认值）。
- 将 **GameUI** 挂到包含 TopBar、GridParent、GameOverPanel 的根物体上，并把各引用拖到 GameUI 组件里。

## 二、格子预制体 (Cell.prefab)

在 `Prefabs` 下新建空物体，改名为 Cell。**CellView 支持两种层级**，任选其一即可。

**结构 A（子节点平铺在根下）：**
```
Cell (Image + CellView.cs)
├── NumberText (Text)
├── FlagImage (Image)
├── MineImage (Image)
└── WrongFlagImage (Image)
```

**结构 B（图标挂在 NumberText 下）：**
```
Cell (Image + CellView.cs)
└── NumberText (Text)
    ├── FlagImage (Image)
    ├── MineImage (Image)
    └── WrongFlagImage (Image)
```

未在 Inspector 中拖引用时，代码会按名称查找（`NumberText`、`FlagImage`、`MineImage`、`WrongFlagImage`），结构 B 会从 `NumberText/xxx` 解析。

**最小可用结构**（无贴图时）：

- 根物体：添加 **Image**（作为背景）+ **CellView** 脚本。
- 子物体：一个 **Text**（可放在中心），命名为 NumberText。
- 在 CellView 中：
  - 不填 FlagImage、MineImage、WrongFlagImage 时，插旗/雷/错旗用根节点的 Image 颜色或文字表示（当前实现里会隐藏/显示这些节点，没有则只更新背景和数字）。
  - 若要显示旗/雷/错旗，在预制体下加对应 Image 子物体并拖到 CellView 的对应引用。

**注意**：

- 根或背景 Image 的 **Raycast Target** 必须勾选，否则格子收不到点击。
- 预制体保存后，在 GameUI 的 **Cell Prefab** 引用中拖入该预制体。
- **Grid Parent** 引用拖入上面的 GridParent（带 GridLayoutGroup 的物体）。

## 三、GameUI 组件引用对照

| GameUI 字段 | 拖入对象 |
|-------------|----------|
| Cell Prefab | Cell 预制体 |
| Grid Parent | GridParent (RectTransform) |
| Grid Layout | GridParent 上的 GridLayoutGroup（可选，有则自动排布格子大小） |
| Mines Remaining Text | 剩余雷数 Text |
| Timer Text | 计时 Text |
| Restart Button | 重新开始按钮 |
| Difficulty Easy/Medium/Hard Button | 初级/中级/高级按钮 |
| Game Over Panel | 结束面板 |
| Game Over Message Text | 结束语 Text |

未用到的引用可留空（如不显示计时就不拖 Timer Text）。

## 四、运行

运行场景后，点击格子为左键翻开，右键插旗；第一次点击的格子一定不是雷。胜利/失败后会弹出 GameOverPanel，点“重新开始”或难度按钮可再开一局。
