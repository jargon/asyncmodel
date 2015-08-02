using System.Collections.Generic;
using FluentAssertions;
using FluentAssertions.Common;
using FluentAssertions.Execution;

namespace Async.Model.TestExtensions
{
    public static class FluentAssertionsExtensions
    {
        public class ItemChangeAssertions<T>
        {
            private static readonly Dictionary<ChangeType, string> changeDescriptions = new Dictionary<ChangeType, string>()
            {
                { ChangeType.Added, "addition" },
                { ChangeType.Removed, "removal" },
                { ChangeType.Unchanged, "unchanging" },  // TODO: Can we figure out a better description for a "no-op change"?
                { ChangeType.Updated, "update" }
            };

            public IItemChange<T> Subject { get; private set; }

            internal ItemChangeAssertions(IItemChange<T> actualItemChange)
            {
                this.Subject = actualItemChange;
            }

            public AndConstraint<ItemChangeAssertions<T>> BeOfChangeType(ChangeType expectedType, string because = "", params object[] reasonArgs)
            {
                Execute.Assertion
                    .BecauseOf(because, reasonArgs)
                    .WithExpectation("Expected {context:item change} to be a(n) {0}{reason}, ", changeDescriptions[expectedType])
                    .ForCondition(!ReferenceEquals(Subject, null))
                    .FailWith("but found <null>.")
                    .Then
                    .Given(() => Subject.Type)
                    .ForCondition(actualType => actualType == expectedType)
                    .FailWith("but its actual type was {0:G}.", actualType => actualType);

                return new AndConstraint<ItemChangeAssertions<T>>(this);
            }

            public AndConstraint<ItemChangeAssertions<T>> BeChange(ChangeType expectedType, T expectedItem, string because = "", params object[] reasonArgs)
            {
                var expectedChange = changeDescriptions[expectedType];

                Execute.Assertion
                    .BecauseOf(because, reasonArgs)
                    .WithExpectation("Expected {context:item change} to be {0} of {1}{reason}, ", expectedChange, expectedItem)
                    .ForCondition(!ReferenceEquals(Subject, null))
                    .FailWith("but found <null>.")
                    .Then
                    .Given(() => Subject)
                    .ForCondition(change => change.Type == expectedType)
                    .FailWith("but found {0} of item {1}.", change => changeDescriptions[change.Type], change => change.Item)
                    .Then
                    .Given(change => change.Item)
                    .ForCondition(item => item.IsSameOrEqualTo(expectedItem))
                    .FailWith("but found {0} of item {1}.", item => expectedChange, item => item);

                return new AndConstraint<ItemChangeAssertions<T>>(this);
            }

            public AndConstraint<ItemChangeAssertions<T>> BeAddition(string because = "", params object[] reasonArgs)
            {
                return BeOfChangeType(ChangeType.Added, because, reasonArgs);
            }

            public AndConstraint<ItemChangeAssertions<T>> BeAdditionOf(T item, string because = "", params object[] reasonArgs)
            {
                return BeChange(ChangeType.Added, item, because, reasonArgs);
            }

            public AndConstraint<ItemChangeAssertions<T>> BeRemoval(string because = "", params object[] reasonArgs)
            {
                return BeOfChangeType(ChangeType.Removed, because, reasonArgs);
            }

            public AndConstraint<ItemChangeAssertions<T>> BeRemovalOf(T item, string because = "", params object[] reasonArgs)
            {
                return BeChange(ChangeType.Removed, item, because, reasonArgs);
            }

            public AndConstraint<ItemChangeAssertions<T>> BeUpdate(string because = "", params object[] reasonArgs)
            {
                return BeOfChangeType(ChangeType.Updated, because, reasonArgs);
            }

            public AndConstraint<ItemChangeAssertions<T>> BeUpdateOf(T item, string because = "", params object[] reasonArgs)
            {
                return BeChange(ChangeType.Updated, item, because, reasonArgs);
            }
        }

        public class SequenceOfItemChangeAssertions<T>
        {
            public IEnumerable<IItemChange<T>> Subject { get; private set; }

            internal SequenceOfItemChangeAssertions(IEnumerable<IItemChange<T>> actualChanges)
            {
                this.Subject = actualChanges;
            }

            public AndConstraint<SequenceOfItemChangeAssertions<T>> Be()
            {
                return null;
            }
        }

        public static ItemChangeAssertions<T> Should<T>(this IItemChange<T> actualValue)
        {
            return new ItemChangeAssertions<T>(actualValue);
        }
    }
}
