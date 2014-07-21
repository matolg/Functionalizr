using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using PatternMatching.Matcher;

namespace PatternMatching
{
	class Program
	{
		public static void Bench()
		{
			// Prepare benchmark
			var rand = new Random();

			var persons = new List<Person>();
			for (int i = 0; i < 100000; i++)
			{
				persons.Add(new Person("Bart", rand.Next(1, 10)));
			}

			// Classic approach
			var baseline = persons.Select(p => (p != null && p.Age == 5)).ToList();

			var watch = new Stopwatch();

			watch.Start();

			foreach (var p in baseline) { }

			watch.Stop();

			long baseMs = watch.ElapsedMilliseconds;
			Console.WriteLine(watch.Elapsed);

			// Matcher approach
			var matcher = new Pattern<bool>()
				.Match((string name) => new Person(name, 5), name => true)
				.Else(() => false);

			matcher.Compile();

			var res3 = persons.Select(matcher.Execute).ToList();

			watch.Reset();
			watch.Start();

			foreach (var p in res3) { }

			watch.Stop();
			Console.WriteLine(watch.Elapsed + " " + watch.ElapsedMilliseconds / baseMs);

			Debug.Assert(baseline.SequenceEqual(res3));
		}

		public static void MatchTest()
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
		}

		public static uint Fib(uint n)
		{
			return new Pattern<uint>(n)
				.Match(() => 0, () => 1)
				.Match(() => 1, () => 1)
				.Else(() => Fib(n - 1) + Fib(n - 2)).Result;
		}

		public static void FibTest()
		{
			var result = Fib(4);

			Console.WriteLine(result);
		}

		public static void BisectTest()
		{
			var bisect = new Pattern<bool>()
				.Match((int x) => new Point(x, x), x => true)
				.Else(() => false);

			bisect.Compile();

			var rand = new Random();
			for (int i = 0; i < 100; i++)
			{
				var p = new Point(rand.Next(1, 5), rand.Next(1, 5));
				if (bisect.Execute(p))
					Console.WriteLine(p);
			}
		}

		public static void BisectWithObjectInitTest()
		{
			var bisect = new Pattern<bool>()
				.Match((int x) => new Point { X = x, Y = x }, x => true)
				.Else(() => false);

			bisect.Compile();

			var rand = new Random();
			for (int i = 0; i < 100; i++)
			{
				var p = new Point(rand.Next(1, 5), rand.Next(1, 5));
				if (bisect.Execute(p))
					Console.WriteLine(p);
			}
		}

		public static void BisectWithArrayInitTest()
		{
			var bisect = new Pattern<bool>()
				.Match((int x) => new[] { x, x, 0 }, x => true)
				.Else(() => false);

			bisect.Compile();

			var rand = new Random();

			for (int i = 0; i < 100; i++)
			{
				var p = new[] { rand.Next(0, 5), rand.Next(0, 5), rand.Next(0, 5) };
				if (bisect.Execute(p))
					Console.WriteLine(p[0] + "," + p[1] + "," + p[2]);
			}
		}

		public static void BisectWithListInitTest()
		{
			var bisect = new Pattern<bool>()
				.Match((int x) => new List<int> { x, x, 0 }, x => true)
				.Else(() => false);

			bisect.Compile();

			var rand = new Random();

			for (int i = 0; i < 100; i++)
			{
				var p = new[] { rand.Next(0, 5), rand.Next(0, 5), rand.Next(0, 5) };
				if (bisect.Execute(p))
					Console.WriteLine(p[0] + "," + p[1] + "," + p[2]);
			}
		}

		public static void BisectWithDictionaryInitTest()
		{
			var bisect = new Pattern<bool>()
				.Match((int x) => new Dictionary<string, int> { { "x", x }, { "y", x } }, x => true)
				.Else(() => false);

			bisect.Compile();

			var rand = new Random();
			for (int i = 0; i < 100; i++)
			{
				var p = new Dictionary<string, int> { { "x", rand.Next(0, 5) }, { "y", rand.Next(0, 5) } };
				if (bisect.Execute(p))
					Console.WriteLine(p["x"] + "," + p["y"]);
			}
		}

		static void Main(string[] args)
		{
			/* MatchTest();
			Bench();
			FibTest();*/
			BisectTest();
			BisectWithObjectInitTest();
			BisectWithListInitTest();
			BisectWithDictionaryInitTest();

			Console.ReadKey();
		}
	}
}
