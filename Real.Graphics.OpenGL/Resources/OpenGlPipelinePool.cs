using System;
using System.Collections.Generic;
using Real.Graphics.OpenGL.Translation;
using Real.Graphics.Rhi.Descriptors;
using Real.Graphics.Rhi.Enums;
using Real.Graphics.Rhi.Handles;
using Silk.NET.OpenGL;

namespace Real.Graphics.OpenGL.Resources;

internal readonly struct OpenGlPipelineEntry
{
    public readonly uint Program;
    public readonly uint Vao;
    public readonly PipelineDescriptor Descriptor;
    public readonly PrimitiveType Topology;

    public OpenGlPipelineEntry(uint program, uint vao, in PipelineDescriptor descriptor, PrimitiveType topology)
    {
        Program = program;
        Vao = vao;
        Descriptor = descriptor;
        Topology = topology;
    }
}

internal sealed class OpenGlPipelinePool : IDisposable
{
    private readonly GL _gl;
    private readonly OpenGlShaderPool _shaderPool;
    private readonly List<OpenGlPipelineEntry?> _slots = new();
    private readonly List<uint> _generations = new();
    private readonly Queue<uint> _freeSlots = new();

    public OpenGlPipelinePool(GL gl, OpenGlShaderPool shaderPool)
    {
        _gl = gl;
        _shaderPool = shaderPool;

        // Reserve slot 0 so valid handles always have Id > 0 (PipelineHandle.Invalid is (0, 0))
        _slots.Add(null);
        _generations.Add(0);
    }

    public unsafe PipelineHandle Create(in PipelineDescriptor descriptor)
    {
        var vs = _shaderPool.Get(descriptor.VertexShader);
        var fs = _shaderPool.Get(descriptor.FragmentShader);

        uint program = _gl.CreateProgram();
        if (program == 0)
            throw new InvalidOperationException($"Failed to create OpenGL program for pipeline '{descriptor.DebugName}'.");

        _gl.AttachShader(program, vs.ShaderId);
        _gl.AttachShader(program, fs.ShaderId);
        _gl.LinkProgram(program);

        _gl.GetProgram(program, ProgramPropertyARB.LinkStatus, out int linkStatus);
        if (linkStatus == 0)
        {
            string infoLog = _gl.GetProgramInfoLog(program);
            _gl.DeleteProgram(program);
            throw new InvalidOperationException($"Failed to link OpenGL program '{descriptor.DebugName}': {infoLog}");
        }

        // Create Vertex Array Object (VAO)
        _gl.GenVertexArrays(1, out uint vao);

        var topology = GlTopologyTranslator.ToGl(descriptor.Topology);
        var entry = new OpenGlPipelineEntry(program, vao, descriptor, topology);

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

        return new PipelineHandle(slotIndex, _generations[(int)slotIndex]);
    }

    public unsafe void Destroy(PipelineHandle handle)
    {
        if (!IsValid(handle)) return;

        var entry = _slots[(int)handle.Id]!.Value;
        if (entry.Program != 0)
        {
            _gl.DeleteProgram(entry.Program);
        }
        if (entry.Vao != 0)
        {
            uint vao = entry.Vao;
            _gl.DeleteVertexArrays(1, in vao);
        }

        _slots[(int)handle.Id] = null;
        _generations[(int)handle.Id]++;
        _freeSlots.Enqueue(handle.Id);
    }

    public bool IsValid(PipelineHandle handle) =>
        handle.Id != 0 &&
        handle.Id < _slots.Count &&
        _slots[(int)handle.Id] is not null &&
        _generations[(int)handle.Id] == handle.Generation;

    public OpenGlPipelineEntry Get(PipelineHandle handle)
    {
        if (!IsValid(handle))
            throw new ArgumentException($"Invalid or stale PipelineHandle {handle}.", nameof(handle));
        return _slots[(int)handle.Id]!.Value;
    }

    public unsafe void Dispose()
    {
        for (int i = 1; i < _slots.Count; i++)
        {
            if (_slots[i] is { } entry)
            {
                if (entry.Program != 0)
                {
                    _gl.DeleteProgram(entry.Program);
                }
                if (entry.Vao != 0)
                {
                    uint vao = entry.Vao;
                    _gl.DeleteVertexArrays(1, in vao);
                }
                _slots[i] = null;
            }
        }
        _freeSlots.Clear();
    }
}
