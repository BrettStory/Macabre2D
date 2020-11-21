﻿namespace Macabresoft.Macabre2D.Tests.Editor.Framework.Services {
    using System;
    using System.Collections.Generic;
    using FluentAssertions;
    using Macabresoft.Macabre2D.Editor.Library.Services;
    using Macabresoft.Macabre2D.Framework;
    using NUnit.Framework;

    [TestFixture]
    public sealed class UndoServiceTests {
        private const int MultiGenerationCount = 100;
        
        [Test]
        [Category("Unit Tests")]
        public void Do_Should_PerformChanges() {
            var undoService = new UndoService();
            var testObject = new TestObject();

            undoService.Do(
                () => testObject.TestProperty = TestObject.NewValue,
                () => testObject.TestProperty = TestObject.OldValue);

            testObject.TestProperty.Should().Be(TestObject.NewValue);
        }

        [Test]
        [Category("Unit Tests")]
        public void Undo_Should_UndoChanges() {
            var undoService = new UndoService();
            var testObject = new TestObject();

            undoService.Do(
                () => testObject.TestProperty = TestObject.NewValue,
                () => testObject.TestProperty = TestObject.OldValue);

            undoService.Undo();

            testObject.TestProperty.Should().Be(TestObject.OldValue);
        }

        [Test]
        [Category("Unit Tests")]
        public void Redo_Should_RedoChanges() {
            var undoService = new UndoService();
            var testObject = new TestObject();

            undoService.Do(
                () => testObject.TestProperty = TestObject.NewValue,
                () => testObject.TestProperty = TestObject.OldValue);

            undoService.Undo();
            undoService.Redo();

            testObject.TestProperty.Should().Be(TestObject.NewValue);
        }

        [Test]
        [Category("Unit Tests")]
        public void Do_Should_SetCanUndoToTrue() {
            var undoService = new UndoService();
            var testObject = new TestObject();

            undoService.Do(
                () => testObject.TestProperty = TestObject.NewValue,
                () => testObject.TestProperty = TestObject.OldValue);

            undoService.CanUndo.Should().BeTrue();
        }
        
        [Test]
        [Category("Unit Tests")]
        public void Undo_Should_SetCanUndoToTrue() {
            var undoService = new UndoService();
            var testObject = new TestObject();

            undoService.Do(
                () => testObject.TestProperty = TestObject.NewValue,
                () => testObject.TestProperty = TestObject.OldValue);

            undoService.Undo();
            undoService.CanUndo.Should().BeFalse();
        }

        [Test]
        [Category("Unit Tests")]
        public void MultiGenerationTest() {
            var undoService = new UndoService();

            var testObjects = new List<TestObject>();
            
            for (var i = 0; i < MultiGenerationCount; i++)
            {
                var testObject = new TestObject();
                undoService.Do(
                    () => testObject.TestProperty = TestObject.NewValue,
                    () => testObject.TestProperty = TestObject.OldValue);
                
                testObjects.Add(testObject);
            }
            
            testObjects.Reverse();

            testObjects.Count.Should().Be(MultiGenerationCount);
            foreach (var testObject in testObjects) {
                testObject.TestProperty.Should().Be(TestObject.NewValue);
                undoService.Undo();
                testObject.TestProperty.Should().Be(TestObject.OldValue);
            }
            
            testObjects.Reverse();

            testObjects.Count.Should().Be(MultiGenerationCount);
            foreach (var testObject in testObjects) {
                testObject.TestProperty.Should().Be(TestObject.OldValue);
                undoService.Redo();
                testObject.TestProperty.Should().Be(TestObject.NewValue);
            }
        }
        
        private sealed class TestObject : NotifyPropertyChanged {
            public TestObject() {
                this.TestProperty = OldValue;
            }

            internal static string NewValue { get; } = Guid.NewGuid().ToString();

            internal static string OldValue { get; } = Guid.NewGuid().ToString();

            internal string TestProperty { get; set; }
        }
    }
}