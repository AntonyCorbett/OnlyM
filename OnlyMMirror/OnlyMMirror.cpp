#include "stdafx.h"

#include <windows.h>
#include <d3d11.h>
#include <dxgi1_2.h>
#include <wincodec.h>
#include <strsafe.h>

#include "Resource.h"

#pragma comment(lib, "d3d11.lib")
#pragma comment(lib, "dxgi.lib")


// WS_DISABLED prevents window being moved
#define HOST_WINDOW_STYLES (WS_CLIPCHILDREN | WS_CAPTION | WS_DISABLED)
#define HOST_WINDOW_STYLES_EX (WS_EX_TOPMOST | WS_EX_TOOLWINDOW)

// Global variables and strings.
constexpr TCHAR WindowClassName[] = TEXT("OnlyMMirrorWindow");
constexpr TCHAR WindowTitle[] = TEXT("OnlyM Mirror");
constexpr UINT TimerInterval = 60;
constexpr int MaxMonitorNameLength = 32;

namespace
{
    HINSTANCE applicationInstance;    
    HWND hostWindow;
    HWND instructionsWindow;
    int instructionsHeight;
    HBRUSH instructionsBrush = nullptr;
    RECT mainMonitorRect;
    RECT targetMonitorRect;
    TCHAR mainMonitorName[MaxMonitorNameLength + 1];
    TCHAR targetMonitorName[MaxMonitorNameLength + 1];
    float zoomFactor = 1.0F;
    TCHAR hotKey = 'Z';

    ID3D11Device* g_d3dDevice = nullptr;
    ID3D11DeviceContext* g_d3dContext = nullptr;
    IDXGIOutputDuplication* g_deskDupl = nullptr;
    int GetMonitorIndexForName(const char* deviceName);
}

// Forward declarations.
namespace
{
    ATOM RegisterHostWindowClass(HINSTANCE instance);
    bool SetupMirror(HINSTANCE instance);
    void CALLBACK UpdateMirrorWindow(HWND /*hostWindow*/, UINT /*message*/, UINT_PTR /*eventId*/, DWORD /*time*/);
    BOOL CALLBACK OnlyMMonitorEnumProc(HMONITOR monitor, HDC monitorDeviceContext, LPRECT monitorRect, LPARAM data);
    bool InitMonitors();
    bool InitHotKey();
    bool InitFromCommandLine();
    void RepositionCursor();
    void PositionCursor();    
    ID3D11Texture2D* AcquireNextFrame();
    void BlitFrameToWindow(ID3D11Texture2D* frame, HWND hwnd);
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
		ShowWindow(hostWindow, nCmdShow | SW_SHOWNA);		
		UpdateWindow(hostWindow);

		PositionCursor();

		TCHAR caption[64];
		(void)sprintf_s(caption, "%s (ALT+%c to close)", WindowTitle, hotKey);

		::SetWindowText(hostWindow, caption);
		const UINT_PTR timerId = SetTimer(hostWindow, 0, TimerInterval, UpdateMirrorWindow);
        
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

		// find OnlyM window and reposition cursor over it...
		RepositionCursor();

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
    // position cursor in centre of target monitor...
    void PositionCursor()
    {
        const int width = targetMonitorRect.right - targetMonitorRect.left;
        const int height = targetMonitorRect.bottom - targetMonitorRect.top;

        SetCursorPos(targetMonitorRect.left + width / 2, targetMonitorRect.top + height / 2);
    }

    // position cursor back in the OnlyM window...
    void RepositionCursor()
    {
        const HWND window = ::FindWindow(nullptr, "S o u n d B o x - O N L Y M");
        if (window)
        {
            RECT r;
            if (GetWindowRect(window, &r))
            {
                const int width = r.right - r.left;
                const int height = r.bottom - r.top;
                SetCursorPos(r.left + width / 2, r.top + height / 2);
            }
        }
    }

    bool InitFromCommandLine()
    {
        lstrcpyn(mainMonitorName, TEXT("\\\\.\\DISPLAY2"), MaxMonitorNameLength);
        lstrcpyn(targetMonitorName, TEXT("\\\\.\\DISPLAY1"), MaxMonitorNameLength);
        zoomFactor = 1.0F;  // Default zoom factor
        hotKey = 'Z';  // Default hotkey
        return true;

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

    BOOL CALLBACK OnlyMMonitorEnumProc(HMONITOR monitor, HDC /*monitorDeviceContext*/, LPRECT /*monitorRect*/, LPARAM /*data*/)
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

    // Window procedure for the window that hosts the mirror.
    LRESULT CALLBACK HostWndProc(HWND window, UINT message, WPARAM wParam, LPARAM lParam)
    {
        switch (message)
        {
        case WM_PAINT:
        {
            PAINTSTRUCT ps;
            HDC hdc = BeginPaint(window, &ps);

            ID3D11Texture2D* frame = AcquireNextFrame();
            if (frame)
            {
                BlitFrameToWindow(frame, window);
                frame->Release();
            }

            EndPaint(window, &ps);
            return 0;
        }

        case WM_SETCURSOR:
            SetCursor(nullptr);
            return TRUE;

        case WM_DESTROY:
            if (instructionsBrush)
            {
                DeleteObject(instructionsBrush);
                instructionsBrush = nullptr;
            }

            if (g_deskDupl) { g_deskDupl->Release(); g_deskDupl = nullptr; }
            if (g_d3dContext) { g_d3dContext->Release(); g_d3dContext = nullptr; }
            if (g_d3dDevice) { g_d3dDevice->Release(); g_d3dDevice = nullptr; }

            PostQuitMessage(0);
            break;

        case WM_SIZE:
            if (instructionsWindow != nullptr)
            {
                RECT clientRect;
                GetClientRect(window, &clientRect);

                // Position instructions at bottom
                SetWindowPos(instructionsWindow, nullptr,
                    0, clientRect.bottom - instructionsHeight,
                    clientRect.right, instructionsHeight,
                    SWP_NOZORDER);
            }
            break;

        case WM_CTLCOLORSTATIC:
        {
            const HWND controlWindow = reinterpret_cast<HWND>(lParam);  // NOLINT(performance-no-int-to-ptr)
            if (controlWindow == instructionsWindow && instructionsBrush != nullptr)
            {
                const auto hdc = reinterpret_cast<HDC>(wParam);  // NOLINT(performance-no-int-to-ptr)
                SetBkColor(hdc, RGB(255, 255, 192)); // Match brush color
                SetTextColor(hdc, RGB(0, 0, 0));     // Black text
                return reinterpret_cast<INT_PTR>(instructionsBrush);
            }
            break;
        }

        default:
            return DefWindowProc(window, message, wParam, lParam);
        }

        return DefWindowProc(window, message, wParam, lParam);
    }

    //  Registers the window class for the window that contains the magnification control.
    ATOM RegisterHostWindowClass(const HINSTANCE instance)
    {
        WNDCLASSEX windowClassEx = {};

        windowClassEx.cbSize = sizeof(WNDCLASSEX);
        windowClassEx.style = CS_HREDRAW | CS_VREDRAW;
        windowClassEx.lpfnWndProc = HostWndProc;
        windowClassEx.hInstance = instance;
        windowClassEx.hCursor = LoadCursor(nullptr, IDC_ARROW);
        //windowClassEx.hbrBackground = reinterpret_cast<HBRUSH>(1 + COLOR_BTNFACE);  // NOLINT(performance-no-int-to-ptr)
        windowClassEx.lpszClassName = WindowClassName;
        windowClassEx.hbrBackground = nullptr;

        return RegisterClassEx(&windowClassEx);
    }

    // Sets the source rectangle and updates the window. Called by a timer.
    void CALLBACK UpdateMirrorWindow(HWND /*hostWindow*/, UINT /*message*/, UINT_PTR /*eventId*/, DWORD /*time*/)
    {
        // Reclaim topmost status, to prevent non-mirrored menus from remaining in view. 
        SetWindowPos(hostWindow, HWND_TOPMOST, 0, 0, 0, 0,
            SWP_NOACTIVATE | SWP_NOMOVE | SWP_NOSIZE);

        // Force redraw.
        InvalidateRect(hostWindow, nullptr, false);
    }

    void BlitFrameToWindow(ID3D11Texture2D* frame, HWND hwnd)
    {
        if (!frame) return;

        // Get frame description
        D3D11_TEXTURE2D_DESC desc;
        frame->GetDesc(&desc);

        // Create staging texture
        D3D11_TEXTURE2D_DESC stagingDesc = desc;
        stagingDesc.Usage = D3D11_USAGE_STAGING;
        stagingDesc.BindFlags = 0;
        stagingDesc.CPUAccessFlags = D3D11_CPU_ACCESS_READ;
        stagingDesc.MiscFlags = 0;

        ID3D11Texture2D* staging = nullptr;
        g_d3dDevice->CreateTexture2D(&stagingDesc, nullptr, &staging);

        // Copy frame to staging
        g_d3dContext->CopyResource(staging, frame);

        // Map and get data
        D3D11_MAPPED_SUBRESOURCE mapped;
        if (SUCCEEDED(g_d3dContext->Map(staging, 0, D3D11_MAP_READ, 0, &mapped)))
        {
            // Check if the first pixel is non-black
            unsigned char* p = (unsigned char*)mapped.pData;
            char buf[128];
            sprintf_s(buf, "First pixel: B=%d G=%d R=%d A=%d\n", p[0], p[1], p[2], p[3]);
            OutputDebugStringA(buf);

            // Get the window's client area size
            RECT clientRect;
            GetClientRect(hwnd, &clientRect);
            int winWidth = clientRect.right - clientRect.left;
            int winHeight = clientRect.bottom - clientRect.top;

            // Blit to window using GDI, scaling to fit the client area
            HDC hdc = GetDC(hwnd);
            BITMAPINFO bmi = {};
            bmi.bmiHeader.biSize = sizeof(BITMAPINFOHEADER);
            bmi.bmiHeader.biWidth = desc.Width;
            bmi.bmiHeader.biHeight = -LONG(desc.Height); // top-down
            bmi.bmiHeader.biPlanes = 1;
            bmi.bmiHeader.biBitCount = 32;
            bmi.bmiHeader.biCompression = BI_RGB;

            StretchDIBits(
                hdc,
                0, 0, winWidth, winHeight, // destination: full client area
                0, 0, desc.Width, desc.Height, // source: full captured frame
                mapped.pData, &bmi, DIB_RGB_COLORS, SRCCOPY);

            ReleaseDC(hwnd, hdc);
            g_d3dContext->Unmap(staging, 0);
        }
        staging->Release();
    }

    ID3D11Texture2D* AcquireNextFrame()
    {
        if (!g_deskDupl) return nullptr;

        DXGI_OUTDUPL_FRAME_INFO frameInfo;
        IDXGIResource* desktopResource = nullptr;
        HRESULT hr = g_deskDupl->AcquireNextFrame(16, &frameInfo, &desktopResource);
        if (hr == DXGI_ERROR_WAIT_TIMEOUT)
        {
            // No new frame, not an error
            return nullptr;
        }

        if (FAILED(hr)) 
        {
            char buf[128];
            sprintf_s(buf, "AcquireNextFrame failed: 0x%08X\n", hr);
            OutputDebugStringA(buf);
            return nullptr;
        }

        ID3D11Texture2D* frame = nullptr;
        desktopResource->QueryInterface(__uuidof(ID3D11Texture2D), (void**)&frame);
        desktopResource->Release();
        g_deskDupl->ReleaseFrame();

        return frame; // Caller must Release() when done
    }

    struct AdapterOutputIndex
    {
        int adapterIndex;
        int outputIndex;
    };

    AdapterOutputIndex GetAdapterOutputIndexForName(const char* deviceName)
    {
        IDXGIFactory1* pFactory = nullptr;
        if (FAILED(CreateDXGIFactory1(__uuidof(IDXGIFactory1), (void**)&pFactory)))
            return { 0, 0 };

        AdapterOutputIndex result = { 0, 0 };
        IDXGIAdapter1* pAdapter = nullptr;
        for (UINT adapterIndex = 0;
            pFactory->EnumAdapters1(adapterIndex, &pAdapter) != DXGI_ERROR_NOT_FOUND;
            ++adapterIndex)
        {
            IDXGIOutput* pOutput = nullptr;
            for (UINT outputIndex = 0;
                pAdapter->EnumOutputs(outputIndex, &pOutput) != DXGI_ERROR_NOT_FOUND;
                ++outputIndex)
            {
                DXGI_OUTPUT_DESC desc;
                if (SUCCEEDED(pOutput->GetDesc(&desc)))
                {
                    char outputName[32];
                    size_t converted = 0;
                    wcstombs_s(&converted, outputName, desc.DeviceName, _TRUNCATE);
                    if (strcmp(outputName, deviceName) == 0)
                    {
                        pOutput->Release();
                        pAdapter->Release();
                        pFactory->Release();
                        return { (int)adapterIndex, (int)outputIndex };
                    }
                }
                pOutput->Release();
            }
            pAdapter->Release();
        }
        pFactory->Release();
        return { 0, 0 }; // fallback
    }

    bool InitDirectXDuplication(int adapterIndex, int outputIndex)
    {
        HRESULT hr;
        IDXGIFactory1* pFactory = nullptr;
        hr = CreateDXGIFactory1(__uuidof(IDXGIFactory1), (void**)&pFactory);
        if (FAILED(hr)) return false;

        IDXGIAdapter1* pAdapter = nullptr;
        hr = pFactory->EnumAdapters1(adapterIndex, &pAdapter);
        pFactory->Release();
        if (FAILED(hr)) return false;

        // *** CRITICAL: Use pAdapter and D3D_DRIVER_TYPE_UNKNOWN ***
        hr = D3D11CreateDevice(
            pAdapter, D3D_DRIVER_TYPE_UNKNOWN, nullptr, 0, nullptr, 0,
            D3D11_SDK_VERSION, &g_d3dDevice, nullptr, &g_d3dContext);
        if (FAILED(hr)) { pAdapter->Release(); return false; }

        IDXGIOutput* dxgiOutput = nullptr;
        hr = pAdapter->EnumOutputs(outputIndex, &dxgiOutput);
        pAdapter->Release();
        if (FAILED(hr)) return false;

        IDXGIOutput1* dxgiOutput1 = nullptr;
        hr = dxgiOutput->QueryInterface(__uuidof(IDXGIOutput1), (void**)&dxgiOutput1);
        dxgiOutput->Release();
        if (FAILED(hr)) return false;

        hr = dxgiOutput1->DuplicateOutput(g_d3dDevice, &g_deskDupl);
        dxgiOutput1->Release();
        if (FAILED(hr)) return false;

        return true;
    }

    bool SetupMirror(const HINSTANCE instance)
    {
        // 1. Calculate font and instructionsHeight FIRST
        constexpr int fontPointSize = 24;
        const HDC hdcScreen = GetDC(nullptr);
        const int fontHeight = -MulDiv(fontPointSize, GetDeviceCaps(hdcScreen, LOGPIXELSY), 72);
        ReleaseDC(nullptr, hdcScreen);

        HFONT hFont = CreateFont(
            fontHeight, 0, 0, 0, FW_NORMAL, FALSE, FALSE, FALSE,
            ANSI_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS,
            DEFAULT_QUALITY, DEFAULT_PITCH, TEXT("Segoe UI"));

        instructionsHeight = 3 * abs(fontHeight);

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

        // 7. Create the host window
        RegisterHostWindowClass(instance);
        hostWindow = CreateWindowEx(
            HOST_WINDOW_STYLES_EX,
            WindowClassName, 
            WindowTitle,
            HOST_WINDOW_STYLES,
            winLeft, winTop, winWidth, winHeight,
            nullptr, 
            nullptr, 
            applicationInstance, 
            nullptr);

        if (!hostWindow)
        {
            return FALSE;
        }

        AdapterOutputIndex idx = GetAdapterOutputIndexForName(targetMonitorName);
        InitDirectXDuplication(idx.adapterIndex, idx.outputIndex);

        // 8. Get client area rect
        RECT clientRect;
        GetClientRect(hostWindow, &clientRect);

        // 10. Create instructions static control (positioned at bottom)
        instructionsWindow = CreateWindow(
            TEXT("STATIC"), 
            TEXT("Mirror Window - Press ALT+Z to close"),
            WS_CHILD | WS_VISIBLE | SS_CENTER | SS_CENTERIMAGE,
            0, clientRect.bottom - instructionsHeight, clientRect.right, instructionsHeight,
            hostWindow, 
            reinterpret_cast<HMENU>(IDC_INSTRUCTIONS),
            applicationInstance, 
            nullptr);

        if (!instructionsWindow)
        {
            return FALSE;
        }

        // 11. Set background brush for instructions
        if (instructionsBrush == nullptr)
        {
            instructionsBrush = CreateSolidBrush(RGB(255, 255, 192)); // Light yellow
        }

        // 12. Set the font for the instructions window
        SendMessage(instructionsWindow, WM_SETFONT, reinterpret_cast<WPARAM>(hFont), TRUE);

        return true;
    }
}