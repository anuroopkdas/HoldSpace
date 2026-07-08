# HoldSpace

HoldSpace is a Windows desktop application that serves as a **hold-to-reveal overlay launcher**. By holding down a designated trigger key (default: Caps Lock), a full-screen overlay appears displaying custom shortcuts. Moving the mouse over a shortcut and releasing the trigger key instantly launches it. Releasing the key outside of any shortcut, or pressing `Esc`, cancels the overlay.

## Features

- **Global Hold Interceptor**: Uses a low-level keyboard hook to capture trigger key holds (120ms threshold) globally, while maintaining normal key tapping functionality.
- **Dynamic Full-Screen Overlay**: Transparent, borderless dark backdrop supporting smooth fade animations and custom hover scaling and glows.
- **Canvas Layout Editor**: Drag-and-drop shortcuts to visually reposition them on a virtual screen. Coordinates are calculated in fluid percentage coordinates that scale with screen size.
- **Rich Shortcut Support**: Launches apps (`.exe`), files, folders, and websites (`URLs`), as well as system actions like Windows Settings and Task Manager.
- **Settings Persistence**: Saves configurations and canvas locations directly inside `%APPDATA%\Roaming\HoldSpace` (`settings.json` and `layout.json`).
- **System Tray Integration**: Quietly runs in the background with a system tray menu to open the dashboard, test the overlay, or exit.
- **Start with Windows**: Toggleable configuration that securely updates the current user's registry startup keys.

---

## Getting Started

### Prerequisites
- **.NET 9.0 SDK** (installed on your system).

### Running the Application

Open a terminal (e.g., PowerShell) in the project directory:

1. **Restore dependencies**:
   ```bash
   dotnet restore
   ```
2. **Build the application**:
   ```bash
   dotnet build
   ```
3. **Run the application**:
   ```bash
   dotnet run
   ```

Upon launch, HoldSpace starts running in the background and places a tray icon (using the system application icon) in your Windows taskbar notification area. The main dashboard will also open automatically.

---

## How to Interact

1. **Open/Show Dashboard**: Double-click the system tray icon or right-click and select **Open HoldSpace**.
2. **Configure Shortcuts**: In the **Canvas** tab, drag the existing sample shortcuts (`Google`, `GitHub`, `Command Prompt`) to reposition them. Use the right panel to edit their titles and targets, add new shortcuts, or delete them. Click **Save Layout** to write changes to disk.
3. **Trigger the Overlay**: Press and **hold** the `Caps Lock` key.
   - Within 120ms, the screen dims and displays your shortcuts.
   - Hover the mouse cursor over a card. It will scale up and glow cyan.
   - **Release Caps Lock** while hovering over a card to launch the shortcut target.
4. **Cancel Overlay**:
   - Release the Caps Lock key over any empty space.
   - Or, press `Esc` while holding Caps Lock to hide the overlay immediately.
5. **Normal Caps Lock behavior**: A quick tap of the Caps Lock key (under 120ms) toggles uppercase typing as normal.

---

## File Structure

- `MainWindow.xaml` / `.xaml.cs`: The core dashboard shell housing configuration tabs.
- `OverlayWindow.xaml` / `.xaml.cs`: Topmost fullscreen borderless layout displaying shortcut nodes.
- `Services/`:
  - `InputHookService.cs`: Isolates the `SetWindowsHookEx` Win32 API calls for global trigger holds and Escape cancels.
  - `OverlayService.cs`: Manages show/hide/fade states of the overlay.
  - `LauncherService.cs`: Runs process launches with expanded environment variables.
  - `SettingsService.cs`: Serializes settings and layouts.
  - `TrayService.cs`: Operates the system tray icon and context menus.
  - `StartupService.cs`: Manages registry startup values.
- `Models/`: Data structures representing settings, coordinates, and actions.
