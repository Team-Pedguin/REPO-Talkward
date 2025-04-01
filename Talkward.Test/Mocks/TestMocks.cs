using System;
using System.Collections.Generic;
using UnityEngine.LowLevel;

namespace Talkward.Test.Mocks;

/// <summary>
/// A mock time provider that allows manually setting the current time for testing purposes.
/// </summary>
public class MockTimeProvider : ITimeProvider
{
    private double _currentTime;

    public double UnscaledTime => _currentTime;

    /// <summary>
    /// Sets the current time to a specific value.
    /// </summary>
    public void SetTime(double time) => _currentTime = time;

    /// <summary>
    /// Advances the current time by the specified amount.
    /// </summary>
    public void AdvanceTime(double deltaTime) => _currentTime += deltaTime;
}

/// <summary>
/// A mock player loop registrar that tracks registered update functions and allows
/// manually triggering them for testing purposes.
/// </summary>
public class MockPlayerLoopRegistrar : IPlayerLoopRegistrar
{
    public List<(Type Type, Action UpdateDelegate)> RegisteredFunctions { get; } = [];

    public void RegisterUpdateFunction(Type type, Action updateDelegate)
    {
        RegisteredFunctions.Add((type, updateDelegate));
    }

    /// <summary>
    /// Invokes all registered update functions.
    /// </summary>
    public void InvokeAll()
    {
        foreach (var (_, updateDelegate) in RegisteredFunctions)
        {
            updateDelegate();
        }
    }

    /// <summary>
    /// Invokes only update functions registered with the specified type.
    /// </summary>
    public void InvokeForType(Type type)
    {
        foreach (var (registeredType, updateDelegate) in RegisteredFunctions)
        {
            if (registeredType == type)
            {
                try
                {
                    updateDelegate();
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error invoking update function: {updateDelegate.Method.DeclaringType?.FullName}.{updateDelegate.Method.Name}", ex);
                }
            }
        }
    }
}
