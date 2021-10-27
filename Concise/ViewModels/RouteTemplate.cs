using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Web;

namespace Concise.ViewModels
{
    public class RouteTemplate
    {
        abstract class Component
        {
            public abstract bool Matches(string component);
            public abstract KeyValuePair<string, string>? GetValue(string component);
            public abstract string? GetString(Dictionary<string, object> values);

            public static Component FromString(string component)
            {
                var decodedComponent = HttpUtility.UrlDecode(component);

                if (decodedComponent.StartsWith("{") && decodedComponent.EndsWith("}"))
                    return new Variable(decodedComponent.Substring(1, decodedComponent.Length - 2));
                else
                    return new Constant(decodedComponent);
            }

            class Constant : Component
            {
                public string Value { get; }

                public override bool Matches(string component) =>
                    string.Equals(Value, component, StringComparison.InvariantCultureIgnoreCase);

                public override KeyValuePair<string, string>? GetValue(string component) => null;

                public override string? GetString(Dictionary<string, object> values) =>
                    Value;

                public Constant(string value) =>
                    (Value) = (value);
            }

            class Variable : Component
            {
                public string Name { get; }

                public override bool Matches(string component) => true;

                public override KeyValuePair<string, string>? GetValue(string component) =>
                    (Name.Length == 0) ? null :
                    new KeyValuePair<string, string>(Name, component);

                public override string? GetString(Dictionary<string, object> values) =>
                    values.TryGetValue(Name, out var obj) ? obj.ToString() : null;

                public Variable(string name) =>
                    (Name) = (name);
            }
        }


        public Uri OriginalRoute { get; }
        public Type ViewModelType { get; }

        private readonly Component[] Path;
        private readonly Dictionary<string, Component> QueryString;

        private static Component[] ParsePathTemplate(string path) =>
            path.Split('/')
            .Where((p) => p.Length > 0)
            .Select((p) => Component.FromString(p))
            .ToArray();

        private static Dictionary<string, Component> ParseQueryStringTemplate(string queryString)
        {
            var result = new Dictionary<string, Component>();

            if (!queryString.StartsWith("?"))
                return result;

            foreach(string item in queryString.Substring(1).Split('&'))
            {
                string[] pair = item.Split('=');

                if (pair.Length != 2)
                    continue;

                result[HttpUtility.UrlDecode(pair[0]).ToLowerInvariant()] = Component.FromString(pair[1]);
            }

            return result;
        }


        public RouteTemplate(Uri routeTemplate, Type viewModelType)
        {
            OriginalRoute = routeTemplate;
            ViewModelType = viewModelType;

            Path = ParsePathTemplate(routeTemplate.AbsolutePath);
            QueryString = ParseQueryStringTemplate(routeTemplate.Query);
        }

        public RouteTemplate(string routeTemplate, Type viewModelType)
            : this(new Uri(routeTemplate), viewModelType) { }

        private bool ParsePath(string path, Dictionary<string, string>? values = null)
        {
            string[] pathComponents = path.Split('/');
            int pathIndex = 0;

            foreach (string pathComponent in pathComponents)
            {
                if (pathComponent.Length == 0)
                    continue; // ignore empty path components

                if (pathIndex >= Path.Length)
                    return false; // path is longer than template

                var templateComponent = Path[pathIndex++];

                if (!templateComponent.Matches(pathComponent))
                    return false;

                if (values != null && templateComponent.GetValue(pathComponent) is KeyValuePair<string, string> pathValue)
                    values[pathValue.Key] = pathValue.Value;
            }

            return pathIndex == Path.Length; // false if our template is longer than our path
        }

        private void ParseQueryString(string queryString, Dictionary<string, string> values)
        {
            if (!queryString.StartsWith("?"))
                return;

            foreach (string item in queryString.Substring(1).Split('&'))
            {
                string[] pair = item.Split('=');

                if (pair.Length != 2)
                    continue;

                if (!QueryString.TryGetValue(HttpUtility.UrlDecode(pair[0]).ToLowerInvariant(), out var templateComponent))
                    continue;

                if (templateComponent.GetValue(pair[1]) is KeyValuePair<string, string> pathValue)
                    values[pathValue.Key] = values[pathValue.Value];
            }
        }

        public bool Matches(Uri route) => ParsePath(route.AbsolutePath);

        private Dictionary<string, string>? Parse(Uri route)
        {
            var values = new Dictionary<string, string>();

            if (!ParsePath(route.AbsolutePath, values))
                return null;

            ParseQueryString(route.Query, values);

            return values;
        }

        public ViewModel? CreateViewModel(Uri route)
        {
            var values = Parse(route);

            if (values == null)
                return null;

            var viewModel = (ViewModel)Activator.CreateInstance(ViewModelType);

            Debug.WriteLine($"Creating {ViewModelType.FullName} from route {route}:");

            foreach (var propertyName in values.Keys)
            {
                Debug.WriteLine($"    {propertyName} = {values[propertyName]}");

                var property = ViewModelType.GetProperty(propertyName);

                if (property == null)
                    throw new Exception($"Failed to initialize {ViewModelType.FullName} from route {route}: Property {propertyName} not found.");

                var value = Convert.ChangeType(values[propertyName], property.PropertyType);

                property.SetValue(viewModel, value);
            }

            if (viewModel is IRoutableViewModel routable)
                routable.Route = route;

            return viewModel;
        }

        public Uri GetUri(Dictionary<string, object> values)
        {
            // Build a Uri from the values pased in

            var path = string.Join("/", Path
                .Select((p) => HttpUtility.UrlEncode(p.GetString(values) ?? "")));

            var queryString = string.Join("&", QueryString
                .Select((x) => new { x.Key, Value = x.Value.GetString(values) })
                .Where((x) => x.Value != null)
                .Select((x) => $"{HttpUtility.UrlEncode(x.Key)}={HttpUtility.UrlEncode(x.Value)}"));

            return new Uri((queryString.Length > 0) ? $"{path}?{queryString}" : path, UriKind.Relative);
        }
    }
}
