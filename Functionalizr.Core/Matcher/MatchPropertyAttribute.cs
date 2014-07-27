using System;

namespace Functionalizr.Core.Matcher
{
	[AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = true)]
	public sealed class MatchPropertyAttribute : Attribute
	{
		public string Name { get; set; }
	}
}