#include "stdafx.h"
#include "HostWindow.h"
#include <strsafe.h>

const TCHAR* HostWindow::GetWindowClassName() { return TEXT("OnlyMMirrorWindow"); }

HostWindow::HostWindow()
    : windowHandle_(nullptr), instructionsHeight_(0), zoomFactor_(1.0f), hInstance_(nullptr)
{
    ZeroMemory(&targetMonitorRect_, sizeof(targetMonitorRect_));
}

HostWindow::~HostWindow()
{
    Destroy();
}

void HostWindow::RegisterWindowClass() const
{
    WNDCLASSEX windowClassEx = {};
    windowClassEx.cbSize = sizeof(WNDCLASSEX);
    windowClassEx.style = CS_HREDRAW | CS_VREDRAW;
    windowClassEx.lpfnWndProc = WindowProc;
    windowClassEx.hInstance = hInstance_;
    windowClassEx.hCursor = LoadCursor(nullptr, IDC_ARROW);
    windowClassEx.hbrBackground = reinterpret_cast<HBRUSH>(1 + COLOR_BTNFACE);  // NOLINT(performance-no-int-to-ptr)
    windowClassEx.lpszClassName = GetWindowClassName();
    RegisterClassEx(&windowClassEx);
}

bool HostWindow::Create(
    const HINSTANCE instance, 
    const int x, const int y, const int width, const int height, 
    const float zoomFactor, 
    const RECT& targetMonitorRect,
    TCHAR hotKey)
{
    Destroy();

    hInstance_ = instance;
    zoomFactor_ = zoomFactor;
    targetMonitorRect_ = targetMonitorRect;
    instructionsHeight_ = InstructionsWindow::CalculateInstructionHeight();

    RegisterWindowClass();

    // create host window...
    windowHandle_ = CreateWindowEx(
        WS_EX_TOPMOST | WS_EX_TOOLWINDOW,
        GetWindowClassName(),
        TEXT("OnlyM Mirror"),
        WS_CLIPCHILDREN | WS_CAPTION | WS_DISABLED,
        x, y, width, height,
        nullptr,
        nullptr,
        hInstance_,
        this);

    if (!windowHandle_)
    {
        return false;
    }

    RECT clientRect;
    GetClientRect(windowHandle_, &clientRect);

    magnifierWindow_.Create(
        windowHandle_, hInstance_, 
        0, 0, clientRect.right, clientRect.bottom - instructionsHeight_);

    instructionsWindow_.Create(
        windowHandle_, hInstance_, 
        clientRect.bottom - instructionsHeight_, clientRect.right, instructionsHeight_,
        hotKey);

    return magnifierWindow_.SetTransform(zoomFactor_);    
}

void HostWindow::Destroy()
{
    instructionsWindow_.Destroy();
    magnifierWindow_.Destroy();
    if (windowHandle_)
    {
        DestroyWindow(windowHandle_);
        windowHandle_ = nullptr;
    }
}

HWND HostWindow::GetWindowHandle() const { return windowHandle_; }

void HostWindow::Show(int nCmdShow) const { if (windowHandle_) ShowWindow(windowHandle_, nCmdShow | SW_SHOWNA); }

void HostWindow::Update() const { if (windowHandle_) UpdateWindow(windowHandle_); }

void HostWindow::SetCaption(const TCHAR* caption) const { if (windowHandle_) SetWindowText(windowHandle_, caption); }

void HostWindow::SetTopMost() const { if (windowHandle_) SetWindowPos(windowHandle_, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOACTIVATE | SWP_NOMOVE | SWP_NOSIZE); }

void HostWindow::UpdateMirror(const RECT& sourceRect) const
{
    magnifierWindow_.SetSourceRect(sourceRect);
    SetTopMost();
    InvalidateRect(magnifierWindow_.GetWindowHandle(), nullptr, TRUE);
}

void HostWindow::PositionCursor() const
{
    const int width = targetMonitorRect_.right - targetMonitorRect_.left;
    const int height = targetMonitorRect_.bottom - targetMonitorRect_.top;
    SetCursorPos(targetMonitorRect_.left + width / 2, targetMonitorRect_.top + height / 2);
}

void HostWindow::RepositionCursor()
{
    const HWND window = ::FindWindow(nullptr, "S o u n d B o x - O N L Y M");
    if (window)
    {
        RECT r;
        if (GetWindowRect(window, &r))
        {
            const int width = r.right - r.left;
            const int height = r.bottom - r.top;
            SetCursorPos(r.left + width / 2, r.top + height / 2);
        }
    }
}

MagnifierWindow& HostWindow::GetMagnifierWindow() { return magnifierWindow_; }

InstructionsWindow& HostWindow::GetInstructionsWindow() { return instructionsWindow_; }

void HostWindow::OnSize()
{
    if (magnifierWindow_.GetWindowHandle() && instructionsWindow_.GetWindowHandle())
    {
        RECT clientRect;
        GetClientRect(windowHandle_, &clientRect);
        magnifierWindow_.Resize(0, 0, clientRect.right, clientRect.bottom - instructionsHeight_);
        instructionsWindow_.Resize(0, clientRect.bottom - instructionsHeight_, clientRect.right, instructionsHeight_);
    }
}

void HostWindow::OnDestroy()
{
    instructionsWindow_.Destroy();
    magnifierWindow_.Destroy();
    PostQuitMessage(0);
}

void HostWindow::OnCtlColorStatic(WPARAM wParam, LPARAM lParam, LRESULT& result, bool& handled)
{
    const HWND controlWindow = reinterpret_cast<HWND>(lParam);  // NOLINT(performance-no-int-to-ptr)
    const HDC hdc = reinterpret_cast<HDC>(wParam);  // NOLINT(performance-no-int-to-ptr)
    result = instructionsWindow_.HandleCtlColorStatic(controlWindow, hdc);
    handled = (result != 0);
}

LRESULT CALLBACK HostWindow::WindowProc(HWND windowHandle, UINT message, WPARAM wParam, LPARAM lParam)
{
    HostWindow* self = nullptr;
    if (message == WM_NCCREATE)
    {
        CREATESTRUCT* cs = reinterpret_cast<CREATESTRUCT*>(lParam);  // NOLINT(performance-no-int-to-ptr)
        self = static_cast<HostWindow*>(cs->lpCreateParams);
        SetWindowLongPtr(windowHandle, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(self));
        self->windowHandle_ = windowHandle;
    }
    else
    {
        self = reinterpret_cast<HostWindow*>(GetWindowLongPtr(windowHandle, GWLP_USERDATA));  // NOLINT(performance-no-int-to-ptr)
    }

    if (!self)
    {
        return DefWindowProc(windowHandle, message, wParam, lParam);
    }

    switch (message)
    {
    case WM_SETCURSOR:
        SetCursor(nullptr);
        return TRUE;
    case WM_DESTROY:
        self->OnDestroy();
        break;
    case WM_SIZE:
        self->OnSize();
        break;
    case WM_CTLCOLORSTATIC:
    {
        LRESULT result = 0;
        bool handled = false;
        self->OnCtlColorStatic(wParam, lParam, result, handled);
        if (handled) return result;
        break;
    }
    default:
        return DefWindowProc(windowHandle, message, wParam, lParam);
    }
    return 0;
}
