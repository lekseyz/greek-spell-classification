using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers;
using Domain;
using Domain.Exceptions;
using Domain.Models;

namespace Ml;

public class MlNetGreekClassifier : IGreekClassifier
{
    private readonly MLContext _mlContext;
    private ITransformer? _trainedModel;
    private PredictionEngine<InputData, OutputData>? _predictionEngine;
    
    // Размеры изображения
    private const int W = GreekSymbolConstants.ImageWidth;
    private const int H = GreekSymbolConstants.ImageHeight;
    private const int Size = GreekSymbolConstants.InputVectorSize;

    public MlNetGreekClassifier()
    {
        // Seed фиксирован для воспроизводимости
        _mlContext = new MLContext(seed: 42);

        // Логирование, чтобы видеть прогресс (L-BFGS может думать пару минут)
        _mlContext.Log += (sender, e) =>
        {
            if (e.Kind == Microsoft.ML.Runtime.ChannelMessageKind.Info)
            {
                // Фильтруем слишком частые сообщения
                if (e.Message.Contains("Optimization") || e.Message.Contains("Iteration"))
                {
                    Debug.WriteLine($"[ML.NET]: {e.Message}");
                    Console.WriteLine($"[ML.NET]: {e.Message}");
                }
            }
        };
    }

    public void Train(List<(GreekSymbolImage image, GreekLetter label)> dataset)
    {
        if (dataset == null || dataset.Count == 0)
            throw new InvalidInputDataException(0, 0);

        Console.WriteLine("Подготовка данных и центрирование изображений...");

        // 1. Преобразуем данные и ЦЕНТРИРУЕМ их на лету
        var trainingData = dataset.Select(d => new InputData
        {
            Pixels = CenterImage(d.image.Pixels), // <--- МАГИЯ ЗДЕСЬ
            Label = (uint)d.label
        });

        IDataView dataView = _mlContext.Data.LoadFromEnumerable(trainingData);

        // 2. Настройка пайплайна
        // Используем LbfgsMaximumEntropy - он точнее чем SDCA для таких задач
        var pipeline = _mlContext.Transforms.Conversion.MapValueToKey(outputColumnName: "LabelKey", inputColumnName: "Label")
            .Append(_mlContext.MulticlassClassification.Trainers.LbfgsMaximumEntropy(
                labelColumnName: "LabelKey", 
                featureColumnName: "PixelFeatures",
                l1Regularization: 0.03f, // Немного убираем шум
                l2Regularization: 0.03f,
                historySize: 50,    // Память для оптимизатора
                optimizationTolerance: 1e-5f // Требуем высокой точности
                ))
            .Append(_mlContext.Transforms.Conversion.MapKeyToValue(outputColumnName: "PredictedLabelValue", inputColumnName: "PredictedLabel"));

        // 3. Запуск обучения
        Console.WriteLine("Запуск обучения модели (L-BFGS)");
        _trainedModel = pipeline.Fit(dataView);
        Console.WriteLine("Обучение завершено!");

        _predictionEngine = null;
    }

    public PredictionResult Predict(GreekSymbolImage image)
    {
        if (_trainedModel == null)
            throw new InvalidNNConfigException("Model has not been trained yet.");

        if (_predictionEngine == null)
        {
            _predictionEngine = _mlContext.Model.CreatePredictionEngine<InputData, OutputData>(_trainedModel);
        }

        // Обязательно центрируем и входное изображение перед предсказанием!
        var centeredPixels = CenterImage(image.Pixels);
        var input = new InputData { Pixels = centeredPixels };
        
        var prediction = _predictionEngine.Predict(input);
        float maxConfidence = prediction.Score.Max();

        return new PredictionResult
        {
            Symbol = (GreekLetter)prediction.PredictedLabelValue,
            Confidence = maxConfidence,
            ModelUsed = "ML.NET (L-BFGS + AutoCenter)"
        };
    }

    public float Test(List<(GreekSymbolImage image, GreekLetter label)> dataset)
    {
        if (_trainedModel == null) return 0.0f;

        // Тестовые данные тоже центрируем
        var testData = dataset.Select(d => new InputData
        {
            Pixels = CenterImage(d.image.Pixels),
            Label = (uint)d.label
        });

        IDataView dataView = _mlContext.Data.LoadFromEnumerable(testData);
        var predictions = _trainedModel.Transform(dataView);
        var metrics = _mlContext.MulticlassClassification.Evaluate(predictions, labelColumnName: "LabelKey", scoreColumnName: "Score");

        return (float)metrics.MacroAccuracy;
    }

    // ------------------------------------------------------------------------------------
    // Логика центрирования (перенесена сюда, чтобы ничего не ломать в DatasetProcessor)
    // ------------------------------------------------------------------------------------
    private float[] CenterImage(float[] source)
    {
        // 1. Находим границы символа
        int minX = W, minY = H, maxX = -1, maxY = -1;

        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                if (source[y * W + x] > 0.1f) // Если пиксель не черный
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        }

        // Если пустая картинка, возвращаем как есть
        if (maxX == -1) return source;

        // 2. Вычисляем смещение к центру
        int width = maxX - minX + 1;
        int height = maxY - minY + 1;
        
        int centerX = minX + width / 2;
        int centerY = minY + height / 2;

        int targetX = W / 2;
        int targetY = H / 2;

        int dx = targetX - centerX;
        int dy = targetY - centerY;

        if (dx == 0 && dy == 0) return source;

        // 3. Применяем смещение
        float[] shifted = new float[Size];
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                // Координаты откуда берем пиксель
                int oldX = x - dx;
                int oldY = y - dy;

                if (oldX >= 0 && oldX < W && oldY >= 0 && oldY < H)
                {
                    shifted[y * W + x] = source[oldY * W + oldX];
                }
            }
        }
        return shifted;
    }

    private class InputData
    {
        [VectorType(GreekSymbolConstants.InputVectorSize)] 
        [ColumnName("PixelFeatures")]
        public float[] Pixels { get; set; } = Array.Empty<float>();

        [ColumnName("Label")]
        public uint Label { get; set; }
    }

    private class OutputData
    {
        [ColumnName("PredictedLabelValue")]
        public uint PredictedLabelValue { get; set; }
        public float[] Score { get; set; } = Array.Empty<float>();
    }
}