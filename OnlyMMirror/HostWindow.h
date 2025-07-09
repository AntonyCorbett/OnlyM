#pragma once
#include <windows.h>
#include "DuplicationWindow.h"
#include "InstructionsWindow.h"

class HostWindow  // NOLINT(cppcoreguidelines-special-member-functions)
{
public:
    HostWindow();
    ~HostWindow();

    bool Create(
        HINSTANCE instance, 
        int x, int y, int width, int height, 
        float zoomFactor, 
        const RECT& targetMonitorRect, 
        TCHAR hotKey,
        const char* targetMonitorName = nullptr);

    void Destroy();
    HWND GetWindowHandle() const;
    void Show(int nCmdShow) const;
    void Update() const;
    void SetCaption(const TCHAR* caption) const;
    void SetTopMost() const;
    void UpdateMirror(const RECT& sourceRect);
    void PositionCursor() const;
    static void RepositionCursor();
    DuplicationWindow& GetDuplicationWindow();
    InstructionsWindow& GetInstructionsWindow();

    static LRESULT CALLBACK WindowProc(HWND windowHandle, UINT message, WPARAM wParam, LPARAM lParam);
    static const TCHAR* GetWindowClassName();

private:
    HWND windowHandle_;
    DuplicationWindow duplicationWindow_;
    InstructionsWindow instructionsWindow_;
    int instructionsWindowHeight_;
    float zoomFactor_;
    RECT targetMonitorRect_;
    HINSTANCE hInstance_;
    void OnSize() const;
    void OnDestroy();
    void RegisterWindowClass() const;
};
