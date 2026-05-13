# Momotalk 视觉参考归档

## 参考来源

- Momotalk BWIKI：https://wiki.biligame.com/ba/Momotalk
- U1805/momotalk：https://github.com/U1805/momotalk
- U1805/momotalk GitHub Pages 预览：https://u1805.github.io/momotalk/
- 访问日期：2026-05-13
- 参考 commit：`2a3e35273892`

## 视觉分工

Stage 2.6 为 Stage 2.7 的 Momotalk UI 外壳准备参考记录、资源清单、生成素材和预览图。资源目标是尽可能贴近参考效果。

- BWIKI 作为手机外框、Loading、联系人首页、黑色手机壳和粉白色应用氛围的主要参考。
- U1805/momotalk 作为聊天页、字体感觉、消息气泡、输入栏和聊天图标的主要参考。
- 聊天页底部只保留单行输入栏：左侧麦克风按钮，中间输入框，右侧图片按钮和发送按钮。
- `phone_frame.png` 只保留 BWIKI 原始手机框效果，不再额外手绘或重做手机外壳。
- 不再输出 `phone_mask.png`，避免裁切辅助图被误当成 UI 素材；裁切范围只在本文档中用坐标记录。
- 预览图将 Loading、联系人页和聊天页内容压进 BWIKI 原手机框的屏幕区，用于检查 2.7 的画面比例和摆放。
- 本阶段不生成 Toki 头像或角色图。角色头像资源后续由用户从角色资源目录提供。

## 屏幕区约定

`phone_frame.png` 为 960x1920。按 BWIKI 页面结构，应用内容区使用以下坐标：

- 内容区左上角：`x=24, y=120`
- 内容区尺寸：`width=900, height=1744`
- 1080x1920 预览图中，手机框左移 `x=60`，内容区实际绘制在 `x=84, y=120`。

Stage 2.7 实现 UI 时，应优先把 Canvas 内容限制在该内容区，再叠加或并列显示手机框资源。

## U1805/momotalk 样式参数

从 U1805/momotalk 的 SCSS 中提取的主要视觉参数：

- 顶栏粉色：`rgb(252, 150, 171)`
- Loading 背景粉色：`#FA94A6`
- 文字深色：`#2a323e`
- 次级文字灰色：`#87929e`
- 角色消息气泡：`#4b5a6f`
- 用户消息气泡：`#4a8ac6`
- 联系人选中底色：`#e1e7ec`
- 输入栏底色：`#eeeeee`
- 边框色：`#cdd3dc`
- 图标灰色：`rgb(189, 189, 189)`

## 输出目录

- `VirtualPartner/Assets/VirtualPartner/UI/Momotalk/Textures/`
- `VirtualPartner/Assets/VirtualPartner/UI/Momotalk/Icons/`
- `VirtualPartner/Assets/VirtualPartner/UI/Momotalk/Fonts/`
- `VirtualPartner/Assets/VirtualPartner/UI/Momotalk/Preview/`

## 图片资源清单

BWIKI 手机 / Loading / 联系人首页方向：

- `phone_frame.png`：BWIKI 原始手机框。
- `loading_bg.png`
- `loading_icon.png`
- `momotalk_peach_mark.png`：从 `loading_icon.png` 上半部分裁出的白色桃子小图标，用于联系人首页标题。
- `phone_shadow.png`
- `contact_home_bg.png`
- `contact_item_bg.png`
- `unread_dot.png`
- `status_badge_bg.png`

U1805/momotalk 聊天页方向：

- `chat_bubble_left.png`
- `chat_bubble_right.png`
- `input_bar_bg.png`
- `reply_card_bg.png`
- `story_card_bg.png`

图标资源：

- `send_icon.png`
- `mic_icon.png`
- `image_icon.png`
- `back_icon.png`
- `more_icon.png`
- `profile_icon.png`
- `heart_icon.png`
- `choice_icon.png`
- `bell_icon.png`
- `add_icon.png`

字体资源：

- `Blueaka.woff2`
- `Gyeonggi_Title_Light.woff`
- `Gyeonggi_Title_Medium.woff`
- `Blueaka.ttf`
- `Gyeonggi_Title_Light.ttf`
- `Gyeonggi_Title_Medium.ttf`

预览图：

- `preview_loading.png`
- `preview_contact_home.png`
- `preview_chat.png`

## 建议由 Unity UI 直接绘制的元素

以下元素在 Stage 2.7 中直接用 Unity UI 绘制更灵活：

- 简单纯色背景。
- 分隔线。
- 遮罩层和半透明覆盖层。
- 不需要九宫格切片的简单圆角矩形。
- 联系人列表和聊天列表的重复布局容器。

## 阶段边界

Stage 2.6 做：

- 记录视觉参考来源。
- 记录 BWIKI 与 U1805/momotalk 的视觉分工。
- 整理资源清单和输出目录。
- 生成或整理 Stage 2.7 所需 UI 美术资源。
- 输出 1080x1920 系预览图用于视觉验收。

Stage 2.6 不做：

- 不实现 Momotalk Unity Runtime 逻辑。
- 不实现右侧滑出行为。
- 不实现联系人切换。
- 不实现聊天记录。
- 不实现 typing indicator 行为。
- 不实现消息发送。
- 不把 U1805 的 Vue 工程作为 Unity Runtime 代码导入。
