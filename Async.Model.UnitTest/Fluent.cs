using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions.Common;
using FluentAssertions.Execution;
using NSubstitute.Core;
using NSubstitute.Core.Arguments;

namespace Async.Model.UnitTest
{
    /// <summary>
    /// Bridges the gap between NSubstitute and Fluent Assertions. Inspired by a blog post by Rory Primrose, but this
    /// version circumvents the NSubstitute exception catching machinery to directly throw exceptions from FA, so you
    /// don't have to check trace output to see the error messages.
    /// </summary>
    /// <see cref="http://www.neovolve.com/2014/10/07/bridging-between-nsubstitute-and-fluentassertions/"/>
    public static class Fluent
    {
        private class AssertionMatcher<T> : IArgumentMatcher
        {
            private readonly Action<T> assertion;

            public AssertionMatcher(Action<T> assertion)
            {
                this.assertion = assertion;
            }

            public bool IsSatisfiedBy(object argument)
            {
                assertion((T)argument);
                return true;
            }
        }

        private class FluentArgumentSpecification : IArgumentSpecification
        {
            private readonly IArgumentSpecification innerSpecification;
            private readonly IArgumentMatcher matcher;

            public FluentArgumentSpecification(IArgumentSpecification innerSpecification, IArgumentMatcher matcher)
            {
                this.innerSpecification = innerSpecification;
                this.matcher = matcher;
            }


            public IArgumentSpecification CreateCopyMatchingAnyArgOfType(Type requiredType)
            {
                return new FluentArgumentSpecification(innerSpecification.CreateCopyMatchingAnyArgOfType(requiredType),
                    new AnyArgumentMatcher(requiredType));
            }

            public string DescribeNonMatch(object argument)
            {
                return innerSpecification.DescribeNonMatch(argument);
            }

            public Type ForType
            {
                get { return innerSpecification.ForType; }
            }

            public string FormatArgument(object argument)
            {
                return innerSpecification.FormatArgument(argument);
            }

            public bool IsSatisfiedBy(object argument)
            {
                if (!argument.IsCompatibleWith(ForType))
                    return false;

                try
                {
                    return matcher.IsSatisfiedBy(argument);
                }
                catch (Exception e)
                {
                    var fluentExceptionType = GetTypeOfExceptionsThrownByFluentAssertionsOnThisPlatform();
                    if (fluentExceptionType.IsInstanceOfType(e))
                    {
                        // We explicitly let exceptions from Fluent Assertions escape
                        throw e;
                    }
                    else
                    {
                        // Follow normal NSubstitute behaviour where exception from matcher => non-match
                        return false;
                    }
                }
            }

            public void RunAction(object argument)
            {
                innerSpecification.RunAction(argument);
            }

            private Type GetTypeOfExceptionsThrownByFluentAssertionsOnThisPlatform()
            {
                try
                {
                    Services.ThrowException("Ignore this exception");
                }
                catch (Exception e)
                {
                    return e.GetType();
                }

                // Surely we cannot get here? ;-)
                throw new Exception("Services.ThrowException did not throw an exception!");
            }
        }

        // NOTE: This is only to emulate behaviour of ArgumentSpecificationQueue, may not be needed
        private static readonly ISubstitutionContext substitutionContextAtStart = SubstitutionContext.Current;

        public static T Match<T>(Action<T> action)
        {
            var matcher = new AssertionMatcher<T>(action);
            var innerArgumentSpecification = new ArgumentSpecification(typeof(T), matcher);
            var fluentArgumentSpecification = new FluentArgumentSpecification(innerArgumentSpecification, matcher);

            substitutionContextAtStart.EnqueueArgumentSpecification(fluentArgumentSpecification);

            return default(T);
        }
    }
}
