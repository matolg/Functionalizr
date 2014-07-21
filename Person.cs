using PatternMatching.Matcher;

namespace PatternMatching
{
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
}