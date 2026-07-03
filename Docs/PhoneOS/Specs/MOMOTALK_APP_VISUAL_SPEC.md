# PhoneOS Momotalk App 视觉规格

更新时间：2026-07-03

本文记录 PhoneOS 内 Momotalk App 的当前视觉开发方向、组件规格、工程边界和阶段性验收标准。它用于替代此前仅凭截图描述的模糊 UI 要求，后续 Codex / 开发 Agent / Unity 开发者修改 Momotalk App 时，应先阅读本文。

本文只约束 `VirtualPartner/Assets/VirtualPartner/Runtime/PhoneOS/UI/Momotalk/` 与 `VirtualPartner/Assets/VirtualPartner/UI/PhoneOS/Prefabs/Apps/MomotalkApp.prefab` 相关的 App 内 UI。PhoneOS 桌面、状态栏、底部 Android 导航栏、AppHost、QuickSettings、RecentApps 等系统级内容仍以 `PHONEOS_VISUAL_SPEC.md` 和后续 PhoneOS Shell 文档为准。

---

## 1. 当前阶段目标

当前 Momotalk App 视觉阶段为：

```text
Stage 5.2 - Momotalk App Visual Alignment Sprint
```

本阶段目标不是接入真实聊天业务，而是先让 PhoneOS 内 Momotalk App 的联系人页和聊天页，在结构、比例、信息密度和视觉语言上接近 `androidInReact` / WhatsApp 风格。

当前明确采用以下策略：

1. 先 1:1 对齐 Android in React / WhatsApp 的结构和绿色主题；
2. 等 App 结构、比例、密度接近目标后，再切换为 Momotalk 粉色主题；
3. 联系人列表真实数据当前只显示 Toki；
4. 后续接入多角色后，再动态增加联系人；
5. 聊天页需要支持图片消息预览样式；
6. PhoneOS 状态栏和底部 Android 导航栏由 PhoneOS Shell 统一提供；
7. Momotalk App 内部只负责 AppBar 以下内容；
8. 允许扩展 `MomotalkTheme`，集中配置颜色、Sprite、字体、尺寸和布局 token。

---

## 2. 视觉参考来源

主要参考：

```text
blueedgetechno/androidInReact
Android in React demo
WhatsApp contact list page
WhatsApp chat page
```

参考的是视觉结构与交互组织方式，不复制 React 项目代码，不嵌入 WebView，不直接使用 WhatsApp 品牌资源，不把网页运行在 Unity 中。

当前优先参考以下特征：

1. App 内顶部绿色 AppBar；
2. Contacts / Status / Calls 三段 Tab；
3. 高信息密度联系人列表；
4. 右下角绿色 Floating Action Button；
5. 聊天页顶部联系人 AppBar；
6. 浅色聊天纹理背景；
7. 左右消息气泡；
8. 图片消息卡片；
9. 底部输入栏胶囊；
10. 附件、相机、语音等图标位置。

注意：状态栏时间、电量、WiFi、底部 Android 三键导航不属于 Momotalk App 内部，不应在 Momotalk App prefab 内重复绘制。

---

## 3. App 边界

PhoneOS Shell 负责：

```text
PhoneRoot
  StatusBar
  AppHost / AppWindow container
  NavigationBar
  Home / Back / Recent system behavior
```

Momotalk App 负责：

```text
MomotalkApp
  ContactListPage
    AppBar
    TabBar
    ContactList
    FloatingActionButton

  ChatPage
    AppBar
    MessageBackground
    MessageScrollView
    InputBar
```

Momotalk App 禁止重复实现：

1. 顶部系统状态栏；
2. 底部 Android NavigationBar；
3. PhoneOS Home / Recent / Quick Settings；
4. AppHost 打开关闭真实业务；
5. WebView；
6. React 运行时代码。

---

## 4. 设计参考尺寸

PhoneOS 总参考尺寸仍以 `PHONEOS_VISUAL_SPEC.md` 为准：

```text
Phone Reference Size: 440 x 960
```

Momotalk App 只设计 App 内容区。假设 PhoneOS Shell 已经保留顶部状态栏和底部导航栏，Momotalk App 内容区建议使用：

```text
App Content Width: 440
App Content Height: 880 - 900，实际由 AppWindow / Shell 决定
```

如果当前 prefab 暂时运行在完整 440 x 960 容器中，也应通过布局预留 Shell 区域，不要把 AppBar 顶到物理手机最顶部再额外绘制状态栏。

---

## 5. 主题方向

### 5.1 当前对齐主题

当前阶段先使用 WhatsApp / Android 风格绿色主题，建议色板如下：

```text
WhatsApp Green: #128C7E
WhatsApp Dark Green: #075E54
WhatsApp Accent Green: #00A884
Outgoing Bubble: #DCF8C6
Incoming Bubble: #FFFFFF
Chat Background: #ECE5DD
Contact Page Background: #FFFFFF
Primary Text: #202124
Secondary Text: #667781
Muted Text: #8696A0
Divider: #E9EDEF
Unread Badge: #25D366
```

### 5.2 后续 Momotalk 主题

结构对齐后，再切换到 Momotalk 粉色主题。切换时不得重新打散布局，只能通过 `MomotalkTheme` 或相邻主题配置替换颜色、Sprite 和少量 token。

后续粉色主题建议保留：

```text
Momotalk Pink: #F78FB3
Momotalk Accent: #FF6F9F
Soft Pink Background: #FFF7FA
Soft User Bubble: #FFD4E2
```

---

## 6. MomotalkTheme 配置要求

`MomotalkTheme` 不应只保存颜色和 Sprite。它应成为 Momotalk App 的视觉 token 入口。

建议字段：

```csharp
// Color
Color appBarColor;
Color appBarDarkColor;
Color accentColor;
Color contactBackgroundColor;
Color chatBackgroundColor;
Color incomingBubbleColor;
Color outgoingBubbleColor;
Color primaryTextColor;
Color secondaryTextColor;
Color mutedTextColor;
Color dividerColor;
Color unreadBadgeColor;

// Sprite
Sprite appBarBackground;
Sprite contactItemBackground;
Sprite chatBackgroundPattern;
Sprite incomingBubbleSprite;
Sprite outgoingBubbleSprite;
Sprite inputBarBackground;
Sprite unreadBadgeSprite;
Sprite fabBackgroundSprite;
Sprite imageMessageMaskSprite;

// Layout
float appBarHeight;
float tabBarHeight;
float contactItemHeight;
float avatarSize;
float floatingActionButtonSize;
float chatHorizontalPadding;
float messageSpacing;
float bubbleMaxWidth;
float inputBarHeight;
float inputPillHeight;
float imageMessageMaxWidth;
float imageMessageMaxHeight;

// Typography
int titleFontSize;
int tabFontSize;
int contactNameFontSize;
int contactPreviewFontSize;
int messageFontSize;
int messageTimeFontSize;
int inputFontSize;
```

禁止项：

1. 不允许把同类颜色散落在多个 View 脚本中；
2. 不允许联系人项高度、头像大小、气泡最大宽度只写在 prefab 里；
3. 不允许为了单张截图在单个 GameObject 上手动写死坐标；
4. 不允许联系人页和聊天页分别定义互不相干的字体体系；
5. 不允许主题切换时需要重写布局脚本。

---

## 7. 联系人列表页规格

### 7.1 目标结构

```text
ContactListPage
  PageBackground
  AppBar
    TitleText: WhatsApp / Momotalk
    SearchButton
    MoreButton
  TabBar
    ChatsTab
    StatusTab
    CallsTab
    ActiveUnderline
  ContactListScrollView
    ContactItem_Toki
  FloatingActionButton
```

当前阶段 title 可暂时显示 `WhatsApp` 或 `Momotalk`。如果目标是结构对齐，建议先显示 `WhatsApp` 以对比参考图；进入项目品牌化阶段后再改为 `Momotalk`。

### 7.2 推荐尺寸

基于 440 宽度：

```text
AppBar Height: 56
TabBar Height: 48
ContactItem Height: 72 - 76
Avatar Size: 48
List Horizontal Padding: 0
Contact Text Left After Avatar: 12
Right Time Width: 56
Unread Badge Size: 20
FAB Size: 56
FAB Right: 20
FAB Bottom Inside App: 20 - 28
```

如果 AppWindow 已经提供顶部 title bar，应隐藏或压缩 AppWindow 的默认 title，避免出现“双标题栏”。Momotalk App 内部 AppBar 应是主要视觉标题栏。

### 7.3 联系人数据

当前真实联系人只显示 Toki：

```text
Name: Toki
Preview: This is the new PhoneOS Momotalk preview.
Time: 18:25
Unread: 1
```

不要为了让真实 UI 看起来像参考图而伪造多角色联系人。后续多角色接入后，联系人列表再从角色注册系统动态生成。

允许在纯视觉测试或编辑器预览模式中使用 mock 联系人，但必须满足：

1. 只在 Preview / Editor / Debug 模式出现；
2. 不进入真实运行默认路径；
3. 代码或配置中明确标记 `previewOnly`；
4. 不影响后续真实角色动态列表。

### 7.4 联系人项视觉

联系人项应接近 Android 聊天列表：

```text
Avatar: 左侧 48 圆形
Name: 16sp / Medium / Primary
Preview: 13sp / Secondary / 单行截断
Time: 11sp / Muted / 右上
Unread Badge: 20 圆形 / 右侧中部
Divider: 从文本区域左侧开始，低透明浅灰
```

当前只有 Toki 一条时，页面下方可以自然留白，但第一条联系人本身的比例、边距和层级必须像真实 Android 聊天列表，而不是居中的卡片或大圆角面板。

### 7.5 Floating Action Button

联系人页右下角 FAB 应接近参考图：

```text
Size: 56 x 56
Shape: Circle
Color: #00A884 或 #25D366
Icon: message/chat symbol
Shadow: 轻微，可无
Position: 右下角，位于 PhoneOS NavigationBar 上方
```

禁止把 FAB 做成写着 `Msg` 的大文字圆球。FAB 内应使用图标，文字只允许作为临时调试占位，最终必须移除。

---

## 8. 聊天页规格

### 8.1 目标结构

```text
ChatPage
  AppBar
    BackButton
    Avatar
    ContactName
    VideoButton
    CallButton
    MoreButton
  ChatBackground
  MessageScrollView
    DateChip
    TextMessageBubble
    ImageMessageBubble
  InputBar
    EmojiButton
    InputPill
      Placeholder
      AttachmentButton
      CameraButton
    VoiceButton / SendButton
```

### 8.2 AppBar

推荐尺寸：

```text
AppBar Height: 56
Back Button Hit Area: 44 x 56
Avatar Size: 36 - 40
Title Font Size: 18
Action Icon Size: 22 - 24
Right Action Spacing: 16
```

AppBar 使用当前绿色主题。左侧返回按钮只负责返回联系人页，不负责 PhoneOS 系统 Back。PhoneOS 系统 Back 仍由 Shell 转发到 `IPhoneApp.OnBackPressed()`。

### 8.3 聊天背景

聊天背景应使用浅色底 + 低对比纹理，接近 WhatsApp doodle background 的气质。

建议：

```text
Base Color: #ECE5DD
Pattern Alpha: 0.05 - 0.12
Pattern Color: #7A7A7A 或低透明灰
```

如果暂时没有纹理 Sprite，可先使用纯 `#ECE5DD`，但文档和 TODO 中应保留“补充聊天纹理背景”的任务。

禁止使用纯白或纯粉大背景作为当前结构对齐阶段的聊天背景。

### 8.4 消息气泡

文本消息气泡推荐：

```text
Max Width: App Width * 0.74 - 0.78
Min Width: 64
Horizontal Padding: 10 - 12
Vertical Padding: 6 - 8
Corner Radius: 8 - 12
Message Font Size: 14
Time Font Size: 11
Message Spacing Y: 4 - 8
Incoming Color: #FFFFFF
Outgoing Color: #DCF8C6
```

左侧消息靠左，右侧消息靠右。时间戳应位于气泡右下角，颜色低对比，不应独立漂在气泡外太远。

当前预览消息应至少包含：

```text
Incoming text
Outgoing text
Incoming short text
Outgoing short text
Image message preview
```

### 8.5 图片消息预览

图片消息预览是本阶段必需能力，但只做静态样式，不接真实图片业务。

推荐结构：

```text
ImageMessageBubble
  BubbleBackground
  ImageMask
    PreviewImage
  CaptionText optional
  TimeText
```

推荐尺寸：

```text
Image Max Width: 320 - 340
Image Max Height: 360 - 420
Image Corner Radius: 8 - 10
Caption Font Size: 13 - 14
```

如果没有正式图片资源，可使用项目内原创占位图或生成简单测试图。禁止使用第三方受版权限制的猫图或 WhatsApp 示例图作为正式资源。

### 8.6 输入栏

输入栏推荐：

```text
InputBar Height: 58 - 64
Horizontal Padding: 8 - 12
Input Pill Height: 44 - 48
Input Pill Corner Radius: 22 - 24
Emoji Icon Size: 24
Attachment Icon Size: 22
Camera Icon Size: 22
Voice / Send Button Size: 44 - 48
```

默认状态显示语音按钮；有输入文本时可切换为发送按钮。当前如果没有真实输入逻辑，可以保持 Send 按钮，但视觉上应接近 WhatsApp 的绿色圆形操作按钮，而不是粉色胶囊按钮。

---

## 9. 当前实现问题修正清单

基于当前 Unity 截图，本阶段需要修正以下问题：

### 9.1 联系人页

当前问题：顶部粉色区域和 Tab 比例不像 Android / WhatsApp，联系人列表只有一张大卡片感，FAB 使用 `Msg` 文字，页面空白显得像未完成 UI。

修正目标：AppBar + TabBar 结构先对齐 WhatsApp；Toki 联系人项按真实聊天列表密度排版；FAB 改为图标圆按钮；真实数据仍只显示 Toki。

### 9.2 聊天页

当前问题：聊天背景过于干净，消息气泡像预览卡片，输入栏像自定义粉色 UI，不像 Android 聊天 App。

修正目标：使用浅米色聊天背景；消息气泡改为 WhatsApp 左白右绿结构；加入图片消息预览；输入栏改为 emoji + 输入胶囊 + 附件/相机 + 圆形操作按钮结构。

### 9.3 主题配置

当前问题：`MomotalkTheme` 只覆盖部分颜色和 Sprite，不足以控制整体视觉比例。

修正目标：扩展 `MomotalkTheme` 或新增相邻 layout token 配置，使 AppBar、TabBar、联系人项、头像、气泡、输入栏、FAB、字体尺寸都可集中调整。

### 9.4 Shell 边界

当前问题：容易把状态栏、底部导航和 App 内 UI 混在一起，造成层级不清。

修正目标：Momotalk App 内只负责 AppBar 以下内容；状态栏和底部导航始终由 PhoneOS Shell 统一提供。

---

## 10. 工程实现建议

### 10.1 推荐修改范围

允许修改：

```text
VirtualPartner/Assets/VirtualPartner/Runtime/PhoneOS/UI/Momotalk/MomotalkTheme.cs
VirtualPartner/Assets/VirtualPartner/Runtime/PhoneOS/UI/Momotalk/MomotalkAppView.cs
VirtualPartner/Assets/VirtualPartner/Runtime/PhoneOS/UI/Momotalk/MomotalkContactListView.cs
VirtualPartner/Assets/VirtualPartner/Runtime/PhoneOS/UI/Momotalk/MomotalkContactItemView.cs
VirtualPartner/Assets/VirtualPartner/Runtime/PhoneOS/UI/Momotalk/MomotalkChatView.cs
VirtualPartner/Assets/VirtualPartner/Runtime/PhoneOS/UI/Momotalk/MomotalkChatBubbleView.cs
VirtualPartner/Assets/VirtualPartner/Runtime/PhoneOS/UI/Momotalk/MomotalkMessageData.cs
VirtualPartner/Assets/VirtualPartner/UI/PhoneOS/Prefabs/Apps/MomotalkApp.prefab
VirtualPartner/Assets/VirtualPartner/UI/PhoneOS/Prefabs/Apps/Momotalk/MomotalkTheme.asset
VirtualPartner/Assets/VirtualPartner/UI/PhoneOS/Sprites/...
```

谨慎修改：

```text
PhoneAppHost
PhoneAppWindowView
PhoneShellView
PhoneNavigationBarView
PhoneStatusBarView
PhoneRoot.prefab
```

只有当 AppWindow 默认 title bar 与 Momotalk AppBar 冲突时，才允许小范围调整 AppWindow 的显示策略，并必须说明是否影响其他 App。

禁止修改：

```text
LlmRelay
StagePlanPlayer
StagePlanValidator
MomotalkConversationController（旧主链路）
TtsManager
AsrManager
MemorySystem
SceneCamera
DebugPanel 业务逻辑
```

### 10.2 实现顺序

建议按以下顺序实现：

1. 扩展 `MomotalkTheme`，加入布局和字体 token；
2. 调整 `MomotalkTheme.asset` 为 WhatsApp 绿色结构对齐主题；
3. 重做联系人页 AppBar / TabBar / Toki contact item / FAB；
4. 重做聊天页 AppBar / 背景 / 输入栏；
5. 重做文本气泡布局；
6. 增加图片消息预览样式；
7. 截图对比并微调 token；
8. 只在结构验收后再考虑粉色 Momotalk 主题。

### 10.3 高内聚低耦合要求

Momotalk App 内部应保持：

```text
MomotalkAppView: 页面切换和 IPhoneApp 生命周期
MomotalkContactListView: 联系人页显示
MomotalkContactItemView: 单个联系人项
MomotalkChatView: 聊天页显示和消息列表构建
MomotalkChatBubbleView: 单条消息气泡
MomotalkTheme: 视觉 token 和资产入口
MomotalkMessageData: 当前阶段预览消息数据
```

不要让一个脚本同时负责 App 生命周期、联系人布局、气泡布局、主题颜色和真实聊天业务。

---

## 11. 阶段性验收标准

### 11.1 视觉验收

Stage 5.2 完成后，应满足：

1. 联系人页顶部结构明显接近 Android / WhatsApp AppBar + Tabs；
2. 当前主题为绿色，而不是粉色；
3. 联系人列表真实只显示 Toki；
4. Toki 联系人项比例接近真实聊天列表；
5. FAB 使用图标圆按钮，不再显示 `Msg` 文字；
6. 聊天页顶部 AppBar 接近 WhatsApp；
7. 聊天背景不是纯白或纯粉，应接近浅米色聊天背景；
8. 左右文本气泡宽度、颜色、圆角和时间戳更接近 WhatsApp；
9. 聊天页存在图片消息预览样式；
10. 输入栏接近 Android 聊天 App 输入栏结构；
11. PhoneOS 状态栏和底部导航栏没有被 Momotalk App 重复绘制；
12. UI 不再像粉色程序色块堆叠。

### 11.2 工程验收

必须满足：

1. 不接入真实 Momotalk 聊天业务；
2. 不修改 LLM / StagePlan / Memory / TTS / ASR 主链路；
3. 不引入 WebView；
4. 不复制 React 项目代码；
5. 主题颜色、关键尺寸和字体大小集中在 `MomotalkTheme` 或相邻配置；
6. 联系人仍可通过后续动态数据扩展；
7. Play Mode 下打开 Momotalk App 不报错；
8. PhoneOS Back 能从聊天页返回联系人页；
9. 其他 PhoneOS App 不因 Momotalk 调整被破坏。

### 11.3 截图验收

完成后应输出至少两张截图：

```text
Docs/PhoneOS/Reference/current_momotalk_stage5_2_contact_list.png
Docs/PhoneOS/Reference/current_momotalk_stage5_2_chat.png
```

如果能在 Unity 场景中展示 PhoneOS 外框，还应输出：

```text
Docs/PhoneOS/Reference/current_momotalk_stage5_2_scene.png
```

截图应与 Android in React / WhatsApp 参考图并排检查，重点比较结构、比例、密度，而不是只比较颜色。

---

## 12. 给开发 Agent 的任务说明

可直接把以下内容作为下一阶段实现任务：

```text
继续在 codex/phoneos-dev 分支开发，只做 PhoneOS 内 Momotalk App Stage 5.2 视觉对齐。

目标：
让 Momotalk App 的联系人页和聊天页先接近 Android in React / WhatsApp 的结构和绿色主题。结构接近后，后续再切换为 Momotalk 粉色主题。

必须做：
1. 扩展 MomotalkTheme，使颜色、Sprite、字体大小、关键布局尺寸集中配置。
2. 联系人页改为 WhatsApp 风格 AppBar + TabBar + Toki 联系人项 + 图标 FAB。
3. 联系人真实数据当前只显示 Toki，不伪造真实多联系人。
4. 聊天页改为 WhatsApp 风格 AppBar + 浅米色聊天背景 + 左右气泡 + 输入栏。
5. 增加图片消息预览样式。
6. Momotalk App 内部不绘制 PhoneOS 状态栏和底部 Android 导航栏。
7. 保持 PhoneOS Back 从聊天页返回联系人页。

禁止：
1. 不接真实 Momotalk 聊天业务。
2. 不迁移旧 Momotalk 主链路。
3. 不修改 LlmRelay、StagePlan、TTS、ASR、Memory。
4. 不实现 QuickSettings、RecentApps 或复杂 AppHost 功能。
5. 不引入 WebView。
6. 不复制 React 项目代码。
7. 不使用 WhatsApp 官方品牌资源作为正式资产。

验收：
1. Play Mode 打开 Momotalk App 无报错。
2. 联系人页整体更像 Android / WhatsApp。
3. 聊天页整体更像 Android / WhatsApp。
4. 当前主题为绿色。
5. Toki 是唯一真实联系人。
6. 图片消息预览存在。
7. 状态栏和底部导航仍由 PhoneOS Shell 提供。
```

---

## 13. 后续路线

Stage 5.2 完成后，建议后续路线：

```text
Stage 5.3:
  结构验收后，抽出 GreenTheme / MomotalkPinkTheme 两套主题配置。

Stage 5.4:
  接入真实 Toki 联系人摘要，但仍不接完整实时聊天。

Stage 5.5:
  将 PhoneOS Momotalk App 与现有 MomotalkConversationController 的真实会话能力做边界设计。

Stage 6:
  多角色接入后，联系人列表从 CharacterRegistry / PhoneOS App 数据源动态生成。
```

在任何阶段，Momotalk App 都应保持：

```text
结构清晰
主题集中
App 内聚
Shell 边界明确
不堆色块
不靠单张截图手调
```
