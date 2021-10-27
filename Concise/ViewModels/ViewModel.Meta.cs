using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace Concise.ViewModels
{
	public partial class ViewModel
	{

		public static class Meta
        {
            #region ViewModel -> IView mapping


            private static readonly Dictionary<Type, Type> ViewModelMap = GetViewModelMap();


            private static Dictionary<Type, Type> GetViewModelMap()
            {
                try
                {
                    var viewModelMap = new Dictionary<Type, Type>();

                    var pairs = AppDomain.CurrentDomain.GetAssemblies()
                        .Where(a => a.GetCustomAttributes(typeof(ConciseViewAssemblyAttribute), true).Length > 0)
                        .SelectMany(a => a.GetTypes())
                        .Where(t => t.IsClass && !t.IsAbstract && t.GetInterfaces().Contains(typeof(IView)))
                        .Select(t =>
                        {
                            var attr = t.GetCustomAttribute<ConciseViewAttribute>();

                            return new { V = t, VM = attr?.ViewModel, Ignore = attr?.Ignore ?? false };
                        });

                    foreach (var pair in pairs)
                    {
                        if (pair.Ignore)
                            continue;

                        if (pair.VM == null)
                            throw new Exception($"Class implements Concise.ViewModels.IView but doesn't have ConciseViewAttribute: {pair.V.FullName}. Use [ConciseView(Ignore:true)] if this is intentional.");

                        if (!typeof(ViewModel).IsAssignableFrom(pair.VM))
                            throw new Exception(@$"Parameter to ConciseViewAttribute must be a ViewModel for {pair.V.FullName}");

                        if (viewModelMap.ContainsKey(pair.VM))
                            throw new Exception($"{pair.VM.FullName} maps to more than one View");

                        // this is a match!!

                        Debug.WriteLine($"Mapped: {pair.VM.Name} --> {pair.V.Name}");

                        viewModelMap[pair.VM] = pair.V;
                    }

                    return viewModelMap;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"ViewLocator Exception: {ex.Message}");
                    throw;
                }
            }


            public static Type? GetViewType(Type viewModel) => ViewModelMap.TryGetValue(viewModel, out Type viewType) ? viewType : null;


            #endregion


            #region Routes -> ViewModel mapping


            public static readonly RouteTemplate[] RouteTemplates = GetRouteTemplates();


            private static RouteTemplate[] GetRouteTemplates() =>
                AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => a.GetCustomAttributes(typeof(ConciseViewModelAssemblyAttribute), true).Length > 0)
                    .SelectMany(a => a.GetTypes())
                    .Where(t => t.IsClass && !t.IsAbstract && typeof(ViewModel).IsAssignableFrom(t))
                    .Select(t =>
                    {
                        var attr = t.GetCustomAttribute<ConciseRouteAttribute>();

                        return new { VM = t, attr?.Route };
                    })
                .Where((p) => p.Route != null)
                .Select((p) => new RouteTemplate(p.Route, p.VM))
                .ToArray();


            #endregion
        }
    }
}
