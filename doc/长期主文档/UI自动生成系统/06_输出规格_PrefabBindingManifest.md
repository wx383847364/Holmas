# 输出规格 PrefabBindingManifest

## 目标

`PrefabBindingManifest` 描述 prefab 草稿中哪些节点、组件、资源位和事件位已经产出，以及哪些地方仍需要人工接线。

## 必填字段

- `prefab_name`
- `prefab_draft_path`
- `entries`

## Entry 字段

每一项至少包含：

- `node_path`
- `component_type`
- `binding_key`
- `asset_slot`
- `event_name`
- `requires_manual_wiring`

## 建议结构

```json
{
  "prefab_name": "AgencyMainPanel",
  "prefab_draft_path": "Assets/Res/Perfabs/Generated/Holmas/AgencyMainPanel.prefab",
  "entries": [
    {
      "node_path": "AgencyMainPanel/TaskList",
      "component_type": "ScrollRect",
      "binding_key": "task_list",
      "asset_slot": "",
      "event_name": "",
      "requires_manual_wiring": false
    },
    {
      "node_path": "AgencyMainPanel/ClaimButton",
      "component_type": "Button",
      "binding_key": "claim_button",
      "asset_slot": "",
      "event_name": "on_click",
      "requires_manual_wiring": true
    }
  ]
}
```

## 用途

- 给 UI 实现方看哪里已经自动生成
- 给验证器看输出是否完整
- 给后续接线者看哪些地方还需人工补位
