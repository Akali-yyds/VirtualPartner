# FinishedRepairedModel 使用说明

这个目录用于存放已经由 ModelRepairTool 修复并整理过的角色资源。每个角色/皮肤对应一个独立子目录，例如：

- `Asuna`
- `Asuna-rabbit`
- `Toki-rabbit`
- `Toki-Combat`

子目录名是本次修复任务的角色包名，不一定等于游戏内部角色 ID。真正的角色 ID 请优先看该角色目录里的 `Report/RepairReport.md`，例如 `Toki-Combat` 的 `Character Id` 是 `CH0333`，并且可能有 `Associated Character Ids`，例如 `CH0333_Scenario`。

## 快速入口

给后续 agent 或 Unity 工程接手时，通常按这个顺序看：

1. 先打开 `Report/RepairReport.md`，确认 `Role`、`Character Id`、`Associated Character Ids`、修复日志和剩余风险。
2. 主要 3D 模型入口在 `Prefabs/SourceParts/<CharacterId>_Mesh.prefab`。
3. 核心材质在 `Materials/`，核心贴图在 `Textures/`，私有修复 Shader 在 `Shaders/`。
4. 嘴部控制脚本在 `Runtime/`，对应组件已经写入主模型 prefab。
5. 官方动画、技能、场景、Spine、音频等参考资源在 `Addressable/`、`Animations/`、`Effects/`、`SpineCharacters/`、`SpineLobbies/`、`Audio/` 等目录。

如果只是要加载可用的修复后 3D 角色模型，优先使用：

```text
<Role>/Prefabs/SourceParts/<CharacterId>_Mesh.prefab
```

不要一开始就拿 `Addressable/Character/*.prefab` 当主入口。那些更接近官方游戏运行时 prefab，通常包含控制器、录制动画、技能数据或游戏侧依赖，适合参考，不一定适合作为独立模型入口。

## 目录结构

### `Prefabs/SourceParts/`

修复后的核心 prefab 入口。

常见文件：

- `<CharacterId>_Mesh.prefab`：主 3D 角色模型。通常已经修复材质引用、写入嘴部事件接收脚本，并在需要时嵌入 Halo。
- `<CharacterId>_Halo.prefab`：Halo 源 prefab。如果主模型已经嵌入 Halo，这里仍可能保留原始 source part 方便检查。
- 其他 source part prefab：武器、道具、CafeOnly 或特殊模型部件，视角色而定。

推荐用途：

- LLM / agent 做骨骼控制、模型预览、材质检查时，以这里的主 Mesh prefab 为第一入口。
- 如果要找主模型绑定的材质槽，也从这个 prefab 查。

### `Materials/`

修复后的核心材质。

这里通常包含：

- 当前角色材质，例如 `CH0333_Body.mat`、`CH0333_EyeMouth.mat`。
- 继承来的基础角色材质，例如皮肤角色可能保留 `Asuna_Original_Weapon.mat`、`CH0187_EyeMouth.mat`。
- Associated/Scenario 材质，例如 `CH0333_Scenario_Body.mat`，用于保留官方附属资源。

说明：

- 材质里的官方 Shader 引用已经替换为 `Shaders/` 下生成的私有 Shader。
- `EyeMouth` 材质会使用统一复制的嘴型贴图，并带有修复后的 tile 参数。
- 某些继承型皮肤缺自己的 EyeMouth、Eyebrow、Weapon、Halo 等材质时，工具会从基础角色复制对应材质。

注意：

- 不要只按文件名前缀判断材质归属。皮肤角色可能会使用基础角色材质。
- 不要删除 `.meta` 文件，否则 prefab 里的 GUID 引用会断。

### `Textures/`

修复后的核心贴图和材质依赖贴图。

常见内容：

- Body / Face / Hair / EyeMouth / Halo / Weapon 等角色贴图。
- `_MXCommon/Mouth/` 或类似目录下的嘴型贴图。
- `DefaultTextures/tex_green_128.png` 等默认 Mask / fallback 贴图。

用途：

- 给 `Materials/` 中的材质提供 `_MainTex`、`_MaskTex`、`_MouthTileTex` 等贴图。
- 如果眼睛、嘴、脸、武器显示异常，优先检查对应材质的贴图 GUID 是否指向这里。

### `Shaders/`

工具生成的私有 Shader。

命名通常类似：

```text
ModelRepair/<Role>/<CharacterId>/MX_C-EyesMouth
ModelRepair/<Role>/<CharacterId>/MX_C-General_Layer4
ModelRepair/<Role>/<CharacterId>/FX_General Unlit Texture
```

用途：

- 替代原导出工程里缺失的官方 Shader GUID。
- 让修复后的材质能在目标 Unity 工程里独立引用。

注意：

- 这些 Shader 是修复用近似实现，不等同于官方完整运行时 Shader。
- 如果后续追求更高视觉还原度，可以基于这些 Shader 继续调参或替换，但替换前请保留 GUID/引用关系。

### `Runtime/`

工具生成的运行时辅助脚本。

最常见的是角色专属的 `CharacterMouthEventReceiver` 类，例如：

```text
Toki_Combat_CH0333_CharacterMouthEventReceiver.cs
```

用途：

- 负责嘴部贴图 tile 切换。
- 组件已经被写入 `Prefabs/SourceParts/<CharacterId>_Mesh.prefab`。
- 组件会记录 `MouthRenderer`、`MouthMaterialIndex`、`MouthDefaultUV` 等信息。

注意：

- 不要删除 `Runtime/`，否则主 prefab 上的脚本组件会 Missing。
- 嘴部材质槽不是固定写死的，不同角色可能不同。以 `RepairReport.md` 或 prefab 内组件记录为准。

### `Models/SourceAssets/`

模型相关的源资产副本。

常见内容：

- Mesh `.asset`
- Avatar `.asset`
- 其他模型源数据

用途：

- 支撑 `Prefabs/SourceParts/` 中的 Mesh / SkinnedMeshRenderer。
- 当 prefab 缺 mesh、avatar、bounds 或 renderer 数据时，到这里查依赖。

### `Addressable/Character/`

从官方导出工程整理来的角色 Addressable 资源。

常见内容：

- `<CharacterId>.prefab`
- `Cafe/Cafe_<CharacterId>.prefab`
- `Echelon/Echelon_<CharacterId>.prefab`
- `Strategy/Strategy_<CharacterId>.prefab`
- `*.overrideController`
- `*_Public.asset`
- `*_EX*.asset`
- `Recorded*.anim`
- Associated/Scenario 入口，例如 `CH0333_Scenario.prefab`

用途：

- 参考官方 prefab 如何组织 renderer、动画控制器、口型组件、技能数据。
- 查看 Cafe / Echelon / Strategy 等不同场景下的官方入口。
- 对 Scenario 类资源，保留官方技能/剧情演出相关入口。

注意：

- 这里的资源可能仍包含游戏运行时系统依赖。
- 需要独立加载 3D 模型时，仍推荐先用 `Prefabs/SourceParts/<CharacterId>_Mesh.prefab`。

### `Addressable/CafeAnimations/`

Cafe 场景相关动画资源。

用途：

- 角色咖啡厅 idle、walk、reaction 等行为参考。
- 可以配合 `Addressable/Character/Cafe/` 下的 prefab 或 controller 查看。

### `Animations/`

角色动画片段、控制器、技能 cutin 动画等。

用途：

- 预览角色动作。
- 查找技能、战斗、胜利、Cafe 等动画资源。
- 配合 `Addressable/Character/*.overrideController` 或 `Timelines/` 使用。

注意：

- 动画能否直接播放取决于目标 prefab、Avatar 和骨架是否匹配。

### `Effects/`

角色技能、演出、命中特效等 VFX 资源。

常见子内容：

- `Prefab/`：特效 prefab。
- `Material/`：特效材质。
- `Texture/`：特效贴图。
- `Mesh/`：特效 mesh。
- `PPV/`：后处理相关 asset。

用途：

- 技能演出、EX 技能、场景特效的参考和迁移。
- 如果 agent 要重建技能演出，先看这里和 `Timelines/`。

注意：

- 特效材质可能使用 fallback shader 修复，视觉上不一定完全等同官方。
- `RepairReport.md` 的 `Output Deadbeef References` 中常能看到 Effects 相关残留，这是官方导出资源复杂依赖导致的，是否需要处理取决于使用目标。

### `Audio/`

角色音频资源。

用途：

- 技能语音、动作音效、Cafe/Scenario 音频等。
- Associated/Scenario 角色可能也会有自己的音频目录。

### `RootMotion/`

Root motion 相关资源。

用途：

- 角色动画位移、技能移动轨迹等参考。
- 如果只做静态模型或简单骨骼控制，通常不是第一优先级。

### `Timelines/`

官方 Timeline / cutscene / 技能演出时序资源。

用途：

- EX 技能、cutin、演出镜头、特效触发顺序参考。
- 常与 `Animations/`、`Effects/`、`Audio/` 一起看。

### `SpineCharacters/` 和 `SpineLobbies/`

2D Spine 资源。

区别：

- `SpineCharacters/`：角色 Spine 立绘/小人相关资源。
- `SpineLobbies/`：大厅/主页 Spine 资源。

常见内容：

- `*_Atlas.asset`
- `*_SkeletonData.asset`
- `*.mat`
- `*.anim`
- 贴图 atlas

用途：

- 2D 展示、Lobby 动画、角色 Spine 表情动作参考。
- 与 3D 主模型是两套资源体系，不要混淆。

### `Report/`

修复报告。

常见文件：

- `RepairReport.md`：给人读。
- `RepairReport.json`：给脚本或 agent 读。

重点字段：

- `Role`：修复角色包名，对应当前目录名。
- `Character Id`：主角色 ID，主模型和核心资源以它为准。
- `Associated Character Ids`：附属角色/Scenario 资源 ID，例如 `CH0333_Scenario`。
- `Copied Categories`：本次复制了哪些类别资源。
- `Generated Private Shaders`：官方 Shader 到私有 Shader 的映射。
- `Repaired Materials`：材质、贴图、Shader 修复记录。
- `Patched Prefabs`：prefab 材质槽、嘴部组件、Halo 等修复记录。
- `Core Missing Material References`：核心 prefab 是否还有缺材质。
- `Core Missing GUID References`：核心材质/prefab 是否还有断 GUID。
- `Output Deadbeef References`：整个输出包内仍残留的 deadbeef 引用。
- `Warnings`：非致命警告。

判断核心模型是否修好，优先看：

```text
Core Missing Material References: None
Core Missing GUID References: None
```

`Output Deadbeef References` 不一定代表主模型不可用，因为它会扫描 Addressable、Effects、Timelines、Spine 等所有附属资源。对主 3D 模型来说，核心缺失引用字段更重要。

## 三种角色关系

### 主角色 `Character Id`

当前修复包的核心角色 ID。

例子：

```text
Role: Toki-Combat
Character Id: CH0333
```

主入口：

```text
Toki-Combat/Prefabs/SourceParts/CH0333_Mesh.prefab
```

### 继承角色 / 基础角色

皮肤角色可能继承原角色的一部分材质或模型资源。

例子：

- `Asuna-rabbit` 的主角色是 `CH0098`，但可能包含 `Asuna_Original_Weapon.mat`。
- `Toki-rabbit` 的主角色是 `CH0211`，但可能包含 `CH0187_EyeMouth.mat`、`CH0187_Weapon.mat`。

含义：

- 这些资源是官方皮肤复用基础角色资产的结果。
- 不要因为文件名前缀不同就删除它们。
- prefab 里可能确实引用这些继承材质。

### Associated / Scenario 角色

新一些的角色包可能同时包含主角色和 Scenario/演出模型。

例子：

```text
Role: Toki-Combat
Character Id: CH0333
Associated Character Ids: CH0333_Scenario
```

含义：

- `CH0333` 是主角色。
- `CH0333_Scenario` 是附属的技能/剧情/场景演出资源。
- Associated 资源会被保留在输出包里，但主 3D 模型入口仍然是 `CH0333_Mesh.prefab`。

常见位置：

```text
Toki-Combat/Addressable/Character/CH0333_Scenario.prefab
Toki-Combat/Animations/CH0333_Exs*.anim
Toki-Combat/Effects/...
```

## 推荐使用流程

### 导入到目标 Unity 工程

1. 将某个角色目录整体复制到目标工程 `Assets/` 下，例如：

```text
Assets/Toki-Combat/
```

2. 必须保留所有 `.meta` 文件。
3. 等待 Unity 导入完成。
4. 打开：

```text
Assets/Toki-Combat/Prefabs/SourceParts/CH0333_Mesh.prefab
```

5. 检查材质、贴图、Shader、嘴部组件是否正常。

### 给 agent 继续处理

建议让 agent 先读：

```text
<Role>/Report/RepairReport.md
<Role>/Prefabs/SourceParts/<CharacterId>_Mesh.prefab
<Role>/Materials/
<Role>/Runtime/
```

如果目标是技能/演出，再继续读：

```text
<Role>/Addressable/Character/
<Role>/Animations/
<Role>/Effects/
<Role>/Timelines/
<Role>/Audio/
```

如果目标是 2D/Lobby 展示，再读：

```text
<Role>/SpineCharacters/
<Role>/SpineLobbies/
```

## 常见问题

### 角色目录名和 Character Id 不一样怎么办？

以 `Report/RepairReport.md` 的 `Character Id` 为准。

目录名只是这次修复任务的包名，可能是 `Asuna-rabbit`、`Toki-Combat` 这种人类可读名称；真正的 prefab、材质、贴图通常按 `CHxxxx` 或官方原始 ID 命名。

### 为什么一个皮肤目录里有另一个角色 ID 的材质？

这是官方资源继承或皮肤复用导致的。工具会按实际 prefab 引用和官方源文件补齐材质。不要只因为前缀不同就删掉。

### 为什么 Report 里还有 Output Deadbeef References？

`Output Deadbeef References` 是全包扫描结果，会包含 Addressable、Effects、Timelines、Spine 等附属资源。主 3D 模型是否可用，优先看核心字段：

```text
Core Missing Material References
Core Missing GUID References
```

如果这两个是 `None`，通常说明核心模型、核心材质、核心 prefab 已经闭环。

### 嘴巴/眼睛坏了先查哪里？

先查主模型上的 `CharacterMouthEventReceiver` 组件：

- `MouthRenderer`
- `MouthMaterialIndex`
- `MouthDefaultUV`

再查 `Materials/<...>_EyeMouth.mat`：

- `_MainTex` 应该指向眼口主贴图。
- `_MouthTileTex` 应该指向公共嘴型 tile 贴图。
- `_MouthTileCols` / `_MouthTileRows` 通常是 `8` / `8`。
- `_MouthTileTex` 的 offset 应与 Report 中发现的官方 `MouthDefaultUV` 一致。

不要随意把嘴部 material index 改成固定值。不同角色和皮肤的官方槽位可能不同。

### 可以移动文件吗？

可以整体移动角色目录，但不要只移动单个文件，也不要丢失 `.meta`。Unity 资源引用依赖 GUID，`.meta` 丢失会导致 prefab、材质、贴图引用断开。

## 最小可用清单

如果要判断一个角色目录是否具备最小可用的修复后 3D 模型，至少应存在：

```text
<Role>/Prefabs/SourceParts/<CharacterId>_Mesh.prefab
<Role>/Materials/
<Role>/Textures/
<Role>/Shaders/
<Role>/Runtime/
<Role>/Report/RepairReport.md
```

并且 `RepairReport.md` 中：

```text
Core Missing Material References: None
Core Missing GUID References: None
```

满足这些条件后，通常就可以把 `Prefabs/SourceParts/<CharacterId>_Mesh.prefab` 作为主要 3D 模型入口继续开发、预览或交给 agent 处理。
