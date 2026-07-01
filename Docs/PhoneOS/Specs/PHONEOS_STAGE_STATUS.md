# VirtualPhoneOS Stage Status

Last updated: 2026-07-01

## Current Stage

VirtualPhoneOS is currently in Stage 3 minimal SettingsApp implementation.

The current implementation is a uGUI-based virtual phone shell. It keeps the real Momotalk, SceneCamera, and Runtime Debug business systems untouched, provides a PhoneOS home screen plus AppHost windows, and replaces only the Settings placeholder with a minimal real SettingsApp.

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

Stage 3 minimal SettingsApp is in progress.

- Settings now opens `SettingsApp.prefab` instead of `EmptyApp_Settings.prefab`.
- Settings supports local UI preferences for wallpaper, 12/24-hour time, and Dock visibility.
- `PhoneSettingsStore` persists only non-sensitive PhoneOS UI preferences through PlayerPrefs.
- `PhoneWallpaperCatalog.asset` owns the built-in wallpaper options.
- `SettingsSection.prefab` and `SettingsOptionButton.prefab` own reusable Settings UI row templates.
- Momotalk, Camera, and Debug remain placeholder app windows.

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

- `momotalk` -> `EmptyApp_Momotalk.prefab`
- `camera` -> `EmptyApp_Camera.prefab`
- `debug` -> `EmptyApp_Debug.prefab`
- `settings` -> `SettingsApp.prefab`

Momotalk, Camera, and Debug prefabs are placeholders only. Settings is currently a minimal real SettingsApp and must not be expanded beyond the active Stage 3 scope without a new task.

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

Stage 3 minimal SettingsApp does not include:

- real Momotalk migration
- real Camera migration
- real Debug migration
- advanced Settings pages
- QuickSettings behavior
- RecentApps behavior
- multi-task/background app runtime
- WebView or React integration

## Stage 3 Scope

The active Stage 3 scope is:

- keep the current PhoneOS shell stable
- replace `EmptyApp_Settings.prefab` with a minimal Settings app prefab
- keep Settings data local, minimal, and non-sensitive
- support wallpaper, time format, and Dock visibility only
- do not migrate Momotalk, Camera, or Debug during the SettingsApp first pass

## Validation Checklist

Before accepting Stage 3 minimal SettingsApp:

- Play Mode shows PhoneOS Home normally.
- Momotalk icon opens the Momotalk empty app window.
- Camera icon opens the Camera empty app window.
- Debug icon opens the Debug empty app window.
- Settings icon opens the real minimal SettingsApp window.
- Settings can switch wallpaper, 12/24-hour time, and Dock visibility.
- Settings changes apply immediately and persist after exiting and re-entering Play Mode.
- Back closes the current app and returns to Home when the app does not handle Back.
- Home closes the current app and returns to Home.
- Recent logs only.
- Existing real Momotalk, SceneCamera, and Debug business code remains untouched.
