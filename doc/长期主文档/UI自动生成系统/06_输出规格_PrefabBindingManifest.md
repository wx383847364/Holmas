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
- `notes`

## Notes 字段约定

- `notes` 是当前 v1 sample manifest 的正式字段，不是临时调试输出。
- 它用于补充 `layout`、`layout_slot`、`handler_key`、`binding 推断来源` 等 reviewer 和 adapter 需要的上下文。
- Holmas adapter 当前允许读取 `notes`，但不能用它替代 `node_path / component_type / asset_slot / event_name` 这些正式主字段。
- 如果后续把这些上下文升级成结构化字段，需要先改长期文档，再改 sample fixture 和 consumer。

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
      "requires_manual_wiring": false,
      "notes": "node_id=task_list; component=ScrollRect; layout=VerticalLayout; manual_wiring=false"
    },
    {
      "node_path": "AgencyMainPanel/ClaimButton",
      "component_type": "RectTransform",
      "binding_key": "",
      "asset_slot": "",
      "event_name": "",
      "requires_manual_wiring": false,
      "notes": "node_id=claim_button; component=RectTransform; layout=Anchored; manual_wiring=false"
    },
    {
      "node_path": "AgencyMainPanel/ClaimButton",
      "component_type": "Button",
      "binding_key": "claim_button",
      "asset_slot": "",
      "event_name": "on_click",
      "requires_manual_wiring": true,
      "notes": "node_id=claim_button; component=Button; handler_key=claim_task; manual_wiring=true"
    }
  ]
}
```

## 用途

- 给 UI 实现方看哪里已经自动生成
- 给验证器看输出是否完整
- 给后续接线者看哪些地方还需人工补位
