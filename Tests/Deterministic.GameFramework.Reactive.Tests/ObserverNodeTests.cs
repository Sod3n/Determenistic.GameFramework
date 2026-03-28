using System;
using Deterministic.GameFramework.Reactive;
using FluentAssertions;
using Xunit;

namespace Deterministic.GameFramework.ECS.Tests
{
    [Collection("Non-Parallel")] public class ObserverNodeTests
    {
        [Fact]
        public void Dispose_WithoutOwner_ShouldNotThrow()
        {
            var observer = new TestObserver();
            Action act = () => observer.Dispose();
            act.Should().NotThrow();
        }

        [Fact]
        public void Dispose_ShouldCallOnDispose()
        {
            var observer = new TestObserver();
            observer.Dispose();
            observer.IsDisposed.Should().BeTrue();
        }

        [Fact]
        public void Reset_DefaultImplementation_ShouldCallCheckAndNotify()
        {
            var observer = new TestObserver();
            observer.Reset();
            observer.CheckAndNotifyCalled.Should().BeTrue();
        }

        [Fact]
        public void ObserverNode_BaseOnDispose_ShouldBeCallable()
        {
            var observer = new MinimalObserver();
            observer.Dispose();
        }

        private class MinimalObserver : ObserverNode
        {
            public override void CheckAndNotify() { }
        }

        private class TestObserver : ObserverNode
        {
            public bool IsDisposed { get; private set; }
            public bool CheckAndNotifyCalled { get; private set; }

            public override void CheckAndNotify()
            {
                CheckAndNotifyCalled = true;
            }

            protected override void OnDispose()
            {
                IsDisposed = true;
            }
        }
    }
}
