#include "flutter_window.h"

#include <optional>
#include <flutter/standard_method_codec.h>

#include "flutter/generated_plugin_registrant.h"

namespace {
constexpr UINT kQuitMessage = WM_APP + 101;
constexpr UINT_PTR kKioskTimer = 101;
constexpr wchar_t kShieldClass[] = L"AIVES_KIOSK_SHIELD";
HHOOK active_keyboard_hook = nullptr;
}

FlutterWindow::FlutterWindow(const flutter::DartProject& project)
    : project_(project) {}

FlutterWindow::~FlutterWindow() { StopKiosk(); }

bool FlutterWindow::OnCreate() {
  if (!Win32Window::OnCreate()) {
    return false;
  }

  // Use physical monitor bounds (not the work area) so the taskbar is covered.
  SetWindowLongPtr(GetHandle(), GWL_STYLE, WS_POPUP | WS_CLIPCHILDREN);
  FitScreen();
  RECT frame = GetClientArea();

  // The size here must match the window dimensions to avoid unnecessary surface
  // creation / destruction in the startup path.
  flutter_controller_ = std::make_unique<flutter::FlutterViewController>(
      frame.right - frame.left, frame.bottom - frame.top, project_);
  // Ensure that basic setup of the controller was successful.
  if (!flutter_controller_->engine() || !flutter_controller_->view()) {
    return false;
  }
  RegisterPlugins(flutter_controller_->engine());
  SetChildContent(flutter_controller_->view()->GetNativeWindow());
  window_channel_ =
      std::make_unique<flutter::MethodChannel<flutter::EncodableValue>>(
          flutter_controller_->engine()->messenger(), "aives/window",
          &flutter::StandardMethodCodec::GetInstance());
  window_channel_->SetMethodCallHandler(
      [this](const auto& call, auto result) {
        if (call.method_name() == "quit") {
          // Dart has already confirmed exit and preserved any recording.
          result->Success();
          PostMessage(GetHandle(), kQuitMessage, 0, 0);
        } else {
          result->NotImplemented();
        }
      });

  flutter_controller_->engine()->SetNextFrameCallback([&]() {
    this->Show();
    if (!StartKiosk()) {
      StopKiosk();
      MessageBox(GetHandle(),
                 L"AIVES could not enable examination screen protection. "
                 L"The application will close. Please contact the proctor.",
                 L"AIVES Desktop", MB_OK | MB_ICONERROR);
      PostMessage(GetHandle(), kQuitMessage, 0, 0);
    }
  });

  // Flutter can complete the first frame before the "show window" callback is
  // registered. The following call ensures a frame is pending to ensure the
  // window is shown. It is a no-op if the first frame hasn't completed yet.
  flutter_controller_->ForceRedraw();

  return true;
}

void FlutterWindow::OnDestroy() {
  StopKiosk();
  window_channel_.reset();
  if (flutter_controller_) {
    flutter_controller_ = nullptr;
  }

  Win32Window::OnDestroy();
}

LRESULT
FlutterWindow::MessageHandler(HWND hwnd, UINT const message,
                              WPARAM const wparam,
                              LPARAM const lparam) noexcept {
  // Handle kiosk controls before plugins can minimize or close the window.
  if (message == kQuitMessage) {
    StopKiosk();
    DestroyWindow(hwnd);
    return 0;
  }
  if (kiosk_active_) {
    switch (message) {
      case WM_CLOSE:
        return 0;
      case WM_SYSCOMMAND:
        switch (wparam & 0xfff0) {
          case SC_MINIMIZE:
          case SC_MAXIMIZE:
          case SC_RESTORE:
          case SC_MOVE:
          case SC_SIZE:
          case SC_CLOSE:
          case SC_KEYMENU:
            return 0;
        }
        break;
      case WM_DISPLAYCHANGE:
        FitScreen();
        if (!RefreshShields() && !protection_warning_shown_) {
          protection_warning_shown_ = true;
          // Keep the app and existing protection alive: only Dart's visible
          // Quit action may exit after saving an in-progress recording.
          MessageBox(hwnd, L"AIVES could not protect all connected displays. "
                     L"Please contact the proctor and use the Quit button "
                     L"to safely leave the application.",
                     L"AIVES Desktop", MB_OK | MB_ICONERROR);
        }
        return 0;
      case WM_DPICHANGED:
        FitScreen();
        // Ignore the suggested restored window rectangle: kiosk bounds are
        // already physical pixels, including under mixed monitor scaling.
        return 0;
      case WM_TIMER:
        if (wparam == kKioskTimer) {
          if (IsIconic(hwnd)) ShowWindow(hwnd, SW_RESTORE);
          SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0,
                       SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
          DWORD foreground_process = 0;
          GetWindowThreadProcessId(GetForegroundWindow(), &foreground_process);
          // Preserve the focus of our own modal dialogs (including errors).
          if (foreground_process != GetCurrentProcessId()) {
            SetForegroundWindow(hwnd);
          }
          return 0;
        }
        break;
    }
  }
  // Give Flutter, including plugins, an opportunity to handle window messages.
  if (flutter_controller_) {
    std::optional<LRESULT> result =
        flutter_controller_->HandleTopLevelWindowProc(hwnd, message, wparam,
                                                      lparam);
    if (result) {
      return *result;
    }
  }

  switch (message) {
    case WM_FONTCHANGE:
      flutter_controller_->engine()->ReloadSystemFonts();
      break;
  }

  return Win32Window::MessageHandler(hwnd, message, wparam, lparam);
}

bool FlutterWindow::StartKiosk() {
  keyboard_hook_ = SetWindowsHookEx(WH_KEYBOARD_LL, KeyboardHook,
                                   GetModuleHandle(nullptr), 0);
  if (!keyboard_hook_) return false;
  active_keyboard_hook = keyboard_hook_;
  kiosk_active_ = true;
  FitScreen();
  if (!RefreshShields()) return false;
  SetForegroundWindow(GetHandle());
  return SetTimer(GetHandle(), kKioskTimer, 250, nullptr) != 0;
}

void FlutterWindow::StopKiosk() {
  kiosk_active_ = false;
  if (GetHandle()) KillTimer(GetHandle(), kKioskTimer);
  if (keyboard_hook_) {
    UnhookWindowsHookEx(keyboard_hook_);
    keyboard_hook_ = nullptr;
    active_keyboard_hook = nullptr;
  }
  for (HWND shield : shields_) DestroyWindow(shield);
  shields_.clear();
}

void FlutterWindow::FitScreen() {
  if (fitting_screen_ || !GetHandle()) return;
  fitting_screen_ = true;
  MONITORINFO info{sizeof(MONITORINFO)};
  if (GetMonitorInfo(MonitorFromWindow(GetHandle(), MONITOR_DEFAULTTOPRIMARY),
                     &info)) {
    const RECT& rect = info.rcMonitor;
    SetWindowPos(GetHandle(), HWND_TOPMOST, rect.left, rect.top,
                 rect.right - rect.left, rect.bottom - rect.top,
                 SWP_FRAMECHANGED | SWP_NOACTIVATE);
  }
  fitting_screen_ = false;
}

LRESULT CALLBACK FlutterWindow::KeyboardHook(int code, WPARAM message,
                                             LPARAM data) {
  if (code == HC_ACTION) {
    const auto* key = reinterpret_cast<KBDLLHOOKSTRUCT*>(data);
    const bool alt = (key->flags & LLKHF_ALTDOWN) != 0;
    const bool ctrl = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
    const DWORD vk = key->vkCode;
    if (vk == VK_LWIN || vk == VK_RWIN ||
        (alt && (vk == VK_TAB || vk == VK_ESCAPE || vk == VK_F4 ||
                 vk == VK_SPACE)) ||
        (ctrl && vk == VK_ESCAPE)) {
      return 1;
    }
  }
  return CallNextHookEx(active_keyboard_hook, code, message, data);
}

bool FlutterWindow::RefreshShields() {
  WNDCLASS wc{};
  wc.lpfnWndProc = ShieldProc;
  wc.hInstance = GetModuleHandle(nullptr);
  wc.lpszClassName = kShieldClass;
  wc.hbrBackground = static_cast<HBRUSH>(GetStockObject(BLACK_BRUSH));
  wc.hCursor = LoadCursor(nullptr, IDC_ARROW);
  // The class may already exist after a display change.
  if (!RegisterClass(&wc) && GetLastError() != ERROR_CLASS_ALREADY_EXISTS)
    return false;
  // Keep previous covers until replacements exist; a failed hotplug update
  // must not expose displays that were already protected.
  auto old_shields = std::move(shields_);
  shields_.clear();
  const bool success = EnumDisplayMonitors(nullptr, nullptr, AddShield,
                            reinterpret_cast<LPARAM>(this)) != FALSE;
  if (success) {
    for (HWND shield : old_shields) DestroyWindow(shield);
    protection_warning_shown_ = false;
  } else {
    shields_.insert(shields_.end(), old_shields.begin(), old_shields.end());
  }
  return success;
}

BOOL CALLBACK FlutterWindow::AddShield(HMONITOR monitor, HDC, LPRECT bounds,
                                       LPARAM data) {
  auto* self = reinterpret_cast<FlutterWindow*>(data);
  if (monitor == MonitorFromWindow(self->GetHandle(), MONITOR_DEFAULTTOPRIMARY))
    return TRUE;
  HWND shield = CreateWindowEx(
      WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE, kShieldClass,
      L"AIVES examination in progress", WS_POPUP, bounds->left, bounds->top,
      bounds->right - bounds->left, bounds->bottom - bounds->top,
      self->GetHandle(), nullptr, GetModuleHandle(nullptr), self);
  if (!shield) return FALSE;
  self->shields_.push_back(shield);
  ShowWindow(shield, SW_SHOWNOACTIVATE);
  return TRUE;
}

LRESULT CALLBACK FlutterWindow::ShieldProc(HWND window, UINT message,
                                           WPARAM wparam, LPARAM lparam) {
  if (message == WM_NCCREATE) {
    const auto* create = reinterpret_cast<CREATESTRUCT*>(lparam);
    SetWindowLongPtr(window, GWLP_USERDATA,
                     reinterpret_cast<LONG_PTR>(create->lpCreateParams));
  }
  if (message == WM_CLOSE) return 0;
  if (message == WM_LBUTTONDOWN || message == WM_RBUTTONDOWN) {
    auto* self = reinterpret_cast<FlutterWindow*>(
        GetWindowLongPtr(window, GWLP_USERDATA));
    if (self) SetForegroundWindow(self->GetHandle());
    return 0;
  }
  return DefWindowProc(window, message, wparam, lparam);
}
