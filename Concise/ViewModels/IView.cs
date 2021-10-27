using System;
using System.Threading.Tasks;

namespace Concise.ViewModels
{
    public interface IView
    {
        ViewModel? ViewModel { get; set; }

        Task<bool> PresentViewAsync();
        Task<bool> DismissViewAsync();
    }
}
