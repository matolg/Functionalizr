using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace PatternMatching.Matcher
{
	public class ClosedPattern<T>
	{
		private readonly List<MatchEntry> _entries;
		private readonly LambdaExpression _else;

		private Func<object, T> _pattern;

		public ClosedPattern(List<MatchEntry> entries, LambdaExpression @else)
		{
			_entries = entries;
			_else = @else;

			Compile();
		}

		private void Compile()
		{
			ParameterExpression input = Expression.Parameter(typeof(object), "o");

			Expression pattern = _else.Body;

			foreach (var entry in Enumerable.Reverse(_entries))
			{
				entry.Compile(input);

				pattern = Expression.Condition(
					entry.Match,
					entry.Map,
					pattern
					);
			}

			_pattern = Expression.Lambda<Func<object, T>>(pattern, input).Compile(); 
		}

		public T Execute(object o)
		{
			return _pattern(o);
		}
	}
}