# Phone OS navigation and recent tasks

Implemented 2026-10-01 on the accepted PhoneOS visual baseline.

## Behavior and ownership

- Bottom navigation now exposes Recent / Home / Back. Recent toggles an overview; Back restores its origin, or Home if that task was removed. Home always leaves the overview for the launcher.
- App instances remain cached independently of the MRU task list. Removing a task resets its page/history/scroll, releases its preview, and removes its linked-return context. It does not destroy the app or stop conversation/StagePlan/TTS/character services.
- `PhoneAppHost` exposes `RecentTasks`, `TasksChanged`, `OverviewChanged`, `IsOverviewOpen`, `NavigationPending`, `ShowOverview`, `ExitOverview`, `ToggleOverview`, and `DismissTask`. `IPhoneTaskReset` is an optional view reset contract; `IPhoneApp` remains compatible.
- Navigation away from a visible app completes after a rendered frame so the screenshot matches the actual app viewport. Callers that need the destination view must wait for `NavigationPending == false`. Competing navigation requests use the latest destination. Input focus and owned microphone capture are released before departure.
- Snapshots crop only the app viewport, live in memory, and are replaced on departure. Settings uses a private placeholder for the entire app, including when the key has been revealed. Tasks and previews do not survive a new run.
- Recent cards support horizontal drag/snap, click-to-restore, upward dismissal, and an explicit close button. Empty state links to Home. Explanatory text states that replies/audio/character actions continue.
- The shared shell installs `PhoneRecentTasksView` at runtime, upgrading existing saved live/preview scenes without regenerating or overwriting them. The generator also wires the enabled Recent button for future builds.
- Existing UI style, fonts, physical frame and main business chain remain intact. No changes to ModelRepairTool, GPT-SoVITS or ASR service implementations.

## App/control review

| Area | Result |
|---|---|
| Recent / Home / Back / Esc | Implemented; cached restore, origin fallback, repeated navigation and linked-return regression passed |
| Recent card gestures | Horizontal drag does not dismiss; upward drag dismisses; collapse resets an active drag; scroll snaps between cards |
| Outside-phone blocker | Existing full-screen consuming blocker retained; task dragging does not call its click action |
| Momotalk search, contacts, send, mic, clear/history/memory | Existing real bindings retained and regression checked; drafts survive dismissal; overview is not a visible conversation |
| Unread | Home and Dock badges plus recent card badge use the existing controller's unread total; original entry dot retained |
| Settings display/API controls | Existing live bindings retained; unsaved API fields survive dismissal; key re-masked on display/lifecycle changes; test status resumes |
| Camera pan/orbit/zoom/reset | Existing controller retained; overview releases joystick; reopening synchronizes zoom; dismissal preserves camera position |
| Debug categories and editor actions | Existing control mapping retained; text edits and bone pin effect survive dismissal; current-page diagnostics continue to refresh |
| Debug ASR | Added explicit ownership cancellation on suspension/page departure; late transcript cannot submit |
| Desktop weather/search | Remain explicit placeholders; no new external service or pretend functionality |

Detailed Debug category mapping is in `PhoneOS-LiveIntegration.md`.

## Verification

Unity 6000.3.12f1, daily scene `Assets/Scenes/PhoneOS.unity`.

- Final navigation checks: **163 passed, 0 failed**. Includes MRU uniqueness/order, cache retention, draft reset boundaries, private previews, texture release, linked Back, input capture, ASR cancellation/late-result, and one conversation owner.
- Final original live UI regression: **451 passed, 0 failed**. Per-label checks vary with currently populated content. Expected invalid-JSON tests deliberately emit the two existing StagePlan validator errors; these are not unexplained runtime failures.
- Controlled service fixture: **11 passed, 0 failed**; separate results in `Library/MCPForUnity/PhoneLiveReview/service-checks.txt`. Uses localhost LLM output and the real StagePlan pipeline, real GPT-SoVITS playback and real ASR start/cancel. It checks task dismissal plus phone collapse while a reply is pending. Transcript fill/auto-send checks inject labeled results; they do not measure microphone accuracy.
- Real configured provider: **HTTP 401 / Invalid API key**. Credentials were not changed or printed. External-provider end-to-end acceptance remains blocked until valid credentials are saved.
- Resolution captures: 1280×720, 1920×1080, 2560×1440, 3440×1440, each at phone height 70%, 90%, 95%. Frame bounds checked in all twelve cases; actual baseline, smallest and ultrawide screenshots inspected. A stale-frame screenshot issue during resolution changes was fixed before final captures.
- New tests: `PhoneNavigationReviewChecks.Run()` in Play Mode. Existing `PhoneLiveReviewChecks` now waits for queued navigation and expects one Back for linked Settings. `PhoneLiveServiceChecks` additionally dismisses the Momotalk task while the reply is pending.

Screenshots and navigation results: `VirtualPartner/Library/MCPForUnity/PhoneNavigationReview/` (generated, ignored artifacts).

Key screenshots: `recent-apps.png`, `recent-apps-phone.png`, `empty-phone.png`, `restored-debug-phone.png`, plus the twelve `recent-<resolution>-<height>` captures.

## Remaining human acceptance

- Native Chinese IME candidate selection and Enter/Shift+Enter on the user's keyboard.
- Actual microphone transcription accuracy (not injected transcript correctness).
- Preferred card drag/snap feel and camera joystick speed.
- Real external LLM → StagePlan → TTS / character execution after resolving the current 401.

The fixture temporarily replaces only the in-memory LLM config and disables memory registration for marked test turns, restoring both in finally. Clearly labeled fixture messages remain in chat history as evidence. This does not establish real-provider success.


Final cleanup: Unity left in Edit Mode with no scene save or scene regeneration. The fixture server and TTS/ASR processes started for this run were stopped; MCP remains running. Final navigation run reported 163 passed / 0 failed and Console returned zero error entries. The button binding inventory contains 148 entries in control-inventory.tsv (dynamic contacts/bones are explicitly labeled).
