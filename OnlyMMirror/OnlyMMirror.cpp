#include "stdafx.h"

#include <windows.h>
#include <wincodec.h>
#include <strsafe.h>
#include <magnification.h>
#include "InstructionsWindow.h"
#include "HostWindow.h"

// WS_DISABLED prevents window being moved
#define HOST_WINDOW_STYLES (WS_CLIPCHILDREN | WS_CAPTION | WS_DISABLED)
#define HOST_WINDOW_STYLES_EX (WS_EX_TOPMOST | WS_EX_TOOLWINDOW)

// Global variables and strings.
constexpr TCHAR WindowTitle[] = TEXT("OnlyM Mirror");
constexpr UINT TimerInterval = 100;
constexpr int MaxMonitorNameLength = 32;

namespace
{
    HINSTANCE applicationInstance;
    HostWindow hostWindow;
    RECT mainMonitorRect;
    RECT targetMonitorRect;
    TCHAR mainMonitorName[MaxMonitorNameLength + 1];
    TCHAR targetMonitorName[MaxMonitorNameLength + 1];
    float zoomFactor = 1.0F;
    TCHAR hotKey = 'Z';
}

// Forward declarations.
namespace
{
    bool SetupMirror(HINSTANCE instance);
    void CALLBACK UpdateMirrorWindow(HWND /*hostWindow*/, UINT /*message*/, UINT_PTR /*eventId*/, DWORD /*time*/);
    BOOL CALLBACK OnlyMMonitorEnumProc(HMONITOR monitor, HDC monitorDeviceContext, LPRECT monitorRect, LPARAM data);
    bool InitMonitors();
    bool InitHotKey();
    bool InitFromCommandLine();
}

/// <summary>
/// Main entry point
/// </summary>
/// <param name="hInstance">handle to current application instance.</param>
/// <param name="nCmdShow">Controls how the window is to be shown.</param>
/// <returns>Returns the exist value contained in the wParam parameter
/// of the WM_QUIT message, or a bespoke error code on failure.</returns>
int APIENTRY WinMain(
    _In_ HINSTANCE hInstance,
	_In_opt_ HINSTANCE /*hPrevInstance*/,
	_In_ LPSTR /*lpCmdLine*/,
	_In_ int nCmdShow)
{
    applicationInstance = hInstance;

	if (!InitFromCommandLine())
	{
		return 1;
	}

	if (!InitMonitors())
	{
		return 4;
	}

	if (!MagInitialize())
	{
		return 2;
	}

	if (!SetupMirror(hInstance))
	{
		return 3;
	}

    if (!InitHotKey())
    {
        // NB - this exit code is used in OnlyM (WebDisplayManager)
        return 5;
    }

	int rv;

	const HANDLE applicationMutex = ::CreateMutex(nullptr, TRUE, "OnlyMMirrorMutex");
	if (applicationMutex && ::GetLastError() != ERROR_ALREADY_EXISTS)
	{
		hostWindow.Show(nCmdShow);
		hostWindow.Update();
		hostWindow.PositionCursor();

		TCHAR caption[64];
		(void)sprintf_s(caption, "%s (ALT+%c to close)", WindowTitle, hotKey);

		hostWindow.SetCaption(caption);
		const UINT_PTR timerId = SetTimer(hostWindow.GetWindowHandle(), 0, TimerInterval, UpdateMirrorWindow);

		MSG msg;
        while (true)
        {
            const int result = GetMessage(&msg, nullptr, 0, 0);
		
			if (result == 0)
			{
                // WM_QUIT received
				break;
			}

            if (result > 0)
            {
                if (msg.message == WM_HOTKEY)
                {
                    // we only register one hotkey
                    // so its value doesn't matter
                    break;
                }

                TranslateMessage(&msg);
                DispatchMessage(&msg);
            }
            else
            {
                break; // An error occurred                
            }
		}

		// Shut down.
		KillTimer(nullptr, timerId);
		MagUninitialize();

		// find OnlyM window and reposition cursor over it...
		hostWindow.RepositionCursor();

		rv = static_cast<int>(msg.wParam);

		CloseHandle(applicationMutex);
	}
	else
	{
		rv = 10;
	}

	return rv;
}

namespace
{
    bool InitFromCommandLine()
    {
        bool rv = FALSE;

        if (__argc >= 3)
        {
#pragma warning(suppress: 6031)
            lstrcpyn(mainMonitorName, __argv[1], MaxMonitorNameLength);
#pragma warning(suppress: 6031)
            lstrcpyn(targetMonitorName, __argv[2], MaxMonitorNameLength);
            rv = TRUE;
        }

        if (__argc >= 4)
        {
            zoomFactor = static_cast<float>(atof(__argv[3]));  // NOLINT(cert-err34-c)
            if (zoomFactor <= 0.0F)
            {
                zoomFactor = 1.0F;
            }
        }

        if (__argc >= 5)
        {
            hotKey = __argv[4][0];
        }

        return rv;
    }

    bool InitMonitors()
    {
        EnumDisplayMonitors(nullptr, nullptr, OnlyMMonitorEnumProc, 0);

        return
            mainMonitorRect.left != mainMonitorRect.right &&
            targetMonitorRect.left != targetMonitorRect.right;
    }

    bool InitHotKey()
    {
        const UINT vkCode = 0x41 + hotKey - 'A';
        return ::RegisterHotKey(nullptr, 1, MOD_ALT, vkCode);  //0x5A is 'Z'
    }

    BOOL CALLBACK OnlyMMonitorEnumProc(
        HMONITOR monitor, HDC /*monitorDeviceContext*/, LPRECT /*monitorRect*/, LPARAM /*data*/)
    {
        if (monitor)
        {
            MONITORINFOEX info{};
            info.cbSize = sizeof(MONITORINFOEX);
            ::GetMonitorInfo(monitor, &info);

            if (strcmp(info.szDevice, mainMonitorName) == 0)
            {
                mainMonitorRect = info.rcWork;
            }

            if (strcmp(info.szDevice, targetMonitorName) == 0)
            {
                targetMonitorRect = info.rcMonitor;
            }
        }

        return true;
    }

    // Sets the source rectangle and updates the window. Called by a timer.
    void CALLBACK UpdateMirrorWindow(HWND /*hostWindow*/, UINT /*message*/, UINT_PTR /*eventId*/, DWORD /*time*/)
    {
        hostWindow.UpdateMirror(targetMonitorRect);
    }

    bool SetupMirror(const HINSTANCE instance)
    {
        // 1. Calculate height of instructionsWindow
        const int instructionsHeight = InstructionsWindow::CalculateInstructionsWindowHeight();

        // 2. Set bounds of host window according to size of media monitor...
        const int mediaMonitorHeight = targetMonitorRect.bottom - targetMonitorRect.top;
        const int mediaMonitorWidth = targetMonitorRect.right - targetMonitorRect.left;

        const int hostMonitorHeight = mainMonitorRect.bottom - mainMonitorRect.top;
        const int hostMonitorWidth = mainMonitorRect.right - mainMonitorRect.left;

        // 3. Calculate the maximum zoom factor that fits in the main monitor
        const float maxZoomWidth = static_cast<float>(hostMonitorWidth) / static_cast<float>(mediaMonitorWidth);
        const float maxZoomHeight = static_cast<float>(hostMonitorHeight - instructionsHeight) / static_cast<float>(mediaMonitorHeight);
        const float maxZoom = (maxZoomWidth < maxZoomHeight) ? maxZoomWidth : maxZoomHeight;

        if (zoomFactor > maxZoom)  // NOLINT(readability-use-std-min-max)
        {
            zoomFactor = maxZoom;
        }

        // 4. Calculate the intended client area size (scaled by ZoomFactor)
        const int clientHeight = static_cast<int>(mediaMonitorHeight * zoomFactor);  // NOLINT(clang-diagnostic-implicit-int-float-conversion, bugprone-narrowing-conversions, cppcoreguidelines-narrowing-conversions)
        const int clientWidth = static_cast<int>(mediaMonitorWidth * zoomFactor); // NOLINT(clang-diagnostic-implicit-int-float-conversion, bugprone-narrowing-conversions, cppcoreguidelines-narrowing-conversions)

        // 5. Add space for instructions
        RECT windowRect = { 0, 0, clientWidth, clientHeight + instructionsHeight };
        AdjustWindowRectEx(&windowRect, HOST_WINDOW_STYLES, FALSE, HOST_WINDOW_STYLES_EX);

        const int winWidth = windowRect.right - windowRect.left;
        const int winHeight = windowRect.bottom - windowRect.top;

        // 6. Center the window in the host monitor
        int winLeft = mainMonitorRect.left + (hostMonitorWidth - winWidth) / 2;
        int winTop = mainMonitorRect.top + (hostMonitorHeight - winHeight) / 2;

        if (winLeft < mainMonitorRect.left)  // NOLINT(readability-use-std-min-max)
        {
            winLeft = mainMonitorRect.left;
        }

        if (winTop < mainMonitorRect.top) // NOLINT(readability-use-std-min-max)
        {
            winTop = mainMonitorRect.top;
        }

        return hostWindow.Create(
            instance, winLeft, winTop, winWidth, winHeight, zoomFactor, targetMonitorRect, hotKey);
    }
}