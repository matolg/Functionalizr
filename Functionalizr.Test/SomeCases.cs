using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Functionalizr.Core.Matcher;
using Functionalizr.Test.Model;

namespace Functionalizr.Test
{
	class SomeCases
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

			var res3 = persons.Select(matcher.Execute).ToList();

			watch.Reset();
			watch.Start();

			foreach (var p in res3) { }

			watch.Stop();
			Console.WriteLine(watch.Elapsed + " " + watch.ElapsedMilliseconds / baseMs);

			Debug.Assert(baseline.SequenceEqual(res3));
		}

		public static uint Fib(uint n)
		{
			return new Pattern<uint>(n)
				.Match(() => 0, () => 1)
				.Match(() => 1, () => 1)
				.Else(() => Fib(n - 1) + Fib(n - 2)).Execute(n);
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

			var rand = new Random();
			for (int i = 0; i < 100; i++)
			{
				var p = new Dictionary<string, int> { { "x", rand.Next(0, 5) }, { "y", rand.Next(0, 5) } };
				if (bisect.Execute(p))
					Console.WriteLine(p["x"] + "," + p["y"]);
			}
		}
	}
}
