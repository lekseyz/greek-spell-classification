using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using UI.ViewModels; // Важно: подключаем пространство имен ViewModel

namespace UI
{
    public partial class MainWindow : Window
    {
		private bool _isDrawing;
		private Point _lastPoint;
        
        public MainWindow()
        {
            InitializeComponent();
            
            // Самая важная строка: связываем окно с ViewModel
            DataContext = new MainWindowViewModel();
        }
		
		private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
		{
			var point = e.GetPosition((Visual)sender);
            
			if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
			{
				_isDrawing = true;
				_lastPoint = point;
                
				if (DataContext is MainWindowViewModel vm)
				{
					vm.DrawLine((int)point.X, (int)point.Y, (int)point.X, (int)point.Y);
                    
					// ПРИНУДИТЕЛЬНОЕ ОБНОВЛЕНИЕ
					DrawingImage.InvalidateVisual();
				}
			}
		}

		private void OnPointerMoved(object? sender, PointerEventArgs e)
		{
			if (!_isDrawing) return;

			var point = e.GetPosition((Visual)sender);
            
			// Простая проверка границ
			if (point.X < 0 || point.Y < 0 || point.X >= 28 || point.Y >= 28) return;

			if (DataContext is MainWindowViewModel vm)
			{
				vm.DrawLine((int)_lastPoint.X, (int)_lastPoint.Y, (int)point.X, (int)point.Y);
				_lastPoint = point;
                
				// ПРИНУДИТЕЛЬНОЕ ОБНОВЛЕНИЕ ПРИ ДВИЖЕНИИ
				DrawingImage.InvalidateVisual();
			}
		}

		private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
		{
			if (_isDrawing)
			{
				_isDrawing = false;
				if (DataContext is MainWindowViewModel vm)
				{
					vm.ClassifyDrawing();
				}
			}
		}
    }
}