#include "stdafx.h"
#include "InstructionsWindow.h"

#include <cstdio>
#include <tchar.h>

#include "Resource.h"

InstructionsWindow::InstructionsWindow()
    : windowHandle_(nullptr), fontHandle_(nullptr), brushHandle_(nullptr), height_(0)
{
}

InstructionsWindow::~InstructionsWindow()
{
    Destroy();
}

int InstructionsWindow::CalculateInstructionsWindowHeight()
{
    const int lineHeight = GetSystemMetrics(SM_CYMENU); // Standard menu text height
    return 4 * lineHeight; // For 4 lines of text
}

bool InstructionsWindow::Create(
    const HWND parent, const HINSTANCE instance, 
    const int y, const int width, const int height, 
    const TCHAR hotKey)
{
    Destroy();    

    windowHandle_ = CreateWindow(
        TEXT("STATIC"),
        nullptr,    
        WS_CHILD | WS_VISIBLE | SS_LEFT | SS_NOPREFIX,
        0, y, width, height,
        parent,
        reinterpret_cast<HMENU>(IDC_INSTRUCTIONS),
        instance,
        nullptr);

    if (!windowHandle_)
    {
        return false;
    }

    if (!brushHandle_)
    {
        brushHandle_ = CreateSolidBrush(RGB(255, 255, 192));
    }

    SetWindowLongPtr(windowHandle_, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(this));
    originalProc_ = reinterpret_cast<WNDPROC>(  // NOLINT(performance-no-int-to-ptr)
        SetWindowLongPtr(
            windowHandle_, GWLP_WNDPROC, reinterpret_cast<LONG_PTR>(&InstructionsWindow::StaticWndProc)));

    if (!fontHandle_)
    {
        constexpr int fontPointSize = 12;
        const HDC hdcScreen = GetDC(nullptr);
        const int fontHeight = -MulDiv(fontPointSize, GetDeviceCaps(hdcScreen, LOGPIXELSY), 72);
        ReleaseDC(nullptr, hdcScreen);
        fontHandle_ = CreateFont(
            fontHeight, 0, 0, 0, FW_NORMAL, FALSE, FALSE, FALSE,
            ANSI_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS,
            DEFAULT_QUALITY, DEFAULT_PITCH, TEXT("Segoe UI"));        
    }

    if (!boldFontHandle_) 
    {
        constexpr int fontPointSize = 12;
        const HDC hdcScreen = GetDC(nullptr);
        const int fontHeight = -MulDiv(fontPointSize, GetDeviceCaps(hdcScreen, LOGPIXELSY), 72);
        ReleaseDC(nullptr, hdcScreen);
        boldFontHandle_ = CreateFont(
            fontHeight, 0, 0, 0, FW_BOLD, FALSE, FALSE, FALSE,
            ANSI_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS,
            DEFAULT_QUALITY, DEFAULT_PITCH, TEXT("Segoe UI"));
    }

    SendMessage(windowHandle_, WM_SETFONT, reinterpret_cast<WPARAM>(fontHandle_), TRUE);

    TCHAR altZ[128];
    _stprintf_s(altZ, TEXT("Press ALT+%c to close Mirror Window"), hotKey);

    TCHAR magnifierText[128];
    _stprintf_s(magnifierText, TEXT("Magnifier: F1 - on/off, F2 - square/circle, F3 - reduce, F4 - enlarge"));

    TCHAR pageZoomText[128];
    _stprintf_s(pageZoomText, TEXT("Page: Ctrl+Plus - zoom in, Ctrl+Minus - zoom out, Ctrl+0 - reset zoom"));

    TCHAR multiLineText[256];
    _stprintf_s(multiLineText, TEXT("%s\r\n%s\r\n%s"), altZ, magnifierText, pageZoomText);
        
    SetWindowText(windowHandle_, multiLineText);

    return true;
}

LRESULT CALLBACK InstructionsWindow::StaticWndProc(
    const HWND windowHandle, const UINT msg, const WPARAM wParam, const LPARAM lParam)
{
    const InstructionsWindow* self = reinterpret_cast<InstructionsWindow*>( // NOLINT(performance-no-int-to-ptr)
        GetWindowLongPtr(windowHandle, GWLP_USERDATA));

    if (!self)
    {
        return DefWindowProc(windowHandle, msg, wParam, lParam);
    }

    if (msg == WM_PAINT)
    {
        PAINTSTRUCT ps;
        const HDC hdc = BeginPaint(windowHandle, &ps);

        RECT rc;
        GetClientRect(windowHandle, &rc);

        // Fill the background with yellow
        FillRect(hdc, &rc, self->brushHandle_);

        // Set your desired padding here (e.g., 8 pixels)
        constexpr int padding = 8;
        rc.left += padding;
        rc.top += padding;
        rc.right -= padding;
        rc.bottom -= padding;

        SetBkColor(hdc, RGB(255, 255, 192));
        SetTextColor(hdc, RGB(0, 0, 0));

        TCHAR buffer[256];
        GetWindowText(windowHandle, buffer, _countof(buffer));

        // Split into lines
        TCHAR* context = nullptr;
        const TCHAR* line = _tcstok_s(buffer, TEXT("\r\n"), &context);
        RECT lineRect = rc;        
        bool firstLine = true;

        while (line)
        {
            const HFONT font = firstLine ? self->boldFontHandle_ : self->fontHandle_;
            const HFONT oldFont = static_cast<HFONT>(SelectObject(hdc, font));

            SIZE sz;
            GetTextExtentPoint32(hdc, line, static_cast<int>(_tcslen(line)), &sz);
            const int lineHeight = sz.cy;

            DrawText(hdc, line, -1, &lineRect, DT_LEFT | DT_TOP | DT_NOPREFIX | DT_SINGLELINE);  // NOLINT(misc-redundant-expression)

            SelectObject(hdc, oldFont);

            lineRect.top += lineHeight;
            firstLine = false;
            line = _tcstok_s(nullptr, TEXT("\r\n"), &context);
        }

        EndPaint(windowHandle, &ps);
        return 0;
    }

    // Call original proc for all other messages
    return self->originalProc_
        ? CallWindowProc(self->originalProc_, windowHandle, msg, wParam, lParam)
        : DefWindowProc(windowHandle, msg, wParam, lParam);
}

void InstructionsWindow::Destroy()
{
    if (windowHandle_)
    {
        DestroyWindow(windowHandle_);
        windowHandle_ = nullptr;
    }

    if (fontHandle_)
    {
        DeleteObject(fontHandle_);
        fontHandle_ = nullptr;
    }

    if (boldFontHandle_)
    {
        DeleteObject(boldFontHandle_);
        boldFontHandle_ = nullptr;
    }

    if (brushHandle_)
    {
        DeleteObject(brushHandle_);
        brushHandle_ = nullptr;
    }
}

HWND InstructionsWindow::GetWindowHandle() const { return windowHandle_; }
int InstructionsWindow::GetHeight() const { return height_; }

void InstructionsWindow::RepositionWithHost(const RECT& hostClientRect) const
{
    if (windowHandle_)
    {
        SetWindowPos(
            windowHandle_, nullptr, 0, hostClientRect.bottom - height_, hostClientRect.right, height_, SWP_NOZORDER);
    }
}

void InstructionsWindow::Resize(const int x, const int y, const int width, const int height) const
{
    if (windowHandle_)
    {
        SetWindowPos(windowHandle_, nullptr, x, y, width, height, SWP_NOZORDER);
    }
}
