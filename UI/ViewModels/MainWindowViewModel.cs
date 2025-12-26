using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq; // Нужно для метода Select при синхронизации
using ReactiveUI;
using Avalonia.Media.Imaging;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Platform;
using Domain;
using Domain.Models;
using Ml; // Убедитесь, что тут лежит NeuralNetworkConfigDto
using UI.DTOs;
using OpenCvSharp;
using Avalonia.Threading;
using Avalonia.Media.Imaging;

namespace UI.ViewModels
{
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
		private const int	DrawWidth		= GreekSymbolConstants.ImageWidth;
		private const int	DrawHeight		= GreekSymbolConstants.ImageHeight;
		private const float CameraDeltaTime = 1/30f;
		private const uint	BlackUInt		= 0xFF000000;
		private const uint	WhiteUInt		= 0xFFFFFFFF;
		
		private readonly Vector _canvasDpi = new Vector(96, 96);
		
		private readonly	IDatasetGenerator		_datasetGenerator;
		private				List<IGreekClassifier>	_currentClassifiers;
		private				IPhotoProcessor			_photoProcessor;
		
        // DTO
        private NeuralNetworkConfigDto _config = new NeuralNetworkConfigDto();

        // UI
        private Bitmap? _cameraFeed;
        private string 	_predictionResult = "Ожидание...";
        private double 	_confidence = 0;
        private string 	_statusInfo = "Система готова";
        private int		_selectedModelIndex = 0;
        
        // Camera
        private VideoCapture?				_capture;
        private CancellationTokenSource?	_cameraCts;
		private DispatcherTimer				_cameraTimer;
		private Mat?						_latestFrame; 
		
		// Draw
		private bool			_isDrawingMode;
		private WriteableBitmap _drawingBitmap;
		
        // Layers
        public ObservableCollection<HiddenLayerVm> EditableHiddenLayers { get; } = new();

        // Commands
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
        public ReactiveCommand<Unit, Unit> ClassifyCameraCommand { get; }

		// -------------------------------------- PropertiesUI --------------------------------------
		#region PropertiesUI

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
		
		public int SelectedModelIndex
		{
			get => _selectedModelIndex;
			set
			{
				this.RaiseAndSetIfChanged(ref _selectedModelIndex, value);
				this.RaisePropertyChanged(nameof(IsCustomSettingsEnabled));
			}
		}

		public bool IsCustomSettingsEnabled => SelectedModelIndex == 0;
        
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

		#endregion
		
		// -------------------------------------- PropertiesConfig --------------------------------------
		#region PropertiesConfig
		public int InputSize
		{
			get => _config.InputSize;
			set
			{
				_config.InputSize = value;
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
		#endregion
		
        public MainWindowViewModel()
        {
			_datasetGenerator	= ServiceLocator.DatasetGenerator;
			_photoProcessor		= ServiceLocator.PhotoProcessor;
			_drawingBitmap		= new WriteableBitmap(new PixelSize(DrawWidth, DrawHeight), _canvasDpi, PixelFormat.Bgra8888, AlphaFormat.Opaque);
            _currentClassifiers = [new CustomNeuralNetwork(new NeuralNetworkConfig()), new MlNetGreekClassifier()];
            ClearDrawing();
            
            _cameraTimer			= new DispatcherTimer();
            _cameraTimer.Interval	= TimeSpan.FromMilliseconds(CameraDeltaTime); 
            _cameraTimer.Tick		+= CameraTimer_Tick;
			
			foreach (var neurons in _config.HiddenLayerNeurons)
			{
				EditableHiddenLayers.Add(new HiddenLayerVm(neurons));
			}
			
			// -------------------------------------- Camera/Drawing --------------------------------------
			#region Camera/Drawing
            ToggleSourceCommand = ReactiveCommand.Create(() =>
            {
                IsDrawingMode = !IsDrawingMode;
    
                if (IsDrawingMode)
                {
                    StopCamera();
                    StatusInfo = "Режим рисования включен.";
                    ClassifyDrawing();
                }
                else
                {
                    StartCamera();
                    StatusInfo = "Режим камеры включен.";
                }
            });

            ClassifyCameraCommand = ReactiveCommand.Create(ClassifyCameraImage);
			ClearDrawingCommand = ReactiveCommand.Create(() =>
			{
				ClearDrawing();
				ClassifyDrawing();
				StatusInfo = "Холст очищен.";
			});
			#endregion
			
			// -------------------------------------- Save/Load --------------------------------------
			#region SaveLoad
			SaveNetworkCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                if (!IsCustomSettingsEnabled)
                {
                    StatusInfo = "Ошибка: Для сохранения выберите 'Самописная (MLP)'.";
                    return;
                }

                if (_currentClassifiers[_selectedModelIndex] is CustomNeuralNetwork customNet)
                {
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
            
            LoadNetworkCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                if (!IsCustomSettingsEnabled)
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
                        var loadedNet = CustomNeuralNetwork.Load(path);
                        _currentClassifiers[0] = loadedNet;

                        var loadedConfig = loadedNet.Config;
                        InputSize = loadedConfig.InputSize;
                        Epochs = loadedConfig.Epochs;
                        TrainingSampleSize = loadedConfig.TrainingSampleSize;
                        LearningRateUi = (decimal)loadedConfig.LearningRate;
                        AcceptableErrorUi = (decimal)loadedConfig.AcceptableError;

						// Update layers UI
                        Avalonia.Threading.Dispatcher.UIThread.Invoke(() =>
                        {
                            EditableHiddenLayers.Clear();
                            foreach (var neurons in loadedConfig.HiddenLayerNeurons)
                            {
                                EditableHiddenLayers.Add(new HiddenLayerVm(neurons));
                            }
							
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
			#endregion

			// -------------------------------------- LayerConfig --------------------------------------
			#region LayerConfig
            AddLayerCommand = ReactiveCommand.Create(() =>
            {
                EditableHiddenLayers.Add(new HiddenLayerVm(64));
                SyncLayersToDto(); // Обновляем DTO
            });

            RemoveLayerCommand = ReactiveCommand.Create(() =>
            {
                if (EditableHiddenLayers.Count > 0)
                {
                    EditableHiddenLayers.RemoveAt(EditableHiddenLayers.Count - 1);
                    SyncLayersToDto();
                }
            });
			#endregion

			// -------------------------------------- Dataset --------------------------------------
			#region Dataset
            CaptureCommand = ReactiveCommand.Create(() => 
            {
				// TODO: 
                StatusInfo = "Изображение захвачено и добавлено в обучение!";
            });
			
			GenerateDatasetCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				StatusInfo = "Generating and augmenting dataset... Please wait.";
                
				string rawPath = Path.Combine(AppContext.BaseDirectory, "dataset"); 
				string outputPath = Path.Combine(AppContext.BaseDirectory, "dataset_processed");
                
				await Task.Run(() => 
				{
					_datasetGenerator.ProcessAndExport(rawPath, outputPath);
				});
                
				StatusInfo = $"Dataset successfully generated and saved to {outputPath}.";
			});
			#endregion
			
			// -------------------------------------- Train/Test --------------------------------------
			#region Train/Test
			TrainNetworkCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				var modelName = _selectedModelIndex == 0 ? "Custom" : "NetML";
                StatusInfo = $"Starting {modelName} network training. Loading training data...";

                string trainPath = Path.Combine(AppContext.BaseDirectory, "dataset_processed", "train");

                await Task.Run(() =>
                {
                    try
                    {
                        SyncLayersToDto(); 
                        var trainDataset = _datasetGenerator.LoadDataset(trainPath); 

                        if (trainDataset.Count == 0)
                        {
                            StatusInfo = $"Error: No training samples found in {trainPath}. Generate dataset first.";
                            return; 
                        }
						
						NeuralNetworkConfig config = new NeuralNetworkConfig();
						Debug.Assert(_config.HiddenLayerNeurons != null, "_config.HiddenLayerNeurons != null");
						
						config.InputSize = _config.InputSize;
						config.HiddenLayerNeurons = _config.HiddenLayerNeurons.ToArray();
						config.TrainingSampleSize = _config.TrainingSampleSize;
						config.Epochs = _config.Epochs;
						config.OutputClasses = _config.OutputClasses;
						config.AcceptableError = _config.AcceptableError;
						config.LearningRate = _config.LearningRate;
						
                        _currentClassifiers[_selectedModelIndex] = ServiceLocator.GetClassifier(config, _selectedModelIndex);
                        _currentClassifiers[_selectedModelIndex].Train(trainDataset);

                        StatusInfo = $"Network retraining completed. Total samples: {trainDataset.Count}.";
                    }
                    catch (Exception ex)
                    {
                        StatusInfo = $"Training failed: {ex.Message}";
                    }
                });
            });

            TestNetworkCommand = ReactiveCommand.CreateFromTask(async () =>
            {
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

                        float accuracy = _currentClassifiers[_selectedModelIndex].Test(testDataset);
                        
                        StatusInfo = $"Test complete. Accuracy: {accuracy:P2} over {testDataset.Count} samples.";
                    }
                    catch (Exception ex)
                    {
                        StatusInfo = $"Testing failed: {ex.Message}";
                    }
                });
            });
			#endregion
        }
		
		// -------------------------------------- DrawingFuncs --------------------------------------
		#region Drawing
		public void ClearDrawing()
		{
			var newBitmap = new WriteableBitmap(
				new PixelSize(DrawWidth, DrawHeight), 
				_canvasDpi, 
				PixelFormat.Bgra8888, 
				AlphaFormat.Opaque);

			using (var buffer = newBitmap.Lock())
			{
				unsafe
				{
					uint* ptr = (uint*)buffer.Address;
					int length = DrawWidth * DrawHeight;
					for (int i = 0; i < length; i++)
						ptr[i] = BlackUInt;
				}
			}
			
			DrawingBitmap = newBitmap; 
		}

        public void DrawLine(int x0, int y0, int x1, int y1)
        {
            using (var buffer = _drawingBitmap.Lock())
            {
                unsafe
                {
                    uint* ptr = (uint*)buffer.Address;
                    int stride = buffer.RowBytes / 4; 

                    int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
                    int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
                    int err = dx + dy, e2;

                    while (true)
                    {
                        if (x0 >= 0 && x0 < DrawWidth && y0 >= 0 && y0 < DrawHeight)
                        {
                            ptr[y0 * stride + x0] = WhiteUInt;
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

        public void ClassifyDrawing()
        {
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
                            // B = (pixel & 0xFF)
                            // G = ((pixel >> 8) & 0xFF)
                            // R = ((pixel >> 16) & 0xFF)
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
                var result = _currentClassifiers[_selectedModelIndex].Predict(image);
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
		
        public void SyncLayersToDto()
        {
            _config.HiddenLayerNeurons.Clear();
            foreach (var layer in EditableHiddenLayers)
            {
                _config.HiddenLayerNeurons.Add(layer.Neurons);
            }
        }
		
		// -------------------------------------- CameraFuncs --------------------------------------
		#region CameraFuncs
        private void StartCamera()
        {
            try
            {
                Debug.WriteLine("Попытка запуска камеры...");
        
                if (_capture == null) _capture = new VideoCapture(0); 

                if (_capture.IsOpened())
                {
                    Debug.WriteLine($"Камера открыта успешно! Backend: {_capture.GetBackendName()}");
                    _capture.Set(VideoCaptureProperties.FrameWidth, 640);
                    _capture.Set(VideoCaptureProperties.FrameHeight, 480);
            
                    _cameraTimer.Start();
                }
                else
                {
                    Debug.WriteLine("Capture.IsOpened() вернул false. Камера не найдена по индексу 0.");
                    StatusInfo = "Не удалось открыть камеру (индекс 0).";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"КРИТИЧЕСКАЯ ОШИБКА КАМЕРЫ: {ex}");
                StatusInfo = $"Ошибка камеры: {ex.GetType().Name}";
            }
        }

        private void ClassifyCameraImage()
		{
		    if (_latestFrame == null || _latestFrame.Empty()) 
		    {
		        StatusInfo = "Нет изображения с камеры.";
		        return;
		    }
		
		    try
		    {
		        var image = _photoProcessor.Process(_latestFrame);
		        var result = _currentClassifiers[_selectedModelIndex].Predict(image);
		        
		        PredictionResult = result.Symbol.ToString();
		        Confidence = result.Confidence * 100;
		        StatusInfo = "Распознано с камеры.";
		    }
		    catch (Exception ex)
		    {
		        StatusInfo = $"Ошибка обработки: {ex.Message}";
		    }
		}
        
        private void StopCamera()
        {
            _cameraTimer.Stop();
            _capture?.Dispose();
            _capture = null;
            CameraFeed = null;
        }

        private void CameraTimer_Tick(object? sender, EventArgs e)
        {
            if (_capture != null && _capture.IsOpened())
            {
                using var frame = new Mat();
                bool readSuccess = _capture.Read(frame);

                if (!readSuccess)
                {
                    Debug.WriteLine("Сбой чтения кадра (_capture.Read вернул false)");
                    return;
                }

                if (frame.Empty())
                {
                    Debug.WriteLine("Кадр пустой (frame.Empty() == true)");
                    return;
                }

                try 
                {
                    _latestFrame = frame.Clone();

					using var stream = frame.ToMemoryStream(".bmp");
					stream.Position = 0;
					CameraFeed = new Bitmap(stream);
				}
                catch (Exception ex)
                {
                    Debug.WriteLine($"Ошибка конвертации кадра: {ex.Message}");
                }
            }
        }
		#endregion
    }
}