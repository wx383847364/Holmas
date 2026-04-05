# Holmas 接入约束

## Holmas 的角色

Holmas 是这套系统的首个试点项目，但不是系统本体目录。  
系统本体固定放在 `Assets/Tools/UiPrefabGenerator`，Holmas 只通过 adapter 和 profile 接入。

## 禁止写入

- `Assets/HotUpdateContent/Script/App.HotUpdate/Holmas/UI`
- `Assets/Scripts/App.Shared`
- 现有 Holmas gameplay 目录

## 允许写入

- `Assets/Tools/UiPrefabGenerator/Runtime/HolmasAdapter`
- Holmas 业务侧对 binding manifest 的消费代码
- Holmas 自己的 UI 资产目录
- `Assets/Tools/UiPrefabGenerator/Samples~/Holmas` 中的 Holmas 接入样例

## 现有 Holmas UI 过渡态

当前仓库已经存在 `Assets/HotUpdateContent/Script/App.HotUpdate/Holmas/UI` 的过渡态实现。  
这批代码仍属于 Holmas 业务试点，不应被继续扩展成生成器系统本体。

## 接入方式

Holmas 第一版只提供：

- `ProjectUiProfile`
- 生成结果目录约定
- 业务侧如何消费 binding manifest 的接线约束

Holmas 不负责：

- 定义生成器核心 schema
- 承担 Editor 生成器本体
- 把生成器与 gameplay 逻辑混写
- 默认把整个生成系统绑进 HotUpdate 规则

## 接入补充规则

- `ProjectUiProfile` 只由 Holmas adapter 实现，不由核心 schema 层实现
- 只有触碰 Holmas 接入代码时，才需要额外叠加 `unity-hotupdate-boundary`
- `Samples~/Holmas` 只放 Holmas 试点样例，不作为通用共享杂物区
- `Documentation~` 中的 Holmas 说明只记录接入约束，不记录系统本体规则
