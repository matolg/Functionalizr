using System;

namespace PatternMatching.Matcher
{
    [AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = true)]
    internal sealed class PropertyAttribute : Attribute
    {
        public string Name { get; set; }
    }
}