#include "stdafx.h"

#include <windows.h>
#include <wincodec.h>
#include <strsafe.h>
#include <magnification.h>

#define HOST_WINDOW_STYLES WS_CLIPCHILDREN | WS_CAPTION
#define HOST_WINDOW_STYLES_EX WS_EX_TOPMOST | WS_EX_TOOLWINDOW

// Global variables and strings.
HINSTANCE           hInst;
const TCHAR         WindowClassName[] = TEXT("OnlyMMirrorWindow");
const TCHAR         WindowTitle[] = TEXT("OnlyM Mirror (ALT-Z to close)");
const UINT          timerInterval = 60; 
HWND                hwndMag;
HWND                hwndHost;
RECT                magWindowRect;
RECT                hostWindowRect;

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

	int rv = 0;

	HANDLE hMutex = ::CreateMutex(0, TRUE, "OnlyMMirrorMutex");
	if (hMutex && ::GetLastError() != ERROR_ALREADY_EXISTS)
	{
		ShowWindow(hwndHost, nCmdShow | SW_SHOWNA);		
		UpdateWindow(hwndHost);

		PositionCursor();

		if (!InitHotKey())
		{
			MessageBox(NULL, "Could not register hotkey", "OnlyM Mirror", MB_OK);
			rv = 5;
		}
		else
		{
			UINT_PTR timerId = SetTimer(hwndHost, 0, timerInterval, UpdateMirrorWindow);

			MSG msg;
			while (GetMessage(&msg, NULL, 0, 0))
			{
				if (msg.message == WM_HOTKEY)
				{
					break;
				}

				TranslateMessage(&msg);
				DispatchMessage(&msg);
			}

			// Shut down.
			KillTimer(NULL, timerId);
			MagUninitialize();

			// find OnlyM window and reposition cursor over it...
			RepositionCursor();

			rv = (int)msg.wParam;
		}

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
	HWND hWnd = ::FindWindow(NULL, "S o u n d B o x - O N L Y M");
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
		lstrcpyn(MainMonitor, __argv[1], MAX_MONITOR_NAME_LEN);
		lstrcpyn(TargetMonitor, __argv[2], MAX_MONITOR_NAME_LEN);
		rv = TRUE;
	}

	if (__argc >= 4)
	{
		ZoomFactor = (float)atof(__argv[3]);
		if (ZoomFactor == 0.0F)
		{
			ZoomFactor = 1.0F;
		}
	}

	return rv;
}

BOOL InitMonitors()
{
	EnumDisplayMonitors(NULL, NULL, OnlyMMonitorEnumProc, 0);

	return
		mainMonitorRect.left != mainMonitorRect.right &&
		targetMonitorRect.left != targetMonitorRect.right;
}

BOOL InitHotKey()
{
	return ::RegisterHotKey(NULL, 1, MOD_ALT, 0x5A);  //0x5A is 'Z'
}

BOOL CALLBACK OnlyMMonitorEnumProc(HMONITOR hMonitor, HDC /*hdcMonitor*/, LPRECT /*lprcMonitor*/, LPARAM /*dwData*/)
{
	if (hMonitor)
	{
		MONITORINFOEX info;
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
LRESULT CALLBACK HostWndProc(HWND hWnd, UINT message, WPARAM wParam, LPARAM lParam)
{
	switch (message)
	{
		case WM_SETCURSOR:
			if (LOWORD(lParam) == HTCAPTION)
			{
				SetCursor(hCursorArrow);
				return TRUE;
			}

			if (LOWORD(lParam) == HTCLIENT)
			{
				// prevent cursor showing (confusing!)
				SetCursor(NULL);
				return TRUE;
			}
			break;

		case WM_DESTROY:
			PostQuitMessage(0);
			break;

		case WM_SIZE:
			if (hwndMag != NULL)
			{
				GetClientRect(hWnd, &magWindowRect);
				// Resize the control to fill the window.
				SetWindowPos(hwndMag, NULL, magWindowRect.left, magWindowRect.top, magWindowRect.right, magWindowRect.bottom, 0);
			}
			break;

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
	wcex.hCursor = LoadCursor(NULL, IDC_ARROW);
	wcex.hbrBackground = (HBRUSH)(1 + COLOR_BTNFACE);
	wcex.lpszClassName = WindowClassName;

	return RegisterClassEx(&wcex);
}

// Creates the windows and initializes mirror.
BOOL SetupMirror(HINSTANCE hinst)
{
	// Set bounds of host window according to size of media monitor...
	int mediaMonitorHeight = targetMonitorRect.bottom - targetMonitorRect.top;
	int mediaMonitorWidth = targetMonitorRect.right - targetMonitorRect.left;

	int hostMonitorHeight = mainMonitorRect.bottom - mainMonitorRect.top;
	int hostMonitorWidth = mainMonitorRect.right - mainMonitorRect.left;

	// limit the size of the host...
	int maxHostHeight = hostMonitorHeight - (hostMonitorHeight / 20);
	int maxHostWidth = hostMonitorWidth - (hostMonitorWidth / 20);
			
	// calculate the proposed host rect
	hostWindowRect.top = mainMonitorRect.top;
	hostWindowRect.bottom = hostWindowRect.top + mediaMonitorHeight;
	hostWindowRect.left = mainMonitorRect.left;
	hostWindowRect.right = hostWindowRect.left + mediaMonitorWidth;
	
	// adjust hostWindowRect to get external size of the window required for the specified client rect
	::AdjustWindowRectEx(&hostWindowRect, HOST_WINDOW_STYLES, FALSE, HOST_WINDOW_STYLES_EX);

	int winHeight = hostWindowRect.bottom - hostWindowRect.top;
	int winWidth = hostWindowRect.right - hostWindowRect.left;

	if (winHeight > maxHostHeight)
	{
		winHeight = maxHostHeight;
	}

	if (winWidth > maxHostWidth)
	{
		winWidth = maxHostWidth;
	}

	hostWindowRect.top = mainMonitorRect.top + ((hostMonitorHeight - winHeight) / 2);
	hostWindowRect.bottom = hostWindowRect.top + winHeight;
	hostWindowRect.left = mainMonitorRect.left + ((hostMonitorWidth - winWidth) / 2);
	hostWindowRect.right = hostWindowRect.left + winWidth;

	// Create the host window.
	RegisterHostWindowClass(hinst);
	hwndHost = CreateWindowEx(HOST_WINDOW_STYLES_EX,
		WindowClassName, WindowTitle,
		HOST_WINDOW_STYLES,
		hostWindowRect.left, hostWindowRect.top, hostWindowRect.right - hostWindowRect.left, hostWindowRect.bottom - hostWindowRect.top,
		NULL, NULL, hInst, NULL);

	if (!hwndHost)
	{
		return FALSE;
	}

	hCursorArrow = LoadCursor(NULL, IDC_ARROW);

	// Make the window opaque...
	SetLayeredWindowAttributes(hwndHost, 0, 255, LWA_ALPHA);

	// Create a magnifier control that fills the client area.
	GetClientRect(hwndHost, &magWindowRect);
	hwndMag = CreateWindow(WC_MAGNIFIER, TEXT("MagnifierWindow"),
		WS_CHILD | MS_SHOWMAGNIFIEDCURSOR | WS_VISIBLE,
		magWindowRect.left, magWindowRect.top, magWindowRect.right, magWindowRect.bottom, hwndHost, NULL, hInst, NULL);

	if (!hwndMag)
	{
		return FALSE;
	}

	// Set the magnification factor.
	MAGTRANSFORM matrix;
	memset(&matrix, 0, sizeof(matrix));
	matrix.v[0][0] = ZoomFactor;
	matrix.v[1][1] = ZoomFactor;
	matrix.v[2][2] = 1.0f;

	BOOL ret = MagSetWindowTransform(hwndMag, &matrix);

	return ret;
}

// Sets the source rectangle and updates the window. Called by a timer.
void CALLBACK UpdateMirrorWindow(HWND /*hwnd*/, UINT /*uMsg*/, UINT_PTR /*idEvent*/, DWORD /*dwTime*/)
{
	POINT mousePoint;
	GetCursorPos(&mousePoint);

	if (mousePoint.x >= targetMonitorRect.left && mousePoint.x <= targetMonitorRect.right &&
		mousePoint.y >= targetMonitorRect.top && mousePoint.y <= targetMonitorRect.bottom)
	{
		int width = (int)((magWindowRect.right - magWindowRect.left) / ZoomFactor);
		int height = (int)((magWindowRect.bottom - magWindowRect.top) / ZoomFactor);

		RECT sourceRect;
		sourceRect.left = mousePoint.x - width / 2;
		sourceRect.top = mousePoint.y - height / 2;

		if (sourceRect.left < targetMonitorRect.left)
		{
			sourceRect.left = targetMonitorRect.left;
		}

		if (sourceRect.left > targetMonitorRect.right - width)
		{
			sourceRect.left = targetMonitorRect.right - width;
		}

		sourceRect.right = sourceRect.left + width;

		if (sourceRect.top < targetMonitorRect.top)
		{
			sourceRect.top = targetMonitorRect.top;
		}

		if (sourceRect.top > targetMonitorRect.bottom - height)
		{
			sourceRect.top = targetMonitorRect.bottom - height;
		}

		sourceRect.bottom = sourceRect.top + height;

		// Set the source rectangle for the magnifier control.
		MagSetWindowSource(hwndMag, sourceRect);

		// Reclaim topmost status, to prevent non-mirrrored menus from remaining in view. 
		SetWindowPos(hwndHost, HWND_TOPMOST, 0, 0, 0, 0,
			SWP_NOACTIVATE | SWP_NOMOVE | SWP_NOSIZE);

		// Force redraw.
		InvalidateRect(hwndMag, NULL, TRUE);
	}
}
