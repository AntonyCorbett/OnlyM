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
    INT_PTR HandleCtlColorStatic(HWND controlWindow, HDC hdc);
    void RepositionWithHost(const RECT& hostClientRect) const;
    void Resize(int x, int y, int width, int height);

    static int CalculateInstructionHeight();

private:
    HWND windowHandle_;
    HFONT fontHandle_;
    HBRUSH brushHandle_;
    int height_;
};
