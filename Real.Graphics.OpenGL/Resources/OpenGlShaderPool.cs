using System;
using System.Collections.Generic;
using Real.Graphics.Rhi.Enums;
using Real.Graphics.Rhi.Handles;
using Silk.NET.OpenGL;

namespace Real.Graphics.OpenGL.Resources;

internal readonly struct OpenGlShaderEntry
{
    public readonly uint ShaderId;
    public readonly ShaderStage Stage;
    public readonly string EntryPoint;

    public OpenGlShaderEntry(uint shaderId, ShaderStage stage, string entryPoint)
    {
        ShaderId = shaderId;
        Stage = stage;
        EntryPoint = entryPoint;
    }
}

internal sealed class OpenGlShaderPool : IDisposable
{
    private readonly GL _gl;
    private readonly List<OpenGlShaderEntry?> _slots = new();
    private readonly List<uint> _generations = new();
    private readonly Queue<uint> _freeSlots = new();

    public OpenGlShaderPool(GL gl)
    {
        _gl = gl;

        // Reserve slot 0 so valid handles always have Id > 0 (ShaderHandle.Invalid is (0, 0))
        _slots.Add(null);
        _generations.Add(0);
    }

    public ShaderHandle Store(uint shaderId, ShaderStage stage, string entryPoint)
    {
        uint slotIndex;
        if (_freeSlots.Count > 0)
        {
            slotIndex = _freeSlots.Dequeue();
            _slots[(int)slotIndex] = new OpenGlShaderEntry(shaderId, stage, entryPoint);
        }
        else
        {
            slotIndex = (uint)_slots.Count;
            _slots.Add(new OpenGlShaderEntry(shaderId, stage, entryPoint));
            _generations.Add(1);
        }

        return new ShaderHandle(slotIndex, _generations[(int)slotIndex]);
    }

    public void Destroy(ShaderHandle handle)
    {
        if (!IsValid(handle)) return;

        var entry = _slots[(int)handle.Id]!.Value;
        if (entry.ShaderId != 0)
        {
            _gl.DeleteShader(entry.ShaderId);
        }

        _slots[(int)handle.Id] = null;
        _generations[(int)handle.Id]++;
        _freeSlots.Enqueue(handle.Id);
    }

    public bool IsValid(ShaderHandle handle) =>
        handle.Id != 0 &&
        handle.Id < _slots.Count &&
        _slots[(int)handle.Id] is not null &&
        _generations[(int)handle.Id] == handle.Generation;

    public OpenGlShaderEntry Get(ShaderHandle handle)
    {
        if (!IsValid(handle))
            throw new ArgumentException($"Invalid or stale ShaderHandle {handle}.", nameof(handle));
        return _slots[(int)handle.Id]!.Value;
    }

    public void Dispose()
    {
        for (int i = 1; i < _slots.Count; i++)
        {
            if (_slots[i] is { } entry)
            {
                if (entry.ShaderId != 0)
                {
                    _gl.DeleteShader(entry.ShaderId);
                }
                _slots[i] = null;
            }
        }
        _freeSlots.Clear();
    }
}
