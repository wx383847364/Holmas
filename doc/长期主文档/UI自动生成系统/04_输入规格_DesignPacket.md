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
      "state": "default"
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
