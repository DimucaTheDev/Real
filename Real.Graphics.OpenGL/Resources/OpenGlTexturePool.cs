using System;
using System.Collections.Generic;
using Real.Graphics.OpenGL.Translation;
using Real.Graphics.Rhi.Descriptors;
using Real.Graphics.Rhi.Enums;
using Real.Graphics.Rhi.Handles;
using Silk.NET.OpenGL;

namespace Real.Graphics.OpenGL.Resources;

internal readonly struct OpenGlTextureEntry
{
    public readonly uint Handle;
    public readonly TextureDescriptor Descriptor;
    public readonly bool OwnsTexture;

    public OpenGlTextureEntry(uint handle, in TextureDescriptor descriptor, bool ownsTexture = true)
    {
        Handle = handle;
        Descriptor = descriptor;
        OwnsTexture = ownsTexture;
    }
}

internal sealed class OpenGlTexturePool : IDisposable
{
    private readonly GL _gl;
    private readonly List<OpenGlTextureEntry?> _slots = new();
    private readonly List<uint> _generations = new();
    private readonly Queue<uint> _freeSlots = new();

    public OpenGlTexturePool(GL gl)
    {
        _gl = gl;
        
        // Reserve slot 0 so valid handles always have Id > 0 (TextureHandle.Invalid is (0, 0))
        _slots.Add(null);
        _generations.Add(0);
    }

    public unsafe TextureHandle Create(in TextureDescriptor descriptor, ReadOnlySpan<byte> initialData = default)
    {
        _gl.GenTextures(1, out uint texture);
        if (texture == 0)
            throw new InvalidOperationException("Failed to create OpenGL texture.");

        _gl.BindTexture(TextureTarget.Texture2D, texture);

        var internalFormat = GlFormatMap.ToSizedInternalFormat(descriptor.Format);
        uint levels = Math.Max(1u, descriptor.MipLevels);
        uint width = Math.Max(1u, descriptor.Width);
        uint height = Math.Max(1u, descriptor.Height);

        _gl.TexStorage2D(TextureTarget.Texture2D, levels, internalFormat, width, height);

        if (!initialData.IsEmpty)
        {
            var pixelFormat = GlFormatMap.ToPixelFormat(descriptor.Format);
            var pixelType = GlFormatMap.ToPixelType(descriptor.Format);
            fixed (byte* ptr = initialData)
            {
                _gl.TexSubImage2D(
                    TextureTarget.Texture2D,
                    0,
                    0, 0,
                    width,
                    height,
                    pixelFormat,
                    pixelType,
                    ptr);
            }
        }

        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

        _gl.BindTexture(TextureTarget.Texture2D, 0);

        return AllocateSlot(new OpenGlTextureEntry(texture, descriptor, ownsTexture: true));
    }

    public TextureHandle Wrap(uint textureId, in TextureDescriptor descriptor, bool ownsTexture = false)
    {
        return AllocateSlot(new OpenGlTextureEntry(textureId, descriptor, ownsTexture));
    }

    public unsafe void Destroy(TextureHandle handle)
    {
        if (!IsValid(handle)) return;

        var entry = _slots[(int)handle.Id]!.Value;
        if (entry.OwnsTexture && entry.Handle != 0)
        {
            uint id = entry.Handle;
            _gl.DeleteTextures(1, in id);
        }

        _slots[(int)handle.Id] = null;
        _generations[(int)handle.Id]++;
        _freeSlots.Enqueue(handle.Id);
    }

    public bool IsValid(TextureHandle handle) =>
        handle.Id != 0 &&
        handle.Id < _slots.Count &&
        _slots[(int)handle.Id] is not null &&
        _generations[(int)handle.Id] == handle.Generation;

    public OpenGlTextureEntry Get(TextureHandle handle)
    {
        if (!IsValid(handle))
            throw new ArgumentException($"Invalid or stale TextureHandle {handle}.", nameof(handle));
        return _slots[(int)handle.Id]!.Value;
    }

    private TextureHandle AllocateSlot(OpenGlTextureEntry entry)
    {
        uint slotIndex;
        if (_freeSlots.Count > 0)
        {
            slotIndex = _freeSlots.Dequeue();
            _slots[(int)slotIndex] = entry;
        }
        else
        {
            slotIndex = (uint)_slots.Count;
            _slots.Add(entry);
            _generations.Add(1);
        }

        return new TextureHandle(slotIndex, _generations[(int)slotIndex]);
    }

    public unsafe void Dispose()
    {
        for (int i = 1; i < _slots.Count; i++)
        {
            if (_slots[i] is { } entry)
            {
                if (entry.OwnsTexture && entry.Handle != 0)
                {
                    uint id = entry.Handle;
                    _gl.DeleteTextures(1, in id);
                }
                _slots[i] = null;
            }
        }
        _freeSlots.Clear();
    }
}
