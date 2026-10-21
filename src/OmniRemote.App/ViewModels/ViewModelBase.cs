using CommunityToolkit.Mvvm.ComponentModel;
using OmniRemote.App.Services;

namespace OmniRemote.App.ViewModels;

public abstract class ViewModelBase : ObservableObject
{
    public LocalizationService Loc => LocalizationService.Instance;
}
