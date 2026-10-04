# PhoneOS 输入法修复（2026-10-04）

用户报告：拼音输入时显示 `<u>…</u>`，并在 TMP_InputField.Append / String.Remove 抛出 ArgumentOutOfRangeException。

原因：构建工具将 TMP_Text.richText 设为 false，却未同步 TMP_InputField.richText（默认 true）。TMP 在组合输入时插入的下划线标签被显示为普通字符；渲染字符索引与实际草稿索引因此错位，选区替换可能越界。

修复：PhoneMessageInputField、PhoneVisualUI 构建入口及 PhoneVisualPolish 统一输入框和文字组件为纯文本。开启 TMP 的原始字符串编辑路径（isRichTextEditingAllowed），避免从渲染字形范围反推选区。LiveStage / VisualStage 的 24 个已保存输入框同步更新；不修改 PackageCache。

验证：Unity 编译通过。PhoneInputReviewChecks.Run 在独立克隆输入框中使用可控 compositionString，7 项通过：富文本状态同步、候选文本无下划线标签、候选不写入草稿、过期候选选区替换、中英文选区替换、用户输入的标签保持字面文本、换行输入。最终 Console 无错误。测试没有发送消息或修改用户草稿。

未改动 Enter／Shift+Enter 的既有发送逻辑，也未改动 LLM、StagePlan、Memory、TTS／ASR。真实 Windows 中文输入法的候选选字、选区替换和 Enter 行为仍需用户实机确认；可控组合输入回归不等同于覆盖所有输入法实现。
