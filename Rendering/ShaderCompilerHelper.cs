using System;
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
            Console.WriteLine("[Shader Compile Error]");
            Console.WriteLine($"  FilePath: {filePath}");
            Console.WriteLine($"  EntryPoint: {entryPoint}");
            Console.WriteLine($"  Profile: {profile}");
            Console.WriteLine("  Exception: " + ex.GetType().FullName);
            Console.WriteLine("  Message: " + ex.Message);
            Console.WriteLine("  StackTrace: " + ex.StackTrace);

            var inner = ex.InnerException;
            int depth = 0;
            while (inner != null && depth < 5)
            {
                Console.WriteLine($"  Inner[{depth}] Type: {inner.GetType().FullName}");
                Console.WriteLine($"  Inner[{depth}] Message: {inner.Message}");
                Console.WriteLine($"  Inner[{depth}] StackTrace: {inner.StackTrace}");
                inner = inner.InnerException;
                depth++;
            }

            return default;
        }
    }
}
