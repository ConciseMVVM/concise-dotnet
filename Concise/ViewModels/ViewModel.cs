using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Concise.Observables;

namespace Concise.ViewModels
{
    public interface IRoutableViewModel
    {
        Uri Route { set; }
    }

    public partial class ViewModel: ObservableObject, INotifyPropertyChanged
    {
        #region INotifyPropertyChanged support

        public event PropertyChangedEventHandler? PropertyChanged;
        public virtual void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        protected override void OnObservableValueAdded(string propertyName, IObservableValue observableValue)
        {
            base.OnObservableValueAdded(propertyName, observableValue);

            observableValue.Subscribe(() =>
            {
                Debug.WriteLine($"{this.GetType().Name}: Property Changed: {propertyName}");
                OnPropertyChanged(propertyName);
            }).AddTo(Subscriptions);
        }

        #endregion

        #region View Management

        public static event Action<ViewModel>? ViewPresented;
        public static void OnViewPresented(ViewModel viewModel) =>
            ViewPresented?.Invoke(viewModel);
        public static void OnViewPresented(IView view) 
        {
            if (view.ViewModel is ViewModel viewModel)
                OnViewPresented(viewModel);
        }

    public static event Action<ViewModel>? ViewDismissed;
        public static void OnViewDismissed(ViewModel viewModel) =>
            ViewDismissed?.Invoke(viewModel);
        public static void OnViewDismissed(IView view)
        {
            if (view.ViewModel is ViewModel viewModel)
                OnViewDismissed(viewModel);
        }

        private IView? _view;


        public IView? View
        {
            get => _view ?? (View = ViewModel.CreateView(this));
            set
            {
                if (_view != null)
                    _view.ViewModel = null;

                _view = value;

                if (_view != null)
                    _view.ViewModel = this;
            }
        }


        public bool HasView => _view != null;


        public Type? GetViewType() => Meta.GetViewType(GetType());


        public static IView? CreateView(ViewModel viewModel)
        {
            var viewType = viewModel.GetViewType();

            if (viewType == null)
                return null;

            return Activator.CreateInstance(viewType) as IView;
        }

        /// <summary>
        // attempts to present the IView associated with this ViewModel.
        /// </summary>
        /// <returns>true if the view was presented</returns>
        public Task<bool> PresentViewAsync()
        {
            var view = View;

            if (view == null)
                return Task.FromResult(false);

            return view.PresentViewAsync();
        }


        public async void PresentView()
        {
            try
            {
                var result = await PresentViewAsync();

                if (!result)
                    Debug.WriteLine($"Failed to present view for {GetType().FullName}");

            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception presenting view for {GetType().FullName}: {ex.Message}");
            }
        }


        public Task<bool> DismissViewAsync(ViewModel viewModel)
        {
            var view = View;

            if (view == null)
                return Task.FromResult(false);

            return view.DismissViewAsync();
        }


        public async void DismissView()
        {
            try
            {
                var result = await DismissViewAsync(this);

                if (!result)
                    Debug.WriteLine($"Failed to dismiss view for {GetType().FullName}");

            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception dismissing view for {GetType().FullName}: {ex.Message}");
            }
        }


        #endregion


        #region ViewModel Presentation


        public static Task<bool> PresentViewModelAsync(ViewModel viewModel) =>
            viewModel.PresentViewAsync();


        public static Task<bool> PresentViewModelAsync<X>() where X : ViewModel, new() =>
            PresentViewModelAsync(new X());


        public static async void PresentViewModel(ViewModel viewModel) 
        {
            try
            {
                var result = await PresentViewModelAsync(viewModel);
                if (!result)
                    Debug.WriteLine($"Failed to present view model {viewModel.GetType().FullName}");
            }
            catch(Exception ex)
            {
                Debug.WriteLine($"Exception dismissing view model {viewModel.GetType().FullName}: {ex.Message}");
            }
        }


        public static async void PresentViewModel<X>() where X : ViewModel, new()
        {
            try
            {
                var result = await PresentViewModelAsync<X>();
                if (!result)
                    Debug.WriteLine($"Failed to present view model {typeof(X).FullName}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception dismissing view model {typeof(X).FullName}: {ex.Message}");
            }
        }


        #endregion


        #region ViewModel Routing

        public static Uri? BaseRoutingUri { get; set; }

        public static RouteTemplate? GetRouteTemplate(Type viewModelType) =>
            Meta.RouteTemplates.FirstOrDefault((rt) => rt.ViewModelType == viewModelType);

        public static Uri? GetRouteUri(Type viewModelType, Dictionary<string, object> values) =>
            GetRouteTemplate(viewModelType)?.GetUri(values);

        public static Uri? GetRouteUri<T>(Dictionary<string, object> values) where T : ViewModel => GetRouteUri(typeof(T), values) switch
        {
            null => null,
            Uri relativeUri => new Uri(BaseRoutingUri, relativeUri)
        };

        /// <summary>
        /// Returns a ViewModel instance associated with the given Uri. The ViewModel must
        /// be decorated with the ConciseRouteAttribute matching the Uri's path (non case
        /// sensitive) If the ViewModel implements the IRoutableViewModel interface the
        /// Route property will be set before returning.
        ///
        /// If the ViewModel cannot be found or created successfully an exception will be
        /// thrown.
        /// 
        /// </summary>
        /// <param name="route">the route to find the ViewModel for</param>
        /// <returns>the ViewModel instance</returns>
        public static ViewModel ViewModelForRoute(Uri route)
        {
            var matchingTemplates = Meta.RouteTemplates
                 .Where((t) => t.Matches(route));

            var template = matchingTemplates.Count() switch
            {
                0 => throw new Exception($"ViewModel Route not found for {route}"),
                1 => matchingTemplates.First(),
                _ => throw new Exception($"Miltiple ViewModel Routes not found for {route}")
            };

            return template.CreateViewModel(route) ?? throw new Exception($"Unable to create ViewModel for route {route}");
        }


        #endregion
    }
}
