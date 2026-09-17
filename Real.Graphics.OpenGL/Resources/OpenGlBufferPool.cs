using System;
using System.Collections.Generic;
using Real.Graphics.Rhi.Descriptors;
using Real.Graphics.Rhi.Enums;
using Real.Graphics.Rhi.Handles;
using Silk.NET.OpenGL;

namespace Real.Graphics.OpenGL.Resources;

internal readonly struct OpenGlBufferEntry
{
    public readonly uint Handle;
    public readonly ulong Size;
    public readonly BufferUsage Usage;
    public readonly BufferDescriptor Descriptor;

    public OpenGlBufferEntry(uint handle, ulong size, BufferUsage usage, BufferDescriptor descriptor)
    {
        Handle = handle;
        Size = size;
        Usage = usage;
        Descriptor = descriptor;
    }
}

internal sealed class OpenGlBufferPool : IDisposable
{
    private readonly GL _gl;
    private readonly List<OpenGlBufferEntry?> _slots = new();
    private readonly List<uint> _generations = new();
    private readonly Queue<uint> _freeSlots = new();

    public OpenGlBufferPool(GL gl)
    {
        _gl = gl;

        // Reserve slot 0 so valid handles always have Id > 0 (BufferHandle.Invalid is (0, 0))
        _slots.Add(null);
        _generations.Add(0);
    }

    public unsafe BufferHandle Create(in BufferDescriptor descriptor, ReadOnlySpan<byte> initialData = default)
    {
        _gl.GenBuffers(1, out uint buffer);
        if (buffer == 0)
            throw new InvalidOperationException("Failed to create OpenGL buffer.");

        var usage = descriptor.CpuVisible ? BufferUsageARB.DynamicDraw : BufferUsageARB.StaticDraw;
        _gl.BindBuffer(BufferTargetARB.CopyWriteBuffer, buffer);
        if (!initialData.IsEmpty)
        {
            fixed (byte* ptr = initialData)
            {
                _gl.BufferData(BufferTargetARB.CopyWriteBuffer, (nuint)Math.Max(descriptor.Size, (ulong)initialData.Length), ptr, usage);
            }
        }
        else
        {
            _gl.BufferData(BufferTargetARB.CopyWriteBuffer, (nuint)descriptor.Size, null, usage);
        }
        _gl.BindBuffer(BufferTargetARB.CopyWriteBuffer, 0);

        uint slotIndex;
        if (_freeSlots.Count > 0)
        {
            slotIndex = _freeSlots.Dequeue();
            _slots[(int)slotIndex] = new OpenGlBufferEntry(buffer, descriptor.Size, descriptor.Usage, descriptor);
        }
        else
        {
            slotIndex = (uint)_slots.Count;
            _slots.Add(new OpenGlBufferEntry(buffer, descriptor.Size, descriptor.Usage, descriptor));
            _generations.Add(1);
        }

        return new BufferHandle(slotIndex, _generations[(int)slotIndex]);
    }

    public unsafe void Destroy(BufferHandle handle)
    {
        if (!IsValid(handle)) return;

        var entry = _slots[(int)handle.Id]!.Value;
        uint id = entry.Handle;
        _gl.DeleteBuffers(1, in id);

        _slots[(int)handle.Id] = null;
        _generations[(int)handle.Id]++;
        _freeSlots.Enqueue(handle.Id);
    }

    public bool IsValid(BufferHandle handle) =>
        handle.Id != 0 &&
        handle.Id < _slots.Count &&
        _slots[(int)handle.Id] is not null &&
        _generations[(int)handle.Id] == handle.Generation;

    public OpenGlBufferEntry Get(BufferHandle handle)
    {
        if (!IsValid(handle))
            throw new ArgumentException($"Invalid or stale BufferHandle {handle}.", nameof(handle));
        return _slots[(int)handle.Id]!.Value;
    }

    public unsafe void Dispose()
    {
        for (int i = 1; i < _slots.Count; i++)
        {
            if (_slots[i] is { } entry)
            {
                uint id = entry.Handle;
                _gl.DeleteBuffers(1, in id);
                _slots[i] = null;
            }
        }
        _freeSlots.Clear();
    }
}
