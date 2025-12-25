using Domain;
using Domain.Models;
using OpenCvSharp;

namespace DataProcessing;

public class PhotoProcessor: IPhotoProcessor
{
	public GreekSymbolImage Process(FileStream stream)
	{
		if (stream.CanSeek && stream.Position != 0)
		{
			stream.Position = 0;
		}

		using var mat = Mat.FromStream(stream, ImreadModes.Color);
		return Process(mat);
	}

	public GreekSymbolImage Process(Mat mat)
	{
		using var gray = new Mat();
		// 1. Перевод в оттенки серого
		Cv2.CvtColor(mat, gray, ColorConversionCodes.BGR2GRAY);

		// 2. Обрезка по центру (делаем квадрат)
		int size = Math.Min(gray.Width, gray.Height);
		int x = (gray.Width - size) / 2;
		int y = (gray.Height - size) / 2;
		using var cropped = new Mat(gray, new OpenCvSharp.Rect(x, y, size, size));

		// 3. Ресайз до 28x28
		using var resized = new Mat();
		Cv2.Resize(cropped, resized, new OpenCvSharp.Size(28, 28));

		// 4. Бинаризация (Threshold) + Инверсия
		// BinaryInv делает черные чернила (значения близкие к 0) белыми (255), 
		// а белую бумагу (255) черной (0). Otsu автоматически подбирает порог.
		using var binary = new Mat();
		Cv2.Threshold(resized, binary, 0, 255, ThresholdTypes.BinaryInv | ThresholdTypes.Otsu);

		// 5. Перевод пикселей в float[] (нормализация 0..1)
		float[] pixels = new float[28 * 28];
        
		// Получаем доступ к байтам изображения (быстрый способ через индексатор для простоты)
		for (int r = 0; r < 28; r++)
		{
			for (int c = 0; c < 28; c++)
			{
				// Получаем значение пикселя (0 или 255)
				byte val = binary.At<byte>(r, c);
				pixels[r * 28 + c] = val / 255.0f;
			}
		}
		
		return new GreekSymbolImage(pixels);
	}
}