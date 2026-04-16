using System;
using System.ComponentModel;

public class Model
{
    public string Name;
}

public sealed class ViewModel : INotifyPropertyChanged
{
    private readonly Model _user = new Model();

    // biến mà view sẽ quan sát
    public string displayName
    {
        get => _user.Name;
        set
        {
            _user.Name = value;
            OnPropertyChanged(nameof(displayName));
        }
    }

    // logic rename
    public void Rename(string name)
    {
        _user.Name = name;
    }

    public event PropertyChangedEventHandler PropertyChanged;

    private void OnPropertyChanged(string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class View
{
    private readonly ViewModel _userViewModel;

    public View(ViewModel userViewModel)
    {
        _userViewModel = userViewModel;

        _userViewModel.PropertyChanged += (s, e) => { Render(_userViewModel.displayName); };
    }

    public void Render(string name)
    {
        if (name == null) throw new ArgumentNullException(nameof(name));
        Console.WriteLine($"Rendering {name}");
    }

    public void RenameButton(string newName)
    {
        Console.WriteLine($"Renaming {nameof(_userViewModel.displayName)}");
        _userViewModel.Rename(newName);
    }
}

class Program
{
    static void Main()
    {
        var vm = new ViewModel();
        var view = new View(vm);
        
        view.Render("Tung");
        
        view.RenameButton("An");
    }
}