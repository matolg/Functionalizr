using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace PatternMatching.Matcher
{
    // TODO: add bool for lazy execution with one ctor
    public class Pattern<T>
    {
        private bool _hasValue;

        private Func<T> _else;

        private readonly List<MatchEntry> _matches = new List<MatchEntry>();

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

            Dictionary<ParameterExpression, PropertyInfo> bindings;

            if (!TryMatch(Object, e, out bindings))
                return this;

            Result = Evaluate(Object, e, f, bindings);
            _hasValue = true;

            return this;
        }

        private static bool TryMatch(object o, LambdaExpression e, out Dictionary<ParameterExpression, PropertyInfo> bindings)
        {
            bindings = new Dictionary<ParameterExpression, PropertyInfo>();

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
                    bindings[pe] = property;
                }
                else
                    throw new NotSupportedException("Can only match constants.");
            }

            return true;
        }

        private static T Evaluate(object o, LambdaExpression e, Delegate f, Dictionary<ParameterExpression, PropertyInfo> bindings)
        {
            var args = new object[e.Parameters.Count];
            int j = 0;

            foreach (ParameterExpression param in e.Parameters)
            {
                PropertyInfo property;
                if (!bindings.TryGetValue(param, out property))
                    throw new InvalidOperationException("Parameter " + param.Name + " was not bound in the pattern match.");

                object value = property.GetValue(o, null);
                if (!value.GetType().IsAssignableFrom(param.Type))
                    throw new InvalidOperationException(
                        String.Format("Property {0} on type {1} cannot be bound to parameter {2}.",
                                      property.Name, property.DeclaringType != null ? property.DeclaringType.Name : string.Empty, param.Name));

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

            foreach (var entry in _matches)
            {
                Dictionary<ParameterExpression, PropertyInfo> bindings;

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
                var ne = Match.Body as NewExpression;
                if (ne == null)
                    throw new NotSupportedException("Match clause can only contain NewExpression.");

                Type target = ne.Constructor.DeclaringType;
                if (target == null)
                    throw new NotSupportedException("Constructor cannot be null.");

                var bindings = new Dictionary<ParameterExpression, PropertyInfo>();

                ParameterExpression o = Expression.Parameter(typeof(object), "o");

                Expression check = GetTypeCheck(o, target);

                int i = 0;
                foreach (ParameterInfo param in ne.Constructor.GetParameters())
                {
                    PropertyAttribute pa = param.GetCustomAttributes(typeof(PropertyAttribute), false)
                                                .Cast<PropertyAttribute>().SingleOrDefault();

                    if (pa == null)
                        throw new InvalidOperationException("Input object doesn't have required mapping information.");

                    PropertyInfo property = target.GetProperty(pa.Name);
                    if (property == null)
                        throw new InvalidOperationException(String.Format("Property {0} on type {1} not found.", pa.Name,
                                                                          target.Name));

                    ConstantExpression ce;
                    ParameterExpression pe;

                    Expression arg = ne.Arguments[i++];
                    if ((ce = arg as ConstantExpression) != null)
                    {
                        check = Expression.AndAlso(
                            check,
                            GetEqualityCheck(o, target, property, ce)
                            );
                    }
                    else if ((pe = arg as ParameterExpression) != null)
                    {
                        bindings[pe] = property;
                    }
                    else
                        throw new NotSupportedException("Can only match constants.");
                }

                CompiledMatch = Expression.Lambda<Func<object, bool>>(check, o).Compile();

                var args = new Expression[Match.Parameters.Count];
                int j = 0;
                foreach (ParameterExpression param in Match.Parameters)
                {
                    PropertyInfo property;
                    if (!bindings.TryGetValue(param, out property))
                        throw new InvalidOperationException("Parameter " + param.Name + " was not bound in the pattern match.");

                    // Expression to grab value from property.
                    args[j++] = Expression.Property(Expression.Convert(o, target), property);
                }

                var invoker = Expression.Invoke(Expression.Constant(Action), args);
                CompiledInvoker = Expression.Lambda<Func<object, T>>(invoker, o).Compile();
            }


            private MethodCallExpression GetEqualityCheck(ParameterExpression parameterExpression, Type target,
                PropertyInfo property, ConstantExpression ce)
            {
                var objectPropertyArgument = Expression.Convert(
                    Expression.Property(
                        Expression.Convert(parameterExpression, target),
                        property
                        ), typeof(object));

                var constantArgument = Expression.Convert(ce, typeof(object));

                return Expression.Call(typeof(object), "Equals", new Type[0], objectPropertyArgument, constantArgument);
            }

            private Expression GetTypeCheck(ParameterExpression parameterExpression, Type target)
            {
                return Expression.TypeIs(parameterExpression, target);
            }
        }
    }
}