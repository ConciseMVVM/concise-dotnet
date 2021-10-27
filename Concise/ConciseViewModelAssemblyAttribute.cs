using System;

namespace Concise
{
    /// <summary>
    /// Mark this asssembly as having ViewModel implementations. The ViewLocator will
    /// scan the classes in this assembly.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly)]
    public class ConciseViewModelAssemblyAttribute : Attribute
    {
        public ConciseViewModelAssemblyAttribute()
        {
        }
    }
}
