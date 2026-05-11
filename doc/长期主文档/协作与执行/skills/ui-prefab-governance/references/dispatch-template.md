# Dispatch Template

Use this shape for execution dispatch:

```text
必须使用的 skill：
- ui-prefab-governance
- 涉及生成/spec/manifest 时再使用 ui-prefab-pipeline
- 涉及 HotUpdate/UI 业务边界时再使用 unity-hotupdate-boundary

目标：
- ……

允许写入范围：
- ……

禁止写入范围：
- ……
- 未经明确要求，不得改动原 prefab 的颜色、透明度、tint 默认值、材质颜色或 CanvasGroup.alpha。

交付物：
- ……

验收点：
- ……
- UI 运行时代码只通过 UiReferenceCollector / generated bindings / manifest 取节点。
- 原 prefab 颜色和透明度保持不变，除非派工目标明确要求视觉调整。
```
