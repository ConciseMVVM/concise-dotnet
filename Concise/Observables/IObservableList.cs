using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;

namespace Concise.Observables
{
    public interface IObservableList<T>: INotifyCollectionChanged, INotifyPropertyChanged, IReadOnlyList<T>, ICollection
    {
    }
}
