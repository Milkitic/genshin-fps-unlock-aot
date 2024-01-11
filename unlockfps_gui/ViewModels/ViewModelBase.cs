using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ReactiveUI;

namespace UnlockFps.Gui.ViewModels;

public class ViewModelBase : ReactiveObject
{
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        this.RaisePropertyChanged(propertyName);
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}