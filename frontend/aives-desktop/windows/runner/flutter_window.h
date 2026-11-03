#ifndef RUNNER_FLUTTER_WINDOW_H_
#define RUNNER_FLUTTER_WINDOW_H_

#include <flutter/dart_project.h>
#include <flutter/flutter_view_controller.h>
#include <flutter/method_channel.h>
#include <flutter/encodable_value.h>

#include <memory>
#include <vector>

#include "win32_window.h"

// A window that does nothing but host a Flutter view.
class FlutterWindow : public Win32Window {
 public:
  // Creates a new FlutterWindow hosting a Flutter view running |project|.
  explicit FlutterWindow(const flutter::DartProject& project);
  virtual ~FlutterWindow();

 protected:
  // Win32Window:
  bool OnCreate() override;
  void OnDestroy() override;
  LRESULT MessageHandler(HWND window, UINT const message, WPARAM const wparam,
                         LPARAM const lparam) noexcept override;

 private:
  bool StartKiosk();
  void StopKiosk();
  void FitScreen();
  bool RefreshShields();
  static LRESULT CALLBACK KeyboardHook(int code, WPARAM message, LPARAM data);
  static LRESULT CALLBACK ShieldProc(HWND window, UINT message, WPARAM wparam,
                                    LPARAM lparam);
  static BOOL CALLBACK AddShield(HMONITOR monitor, HDC dc, LPRECT bounds,
                                LPARAM data);

  std::unique_ptr<flutter::MethodChannel<flutter::EncodableValue>> window_channel_;
  std::vector<HWND> shields_;
  HHOOK keyboard_hook_ = nullptr;
  bool kiosk_active_ = false;
  bool fitting_screen_ = false;
  bool protection_warning_shown_ = false;

  // The project to run.
  flutter::DartProject project_;

  // The Flutter instance hosted by this window.
  std::unique_ptr<flutter::FlutterViewController> flutter_controller_;
};

#endif  // RUNNER_FLUTTER_WINDOW_H_
