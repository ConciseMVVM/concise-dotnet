using System;

namespace Concise
{
    [AttributeUsage(AttributeTargets.Class)]
    public class ConciseViewAttribute: Attribute
    {
        public Type? ViewModel { get; }
        public bool Ignore { get; }

        public ConciseViewAttribute(Type viewModel) =>
            (ViewModel, Ignore) = (viewModel, false);

        public ConciseViewAttribute(bool Ignore) =>
            (ViewModel, this.Ignore) = (null, Ignore);
    }
}
