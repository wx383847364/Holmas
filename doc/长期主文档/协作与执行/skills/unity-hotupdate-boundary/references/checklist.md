# Boundary Checklist

## Before Coding

- Which layer owns this change: `App.AOT`, `App.Shared`, or `App.HotUpdate`?
- Does this task really require a new shared DTO or interface?
- Is the resource path supposed to be a YooAssets key?
- For WeChat MiniGame, is this a CDN/`DATA_CDN`/`WechatFileSystem` concern rather than a local file-copy concern?
- Will this feature create mutable runtime state?
- Can the core logic stay pure C# without Unity scene objects?

## Before Parallel Work

- Are DTO names frozen?
- Is ownership of `App.Shared` explicit?
- Is ownership of HotUpdate entry or composition root explicit?
- Is ownership of UI prefabs explicit?

## Before Finishing

- Did any gameplay rule leak into `App.AOT`?
- Did any Unity object logic leak into `App.Shared`?
- Did any reward or generation logic leak into UI scripts?
- Did any runtime mutable state leak into config or template assets?
- Did this feature bypass YooAssets for formal runtime content?
- Did any runtime code accidentally use `UnityEditor`?
- Did any new project-owned name unnecessarily include `meta`?
- If touching WeChat MiniGame resources, is the runtime mode `WebPlayModeParameters + WechatFileSystem`?
- Is `IRemoteServices` non-null when creating the WeChat MiniGame file system?
- Is `DATA_CDN` a real HTTPS CDN root, not empty, localhost, LAN IP, or local HTTP?
- Does the expected version URL resolve as `{CDN_ROOT}/StreamingAssets/yoo/DefaultPackage/DefaultPackage.version`?
- Is `packageRoot` a WeChat cache root under `WX.env.USER_DATA_PATH`, not a CDN URL?
- If AppID changed, was that explicitly requested and reflected through build profile/config instead of hardcoding?
- If C# runtime or `.jslib` changed, was the WeChat MiniGame project re-exported before verification?
- Do logs avoid `game.weixin.qq.com/StreamingAssets/yoo/DefaultPackage/DefaultPackage.version`?

## Project-Specific Reminders

- `MinesweeperTerrainData` is only a map template.
- Terrain color is presentation input, not gameplay authority.
- Task generation depends on player-level config and task slot rules.
- Reward formulas belong in gameplay services.
- Ad unlock timing and offline settlement belong in time, persistence, or progression services.
- Copying `StreamingAssets` into the minigame folder is not a final remote-resource solution.
- WeChat backend must allow the HTTPS CDN domain as a legal download domain for true device/experience validation.
