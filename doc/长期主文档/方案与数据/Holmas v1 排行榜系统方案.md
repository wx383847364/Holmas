# Holmas v1 排行榜系统方案

**Summary**

- 本方案用于为 Holmas 设计首期排行榜系统，覆盖 `等级榜`、`每周找到猫数量榜`、`每日收入榜` 三类榜单。
- 首期按“真实协议 + 本地 Mock”落地：客户端的数据结构、接口边界和提交流程按未来全服榜设计，但当前仓库先使用本地 Mock 网关和静态种子数据跑通完整闭环。
- 榜单范围固定为“全服榜 + 我的名次”，暂不引入好友榜、分区榜、大区切榜和社交关系。
- 排行榜 UI 作为新的独立 Page 接入现有 `UiScreenService`，入口放在 `Main` 页，不复用 `AgencyMain`。

**Goals**

- 给当前主循环增加可持续扩展的排行榜系统边界，避免后续接真服务端时重做 Shared / HotUpdate / UI 接口。
- 让玩家能够在主界面进入榜单页，查看三类榜单的 Top 20、自己的当前名次、当前统计周期和下次重置时间。
- 让排行榜统计严格绑定现有真实玩法事件，而不是从最终余额或历史累计值反推，避免口径污染。
- 保持当前三层边界不变：
  - `App.Shared` 负责榜单 DTO 和协议接口。
  - `App.HotUpdate` 负责统计口径、周期滚动、快照构建和 UI 组装。
  - `App.AOT` 继续只提供通用基础设施，不承载排行榜业务规则。

**Non-Goals**

- 首期不做好友榜。
- 首期不做分区榜、大区榜、城市榜。
- 首期不做服务端事件流上报，只做“玩家快照上报 + 榜单查询”。
- 首期不做独立昵称系统；若当前无正式昵称来源，则回退显示 `PlayerId` 或本地占位名。
- 首期不做离线收益并入收入榜，不做净收入榜，不做历史赛季回放。

**Leaderboard Definitions**

- `等级榜`
  - 主排序值：`PlayerLevel`
  - 第一平分值：`Experience`
  - 统计方式：展示玩家当前长期成长状态，不按日或周重置
  - 展示目标：反映当前长期成长进度
- `每周找到猫数量榜`
  - 主排序值：本周内成功找到的猫总次数
  - 统计方式：只在玩家翻到猫、且 `FoundCat=true` 时累计 `+1`
  - 不统计：空格翻开、插旗、通关次数、不同猫种类数
  - 展示目标：反映本周找猫活跃度
- `每日收入榜`
  - 主排序值：当天通过任务奖励获得的金币总量
  - 统计方式：只在任务奖励结算事件发生时累计
  - 不统计：离线收益、宣传升级消耗、金币净变化、调试加金币
  - 展示目标：反映当天任务推进带来的产出效率

**Time Rules**

- 时间口径固定为北京时间 `Asia/Shanghai`。
- `每日收入榜`
  - 使用自然日统计。
  - 每天 `00:00` 重置。
  - `periodKey` 格式固定为 `yyyyMMdd`，例如 `20260502`。
- `每周找到猫数量榜`
  - 使用自然周统计。
  - 每周一 `00:00` 重置。
  - `periodKey` 格式固定为 `yyyyWww`，其中 `ww` 为当年内的周编号。
- `等级榜`
  - 不做日/周周期重置。
  - 仍保留 `periodKey` 字段，但其值固定为长期榜标识，例如 `alltime`，便于客户端统一消费。
- 周期滚动触发点固定为：
  - 游戏启动完成后
  - 存档恢复后
  - 应用切前台 / 恢复交互时
  - 打开排行榜页前
  - 提交榜单快照前

**Domain Model**

- `App.Shared` 新增排行榜公共模型：
  - `LeaderboardType`
    - `Level`
    - `WeeklyCatsFound`
    - `DailyTaskIncome`
  - `LeaderboardEntry`
    - `playerId`
    - `displayName`
    - `rank`
    - `score`
    - `secondaryScore`
    - `updatedAtUtcMilliseconds`
    - `isSelf`
  - `LeaderboardResponse`
    - `type`
    - `periodKey`
    - `entries`
    - `selfEntry`
    - `nextResetAtUtcMilliseconds`
  - `HolmasLeaderboardSnapshot`
    - 用于一次性提交玩家当前三类榜单状态
    - 包含玩家标识、展示名、时间戳以及三类榜单当前值
  - `IHolmasLeaderboardGateway`
    - `SubmitSnapshotAsync(snapshot)`
    - `GetLeaderboardAsync(type, playerId, topN)`

**Archive Changes**

- 扩展 `HolmasProgressionArchiveData`，补齐排行榜本地统计状态：
  - `DisplayName`
  - `CurrentDailyTaskIncome`
  - `CurrentDailyPeriodKey`
  - `CurrentDailyTaskIncomeUpdatedAtUtcMilliseconds`
  - `CurrentWeeklyCatsFound`
  - `CurrentWeeklyPeriodKey`
  - `CurrentWeeklyCatsFoundUpdatedAtUtcMilliseconds`
  - `CurrentLevelRankUpdatedAtUtcMilliseconds`
- 新增这些字段的原因：
  - 支持断点恢复后继续按正确周期累计
  - 支持本地 Mock 重排时使用统一平分规则
  - 避免从 `GoldBalance`、`CatDiscoveryCounts` 等历史数据逆推当前窗口统计

**Client Architecture**

- `HolmasLeaderboardTrackerService`
  - 归属 `App.HotUpdate`
  - 负责榜单统计、周期滚动、快照生成与提交流程编排
  - 持有 `IHolmasLeaderboardGateway`
  - 订阅 `HolmasGameplayRuntime.StateChanged`
- 统计规则固定如下：
  - `TaskRewardClaimed`
    - 从当前奖励事件读取本次任务奖励金币
    - 先执行日/周 rollover
    - `CurrentDailyTaskIncome += reward`
    - 更新 `CurrentDailyTaskIncomeUpdatedAtUtcMilliseconds`
  - `LevelRevealed`
    - 仅当本次揭示结果 `FoundCat=true`
    - 先执行日/周 rollover
    - `CurrentWeeklyCatsFound += 1`
    - 更新 `CurrentWeeklyCatsFoundUpdatedAtUtcMilliseconds`
  - `PromotionUpgraded`
    - 刷新等级榜时间戳
    - 重新提交长期榜快照
  - 启动恢复完成
    - 立刻执行一次 rollover
    - 生成当前玩家三类榜单快照并提交
- 提交流程固定为：
  - tracker 在本地状态变更后构建完整 `HolmasLeaderboardSnapshot`
  - 通过 `IHolmasLeaderboardGateway.SubmitSnapshotAsync` 提交
  - 不区分“只提某一个榜”，统一用整包快照覆盖，简化首期逻辑
- 查询流程固定为：
  - 打开排行榜页时先触发 rollover
  - 再读取三类榜单数据
  - 默认每类请求 `Top 20`
  - 若本人未进入 Top 20，仍通过 `selfEntry` 单独展示

**Gateway Strategy**

- `IHolmasLeaderboardGateway` 保持真实服务端接口形态：
  - `SubmitSnapshotAsync(snapshot)`：提交玩家当前完整快照
  - `GetLeaderboardAsync(type, playerId, topN)`：查询榜单与本人名次
- 本地 Mock 实现固定行为：
  - 内置三套静态榜单种子数据，每类至少 `100` 条
  - 收到玩家快照后，把当前玩家插入或覆盖进种子集合
  - 按对应榜单排序规则重排
  - 返回 `Top 20 + selfEntry`
- Mock 数据要求：
  - 显示名和分数分布需足够自然，避免 UI 看起来像重复占位数据
  - 三类榜单必须各自独立种子，不能共用完全相同的排序序列
  - 种子中的 `updatedAtUtcMilliseconds` 需覆盖不同时间值，以验证平分规则

**Sorting Rules**

- `等级榜`
  - `PlayerLevel DESC`
  - `Experience DESC`
  - `updatedAtUtcMilliseconds ASC`
  - `playerId ASC`
- `每周找到猫数量榜`
  - `CurrentWeeklyCatsFound DESC`
  - `updatedAtUtcMilliseconds ASC`
  - `playerId ASC`
- `每日收入榜`
  - `CurrentDailyTaskIncome DESC`
  - `updatedAtUtcMilliseconds ASC`
  - `playerId ASC`
- 选择 `updatedAtUtcMilliseconds ASC` 作为后续平分位的目的：
  - 同分时更早达到该分数的玩家排名更高
  - 本地 Mock 与未来服务端可以共享同一条规则

**UI Plan**

- 入口
  - 在 `Main` 页顶部摘要区新增“排行榜”按钮。
  - 点击后进入新的 `LeaderboardPage`。
- 页面结构
  - 顶部标题：`排行榜`
  - Tab：`等级榜` / `周找猫榜` / `日收入榜`
  - 周期说明：展示当前榜单所属周期
  - 重置倒计时：展示距下次重置剩余时间
  - 榜单列表：默认显示 Top 20
  - 我的名次区：固定展示当前玩家名次和分数
- 页面状态覆盖
  - `加载中`
  - `空榜`
  - `请求失败`
  - `我的名次未进前 20`
- UI 交互约束
  - 首期不做分页和无限滚动
  - 首期不做筛选器
  - 首期只在打开页面或切换 tab 时刷新
  - 若查询失败，保留上次成功内容并展示失败状态文案

**Integration Notes**

- `Main` 页新增排行榜入口，但不改变当前找猫主链路。
- `AgencyMain` 不接入榜单入口，避免主流程入口分散。
- 排行榜页依然走现有 `UiScreenService` 的 Page 机制。
- `HolmasFlowCoordinator` 只需要负责从 `Main` 打开 `LeaderboardPage` 和从榜单页返回，不承担榜单业务逻辑。
- 排行榜统计服务应尽量依附于现有 `HolmasApplicationContext` / `GameplayRuntime` 生命周期，避免单独管理额外全局单例。

**Failure Handling**

- 若排行榜提交失败：
  - 不影响本地玩法状态
  - 不回滚当前统计值
  - 下次打开排行榜页或下次关键事件到来时继续提交最新快照
- 若排行榜查询失败：
  - 页面显示失败状态
  - 若存在上次成功结果，可继续显示缓存结果
- 若发生跨日 / 跨周但尚未来得及提交：
  - 本地先执行 rollover 再提交新周期快照
  - 不回写已过期周期的历史榜单，首期不做历史周期榜查询

**Test Plan**

- 冷启动新号
  - 三类榜单都能为当前玩家生成快照
  - 等级榜有本人行
  - 周找猫榜与日收入榜初始分数为 `0`
- 任务自动领奖后
  - `GoldBalance` 增加
  - 仅 `每日收入榜` 分数增加
  - `每周找到猫数量榜` 与 `等级榜` 不受影响
- 离线收益结算后
  - `GoldBalance` 增加
  - `每日收入榜` 不增加
- 翻到猫后
  - 仅 `每周找到猫数量榜` 分数增加
  - 翻空格、插旗、普通揭示不增加周榜分数
- 宣传升级后
  - 等级榜可发生名次变化
  - 周榜、日榜分数不变化
- 跨天
  - 打开游戏或打开榜单页时，`CurrentDailyTaskIncome` 按北京时间重置
  - 周榜分数保持不变
- 跨周
  - 打开游戏或打开榜单页时，`CurrentWeeklyCatsFound` 按北京时间每周一零点重置
  - 日榜按自然日单独处理
- 排行榜展示
  - 正常显示 Top 20
  - 本人不在 Top 20 时仍显示本人名次
  - 空榜、失败、加载中、倒计时都能正确展示

**Implementation Order**

- 第一步：冻结 Shared 层排行榜 DTO 与网关接口
- 第二步：扩展玩家存档结构与 mapper
- 第三步：实现 `HolmasLeaderboardTrackerService`
- 第四步：实现本地 Mock 网关和三类种子数据
- 第五步：接入 `Main` 页按钮与 `LeaderboardPage`
- 第六步：补自动化测试与 UI 验收

**Assumptions**

- 当前没有正式昵称来源时，`DisplayName` 可直接回退为 `PlayerId`。
- `每周找到猫数量榜` 按“找到猫次数”统计，不去重猫种。
- `每日收入榜` 只按任务奖励事件记账，不从 `GoldBalance` 差值反推。
- `等级榜` 虽然不做日/周清零，但仍沿用统一榜单协议结构，方便客户端统一渲染。
- 首期文档不要求同时落地真服务端；当前重点是先把客户端边界、口径和 Mock 行为定死。
