using UnityEngine;
using Debug = System.Diagnostics.Debug;

namespace Talkward;

public static class Helpers
{
    /*public static T Get<T>(this JsonElement element, string name, T defaultValue = default!)
    {
        if (element.TryGetProperty(name, out var property))
        {
            return property.Deserialize<T>() ?? defaultValue;
        }

        return defaultValue;
    }*/

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TTo UnsafeBitCast<TFrom, TTo>(TFrom source)
        where TFrom : struct
        where TTo : struct
    {
        if (Unsafe.SizeOf<TFrom>() != Unsafe.SizeOf<TTo>())
            ThrowHelper.Throw<NotSupportedException>();
        return Unsafe.ReadUnaligned<TTo>(ref Unsafe.As<TFrom, byte>(ref source));
    }
    
    /// <summary>
    /// Dumps the entire GameObject hierarchy with components to the log
    /// </summary>
    /// <param name="root">The root GameObject to start the dump from</param>
    /// <param name="log">A logging action. Can be Debug.Log or any other logging method</param>
    /// <param name="maxDepth">Maximum depth to traverse, -1 for unlimited</param>
    /// <param name="includeInactive">Whether to include inactive GameObjects</param>
    /// <exception cref="ArgumentNullException">Thrown when the root or log action is null</exception>
    public static void LogHierarchy(this GameObject root, Action<string> log, int maxDepth = -1, bool includeInactive = true)
    {
        if (root == null)
            throw new ArgumentNullException(nameof(root));
        if (log == null)
            throw new ArgumentNullException(nameof(log));
    
        log("===== HIERARCHY DUMP =====");
        LogHierarchyInternal(root, 0, log, maxDepth, includeInactive);
        log("==========================");
    }
    
    /// <summary>
    /// Dumps the entire GameObject hierarchy with components to UnityEngine.Debug.Log
    /// </summary>
    public static void LogHierarchy(this GameObject root, int maxDepth = -1, bool includeInactive = true)
    {
        LogHierarchy(root, UnityEngine.Debug.Log, maxDepth, includeInactive);
    }
    
    private static void LogHierarchyInternal(GameObject obj, int depth, Action<string> log, int maxDepth, bool includeInactive)
    {
        if (maxDepth >= 0 && depth > maxDepth) return;
        if (!obj.activeSelf && !includeInactive) return;
    
        // Create indentation
        var indent = new string(' ', depth * 2);
        var name = string.IsNullOrEmpty(obj.name)
            ? $"(ID {obj.GetInstanceID()})"
            : obj.name;
    
        // Log GameObject info
        log($"{indent}{name} {(obj.activeSelf ? "(active)" : "(inactive)")}");
    
        // Log all components
        var components = obj.GetComponents<Component>();
        for (var i = 0; i < components.Length; i++)
        {
            var component = components[i];
            if (!component) continue;

            var j = i == components.Length - 1 ? "└" : "├";

            var compName = component.GetType().Name;
            var compId = component.GetInstanceID();

            var additionalInfo = component switch
            {
                MonoBehaviour mb => mb.enabled ? " (enabled)" : " (disabled)",
                Renderer renderer => renderer.enabled ? " (visible)" : " (hidden)",
                _ => string.Empty
            };

            log($"{indent}  {j}─ {compName}{additionalInfo} (ID: {compId})");
        }

        // Recursively process children
        for (var i = 0; i < obj.transform.childCount; i++)
            LogHierarchyInternal(obj.transform.GetChild(i).gameObject, depth + 1, log, maxDepth, includeInactive);
    }
    
    public static Color WithAlpha(this Color color, float alpha)
    {
        var newColor = color;
        newColor.a = alpha;
        return newColor;
    }
}