# 输入规格 DesignPacket

## 目标

`DesignPacket` 是设计输入的统一打包格式。  
第一版要求所有设计输入先被整理成 `DesignPacket`，再交给 Spec Agent。

## 必填字段

- `page_id`
- `page_title`
- `prefab_name`
- `design_images`
- `states`

## 推荐字段

- `rules`
- `asset_slot_hints`
- `notes`

## 建议结构

```json
{
  "page_id": "agency_main",
  "page_title": "Agency Main",
  "prefab_name": "AgencyMainPanel",
  "design_images": [
    {
      "image_id": "default",
      "image_path": "Design/AgencyMain/default.png",
      "state_id": "default"
    }
  ],
  "states": [
    {
      "state_id": "default",
      "description": "默认态"
    }
  ],
  "rules": [
    {
      "rule_id": "task_list_scrollable",
      "description": "任务列表需要可滚动"
    },
    {
      "rule_id": "claim_button_clickable",
      "description": "领奖按钮需要暴露点击事件"
    }
  ],
  "asset_slot_hints": [
    {
      "slot_id": "task_icon",
      "usage": "任务图标"
    }
  ],
  "notes": "图片仅作结构参考，文字走运行时绑定。"
}
```

## 输入要求

- 图片必须可定位到页面和状态
- 标注包必须能说明节点语义、交互和资源位
- 规则文本必须是可执行约束，而不是泛泛描述
- 不能把业务逻辑说明混成布局标注

## Intake 审阅层

第一版在 `DesignPacket` 正式进入 spec 生成前，允许先产出 intake 审阅结果：

- 记录缺失字段和未决项
- 记录哪些问题必须人工确认
- 记录哪些问题会阻塞进入 `UiPrefabSpec`

冻结约束：

- intake 只做 review-only assessment，不直接产正式 `UiPrefabSpec`
- generator 和 validator 继续只消费 `UiPrefabSpec`
- `rules`、`asset_slot_hints`、图片状态映射不清时，应以未决项而不是猜测结构的方式暴露

当前最小解释器实现口径：

- 先运行 intake analyzer
- 存在 blocking issue 时，禁止直接进入 spec 解释
- 无 blocking issue 时，允许产出最小 `UiPrefabSpec`
- 当前默认生成 1 个 `root` 节点
- 当前自动支持的最小规则集：
  - `RectTransform`
  - 来自首个 `asset_slot_hint` 的 `Image`
  - `task_list_scrollable -> task_list / ScrollRect`
  - `claim_button_clickable -> claim_button / Button / on_click`
- 尚未接入自动解释的其他规则继续留在 intake 未决项中，不在 spec 层猜测生成
