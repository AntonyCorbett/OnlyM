#pragma once

#include <windows.h>

class InstructionsWindow  // NOLINT(cppcoreguidelines-special-member-functions)
{
public:
    InstructionsWindow();
    ~InstructionsWindow();

    bool Create(HWND parent, HINSTANCE instance, int y, int width, int height, TCHAR hotKey);
    void Destroy();
    HWND GetWindowHandle() const;
    int GetHeight() const;    
    void RepositionWithHost(const RECT& hostClientRect) const;
    void Resize(int x, int y, int width, int height) const;

    static int CalculateInstructionsWindowHeight();

private:
    static LRESULT CALLBACK StaticWndProc(HWND windowHandle, UINT msg, WPARAM wParam, LPARAM lParam);
    WNDPROC originalProc_ = nullptr;
    HWND windowHandle_;
    HFONT fontHandle_;
    HFONT boldFontHandle_;
    HBRUSH brushHandle_;
    int height_;
};
