using System;
using System.Threading;
using FluentAssertions;
using Talkward.Test.Mocks;

namespace Talkward.Test;

public partial class UnityThreadHelperTests
{
    private MockTimeProvider _mockTimeProvider = null!;
    private MockPlayerLoopRegistrar _mockLoopRegistrar = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _mockTimeProvider = new MockTimeProvider();
        _mockLoopRegistrar = new MockPlayerLoopRegistrar();
        UnityThreadHelper._timeProvider = _mockTimeProvider;
        UnityThreadHelper._loopRegistrar = _mockLoopRegistrar;
        UnityThreadHelper._logger = null;
        UnityThreadHelper.Initialize();
    }
    
    [SetUp]
    public void Setup()
    {
        UnityThreadHelper.ResetForTesting();
    }

    [TearDown]
    public void TearDown()
    {
        UnityThreadHelper.ResetForTesting();
    }

    [Test]
    public void Initialize_RegistersUpdateFunction()
    {
        // Assert
        UnityThreadHelper.Initialized.Value.Should().BeTrue();
        _mockLoopRegistrar.RegisteredFunctions.Should().HaveCount(1);
        _mockLoopRegistrar.RegisteredFunctions[0].Type.Should().Be(typeof(UnityThreadHelper));
    }

    [Test]
    public void Post_EnqueuesCallback()
    {
        // Arrange
        var callbackExecuted = false;
        var expectedState = "test state";
        string? actualState = null;

        void Callback(object? state)
        {
            callbackExecuted = true;
            actualState = state as string;
        }

        // Act
        UnityThreadHelper.Post(Callback, expectedState);
        _mockLoopRegistrar.InvokeForType(typeof(UnityThreadHelper));

        // Assert
        callbackExecuted.Should().BeTrue();
        actualState.Should().Be(expectedState);
    }

    [Test]
    public void Send_BlocksUntilCallbackCompletes()
    {
        // Arrange
        var callbackExecuted = false;
        var testState = "test state";

        void Callback(object? state)
        {
            Thread.Sleep(100); // Simulate work
            callbackExecuted = true;
        }

        // Act - this would hang if Send didn't work properly
        // We need to run the Unity update function in a separate thread
        var thread = new Thread(() =>
        {
            var started = DateTime.Now;
            Thread.Sleep(50); // Give Send time to enqueue
            do
            {
                _mockLoopRegistrar.InvokeForType(typeof(UnityThreadHelper));
                Thread.Sleep(50);
            } while (!callbackExecuted && (DateTime.Now - started).TotalSeconds <= 5);
        });
        thread.Start();

        UnityThreadHelper.Send(Callback, testState);

        // Assert
        callbackExecuted.Should().BeTrue();
    }

    [Test]
    public void Schedule_EnqueuesForFutureExecution()
    {
        // Arrange
        _mockTimeProvider.SetTime(10.0);
        var callbackExecuted = false;
        var scheduledTime = 15.0;

        void Callback(Empty _)
        {
            callbackExecuted = true;
        }

        // Act
        UnityThreadHelper.Schedule(scheduledTime, Callback);
        
        // Process with current time - shouldn't execute yet
        _mockLoopRegistrar.InvokeForType(typeof(UnityThreadHelper));
        var executedBeforeScheduledTime = callbackExecuted;
        
        // Advance time past scheduled point
        _mockTimeProvider.SetTime(scheduledTime + 1.0);
        _mockLoopRegistrar.InvokeForType(typeof(UnityThreadHelper));
        
        // Assert
        executedBeforeScheduledTime.Should().BeFalse("Callback should not execute before scheduled time");
        callbackExecuted.Should().BeTrue("Callback should execute after scheduled time");
    }

    [Test]
    public void Schedule_ExecutesImmediatelyIfTimeHasPassed()
    {
        // Arrange
        _mockTimeProvider.SetTime(20.0);
        var callbackExecuted = false;
        var pastTime = 15.0; // Time in the past

        void Callback(Empty _)
        {
            callbackExecuted = true;
        }

        // Act
        UnityThreadHelper.Schedule(pastTime, Callback);
        _mockLoopRegistrar.InvokeForType(typeof(UnityThreadHelper));

        // Assert
        callbackExecuted.Should().BeTrue("Callback should execute immediately when scheduled for past time");
    }

    [Test]
    public void RunQueued_ProcessesDueCallbacksAtCorrectTime()
    {
        // Arrange
        _mockTimeProvider.SetTime(10.0);
        
        var callback1Executed = false;
        var callback2Executed = false;
        var callback3Executed = false;
        
        void Callback1(Empty _) => callback1Executed = true;
        void Callback2(Empty _) => callback2Executed = true;
        void Callback3(Empty _) => callback3Executed = true;
        
        // Schedule at different times
        UnityThreadHelper.Schedule(11.0, Callback1);
        UnityThreadHelper.Schedule(12.0, Callback2);
        UnityThreadHelper.Schedule(15.0, Callback3);
        
        // Act - Run at time 11.5
        _mockTimeProvider.SetTime(11.5);
        _mockLoopRegistrar.InvokeForType(typeof(UnityThreadHelper));
        
        var callback1After11_5 = callback1Executed;
        var callback2After11_5 = callback2Executed;
        var callback3After11_5 = callback3Executed;
        
        // Run at time 13.0
        _mockTimeProvider.SetTime(13.0);
        _mockLoopRegistrar.InvokeForType(typeof(UnityThreadHelper));
        
        var callback1After13 = callback1Executed;
        var callback2After13 = callback2Executed;
        var callback3After13 = callback3Executed;
        
        // Run at time 20.0
        _mockTimeProvider.SetTime(20.0);
        _mockLoopRegistrar.InvokeForType(typeof(UnityThreadHelper));
        
        // Assert
        callback1After11_5.Should().BeTrue("Callback1 should execute at time 11.5");
        callback2After11_5.Should().BeFalse("Callback2 should not execute at time 11.5");
        callback3After11_5.Should().BeFalse("Callback3 should not execute at time 11.5");
        
        callback1After13.Should().BeTrue("Callback1 should still be executed after time 13");
        callback2After13.Should().BeTrue("Callback2 should execute by time 13");
        callback3After13.Should().BeFalse("Callback3 should not execute at time 13");
        
        callback3Executed.Should().BeTrue("Callback3 should execute by time 20");
    }
}
