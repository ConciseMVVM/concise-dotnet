using System;
using System.Linq;
using System.Threading.Tasks;
using Concise.Observables;
using Concise.ViewModels;
using Xamarin.Forms;

namespace Concise.Forms
{
    [ConciseView(Ignore:true)]
    public class ConciseContentPage: ContentPage, IView
    {
        protected readonly DisposableCollection Subscriptions = new();

        public ConciseContentPage()
        {
        }

        ViewModel? IView.ViewModel
        {
            get => BindingContext as ViewModel;
            set => BindingContext = value;
        }

        public bool ModalPresentation { get; set; } = false;
        public bool AnimatePresentation { get; set; } = true;

        static INavigation GetTopNavigation()
        {
            // get root navigation...

            var navigation = Application.Current.MainPage.Navigation;

            // get the top of the modal stack...

            navigation = navigation.ModalStack.LastOrDefault()?.Navigation ?? navigation;

            // get the current page in the nav...

            navigation = navigation.NavigationStack.LastOrDefault()?.Navigation ?? navigation;

            return navigation;
        }

        public virtual async Task<bool> PresentViewAsync()
        {
            var navigation = GetTopNavigation();

            if (ModalPresentation)
                await navigation.PushModalAsync(new NavigationPage(this), AnimatePresentation);
            else
                await navigation.PushAsync(this, AnimatePresentation);

            ViewModel.OnViewPresented(this);

            return true;
        }

        public virtual async Task<bool> DismissViewAsync()
        {
            if (ModalPresentation)
                await Navigation.PopModalAsync(AnimatePresentation);
            else
                await Navigation.PopAsync(AnimatePresentation);

            ViewModel.OnViewDismissed(this);

            return true;
        }

        protected virtual void OnViewModelSet(ViewModel viewModel)
        {
        }

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();

            if (BindingContext is ViewModel viewModel)
                OnViewModelSet(viewModel);
        }
    }
}
