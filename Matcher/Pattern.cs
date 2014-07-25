using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace PatternMatching.Matcher
{
	// TODO: add bool for lazy execution with one ctor
	// TODO: use ConditionalWeakTable for cache (use Flyweight pattern)
	public class Pattern<T>
	{
		private readonly List<MatchEntry> _entries = new List<MatchEntry>();

		public object Object { get; private set; }

		public Pattern() { }

		public Pattern(object obj)
		{
			if (obj == null)
			{
				throw new ArgumentNullException("obj");
			}

			Object = obj;
		}

		public Pattern<T> Match<TR>(Expression<Func<TR>> clause, Expression<Func<T>> body)
		{
			return MatchInternal(clause, body);
		}

		public Pattern<T> Match<T1, TR>(Expression<Func<T1, TR>> clause, Expression<Func<T1, T>> body)
		{
			return MatchInternal(clause, body);
		}

		public Pattern<T> Match<T1, T2, TR>(Expression<Func<T1, T2, TR>> clause, Expression<Func<T1, T2, T>> body)
		{
			return MatchInternal(clause, body);
		}

		public Pattern<T> Match<T1, T2, T3, TR>(Expression<Func<T1, T2, T3, TR>> clause, Expression<Func<T1, T2, T3, T>> body)
		{
			return MatchInternal(clause, body);
		}

		public ClosedPattern<T> Else(Expression<Func<T>> @else)
		{
			return new ClosedPattern<T>(_entries, @else);
		}

		private Pattern<T> MatchInternal(LambdaExpression clause, LambdaExpression body)
		{
			_entries.Add(new MatchEntry { Clause = clause, Body = body });

			return this;
		}
	}
}