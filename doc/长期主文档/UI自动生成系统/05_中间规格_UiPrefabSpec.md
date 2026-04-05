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
      "components": ["RectTransform", "Image"],
      "layout": "FullScreen"
    },
    {
      "node_id": "task_list",
      "node_name": "TaskList",
      "parent_node_id": "root",
      "components": ["RectTransform", "ScrollRect", "VerticalLayoutGroup"],
      "layout": "LeftColumn"
    }
  ],
  "bindings": [
    {
      "node_id": "task_list",
      "binding_key": "task_list"
    }
  ],
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
