using System.IO;
using System.Reflection;
using System.Resources;
using Talkward.Sam;

namespace Talkward;

/// <summary>
/// A class retrieving raw resources from assemblies.
/// </summary>
[PublicAPI]
public static class RawResources
{
    /// <summary>
    /// Get a raw resource from the assembly of the specified type.
    /// </summary>
    /// <param name="type">The type within an assembly that holds the resource.</param>
    /// <param name="name">The name of the resource.</param>
    /// <returns>A <see cref="RawResource"/> representing the resource.</returns>
    /// <exception cref="MissingManifestResourceException">If the resource is not found.</exception>
    /// <exception cref="InvalidOperationException">If the resource does not follow standard layout.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RawResource Get(string name)
        => Get(Assembly.GetCallingAssembly(), name);
    /// <summary>
    /// Get a raw resource from the assembly of the specified type.
    /// </summary>
    /// <param name="type">The type within an assembly that holds the resource.</param>
    /// <param name="name">The name of the resource.</param>
    /// <returns>A <see cref="RawResource"/> representing the resource.</returns>
    /// <exception cref="MissingManifestResourceException">If the resource is not found.</exception>
    /// <exception cref="InvalidOperationException">If the resource does not follow standard layout.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RawResource Get(Type type, string name)
        => Get(type.Assembly, name);

    /// <summary>
    /// Get a raw resource from the specified assembly.
    /// </summary>
    /// <param name="assembly">The assembly that holds the resource.</param>
    /// <param name="name">The name of the resource.</param>
    /// <returns>A <see cref="RawResource"/> representing the resource.</returns>
    /// <exception cref="MissingManifestResourceException">If the resource is not found.</exception>
    /// <exception cref="InvalidOperationException">If the resource does not follow standard layout.</exception>
    public static unsafe RawResource Get(Assembly assembly, string name)
    {
        var mrs = assembly.GetManifestResourceStream(name);
        if (mrs == null)
            throw new MissingManifestResourceException(
                $"Resource '{name}' not found in assembly '{assembly.FullName}'.");
        if (mrs is not UnmanagedMemoryStream stream)
            throw new InvalidOperationException($"Resource '{name}' does not follow standard layout.");
        var start = stream.PositionPointer;
        var length = (nuint) stream.Length; // will not be negative
        stream.Dispose(); // basically does nothing for UnmanagedMemoryStream
        return new RawResource(start, length);
    }
}

/// <summary>
/// A class retrieving raw resources from assemblies relative to a specified type.
/// </summary>
/// <typeparam name="T">The type within an assembly that holds the resource.</typeparam>
[PublicAPI]
public static class RawResources<T>
{
    /// <summary>
    /// Get a raw resource from the assembly of the specified type.
    /// </summary>
    /// <param name="name">The name of the resource.</param>
    /// <returns>A <see cref="RawResource"/> representing the resource.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RawResource Get(string name)
        => RawResources.Get(typeof(T), name);
}