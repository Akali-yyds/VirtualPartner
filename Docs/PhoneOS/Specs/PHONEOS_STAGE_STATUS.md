# VirtualPhoneOS Stage Status

Last updated: 2026-06-30

## Current Stage

VirtualPhoneOS is currently frozen at Stage 2.5 shell closeout.

The current implementation is a uGUI-based virtual phone shell. It keeps the real Momotalk, SceneCamera, and Runtime Debug business systems untouched, and only provides a PhoneOS home screen plus empty AppHost windows for the first four apps.

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
- Momotalk, Camera, Debug, and Settings currently use empty app prefabs.

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

The registered Stage 2 apps are:

- `momotalk` -> `EmptyApp_Momotalk.prefab`
- `camera` -> `EmptyApp_Camera.prefab`
- `debug` -> `EmptyApp_Debug.prefab`
- `settings` -> `EmptyApp_Settings.prefab`

These app prefabs are placeholders only. They must not be treated as real Momotalk, Camera, Debug, or Settings implementations.

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

Stage 2.5 does not include:

- real SettingsApp functionality
- real Momotalk migration
- real Camera migration
- real Debug migration
- QuickSettings behavior
- RecentApps behavior
- multi-task/background app runtime
- WebView or React integration

## Stage 3 Entry

Stage 3 will start the minimal SettingsApp implementation.

The recommended Stage 3 scope is:

- keep the current PhoneOS shell stable
- replace `EmptyApp_Settings.prefab` with a minimal Settings app prefab
- keep Settings data local and minimal at first
- do not migrate Momotalk, Camera, or Debug during the SettingsApp first pass

## Validation Checklist

Before accepting Stage 2.5:

- Play Mode shows PhoneOS Home normally.
- Momotalk icon opens the Momotalk empty app window.
- Camera icon opens the Camera empty app window.
- Debug icon opens the Debug empty app window.
- Settings icon opens the Settings empty app window.
- Back closes the current empty app and returns to Home.
- Home closes the current empty app and returns to Home.
- Recent logs only.
- Existing real Momotalk, SceneCamera, and Debug business code remains untouched.
