using System;
using Deterministic.GameFramework.Reactive;
using FluentAssertions;
using R3;
using Xunit;

namespace Deterministic.GameFramework.ECS.Tests
{
    [Collection("Non-Parallel")]
    public class ReactiveTypesTests
    {
        [Fact]
        public void EntityDefinitionAttribute_ShouldStoreTypes()
        {
            var types = new[] { typeof(int), typeof(string) };
            var attr = new EntityDefinitionAttribute(types);
            
            attr.Components.Should().BeSameAs(types);
        }

        [Fact]
        public void Model_Dispose_ShouldNotThrow()
        {
            var model = new Model();
            Action act = () => model.Dispose();
            act.Should().NotThrow();
        }

        [Fact]
        public void ViewModel_Dispose_ShouldDisposeDisposables()
        {
            var viewModel = new ViewModel();
            var disposed = false;
            viewModel.Disposables.Add(Disposable.Create(() => disposed = true));
            
            viewModel.Dispose();
            
            disposed.Should().BeTrue();
            viewModel.Disposables.IsDisposed.Should().BeTrue();
        }
        
        [Fact]
        public void ViewModel_ShouldInitialize_WithEmptyDisposables()
        {
            var viewModel = new ViewModel();
            viewModel.Disposables.Should().NotBeNull();
            viewModel.Disposables.IsDisposed.Should().BeFalse();
        }
    }
}
