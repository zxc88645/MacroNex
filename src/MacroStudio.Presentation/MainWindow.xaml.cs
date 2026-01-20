using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using MacroStudio.Presentation.ViewModels;

namespace MacroStudio.Presentation;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow(MainViewModel mainViewModel)
    {
        InitializeComponent();
        DataContext = mainViewModel ?? throw new ArgumentNullException(nameof(mainViewModel));
        
        // Subscribe to log entries collection changes to auto-scroll
        if (mainViewModel.Logging?.Entries is INotifyCollectionChanged notifyCollection)
        {
            notifyCollection.CollectionChanged += OnLogEntriesCollectionChanged;
        }
    }

    private void OnLogEntriesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Auto-scroll to bottom when new items are added
        if (e.Action == NotifyCollectionChangedAction.Add && LogsListBox.Items.Count > 0)
        {
            // Use Dispatcher to ensure UI thread execution
            Dispatcher.BeginInvoke(() =>
            {
                var lastItem = LogsListBox.Items[LogsListBox.Items.Count - 1];
                LogsListBox.ScrollIntoView(lastItem);
            }, System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }
}