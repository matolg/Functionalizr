using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using PatternMatching.Matcher;

namespace PatternMatching
{
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

            matcher.Compile();

            var res3 = persons.Select(matcher.Execute);

            watch.Reset();
            watch.Start();

            foreach (var p in res3);

            watch.Stop();
            Console.WriteLine(watch.Elapsed + " " + watch.ElapsedMilliseconds / baseMs);

            Debug.Assert(baseline.SequenceEqual(res3));
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
