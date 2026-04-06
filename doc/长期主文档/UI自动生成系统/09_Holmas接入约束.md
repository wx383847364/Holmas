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
- `Assets/UiPrefabGeneratorData`
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
- 竖屏 profile 与输出目录约定

Holmas 不负责：

- 定义生成器核心 schema
- 承担 Editor 生成器本体
- 把生成器与 gameplay 逻辑混写
- 默认把整个生成系统绑进 HotUpdate 规则

## 接入补充规则

- `ProjectUiProfile` 只由 Holmas adapter 实现，不由核心 schema 层实现
- Holmas 当前允许两个 profile：
  - `holmas_ugui`，用于旧横屏样例回归
  - `holmas_ugui_portrait`，用于竖屏小游戏主入口
- `holmas_ugui_portrait` 的输出目录固定为 `Assets/Res/Perfabs/Generated/Holmas/Portrait`
- 只有触碰 Holmas 接入代码时，才需要额外叠加 `unity-hotupdate-boundary`
- `Samples~/Holmas` 只放 Holmas 试点样例，不作为通用共享杂物区
- `Documentation~` 中的 Holmas 说明只记录接入约束，不记录系统本体规则

## 第一版消费边界

Holmas adapter 第一版只做：

- 校验 `PrefabBindingManifest` 是否属于 Holmas 允许目录
- 把 manifest 归一化成 Holmas 侧可读的 generated result plan
- 收集 `requires_manual_wiring` 节点，交给后续业务侧人工接线

Holmas adapter 不做：

- 改写核心 schema
- 自动生成 gameplay 逻辑
- 直接扩展为 HotUpdate 业务 Presenter

## 项目数据补充约束

- `Assets/UiPrefabGeneratorData` 只存模板、任务、缓存和中间结果
- 这里的数据属于项目态文件，不属于 Holmas gameplay 运行时代码
- Holmas 业务侧消费生成结果时，应优先读取最终 prefab 和 manifest，而不是把任务目录当成运行时依赖
