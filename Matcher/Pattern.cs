using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace PatternMatching.Matcher
{
	// TODO: add bool for lazy execution with one ctor
	// TODO: use ConditionalWeakTable for cache (use Flyweight pattern)
	public class Pattern<T>
	{
		private bool _hasValue;

		private Func<T> _else;

		private readonly List<MatchEntry> _matches = new List<MatchEntry>();

		private readonly Dictionary<object, T> _cache = new Dictionary<object, T>();

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

			Dictionary<ParameterExpression, Expression> bindings;

			if (!TryMatch(Object, e, out bindings))
				return this;

			Result = Evaluate(Object, e, f, bindings);
			_hasValue = true;

			return this;
		}

		private static bool TryMatch(object o, LambdaExpression e, out Dictionary<ParameterExpression, Expression> bindings)
		{
			bindings = new Dictionary<ParameterExpression, Expression>();

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
					bindings[pe] = Expression.PropertyOrField(Expression.Convert(e, target), property.Name);
				}
				else
					throw new NotSupportedException("Can only match constants.");
			}

			return true;
		}

		private static T Evaluate(object o, LambdaExpression e, Delegate f, Dictionary<ParameterExpression, Expression> bindings)
		{
			var args = new object[e.Parameters.Count];
			int j = 0;

			foreach (ParameterExpression param in e.Parameters)
			{
				Expression member;
				if (!bindings.TryGetValue(param, out member))
					throw new InvalidOperationException("Parameter " + param.Name + " was not bound in the pattern match.");

				object value = member ?? o;

				if (!value.GetType().IsAssignableFrom(param.Type))
					throw new InvalidOperationException(
						String.Format("Property {0} on type {1} cannot be bound to parameter {2}.",
										member.Type.Name, member.Type.DeclaringType != null ? member.Type.DeclaringType.Name : string.Empty, param.Name));

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

			T res;
			if (_cache.TryGetValue(o, out res))
				return res; 

			foreach (var entry in _matches)
			{
				Dictionary<ParameterExpression, Expression> bindings;

				if (entry.CompiledMatch != null)
				{
					if (entry.CompiledMatch(o))
					{
						return entry.CompiledInvoker(o); 
					}
				}
				if (TryMatch(o, entry.Match, out bindings))
				{
					return Evaluate(o, entry.Match, entry.Action, bindings);
				}
			}

			return _else();
		}

		public void Compile()
		{
			if (Object != null)
				throw new InvalidOperationException("Compilation is only valid on unbound pattern match objects.");

			if (_else == null)
				throw new InvalidOperationException("Can't compile the match. Incomplete match object. Are you missing an Else clause?");

			foreach (MatchEntry entry in _matches)
			{
				if (entry.CompiledMatch == null)
					entry.Compile();
			}
		}

		private class MatchEntry
		{
			public LambdaExpression Match { get; set; }

			public Delegate Action { get; set; }

			public Func<object, bool> CompiledMatch { get; private set; }

			public Func<object, T> CompiledInvoker { get; private set; }

			internal void Compile()
			{
				ParameterExpression o = Expression.Parameter(typeof(object), "o");
				var bindings = new Dictionary<ParameterExpression, Expression>();
				Type target = null;

				var matchExpression = CompileMatchExpression(o, target, Match.Body, bindings);

				CompiledMatch = Expression.Lambda<Func<object, bool>>(matchExpression, o).Compile();

				var args = new Expression[Match.Parameters.Count];
				int j = 0;
				foreach (ParameterExpression param in Match.Parameters)
				{
					Expression member;
					if (!bindings.TryGetValue(param, out member))
						throw new InvalidOperationException("Parameter " + param.Name + " was not bound in the pattern match.");

					// Expression to grab value from property.
					Expression me = Expression.Convert(o, target);
					args[j++] = member ?? me;
				}

				var invoker = Expression.Invoke(Expression.Constant(Action), args);
				CompiledInvoker = Expression.Lambda<Func<object, T>>(invoker, o).Compile();
			}

			private Expression CompileMatchExpression(ParameterExpression o, Type target,
						Expression expression, Dictionary<ParameterExpression, Expression> bindings)
			{
				Expression match;

				// TODO: here it is a point for extensibility
				if (Match.Body is NewExpression)
				{
					match = CompileNewExpression(o, target, expression, bindings); 
				}
				else if (Match.Body is  ConstantExpression)
				{
					match = CompileConstantExpression(o, target, expression, bindings); 
				}
				else if (Match.Body is ParameterExpression)
				{
					match = CompileParameterExpression(o, target, expression, bindings); 
				}
				else if (Match.Body is MemberInitExpression)
				{
					match = CompileMemberInitExpression(o, target, expression, bindings); 
				}
				else if (Match.Body is NewArrayExpression)
				{
					match = CompileNewArrayExpression(o, target, expression, bindings);
				}
				else if (Match.Body is ListInitExpression)
				{
					match = CompileListInitExpression(o, target, expression, bindings); 
				}
				else
				{
					throw new NotSupportedException("Unsupported expression detected.");
				}

				return match;
			}

			private MethodCallExpression GetEqualityCheck(ParameterExpression parameterExpression, Type target,
				Expression left, MemberInfo right)
			{
				var leftPart = Expression.Convert(left, typeof(object));

				var rightPart = 
					Expression.Convert(
						Expression.PropertyOrField(
							Expression.Convert(parameterExpression, target), right.Name), typeof(object));

				return Expression.Call(typeof(object), "Equals", new Type[0], leftPart, rightPart);
			}

			private MethodCallExpression GetEqualityCheck(ParameterExpression parameterExpression, Type target, MemberInfo left, ConstantExpression right)
			{
				var leftPart =
					Expression.Convert(
						Expression.PropertyOrField(
							Expression.Convert(parameterExpression, target), left.Name), typeof(object));

				var rightPart = Expression.Convert(right, typeof(object));

				return Expression.Call(typeof(object), "Equals", new Type[0], leftPart, rightPart);
			}

			private Expression GetEqualityCheck(ParameterExpression parameterExpression, Type target, int index, Expression right)
			{
				var leftPart =
					Expression.Convert(
						Expression.ArrayIndex(
							Expression.Convert(parameterExpression, target), Expression.Constant(index)), typeof(object));

				var rightPart = Expression.Convert(right, typeof(object));

				// TODO: what's wrong with the Object.Equals method?
				return Expression.Call(typeof(object), "Equals", new Type[0], leftPart, rightPart);
			}

			private Expression GetEqualityCheckForListOfT(ParameterExpression o, Type target, int index, Expression right)
			{
				var leftPart = 
					Expression.Convert(
						Expression.Call(Expression.Convert(o, target), target.GetMethod("get_Item"), Expression.Constant(index)), 
						typeof (object));

				var rightPart = Expression.Convert(right, typeof(object));

				// TODO: what's wrong with the Object.Equals method?
				return Expression.Call(typeof(object), "Equals", new Type[0], leftPart, rightPart);
			}

			private Expression GetEqualityCheckForDictionaryEntry(ParameterExpression o, Type target,
				ConstantExpression key, Expression right)
			{
				var leftPart =
					Expression.Convert(
						Expression.Call(Expression.Convert(o, target), target.GetMethod("get_Item"), key),
						typeof(object));

				var rightPart = Expression.Convert(right, typeof(object));

				// TODO: what's wrong with the Object.Equals method?
				return Expression.Call(typeof(object), "Equals", new Type[0], leftPart, rightPart);
			}

			private Expression GetTypeCheck(ParameterExpression parameterExpression, Type target)
			{
				return Expression.TypeIs(parameterExpression, target);
			}

			private static bool TryMatchParameterExpression(object o, ParameterExpression pe,
				Dictionary<ParameterExpression, Expression> bindings)
			{
				bindings[pe] = null;
				return pe.Type.IsInstanceOfType(o);
			}

			private Expression CompileNewExpression(ParameterExpression o, Type target,
				Expression expression, Dictionary<ParameterExpression, Expression> bindings)
			{
				var newExpression = expression as NewExpression;

				if (newExpression == null)
				{
					throw new ArgumentException("expression", new Exception("Expression is not instance of NewExpression"));
				}

				target = newExpression.Constructor.DeclaringType;

				Expression check = GetTypeCheck(o, target);

				//
				// Resolve parameters.
				//
				int i = 0;
				foreach (ParameterInfo param in newExpression.Constructor.GetParameters())
				{
					//
					// Mapping information from constructor parameters to properties required.
					//
					PropertyAttribute pa = param.GetCustomAttributes(typeof(PropertyAttribute), false)
												.Cast<PropertyAttribute>().SingleOrDefault();

					if (pa == null)
						throw new InvalidOperationException("Input object doesn't have required mapping information.");

					//
					// Find the property.
					//
					PropertyInfo property = target.GetProperty(pa.Name);
					if (property == null)
						throw new InvalidOperationException(String.Format("Property {0} on type {1} not found.", pa.Name, target.Name));

					check = TryBind(check, o, target, property, newExpression.Arguments[i++], bindings);
				}

				return check; 
			}

			private Expression CompileParameterExpression(ParameterExpression o, Type target,
				Expression expression, Dictionary<ParameterExpression, Expression> bindings)
			{
				var parameterExpression = expression as ParameterExpression;

				if (parameterExpression == null)
				{
					throw new ArgumentException("expression", new Exception("Expression is not instance of ParameterExpression"));
				}

				bindings[parameterExpression] = null;
				
				return Expression.TypeIs(o, target);
			}

			private Expression CompileConstantExpression(ParameterExpression o, Type target,
				Expression expression, Dictionary<ParameterExpression, Expression> bindings)
			{
				var constantExpression = expression as ConstantExpression;

				if (constantExpression == null)
				{
					throw new ArgumentException("expression", new Exception("Expression is not instance of ConstantExpression"));
				}

				return Expression.Equal(Expression.Convert(o, constantExpression.Type), constantExpression);
			}

			private Expression CompileMemberInitExpression(ParameterExpression o, Type target,
				Expression expression, Dictionary<ParameterExpression, Expression> bindings)
			{
				var memberInitExpression = expression as MemberInitExpression;

				if (memberInitExpression == null)
				{
					throw new ArgumentException("expression", new Exception("Expression is not instance of MemberInitExpression"));
				}

				Expression check = CompileNewExpression(o, target, memberInitExpression.NewExpression, bindings);

				//
				// Parse bindings.
				//
				foreach (MemberBinding binding in memberInitExpression.Bindings)
				{
					var ma = binding as MemberAssignment;

					if (ma == null)
						throw new NotSupportedException("Only top-level regular assignment bindings are supported.");

					check = TryBind(check, o, target, ma.Member, ma.Expression, bindings);
				}

				return check; 
			}

			private Expression CompileNewArrayExpression(ParameterExpression o, Type target,
				Expression expression, Dictionary<ParameterExpression, Expression> bindings)
			{
				var newArrayExpression = expression as NewArrayExpression;

				if (newArrayExpression == null)
				{
					throw new ArgumentException("expression", new Exception("Expression is not instance of NewArrayExpression"));
				}

				// check for type & length
				Expression check = Expression.AndAlso(Expression.TypeIs(o, target),
					Expression.Equal(Expression.ArrayLength(Expression.Convert(o, target)),
						Expression.Constant(newArrayExpression.Expressions.Count)
					)
				);

				int i = 0;
				foreach (var e in newArrayExpression.Expressions)
				{
					ParameterExpression pe;
					ConstantExpression ce;

					if ((pe = e as ParameterExpression) != null)
					{
						if (bindings.ContainsKey(pe))
						{
							check = Expression.AndAlso(check, GetEqualityCheck(o, target, i, bindings[pe]));
						}
						else
							bindings[pe] = Expression.ArrayIndex(Expression.Convert(o, target), Expression.Constant(i));
					}
					else if ((ce = e as ConstantExpression) != null)
					{
						check = Expression.AndAlso(check, GetEqualityCheck(o, target, i, ce));
					}
					else
						throw new NotSupportedException("Can only match constants.");

					i++;
				}

				return check; 
			}

			private Expression CompileListInitExpression(ParameterExpression o, Type target, 
				Expression expression, Dictionary<ParameterExpression, Expression> bindings)
			{
				var listInitExpression = expression as ListInitExpression;

				if (listInitExpression == null)
				{
					throw new ArgumentException("expression", new Exception("Expression is not instance of ListInitExpression"));
				}

				if (listInitExpression.NewExpression.Arguments.Count != 0)
					throw new NotSupportedException("Collection initializers in match expressions should use the default constructor.");

				var t = listInitExpression.Type.GetGenericTypeDefinition();

				if (t == typeof (List<>))
				{
					return CompileListInitExpressionForListOfT(o, target, listInitExpression, bindings); 
				}
				
				if (t == typeof(Dictionary<,>))
				{
					return CompileListInitExpressionForDictionaryKonV(o, target, listInitExpression, bindings);
				}

				throw new NotSupportedException("Only List<T> and Dictionary<K,V> are supported.");
			}

			private Expression CompileListInitExpressionForDictionaryKonV(ParameterExpression o, Type target, 
				ListInitExpression listInitExpression, Dictionary<ParameterExpression, Expression> bindings)
			{
				Expression check = Expression.TypeIs(o, target);

				int i = 0;
				foreach (var e in listInitExpression.Initializers)
				{
					Expression keyArg = e.Arguments[0];
					Expression valArg = e.Arguments[1];

					var key = keyArg as ConstantExpression;
					if (key == null)
						throw new NotSupportedException("Keys for dictionaries used in pattern matches should be constant expressions.");

					check = Expression.AndAlso(check,
							Expression.Call(Expression.Convert(o, target), target.GetMethod("ContainsKey"), key));

					ParameterExpression pe;
					ConstantExpression ce;

					if ((pe = valArg as ParameterExpression) != null)
					{
						if (bindings.ContainsKey(pe))
						{
							check = Expression.AndAlso(check,
								GetEqualityCheckForDictionaryEntry(o, target, key, bindings[pe]));
						}
						else
						{
							bindings[pe] = Expression.Call(Expression.Convert(o,target), target.GetMethod("get_Item"), key);
						}
					}
					else if ((ce = valArg as ConstantExpression) != null)
					{
						check = Expression.AndAlso(check, 
							GetEqualityCheckForDictionaryEntry(o, target, key, ce));
					}
					else
						throw new NotSupportedException("Can only match constants.");

					i++;
				}

				return check;
			}

			private Expression CompileListInitExpressionForListOfT(ParameterExpression o, Type target, 
				ListInitExpression listInitExpression, Dictionary<ParameterExpression, Expression> bindings)
			{
				Expression check = Expression.AndAlso(
					Expression.TypeIs(o, target),
					Expression.Equal(
						Expression.Property(
							Expression.Convert(
								o,
								target
							),
							"Count"
						), Expression.Constant(listInitExpression.Initializers.Count)
					)
				);

				int i = 0;
				foreach (var e in listInitExpression.Initializers)
				{
					//
					// Note: We know we're processing List<T>, which only has one Add overload,
					//       so we omit additional checks for now.
					//
					Expression arg = e.Arguments[0];

					ParameterExpression pe;
					ConstantExpression ce;

					if ((pe = arg as ParameterExpression) != null)
					{
						if (bindings.ContainsKey(pe))
						{
							check = Expression.AndAlso(check, GetEqualityCheckForListOfT(o, target, i, bindings[pe]));
						}
						else
							bindings[pe] = Expression.Call(
								Expression.Convert(
									o,
									target
								),
								target.GetMethod("get_Item"),
								Expression.Constant(i)
							);
					}
					else if ((ce = arg as ConstantExpression) != null)
					{
						check = Expression.AndAlso(check, GetEqualityCheckForListOfT(o, target, i, ce));
					}
					else
						throw new NotSupportedException("Can only match constants.");

					i++;
				}

				return check; 
			}

			private Expression TryBind(Expression check, ParameterExpression o, Type target, MemberInfo member,
				Expression expression, Dictionary<ParameterExpression, Expression> bindings)
			{
				ConstantExpression ce;
				ParameterExpression pe;

				if ((ce = expression as ConstantExpression) != null)
				{
					check = Expression.AndAlso(check, GetEqualityCheck(o, target, member, ce));
				}
				else if ((pe = expression as ParameterExpression) != null)
				{
					if (bindings.ContainsKey(pe))
					{
						check = Expression.AndAlso(check, GetEqualityCheck(o, target, bindings[pe], member));
					}
					else
					{
						bindings[pe] = Expression.PropertyOrField(Expression.Convert(o, target), member.Name);
					}
				}
				else
					throw new NotSupportedException("Can only match constants.");

				return check;
			}
		}
	}
}