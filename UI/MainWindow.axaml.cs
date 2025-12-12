using Avalonia.Controls;
using UI.ViewModels; // Важно: подключаем пространство имен ViewModel

namespace UI
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            
            // Самая важная строка: связываем окно с ViewModel
            DataContext = new MainWindowViewModel();
        }
    }
}