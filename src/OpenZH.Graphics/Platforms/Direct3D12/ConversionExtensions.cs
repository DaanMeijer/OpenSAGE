﻿using System;
using SharpDX.Direct3D;
using SharpDX.Direct3D12;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;

namespace OpenZH.Graphics.Platforms.Direct3D12
{
    internal static class ConversionExtensions
    {
        public static DescriptorRangeType ToDescriptorRangeType(this BindingType value)
        {
            switch (value)
            {
                case BindingType.ConstantBuffer:
                    return DescriptorRangeType.ConstantBufferView;

                case BindingType.Sampler:
                    return DescriptorRangeType.Sampler;

                case BindingType.Texture:
                    return DescriptorRangeType.ShaderResourceView;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static Format ToDxgiFormat(this IndexType value)
        {
            switch (value)
            {
                case IndexType.UInt16:
                    return Format.R16_UInt;

                case IndexType.UInt32:
                    return Format.R32_UInt;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static Format ToDxgiFormat(this PixelFormat value)
        {
            switch (value)
            {
                case PixelFormat.Bc1:
                    return Format.BC1_UNorm;

                case PixelFormat.Bc2:
                    return Format.BC2_UNorm;

                case PixelFormat.Bc3:
                    return Format.BC3_UNorm;

                case PixelFormat.Rgba8UNorm:
                    return Format.R8G8B8A8_UNorm;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static Format ToDxgiFormat(this VertexFormat value)
        {
            switch (value)
            {
                case VertexFormat.Float2:
                    return Format.R32G32_Float;

                case VertexFormat.Float3:
                    return Format.R32G32B32_Float;

                case VertexFormat.Float4:
                    return Format.R32G32B32A32_Float;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static PrimitiveTopology ToPrimitiveTopology(this PrimitiveType value)
        {
            switch (value)
            {
                case PrimitiveType.TriangleList:
                    return PrimitiveTopology.TriangleList;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static RawColor4 ToRawColor4(this ColorRgba value)
        {
            return new RawColor4(value.R, value.G, value.B, value.A);
        }

        public static ShaderVisibility ToShaderVisibility(this ShaderStageVisibility value)
        {
            switch (value)
            {
                case ShaderStageVisibility.All:
                    return ShaderVisibility.All;

                case ShaderStageVisibility.Vertex:
                    return ShaderVisibility.Vertex;

                case ShaderStageVisibility.Pixel:
                    return ShaderVisibility.Pixel;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static RawViewportF ToViewportF(this Viewport v)
        {
            return new RawViewportF
            {
                X = v.X,
                Y = v.Y,
                Width = v.Width,
                Height = v.Height,
                MinDepth = v.MinDepth,
                MaxDepth = v.MaxDepth
            };
        }

        public static ResourceStates ToResourceStates(this GraphicsResourceState resourceState)
        {
            switch (resourceState)
            {
                case GraphicsResourceState.Present:
                    return ResourceStates.Present;

                case GraphicsResourceState.RenderTarget:
                    return ResourceStates.RenderTarget;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}