# 中间规格 UiPrefabSpec

## 目标

`UiPrefabSpec` 是第一版的唯一机器权威中间层。  
生成器、校验器和回归比较都只依赖它，不直接依赖设计图。

## 必填字段

- `page_id`
- `prefab_name`
- `root_node_id`
- `nodes`

## 推荐字段

- `bindings`
- `interactions`
- `generation_profile_id`

## 节点定义

每个节点至少描述：

- `node_id`
- `node_name`
- `parent_node_id`
- `components`
- `layout`

## 建议结构

```json
{
  "page_id": "agency_main",
  "prefab_name": "AgencyMainPanel",
  "root_node_id": "root",
  "generation_profile_id": "holmas_ugui",
  "nodes": [
    {
      "node_id": "root",
      "node_name": "AgencyMainPanel",
      "parent_node_id": "",
      "components": [
        {
          "component_type": "RectTransform",
          "binding_key": "",
          "asset_slot": ""
        },
        {
          "component_type": "Image",
          "binding_key": "",
          "asset_slot": "panel_bg"
        }
      ],
      "layout": {
        "layout_type": "FullScreen",
        "layout_slot": "root"
      }
    },
    {
      "node_id": "task_list",
      "node_name": "TaskList",
      "parent_node_id": "root",
      "components": [
        {
          "component_type": "RectTransform",
          "binding_key": "",
          "asset_slot": ""
        },
        {
          "component_type": "ScrollRect",
          "binding_key": "task_list",
          "asset_slot": ""
        }
      ],
      "layout": {
        "layout_type": "VerticalLayout",
        "layout_slot": "task_list"
      }
    },
    {
      "node_id": "claim_button",
      "node_name": "ClaimButton",
      "parent_node_id": "root",
      "components": [
        {
          "component_type": "RectTransform",
          "binding_key": "",
          "asset_slot": ""
        },
        {
          "component_type": "Button",
          "binding_key": "claim_button",
          "asset_slot": ""
        }
      ],
      "layout": {
        "layout_type": "Anchored",
        "layout_slot": "claim_button"
      }
    }
  ],
  "bindings": [],
  "interactions": [
    {
      "node_id": "claim_button",
      "event_name": "on_click",
      "handler_key": "claim_task"
    }
  ]
}
```

## 冻结规则

- `UiPrefabSpec` 不承载玩法规则
- `UiPrefabSpec` 只表达结构、组件、资源位、绑定位和交互出口
- 所有生成与校验都必须可从 spec 重建
- `DesignPacket` 的 intake 结果只能作为前置审阅材料，不能绕开 spec 直接进入 generator 或 validator
- 当前 `DesignPacket -> UiPrefabSpec` 最小解释器固定产出 `root`，并对 `task_list_scrollable` 追加 `task_list` / `ScrollRect` 子节点、对 `claim_button_clickable` 追加 `claim_button` 节点与 `on_click` 交互出口；未自动解释的其他规则必须继续留在 intake 未决项中
