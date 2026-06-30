# PHONEOS_VISUAL_SPEC.md

# VirtualPartner PhoneOS 视觉规范文档

## 1. 文档目的

本文档用于约束 VirtualPartner 项目中 `VirtualPhoneOS` 模块的视觉风格、布局规格、UI 资产规范和阶段性验收标准。

`VirtualPhoneOS` 是 Unity 场景内的虚拟手机操作系统界面，用于承接项目后续各类功能模块。现有的 Momotalk、镜头移动、Debug 页面后续都应迁移为 PhoneOS 内部 App。未来新增的角色系统、家具系统、记忆系统、相册、设置、房间控制等功能，也应通过 App 形式接入 PhoneOS。

本文档不是普通 UI 描述文档，而是给 Codex / 开发 Agent / Unity 开发者使用的可执行视觉规范。后续修改 PhoneOS UI 时，应优先遵守本文档，避免再次出现“程序色块堆叠”“布局随意”“风格漂移”“写死 UI”的问题。

---

## 2. 视觉参考来源

PhoneOS 的视觉风格参考 `androidInReact` 项目的虚拟 Android 桌面效果，尤其参考其以下设计特征：

1. 粉色 / 紫色渐变壁纸；
2. 顶部状态栏；
3. 白色圆角 Google Search 风格搜索框；
4. 大号数字时钟 Widget；
5. 白色圆角天气 Widget；
6. 4 列 App 图标网格；
7. 底部 favorite bar / dock 应用区域；
8. Android 风格底部三键导航；
9. Quick Panel、Recent Apps、App 内导航等后续系统交互；
10. 模块化 App 和样式组织方式。

注意：本项目只参考其视觉语言和系统交互结构，不直接嵌入 React 项目，不使用 WebView，不把网页项目直接运行在 Unity 内部。

---

## 3. PhoneOS 的产品定位

PhoneOS 不是一个单独的 UI 页面，而是 VirtualPartner 的系统级交互入口。

目标结构如下：

```text
Unity Scene
  └─ VirtualPhoneOS
       ├─ HomeScreen
       ├─ StatusBar
       ├─ AppGrid
       ├─ Dock / FavoriteBar
       ├─ NavigationBar
       ├─ QuickSettings
       ├─ RecentApps
       ├─ NotificationCenter
       ├─ SettingsApp
       ├─ MomotalkApp
       ├─ CameraApp
       ├─ DebugApp
       ├─ CharacterApp
       ├─ RoomApp
       └─ Future Apps
```

PhoneOS 需要同时满足：

1. **视觉上像一个清爽的虚拟手机桌面**；
2. **工程上像一个可扩展的 App 容器**；
3. **设置上支持后续更换壁纸、时间格式、主题、图标布局、Dock 显示等扩展**；
4. **交互上支持 App 打开、返回、Home、Recent、Quick Panel、Notification 等后续功能**。

---

## 4. 当前阶段目标

当前阶段为：

```text
Stage 1.6 - Home Visual Alignment Sprint
```

本阶段只做 PhoneOS 桌面视觉对齐，不进入复杂系统功能开发。

### 4.1 本阶段允许修改

允许修改：

1. PhoneRoot.prefab；
2. HomeScreen 层级；
3. StatusBar 视觉；
4. SearchWidget 视觉；
5. BigClockWidget 视觉；
6. WeatherWidget 视觉；
7. AppGrid 布局；
8. Dock / FavoriteBar 视觉；
9. NavigationBar 视觉；
10. PhoneOSStyle 配置；
11. UI Sprites / Icons / 9-slice panel；
12. 视觉参考目录和文档。

### 4.2 本阶段禁止修改

禁止修改：

1. 不迁移 Momotalk 真实业务逻辑；
2. 不迁移 SceneCamera 真实业务逻辑；
3. 不迁移 Debug 真实业务逻辑；
4. 不实现 AppHost 打开真实 App；
5. 不实现复杂 Recent Apps；
6. 不实现 Quick Settings 真实功能；
7. 不实现真实系统设置；
8. 不接入 WebView；
9. 不复制 React 项目的运行时代码；
10. 不把 App 图标手动写死在场景层级里。

---

## 5. 视觉总体目标

PhoneOS 桌面应呈现以下气质：

```text
清爽、轻量、柔和、可爱、现代、虚拟手机感、轻拟物但不过度装饰。
```

具体要求：

1. 背景以粉色 / 紫色柔和渐变为主；
2. Widget 以白色圆角卡片为主；
3. App 图标使用圆角方形或圆形；
4. 字体使用清晰、轻量、统一的现代字体；
5. 桌面布局要有明显留白；
6. 底部 Dock 不应有厚重白色背景框；
7. 底部导航栏必须使用图标，不允许使用文字按钮；
8. UI 元素之间应有统一间距；
9. 不使用大面积纯色程序色块；
10. 不使用风格杂乱的临时图标。

---

## 6. 参考分辨率与坐标规范

### 6.1 逻辑分辨率

PhoneOS 的 uGUI 设计参考分辨率固定为：

```text
Phone Reference Size: 440 x 960
```

Canvas Scaler 建议：

```text
UI Scale Mode: Scale With Screen Size
Reference Resolution: 440 x 960
Match: 0.5
```

### 6.2 设计安全区

```text
Top Safe Area: 16 - 24 px
StatusBar Height: 28 - 32 px
Bottom Navigation Height: 36 - 44 px
Bottom Safe Area: 8 - 16 px
```

### 6.3 布局原则

1. 所有主 UI 元素应基于 `PhoneRoot` 的 440 x 960 参考尺寸设计；
2. 不允许在不同脚本中散落写死位置；
3. 关键尺寸应进入 `PhoneOSStyle.asset`；
4. AppGrid、Dock、NavigationBar 应使用锚点和 LayoutGroup / 自定义布局脚本控制；
5. 不允许每个图标手动摆坐标；
6. 不允许为了当前截图效果破坏后续自适应能力。

---

## 7. 推荐目录结构

建议在项目中建立以下目录：

```text
Docs/
  PhoneOS/
    Reference/
      target_home_reference_01.png
      target_home_reference_02.png
      current_phoneos_stage1_01.png
      current_phoneos_stage1_scene_01.png
      annotated_home_issues_01.png

    Specs/
      PHONEOS_VISUAL_SPEC.md
      PHONEOS_LAYOUT_SPEC.md

Assets/
  VirtualPartner/
    Runtime/
      PhoneOS/
        Core/
        UI/
        Animation/
        Settings/

    UI/
      PhoneOS/
        Prefabs/
        Sprites/
          Wallpaper/
          Icons/
          Common/
          Widgets/
          Navigation/
        Styles/
        AppDefinitions/
```

### 7.1 Reference 目录用途

`Docs/PhoneOS/Reference/` 用于保存视觉参考图和当前实现截图。

至少应包含：

```text
target_home_reference_01.png
```

用于保存目标效果图，即当前参考的 androidInReact 桌面图。

```text
current_phoneos_stage1_01.png
```

用于保存当前 Unity 内 PhoneOS 单独显示截图。

```text
current_phoneos_stage1_scene_01.png
```

用于保存当前 Unity 场景中 PhoneOS 叠加显示截图。

```text
annotated_home_issues_01.png
```

用于保存带标注的问题图，比如标注“Dock 白底过重”“底部导航应改为图标”。

Codex 后续修改 PhoneOS UI 时，应先阅读本规范，并查看 Reference 目录中的目标图和当前实现图。

---

## 8. PhoneOSStyle 配置规范

必须新增或完善：

```text
Assets/VirtualPartner/UI/PhoneOS/Styles/PhoneOSStyle.asset
```

`PhoneOSStyle` 应作为 PhoneOS 的视觉配置入口，不应把颜色、间距、尺寸全部写死在 Prefab 或脚本中。

建议字段：

```csharp
Vector2 phoneReferenceSize = new Vector2(440, 960);

Sprite wallpaperSprite;
Sprite roundedPanelSprite;

Color primaryTextColor;
Color secondaryTextColor;
Color mutedTextColor;
Color widgetPanelColor;
Color navigationIconColor;
Color navigationIconColorOnLight;
Color dockIconLabelColor;

Vector2 appIconSize = new Vector2(52, 52);
Vector2 appGridCellSize = new Vector2(96, 104);
Vector2 dockIconSize = new Vector2(52, 52);
Vector2 dockCellSize = new Vector2(96, 84);

float widgetCornerRadius = 20;
float searchWidgetHeight = 52;
float statusBarHeight = 30;
float navigationBarHeight = 40;

float appIconLabelFontSize = 12;
float statusBarFontSize = 10;
float widgetSmallFontSize = 12;
float widgetMediumFontSize = 16;
float clockLargeFontSize = 56;
```

### 8.1 颜色建议

```text
Primary Text: #2F3437
Secondary Text: #666A70
Muted Text: #8A8D93
White Panel: #FFFFFF
White Panel Alpha Optional: 0.92 - 1.0
Navigation Icon: #202124 或 #303236
Navigation Icon On Dark: #FFFFFF
Page Dot Active: #303236
Page Dot Inactive: rgba(48, 50, 54, 0.25)
```

### 8.2 禁止项

1. 不允许在多个脚本中分别写一套颜色；
2. 不允许每个 Widget 单独定义完全不同的圆角；
3. 不允许 Dock、AppGrid、NavigationBar 使用彼此无关的图标尺寸；
4. 不允许临时硬编码状态栏时间颜色；
5. 不允许为了某张截图手动改动单个图标坐标。

---

## 9. PhoneRoot 层级规范

`PhoneRoot.prefab` 应保持清晰层级。

推荐结构：

```text
PhoneRoot
  Canvas_Static
    Wallpaper

  Canvas_System
    StatusBar
      TimeText
      NetworkGroup
      SignalIcon
      WifiIcon
      BatteryText
      BatteryIcon

    HomeScreen
      SearchWidget
      BigClockWidget
      WeatherWidget
      OptionalClockWidget
      AppGrid
      PageIndicator
      DockBar

    NavigationBar
      RecentButton
      HomeButton
      BackButton

  Canvas_Overlay
    ToastRoot
    QuickSettingsPanel
    RecentAppsPanel
    NotificationPanel
```

### 9.1 Canvas 分层原则

1. `Canvas_Static`：只放壁纸和不频繁变化的静态背景；
2. `Canvas_System`：放状态栏、桌面、Dock、导航栏；
3. `Canvas_Overlay`：放后续 QuickSettings、RecentApps、Toast、通知；
4. 非交互 Canvas 不应挂 Graphic Raycaster；
5. 静态 Image 和 Text 应关闭 Raycast Target；
6. 动态元素应尽量隔离，避免一个元素变化导致整个 Canvas 重建。

---

## 10. 背景 Wallpaper 规范

### 10.1 风格

背景应参考 androidInReact 的粉色柔和渐变风格。

建议特征：

```text
主色：粉色、浅紫色、暖粉色
过渡：柔和渐变
纹理：允许轻微噪声 / 柔光质感
饱和度：中等，不刺眼
亮度：偏亮
```

### 10.2 禁止项

1. 不使用纯色背景；
2. 不使用过于复杂的插画背景；
3. 不使用强对比纹理；
4. 不使用影响图标识别的背景；
5. 不使用大面积暗色背景作为默认主题。

### 10.3 后续扩展

壁纸应由配置驱动，未来 Settings App 需要支持更换壁纸。

---

## 11. StatusBar 视觉规范

### 11.1 位置

```text
高度：28 - 32 px
顶部边距：8 - 12 px
左右内边距：20 - 28 px
```

### 11.2 内容

左侧：

```text
时间，例如 09:37
```

右侧：

```text
5G / WiFi / Battery
```

### 11.3 视觉要求

1. 状态栏应轻量；
2. 字号建议 9 - 11；
3. 图标使用深灰或白色，根据壁纸亮度自动或配置切换；
4. 不要使用过大的状态栏图标；
5. 不要让状态栏与搜索框挤在一起；
6. 状态栏文字和图标应水平对齐。

### 11.4 当前问题修正

当前版本状态栏基本可接受，但右侧图标偏文字化。后续应逐步替换为图标 Sprite 或 TMP 图标字体。

---

## 12. SearchWidget 视觉规范

SearchWidget 是桌面最重要的视觉锚点之一。

### 12.1 参考效果

应接近 androidInReact 中的 Google Search Widget：

```text
白色长条 pill
大圆角
左侧 G / Search 标识
中间搜索文本
右侧 Mic 图标
整体轻、宽、靠上
```

### 12.2 推荐尺寸

基于 440 x 960：

```text
Width: 388 - 392
Height: 50 - 54
Top: 48 - 62
Left/Right Margin: 24 - 28
Corner Radius: 24 - 28
```

### 12.3 内容布局

```text
SearchWidget
  BackgroundPanel
  LeftIconOrText
  SearchText
  RightMicIcon
```

### 12.4 视觉要求

1. 背景为白色；
2. 圆角必须足够大，接近胶囊形；
3. 搜索文字使用浅灰；
4. 左右图标不要过大；
5. 不要使用厚边框；
6. 不要使用投影过重的效果。

---

## 13. BigClockWidget 视觉规范

### 13.1 位置

BigClockWidget 应位于桌面左上到中上区域，和 WeatherWidget 形成左右平衡。

推荐：

```text
Left: 26 - 32
Top: 150 - 190
Width: 150 - 180
Height: 150 - 190
```

### 13.2 样式

参考目标图，BigClockWidget 应有明显的大号数字感。

建议显示方式：

```text
9
38
Tue, Jun 30
```

或：

```text
09:41
Sunday, Jun 28
```

### 13.3 字体

```text
Large Clock Font Size: 52 - 64
Date Font Size: 12 - 14
Weight: Light / Regular
Color: 深灰
```

### 13.4 禁止项

1. 不要让时钟太小；
2. 不要使用普通 UI Label 的默认粗黑字体；
3. 不要加白色背景框；
4. 不要和天气卡片重叠；
5. 不要和 AppGrid 距离太近。

---

## 14. WeatherWidget 视觉规范

### 14.1 位置

WeatherWidget 应位于右上区域，与 BigClockWidget 构成平衡。

推荐：

```text
Width: 170 - 185
Height: 160 - 230
Right: 24 - 32
Top: 145 - 190
Corner Radius: 18 - 22
```

如果采用当前阶段较小天气卡片：

```text
Width: 170 - 180
Height: 110 - 130
```

如果采用目标图 3 大天气卡片：

```text
Width: 180
Height: 240
```

### 14.2 内容

```text
WeatherWidget
  BackgroundPanel
  WeatherIcon
  TemperatureText
  LocationText
  DateText
```

示例：

```text
☀ 32°
New york
Tue, June 30
```

### 14.3 视觉要求

1. 背景为白色圆角卡片；
2. 天气图标使用黄色圆点或简化太阳图标；
3. 温度数字明显但不拥挤；
4. 地点和日期颜色较浅；
5. 圆角统一；
6. 不使用厚重阴影。

### 14.4 当前问题修正

当前版本 WeatherWidget 的方向基本正确，但应继续调整：

1. 卡片与主时钟的比例；
2. 温度文字大小；
3. 卡片内部留白；
4. 地点 / 日期层级；
5. 与搜索框、AppGrid 的垂直间距。

---

## 15. AppGrid 视觉规范

### 15.1 基础要求

AppGrid 必须由 `PhoneAppDefinition` / `PhoneAppRegistry` 动态生成，不允许在场景中手动摆死 App 图标。

### 15.2 网格规格

参考 androidInReact 的桌面网格，建议：

```text
Columns: 4
Rows: 按页面内容动态，Stage 1 可显示 1 - 2 行
Cell Width: 96
Cell Height: 104
Icon Size: 52 x 52
Label Font Size: 11 - 12
Label Top Margin: 6 - 8
```

### 15.3 当前阶段 App

Stage 1.6 至少显示：

```text
Momotalk
Camera
Debug
Settings
```

### 15.4 图标风格

图标应满足：

1. 圆角方形；
2. 尺寸统一；
3. 饱和度适中；
4. 图标内部 symbol 居中；
5. label 与图标居中对齐；
6. label 使用深灰或中灰；
7. 图标之间间距均匀；
8. 不使用风格差异过大的图标。

### 15.5 AppGrid 位置

当前 4 个 App 不应过早贴近顶部，也不应离 Dock 过近。

推荐：

```text
Top: 315 - 370
Horizontal Padding: 24 - 32
```

### 15.6 禁止项

1. 不允许每个图标单独写位置；
2. 不允许图标大小不统一；
3. 不允许 label 字号不统一；
4. 不允许 label 颜色过浅导致看不清；
5. 不允许 AppGrid 和 Dock 共享同一个 LayoutGroup，二者应是独立区域。

---

## 16. Dock / FavoriteBar 视觉规范

这是当前版本最需要修正的地方。

### 16.1 目标效果

目标图中的底部应用区域没有厚重白色背景框，底部应用更靠近屏幕底部，像真实 Android 桌面的 favorite bar。

### 16.2 当前问题

当前版本的 Dock 存在大面积白色圆角背景框，视觉重量过重，导致：

1. 和目标图差异大；
2. 抢走桌面主体注意力；
3. 让 PhoneOS 看起来像“游戏 UI 面板”，而不是“手机桌面”；
4. 与底部导航栏距离和层级不自然。

### 16.3 修正要求

必须执行：

1. 移除当前 DockBar 的厚重白色背景框；
2. Dock 图标直接悬浮在壁纸上；
3. 如必须保留 Dock 背景，只允许使用极轻量半透明背景；
4. Dock 图标整体下移；
5. Dock 和 NavigationBar 之间保留合理距离；
6. Dock 图标仍然由 `showInDock` 的 AppDefinition 动态生成。

### 16.4 推荐尺寸

```text
DockBar Height: 78 - 92
Dock Icon Size: 52 x 52
Dock Cell Width: 96
Dock Cell Height: 76 - 84
Dock Bottom Offset: 42 - 56
```

### 16.5 Dock 背景可选方案

首选：

```text
No Background
```

备选：

```text
Background: rgba(255, 255, 255, 0.18 - 0.28)
Blur / Glass optional
Corner Radius: 22 - 28
Height: 74 - 82
```

禁止：

```text
不允许使用当前这种接近纯白、不透明、厚重的大圆角背景框。
```

### 16.6 Dock App 数量

Stage 1.6 推荐 Dock 显示 3 - 4 个 App：

```text
Momotalk
Camera
Settings
```

或：

```text
Momotalk
Camera
Debug
Settings
```

其中 Debug 是否放在 Dock 后续可由配置决定。

---

## 17. PageIndicator 视觉规范

### 17.1 位置

PageIndicator 应位于 AppGrid 和 Dock 之间，靠近 Dock 上方。

推荐：

```text
Bottom Offset: 120 - 140
Dot Size: 5 - 6
Dot Spacing: 8 - 10
```

### 17.2 样式

```text
Active Dot: 深灰 / 半透明深灰
Inactive Dot: 低透明深灰
```

### 17.3 禁止项

1. 不要使用过大的点；
2. 不要使用高饱和颜色；
3. 不要让 PageIndicator 和 Dock 挤在一起；
4. 不要让 PageIndicator 被白色 Dock 背景吞掉。

---

## 18. NavigationBar 视觉规范

这是当前版本第二个必须修正的问题。

### 18.1 目标效果

底部导航栏应是 Android 三键导航风格，使用图标，不使用文字。

### 18.2 当前问题

当前版本显示为：

```text
Back     Home     Recent
```

这是不符合目标图的。文字按钮会让界面显得像调试工具，而不是手机系统。

### 18.3 必须修正

NavigationBar 必须改为图标按钮：

```text
Recent: 三竖线 / 最近任务图标
Home: 方形 / 圆形 / Home Gesture 图标
Back: 左箭头 / 返回图标
```

根据目标图，视觉顺序可使用：

```text
Recent    Home    Back
```

或按照项目内部逻辑使用：

```text
Back      Home    Recent
```

但需要在设计中统一，不允许同一阶段混用。

### 18.4 推荐尺寸

```text
NavigationBar Height: 36 - 44
Icon Size: 18 - 22
Bottom Offset: 8 - 14
Button Hit Area: 56 x 36
```

### 18.5 颜色

默认使用：

```text
#202124
```

或：

```text
rgba(32, 33, 36, 0.85)
```

如果壁纸较暗，可切换为：

```text
rgba(255, 255, 255, 0.85)
```

### 18.6 禁止项

1. 不允许使用文字 Back/Home/Recent；
2. 不允许图标颜色过浅到看不清；
3. 不允许使用过大的按钮背景；
4. 不允许给导航按钮加厚重白色面板；
5. 不允许导航栏和 Dock 混成一个白色大面板。

---

## 19. 图标资产规范

### 19.1 图标类型

PhoneOS 图标分为两类：

```text
System Icons:
  StatusBar icons
  NavigationBar icons
  Search / Mic icons
  PageIndicator dots
  QuickSettings icons

App Icons:
  Momotalk
  Camera
  Debug
  Settings
  Character
  Room
  Memory
  Album
  Schedule
  Store
```

### 19.2 图标尺寸

```text
App Icon Source Size: 128 x 128 或 256 x 256
Runtime Display Size: 52 x 52

Navigation Icon Source Size: 64 x 64
Runtime Display Size: 18 - 22

Status Icon Source Size: 32 x 32 或 64 x 64
Runtime Display Size: 10 - 16
```

### 19.3 风格要求

1. App 图标使用统一圆角比例；
2. 内部 symbol 尺寸统一；
3. 不使用多套风格混杂图标；
4. 不直接使用 Google、YouTube、WhatsApp 等品牌图标作为项目正式资源；
5. 可参考 androidInReact 的图标布局和尺寸，但应设计 VirtualPartner 自己的图标体系。

### 19.4 命名规范

```text
icon_app_momotalk.png
icon_app_camera.png
icon_app_debug.png
icon_app_settings.png

icon_nav_back.png
icon_nav_home.png
icon_nav_recent.png

icon_status_wifi.png
icon_status_signal.png
icon_status_battery.png

icon_widget_search.png
icon_widget_mic.png
icon_widget_weather_sun.png
```

---

## 20. 圆角面板与 9-slice 规范

PhoneOS 中所有可拉伸圆角面板必须使用 9-slice。

包括：

```text
SearchWidget 背景
WeatherWidget 背景
Settings 列表项
AppWindow 背景
Toast 背景
Notification 背景
QuickSettings 面板
Momotalk 聊天气泡
联系人卡片
```

### 20.1 导入设置

```text
Texture Type: Sprite
Sprite Mode: Single
Mesh Type: Full Rect
Border: 16 / 16 / 16 / 16 或 20 / 20 / 20 / 20
Image Type: Sliced
```

### 20.2 禁止项

1. 不允许普通拉伸圆角 PNG；
2. 不允许每个尺寸单独导出一张圆角卡片；
3. 不允许圆角拉伸变形；
4. 不允许圆角半径在不同 Widget 中无规则变化。

---

## 21. 字体规范

### 21.1 推荐

优先使用 TextMeshPro。

### 21.2 字体气质

应接近 Android / Material 风格：

```text
清晰、轻量、现代、无衬线
```

### 21.3 字号建议

```text
StatusBar: 9 - 11
App Label: 11 - 12
Widget Small Text: 11 - 12
Widget Medium Text: 14 - 16
Weather Temperature: 30 - 40
Big Clock: 52 - 64
```

### 21.4 字重

```text
StatusBar: Medium
App Label: Medium / SemiBold
Widget Date: Regular
Big Clock: Light / Regular
Weather Temperature: Light / Regular
```

### 21.5 禁止项

1. 不允许使用 Unity 默认字体作为最终效果；
2. 不允许字号忽大忽小；
3. 不允许 App label 太粗；
4. 不允许 Widget 文本过黑过重；
5. 不允许底部导航使用文字替代图标。

---

## 22. 动效规范

Stage 1.6 不要求实现复杂动画，但需要预留动效方向。

### 22.1 基础动效

后续应支持：

```text
Open Phone:
  scale 0.96 -> 1.0
  alpha 0 -> 1
  duration 0.16 - 0.22s

Tap App Icon:
  scale 1.0 -> 0.88 -> 1.0
  duration 0.12 - 0.18s

Open App:
  scale 0.2 -> 1.0
  alpha 0 -> 1
  duration 0.20 - 0.28s

Back Home:
  scale 1.0 -> 0.96
  alpha 1 -> 0
  duration 0.16 - 0.22s

QuickPanel:
  translateY -100% -> 0
  duration 0.25 - 0.35s
```

### 22.2 动效气质

1. 轻；
2. 快；
3. 不拖泥带水；
4. 不使用过度弹性；
5. 不影响操作反馈；
6. 接近手机系统动画，而不是网页弹窗动画。

---

## 23. 当前版本问题清单

基于当前 PhoneOS 截图，Stage 1.6 必须修正以下问题。

### 23.1 Dock 区域

当前问题：

```text
Dock 使用了明显白色大背景框。
```

修正：

```text
移除 Dock 白色背景框，或改为非常轻的透明背景。
Dock 图标下移，更接近目标图。
```

验收：

```text
底部应用看起来像桌面 favorite bar，而不是一个单独卡片面板。
```

### 23.2 NavigationBar

当前问题：

```text
底部导航是 Back / Home / Recent 文字。
```

修正：

```text
改为图标按钮。
```

验收：

```text
底部导航一眼看起来像 Android 三键导航。
```

### 23.3 Navigation 颜色

当前问题：

```text
当前文字配色不突出，可读性弱。
```

修正：

```text
图标颜色使用深灰或根据壁纸亮度自适应。
```

验收：

```text
在粉色壁纸上能清楚看到导航图标，但不抢主视觉。
```

### 23.4 Widget 比例

当前问题：

```text
WeatherWidget、BigClockWidget、SearchWidget 已经有雏形，但整体比例还需要继续贴近目标图。
```

修正：

```text
增大主时钟视觉权重；
调整天气卡片宽高和内部留白；
保持搜索框胶囊感。
```

### 23.5 图标系统

当前问题：

```text
图标已有基本资源，但风格还需统一。
```

修正：

```text
统一 AppIcon 尺寸、圆角、内部 symbol 比例和 label 样式。
```

---

## 24. Unity 实现约束

### 24.1 不允许 WebView

PhoneOS 必须使用 Unity uGUI 实现，不允许通过 WebView 直接嵌入 androidInReact。

原因：

1. 后续要与 Unity 场景交互；
2. 后续要接入角色、镜头、Debug、TTS、ASR、LLM；
3. WebView 与 Unity 运行时状态同步复杂；
4. 不利于打包和维护；
5. 不利于后续 App 模块扩展。

### 24.2 不允许把视觉写死在脚本里

以下内容必须配置化：

```text
Wallpaper
App Icon
App Label
Dock 显示
App 排序
Widget 显示
主题颜色
字体颜色
导航图标
```

### 24.3 不允许破坏 AppDefinition 机制

App 图标必须来自：

```text
PhoneAppDefinition
PhoneAppRegistry
```

不允许直接在 `PhoneRoot.prefab` 里手动摆 App 图标作为最终实现。

### 24.4 不允许过早接入业务

Stage 1.6 只修视觉，不接入 Momotalk / Camera / Debug 的真实功能。

---

## 25. Stage 1.6 验收标准

Stage 1.6 完成后，应满足：

### 25.1 视觉验收

1. PhoneOS 桌面整体接近参考图气质；
2. 粉色渐变壁纸自然；
3. SearchWidget 为白色胶囊形；
4. BigClockWidget 有明显视觉重量；
5. WeatherWidget 为白色圆角卡片；
6. AppGrid 为 4 列布局；
7. AppIcon 尺寸统一；
8. Dock 不再有厚重白色背景框；
9. Dock 图标位置更靠下；
10. PageIndicator 位置自然；
11. NavigationBar 使用图标；
12. NavigationBar 不再使用 Back/Home/Recent 文字；
13. 状态栏清晰但不抢眼；
14. UI 不再像程序色块堆叠。

### 25.2 工程验收

1. App 图标仍由 AppDefinition 动态生成；
2. Dock 图标仍由 showInDock 配置生成；
3. PhoneOSStyle.asset 存在并可调整关键视觉参数；
4. PhoneRoot.prefab 层级命名清晰；
5. 非交互 UI 关闭 Raycast Target；
6. 不修改现有 Momotalk 业务；
7. 不修改现有 Camera 业务；
8. 不修改现有 Debug 业务；
9. 不新增真实 AppHost；
10. 不新增复杂 Recent / QuickPanel 逻辑。

### 25.3 截图验收

必须输出两张截图用于对比：

```text
Docs/PhoneOS/Reference/current_phoneos_stage1_6_home.png
Docs/PhoneOS/Reference/current_phoneos_stage1_6_scene.png
```

第一张为单独 PhoneOS UI 截图。
第二张为 Unity 场景中 PhoneOS 叠加显示截图。

---

## 26. 给 Codex 的开发注意事项

Codex 修改时必须遵守：

1. 先阅读本文档；
2. 查看 `Docs/PhoneOS/Reference/` 中的目标图；
3. 不要推进 AppHost；
4. 不要迁移 Momotalk；
5. 不要把 Dock 做成厚重白色面板；
6. 不要继续使用文字版底部导航；
7. 不要把 App 图标摆死；
8. 不要删除现有 PhoneAppDefinition / PhoneAppRegistry；
9. 不要引入 WebView；
10. 不要复制 React 项目代码；
11. 只用 uGUI + Sprite + ScriptableObject + Prefab 方式实现；
12. 修改后保持 Play Mode 可运行。

---

## 27. Stage 1.6 建议任务说明

可以将以下内容作为 Codex 的阶段任务：

```text
继续在当前 phoneos-dev 分支上修改，只做 VirtualPhoneOS Stage 1.6 视觉对齐，不进入 AppHost，不迁移 Momotalk、Camera、Debug 真实业务。

目标：
让当前 PhoneOS 桌面更接近 Docs/PhoneOS/Reference/target_home_reference_01.png 的视觉效果。

必须修正：
1. 移除 DockBar 当前厚重白色背景框。
2. Dock 图标直接悬浮在壁纸上，或只使用极轻量透明背景。
3. Dock 整体位置下移，接近参考图底部应用区域。
4. NavigationBar 从文字 Back/Home/Recent 改为图标按钮。
5. NavigationBar 图标使用深灰色或可配置颜色，保证在粉色壁纸上清晰可见。
6. 调整 PageIndicator，使其位于 AppGrid 与 Dock 之间。
7. 调整 BigClockWidget 和 WeatherWidget 的比例与间距。
8. 保持 AppGrid 4 列布局。
9. 保持 App 图标由 PhoneAppDefinition 动态生成。
10. 新增或完善 PhoneOSStyle.asset，用于集中管理颜色、图标尺寸、字体大小、Widget 尺寸。

禁止：
1. 不实现 AppHost。
2. 不打开真实 App。
3. 不迁移 Momotalk。
4. 不迁移 Camera。
5. 不迁移 Debug。
6. 不实现 QuickSettings。
7. 不实现 RecentApps。
8. 不使用 WebView。
9. 不手动摆死 App 图标。

验收：
1. Play Mode 下 PhoneOS 桌面显示正常。
2. Dock 不再是厚重白色大卡片。
3. 底部导航为图标，不是文字。
4. 桌面整体更接近参考图。
5. Momotalk、Camera、Debug、Settings 图标仍正常显示。
6. 点击 App 图标仍只打印 appId。
```

---

## 28. 长期视觉路线

Stage 1.6 完成后，后续视觉路线建议如下：

```text
Stage 2:
  AppWindow 空窗口打开 / 关闭动画

Stage 3:
  SettingsApp 最小版
  支持更换壁纸、时间格式、Dock 显示

Stage 4:
  MomotalkApp 迁移
  联系人列表、聊天页、角色资料页

Stage 5:
  CameraApp / DebugApp 迁移

Stage 6:
  QuickSettings / RecentApps / NotificationCenter

Stage 7:
  多角色、家具、记忆、房间系统以 App 形式接入
```

在任何阶段，PhoneOS 都必须保持：

```text
视觉统一
配置驱动
App 模块化
Prefab 可维护
不写死
不堆色块
```

---

## 29. 最终目标

最终 PhoneOS 应成为 VirtualPartner 的核心交互壳：

```text
用户在 Unity 场景中看到角色和房间
  ↓
打开虚拟手机
  ↓
进入 PhoneOS 桌面
  ↓
点击 Momotalk 与角色聊天
  ↓
点击 Camera 控制场景镜头
  ↓
点击 Debug 查看模型状态
  ↓
点击 Settings 修改壁纸、时间、主题、声音
  ↓
后续点击 Character / Room / Memory / Furniture 等 App 扩展更多功能
```

PhoneOS 的价值不是单纯“好看”，而是让 VirtualPartner 从一个聊天 Demo，升级为一个可长期扩展的虚拟陪伴系统。
