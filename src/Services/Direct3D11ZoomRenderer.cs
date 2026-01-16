using System;
using System.Runtime.InteropServices;
using System.Numerics;
using EasyScreenRecord.Helpers;
using Windows.Graphics.DirectX.Direct3D11;
using Vortice.Direct3D11;
using Vortice.Direct3D;
using Vortice.DXGI;
using Vortice.D3DCompiler;
using Vortice.Mathematics;

using WinRT; // Required for As<T>()

namespace EasyScreenRecord.Services
{
    public class Direct3D11ZoomRenderer : IDisposable
    {
        private ID3D11Device _d3dDevice;
        private ID3D11DeviceContext _d3dContext;
        private ID3D11VertexShader _vertexShader;
        private ID3D11PixelShader _pixelShader;
        private ID3D11InputLayout _inputLayout;
        private ID3D11Buffer _vertexBuffer;
        private ID3D11Buffer _constantBuffer;
        private ID3D11SamplerState _samplerState;
        
        // Debug Properties
        public System.Drawing.Size LastTextureSize { get; private set; }
        public Vector4 LastCropRect { get; private set; }
        
        [StructLayout(LayoutKind.Sequential)]
        private struct ConstantBufferData
        {
            public Vector4 CropRect; // x, y, width, height
        }

        public Direct3D11ZoomRenderer(IDirect3DDevice winrtDevice)
        {
            // 1. Get Native Device
            // Use As<T>() extension method from WinRT to unwrap/QI the interface
            var access = winrtDevice.As<EasyScreenRecord.Helpers.IDirect3DDxgiInterfaceAccess>();
            Guid d3d11DeviceGuid = typeof(ID3D11Device).GUID;
            IntPtr pDevice = access.GetInterface(ref d3d11DeviceGuid);
            _d3dDevice = new ID3D11Device(pDevice);
            _d3dContext = _d3dDevice.ImmediateContext;

            InitializeResources();
        }

        private unsafe void InitializeResources()
        {
            string vsSource = @"
                struct VS_INPUT { float4 pos : POSITION; float2 uv : TEXCOORD; };
                struct PS_INPUT { float4 pos : SV_POSITION; float2 uv : TEXCOORD; };
                PS_INPUT main(VS_INPUT input) {
                    PS_INPUT output;
                    output.pos = input.pos;
                    output.uv = input.uv;
                    return output;
                }";

            string psSource = @"
                cbuffer CBuf : register(b0) { float4 CropRect; }
                Texture2D tex : register(t0);
                SamplerState samp : register(s0);
                struct PS_INPUT { float4 pos : SV_POSITION; float2 uv : TEXCOORD; };
                float4 main(PS_INPUT input) : SV_Target {
                    float2 zoomedUV = input.uv * CropRect.zw + CropRect.xy;
                    return tex.Sample(samp, zoomedUV);
                }";

            Blob vsBlob, vsError;
            // Compile(source, defines, include, entryPoint, sourceName, profile, shaderFlags, effectFlags, out blob, out error)
            Compiler.Compile(vsSource, null, null, "main", "VS", "vs_5_0", ShaderFlags.None, EffectFlags.None, out vsBlob, out vsError);
            
            if (vsError != null && (nuint)vsError.BufferSize > 0)
            {
                 string err = Marshal.PtrToStringAnsi(vsError.BufferPointer);
                 if (vsBlob == null) throw new Exception("VS Compile Error: " + err);
            }
            if (vsBlob == null) throw new Exception("VS Compile Failed");
            
            using (vsBlob)
            {
                var span = new ReadOnlySpan<byte>(vsBlob.BufferPointer.ToPointer(), (int)(nuint)vsBlob.BufferSize);
                _vertexShader = _d3dDevice.CreateVertexShader(span);
                
                var inputElements = new[] {
                    new InputElementDescription("POSITION", 0, Format.R32G32B32_Float, 0, 0, InputClassification.PerVertexData, 0),
                    new InputElementDescription("TEXCOORD", 0, Format.R32G32_Float, 12, 0, InputClassification.PerVertexData, 0)
                };
                _inputLayout = _d3dDevice.CreateInputLayout(inputElements, span);
            }

            Blob psBlob, psError;
            Compiler.Compile(psSource, null, null, "main", "PS", "ps_5_0", ShaderFlags.None, EffectFlags.None, out psBlob, out psError);
            
            if (psError != null && (nuint)psError.BufferSize > 0)
            {
                 string err = Marshal.PtrToStringAnsi(psError.BufferPointer);
                 if (psBlob == null) throw new Exception("PS Compile Error: " + err);
            }
            if (psBlob == null) throw new Exception("PS Compile Failed");

            using (psBlob)
            {
                var span = new ReadOnlySpan<byte>(psBlob.BufferPointer.ToPointer(), (int)(nuint)psBlob.BufferSize);
                _pixelShader = _d3dDevice.CreatePixelShader(span);
            }

            float[] vertices = {
                -1f, 1f, 0f,  0f, 0f, // Top-Left
                 1f, 1f, 0f,  1f, 0f, // Top-Right
                -1f,-1f, 0f,  0f, 1f, // Bottom-Left
                 1f,-1f, 0f,  1f, 1f  // Bottom-Right
            };
            
            _vertexBuffer = _d3dDevice.CreateBuffer(vertices, BindFlags.VertexBuffer);
            
            var initialData = new ConstantBufferData { CropRect = new Vector4(0,0,1,1) };
            _constantBuffer = _d3dDevice.CreateBuffer(ref initialData, new BufferDescription(16, BindFlags.ConstantBuffer, ResourceUsage.Dynamic, CpuAccessFlags.Write));
            
            var samplerDesc = new SamplerDescription();
            samplerDesc.Filter = Filter.MinMagMipLinear;
            samplerDesc.AddressU = TextureAddressMode.Clamp;
            samplerDesc.AddressV = TextureAddressMode.Clamp;
            samplerDesc.AddressW = TextureAddressMode.Clamp;
            
            _samplerState = _d3dDevice.CreateSamplerState(samplerDesc);
        }

        public void Render(IDirect3DSurface source, IDirect3DSurface dest, System.Drawing.Rectangle srcRect, System.Drawing.Size totalSize)
        {
            var srcAccess = source.As<EasyScreenRecord.Helpers.IDirect3DDxgiInterfaceAccess>();
            var dstAccess = dest.As<EasyScreenRecord.Helpers.IDirect3DDxgiInterfaceAccess>();
            Guid texGuid = typeof(ID3D11Texture2D).GUID;
            
            using var srcTex = new ID3D11Texture2D(srcAccess.GetInterface(ref texGuid));
            using var dstTex = new ID3D11Texture2D(dstAccess.GetInterface(ref texGuid));

            using var srv = _d3dDevice.CreateShaderResourceView(srcTex);
            using var rtv = _d3dDevice.CreateRenderTargetView(dstTex);

            var srcDesc = srcTex.Description;
            float texW = (float)srcDesc.Width;
            float texH = (float)srcDesc.Height;

            // Normalize using ACTUAL texture dimensions to handle padding/power-of-2 textures
            float x = (float)srcRect.Left / texW;
            float y = (float)srcRect.Top / texH;
            float w = (float)srcRect.Width / texW;
            float h = (float)srcRect.Height / texH;
            
            LastTextureSize = new System.Drawing.Size((int)texW, (int)texH);
            LastCropRect = new Vector4(x, y, w, h);
            
            var cbData = new ConstantBufferData { CropRect = new Vector4(x, y, w, h) };
            unsafe 
            { 
                 var map = _d3dContext.Map(_constantBuffer, 0, MapMode.WriteDiscard, Vortice.Direct3D11.MapFlags.None);
                 System.Runtime.CompilerServices.Unsafe.Copy(map.DataPointer.ToPointer(), ref cbData);
                 _d3dContext.Unmap(_constantBuffer, 0);
            }

            _d3dContext.IASetInputLayout(_inputLayout);
            _d3dContext.IASetPrimitiveTopology(PrimitiveTopology.TriangleStrip);
            
            _d3dContext.IASetVertexBuffers(0, 1, new[] { _vertexBuffer }, new uint[] { 20 }, new uint[] { 0 });

            _d3dContext.VSSetShader(_vertexShader);
            
            _d3dContext.PSSetShader(_pixelShader);
            _d3dContext.PSSetConstantBuffers(0, new[] { _constantBuffer });
            _d3dContext.PSSetShaderResources(0, new [] { srv });
            _d3dContext.PSSetSamplers(0, new [] { _samplerState });
            
            _d3dContext.OMSetRenderTargets(rtv);
            
            var destDesc = dstTex.Description;
            _d3dContext.RSSetViewport(0, 0, destDesc.Width, destDesc.Height);

            _d3dContext.Draw(4, 0);

            _d3dContext.OMSetRenderTargets((ID3D11RenderTargetView)null);
            _d3dContext.PSSetShaderResources(0, new ID3D11ShaderResourceView[] { null });
        }

        public Windows.Graphics.DirectX.Direct3D11.IDirect3DSurface CreateOutputSurface(int width, int height)
        {
            var desc = new Vortice.Direct3D11.Texture2DDescription
            {
                Width = (uint)width,
                Height = (uint)height,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.B8G8R8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource
            };
            
            var texture = _d3dDevice.CreateTexture2D(desc);
            
            // QI for IDXGISurface
            using var dxgiSurface = texture.QueryInterface<Vortice.DXGI.IDXGISurface>();
            var surf = Direct3D11Helper.CreateSurfaceFromNative(dxgiSurface.NativePointer);
            
            texture.Dispose();
            return surf;
        }

        public void Dispose()
        {
            _samplerState?.Dispose();
            _constantBuffer?.Dispose();
            _vertexBuffer?.Dispose();
            _inputLayout?.Dispose();
            _pixelShader?.Dispose();
            _vertexShader?.Dispose();
            _d3dContext?.Dispose();
            _d3dDevice?.Dispose(); 
        }
    }
}
