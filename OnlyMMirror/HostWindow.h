#pragma once
#include <windows.h>
#include "MagnifierWindow.h"
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
        TCHAR hotKey);

    void Destroy();
    HWND GetWindowHandle() const;
    void Show(int nCmdShow) const;
    void Update() const;
    void SetCaption(const TCHAR* caption) const;
    void SetTopMost() const;
    void UpdateMirror(const RECT& sourceRect) const;
    void PositionCursor() const;
    static void RepositionCursor();
    MagnifierWindow& GetMagnifierWindow();
    InstructionsWindow& GetInstructionsWindow();

    static LRESULT CALLBACK WindowProc(HWND windowHandle, UINT message, WPARAM wParam, LPARAM lParam);
    static const TCHAR* GetWindowClassName();

private:
    HWND windowHandle_;
    MagnifierWindow magnifierWindow_;
    InstructionsWindow instructionsWindow_;
    int instructionsWindowHeight_;
    float zoomFactor_;
    RECT targetMonitorRect_;
    HINSTANCE hInstance_;
    void OnSize() const;
    void OnDestroy();
    //void OnCtlColorStatic(WPARAM wParam, LPARAM lParam, LRESULT& result, bool& handled);
    void RegisterWindowClass() const;
};
