using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace PatternMatching
{
    // TODO: add bool for lazy execution with one ctor
    public class Pattern<T>
    {
        private bool _hasValue;

        private Func<T> _else;

        private readonly List<MatchEntry> _matches = new List<MatchEntry>();

        public object Object { get; private set; }

        public T Result { get; private set; }

        public Pattern() { }

        public Pattern(object obj)
        {
            if (obj == null)
            {
                throw new ArgumentNullException("obj");
            }

            Object = obj;
        }

        public Pattern<T> Match<TR>(Expression<Func<TR>> expr, Func<T> foo)
        {
            return MatchInternal(expr, foo);
        }

        public Pattern<T> Match<T1, TR>(Expression<Func<T1, TR>> expr, Func<T1, T> foo)
        {
            return MatchInternal(expr, foo);
        }

        public Pattern<T> Match<T1, T2, TR>(Expression<Func<T1, T2, TR>> expr, Func<T1, T2, T> foo)
        {
            return MatchInternal(expr, foo);
        }

        public Pattern<T> Match<T1, T2, T3, TR>(Expression<Func<T1, T2, T3, TR>> expr, Func<T1, T2, T3, T> foo)
        {
            return MatchInternal(expr, foo);
        }

        public Pattern<T> Else(Func<T> foo)
        {
            if (Object == null)
            {
                if (_else != null)
                {
                    throw new Exception("Else clause has been set already");
                }

                _else = foo;
            }
            if (!_hasValue)
            {
                _hasValue = true;
                Result = foo();
            }

            return this;
        }

        private Pattern<T> MatchInternal(LambdaExpression e, Delegate f)
        {
            if (Object == null) // Lazy
            {
                if (_else != null)
                {
                    throw new Exception("Else clause has been set already");
                }

                _matches.Add(new MatchEntry { Match = e, Action = f });

                return this;
            }

            if (_hasValue)
                return this;

            Dictionary<ParameterExpression, PropertyInfo> bindings;

            if (!TryMatch(Object, e, out bindings))
                return this;

            Result = Evaluate(Object, e, f, bindings);
            _hasValue = true;

            return this;
        }

        private static bool TryMatch(object o, LambdaExpression e, out Dictionary<ParameterExpression, PropertyInfo> bindings)
        {
            bindings = new Dictionary<ParameterExpression, PropertyInfo>();

            var ne = e.Body as NewExpression;
            if (ne == null)
                throw new NotSupportedException("Match clause can only contain NewExpression.");

            Type target = ne.Constructor.DeclaringType;

            if (target == null)
                return false;

            if (!target.IsInstanceOfType(o))
                return false;

            int i = 0;
            foreach (ParameterInfo param in ne.Constructor.GetParameters())
            {
                var pa = param.GetCustomAttributes(typeof(PropertyAttribute), false).Cast<PropertyAttribute>().SingleOrDefault();
                if (pa == null)
                    throw new InvalidOperationException("Input object doesn't have required mapping information.");

                PropertyInfo property = target.GetProperty(pa.Name);
                if (property == null)
                    throw new InvalidOperationException(String.Format("Property {0} on type {1} not found.", pa.Name, target.Name));

                ConstantExpression ce;
                ParameterExpression pe;

                Expression arg = ne.Arguments[i++];
                if ((ce = arg as ConstantExpression) != null)
                {
                    object value = property.GetValue(o, null);

                    if (!Equals(value, ce.Value))
                        return false;
                }
                else if ((pe = arg as ParameterExpression) != null)
                {
                    bindings[pe] = property;
                }
                else
                    throw new NotSupportedException("Can only match constants.");
            }

            return true;
        }

        private static T Evaluate(object o, LambdaExpression e, Delegate f, Dictionary<ParameterExpression, PropertyInfo> bindings)
        {
            var args = new object[e.Parameters.Count];
            int j = 0;

            foreach (ParameterExpression param in e.Parameters)
            {
                PropertyInfo property;
                if (!bindings.TryGetValue(param, out property))
                    throw new InvalidOperationException("Parameter " + param.Name + " was not bound in the pattern match.");

                object value = property.GetValue(o, null);
                if (!value.GetType().IsAssignableFrom(param.Type))
                    throw new InvalidOperationException(
                        String.Format("Property {0} on type {1} cannot be bound to parameter {2}.",
                                        property.Name, property.DeclaringType != null ? property.DeclaringType.Name : string.Empty, param.Name));

                args[j++] = value;
            }

            var result = (T)f.DynamicInvoke(args);
            return result;
        }

        public T Execute(object o)
        {
            if (Object != null)
            {
                throw new Exception("Could not execute eager style");
            }

            if (_else == null)
            {
                throw new Exception("Execution chain is not completed. Do you missing Else clause?");
            }

            foreach (var entry in _matches)
            {
                Dictionary<ParameterExpression, PropertyInfo> bindings;

                if (TryMatch(o, entry.Match, out bindings))
                {
                    return Evaluate(o, entry.Match, entry.Action, bindings);
                }
            }

            return _else();
        }
    }

    internal class MatchEntry
    {
        public LambdaExpression Match { get; set; }

        public Delegate Action { get; set; }
    }

    [AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = true)]
    internal sealed class PropertyAttribute : Attribute
    {
        public string Name { get; set; }
    }

    public class Person
    {
        public Person([Property(Name = "Name")] string name, [Property(Name = "Age")] int age)
        {
            Name = name;
            Age = age;
        }

        public string Name { get; private set; }

        public int Age { get; private set; }
    }

    class Program
    {
        static void Bench()
        {
            var rand = new Random();

            var persons = new List<Person>();
            for (int i = 0; i < 100000; i++)
                persons.Add(new Person("Bart", rand.Next(1, 10)));

            var watch = new Stopwatch();

            var baseline = persons.Select(p =>
            {
                var person = p as Person;
                if (person != null && person.Age == 5)
                    return true;
                else
                    return false;
            });

            watch.Start();

            foreach (var p in baseline)
                ;

            watch.Stop();

            long baseMs = watch.ElapsedMilliseconds;
            Console.WriteLine(watch.Elapsed);

            var matcher = new Pattern<bool>()
                .Match((string name) => new Person(name, 5), name => true)
                .Else(() => false);

            watch.Reset();
            watch.Start();

            var res = persons.Select(matcher.Execute);

            foreach (var p in res)
                ;

            watch.Stop();

            Console.WriteLine(watch.Elapsed + " " + watch.ElapsedMilliseconds / baseMs);

            Debug.Assert(baseline.SequenceEqual(res));
            Console.ReadKey();
        }

        static void Test()
        {
            var people = new List<object>
                {
               new Person("Bart", 25),
               new Person("John", 52),
               new Person("Lisa", 25),
               new Person("Rosa", 63),
               "Hello"
            };

            var res = people.Select(p => new Pattern<string>(p)
                .Match((string name) => new Person(name, 25), name => name + " is 25.")
                .Match((int age) => new Person("John", age), age => "John is " + age + ".")
                .Match((string name, int age) => new Person(name, age), (name, age) => "I'm matching them all!")
                .Else(p.ToString)
            );

            foreach (var s in res)
                Console.WriteLine(s);

            Console.ReadKey();
        }

        static void Main(string[] args)
        {
            Bench();
        }
    }
}
