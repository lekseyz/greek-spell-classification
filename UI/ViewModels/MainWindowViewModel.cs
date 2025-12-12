using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq; // Нужно для метода Select при синхронизации
using ReactiveUI;
using Avalonia.Media.Imaging;
using System.Reactive;
using System.Threading.Tasks;
using Domain;
using Domain.Models; // Убедитесь, что тут лежит NeuralNetworkConfigDto
using UI.DTOs;

namespace UI.ViewModels
{
    // 1. Вспомогательный класс-обертка для одного слоя
    // Нужен, чтобы мы могли менять число нейронов в UI, и интерфейс об этом узнавал
    public class HiddenLayerVm : ReactiveObject
    {
        private int _neurons;
        public int Neurons
        {
            get => _neurons;
            set => this.RaiseAndSetIfChanged(ref _neurons, value);
        }

        public HiddenLayerVm(int neurons) => Neurons = neurons;
    }

    public class MainWindowViewModel : ViewModelBase
    {
		private readonly IDatasetGenerator _datasetGenerator;
		
        // Хранилище данных (DTO)
        private NeuralNetworkConfigDto _config = new NeuralNetworkConfigDto();

        // Поля для UI (стандартные)
        private Bitmap? _cameraFeed;
        private string _predictionResult = "Ожидание...";
        private double _confidence = 0;
        private string _statusInfo = "Система готова";
        private int _selectedModelIndex = 0;

        // --- Коллекция слоев для UI ---
        public ObservableCollection<HiddenLayerVm> EditableHiddenLayers { get; } = new();

        // --- Команды ---
        public ReactiveCommand<Unit, Unit> CaptureCommand { get; }
        public ReactiveCommand<Unit, Unit> AddLayerCommand { get; }
        public ReactiveCommand<Unit, Unit> RemoveLayerCommand { get; }
		public ReactiveCommand<Unit, Unit> GenerateDatasetCommand { get; }

        public MainWindowViewModel()
        {
			_datasetGenerator = ServiceLocator.DatasetGenerator;
			
            // Инициализация слоев из конфига (если там что-то есть по умолчанию)
            if (_config.HiddenLayerNeurons != null)
            {
                foreach (var neurons in _config.HiddenLayerNeurons)
                {
                    EditableHiddenLayers.Add(new HiddenLayerVm(neurons));
                }
            }

            // Команда: Добавить слой (по умолчанию 64 нейрона)
            AddLayerCommand = ReactiveCommand.Create(() =>
            {
                EditableHiddenLayers.Add(new HiddenLayerVm(64));
                SyncLayersToDto(); // Обновляем DTO
            });

            // Команда: Удалить последний слой
            RemoveLayerCommand = ReactiveCommand.Create(() =>
            {
                if (EditableHiddenLayers.Count > 0)
                {
                    EditableHiddenLayers.RemoveAt(EditableHiddenLayers.Count - 1);
                    SyncLayersToDto();
                }
            });

            // Команда: Захват изображения
            CaptureCommand = ReactiveCommand.Create(() => 
            {
                StatusInfo = "Изображение захвачено и добавлено в обучение!";
                // Тут будет логика сохранения кадра
            });
			
			GenerateDatasetCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				StatusInfo = "Generating and augmenting dataset... Please wait.";
                
				// Define paths
				string rawPath = Path.Combine(AppContext.BaseDirectory, "dataset"); 
				string outputPath = Path.Combine(AppContext.BaseDirectory, "dataset_processed");
                
				// Execute generation on a background thread to keep the UI responsive
				await Task.Run(() => 
				{
					// Call the real implementation
					_datasetGenerator.ProcessAndExport(rawPath, outputPath);
				});
                
				StatusInfo = $"Dataset successfully generated and saved to {outputPath}.";
			});
        }

        // Метод синхронизации: UI (EditableHiddenLayers) -> DTO (_config)
        // Вызывайте его перед началом обучения
        public void SyncLayersToDto()
        {
            _config.HiddenLayerNeurons.Clear();
            foreach (var layer in EditableHiddenLayers)
            {
                _config.HiddenLayerNeurons.Add(layer.Neurons);
            }
        }

        // --- Свойства конфигурации (DTO proxy) ---

        public int InputSize
        {
            get => _config.InputSize;
            set
            {
                // Прямая установка в DTO
                _config.InputSize = value;
                // Уведомляем UI, что свойство изменилось
                this.RaisePropertyChanged(); 
            }
        }

        public int TrainingSampleSize
        {
            get => _config.TrainingSampleSize;
            set
            {
                _config.TrainingSampleSize = value;
                this.RaisePropertyChanged();
            }
        }

        public int Epochs
        {
            get => _config.Epochs;
            set
            {
                _config.Epochs = value;
                this.RaisePropertyChanged();
            }
        }

        // ВАЖНО: NumericUpDown использует decimal, а в DTO у нас float.
        // Делаем свойства-конвертеры.

        public decimal AcceptableErrorUi
        {
            get => (decimal)_config.AcceptableError;
            set
            {
                _config.AcceptableError = (float)value;
                this.RaisePropertyChanged();
            }
        }

        public decimal LearningRateUi
        {
            get => (decimal)_config.LearningRate;
            set
            {
                _config.LearningRate = (float)value;
                this.RaisePropertyChanged();
            }
        }
        
        // --- Свойства UI ---

        public int SelectedModelIndex
        {
            get => _selectedModelIndex;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedModelIndex, value);
                // Уведомляем зависимое свойство
                this.RaisePropertyChanged(nameof(IsMlpSettingsEnabled));
            }
        }

        public bool IsMlpSettingsEnabled => SelectedModelIndex == 0;
        
        public Bitmap? CameraFeed
        {
            get => _cameraFeed;
            set => this.RaiseAndSetIfChanged(ref _cameraFeed, value);
        }

        public string PredictionResult
        {
            get => _predictionResult;
            set => this.RaiseAndSetIfChanged(ref _predictionResult, value);
        }

        public double Confidence
        {
            get => _confidence;
            set => this.RaiseAndSetIfChanged(ref _confidence, value);
        }

        public string StatusInfo
        {
            get => _statusInfo;
            set => this.RaiseAndSetIfChanged(ref _statusInfo, value);
        }
    }
}