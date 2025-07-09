#include "stdafx.h"
#include "DuplicationWindow.h"
#include <d3dcompiler.h>
#include <string>
#include <wingdi.h>
#include <iostream>

#pragma comment(lib, "d3d11.lib")
#pragma comment(lib, "dxgi.lib")
#pragma comment(lib, "d3dcompiler.lib")

namespace
{
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
    // ReSharper disable once CppInconsistentNaming
    struct Vertex {        
        float x, y, z, w;  // Position
        float u, v;        // Texture coordinates
    };
}

bool DuplicationWindow::LoadDefaultCursor()
{
    // Load the default arrow cursor
    const HCURSOR cursor = LoadCursor(nullptr, IDC_ARROW);
    if (!cursor)
    {
        return false;
    }

    ICONINFO iconInfo;
    if (!GetIconInfo(cursor, &iconInfo))
    {
        return false;
    }

    // Get the dimensions of the cursor
    BITMAP bmpColor = {};
    GetObject(iconInfo.hbmColor, sizeof(bmpColor), &bmpColor);

    const int width = bmpColor.bmWidth;
    const int height = bmpColor.bmHeight;

    cursorShapeInfo_.Width = width;
    cursorShapeInfo_.Height = height;
    cursorShapeInfo_.HotSpot.x = iconInfo.xHotspot;  // NOLINT(cppcoreguidelines-narrowing-conversions, bugprone-narrowing-conversions)
    cursorShapeInfo_.HotSpot.y = iconInfo.yHotspot;  // NOLINT(bugprone-narrowing-conversions, cppcoreguidelines-narrowing-conversions)

    std::vector<BYTE> cursorPixels(width * height * 4);

    HDC hdcScreen = GetDC(nullptr);
    HDC hdcMem = CreateCompatibleDC(hdcScreen);

    // Create a 32-bpp bitmap and select it into the memory DC
    BITMAPINFO bmi = {};
    bmi.bmiHeader.biSize = sizeof(BITMAPINFOHEADER);
    bmi.bmiHeader.biWidth = width;
    bmi.bmiHeader.biHeight = -height; // Top-down DIB
    bmi.bmiHeader.biPlanes = 1;
    bmi.bmiHeader.biBitCount = 32;
    bmi.bmiHeader.biCompression = BI_RGB;

    void* pPixels = nullptr;
    HBITMAP hbm32 = CreateDIBSection(hdcScreen, &bmi, DIB_RGB_COLORS, &pPixels, nullptr, 0);
    HBITMAP hbmOld = static_cast<HBITMAP>(SelectObject(hdcMem, hbm32));

    // Draw the icon into the 32-bpp bitmap. This correctly handles the alpha channel.
    DrawIconEx(hdcMem, 0, 0, cursor, width, height, 0, nullptr, DI_NORMAL);

    // Copy the pixel data
    memcpy(cursorPixels.data(), pPixels, cursorPixels.size());

    // Clean up GDI objects
    SelectObject(hdcMem, hbmOld);
    DeleteObject(hbm32);
    DeleteDC(hdcMem);
    ReleaseDC(nullptr, hdcScreen);
    DeleteObject(iconInfo.hbmColor);
    DeleteObject(iconInfo.hbmMask);

    // Create the texture
    D3D11_TEXTURE2D_DESC desc = {};
    desc.Width = width;
    desc.Height = height;
    desc.MipLevels = 1;
    desc.ArraySize = 1;
    desc.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
    desc.SampleDesc.Count = 1;
    desc.Usage = D3D11_USAGE_DEFAULT;
    desc.BindFlags = D3D11_BIND_SHADER_RESOURCE;

    D3D11_SUBRESOURCE_DATA subresourceData = {};
    subresourceData.pSysMem = cursorPixels.data();
    subresourceData.SysMemPitch = width * 4;

    HRESULT hr = d3dDevice_->CreateTexture2D(&desc, &subresourceData, &cursorTexture_);
    if (SUCCEEDED(hr))
    {
        hr = d3dDevice_->CreateShaderResourceView(cursorTexture_, nullptr, &cursorSRV_);
    }

    return SUCCEEDED(hr);
}

const TCHAR* DuplicationWindow::GetWindowClassName()
{ 
    return TEXT("DuplicationWindow"); 
}

DuplicationWindow::DuplicationWindow()
    : windowHandle_(nullptr)
    , hInstance_(nullptr)
    , d3dDevice_(nullptr)
    , d3dContext_(nullptr)
    , swapChain_(nullptr)
    , renderTargetView_(nullptr)
    , capturedTexture_(nullptr)
    , capturedSRV_(nullptr)
    , samplerState_(nullptr)
    , duplication_(nullptr)
    , output_(nullptr)
    , vertexShader_(nullptr)
    , pixelShader_(nullptr)
    , vertexBuffer_(nullptr)
    , inputLayout_(nullptr)
    , zoomFactor_(1.0f)
    , windowWidth_(0)
    , windowHeight_(0)
    , cursorTexture_(nullptr)
    , cursorSRV_(nullptr)
    , blendState_(nullptr)
    , cursorShapeInfo_()
    , cursorPosition_()
    , cursorVisible_(false)
{
    ZeroMemory(&sourceRect_, sizeof(sourceRect_));
    ZeroMemory(&targetMonitorRect_, sizeof(targetMonitorRect_));
}

DuplicationWindow::~DuplicationWindow()
{ 
    Destroy(); 
}

bool DuplicationWindow::Create(
    const HWND parent, 
    const HINSTANCE instance,
    const int x, const int y, const int width, const int height, 
    const char* targetMonitorName)
{
    Destroy();

    hInstance_ = instance;
    windowWidth_ = width;
    windowHeight_ = height;

    if (targetMonitorName) 
    {
        targetMonitorName_ = targetMonitorName;
    }

    WNDCLASSEX wc = {};
    wc.cbSize = sizeof(WNDCLASSEX);
    wc.style = CS_HREDRAW | CS_VREDRAW;
    wc.lpfnWndProc = WindowProc;
    wc.hInstance = instance;
    wc.hCursor = LoadCursor(nullptr, IDC_ARROW);
    wc.hbrBackground = reinterpret_cast<HBRUSH>(COLOR_WINDOW + 1);  // NOLINT(performance-no-int-to-ptr)
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

    if (!windowHandle_) 
    {
        return false;
    }

    if (!InitializeDX()) 
    {
        return false;
    }

    if (!InitializeDuplication()) 
    {
        return false;
    }

    return true;
}

void DuplicationWindow::Destroy()
{
    CleanupDuplication();
    CleanupDX();

    if (windowHandle_) 
    {
        DestroyWindow(windowHandle_);
        windowHandle_ = nullptr;
    }
}

HWND DuplicationWindow::GetWindowHandle() const
{ 
    return windowHandle_; 
}

void DuplicationWindow::SetSourceRect(const RECT& rect)
{
    sourceRect_ = rect;

    // Store the target monitor rect for mouse coordinate conversion
    targetMonitorRect_ = rect;    
}

bool DuplicationWindow::SetTransform(const float zoomFactor)
{
    zoomFactor_ = zoomFactor;
    return true;
}

void DuplicationWindow::Resize(const int x, const int y, const int width, const int height) const
{
    if (windowHandle_) 
    {
        SetWindowPos(windowHandle_, nullptr, x, y, width, height, SWP_NOZORDER);
    }
}

bool DuplicationWindow::UpdateFrame()
{
    // Try to capture a new frame (may reuse existing if no new frame available)
    const bool hasCapturedContent = CaptureFrame();

    // Always try to render if we have content, regardless of timing
    if (hasCapturedContent) 
    {
        return RenderFrame();
    }

    return false;
}

// ReSharper disable once CppInconsistentNaming
bool DuplicationWindow::InitializeDX()
{
    // Create D3D11 device and context
    D3D_FEATURE_LEVEL featureLevel;

    HRESULT hr = D3D11CreateDevice(
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

    if (FAILED(hr))
    {
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
    hr = d3dDevice_->QueryInterface(__uuidof(IDXGIDevice), reinterpret_cast<void**>(&dxgiDevice));  // NOLINT(clang-diagnostic-language-extension-token)
    if (FAILED(hr))
    {
        return false;
    }

    IDXGIAdapter* dxgiAdapter = nullptr;
    hr = dxgiDevice->GetAdapter(&dxgiAdapter);
    dxgiDevice->Release();
    if (FAILED(hr))
    {
        return false;
    }

    IDXGIFactory* dxgiFactory = nullptr;
    hr = dxgiAdapter->GetParent(__uuidof(IDXGIFactory), reinterpret_cast<void**>(&dxgiFactory));  // NOLINT(clang-diagnostic-language-extension-token)
    dxgiAdapter->Release();
    if (FAILED(hr))
    {
        return false;
    }

    hr = dxgiFactory->CreateSwapChain(d3dDevice_, &swapChainDesc, &swapChain_);
    dxgiFactory->Release();
    if (FAILED(hr))
    {
        return false;
    }

    // Create render target view
    ID3D11Texture2D* backBuffer = nullptr;
    hr = swapChain_->GetBuffer(0, __uuidof(ID3D11Texture2D), reinterpret_cast<void**>(&backBuffer));  // NOLINT(clang-diagnostic-language-extension-token)
    if (FAILED(hr))
    {
        return false;
    }

    hr = d3dDevice_->CreateRenderTargetView(backBuffer, nullptr, &renderTargetView_);
    backBuffer->Release();
    if (FAILED(hr))
    {
        return false;
    }

    // Compile and create shaders
    ID3DBlob* vsBlob = nullptr;
    ID3DBlob* errorBlob = nullptr;
    hr = D3DCompile(
        vertexShaderSource,
        strlen(vertexShaderSource),
        nullptr, nullptr, nullptr,
        "main", "vs_4_0", 0, 0, &vsBlob, &errorBlob);

    if (FAILED(hr))
    {
        if (errorBlob)
        {
            errorBlob->Release();
        }

        return false;
    }

    hr = d3dDevice_->CreateVertexShader(
        vsBlob->GetBufferPointer(), vsBlob->GetBufferSize(), nullptr, &vertexShader_);

    if (FAILED(hr))
    {
        vsBlob->Release();
        return false;
    }

    // Create input layout
    D3D11_INPUT_ELEMENT_DESC layout[] =
    {
        { "POSITION", 0, DXGI_FORMAT_R32G32B32A32_FLOAT, 0, 0, D3D11_INPUT_PER_VERTEX_DATA, 0 },
        { "TEXCOORD", 0, DXGI_FORMAT_R32G32_FLOAT, 0, 16, D3D11_INPUT_PER_VERTEX_DATA, 0 }
    };

    hr = d3dDevice_->CreateInputLayout(
        layout, 2, vsBlob->GetBufferPointer(), vsBlob->GetBufferSize(), &inputLayout_);

    vsBlob->Release();

    if (FAILED(hr))
    {
        return false;
    }

    // Compile pixel shader
    ID3DBlob* psBlob = nullptr;
    hr = D3DCompile(
        pixelShaderSource, strlen(pixelShaderSource),
        nullptr, nullptr, nullptr,
        "main", "ps_4_0", 0, 0, &psBlob, &errorBlob);

    if (FAILED(hr))
    {
        if (errorBlob)
        {
            errorBlob->Release();
        }

        return false;
    }

    hr = d3dDevice_->CreatePixelShader(
        psBlob->GetBufferPointer(), psBlob->GetBufferSize(), nullptr, &pixelShader_);

    psBlob->Release();

    if (FAILED(hr))
    {
        return false;
    }

    // Create vertex buffer
    constexpr Vertex vertices[] =
    {
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
    if (FAILED(hr))
    {
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
    if (FAILED(hr))
    {
        return false;
    }

    // Create blend state for cursor
    D3D11_BLEND_DESC blendDesc = {};
    blendDesc.RenderTarget[0].BlendEnable = TRUE;
    blendDesc.RenderTarget[0].SrcBlend = D3D11_BLEND_SRC_ALPHA;
    blendDesc.RenderTarget[0].DestBlend = D3D11_BLEND_INV_SRC_ALPHA;
    blendDesc.RenderTarget[0].BlendOp = D3D11_BLEND_OP_ADD;
    blendDesc.RenderTarget[0].SrcBlendAlpha = D3D11_BLEND_ONE;
    blendDesc.RenderTarget[0].DestBlendAlpha = D3D11_BLEND_ZERO;
    blendDesc.RenderTarget[0].BlendOpAlpha = D3D11_BLEND_OP_ADD;
    blendDesc.RenderTarget[0].RenderTargetWriteMask = D3D11_COLOR_WRITE_ENABLE_ALL;
    hr = d3dDevice_->CreateBlendState(&blendDesc, &blendState_);
    if (FAILED(hr))
    {
        return false;
    }

    hr = d3dDevice_->CreateBlendState(&blendDesc, &blendState_);
    if (FAILED(hr))
    {
        return false;
    }

    // Load the default cursor texture
    if (!LoadDefaultCursor())
    {
        return false;
    }

    return true;
}

// ReSharper disable once CppInconsistentNaming
void DuplicationWindow::CleanupDX()
{
    if (blendState_) { blendState_->Release(); blendState_ = nullptr; }
    if (cursorSRV_) { cursorSRV_->Release(); cursorSRV_ = nullptr; }
    if (cursorTexture_) { cursorTexture_->Release(); cursorTexture_ = nullptr; }
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
    // Get DXGI adapter
    IDXGIDevice* dxgiDevice = nullptr;
    HRESULT hr = d3dDevice_->QueryInterface(__uuidof(IDXGIDevice), reinterpret_cast<void**>(&dxgiDevice));  // NOLINT(clang-diagnostic-language-extension-token)
    if (FAILED(hr)) 
    {
        return false;
    }

    IDXGIAdapter* adapter = nullptr;
    hr = dxgiDevice->GetAdapter(&adapter);
    dxgiDevice->Release();
    if (FAILED(hr)) 
    {
        return false;
    }

    // Find the target output instead of always using the primary
    if (!FindTargetOutput()) 
    {
        adapter->Release();
        return false;
    }

    adapter->Release();

    // Create desktop duplication
    hr = output_->DuplicateOutput(d3dDevice_, &duplication_);
    if (FAILED(hr)) 
    {
        return false;
    }

    return true;
}

bool DuplicationWindow::FindTargetOutput()
{   
    // Get DXGI adapter
    IDXGIDevice* dxgiDevice = nullptr;
    HRESULT hr = d3dDevice_->QueryInterface(__uuidof(IDXGIDevice), reinterpret_cast<void**>(&dxgiDevice));  // NOLINT(clang-diagnostic-language-extension-token)
    if (FAILED(hr)) 
    {
        return false;
    }

    IDXGIAdapter* adapter = nullptr;
    hr = dxgiDevice->GetAdapter(&adapter);
    dxgiDevice->Release();
    if (FAILED(hr)) 
    {
        return false;
    }

    // Enumerate all outputs to find the target monitor
    UINT outputIndex = 0;
    IDXGIOutput* output = nullptr;
    
    while (adapter->EnumOutputs(outputIndex, &output) != DXGI_ERROR_NOT_FOUND) 
    {
        DXGI_OUTPUT_DESC outputDesc;
        hr = output->GetDesc(&outputDesc);
        
        if (SUCCEEDED(hr)) 
        {
            // Convert wide string to narrow string for comparison
            char deviceName[32];
            WideCharToMultiByte(CP_ACP, 0, outputDesc.DeviceName, -1, deviceName, sizeof(deviceName), nullptr, nullptr);
            
            // Check if this output matches our target monitor
            if (targetMonitorName_.empty() || targetMonitorName_ == deviceName) {
                // Found the target output
                hr = output->QueryInterface(__uuidof(IDXGIOutput1), reinterpret_cast<void**>(&output_));  // NOLINT(clang-diagnostic-language-extension-token)
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
    if (!targetMonitorName_.empty()) 
    {
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

bool DuplicationWindow::CaptureFrame()
{
    if (!duplication_)
    {
        return false;
    }

    DXGI_OUTDUPL_FRAME_INFO frameInfo = {}; // Zero-initialize
    IDXGIResource* desktopResource = nullptr;

    HRESULT hr = duplication_->AcquireNextFrame(0, &frameInfo, &desktopResource);

    // Only update cursor data if the mouse has actually moved.
    if (frameInfo.LastMouseUpdateTime.QuadPart != 0)
    {
        cursorPosition_ = frameInfo.PointerPosition.Position;
        cursorVisible_ = frameInfo.PointerPosition.Visible;
    }

    if (hr == S_OK)
    {
        // We have a new frame, process it.
        ID3D11Texture2D* desktopTexture = nullptr;
        if (SUCCEEDED(desktopResource->QueryInterface(  // NOLINT(clang-diagnostic-language-extension-token)
            __uuidof(ID3D11Texture2D), reinterpret_cast<void**>(&desktopTexture))))
        {
            D3D11_TEXTURE2D_DESC newDesc;
            desktopTexture->GetDesc(&newDesc);

            bool needRecreate = false;
            if (capturedTexture_)
            {
                D3D11_TEXTURE2D_DESC existingDesc;
                capturedTexture_->GetDesc(&existingDesc);
                needRecreate = (existingDesc.Width != newDesc.Width ||
                    existingDesc.Height != newDesc.Height ||
                    existingDesc.Format != newDesc.Format);
            }
            else
            {
                needRecreate = true;
            }

            if (needRecreate)
            {
                if (capturedTexture_) { capturedTexture_->Release(); capturedTexture_ = nullptr; }
                if (capturedSRV_) { capturedSRV_->Release(); capturedSRV_ = nullptr; }

                D3D11_TEXTURE2D_DESC desc = newDesc;
                desc.Usage = D3D11_USAGE_DEFAULT;
                desc.BindFlags = D3D11_BIND_SHADER_RESOURCE;
                desc.CPUAccessFlags = 0;
                desc.MiscFlags = 0;

                if (SUCCEEDED(d3dDevice_->CreateTexture2D(&desc, nullptr, &capturedTexture_)))
                {
                    D3D11_SHADER_RESOURCE_VIEW_DESC srvDesc = {};
                    srvDesc.Format = desc.Format;
                    srvDesc.ViewDimension = D3D11_SRV_DIMENSION_TEXTURE2D;
                    srvDesc.Texture2D.MipLevels = 1;
                    d3dDevice_->CreateShaderResourceView(capturedTexture_, &srvDesc, &capturedSRV_);
                }
            }

            if (capturedTexture_)
            {
                d3dContext_->CopySubresourceRegion(capturedTexture_, 0, 0, 0, 0, desktopTexture, 0, nullptr);
            }

            desktopTexture->Release();
        }

        desktopResource->Release();
        duplication_->ReleaseFrame();
    }
    else if (hr != DXGI_ERROR_WAIT_TIMEOUT)
    {
        // A real error occurred (not a timeout). Re-initialize the duplication.
        CleanupDuplication();
        InitializeDuplication();
    }

    // We can always try to render, as long as we have a texture from a previous successful frame.
    return capturedTexture_ != nullptr;
}

bool DuplicationWindow::RenderFrame() const
{
    if (!capturedSRV_ || !capturedTexture_)
    {
        return false;
    }

    // Set up rendering pipeline
    d3dContext_->OMSetRenderTargets(1, &renderTargetView_, nullptr);

    D3D11_VIEWPORT viewport;
    viewport.TopLeftX = 0;
    viewport.TopLeftY = 0;
    viewport.Width = static_cast<FLOAT>(windowWidth_);
    viewport.Height = static_cast<FLOAT>(windowHeight_);
    viewport.MinDepth = 0.0f;
    viewport.MaxDepth = 1.0f;
    d3dContext_->RSSetViewports(1, &viewport);

    // Clear the render target
    constexpr float clearColor[4] = { 0.0f, 0.0f, 0.0f, 1.0f };
    d3dContext_->ClearRenderTargetView(renderTargetView_, clearColor);

    // Draw the captured desktop texture
    D3D11_TEXTURE2D_DESC textureDesc;
    capturedTexture_->GetDesc(&textureDesc);

    constexpr Vertex vertices[] =
    {
        { -1.0f, -1.0f, 0.0f, 1.0f, 0.0f, 1.0f },   // Bottom left
        { -1.0f,  1.0f, 0.0f, 1.0f, 0.0f, 0.0f },   // Top left
        {  1.0f, -1.0f, 0.0f, 1.0f, 1.0f, 1.0f },   // Bottom right
        {  1.0f,  1.0f, 0.0f, 1.0f, 1.0f, 0.0f }    // Top right
    };

    D3D11_MAPPED_SUBRESOURCE mappedResource;
    HRESULT hr = d3dContext_->Map(vertexBuffer_, 0, D3D11_MAP_WRITE_DISCARD, 0, &mappedResource);
    if (SUCCEEDED(hr))
    {
        memcpy(mappedResource.pData, vertices, sizeof(vertices));
        d3dContext_->Unmap(vertexBuffer_, 0);
    }

    d3dContext_->VSSetShader(vertexShader_, nullptr, 0);
    d3dContext_->PSSetShader(pixelShader_, nullptr, 0);
    d3dContext_->IASetInputLayout(inputLayout_);
    constexpr UINT stride = sizeof(Vertex);
    constexpr UINT offset = 0;
    d3dContext_->IASetVertexBuffers(0, 1, &vertexBuffer_, &stride, &offset);
    d3dContext_->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLESTRIP);
    d3dContext_->PSSetShaderResources(0, 1, &capturedSRV_);
    d3dContext_->PSSetSamplers(0, 1, &samplerState_);
    d3dContext_->Draw(4, 0);

    // Draw the cursor
    if (cursorVisible_ && cursorSRV_)
    {
        // Set up blend state for transparency
        d3dContext_->OMSetBlendState(blendState_, nullptr, 0xffffffff);

        // Calculate cursor position in normalized device coordinates, accounting for the hotspot
        const float cursorX =
            (static_cast<float>(
                cursorPosition_.x - cursorShapeInfo_.HotSpot.x) / textureDesc.Width) * 2.0f - 1.0f;  // NOLINT(bugprone-narrowing-conversions, cppcoreguidelines-narrowing-conversions, clang-diagnostic-implicit-int-float-conversion)

        const float cursorY =
            1.0f - (static_cast<float>(
                cursorPosition_.y - cursorShapeInfo_.HotSpot.y) / textureDesc.Height) * 2.0f;  // NOLINT(bugprone-narrowing-conversions, clang-diagnostic-implicit-int-float-conversion, cppcoreguidelines-narrowing-conversions)

        const float cursorWidth =
            (static_cast<float>(cursorShapeInfo_.Width) / textureDesc.Width) * 2.0f;  // NOLINT(bugprone-narrowing-conversions, clang-diagnostic-implicit-int-float-conversion, cppcoreguidelines-narrowing-conversions)

        const float cursorHeight =
            (static_cast<float>(cursorShapeInfo_.Height) / textureDesc.Height) * 2.0f; // NOLINT(bugprone-narrowing-conversions, clang-diagnostic-implicit-int-float-conversion, cppcoreguidelines-narrowing-conversions)

        const Vertex cursorVertices[] =
        {
            { cursorX,               cursorY - cursorHeight, 0.0f, 1.0f, 0.0f, 1.0f }, // Bottom-left
            { cursorX,               cursorY,                0.0f, 1.0f, 0.0f, 0.0f }, // Top-left
            { cursorX + cursorWidth, cursorY - cursorHeight, 0.0f, 1.0f, 1.0f, 1.0f }, // Bottom-right
            { cursorX + cursorWidth, cursorY,                0.0f, 1.0f, 1.0f, 0.0f }  // Top-right
        };

        hr = d3dContext_->Map(vertexBuffer_, 0, D3D11_MAP_WRITE_DISCARD, 0, &mappedResource);
        if (SUCCEEDED(hr))
        {
            memcpy(mappedResource.pData, cursorVertices, sizeof(cursorVertices));
            d3dContext_->Unmap(vertexBuffer_, 0);
        }

        d3dContext_->PSSetShaderResources(0, 1, &cursorSRV_);
        d3dContext_->Draw(4, 0);

        // Reset blend state
        d3dContext_->OMSetBlendState(nullptr, nullptr, 0xffffffff);
    }


    // Present the DirectX content
    hr = swapChain_->Present(0, 0);
    if (hr == DXGI_ERROR_DEVICE_REMOVED || hr == DXGI_ERROR_DEVICE_RESET)
    {
        // Device lost - we should reinitialize DirectX resources
        return false;
    }

    return SUCCEEDED(hr);
}

LRESULT CALLBACK DuplicationWindow::WindowProc(const HWND windowHandle, const UINT msg, const WPARAM wParam, const LPARAM lParam)
{
    DuplicationWindow* self;

    if (msg == WM_NCCREATE) 
    {
        const CREATESTRUCT* cs = reinterpret_cast<CREATESTRUCT*>(lParam);  // NOLINT(performance-no-int-to-ptr)
        self = static_cast<DuplicationWindow*>(cs->lpCreateParams);
        SetWindowLongPtr(windowHandle, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(self));
    }
    else 
    {
        self = reinterpret_cast<DuplicationWindow*>(GetWindowLongPtr(windowHandle, GWLP_USERDATA));  // NOLINT(performance-no-int-to-ptr)
    }

    if (!self) 
    {
        return DefWindowProc(windowHandle, msg, wParam, lParam);
    }

    switch (msg)
    {
        case WM_SIZE:
            if (self->swapChain_) 
            {
                self->windowWidth_ = LOWORD(lParam);
                self->windowHeight_ = HIWORD(lParam);
                self->d3dContext_->OMSetRenderTargets(0, nullptr, nullptr);
                if (self->renderTargetView_) 
                {
                    self->renderTargetView_->Release();
                    self->renderTargetView_ = nullptr;
                }

                HRESULT hr = self->swapChain_->ResizeBuffers(
                    0, self->windowWidth_, self->windowHeight_, DXGI_FORMAT_UNKNOWN, 0);

                if (SUCCEEDED(hr)) 
                {
                    ID3D11Texture2D* backBuffer = nullptr;
                    hr = self->swapChain_->GetBuffer(0, __uuidof(ID3D11Texture2D), reinterpret_cast<void**>(&backBuffer));  // NOLINT(clang-diagnostic-language-extension-token)
                    if (SUCCEEDED(hr)) 
                    {
                        // ReSharper disable once CppFunctionResultShouldBeUsed
                        self->d3dDevice_->CreateRenderTargetView(backBuffer, nullptr, &self->renderTargetView_);
                        backBuffer->Release();
                    }
                }

                InvalidateRect(windowHandle, nullptr, FALSE);
            }
            break;

        case WM_PAINT:
            {
                PAINTSTRUCT ps;
                BeginPaint(windowHandle, &ps);
                if (self) 
                {
                    self->UpdateFrame();
                }
                EndPaint(windowHandle, &ps);
                return 0;
            }

        case WM_ERASEBKGND:
            return 1;

        case WM_DISPLAYCHANGE:
            if (self) 
            {
                self->CleanupDuplication();
                self->InitializeDuplication();
                InvalidateRect(windowHandle, nullptr, FALSE);
            }
            break;

        default:
            break;
    }
    return DefWindowProc(windowHandle, msg, wParam, lParam);
}
