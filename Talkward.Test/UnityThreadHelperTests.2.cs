using System;
using System.Collections.Generic;
using System.Threading;
using FluentAssertions;
using Talkward.Test.Mocks;

namespace Talkward.Test;

public partial class UnityThreadHelperTests
{
    [Test]
    public void RunQueued_HandlesMultipleMissedSeconds()
    {
        // Arrange
        _mockTimeProvider.SetTime(10.0);
        
        var executionOrder = new List<int>();
        
        void Callback1(object? state) => executionOrder.Add(1);
        void Callback2(object? state) => executionOrder.Add(2);
        void Callback3(object? state) => executionOrder.Add(3);
        void Callback4(object? state) => executionOrder.Add(4);
        void Callback5(object? state) => executionOrder.Add(5);
        
        // Schedule callbacks across multiple seconds
        UnityThreadHelper.Schedule(11.0, Callback1, null);
        UnityThreadHelper.Schedule(13.0, Callback2, null);
        UnityThreadHelper.Schedule(15.0, Callback3, null);
        UnityThreadHelper.Schedule(17.0, Callback4, null);
        UnityThreadHelper.Schedule(19.0, Callback5, null);
        
        // Act - Jump far ahead in time, missing several seconds
        _mockTimeProvider.SetTime(20.0);
        _mockLoopRegistrar.InvokeForType(typeof(UnityThreadHelper));
        
        // Assert
        executionOrder.Should().HaveCount(5, "All five callbacks should execute");
        executionOrder.Should().ContainInOrder([1, 2, 3, 4, 5], 
            "Callbacks should execute in chronological order");
    }

    [Test]
    public void RunQueued_HandlesNonSequentialScheduling()
    {
        // Arrange
        _mockTimeProvider.SetTime(10.0);
        
        var callbacksExecuted = new bool[3];
        
        void Callback1(object? state) => callbacksExecuted[0] = true;
        void Callback2(object? state) => callbacksExecuted[1] = true;
        void Callback3(object? state) => callbacksExecuted[2] = true;
        
        // Schedule in non-sequential order
        UnityThreadHelper.Schedule(20.0, Callback2, null);
        UnityThreadHelper.Schedule(15.0, Callback1, null);
        UnityThreadHelper.Schedule(25.0, Callback3, null);
        
        // Assert initial state
        var initialNextSecond = UnityThreadHelper._nextSecondToRun;
        initialNextSecond.Should().Be(15, "Should track earliest scheduled second");
        
        // Act - Process time 22
        _mockTimeProvider.SetTime(22.0);
        _mockLoopRegistrar.InvokeForType(typeof(UnityThreadHelper));
        
        // Assert
        callbacksExecuted[0].Should().BeTrue("First callback should execute");
        callbacksExecuted[1].Should().BeTrue("Second callback should execute");
        callbacksExecuted[2].Should().BeFalse("Third callback should not execute yet");
        
        UnityThreadHelper._nextSecondToRun.Should().Be(22, "Next second should be 22");
    }

    [Test]
    public void RunQueued_HandlesZeroCallbacksInSecond()
    {
        // Arrange
        _mockTimeProvider.SetTime(10.0);
        
        var executed1 = false;
        var executed2 = false;
        
        void Callback1(object? state) 
        {
            executed1 = true;
        }
        
        void Callback2(object? state) 
        {
            executed2 = true;
        }
        
        // Add callbacks at seconds 15 and 17
        UnityThreadHelper.Schedule(15.0, Callback1, null);
        UnityThreadHelper.Schedule(17.0, Callback2, null);
        
        _mockLoopRegistrar.InvokeForType(typeof(UnityThreadHelper));
        UnityThreadHelper._nextSecondToRun.Should().Be(10, "Next second should be 10 before any callbacks are processed");
        
        // Act - Jump to second 16, processing second 15 but not 17
        _mockTimeProvider.SetTime(16.0);
        _mockLoopRegistrar.InvokeForType(typeof(UnityThreadHelper));
        
        // Assert
        executed1.Should().BeTrue("First callback should execute");
        executed2.Should().BeFalse("Second callback should not execute yet");
        UnityThreadHelper._nextSecondToRun.Should().Be(16, "Next second should be 16");
    }

    [Test]
    public void RunQueued_HandlesLargeTimeJump()
    {
        // Arrange
        _mockTimeProvider.SetTime(0.0);
        
        var executionTimes = new List<double>();
        
        void RecordTimeCallback(object? state)
        {
            executionTimes.Add(_mockTimeProvider.UnscaledTime);
        }
        
        // Schedule callbacks at distant times
        UnityThreadHelper.Schedule(10.0, RecordTimeCallback, null);
        UnityThreadHelper.Schedule(100.0, RecordTimeCallback, null);
        UnityThreadHelper.Schedule(1000.0, RecordTimeCallback, null);
        
        // Act - Make a massive time jump
        _mockTimeProvider.SetTime(1500.0);
        _mockLoopRegistrar.InvokeForType(typeof(UnityThreadHelper));
        
        // Assert
        executionTimes.Should().HaveCount(3, "All callbacks should have executed despite large time jump");
        executionTimes.Should().AllBeEquivalentTo(1500.0, "All should record execution at the current time");
        UnityThreadHelper._nextSecondToRun.Should().Be(1500, "Next second should be the current second");
    }

    [Test]
    public void RunQueued_ExecutesSameSecondCallbacksInScheduledOrder()
    {
        // Arrange
        _mockTimeProvider.SetTime(10.0);
        
        var executionOrder = new List<int>();
        
        void Callback1(object? state) => executionOrder.Add(1);
        void Callback2(object? state) => executionOrder.Add(2);
        void Callback3(object? state) => executionOrder.Add(3);
        
        // Schedule multiple callbacks at the same second but with different fractional times
        UnityThreadHelper.Schedule(15.9, Callback3, null);
        UnityThreadHelper.Schedule(15.5, Callback2, null);
        UnityThreadHelper.Schedule(15.1, Callback1, null);
        
        // Act
        _mockTimeProvider.SetTime(16.0);
        _mockLoopRegistrar.InvokeForType(typeof(UnityThreadHelper));
        
        // Assert - Since the RefList preserves insertion order, we would expect them to execute in the order added
        executionOrder.Should().HaveCount(3);
        executionOrder.Should().ContainInOrder([3, 2, 1], 
            "Callbacks should execute in the order they were scheduled");
    }
}
