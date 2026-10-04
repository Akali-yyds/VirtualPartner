# PhoneOS 第三阶段第二步：页面统一与链路验收

日期：2026-10-03。状态：页面实现已交付，自动回归通过；真实移动场景存在已定位失败，人工体验尚待确认。

## 实现范围

- Settings：分组首页；常用 API 字段直接显示，Advanced 默认折叠；Test / Save、次级加载操作和可复制详情；覆盖未保存编辑前确认，Back 优先关闭弹层。高级区域随 Home 保留、随任务移除折叠，草稿保留；密钥重新进入遮蔽。
- Camera：上下两个 168 设计单位的圆形摇杆，Pan、Rotate、Zoom 和 Reset 一屏显示；保留原控制器、归一化输入、持续输入及取消语义。
- Debug：三个分组、原有 13 个首页入口及页面编号；参数、操作、停止／释放和折叠测试工具区；完整诊断和错误可滚动、复制；只读文档禁用 Paste，返回骨骼页面同步真实轴值。
- Momotalk：真实头像、联系人摘要、最近消息时间和独立未读计数；区分无角色与无搜索结果；角色详情及会话管理统一样式，清理操作有角色专属确认弹层。
- 共用主题增加分隔线与圆形资源，共用页面、折叠和弹层组件用于构建工具和 prefab 定向升级。未重建角色场景。

主链路仍为 Momotalk → LlmRelay → StagePlan 2.0 → Validator → Player。未更改 FSM、动作执行器、Memory 数据格式、TTS／ASR 服务实现或 ModelRepairTool。

## 控件映射

[完整控件表](PhoneOS-PageControls.tsv) 包含各页序列化回调和新增运行时绑定。升级前后序列化方法计数一致：Momotalk 15、Settings 14、Camera 1、Debug 95；无旧回调缺失。

| 页面 | 核对重点 |
|---|---|
| Settings | Display、壁纸、时钟、语音自动发送；所有 API 字段、Advanced、Test、Save、Load current、Reload file、详情和覆盖确认 |
| Camera | Pan / Rotate 指针捕获、松手回中、Zoom、Reset、离开停止 |
| Debug | Overview、LLM、StagePlan、Momotalk、TTS、ASR、Memory、Character、FSM、Root、Bone、Expression / Mouth、API 跳转 |
| Debug 动态内容 | 骨骼选择列表、轴值、固定／释放、JSON 编辑、只读诊断、复制／粘贴、验证／播放／替换／停止／导出 |
| Momotalk | 动态联系人、搜索、时间、未读、聊天、详情、麦克风状态和取消、独立历史／记忆清理确认 |

Settings 的加载按钮和 Momotalk 清理按钮在运行时更换为带确认的处理器；原服务方法仍负责实际操作。移除任务不会停止已发送请求、语音播放或调试效果。

## 自动及运行验证

| 检查 | 结果 | 范围 |
|---|---|---|
| PhoneLiveReviewChecks | 367 通过、0 失败 | 页面文字、业务绑定、草稿、配置编辑、真实镜头、骨骼效果、JSON 验证、隔离历史清理 |
| PhoneNavigationReviewChecks | 159 通过、0 失败 | 三键、最近任务、快速导航、来源返回、任务缓存／移除、ASR 迟到结果、尺寸 |
| PhonePagesReviewChecks | 336 通过、0 失败 | Advanced、覆盖确认、弹层优先返回、分组映射、只读文档、搜索空状态及完整尺寸组合 |
| PhonePolishReviewChecks | 25 通过、0 失败 | 已接受桌面、聊天、最近任务和动效回归 |
| PhoneLiveServiceChecks | 14 通过 | 本地受控 LLM + 真实 TTS／ASR；配置后台测试／隔离保存重载、请求替换、未读、后台播放、取消／填入／自动发送 |

页面矩阵覆盖 1280×720、1920×1080、2560×1440、3440×1440，各自 70%／90%／95%。固定控件检查覆盖每页；滚动内容通过实际页面遍历、文本检查及截图复核，几何断言不等于所有控件的人工点击验收。

应用测试刻意产生无效 StagePlan JSON 的 Validator 错误；真实场景存在原有移动边界／障碍警告。编译及页面运行未发现新增 UI Console 错误。配置保存重载使用隔离文件；跨整个 Unity 进程重启的持久化验证尚未单独执行。

测试项数量不同于上一阶段：移除了说明文字、折叠了测试控件，当前可见文字断言数量随布局变化；不是旧回调被删除。

## 真实 DeepSeek 验收

使用用户提供的密钥、官方端点、`deepseek-chat`、JSON 格式和兼容消息角色。密钥由当前会话的原始用户消息读取，经一次性本机内存通道传入 Unity；没有再次打印或写入源码、配置文件、报告及截图。

测试临时替换内存配置，使用同一 MomotalkHistoryStore 的隔离目录；同时解除会话记忆写入和 Relay 长期记忆上下文引用。正常 Momotalk Send 路径、原 Validator／Player 和真实 GPT-SoVITS 执行。测试后恢复配置、历史存储、记忆引用及聊天草稿；字节比对确认用户原始聊天文件未改变。

| 场景 | 结果 | 实际证据 |
|---|---|---|
| 中文问候 | 通过 | 请求完成，StagePlan 验证通过，speech 完成，GPT-SoVITS 播放事件 |
| 基础微笑 | 通过 | expression + speech 完成，观测到 smile 状态和真实播放 |
| 简单骨骼姿态 | 通过 | bonePose + speech 完成，动作期间观测到最大约 48.12° 局部骨骼旋转变化 |
| 短距离移动／朝向 | **失败（部分执行）** | JSON 合法；speech 与 facing 完成，约 179.9° 转向；locomotion 失败，角色未产生要求的位移 |
| 回复期间切换／收起／移除 | 通过 | 最后一场景中移除 Momotalk 并收起，回复和真实语音继续，未读增加 |
| 真实麦克风启动和取消 | 通过 | 收音会话启动，进入最近任务取消；受控注入的迟到结果未写入或发送 |

移动的具体失败原因：`Root clearance would overlap ObstacleArea 'Stage7_ObstacleArea_Table'.` 首轮也观测到房间边界限制。未更改碰撞边界或用预设动作代替移动；该结果保留为现有场景移动约束待处理项。模型动作自然程度、复杂动作质量不计为本轮自动通过。

## 证据与复现

本地证据目录：`VirtualPartner/Library/MCPForUnity/PhonePagesReview/`。

- `settings-*.png`、`camera-*.png`、`debug-*.png`、`momotalk-*.png`：Unity 实际截图；`settings-confirm.png`、`contacts-no-results.png`：弹层和空状态。
- `remaining-pages-walkthrough.mp4`：实际运行页面遍历录屏，按采集时间戳编码；无音轨，不代表声学质量验收。
- `real-0.png` 至 `real-3.png`、`real-plan-*.json`、`real-chain.txt`：真实链路证据；截图仅记录已捕获到的执行时刻。
- `checks.txt`、`control-mapping.tsv`、`persistent-bindings.txt`：专项检查与映射。
- `PhoneLiveReview/checks.txt`、`service-checks.txt` 和 `PhoneNavigationReview/checks.txt`：相邻目录中的回归记录。

Play `Assets/Scenes/PhoneOS.unity`，等待运行时初始化后通过 Editor 检查类 `Run()` 执行相应测试。受控服务需先运行 `Tools/PhoneOS/controlled_llm.py`（127.0.0.1:18767）并启动现有 TTS／ASR。真实检查用 `PhoneRealChainChecks.ReceiveAndRun()`，45 秒内向 127.0.0.1:18769 POST 临时密钥；仅在空闲测试运行时显式执行，不保存密钥。

## 人工验收及剩余事项

- 完整页面的字体、层次和视觉接受度。
- 中文输入法选字、Enter／Shift+Enter；真实麦克风识别准确率与音频听感。
- 上下摇杆、最近任务手势与不同手机高度的主观手感。
- 桌子附近与房间边界的可通行区域、角色起点和移动约束需另行检查。
- 测试通过不等于最终人工验收；本轮没有宣称真实移动场景全通过。

结束状态：Unity 已退出 Play Mode；本次启动的 TTS、ASR 和本地 LLM 测试服务已停止。原有 8317 服务保留。字体资源仅产生的运行时字形缓存变动已去除，仍使用原有动态字体与中文回退。
