using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Async.Model.UnitTest
{
    [TestFixture]
    public class GenericContravarianceTest
    {
        delegate void ContravariantHandler<in T>(T arg);

        class EventingClass
        {
            public event ContravariantHandler<string> MyEvent;
            public void FireTheEvent()
            {
                var handler = MyEvent;
                if (handler != null)
                    handler("hej");
            }
        }

        /// <summary>
        /// This test shows that using contravariant delegates as event handlers can be dangerous: if you try to add
        /// handlers of different generic type, the code will compile but fail at runtime! This is the reason why
        /// EventHandler&lt;TEventArgs&gt; is NOT declared as contravariant in its type argument. The underlying
        /// problem is that Delegate.Combine, which is called under the covers, does not support combining delegates of
        /// different types.
        /// </summary>
        /// <see cref="http://stackoverflow.com/questions/1120688/event-and-delegate-contravariance-in-net-4-0-and-c-sharp-4-0"/>
        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void DangerousToUseContravariantDelegateAsEventHandler()
        {
            var eventingClass = new EventingClass();
            var handleString = new ContravariantHandler<string>(HandleString);
            var handleObject = new ContravariantHandler<object>(HandleObject);

            eventingClass.MyEvent += handleString;
            eventingClass.MyEvent += handleObject;

            eventingClass.FireTheEvent();
        }

        private static void HandleString(string s)
        {
            Console.WriteLine("Argument as string: {0}", s);
        }

        private static void HandleObject(object obj)
        {
            Console.WriteLine("Argument as object: {0}", obj);
        }
    }
}
