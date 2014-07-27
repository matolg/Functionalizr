using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

using Functionalizr.Core.Matcher;
using Functionalizr.Test.Model;

namespace Functionalizr.Test
{
	[TestFixture]
	public class PatternMatchTest
	{
		public List<Person> Persons { get; set; }

		[SetUp]
		public void PrepareTest()
		{
			Persons = new List<Person>
				{
					new Person("Bart", 25),
					new Person("John", 52),
					new Person("Lisa", 25),
					new Person("Rosa", 63)
				};
		}

		[Test]
		public void Should_throw_exception_if_no_Match_Property_Attribute_is_used_for()
		{
			var testLambda = 
				new Action(() => 
					new Pattern<string>()
						.Match((string name, int age) => new WrongMatchPerson(name, age), (name, age) => "Ops")
						.Else(() => "Fin"));

			Assert.That(new TestDelegate(testLambda), Throws.InvalidOperationException);
		}

		[Test]
		public void Should_execute_match_if_no_matching_condition()
		{
			var pattern = new Pattern<bool>()
				.Match((string name, int age) => new Person(name, age), (name, age) => true)
				.Else(() => false);

			pattern.Execute(Persons);

			var res = Persons.Select(pattern.Execute);

			Assert.That(res, Is.Not.Empty);
			Assert.That(res.All(x => x.Equals(true)), Is.True);
		}

		[Test]
		public void Should_execute_match_action_if_matching_successful()
		{
			var person = new Person("John", 25);

			var pattern = new Pattern<bool>()
				.Match((int age) => new Person("John", age), age => true)
				.Else(() => false);

			var res = pattern.Execute(person);

			Assert.That(res, Is.True);
		}

		[Test]
		public void Should_execute_else_action_if_matching_not_found()
		{
			var person = new Person("Bart", 25);

			var pattern = new Pattern<bool>()
				.Match((string name) => new Person(name, 30), name => true)
				.Else(() => false);

			var res = pattern.Execute(person);

			Assert.That(res, Is.False);
		}
	}
}
