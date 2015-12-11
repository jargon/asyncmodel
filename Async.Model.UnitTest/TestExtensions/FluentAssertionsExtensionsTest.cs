using System;
using Async.Model.TestExtensions;
using FluentAssertions;
using NUnit.Framework;

namespace Async.Model.UnitTest.TestExtensions
{
    [TestFixture]
    public class FluentAssertionsExtensionsTest
    {
        #region ItemChangeAssertions
        [Test]
        public void CanAssertAddition()
        {
            var change = new ItemChange<int>(ChangeType.Added, 4);
            change.Should().BeOfChangeType(ChangeType.Added);
            change.Should().BeAddition();
        }

        [Test]
        public void CanAssertRemoval()
        {
            var change = new ItemChange<int>(ChangeType.Removed, 2);
            change.Should().BeOfChangeType(ChangeType.Removed);
            change.Should().BeRemoval();
        }

        [Test]
        public void CanAssertUpdate()
        {
            var change = new ItemChange<int>(ChangeType.Updated, 1);
            change.Should().BeOfChangeType(ChangeType.Updated);
            change.Should().BeUpdate();
        }

        [Test]
        public void AssertOfChangeTypeHandlesNullSubject()
        {
            IItemChange<int> change = null;
            Action assert = () => change.Should().BeOfChangeType(ChangeType.Added);
            assert.ShouldThrow<Exception>().WithMessage("Expected item change to be*addition*but found <null>*");
        }

        [Test]
        public void AssertOfChangeTypeDoesNotAcceptDifferentChange()
        {
            var change = new ItemChange<int>(ChangeType.Removed, 1);

            // Expect addition, but actual is removal
            Action assert = () => change.Should().BeOfChangeType(ChangeType.Added);
            assert.ShouldThrow<Exception>().WithMessage("Expected item change to be*addition*but*was Removed*");

            // Expect update, but actual is removal
            assert = () => change.Should().BeOfChangeType(ChangeType.Updated);
            assert.ShouldThrow<Exception>().WithMessage("Expected item change to be*update*but*was Removed*");
        }

        [Test]
        public void FailedAssertOfChangeTypeAppendsReasonAsExpected()
        {
            var change = new ItemChange<int>(ChangeType.Removed, 333);

            Action assert = () => change.Should().BeOfChangeType(ChangeType.Added, "because I say so and also because {0} <> {1}", 1, 2);
            assert.ShouldThrow<Exception>().WithMessage("*to be*addition*because I say so and also because 1 <> 2, but*");
        }

        [Test]
        public void CanAssertAdditionOfItem()
        {
            var change = new ItemChange<int>(ChangeType.Added, 5);
            change.Should().BeChange(ChangeType.Added, 5);
            change.Should().BeAdditionOf(5);
        }

        [Test]
        public void CanAssertRemovalOfItem()
        {
            var change = new ItemChange<int>(ChangeType.Removed, 1);
            change.Should().BeChange(ChangeType.Removed, 1);
            change.Should().BeRemovalOf(1);
        }

        [Test]
        public void CanAssertUpdateOfItem()
        {
            var change = new ItemChange<int>(ChangeType.Updated, 100);
            change.Should().BeChange(ChangeType.Updated, 100);
            change.Should().BeUpdateOf(100);
        }

        [Test]
        public void AssertOfChangeHandlesNullSubject()
        {
            IItemChange<int> change = null;
            Action assert = () => change.Should().BeChange(ChangeType.Added, 1);
            assert.ShouldThrow<Exception>().WithMessage("Expected item change to be*addition*of 1*but found <null>*");
        }

        [Test]
        public void AssertOfChangeDoesNotAcceptDifferentType()
        {
            var change = new ItemChange<int>(ChangeType.Unchanged, 10);
            Action assert = () => change.Should().BeChange(ChangeType.Added, 10);
            assert.ShouldThrow<Exception>().WithMessage("Expected item change to be*addition*of 10*but found *unchanging*of*10*");
        }

        [Test]
        public void AssertOfChangeDoesNotAcceptDifferentItem()
        {
            var change = new ItemChange<int>(ChangeType.Added, 1);
            Action assert = () => change.Should().BeChange(ChangeType.Added, 2);
            assert.ShouldThrow<Exception>().WithMessage("Expected item change to be*addition*of 2*but found *addition*of*1*");
        }

        [Test]
        public void FailedAssertOfChangeAppendsReasonAsExpected()
        {
            var change = new ItemChange<int>(ChangeType.Updated, 2);
            Action assert = () => change.Should().BeChange(ChangeType.Removed, 1, "because {0} should be removed", 1);
            assert.ShouldThrow<Exception>().WithMessage("*removal*of 1 because 1 should be removed*");
        }
        #endregion ItemChangeAssertions
    }
}
