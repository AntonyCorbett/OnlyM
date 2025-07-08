#pragma once
#include <windows.h>

class MagnifierWindow  // NOLINT(cppcoreguidelines-special-member-functions)
{
public:
    MagnifierWindow();
    ~MagnifierWindow();

    bool Create(HWND parent, HINSTANCE instance, int x, int y, int width, int height);
    void Destroy();
    HWND GetWindowHandle() const;
    void SetSourceRect(const RECT& rect) const;
    bool SetTransform(float zoomFactor) const;
    void Resize(int x, int y, int width, int height) const;

private:
    HWND windowHandle_;
};
