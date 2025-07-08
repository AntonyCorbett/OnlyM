#include "stdafx.h"
#include <magnification.h>
#include "MagnifierWindow.h"

MagnifierWindow::MagnifierWindow() : windowHandle_(nullptr) {}

MagnifierWindow::~MagnifierWindow() { Destroy(); }

bool MagnifierWindow::Create(
    const HWND parent, const HINSTANCE instance, const int x, const int y, const int width, const int height)
{
    Destroy();

    windowHandle_ = CreateWindow(
        WC_MAGNIFIER,
        TEXT("MagnifierWindow"),
        WS_CHILD | MS_SHOWMAGNIFIEDCURSOR | WS_VISIBLE,
        x, y, width, height,
        parent,
        nullptr,
        instance,
        nullptr);

    return windowHandle_ != nullptr;
}

void MagnifierWindow::Destroy()
{
    if (windowHandle_)
    {
        DestroyWindow(windowHandle_);
        windowHandle_ = nullptr;
    }
}

HWND MagnifierWindow::GetWindowHandle() const { return windowHandle_; }

void MagnifierWindow::SetSourceRect(const RECT& rect) const
{
    if (windowHandle_)
    {
        MagSetWindowSource(windowHandle_, rect);
    }
}

bool MagnifierWindow::SetTransform(const float zoomFactor) const
{
    if (!windowHandle_)
    {
        return false;
    }

    MAGTRANSFORM matrix = {};
    matrix.v[0][0] = zoomFactor;
    matrix.v[1][1] = zoomFactor;
    matrix.v[2][2] = 1.0f;

    return MagSetWindowTransform(windowHandle_, &matrix) == TRUE;
}

void MagnifierWindow::Resize(const int x, const int y, const int width, const int height) const
{
    if (windowHandle_)
    {
        SetWindowPos(windowHandle_, nullptr, x, y, width, height, SWP_NOZORDER);
    }
}
