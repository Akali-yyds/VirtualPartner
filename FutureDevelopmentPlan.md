# VirtualPartner Future Development Plan

更新时间：2026-05-25

本文记录暂不进入当前开发主线、但值得保留的未来方向。当前主线以 `LlmRelay` prompt 工程为准。

## 近期候选

### 1. 对话级日志文档

为 Momotalk / LlmRelay 增加一次对话一份日志：

- 用户输入。
- Prompt 组成摘要。
- 请求开始/结束时间。
- LLM 原始输出。
- JSON 解析和 validator 结果。
- StagePlan 播放结果。
- TTS / speech / memory 相关状态。

用途是定位“慢、动作少、示例锚定、preset 偷懒、JSON 不稳定”等问题。

### 2. 长动作 Prompt 专项

针对“跳舞 / 表演 / 展示动作 / 连续动作”建立更明确的 prompt 规则：

- 要求多 stage。
- 优先使用参数 `bonePose`。
- preset animation 只能作为辅助点缀。
- 不允许用 greeting / scissors 等无关预设动作冒充舞蹈。
- 示例仍只作为格式参考，不提供可复制舞蹈答案。

### 3. Runtime 能力摘要继续瘦身

继续检查 Runtime Generated Capabilities 和静态 prompt 的重复：

- 保留运行时真源。
- 去掉重复解释。
- 保证模型知道可用能力，但不被能力表淹没。

## 中期候选

### 1. AgentLoop 重新评估

Stage 3 AgentLoop 暂停后，未来可以重新评估，但必须先回答：

- 角色控制 Agent 的最小可见输出单位是什么。
- 如何保证用户在等待期间能持续看到 speech / 动作反馈。
- 如何让模型分段提交动作，而不是一次性生成长 JSON。
- 如何严格处理协议错误，不做文本兜底或静默降级。
- 如何避免模型用 preset 动作逃避参数动作生成。

在这些问题没有设计清楚前，不重启 Momotalk 正式入口迁移。

### 2. 动作质量评估

可以重新设计轻量 MotionCritic，但它应该服务 `LlmRelay` 或未来 Agent，而不是替代模型生成动作：

- 发现过短。
- 发现重复。
- 发现 preset 冒充复杂动作。
- 发现缺少协同。
- 只给 warning / suggestion，不自动改写。

### 3. Memory 体验优化

当前只新增了 Clear Memory。未来可以再考虑：

- 按角色查看记忆摘要。
- 按类别清除记忆。
- 更透明地显示记忆是否注入 prompt。

## 暂不做

- 不恢复旧 Timeline 1.0 主链路。
- 不把固定动作模板库作为主动作生成方式。
- 不把 AgentLoop 作为当前产品入口。
- 不新增用户不可理解的 Debug 信息到普通 Momotalk UI。
- 不用兜底规则掩盖 LLM 协议或 JSON 生成错误。
