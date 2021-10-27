using System;

namespace Concise
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class GenerateConcreteObservableObjectAttribute : Attribute
    {
        public GenerateConcreteObservableObjectAttribute()
        {
        }
    }
}
