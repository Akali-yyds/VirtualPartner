# Phone OS — representative-page polish review

Status: first visual checkpoint of phase 3. The user must review the Unity screenshots and motion before the remaining app pages are redesigned.

## Delivered scope

- One live clock/date and four labeled Dock apps. Search, weather, duplicate clocks and duplicate launcher grid are hidden. The physical frame and scene objects remain intact.
- Dock is always available; the old `showDock` field still deserializes, but its UI switch is hidden and cannot remove the only application launchers.
- Shared `PhoneVisualTheme` resource: Noto Sans with existing Chinese fallback, readable ink/muted colors, pink Momotalk accent, separate control/bubble/card radii, and transition timings.
- Material Symbols Rounded raster assets rendered at high resolution from Google's official variable font. License is included beside the assets. Four application icons use distinct original compositions/colors; Settings and Debug incorporate the licensed symbols.
- Chat body/input typography, bubble spacing and backgrounds. Errors expose a short state and a scrollable/copyable details page; Back closes details before leaving chat.
- Recent tasks use standalone previews, a one-time dismissible explanation, inertial horizontal navigation, snap, upward dismissal, return and neighbor movement. Settings never supplies a thumbnail texture.
- `PhoneTransitionCoordinator` owns presentation only: icon-to-app, app-to-icon, app-to-card and card-to-app motion, temporary masks, and cancellation cleanup. Rapid interruption preserves the current visual rectangle and retains the last complete thumbnail instead of capturing a partial animation.

## Changes and compatibility

Runtime changes are concentrated in `PhoneAppHost`, `PhonePresentationShell`, `PhoneRecentTasksView`, `PhonePreviewApp`, `PhoneTextBubble`, `PhoneGlyph` and `PhoneLiveMomotalk`. New runtime files are `PhoneVisualTheme`, `PhoneVisualPolish`, `PhoneTransitionCoordinator` and `PhoneMessageDetails`.

`PhonePolishAssets` imports assets and applies an idempotent targeted update to the existing live/preview shell, Momotalk and Settings prefabs. `PhoneVisualStageBuilder` applies the same theme when generating future content. Existing serialized events and service owners are retained. The daily scene was saved after the targeted update; no scene replacement was performed.

New review entry point: `PhonePolishReviewChecks.Run()` in Play Mode. Existing navigation and live UI regressions remain available. Review messages are UI-only, explicitly labeled, and are not sent to LLM, persisted in history, or registered with memory.

The LLM → StagePlan → Player chain, character FSM, history/memory formats, TTS and ASR service implementations are unchanged. Existing user modifications are retained. No API credentials were written to source, assets, documentation or screenshots.

## Control mapping at this checkpoint

| Surface | Behavior |
|---|---|
| Dock Momotalk / Camera / Settings / Debug | Existing app definitions and cached instances |
| Recent / Home / Back | Existing Host navigation, new presentation transitions |
| Recent card / close / upward drag | Restore task / reset view and release task preview; services continue |
| First-use OK | Persist only the notice dismissal flag |
| Chat send / microphone / input / unread | Existing live handlers retained |
| Error details / Copy / Back | View original error / copy / close details |
| Display Show Dock | Hidden intentionally; legacy field accepted |
| Remaining app controls | Existing behavior retained; detailed visual redesign deferred |

## Evidence and acceptance boundary

Generated runtime screenshots, review checks and recording frames are under `VirtualPartner/Library/MCPForUnity/PhonePolishReview/`. These are ignored build artifacts. Recording frames include wall-clock timestamps; video export must preserve their durations rather than pretending every captured frame was rendered at 30 fps.

The first checkpoint covers desktop, live/sample chat, recent tasks, empty state, private Settings preview and representative resolution/phone-size combinations. It does not establish complete visual acceptance of Settings, Camera, Debug or every secondary Momotalk page.

After visual confirmation: propagate the accepted style to remaining pages, run the complete resolution and behavior matrix, then exercise the real external LLM → StagePlan → TTS/character chain with temporary configuration. DeepSeek previously passed only a minimal authentication/request probe; this checkpoint does not claim real-service end-to-end success. Native IME, microphone accuracy and preferred gesture/camera feel remain manual acceptance items.

## Results from the representative-page checkpoint

- Representative-page checks: **25 passed, 0 failed**. Includes actual transition bounds, interruption cleanup, error details, task restore, Settings privacy and nine additional resolution/size combinations.
- Navigation regression: **163 passed, 0 failed** after the transition changes.
- Existing app regression: **449 passed, 0 failed** in the final run; label-count totals depend on visible content.
- Unity compilation succeeded. The final live regression emitted exactly the two expected invalid-JSON validator errors; no additional runtime errors were present.
- Export: `phone-navigation.mp4`, 7.68 seconds from 134 actual Game View frames. Wall-clock durations are preserved in a 30 fps video container; the capture itself is approximately 18 fps and is not a frame-rate benchmark.
- `comparison.png` compares the archived September 8 visual-review desktop with the new live desktop, sample chat and recent tasks. The older image includes its original preview ribbon and a different wallpaper.
- Full-size screenshots include `home.png`, `chat-live.png`, `chat-sample.png`, `recent-chat.png`, `recent-private.png`, `recent-apps.png`, `empty.png`; corresponding `-phone.png` files show the phone crop.

Review priority: desktop whitespace and icon scale; Chinese/English readability and message spacing; task-card proportions; app/card transition feel. The next implementation stage remains gated on the user's visual confirmation.
