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

int InstructionsWindow::CalculateInstructionHeight()
{
    const int lineHeight = GetSystemMetrics(SM_CYMENU); // Standard menu text height
    return 6 * lineHeight; // For 6 lines of text
}

//int InstructionsWindow::CalculateInstructionHeight()
//{
//    constexpr int fontPointSize = 24;
//    const HDC hdcScreen = GetDC(nullptr);
//    const int fontHeight = -MulDiv(fontPointSize, GetDeviceCaps(hdcScreen, LOGPIXELSY), 72);
//    ReleaseDC(nullptr, hdcScreen);
//    return 3 * abs(fontHeight);
//}

bool InstructionsWindow::Create(
    const HWND parent, const HINSTANCE instance, const int y, const int width, const int height, TCHAR hotKey)
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

        height_ = CalculateInstructionHeight();
    }

    SendMessage(windowHandle_, WM_SETFONT, reinterpret_cast<WPARAM>(fontHandle_), TRUE);

    TCHAR altZ[64];
    _stprintf_s(altZ, TEXT("Press ALT+%c to close Mirror Window"), hotKey);

    TCHAR multiLineText[256];
    _stprintf_s(multiLineText, TEXT("%s\r\nHello\r\nWorld"), altZ);

    SetWindowText(windowHandle_, multiLineText);

    return true;
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
    if (brushHandle_)
    {
        DeleteObject(brushHandle_);
        brushHandle_ = nullptr;
    }
}

HWND InstructionsWindow::GetWindowHandle() const { return windowHandle_; }
int InstructionsWindow::GetHeight() const { return height_; }

INT_PTR InstructionsWindow::HandleCtlColorStatic(const HWND controlWindow, const HDC hdc)
{
    if (controlWindow == windowHandle_ && brushHandle_)
    {
        SetBkColor(hdc, RGB(255, 255, 192));
        SetTextColor(hdc, RGB(0, 0, 0));
        return reinterpret_cast<INT_PTR>(brushHandle_);
    }

    return 0;
}

void InstructionsWindow::RepositionWithHost(const RECT& hostClientRect) const
{
    if (windowHandle_)
    {
        SetWindowPos(
            windowHandle_, nullptr, 0, hostClientRect.bottom - height_, hostClientRect.right, height_, SWP_NOZORDER);
    }
}

void InstructionsWindow::Resize(const int x, const int y, const int width, const int height)
{
    if (windowHandle_)
    {
        SetWindowPos(windowHandle_, nullptr, x, y, width, height, SWP_NOZORDER);
    }
}
