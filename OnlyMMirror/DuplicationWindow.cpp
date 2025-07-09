#include "stdafx.h"
#include "DuplicationWindow.h"
#include <d3dcompiler.h>
#include <string>
#include <wingdi.h>
#include <iostream>

#pragma comment(lib, "d3d11.lib")
#pragma comment(lib, "dxgi.lib")
#pragma comment(lib, "d3dcompiler.lib")

// Simple vertex shader for rendering the captured texture
const char* vertexShaderSource = R"(
struct VS_INPUT {
    float4 pos : POSITION;
    float2 tex : TEXCOORD0;
};

struct VS_OUTPUT {
    float4 pos : SV_POSITION;
    float2 tex : TEXCOORD0;
};

VS_OUTPUT main(VS_INPUT input) {
    VS_OUTPUT output;
    output.pos = input.pos;
    output.tex = input.tex;
    return output;
}
)";

// Simple pixel shader for rendering with high-quality filtering
const char* pixelShaderSource = R"(
Texture2D shaderTexture : register(t0);
SamplerState samplerType : register(s0);

struct PS_INPUT {
    float4 pos : SV_POSITION;
    float2 tex : TEXCOORD0;
};

float4 main(PS_INPUT input) : SV_TARGET {
    return shaderTexture.Sample(samplerType, input.tex);
}
)";

// Vertex structure for rendering
struct Vertex {
    float x, y, z, w;  // Position
    float u, v;        // Texture coordinates
};

const TCHAR* DuplicationWindow::GetWindowClassName() { 
    return TEXT("DuplicationWindow"); 
}

DuplicationWindow::DuplicationWindow() 
    : windowHandle_(nullptr), hInstance_(nullptr), d3dDevice_(nullptr), 
      d3dContext_(nullptr), swapChain_(nullptr), renderTargetView_(nullptr),
      capturedTexture_(nullptr), capturedSRV_(nullptr), samplerState_(nullptr),
      duplication_(nullptr), output_(nullptr), vertexShader_(nullptr), 
      pixelShader_(nullptr), vertexBuffer_(nullptr), inputLayout_(nullptr),
      mouseTexture_(nullptr), mouseSRV_(nullptr), mouseVertexBuffer_(nullptr),
      mouseVisible_(false), mouseWidth_(0), mouseHeight_(0),
      zoomFactor_(1.0f), windowWidth_(0), windowHeight_(0)
{
    ZeroMemory(&sourceRect_, sizeof(sourceRect_));
    ZeroMemory(&mousePosition_, sizeof(mousePosition_));
    ZeroMemory(&targetMonitorRect_, sizeof(targetMonitorRect_));
}

DuplicationWindow::~DuplicationWindow() { 
    Destroy(); 
}

bool DuplicationWindow::Create(HWND parent, HINSTANCE instance, int x, int y, int width, int height, const char* targetMonitorName)
{
    Destroy();
    hInstance_ = instance;
    windowWidth_ = width;
    windowHeight_ = height;
    if (targetMonitorName) {
        targetMonitorName_ = targetMonitorName;
    }
    WNDCLASSEX wc = {};
    wc.cbSize = sizeof(WNDCLASSEX);
    wc.style = CS_HREDRAW | CS_VREDRAW;
    wc.lpfnWndProc = WindowProc;
    wc.hInstance = instance;
    wc.hCursor = LoadCursor(nullptr, IDC_ARROW);
    wc.hbrBackground = (HBRUSH)(COLOR_WINDOW + 1);
    wc.lpszClassName = GetWindowClassName();
    RegisterClassEx(&wc);
    windowHandle_ = CreateWindowEx(
        0,
        GetWindowClassName(),
        TEXT("DuplicationWindow"),
        WS_CHILD | WS_VISIBLE,
        x, y, width, height,
        parent,
        nullptr,
        instance,
        this);
    if (!windowHandle_) {
        return false;
    }
    if (!InitializeDX()) {
        return false;
    }
    if (!InitializeDuplication()) {
        return false;
    }
    return true;
}

void DuplicationWindow::Destroy()
{
    CleanupDuplication();
    CleanupDX();
    if (windowHandle_) {
        DestroyWindow(windowHandle_);
        windowHandle_ = nullptr;
    }
}

HWND DuplicationWindow::GetWindowHandle() const { 
    return windowHandle_; 
}

void DuplicationWindow::SetSourceRect(const RECT& rect)
{
    sourceRect_ = rect;

    // Store the target monitor rect for mouse coordinate conversion
    targetMonitorRect_ = rect;    
}

bool DuplicationWindow::SetTransform(float zoomFactor)
{
    zoomFactor_ = zoomFactor;
    return true;
}

void DuplicationWindow::Resize(int x, int y, int width, int height) const
{
    if (windowHandle_) {
        SetWindowPos(windowHandle_, nullptr, x, y, width, height, SWP_NOZORDER);
    }
}

bool DuplicationWindow::UpdateFrame()
{
    // Try to capture a new frame (may reuse existing if no new frame available)
    bool hasCapturedContent = CaptureFrame();
    // Get current mouse pointer information
    GetMousePointerInfo();
    // Always try to render if we have content, regardless of timing
    if (hasCapturedContent) {
        return RenderFrame();
    }
    return false;
}

bool DuplicationWindow::InitializeDX()
{
    HRESULT hr;

    // Create D3D11 device and context
    D3D_FEATURE_LEVEL featureLevel;
    hr = D3D11CreateDevice(
        nullptr,
        D3D_DRIVER_TYPE_HARDWARE,
        nullptr,
        0,
        nullptr,
        0,
        D3D11_SDK_VERSION,
        &d3dDevice_,
        &featureLevel,
        &d3dContext_);

    if (FAILED(hr)) {
        return false;
    }

    // Create swap chain
    DXGI_SWAP_CHAIN_DESC swapChainDesc = {};
    swapChainDesc.BufferCount = 1;
    swapChainDesc.BufferDesc.Width = windowWidth_;
    swapChainDesc.BufferDesc.Height = windowHeight_;
    swapChainDesc.BufferDesc.Format = DXGI_FORMAT_R8G8B8A8_UNORM;
    swapChainDesc.BufferDesc.RefreshRate.Numerator = 60;
    swapChainDesc.BufferDesc.RefreshRate.Denominator = 1;
    swapChainDesc.BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT;
    swapChainDesc.OutputWindow = windowHandle_;
    swapChainDesc.SampleDesc.Count = 1;
    swapChainDesc.SampleDesc.Quality = 0;
    swapChainDesc.Windowed = TRUE;

    IDXGIDevice* dxgiDevice = nullptr;
    hr = d3dDevice_->QueryInterface(__uuidof(IDXGIDevice), (void**)&dxgiDevice);
    if (FAILED(hr)) {
        return false;
    }

    IDXGIAdapter* dxgiAdapter = nullptr;
    hr = dxgiDevice->GetAdapter(&dxgiAdapter);
    dxgiDevice->Release();
    if (FAILED(hr)) {
        return false;
    }

    IDXGIFactory* dxgiFactory = nullptr;
    hr = dxgiAdapter->GetParent(__uuidof(IDXGIFactory), (void**)&dxgiFactory);
    dxgiAdapter->Release();
    if (FAILED(hr)) {
        return false;
    }

    hr = dxgiFactory->CreateSwapChain(d3dDevice_, &swapChainDesc, &swapChain_);
    dxgiFactory->Release();
    if (FAILED(hr)) {
        return false;
    }

    // Create render target view
    ID3D11Texture2D* backBuffer = nullptr;
    hr = swapChain_->GetBuffer(0, __uuidof(ID3D11Texture2D), (void**)&backBuffer);
    if (FAILED(hr)) {
        return false;
    }

    hr = d3dDevice_->CreateRenderTargetView(backBuffer, nullptr, &renderTargetView_);
    backBuffer->Release();
    if (FAILED(hr)) {
        return false;
    }

    // Compile and create shaders
    ID3DBlob* vsBlob = nullptr;
    ID3DBlob* errorBlob = nullptr;
    hr = D3DCompile(vertexShaderSource, strlen(vertexShaderSource), nullptr, nullptr, nullptr,
                    "main", "vs_4_0", 0, 0, &vsBlob, &errorBlob);
    if (FAILED(hr)) {
        if (errorBlob) errorBlob->Release();
        return false;
    }

    hr = d3dDevice_->CreateVertexShader(vsBlob->GetBufferPointer(), vsBlob->GetBufferSize(),
                                       nullptr, &vertexShader_);
    if (FAILED(hr)) {
        vsBlob->Release();
        return false;
    }

    // Create input layout
    D3D11_INPUT_ELEMENT_DESC layout[] = {
        { "POSITION", 0, DXGI_FORMAT_R32G32B32A32_FLOAT, 0, 0, D3D11_INPUT_PER_VERTEX_DATA, 0 },
        { "TEXCOORD", 0, DXGI_FORMAT_R32G32_FLOAT, 0, 16, D3D11_INPUT_PER_VERTEX_DATA, 0 }
    };

    hr = d3dDevice_->CreateInputLayout(layout, 2, vsBlob->GetBufferPointer(),
                                      vsBlob->GetBufferSize(), &inputLayout_);
    vsBlob->Release();
    if (FAILED(hr)) {
        return false;
    }

    // Compile pixel shader
    ID3DBlob* psBlob = nullptr;
    hr = D3DCompile(pixelShaderSource, strlen(pixelShaderSource), nullptr, nullptr, nullptr,
                    "main", "ps_4_0", 0, 0, &psBlob, &errorBlob);
    if (FAILED(hr)) {
        if (errorBlob) errorBlob->Release();
        return false;
    }

    hr = d3dDevice_->CreatePixelShader(psBlob->GetBufferPointer(), psBlob->GetBufferSize(),
                                      nullptr, &pixelShader_);
    psBlob->Release();
    if (FAILED(hr)) {
        return false;
    }

    // Create vertex buffer
    Vertex vertices[] = {
        { -1.0f, -1.0f, 0.0f, 1.0f, 0.0f, 1.0f },  // Bottom left
        { -1.0f,  1.0f, 0.0f, 1.0f, 0.0f, 0.0f },  // Top left
        {  1.0f, -1.0f, 0.0f, 1.0f, 1.0f, 1.0f },  // Bottom right
        {  1.0f,  1.0f, 0.0f, 1.0f, 1.0f, 0.0f }   // Top right
    };

    D3D11_BUFFER_DESC bufferDesc = {};
    bufferDesc.ByteWidth = sizeof(vertices);
    bufferDesc.Usage = D3D11_USAGE_DYNAMIC;  // Changed to DYNAMIC for updates
    bufferDesc.BindFlags = D3D11_BIND_VERTEX_BUFFER;
    bufferDesc.CPUAccessFlags = D3D11_CPU_ACCESS_WRITE;  // Allow CPU write access

    D3D11_SUBRESOURCE_DATA initData = {};
    initData.pSysMem = vertices;

    hr = d3dDevice_->CreateBuffer(&bufferDesc, &initData, &vertexBuffer_);
    if (FAILED(hr)) {
        return false;
    }

    // Create high-quality sampler state
    D3D11_SAMPLER_DESC samplerDesc = {};
    samplerDesc.Filter = D3D11_FILTER_MIN_MAG_MIP_LINEAR;  // High-quality linear filtering
    samplerDesc.AddressU = D3D11_TEXTURE_ADDRESS_CLAMP;
    samplerDesc.AddressV = D3D11_TEXTURE_ADDRESS_CLAMP;
    samplerDesc.AddressW = D3D11_TEXTURE_ADDRESS_CLAMP;
    samplerDesc.MipLODBias = 0.0f;
    samplerDesc.MaxAnisotropy = 1;
    samplerDesc.ComparisonFunc = D3D11_COMPARISON_ALWAYS;
    samplerDesc.MinLOD = 0;
    samplerDesc.MaxLOD = D3D11_FLOAT32_MAX;

    hr = d3dDevice_->CreateSamplerState(&samplerDesc, &samplerState_);
    if (FAILED(hr)) {
        return false;
    }

    return true;
}

void DuplicationWindow::CleanupDX()
{
    if (samplerState_) { samplerState_->Release(); samplerState_ = nullptr; }
    if (vertexBuffer_) { vertexBuffer_->Release(); vertexBuffer_ = nullptr; }
    if (inputLayout_) { inputLayout_->Release(); inputLayout_ = nullptr; }
    if (pixelShader_) { pixelShader_->Release(); pixelShader_ = nullptr; }
    if (vertexShader_) { vertexShader_->Release(); vertexShader_ = nullptr; }
    if (capturedSRV_) { capturedSRV_->Release(); capturedSRV_ = nullptr; }
    if (capturedTexture_) { capturedTexture_->Release(); capturedTexture_ = nullptr; }
    if (renderTargetView_) { renderTargetView_->Release(); renderTargetView_ = nullptr; }
    if (swapChain_) { swapChain_->Release(); swapChain_ = nullptr; }
    if (d3dContext_) { d3dContext_->Release(); d3dContext_ = nullptr; }
    if (d3dDevice_) { d3dDevice_->Release(); d3dDevice_ = nullptr; }
}

bool DuplicationWindow::InitializeDuplication()
{
    HRESULT hr;

    // Get DXGI adapter
    IDXGIDevice* dxgiDevice = nullptr;
    hr = d3dDevice_->QueryInterface(__uuidof(IDXGIDevice), (void**)&dxgiDevice);
    if (FAILED(hr)) {
        return false;
    }

    IDXGIAdapter* adapter = nullptr;
    hr = dxgiDevice->GetAdapter(&adapter);
    dxgiDevice->Release();
    if (FAILED(hr)) {
        return false;
    }

    // Find the target output instead of always using the primary
    if (!FindTargetOutput()) {
        adapter->Release();
        return false;
    }

    adapter->Release();

    // Create desktop duplication
    hr = output_->DuplicateOutput(d3dDevice_, &duplication_);
    if (FAILED(hr)) {
        return false;
    }

    return true;
}

bool DuplicationWindow::FindTargetOutput()
{
    HRESULT hr;
    
    // Get DXGI adapter
    IDXGIDevice* dxgiDevice = nullptr;
    hr = d3dDevice_->QueryInterface(__uuidof(IDXGIDevice), (void**)&dxgiDevice);
    if (FAILED(hr)) {
        return false;
    }

    IDXGIAdapter* adapter = nullptr;
    hr = dxgiDevice->GetAdapter(&adapter);
    dxgiDevice->Release();
    if (FAILED(hr)) {
        return false;
    }

    // Enumerate all outputs to find the target monitor
    UINT outputIndex = 0;
    IDXGIOutput* output = nullptr;
    
    while (adapter->EnumOutputs(outputIndex, &output) != DXGI_ERROR_NOT_FOUND) {
        DXGI_OUTPUT_DESC outputDesc;
        hr = output->GetDesc(&outputDesc);
        
        if (SUCCEEDED(hr)) {
            // Convert wide string to narrow string for comparison
            char deviceName[32];
            WideCharToMultiByte(CP_ACP, 0, outputDesc.DeviceName, -1, deviceName, sizeof(deviceName), nullptr, nullptr);
            
            // Check if this output matches our target monitor
            if (targetMonitorName_.empty() || targetMonitorName_ == deviceName) {
                // Found the target output
                hr = output->QueryInterface(__uuidof(IDXGIOutput1), (void**)&output_);
                output->Release();
                adapter->Release();
                return SUCCEEDED(hr);
            }
        }
        
        output->Release();
        outputIndex++;
    }
    
    adapter->Release();
    
    // If we didn't find the target monitor, try to fall back to primary
    if (!targetMonitorName_.empty()) {
        // Reset and try again without target monitor name (fallback to primary)
        targetMonitorName_.clear();
        return FindTargetOutput();
    }
    
    return false;
}

void DuplicationWindow::CleanupDuplication()
{
    if (duplication_) { duplication_->Release(); duplication_ = nullptr; }
    if (output_) { output_->Release(); output_ = nullptr; }
}

// Helper: Convert HICON to D3D11 texture
bool DuplicationWindow::UpdateMouseTexture(HICON hIcon, int width, int height)
{
    if (mouseTexture_) { mouseTexture_->Release(); mouseTexture_ = nullptr; }
    if (mouseSRV_) { mouseSRV_->Release(); mouseSRV_ = nullptr; }

    if (!hIcon || width <= 0 || height <= 0) {
        OutputDebugStringA("[DIAG] Invalid HICON or size for mouse texture.\n");
        return false;
    }

    // Create a 32-bit BGRA DIB section
    BITMAPV5HEADER bi = {};
    bi.bV5Size = sizeof(BITMAPV5HEADER);
    bi.bV5Width = width;
    bi.bV5Height = -height; // top-down
    bi.bV5Planes = 1;
    bi.bV5BitCount = 32;
    bi.bV5Compression = BI_BITFIELDS;
    bi.bV5RedMask   = 0x00FF0000;
    bi.bV5GreenMask = 0x0000FF00;
    bi.bV5BlueMask  = 0x000000FF;
    bi.bV5AlphaMask = 0xFF000000;

    void* pBits = nullptr;
    HDC hdc = GetDC(nullptr);
    HBITMAP hBitmap = CreateDIBSection(hdc, (BITMAPINFO*)&bi, DIB_RGB_COLORS, &pBits, nullptr, 0);
    HDC hMemDC = CreateCompatibleDC(hdc);
    HGDIOBJ oldBmp = SelectObject(hMemDC, hBitmap);
    PatBlt(hMemDC, 0, 0, width, height, WHITENESS);
    DrawIconEx(hMemDC, 0, 0, hIcon, width, height, 0, nullptr, DI_NORMAL);
    // Copy to D3D11 texture
    D3D11_TEXTURE2D_DESC desc = {};
    desc.Width = width;
    desc.Height = height;
    desc.MipLevels = 1;
    desc.ArraySize = 1;
    desc.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
    desc.SampleDesc.Count = 1;
    desc.Usage = D3D11_USAGE_DEFAULT;
    desc.BindFlags = D3D11_BIND_SHADER_RESOURCE;
    D3D11_SUBRESOURCE_DATA initData = {};
    initData.pSysMem = pBits;
    initData.SysMemPitch = width * 4;
    HRESULT hr = d3dDevice_->CreateTexture2D(&desc, &initData, &mouseTexture_);
    if (SUCCEEDED(hr)) {
        D3D11_SHADER_RESOURCE_VIEW_DESC srvDesc = {};
        srvDesc.Format = desc.Format;
        srvDesc.ViewDimension = D3D11_SRV_DIMENSION_TEXTURE2D;
        srvDesc.Texture2D.MipLevels = 1;
        hr = d3dDevice_->CreateShaderResourceView(mouseTexture_, &srvDesc, &mouseSRV_);
        if (FAILED(hr)) {
            OutputDebugStringA("[DIAG] Failed to create mouse SRV.\n");
        }
    } else {
        OutputDebugStringA("[DIAG] Failed to create mouse texture.\n");
    }
    // Cleanup
    SelectObject(hMemDC, oldBmp);
    DeleteDC(hMemDC);
    DeleteObject(hBitmap);
    ReleaseDC(nullptr, hdc);
    if (mouseTexture_ && mouseSRV_) {
        OutputDebugStringA("[DIAG] Mouse texture and SRV created successfully.\n");
    }
    return mouseTexture_ && mouseSRV_;
}

bool DuplicationWindow::CaptureFrame()
{
    if (!duplication_) {
        return false;
    }

    DXGI_OUTDUPL_FRAME_INFO frameInfo;
    IDXGIResource* desktopResource = nullptr;
    
    HRESULT hr = duplication_->AcquireNextFrame(0, &frameInfo, &desktopResource);
    if (hr == DXGI_ERROR_WAIT_TIMEOUT) {
        // No new frame available - continue with existing captured texture if available
        return capturedTexture_ != nullptr;
    }
    if (FAILED(hr)) {
        // Handle DXGI_ERROR_INVALID_CALL and other errors by recreating duplication
        if (hr == DXGI_ERROR_INVALID_CALL || hr == DXGI_ERROR_ACCESS_LOST) {
            CleanupDuplication();
            if (InitializeDuplication()) {
                // Try again after reinitializing
                hr = duplication_->AcquireNextFrame(0, &frameInfo, &desktopResource);
                if (FAILED(hr)) {
                    return capturedTexture_ != nullptr;
                }
            } else {
                return false;
            }
        } else {
            return false;
        }
    }

    // Get the desktop texture
    ID3D11Texture2D* desktopTexture = nullptr;
    hr = desktopResource->QueryInterface(__uuidof(ID3D11Texture2D), (void**)&desktopTexture);
    desktopResource->Release();
    if (FAILED(hr)) {
        duplication_->ReleaseFrame();
        return false;
    }

    // Get texture description to check if we need to recreate our resources
    D3D11_TEXTURE2D_DESC newDesc;
    desktopTexture->GetDesc(&newDesc);
    
    bool needRecreate = false;
    if (capturedTexture_) {
        D3D11_TEXTURE2D_DESC existingDesc;
        capturedTexture_->GetDesc(&existingDesc);
        needRecreate = (existingDesc.Width != newDesc.Width || 
                       existingDesc.Height != newDesc.Height ||
                       existingDesc.Format != newDesc.Format);
    } else {
        needRecreate = true;
    }

    // Recreate texture and SRV only if necessary
    if (needRecreate) {
        if (capturedTexture_) {
            capturedTexture_->Release();
            capturedTexture_ = nullptr;
        }
        if (capturedSRV_) {
            capturedSRV_->Release();
            capturedSRV_ = nullptr;
        }

        D3D11_TEXTURE2D_DESC desc = newDesc;
        desc.Usage = D3D11_USAGE_DEFAULT;
        desc.BindFlags = D3D11_BIND_SHADER_RESOURCE;
        desc.CPUAccessFlags = 0;
        desc.MiscFlags = 0;

        hr = d3dDevice_->CreateTexture2D(&desc, nullptr, &capturedTexture_);
        if (SUCCEEDED(hr)) {
            // Create shader resource view
            D3D11_SHADER_RESOURCE_VIEW_DESC srvDesc = {};
            srvDesc.Format = desc.Format;
            srvDesc.ViewDimension = D3D11_SRV_DIMENSION_TEXTURE2D;
            srvDesc.Texture2D.MipLevels = 1;
            
            hr = d3dDevice_->CreateShaderResourceView(capturedTexture_, &srvDesc, &capturedSRV_);
        }
    }

    // Copy the desktop content to our texture
    if (SUCCEEDED(hr) && capturedTexture_) {
        d3dContext_->CopySubresourceRegion(capturedTexture_, 0, 0, 0, 0, desktopTexture, 0, nullptr);
    }

    desktopTexture->Release();
    duplication_->ReleaseFrame();

    return SUCCEEDED(hr) && capturedTexture_ != nullptr;
}

bool DuplicationWindow::RenderFrame()
{
    if (!capturedSRV_ || !capturedTexture_) {
        return false;
    }

    // Set up rendering pipeline
    d3dContext_->OMSetRenderTargets(1, &renderTargetView_, nullptr);

    D3D11_VIEWPORT viewport = {};
    viewport.TopLeftX = 0;
    viewport.TopLeftY = 0;
    viewport.Width = static_cast<FLOAT>(windowWidth_);
    viewport.Height = static_cast<FLOAT>(windowHeight_);
    viewport.MinDepth = 0.0f;
    viewport.MaxDepth = 1.0f;
    d3dContext_->RSSetViewports(1, &viewport);

    // Clear the render target
    float clearColor[4] = { 0.0f, 0.0f, 0.0f, 1.0f };
    d3dContext_->ClearRenderTargetView(renderTargetView_, clearColor);

    // The captured texture contains only the target monitor's content (not the entire desktop)
    D3D11_TEXTURE2D_DESC textureDesc;
    capturedTexture_->GetDesc(&textureDesc);
    Vertex vertices[] = {
        { -1.0f, -1.0f, 0.0f, 1.0f, 0.0f, 1.0f },   // Bottom left
        { -1.0f,  1.0f, 0.0f, 1.0f, 0.0f, 0.0f },   // Top left
        {  1.0f, -1.0f, 0.0f, 1.0f, 1.0f, 1.0f },   // Bottom right
        {  1.0f,  1.0f, 0.0f, 1.0f, 1.0f, 0.0f }    // Top right
    };
    D3D11_MAPPED_SUBRESOURCE mappedResource;
    HRESULT hr = d3dContext_->Map(vertexBuffer_, 0, D3D11_MAP_WRITE_DISCARD, 0, &mappedResource);
    if (SUCCEEDED(hr)) {
        memcpy(mappedResource.pData, vertices, sizeof(vertices));
        d3dContext_->Unmap(vertexBuffer_, 0);
    }
    d3dContext_->VSSetShader(vertexShader_, nullptr, 0);
    d3dContext_->PSSetShader(pixelShader_, nullptr, 0);
    d3dContext_->IASetInputLayout(inputLayout_);
    UINT stride = sizeof(Vertex);
    UINT offset = 0;
    d3dContext_->IASetVertexBuffers(0, 1, &vertexBuffer_, &stride, &offset);
    d3dContext_->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLESTRIP);
    d3dContext_->PSSetShaderResources(0, 1, &capturedSRV_);
    d3dContext_->PSSetSamplers(0, 1, &samplerState_);
    d3dContext_->Draw(4, 0);

    // Present the DirectX content
    hr = swapChain_->Present(0, 0);
    if (hr == DXGI_ERROR_DEVICE_REMOVED || hr == DXGI_ERROR_DEVICE_RESET) {
        // Device lost - we should reinitialize DirectX resources
        return false;
    }
    return SUCCEEDED(hr);
}

bool DuplicationWindow::GetMousePointerInfo()
{
    CURSORINFO cursorInfo;
    cursorInfo.cbSize = sizeof(CURSORINFO);
    if (!GetCursorInfo(&cursorInfo)) {
        mouseVisible_ = false;
        OutputDebugStringA("[DIAG] GetCursorInfo failed.\n");
        return false;
    }
    mouseVisible_ = (cursorInfo.flags == CURSOR_SHOWING);
    if (!mouseVisible_) {
        OutputDebugStringA("[DIAG] Mouse not visible.\n");
        return true;
    }
    // Get cursor position relative to target monitor
    POINT cursorPos = cursorInfo.ptScreenPos;
    bool isOnTargetMonitor = true;
    if (targetMonitorRect_.right > targetMonitorRect_.left && 
        targetMonitorRect_.bottom > targetMonitorRect_.top) {
        isOnTargetMonitor = (cursorPos.x >= targetMonitorRect_.left && cursorPos.x < targetMonitorRect_.right &&
                            cursorPos.y >= targetMonitorRect_.top && cursorPos.y < targetMonitorRect_.bottom);
    }
    if (isOnTargetMonitor) {
        mousePosition_.x = cursorPos.x - targetMonitorRect_.left;
        mousePosition_.y = cursorPos.y - targetMonitorRect_.top;
        ICONINFO iconInfo;
        if (GetIconInfo(cursorInfo.hCursor, &iconInfo)) {
            BITMAP bitmap;
            if (GetObject(iconInfo.hbmColor ? iconInfo.hbmColor : iconInfo.hbmMask, sizeof(bitmap), &bitmap)) {
                mouseWidth_ = bitmap.bmWidth;
                mouseHeight_ = abs(bitmap.bmHeight);
                mousePosition_.x -= iconInfo.xHotspot;
                mousePosition_.y -= iconInfo.yHotspot;
                // Update mouse texture if needed
                static HICON lastIcon = nullptr;
                static int lastW = 0, lastH = 0;
                if (cursorInfo.hCursor != lastIcon || mouseWidth_ != lastW || mouseHeight_ != lastH) {
                    char buf[128];
                    sprintf_s(buf, "[DIAG] UpdateMouseTexture: w=%d h=%d hotspot=(%d,%d)\n", mouseWidth_, mouseHeight_, iconInfo.xHotspot, iconInfo.yHotspot);
                    OutputDebugStringA(buf);
                    UpdateMouseTexture(cursorInfo.hCursor, mouseWidth_, mouseHeight_);
                    lastIcon = cursorInfo.hCursor;
                    lastW = mouseWidth_;
                    lastH = mouseHeight_;
                }
            }
            if (iconInfo.hbmColor) DeleteObject(iconInfo.hbmColor);
            if (iconInfo.hbmMask) DeleteObject(iconInfo.hbmMask);
        }
    } else {
        mouseVisible_ = false;
        OutputDebugStringA("[DIAG] Mouse not on target monitor.\n");
    }
    return true;
}

LRESULT CALLBACK DuplicationWindow::WindowProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam)
{
    DuplicationWindow* self = nullptr;
    if (msg == WM_NCCREATE) {
        CREATESTRUCT* cs = reinterpret_cast<CREATESTRUCT*>(lParam);
        self = static_cast<DuplicationWindow*>(cs->lpCreateParams);
        SetWindowLongPtr(hwnd, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(self));
    } else {
        self = reinterpret_cast<DuplicationWindow*>(GetWindowLongPtr(hwnd, GWLP_USERDATA));
    }
    if (!self) {
        return DefWindowProc(hwnd, msg, wParam, lParam);
    }
    switch (msg) {
        case WM_SIZE:
            if (self->swapChain_) {
                self->windowWidth_ = LOWORD(lParam);
                self->windowHeight_ = HIWORD(lParam);
                self->d3dContext_->OMSetRenderTargets(0, nullptr, nullptr);
                if (self->renderTargetView_) {
                    self->renderTargetView_->Release();
                    self->renderTargetView_ = nullptr;
                }
                HRESULT hr = self->swapChain_->ResizeBuffers(0, self->windowWidth_, self->windowHeight_, DXGI_FORMAT_UNKNOWN, 0);
                if (SUCCEEDED(hr)) {
                    ID3D11Texture2D* backBuffer = nullptr;
                    hr = self->swapChain_->GetBuffer(0, __uuidof(ID3D11Texture2D), (void**)&backBuffer);
                    if (SUCCEEDED(hr)) {
                        self->d3dDevice_->CreateRenderTargetView(backBuffer, nullptr, &self->renderTargetView_);
                        backBuffer->Release();
                    }
                }
                InvalidateRect(hwnd, nullptr, FALSE);
            }
            break;
        case WM_PAINT:
            {
                PAINTSTRUCT ps;
                BeginPaint(hwnd, &ps);
                if (self) {
                    self->UpdateFrame();
                }
                EndPaint(hwnd, &ps);
                return 0;
            }
        case WM_ERASEBKGND:
            return 1;
        case WM_DISPLAYCHANGE:
            if (self) {
                self->CleanupDuplication();
                self->InitializeDuplication();
                InvalidateRect(hwnd, nullptr, FALSE);
            }
            break;
    }
    return DefWindowProc(hwnd, msg, wParam, lParam);
}