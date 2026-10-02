# Phone OS visual review — phase 1

This delivery implements the visual and interaction acceptance gate agreed on 2026-09-08. **It is not the live feature migration.** The preview ribbon is present on every screen. Service actions cannot submit LLM requests, record microphone audio, play TTS, change character poses, or delete live history/memory.

## Open and review

Open `VirtualPartner/Assets/Scenes/PhoneOS_VisualReview.unity`, enter Play Mode, and click the small phone button on the right. The phone starts collapsed. Use the desktop icons to open Momotalk, Camera, Settings and Debug. Click outside to collapse, Home to return to the desktop, or Back/Esc to navigate backwards.

- Settings → Display changes phone height (70–95%), wallpaper, clock format and Dock immediately.
- Momotalk → Toki shows explicitly labeled sample messages. Sending appends a preview message without contacting any service. Drafts use a separate preview preference key. The microphone opens an explanatory preview page and does not record audio.
- Camera has two pointer-capturing joysticks with local-coordinate normalization and immediate release on collapse, page change or focus loss. Its slider and Reset are interactive; scene camera integration belongs to phase 2.
- Debug has category/detail navigation, editable parameter controls, a full-page text/JSON editor, and clipboard copy/paste. Service command buttons report that no operation was performed.
- Debug → API configuration links to Settings; Back through the Settings root returns to the originating Debug page.

Generated assets are under `Assets/VirtualPartner/UI/PhoneOS/VisualStage`. They are normal serialized Unity prefabs: inspect layout, typography, input fields, persistent button bindings and page hierarchy directly in the Editor. Runtime code handles navigation, content and layout measurement; it does not rebuild these page trees in Awake/OnValidate.

The existing physical frame and wallpapers are reused. Noto Sans Regular/Light/SemiBold and Noto Sans CJK SC provide TMP typography; font license files are alongside the source fonts. Icons are original UI vector geometry in `PhoneGlyph`, not Unicode placeholders.

### Source entrypoints

- `Assets/VirtualPartner/Runtime/PhoneOS/Core/PhoneAppHost.cs`: cached app instances, visibility lifecycle and cross-app return.
- `Assets/VirtualPartner/Runtime/PhoneOS/UI/PhonePresentationShell.cs`: phone entry, sizing, outside-click blocker, clock, navigation and isolated preview preferences.
- `PhonePreviewApp.cs`, `PhoneChatPreview.cs`, `PhoneMessageInputField.cs`, `PhoneTextBubble.cs`: page history, sample chat, submit/IME boundary and measured bubble layout.
- `PhoneJoystick.cs`, `PhoneSwitch.cs`, `PhoneGlyph.cs`, `PhonePreviewSettings.cs`, `PhonePreviewAction.cs` in the same UI directory: input controls, icons and preview-only actions.
- `Assets/VirtualPartner/Editor/PhoneVisualStageBuilder.cs`, `PhoneVisualStageApps.cs`, `PhoneVisualStageDebug.cs`, `PhoneVisualUI.cs`: editable source of the generated visual assets.
- `Assets/VirtualPartner/Editor/PhoneVisualReviewChecks.cs`: Play Mode checks and screenshot capture.

Chat submission uses TMP submit events. Pasting multiline text preserves newlines without sending, Shift+Enter uses newline mode, and active/recent IME composition suppresses submission. Windows IME candidate selection and physical keyboard feel still need a manual pass in Play Mode; the automated submit tests do not claim to reproduce a full native IME session.

## Building and checking

`VirtualPartner → Phone OS → Build Visual Review` regenerates the preview prefabs and review scene. This **overwrites edits to the generated preview assets**, so move accepted Inspector adjustments into the editor builder before regenerating. It does not overwrite SampleScene or the pre-existing Momotalk prefab changes.

In Play Mode, `VirtualPartner → Phone OS → Run Visual Review Checks` checks navigation, session state, cross-app return, joystick input, phone bounds and bubble layout; it captures the Game View into `Library/MCPForUnity/PhoneVisualReview`. The checks use 1280×720, 1920×1080, 2560×1440 and 3440×1440 at 70%, 90% and 95% height. Additional baseline screenshots cover contacts, display, API form, Camera, Debug, bone controls and JSON.

Visual acceptance still requires the user's review of actual screenshots and Play Mode text/input/joystick feel. The automated check output is evidence of the conditions it explicitly checks, not proof of aesthetic acceptance or live-service behavior.

Latest completed full check run: **466 passed, 0 failed**. This includes all-page label height checks, multiline paste/submit event checks, JSON return-source navigation, app/scroll/draft restoration, joystick release and the 12 resolution/height combinations. Unity Console had no errors after generation and the completed run. Captured overview: `Library/MCPForUnity/PhoneVisualReview/overview.png`; detailed results: `Library/MCPForUnity/PhoneVisualReview/checks.txt`.

A subsequent focused Play Mode check confirmed that collapsing the phone deactivates a focused chat input and clears EventSystem selection (`focus-check.txt`). The editor was left in the saved review scene in Edit Mode; background rendering was restored to false. SampleScene and the live relay/conversation controller files have no changes from this implementation.

## Migration inventory for phase 2

The existing active chain remains `Momotalk → LlmRelay → StagePlan 2.0 → Validator → Player`. Parameter bonePose remains part of that chain. No ModelRepairTool, TTS server or ASR server files are changed.

| Existing functionality | New destination | Phase 1 / phase 2 boundary |
| --- | --- | --- |
| Phone host lifecycle | Shared PhoneAppHost | Cache instances; pause/resume visibility; cross-app return implemented. Live conversation lifecycle still needs separation from old views. |
| Contacts, messages, drafts, unread | Momotalk | TMP preview pages and isolated preview draft storage; bind real character/history/request events in phase 2. |
| LLM replacement, failure, background replies | Momotalk | Preview has no relay reference; migrate existing controller and request registry in phase 2. |
| Chat clear / memory clear | Contact details, separate confirmation pages | Preview only; bind distinct existing clear methods later. |
| API draft, load current, reload, test, save | Settings → LLM connection | All form fields presented; bind existing LlmRelay configuration methods without a second credential store. |
| ASR fill / auto-send and cancellation | Settings → Voice input / Momotalk | Preview option and voice page; real recognition lifecycle and late-result guards remain phase 2. |
| Orbit, ground pan, zoom, reset | Camera | Controls and release behavior implemented; bind controller APIs later. |
| Runtime overview and character bindings | Debug → Overview / Character | Sample status presentation; real snapshots later. |
| LLM submit, stop, prompt/response copy | Debug → LLM | Preview command layout and text editor; extract existing service commands later. |
| StagePlan basic/full, paste, validate, play, replace, stop, clear, results | Debug → StagePlan | Page and command layout; same existing validator/player in phase 2. |
| Open/close chat, show contacts, history folder | Debug → Momotalk | Preview commands; route to new app and existing storage APIs later. |
| TTS failure mode, 3D audio, test text, health, real/warmup/failure tests, stop | Debug → TTS | UI preview; use the same existing TtsManager commands later. |
| ASR provider/result mode, unavailable/failure, mock text, health, start/cancel, status | Debug → ASR | UI preview; use existing AsrManager later. |
| Memory reload, judge, folder, clear decision, raw result | Debug → Memory | UI preview and text editor; same MemorySystem later. |
| Scheduler enable/disable, enter/exit interaction | Debug → FSM | UI preview; extract existing scheduler calls later. |
| Root status and enter/exit interaction | Debug → Root | UI preview; existing orientation/locomotion APIs later. |
| Bone selection, overlay, refresh, bounded axes, zero, pin pair/selected, unpin/clear, exports | Debug → Bone | Sample list and angle controls; populate real bone map/ranges and export actions later. |
| Mouth index apply/release; expression controls and clear | Debug → Expression / Mouth | UI preview; bind existing executor/driver methods later. |

During phase 2, extract non-UI debug commands/state from IMGUI so both the new app and the developer-only regression panels share logic. Do not invoke old DrawEmbedded methods from the new UI. Complete command-by-command parity checks against the current source before replacing the daily entrypoints.

## State and compatibility

The review scene copies the existing scene and disables only its old PhoneRoot, MomotalkCanvas and SceneCameraControlCanvas roots. **VirtualPartnerBootstrap stays enabled**: it initializes and advances idle animation, StagePlan, locomotion and the autonomous FSM. Preview service isolation is implemented by the new phone's preview-only buttons, not by stopping the character runtime. SampleScene and its existing scene wiring remain intact. Preview settings and drafts use `VirtualPartner.PhoneOS.VisualPreview.*` keys, never live settings/config/history/memory keys.

Correction after the initial visual delivery: disabling VirtualPartnerBootstrap had unintentionally frozen the character in the review scene. The scene and generator now restore it, and the review checks assert that both the runtime and autonomous FSM are active. The original FSM behavior profiles and runtime code are unchanged.

Targeted Play Mode verification after this correction: bootstrap initialization succeeded; idle time advanced; FSM started, completed a natural action, and moved the character from `(0,0,0)` to approximately `(-0.9663,0,1.5905)`. Console contained no errors. Evidence is recorded in `Library/MCPForUnity/PhoneVisualReview/character-runtime-check.txt`. The scene was left saved in Edit Mode with VirtualPartnerBootstrap active. The earlier 466 UI-check result predates the two additional runtime assertions; it is not a claim that those new assertions were part of that earlier run.

Pre-existing user changes to the six legacy PhoneOS Momotalk scripts, Momotalk theme/prefab, and VFX settings were retained. The existing file changed by phase 1 is PhoneAppHost; all other phone presentation files/assets are additions. TextMeshPro essential resources were imported to support TMP in this project.

Phase 2 starts only after the user accepts the visual direction. The complete final acceptance must additionally cover the real LLM/StagePlan/TTS/ASR chain, service failures, request replacement, data clear semantics, background behavior, configuration persistence and full Debug parity.


## Android in React density revision (2026-09-08)

Measured the live reference at https://android.blueedge.me/: launcher labels are 12.8 CSS px and the vertical clock is 60 CSS px. The Unity review keeps its physical frame and 90% default height; internal logical metrics changed instead of reducing the rendered phone or changing resolution.

- Launcher icons: 64 → 52; labels: 16 → 13; main clock: 88 → 60. Desktop now uses the reference's weather-card / vertical-clock / secondary-clock composition and an unboxed Dock. Both digital clocks and the analog hands use local time. Weather and search remain explicitly marked previews.
- Chat body: 20 → 15; conversation toolbar: 90 → 60; composer: 94 → 60. Bubble horizontal padding: 16 → 10; timestamp is compact, with the global ribbon identifying sample data. All five bilingual sample messages now fit together.
- Standard app toolbar: 94 → 68; settings/debug rows: 80 → 60; form cards: 114 → 86. Labels and secondary text were reduced consistently; camera joystick hit areas remain unchanged.
- Fonts remain distributable Noto Sans with Chinese fallback; Momotalk keeps its pink brand and real Toki portrait. No live service integration or FSM behavior changes were made in this revision.

Actual Game View captures are in Library/MCPForUnity/PhoneVisualReview. The final density pass passed 477 checks with 0 failures, including active bootstrap/FSM, five-message density, short Chinese, long unbroken English, long Chinese and multiline bubble layout. Console contained 0 errors. Results are in checks.txt. Native IME operation, small-window readability and visual preference still require human review.
