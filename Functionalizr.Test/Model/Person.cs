using Functionalizr.Core.Matcher;

namespace Functionalizr.Test.Model
{
	public class Person
	{
		public Person([MatchProperty(Name = "Name")] string name, [MatchProperty(Name = "Age")] int age)
		{
			Name = name;
			Age = age;
		}

		public string Name { get; private set; }

		public int Age { get; private set; }
	}

	public class WrongMatchPerson
	{
		public WrongMatchPerson(string name, int age)
		{
			Name = name;
			Age = age;
		}

		public string Name { get; private set; }

		public int Age { get; private set; }
	}
}