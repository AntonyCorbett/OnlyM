#include "stdafx.h"

#include <windows.h>
#include <wincodec.h>
#include <strsafe.h>
#include <magnification.h>

#include "Resource.h"

// WS_DISABLED prevents window being moved
#define HOST_WINDOW_STYLES WS_CLIPCHILDREN | WS_CAPTION | WS_DISABLED
#define HOST_WINDOW_STYLES_EX WS_EX_TOPMOST | WS_EX_TOOLWINDOW

// Global variables and strings.
HINSTANCE           hInst;
const TCHAR         WindowClassName[] = TEXT("OnlyMMirrorWindow");
const TCHAR         WindowTitle[] = TEXT("OnlyM Mirror");
const UINT          timerInterval = 60; 
HWND                hwndMag;
HWND                hwndHost;
HWND                hwndInstructions;
RECT                magWindowRect;
RECT                hostWindowRect;
int                 instructionsHeight;
HBRUSH              instructionsBrush = nullptr;

// Forward declarations.
ATOM                RegisterHostWindowClass(HINSTANCE hInstance);
BOOL                SetupMirror(HINSTANCE hInstance);
LRESULT CALLBACK    WndProc(HWND, UINT, WPARAM, LPARAM);
void CALLBACK       UpdateMirrorWindow(HWND hwnd, UINT uMsg, UINT_PTR idEvent, DWORD dwTime);
BOOL CALLBACK		OnlyMMonitorEnumProc(HMONITOR hMonitor, HDC hdcMonitor, LPRECT lprcMonitor, LPARAM dwData);
BOOL				InitMonitors();
BOOL				InitHotKey();
BOOL				InitFromCommandLine();
void				RepositionCursor();
void				PositionCursor();
HCURSOR				hCursorArrow;

RECT mainMonitorRect;
RECT targetMonitorRect;

const int MAX_MONITOR_NAME_LEN = 32;
TCHAR MainMonitor[MAX_MONITOR_NAME_LEN + 1];
TCHAR TargetMonitor[MAX_MONITOR_NAME_LEN + 1];
float ZoomFactor = 1.0F;
TCHAR HotKey = 'Z';

int APIENTRY WinMain(_In_ HINSTANCE hInstance,
	_In_opt_ HINSTANCE /*hPrevInstance*/,
	_In_ LPSTR /*lpCmdLine*/,
	_In_ int nCmdShow)
{
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

	int rv = 0;

	HANDLE hMutex = ::CreateMutex(0, TRUE, "OnlyMMirrorMutex");
	if (hMutex && ::GetLastError() != ERROR_ALREADY_EXISTS)
	{
		ShowWindow(hwndHost, nCmdShow | SW_SHOWNA);		
		UpdateWindow(hwndHost);

		PositionCursor();

		TCHAR caption[64];
		sprintf_s(caption, "%s (ALT+%c to close)", WindowTitle, HotKey);

		::SetWindowText(hwndHost, caption);
		UINT_PTR timerId = SetTimer(hwndHost, 0, timerInterval, UpdateMirrorWindow);

		MSG msg;
		BOOL bRet;
		while ((bRet = GetMessage(&msg, nullptr, 0, 0)) != 0)			
		{
			if (bRet == -1)
			{
				break;
			}

			if (msg.message == WM_HOTKEY)
			{
				// we only register one hotkey
				// so its value doesn't matter
				break;
			}

			TranslateMessage(&msg);
			DispatchMessage(&msg);
		}

		// Shut down.
		KillTimer(nullptr, timerId);
		MagUninitialize();

		// find OnlyM window and reposition cursor over it...
		RepositionCursor();

		rv = static_cast<int>(msg.wParam);

		::CloseHandle(hMutex);
	}
	else
	{
		rv = 10;
	}

	return rv;
}

// position cursor in centre of target monitor...
void PositionCursor()
{
	int width = targetMonitorRect.right - targetMonitorRect.left;
	int height = targetMonitorRect.bottom - targetMonitorRect.top;

	::SetCursorPos(targetMonitorRect.left + width / 2, targetMonitorRect.top + height / 2);
}

// position cursor back in the OnlyM window...
void RepositionCursor()
{
	const HWND hWnd = ::FindWindow(nullptr, "S o u n d B o x - O N L Y M");
	if (hWnd)
	{
		RECT r;
		if (::GetWindowRect(hWnd, &r))
		{
			int width = r.right - r.left;
			int height = r.bottom - r.top;
			::SetCursorPos(r.left + width / 2, r.top + height / 2);
		}
	}
}

BOOL InitFromCommandLine()
{
	BOOL rv = FALSE;

	if (__argc >= 3)
	{
#pragma warning(suppress: 6031)
		lstrcpyn(MainMonitor, __argv[1], MAX_MONITOR_NAME_LEN);
#pragma warning(suppress: 6031)
		lstrcpyn(TargetMonitor, __argv[2], MAX_MONITOR_NAME_LEN);
		rv = TRUE;
	}

	if (__argc >= 4)
	{
		ZoomFactor = static_cast<float>(atof(__argv[3]));
		if (ZoomFactor <= 0.0F)
		{
			ZoomFactor = 1.0F;
		}
	}

	if (__argc >= 5)
	{
		HotKey = __argv[4][0];
	}

	return rv;
}

BOOL InitMonitors()
{
	EnumDisplayMonitors(nullptr, nullptr, OnlyMMonitorEnumProc, 0);

	return
		mainMonitorRect.left != mainMonitorRect.right &&
		targetMonitorRect.left != targetMonitorRect.right;
}

BOOL InitHotKey()
{
	UINT vkCode = 0x41 + HotKey - 'A';
	return ::RegisterHotKey(nullptr, 1, MOD_ALT, vkCode);  //0x5A is 'Z'
}

BOOL CALLBACK OnlyMMonitorEnumProc(HMONITOR hMonitor, HDC /*hdcMonitor*/, LPRECT /*lprcMonitor*/, LPARAM /*dwData*/)
{
	if (hMonitor)
	{
        MONITORINFOEX info{};
		info.cbSize = sizeof(MONITORINFOEX);
		::GetMonitorInfo(hMonitor, &info);

		if (strcmp(info.szDevice, MainMonitor) == 0)
		{
			mainMonitorRect = info.rcWork;
		}

		if (strcmp(info.szDevice, TargetMonitor) == 0)
		{
			targetMonitorRect = info.rcMonitor;
		}
	}

	return true;
}

// Window procedure for the window that hosts the mirror.
static LRESULT CALLBACK HostWndProc(HWND hWnd, UINT message, WPARAM wParam, LPARAM lParam)
{
	switch (message)
	{
		case WM_SETCURSOR:
			SetCursor(nullptr);
			return TRUE;			

        case WM_DESTROY:
            if (instructionsBrush)
            {
                DeleteObject(instructionsBrush);
                instructionsBrush = nullptr;
            }
            PostQuitMessage(0);
            break;

		case WM_SIZE:
            if (hwndMag != nullptr && hwndInstructions != nullptr)
            {
                RECT clientRect;
                GetClientRect(hWnd, &clientRect);

                // Resize magnifier to fill all but bottom area
                SetWindowPos(hwndMag, nullptr,
                    0, 0,
                    clientRect.right, clientRect.bottom - instructionsHeight,
                    SWP_NOZORDER);

                // Position instructions at bottom
                SetWindowPos(hwndInstructions, nullptr,
                    0, clientRect.bottom - instructionsHeight,
                    clientRect.right, instructionsHeight,
                    SWP_NOZORDER);
            }
            break;

        case WM_CTLCOLORSTATIC:
        {
            HWND hwndStatic = (HWND)lParam;
            if (hwndStatic == hwndInstructions && instructionsBrush != nullptr)
            {
                SetBkColor((HDC)wParam, RGB(255, 255, 192)); // Match brush color
                SetTextColor((HDC)wParam, RGB(0, 0, 0));     // Black text
                return (INT_PTR)instructionsBrush;
            }
            break;
        }

		default:
			return DefWindowProc(hWnd, message, wParam, lParam);
	}

	return 0;
}

//  Registers the window class for the window that contains the magnification control.
ATOM RegisterHostWindowClass(HINSTANCE hInstance)
{
	WNDCLASSEX wcex = {};

	wcex.cbSize = sizeof(WNDCLASSEX);
	wcex.style = CS_HREDRAW | CS_VREDRAW;
	wcex.lpfnWndProc = HostWndProc;
	wcex.hInstance = hInstance;
	wcex.hCursor = LoadCursor(nullptr, IDC_ARROW);
	wcex.hbrBackground = (HBRUSH)(1 + COLOR_BTNFACE);
	wcex.lpszClassName = WindowClassName;

	return RegisterClassEx(&wcex);
}

// Sets the source rectangle and updates the window. Called by a timer.
void CALLBACK UpdateMirrorWindow(HWND /*hwnd*/, UINT /*uMsg*/, UINT_PTR /*idEvent*/, DWORD /*dwTime*/)
{
    // Always show the full target monitor area
    const RECT sourceRect = targetMonitorRect;

    // Set the source rectangle for the magnifier control.
    MagSetWindowSource(hwndMag, sourceRect);

    // Reclaim topmost status, to prevent non-mirrored menus from remaining in view. 
    SetWindowPos(hwndHost, HWND_TOPMOST, 0, 0, 0, 0,
        SWP_NOACTIVATE | SWP_NOMOVE | SWP_NOSIZE);

    // Force redraw.
    InvalidateRect(hwndMag, nullptr, TRUE);
}

BOOL SetupMirror(HINSTANCE hinst)
{
    // 1. Calculate font and instructionsHeight FIRST
    int fontPointSize = 24;
    HDC hdcScreen = GetDC(nullptr);
    int fontHeight = -MulDiv(fontPointSize, GetDeviceCaps(hdcScreen, LOGPIXELSY), 72);
    ReleaseDC(nullptr, hdcScreen);

    HFONT hFont = CreateFont(
        fontHeight, 0, 0, 0, FW_NORMAL, FALSE, FALSE, FALSE,
        ANSI_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS,
        DEFAULT_QUALITY, DEFAULT_PITCH | FF_DONTCARE, TEXT("Segoe UI"));

    instructionsHeight = 3 * abs(fontHeight);

    // 2. Set bounds of host window according to size of media monitor...
    int mediaMonitorHeight = targetMonitorRect.bottom - targetMonitorRect.top;
    int mediaMonitorWidth = targetMonitorRect.right - targetMonitorRect.left;

    int hostMonitorHeight = mainMonitorRect.bottom - mainMonitorRect.top;
    int hostMonitorWidth = mainMonitorRect.right - mainMonitorRect.left;

    // 3. Calculate the maximum zoom factor that fits in the main monitor
    float maxZoomWidth = (float)hostMonitorWidth / (float)mediaMonitorWidth;
    float maxZoomHeight = (float)(hostMonitorHeight - instructionsHeight) / (float)mediaMonitorHeight;
    float maxZoom = (maxZoomWidth < maxZoomHeight) ? maxZoomWidth : maxZoomHeight;

    if (ZoomFactor > maxZoom)
    {
        ZoomFactor = maxZoom;
    }

    // 4. Calculate the intended client area size (scaled by ZoomFactor)
    int clientHeight = static_cast<int>(mediaMonitorHeight * ZoomFactor);
    int clientWidth = static_cast<int>(mediaMonitorWidth * ZoomFactor);

    // 5. Add space for instructions
    RECT windowRect = { 0, 0, clientWidth, clientHeight + instructionsHeight };
    AdjustWindowRectEx(&windowRect, HOST_WINDOW_STYLES, FALSE, HOST_WINDOW_STYLES_EX);

    int winWidth = windowRect.right - windowRect.left;
    int winHeight = windowRect.bottom - windowRect.top;

    // 6. Center the window in the host monitor
    int winLeft = mainMonitorRect.left + (hostMonitorWidth - winWidth) / 2;
    int winTop = mainMonitorRect.top + (hostMonitorHeight - winHeight) / 2;

    if (winLeft < mainMonitorRect.left)
        winLeft = mainMonitorRect.left;
    if (winTop < mainMonitorRect.top)
        winTop = mainMonitorRect.top;

    // 7. Create the host window
    RegisterHostWindowClass(hinst);
    hwndHost = CreateWindowEx(
        HOST_WINDOW_STYLES_EX,
        WindowClassName, WindowTitle,
        HOST_WINDOW_STYLES,
        winLeft, winTop, winWidth, winHeight,
        nullptr, nullptr, hInst, nullptr);

    if (!hwndHost)
    {
        return FALSE;
    }

    // 8. Get client area rect
    RECT clientRect;
    GetClientRect(hwndHost, &clientRect);

    // 9. Create magnifier control (positioned at top)
    hwndMag = CreateWindow(
        WC_MAGNIFIER, TEXT("MagnifierWindow"),
        WS_CHILD | MS_SHOWMAGNIFIEDCURSOR | WS_VISIBLE,
        0, 0,
        clientRect.right, clientRect.bottom - instructionsHeight,
        hwndHost, nullptr, hInst, nullptr);

    if (!hwndMag)
    {
        return FALSE;
    }

    // 10. Create instructions static control (positioned at bottom)
    hwndInstructions = CreateWindow(
        TEXT("STATIC"), TEXT("Mirror Window - Press ALT+Z to close"),
        WS_CHILD | WS_VISIBLE | SS_CENTER | SS_CENTERIMAGE,
        0, clientRect.bottom - instructionsHeight,
        clientRect.right, instructionsHeight,
        hwndHost, (HMENU)IDC_INSTRUCTIONS, hInst, nullptr);

    if (!hwndInstructions)
    {
        return FALSE;
    }

    // 11. Set background brush for instructions
    if (instructionsBrush == nullptr)
    {
        instructionsBrush = CreateSolidBrush(RGB(255, 255, 192)); // Light yellow
    }

    // 12. Set the font for the instructions window
    SendMessage(hwndInstructions, WM_SETFONT, (WPARAM)hFont, TRUE);

    // 13. Set the magnification factor
    MAGTRANSFORM matrix = {};
    matrix.v[0][0] = ZoomFactor;
    matrix.v[1][1] = ZoomFactor;
    matrix.v[2][2] = 1.0f;

    return MagSetWindowTransform(hwndMag, &matrix);
}