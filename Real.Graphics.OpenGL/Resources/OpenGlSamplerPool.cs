using System;
using System.Collections.Generic;
using Real.Graphics.Rhi.Descriptors;
using Real.Graphics.Rhi.Handles;
using Silk.NET.OpenGL;

namespace Real.Graphics.OpenGL.Resources;

internal sealed class OpenGlSamplerPool : IDisposable
{
    private readonly GL _gl;
    private readonly List<uint?> _slots = new();
    private readonly List<uint> _generations = new();
    private readonly Queue<uint> _freeSlots = new();

    public OpenGlSamplerPool(GL gl)
    {
        _gl = gl;

        // Reserve slot 0 so valid handles always have Id > 0 (SamplerHandle.Invalid is (0, 0))
        _slots.Add(null);
        _generations.Add(0);
    }

    public unsafe SamplerHandle Create(in SamplerDescriptor descriptor)
    {
        _gl.CreateSamplers(1, out uint sampler);
        if (sampler == 0)
            throw new InvalidOperationException("Failed to create OpenGL sampler.");

        var minFilter = descriptor.MinFilter == FilterMode.Nearest ? TextureMinFilter.Nearest : TextureMinFilter.Linear;
        var magFilter = descriptor.MagFilter == FilterMode.Nearest ? TextureMagFilter.Nearest : TextureMagFilter.Linear;
        var wrapS = ToGlWrap(descriptor.AddressU);
        var wrapT = ToGlWrap(descriptor.AddressV);

        _gl.SamplerParameter(sampler, SamplerParameterI.MinFilter, (int)minFilter);
        _gl.SamplerParameter(sampler, SamplerParameterI.MagFilter, (int)magFilter);
        _gl.SamplerParameter(sampler, SamplerParameterI.WrapS, (int)wrapS);
        _gl.SamplerParameter(sampler, SamplerParameterI.WrapT, (int)wrapT);
        _gl.SamplerParameter(sampler, SamplerParameterF.MaxAnisotropy, Math.Max(1.0f, descriptor.MaxAnisotropy));

        uint slotIndex;
        if (_freeSlots.Count > 0)
        {
            slotIndex = _freeSlots.Dequeue();
            _slots[(int)slotIndex] = sampler;
        }
        else
        {
            slotIndex = (uint)_slots.Count;
            _slots.Add(sampler);
            _generations.Add(1);
        }

        return new SamplerHandle(slotIndex, _generations[(int)slotIndex]);
    }

    private static TextureWrapMode ToGlWrap(AddressMode mode) => mode switch
    {
        AddressMode.Repeat => TextureWrapMode.Repeat,
        AddressMode.ClampToEdge => TextureWrapMode.ClampToEdge,
        AddressMode.MirroredRepeat => TextureWrapMode.MirroredRepeat,
        _ => TextureWrapMode.Repeat
    };

    public unsafe void Destroy(SamplerHandle handle)
    {
        if (!IsValid(handle)) return;

        uint id = _slots[(int)handle.Id]!.Value;
        _gl.DeleteSamplers(1, in id);

        _slots[(int)handle.Id] = null;
        _generations[(int)handle.Id]++;
        _freeSlots.Enqueue(handle.Id);
    }

    public bool IsValid(SamplerHandle handle) =>
        handle.Id != 0 &&
        handle.Id < _slots.Count &&
        _slots[(int)handle.Id] is not null &&
        _generations[(int)handle.Id] == handle.Generation;

    public uint Get(SamplerHandle handle)
    {
        if (!IsValid(handle))
            throw new ArgumentException($"Invalid or stale SamplerHandle {handle}.", nameof(handle));
        return _slots[(int)handle.Id]!.Value;
    }

    public unsafe void Dispose()
    {
        for (int i = 1; i < _slots.Count; i++)
        {
            if (_slots[i] is { } id)
            {
                _gl.DeleteSamplers(1, in id);
                _slots[i] = null;
            }
        }
        _freeSlots.Clear();
    }
}
