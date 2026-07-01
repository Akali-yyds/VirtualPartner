# VirtualPhoneOS Stage Status

Last updated: 2026-07-01

## Current Stage

VirtualPhoneOS has entered Stage 5 - Momotalk Static UI Completion.

The current implementation is a uGUI-based virtual phone shell. It keeps the real Momotalk, SceneCamera, and Runtime Debug business systems untouched, provides a PhoneOS home screen plus AppHost windows, replaces Settings with a minimal real SettingsApp, replaces the Momotalk placeholder with a minimal PhoneOS-native MomotalkApp shell, and is now completing Momotalk's static UI structure.

## Completed

### Stage 1 - Static Home

Stage 1 static Home is complete.

- `PhoneRoot.prefab` owns the virtual phone shell.
- Home uses a `PhoneDevice / PhoneFrame / PhoneScreenViewport` structure.
- Home contains StatusBar, SearchWidget, BigClockWidget, WeatherWidget, AppGrid, PageIndicator, DockArea, and NavigationBar.
- App icons are generated from `PhoneAppDefinition` through `PhoneAppRegistry`.
- App icons are not manually placed in the scene hierarchy.
- Home still uses placeholder/programmatic uGUI visual styling and early PhoneOS sprites.

### Stage 2 - Empty AppHost

Stage 2 empty AppHost is complete.

- `PhoneAppHost` opens one foreground app at a time.
- `AppWindowContainer` hosts the instantiated app prefab inside the phone screen viewport.
- `PhoneOSController` routes Back, Home, and Recent actions.
- Back closes the current empty app window when the app does not handle Back.
- Home closes the current empty app window and returns to Home.
- Recent only logs for now.
- At Stage 2 completion, Momotalk, Camera, Debug, and Settings used empty app prefabs.

### Stage 2.5 - Shell Closeout

Stage 2.5 shell closeout is complete.

- PhoneOS shell, PhoneFrame, PhoneScreenViewport, HomeLayer, AppLayer, and NavigationBar are treated as stable shell structure.
- Back/Home behavior closes the foreground app and returns to Home.
- Recent remains a log-only placeholder.

### Stage 3 - Minimal SettingsApp

Stage 3 Minimal SettingsApp is complete.

- Settings now opens `SettingsApp.prefab` instead of `EmptyApp_Settings.prefab`.
- Settings supports wallpaper switching.
- Settings supports 12/24-hour time format switching.
- Settings supports Dock show/hide.
- Settings changes apply immediately through `PhoneSettingsApplier`.
- `PhoneSettingsStore` persists only non-sensitive PhoneOS UI preferences through PlayerPrefs.
- Back and Home return from SettingsApp to Home through the existing `PhoneAppHost` flow.
- `PhoneWallpaperCatalog.asset` owns the built-in wallpaper options.
- `SettingsSection.prefab` and `SettingsOptionButton.prefab` own reusable Settings UI row templates.
- At Stage 3 completion, Momotalk, Camera, and Debug remained placeholder app windows.

### Stage 4 - Minimal MomotalkApp Migration

Stage 4 Minimal MomotalkApp Migration is complete.

- Momotalk has switched from `EmptyApp_Momotalk.prefab` to `MomotalkApp.prefab`.
- `MomotalkApp.prefab` contains a minimal ContactList page and Chat page.
- ContactList shows one mock contact: Toki.
- Clicking Toki enters the Chat page.
- Chat shows a Toki title, mock messages, an input placeholder, and a Send button placeholder.
- Send only logs a Stage 4 preview message and does not call any real chat system.
- Back behavior is `Chat -> ContactList -> Home`.
- Home returns from any Momotalk page to PhoneOS Home through the existing `PhoneAppHost` flow.

Stage 4 deliberately does not connect real Momotalk chat, LLM, TTS, ASR, StagePlan, Memory, or old Momotalk business logic. Camera and Debug remain EmptyApp placeholders. SettingsApp remains the completed Stage 3 minimal SettingsApp.

## In Progress

### Stage 5 - Momotalk Static UI Completion

Stage 5 is in progress.

- Stage 5 references the contact list and chat detail page structure of the androidInReact WhatsApp Application.
- The final UI remains Momotalk-style, using the old Momotalk pink/peach visual language instead of WhatsApp green, name, or logo.
- ContactList is being completed as a static chat app contact list with a Momotalk AppBar, static tab area, Toki contact card, avatar placeholder, recent message preview, time, and unread indicator.
- Chat is being completed as a static chat detail page with a Momotalk top chat bar, Toki avatar/status, ScrollRect message list, left/right chat bubbles, long-message wrapping, and a fixed bottom input bar.
- Stage 5 uses only static mock messages and does not connect a database, Memory, or the old Momotalk message system.
- Send remains a placeholder action that logs only.

Stage 6 is the earliest stage that may consider a real text chat pipeline. LLM, LlmRelay, TTS, ASR, StagePlan, Memory, and old Momotalk business logic remain explicitly out of scope during Stage 5.

## Frozen Shell Contract

The system shell structure should remain stable before Stage 3:

```text
PhoneRoot
  PhoneDevice
    PhoneShadow
    PhoneFrame
    PhoneScreenMask
      PhoneScreenViewport
        Canvas_Static
          Wallpaper
        Canvas_System
          StatusBar
          HomeLayer
            HomeScreen
          AppLayer
            AppWindowContainer
          NavigationArea
            NavigationBar
        Canvas_Overlay
```

Do not remove or rename the shell nodes above without an explicit PhoneOS shell migration task.

## Current App Definitions

The registered PhoneOS apps are:

- `momotalk` -> `MomotalkApp.prefab`
- `camera` -> `EmptyApp_Camera.prefab`
- `debug` -> `EmptyApp_Debug.prefab`
- `settings` -> `SettingsApp.prefab`

Momotalk is a minimal real PhoneOS app shell for Stage 4. Camera and Debug prefabs are placeholders only. Settings is a completed minimal real SettingsApp for Stage 3.

## Art Replacement Contract

Current PhoneOS resources are still mostly placeholder art and uGUI programmatic styling. Future art replacement should follow these ownership rules:

- `PhoneOSStyle` owns system-level resources and tokens:
  - phone frame sprite
  - phone shadow sprite
  - wallpaper sprite
  - rounded panel sprite
  - navigation Back/Home/Recent icons
  - screen viewport insets
  - shared colors, icon sizes, grid sizes, and font sizes
- `PhoneAppDefinition` owns per-app launcher metadata:
  - app id
  - display name
  - launcher icon
  - app prefab
  - Home/Dock visibility
  - ordering
- Each app prefab owns its internal page resources:
  - app-local backgrounds
  - page icons
  - title/content layout
  - future app-specific art

Do not replace system-level resources by hard-coding sprites into runtime scripts. Prefer updating `PhoneOSStyle.asset`, `PhoneAppDefinition` assets, or the relevant app prefab.

## Explicit Non-Goals

Stage 5 Momotalk Static UI Completion does not include:

- real Momotalk chat migration
- real Camera migration
- real Debug migration
- multi-character support
- LLM integration
- LlmRelay integration
- TTS integration
- ASR integration
- StagePlan integration
- Memory integration
- old Momotalk business integration
- advanced Settings pages
- QuickSettings behavior
- RecentApps behavior
- multi-task/background app runtime
- WebView or React integration
- WhatsApp green theme, name, or logo as final Momotalk UI

## Stage 3 Scope

Stage 3 completed the following scope:

- keep the current PhoneOS shell stable
- replace `EmptyApp_Settings.prefab` with a minimal Settings app prefab
- keep Settings data local, minimal, and non-sensitive
- support wallpaper, time format, and Dock visibility only
- do not migrate Momotalk, Camera, or Debug during the SettingsApp first pass

## Stage 4 Scope

Stage 4 completed the minimal MomotalkApp migration into PhoneOS.

Stage 4 scope is limited to:

- `MomotalkApp.prefab`
- ContactList page
- Toki contact
- mock Chat page
- Momotalk app-internal Back behavior

Real chat, LLM, TTS, ASR, StagePlan, Memory, Camera, Debug, QuickSettings, RecentApps, and multi-task/background runtime remain out of scope.

## Stage 5 Scope

Stage 5 continues improving and completing Momotalk static UI and page structure.

Stage 5 scope is limited to:

- Momotalk-style ContactList static UI
- Toki contact card with avatar, preview, time, and unread indicator
- Momotalk-style Chat static UI
- ScrollRect mock message list
- left-side Toki bubbles and right-side user bubbles
- fixed bottom input bar
- Send placeholder log only
- Stage 5 status documentation

Stage 5 does not connect real models or the real chat pipeline.

## Stage 6 Next

Stage 6 will be the first stage that may consider the real text chat pipeline. Stage 6 should be planned separately before connecting any LLM, TTS, ASR, StagePlan, Memory, or old Momotalk business code.

## Validation Checklist

Stage 5 Momotalk Static UI Completion acceptance:

- Play Mode shows PhoneOS Home normally.
- Momotalk icon opens the real minimal MomotalkApp window.
- ContactList shows a chat app contact list structure.
- ContactList top area shows a Momotalk title and Momotalk pink/peach AppBar.
- ContactList Toki card includes an avatar placeholder, name, message preview, time, and unread indicator.
- Clicking Toki opens Chat.
- Chat shows a Momotalk-style top chat bar.
- Chat shows a ScrollRect mock message list.
- The message list includes left-side Toki bubbles and right-side user bubbles.
- Long text wraps without breaking the UI.
- The input area is fixed at the bottom and does not cover the message list or PhoneOS NavigationBar.
- Send logs only and does not call real chat, LLM, LlmRelay, TTS, ASR, StagePlan, or Memory.
- Back from Chat returns to ContactList.
- Back from ContactList closes MomotalkApp and returns to Home.
- Camera icon opens the Camera empty app window.
- Debug icon opens the Debug empty app window.
- Settings icon opens the real minimal SettingsApp window.
- Settings can switch wallpaper, 12/24-hour time, and Dock visibility.
- Settings changes apply immediately and persist after exiting and re-entering Play Mode.
- Back and Home close SettingsApp and return to Home.
- Home closes MomotalkApp from any Momotalk page and returns to Home.
- Recent logs only.
- Existing real Momotalk, SceneCamera, and Debug business code remains untouched.
