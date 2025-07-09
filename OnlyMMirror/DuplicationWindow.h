#pragma once
#include <windows.h>
#include <d3d11.h>
#include <dxgi1_2.h>
#include <memory>
#include <string>

class DuplicationWindow  // NOLINT(cppcoreguidelines-special-member-functions)
{
public:
    DuplicationWindow();
    ~DuplicationWindow();

    bool Create(HWND parent, HINSTANCE instance, int x, int y, int width, int height, const char* targetMonitorName = nullptr);
    void Destroy();
    HWND GetWindowHandle() const;
    void SetSourceRect(const RECT& rect);
    bool SetTransform(float zoomFactor);
    void Resize(int x, int y, int width, int height) const;
    bool UpdateFrame();
    bool UpdateMouseTexture(HICON hIcon, int width, int height);

private:
    bool InitializeDX();
    void CleanupDX();
    bool InitializeDuplication();
    void CleanupDuplication();
    bool CaptureFrame();
    bool RenderFrame();
    bool FindTargetOutput();
    bool GetMousePointerInfo();
    void CleanupMousePointer();
    static LRESULT CALLBACK WindowProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam);
    static const TCHAR* GetWindowClassName();

    // Window handles
    HWND windowHandle_;
    HINSTANCE hInstance_;
    
    // DirectX resources
    ID3D11Device* d3dDevice_;
    ID3D11DeviceContext* d3dContext_;
    IDXGISwapChain* swapChain_;
    ID3D11RenderTargetView* renderTargetView_;
    ID3D11Texture2D* capturedTexture_;
    ID3D11ShaderResourceView* capturedSRV_;
    ID3D11SamplerState* samplerState_;
    
    // Desktop Duplication resources
    IDXGIOutputDuplication* duplication_;
    IDXGIOutput1* output_;
    
    // Shader resources
    ID3D11VertexShader* vertexShader_;
    ID3D11PixelShader* pixelShader_;
    ID3D11Buffer* vertexBuffer_;
    ID3D11InputLayout* inputLayout_;
    
    // Mouse pointer resources
    ID3D11Texture2D* mouseTexture_;
    ID3D11ShaderResourceView* mouseSRV_;
    ID3D11Buffer* mouseVertexBuffer_;
    POINT mousePosition_;
    bool mouseVisible_;
    int mouseWidth_;
    int mouseHeight_;
    RECT targetMonitorRect_;
    
    // Rendering parameters
    RECT sourceRect_;
    float zoomFactor_;
    int windowWidth_;
    int windowHeight_;
    
    // Monitor selection
    std::string targetMonitorName_;
    ID3D11PixelShader* cursorPixelShader_ = nullptr; // Diagnostic solid color shader
};