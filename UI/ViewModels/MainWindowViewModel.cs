using ReactiveUI;
using Avalonia.Media.Imaging;
using System.Reactive;

namespace UI.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private Bitmap? _cameraFeed;
        private string _predictionResult = "Ожидание...";
        private double _confidence = 0;
        private string _statusInfo = "Система готова";
        private int _selectedModelIndex = 0;

        // Картинка с камеры (биндится к <Image>)
        public Bitmap? CameraFeed
        {
            get => _cameraFeed;
            set => this.RaiseAndSetIfChanged(ref _cameraFeed, value);
        }

        // Результат распознавания (Буква)
        public string PredictionResult
        {
            get => _predictionResult;
            set => this.RaiseAndSetIfChanged(ref _predictionResult, value);
        }

        // Уверенность (для ProgressBar)
        public double Confidence
        {
            get => _confidence;
            set => this.RaiseAndSetIfChanged(ref _confidence, value);
        }
        
        // Индекс выбранной модели в ComboBox
        public int SelectedModelIndex
        {
            get => _selectedModelIndex;
            set => this.RaiseAndSetIfChanged(ref _selectedModelIndex, value);
        }

        // Статус бар внизу
        public string StatusInfo
        {
            get => _statusInfo;
            set => this.RaiseAndSetIfChanged(ref _statusInfo, value);
        }

        // Команда для кнопки (например, добавить в датасет)
        public ReactiveCommand<Unit, Unit> CaptureCommand { get; }

        public MainWindowViewModel()
        {
            CaptureCommand = ReactiveCommand.Create(() => 
            {
                StatusInfo = "Изображение захвачено и добавлено в обучение!";
            });
        }
    }
}