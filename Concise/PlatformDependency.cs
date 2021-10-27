using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Concise
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public class RegisterPlatformDependencyAttribute: Attribute
    {
        public readonly Type Implementation;

        public RegisterPlatformDependencyAttribute(Type implementation) =>
            (Implementation) = (implementation);
    }

    public static class PlatformDependency
    {
        private static readonly Type[] Implementations = GetImplementations();

        private static Type[] GetImplementations()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetCustomAttributes(typeof(RegisterPlatformDependencyAttribute)))
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                .Select(attr => (attr as RegisterPlatformDependencyAttribute).Implementation)
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                .ToArray();
        }

        static public Type? TryResolveType(Type dependencyInterface)
        {
            if (!dependencyInterface.IsInterface)
                throw new ArgumentException("dependencyInterface must be an interface");

            var types = Implementations
                .Where(t => t.GetInterfaces().Contains(dependencyInterface));

            return (types.Count()) switch
            {
                0 => null,
                1 => types.First(),
                _ => throw new Exception($"Multiple PlatformDependency implementations found for {dependencyInterface.Name}"),
            };
        }

        static public Type? TryResolveType<T>() where T: class?
        {
            return TryResolveType(typeof(T));
        }

        static public T? TryResolve<T>() where T: class?
        {
            var type = TryResolveType<T>();

            if (type == null)
                return null;

            return Activator.CreateInstance(type) as T;
        }

        static public T Resolve<T>() where T: class
        {
            var imp = TryResolve<T>();

            if (imp == null)
                throw new Exception($"Unable to create instance of {typeof(T).Name}");

            return imp;
        }
    }
}
