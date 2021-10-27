using System;
using System.ComponentModel;
using System.Diagnostics;
using Concise.Observables;

namespace Concise.ViewModels
{
    public class ViewModelItem: ObservableObject, INotifyPropertyChanged
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

    }
}
