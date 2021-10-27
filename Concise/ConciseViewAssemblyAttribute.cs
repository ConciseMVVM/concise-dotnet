using System;

namespace Concise
{
    /// <summary>
    /// Mark this asssembly as having IView implementations. The ViewLocator will
    /// scan the classes in this assembly.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly)]
    public class ConciseViewAssemblyAttribute: Attribute
    {
        public ConciseViewAssemblyAttribute()
        {
        }
    }
}
