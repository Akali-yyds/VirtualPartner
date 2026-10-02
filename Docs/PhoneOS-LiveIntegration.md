# Phone OS live integration

The visual baseline was accepted on 2026-09-08. The daily scene is `VirtualPartner/Assets/Scenes/PhoneOS.unity`; the accepted preview remains `PhoneOS_VisualReview.unity`, and the original scene remains available for regression.

## Implementation

- `PhoneLiveRuntime` is composed by `VirtualPartnerStage1Bootstrap`. Exactly one `MomotalkConversationController` owns history, memory registration, request tracking and subscriptions. The live view reads message/pending snapshots and sends through its view-independent API. Hiding the phone does not disable the bootstrap, relay, player, TTS or FSM.
- `PhoneLiveMomotalk` binds registered contacts and actual avatars, existing histories, per-character persistent drafts, request errors/replacement, unread counts and incremental message rows. It owns its ASR session token; navigation invalidates ownership before cancellation. Late results cannot write into another page or auto-send.
- `PhoneLiveSettings` uses `CreateConfigDraft`, `ReloadConfig`, `StartConfigTest` and `SaveConfig` from the existing relay. No second API/key store exists. Display options extend `PhoneSettingsData`, retain its key/format, and supply defaults for height and ASR auto-send.
- `PhoneLiveCamera` drives the existing ground-pan/orbit/radius/reset APIs using elapsed time. Pointer capture stays in the joysticks. Scene mouse input is disabled in the live scene.
- `PhoneLiveDebug` uses public service operations. StagePlan and bone operations were exposed from the existing panels so both presentations call the same logic; no IMGUI drawing method is used by the phone. Summaries refresh only for the visible page; long diagnostic snapshots open in a scrollable, copyable document view. Applied bone effects remain owned by the runtime panel when the phone app is hidden.

## Debug migration mapping

| Existing functionality | New entry |
|---|---|
| Runtime/service overview | Debug → Overview → Full diagnostics |
| LLM submit/stop, final prompt and raw response | Debug → LLM |
| Load basic/full, paste, validate/play/replace/stop/clear, validation output | Debug → StagePlan |
| Phone open/close/contacts, history folder and unread/request counters | Debug → Momotalk |
| Failure toggle, spatial audio, test text, health, real/warmup/failure test, stop | Debug → TTS |
| Provider/result modes, unavailable/failure toggles, transcript, health/start/cancel, engine/VAD/microphone diagnostics | Debug → ASR |
| Reload, judge last turn, folder, clear decision, raw judge response | Debug → Memory |
| Registered character/profile/runtime information | Debug → Character |
| Enable/disable scheduler, enter/exit interaction, active action/state | Debug → FSM |
| Ground position, orientation, movement/constraint diagnostics, enter/exit interaction | Debug → Root |
| All control bones, constrained axes, overlay, zero/refresh, pin/pair/unpin/clear, selected/pinned export | Debug → Bone |
| Mouth index override/release, all five existing expression tests and clear, speech-mouth diagnostics | Debug → Expression / Mouth |
| All existing API fields, reload/current/test/save, masked key | Settings → LLM connection (linked from Debug) |

## Validation boundaries

`PhoneLiveReviewChecks` exercises real view bindings, persisted-history rendering, draft/app restoration, API draft isolation, validation, camera input, bone persistence and isolated test-character clear/failure semantics. It does not clear Toki data. Its latest result is `Library/MCPForUnity/PhoneLiveReview/checks.txt`.

`PhoneLiveServiceChecks` is an explicit Editor-only localhost fixture. It temporarily changes only the in-memory relay configuration, disables memory registration for the marked fixture turns, and restores both afterward. The fixture tests replacement, StagePlan delivery, real TTS, background unread and real ASR cancellation/late-result handling. Marked integration messages remain in history as test evidence. This is not a claim that the user's external LLM provider succeeded.

The existing provider returned HTTP 401 Invalid API key during the real configuration/request tests. Update Settings → LLM connection, Test, then Save to complete external-LLM acceptance. No key value is printed in test artifacts.

The existing TTS startup script successfully started GPT-SoVITS and the wrapper and prewarmed the Toki voice. ASR initially failed because its old Python 3.13 binary packages were being loaded by Python 3.12. Reinstalled matching wheels in the existing ignored `.venv` (numpy 2.4.5, sherpa-onnx 1.13.2, sounddevice 0.5.5 and dependencies). ASR engine/model/VAD/microphone health now reports ready. Service source, voice/model assets and ModelRepairTool were not changed.

Manual checks still required: native Chinese IME candidate selection; microphone transcript accuracy with the user's speech; preferred joystick speed; external LLM → StagePlan → TTS after valid credentials are supplied. Current screenshots and detailed test outcomes live under `Library/MCPForUnity/PhoneLiveReview`.


## Final verification record (2026-09-09)

- Live UI and action-binding pass: **451 passed, 0 failed**. Includes every live page's static labels and checks that no button remains wired to a preview service action.
- Explicit local HTTP fixture pass: **11 passed, 0 failed**. LLM generation was controlled; StagePlan validation/playback, GPT-SoVITS audio, real ASR start/cancel and conversation lifecycle were real. Fill/auto-send/late-result assertions used deliberately injected transcripts; microphone recognition accuracy is not implied.
- Invalid-JSON testing intentionally produced two existing validator Console errors. No unexpected runtime/compilation errors were observed. The earlier startup assertion was corrected to wait for the bootstrap's staged initialization; the FSM was active and running.
- External provider test: HTTP 401 / Invalid API key. No credential was changed or exposed. Original relay configuration restored after the localhost fixture. The fixture server has been stopped.
- The live scene is saved in Edit Mode; background execution is enabled while the live runtime exists and restored on teardown. PhoneOS is the enabled first build scene; SampleScene is retained but disabled in build settings.
- TTS and ASR services remain running for manual acceptance. ASR's default microphone input stays open while its service runs, per the existing service design.

Changed existing runtime sources: MomotalkConversationController, VirtualPartnerStage1Bootstrap, StagePlanDebugPanel, VirtualPartnerBoneDebugPanel, VirtualSceneCameraController and PhoneSettingsData. Live phone components/builders/checks are new files. Shared phase-1 shell/app navigation was extended; pre-existing legacy Momotalk UI modifications were retained.
