using System;

namespace Concise
{
    /// <summary>
    /// Defines a URI route for a ViewModel
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class ConciseRouteAttribute: Attribute
    {
        public string Route { get; }

        public ConciseRouteAttribute(string route) =>
            (Route) = (route);
    }
}
