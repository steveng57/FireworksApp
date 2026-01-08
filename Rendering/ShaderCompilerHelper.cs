using System;
using System.Diagnostics;
using Vortice.D3DCompiler;

namespace FireworksApp.Rendering;

internal static class ShaderCompilerHelper
{
    /// <summary>
    /// Compiles HLSL and logs full details if compilation throws. Returns default on failure.
    /// </summary>
    public static ReadOnlyMemory<byte> CompileAndCatch(string source, string entryPoint, string filePath, string profile)
    {
        try
        {
            return Compiler.Compile(source, entryPoint, filePath, profile);
        }
        catch (Exception ex)
        {
            Debug.WriteLine("[Shader Compile Error]");
            Debug.WriteLine($"  FilePath: {filePath}");
            Debug.WriteLine($"  EntryPoint: {entryPoint}");
            Debug.WriteLine($"  Profile: {profile}");
            Debug.WriteLine("  Exception: " + ex.GetType().FullName);
            Debug.WriteLine("  Message: " + ex.Message);
            Debug.WriteLine("  StackTrace: " + ex.StackTrace);

            var inner = ex.InnerException;
            int depth = 0;
            while (inner != null && depth < 5)
            {
                Debug.WriteLine($"  Inner[{depth}] Type: {inner.GetType().FullName}");
                Debug.WriteLine($"  Inner[{depth}] Message: {inner.Message}");
                Debug.WriteLine($"  Inner[{depth}] StackTrace: {inner.StackTrace}");
                inner = inner.InnerException;
                depth++;
            }

            return default;
        }
    }
}
