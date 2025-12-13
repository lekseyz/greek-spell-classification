using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq; // Нужно для метода Select при синхронизации
using ReactiveUI;
using Avalonia.Media.Imaging;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Platform;
using Domain;
using Domain.Models;
using Ml; // Убедитесь, что тут лежит NeuralNetworkConfigDto
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
		private readonly	IDatasetGenerator	_datasetGenerator;
		private				IGreekClassifier	_currentClassifier;
		
        // Хранилище данных (DTO)
        private NeuralNetworkConfigDto _config = new NeuralNetworkConfigDto();

        // Поля для UI (стандартные)
        private Bitmap? _cameraFeed;
        private string _predictionResult = "Ожидание...";
        private double _confidence = 0;
        private string _statusInfo = "Система готова";
        private int _selectedModelIndex = 0;
		
		// --- Рисовалка ---
		private bool _isDrawingMode;
		private WriteableBitmap _drawingBitmap;
		
		private const int DrawWidth = GreekSymbolConstants.ImageWidth;
		private const int DrawHeight =  GreekSymbolConstants.ImageHeight;

        // --- Коллекция слоев для UI ---
        public ObservableCollection<HiddenLayerVm> EditableHiddenLayers { get; } = new();

        // --- Команды ---
        public ReactiveCommand<Unit, Unit> CaptureCommand { get; }
        public ReactiveCommand<Unit, Unit> AddLayerCommand { get; }
        public ReactiveCommand<Unit, Unit> RemoveLayerCommand { get; }
		public ReactiveCommand<Unit, Unit> GenerateDatasetCommand { get; }
		public ReactiveCommand<Unit, Unit> TrainNetworkCommand { get; }  
		public ReactiveCommand<Unit, Unit> TestNetworkCommand { get; }
		public ReactiveCommand<Unit, Unit> SaveNetworkCommand { get; }
		public ReactiveCommand<Unit, Unit> LoadNetworkCommand { get; }
		
		public ReactiveCommand<Unit, Unit> ToggleSourceCommand { get; }
		public ReactiveCommand<Unit, Unit> ClearDrawingCommand { get; }

        public MainWindowViewModel()
        {
			_datasetGenerator = ServiceLocator.DatasetGenerator;
			
			_drawingBitmap = new WriteableBitmap(new PixelSize(DrawWidth, DrawHeight), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
			ClearDrawing();
			
			ToggleSourceCommand = ReactiveCommand.Create(() =>
			{
				IsDrawingMode = !IsDrawingMode;
				StatusInfo = IsDrawingMode ? "Режим рисования включен." : "Режим камеры включен.";
                
				// Если переключились на рисование и там уже что-то есть, можно сразу классифицировать
				if (IsDrawingMode)
				{
					ClassifyDrawing();
				}
			});

			// --- Команда очистки холста ---
			ClearDrawingCommand = ReactiveCommand.Create(() =>
			{
				ClearDrawing();
				ClassifyDrawing();
				StatusInfo = "Холст очищен.";
			});
			
			SaveNetworkCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                if (!IsMlpSettingsEnabled)
                {
                    StatusInfo = "Ошибка: Для сохранения выберите 'Самописная (MLP)'.";
                    return;
                }

                if (_currentClassifier is CustomNeuralNetwork customNet)
                {
                    // Для простоты сохраняем в папку приложения. 
                    // Можно заменить на SaveFileDialog.
                    string path = Path.Combine(AppContext.BaseDirectory, "custom_model.json");
                    
                    await Task.Run(() => 
                    {
                        try 
                        {
                            customNet.Save(path);
                            StatusInfo = $"Сеть успешно сохранена в: {path}";
                        }
                        catch (Exception ex)
                        {
                            StatusInfo = $"Ошибка сохранения: {ex.Message}";
                        }
                    });
                }
                else
                {
                    StatusInfo = "Ошибка: Текущая сеть не инициализирована или не является CustomNeuralNetwork.";
                }
            });

            // --- Реализация команды загрузки ---
            LoadNetworkCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                if (!IsMlpSettingsEnabled)
                {
                    StatusInfo = "Ошибка: Переключитесь на 'Самописная (MLP)' перед загрузкой.";
                    return;
                }

                string path = Path.Combine(AppContext.BaseDirectory, "custom_model.json");
                if (!File.Exists(path))
                {
                    StatusInfo = "Ошибка: Файл сохраненной модели не найден.";
                    return;
                }

                StatusInfo = "Загрузка сети...";

                await Task.Run(() =>
                {
                    try
                    {
                        // 1. Загружаем сеть
                        var loadedNet = CustomNeuralNetwork.Load(path);
                        _currentClassifier = loadedNet;

                        // 2. Обновляем DTO и свойства VM для синхронизации UI
                        var loadedConfig = loadedNet.Config;

                        // Обновляем простые свойства (Dispatcher не обязателен, т.к. ReactiveUI handle property changes)
                        // Но если возникнут проблемы с потоками, оберните в Dispatcher.UIThread.InvokeAsync
                        InputSize = loadedConfig.InputSize;
                        Epochs = loadedConfig.Epochs;
                        TrainingSampleSize = loadedConfig.TrainingSampleSize;
                        LearningRateUi = (decimal)loadedConfig.LearningRate;
                        AcceptableErrorUi = (decimal)loadedConfig.AcceptableError;

                        // Обновляем коллекцию слоев (нужно делать в UI потоке для надежности, или использовать BindingOperations.EnableCollectionSynchronization)
                        Avalonia.Threading.Dispatcher.UIThread.Invoke(() =>
                        {
                            EditableHiddenLayers.Clear();
                            foreach (var neurons in loadedConfig.HiddenLayerNeurons)
                            {
                                EditableHiddenLayers.Add(new HiddenLayerVm(neurons));
                            }
                            // Синхронизируем внутренний DTO с UI
                            SyncLayersToDto();
                        });

                        StatusInfo = "Сеть и конфигурация успешно загружены!";
                    }
                    catch (Exception ex)
                    {
                        StatusInfo = $"Ошибка загрузки: {ex.Message}";
                    }
                });
            });
			
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
			
			TrainNetworkCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                if (!IsMlpSettingsEnabled) // Проверка, что выбрана самописная сеть
                {
                    StatusInfo = "Error: Custom network must be selected for training.";
                    return;
                }
                StatusInfo = "Starting custom network training. Loading training data...";

                string trainPath = Path.Combine(AppContext.BaseDirectory, "dataset_processed", "train");

                await Task.Run(() =>
                {
                    try
                    {
                        SyncLayersToDto(); // Синхронизируем настройки UI с DTO перед обучением
                        var trainDataset = _datasetGenerator.LoadDataset(trainPath); 

                        if (trainDataset.Count == 0)
                        {
                            StatusInfo = $"Error: No training samples found in {trainPath}. Generate dataset first.";
                            return; 
                        }
						
						
						NeuralNetworkConfig config = new NeuralNetworkConfig();
						config.InputSize = _config.InputSize;
						config.HiddenLayerNeurons = _config.HiddenLayerNeurons.ToArray();
						config.TrainingSampleSize = _config.TrainingSampleSize;
						config.Epochs = _config.Epochs;
						config.OutputClasses = _config.OutputClasses;
						config.AcceptableError = _config.AcceptableError;
						config.LearningRate = _config.LearningRate;


                        _currentClassifier = ServiceLocator.GetClassifier(config); // Получаем новый классификатор с текущей конфигурацией
                        _currentClassifier.Train(trainDataset);

                        StatusInfo = $"Network retraining completed. Total samples: {trainDataset.Count}.";
                    }
                    catch (Exception ex)
                    {
                        StatusInfo = $"Training failed: {ex.Message}";
                    }
                });
            });

            // НОВАЯ РЕАЛИЗАЦИЯ: Команда для кнопки "Тестировать сеть"
            TestNetworkCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                if (!IsMlpSettingsEnabled) // Проверка, что выбрана самописная сеть
                {
                    StatusInfo = "Error: Custom network must be selected for testing.";
                    return;
                }
                StatusInfo = "Loading test dataset and running test... Please wait.";

                string testPath = Path.Combine(AppContext.BaseDirectory, "dataset_processed", "test");

                await Task.Run(() =>
                {
                    try
                    {
                        var testDataset = _datasetGenerator.LoadDataset(testPath); 

                        if (testDataset.Count == 0)
                        {
                            StatusInfo = $"Error: No test samples found in {testPath}. Generate dataset first.";
                            return; 
                        }

                        // Тестируем текущий экземпляр классификатора
                        float accuracy = _currentClassifier.Test(testDataset);
                        
                        StatusInfo = $"Test complete. Accuracy: {accuracy:P2} over {testDataset.Count} samples.";
                    }
                    catch (Exception ex)
                    {
                        StatusInfo = $"Testing failed: {ex.Message}";
                    }
                });
            });
        }

		#region Drawing

		public void ClearDrawing()
		{
			// Создаем НОВЫЙ битмап. Это заставит UI гарантированно обновиться через Binding,
			// так как изменится ссылка на объект.
			var newBitmap = new WriteableBitmap(
				new PixelSize(DrawWidth, DrawHeight), 
				new Vector(96, 96), 
				PixelFormat.Bgra8888, 
				AlphaFormat.Opaque);

			using (var buffer = newBitmap.Lock())
			{
				unsafe
				{
					uint* ptr = (uint*)buffer.Address;
					int length = DrawWidth * DrawHeight;
					// Заливка черным
					for (int i = 0; i < length; i++)
						ptr[i] = 0xFF000000;
				}
			}

			// Присваивание нового объекта вызывает RaiseAndSetIfChanged -> UI обновляется
			DrawingBitmap = newBitmap; 
		}

        // Метод рисования линии (вызывается из Code-behind при движении мыши)
        public void DrawLine(int x0, int y0, int x1, int y1)
        {
            using (var buffer = _drawingBitmap.Lock())
            {
                unsafe
                {
                    uint* ptr = (uint*)buffer.Address;
                    int stride = buffer.RowBytes / 4; // int stride

                    // Алгоритм Брезенхема
                    int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
                    int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
                    int err = dx + dy, e2;

                    while (true)
                    {
                        if (x0 >= 0 && x0 < DrawWidth && y0 >= 0 && y0 < DrawHeight)
                        {
                            // Рисуем белым (255,255,255,255) -> 0xFFFFFFFF
                            ptr[y0 * stride + x0] = 0xFFFFFFFF;
                        }

                        if (x0 == x1 && y0 == y1) break;
                        e2 = 2 * err;
                        if (e2 >= dy) { err += dy; x0 += sx; }
                        if (e2 <= dx) { err += dx; y0 += sy; }
                    }
                }
            }
            this.RaisePropertyChanged(nameof(DrawingBitmap));
        }

        // Классификация нарисованного
        public void ClassifyDrawing()
        {
            if (_currentClassifier == null)
            {
                PredictionResult = "Сеть не готова";
                return;
            }

            float[] pixels = new float[DrawWidth * DrawHeight];

            using (var buffer = _drawingBitmap.Lock())
            {
                unsafe
                {
                    uint* ptr = (uint*)buffer.Address;
                    int stride = buffer.RowBytes / 4;

                    for (int y = 0; y < DrawHeight; y++)
                    {
                        for (int x = 0; x < DrawWidth; x++)
                        {
                            uint pixel = ptr[y * stride + x];
                            // Извлекаем компоненты. PixelFormat.Bgra8888
                            // B = (pixel & 0xFF)
                            // G = ((pixel >> 8) & 0xFF)
                            // R = ((pixel >> 16) & 0xFF)
                            // Берем R канал (так как рисуем ЧБ, они равны)
                            byte r = (byte)((pixel >> 16) & 0xFF);
                            
                            // Нормализация 0..1
                            pixels[y * DrawWidth + x] = r / 255.0f;
                        }
                    }
                }
            }

            try
            {
                var image = new GreekSymbolImage(pixels);
                var result = _currentClassifier.Predict(image);
                PredictionResult = result.Symbol.ToString();
                Confidence = result.Confidence * 100;
            }
            catch (Exception ex)
            {
                PredictionResult = "Ошибка";
                StatusInfo = ex.Message;
            }
        }

		#endregion

		public bool IsDrawingMode
		{
			get => _isDrawingMode;
			set => this.RaiseAndSetIfChanged(ref _isDrawingMode, value);
		}

		public WriteableBitmap DrawingBitmap
		{
			get => _drawingBitmap;
			set => this.RaiseAndSetIfChanged(ref _drawingBitmap, value);
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