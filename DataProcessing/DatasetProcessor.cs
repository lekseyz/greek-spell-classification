using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Domain;
using System.IO;
using System.Linq;
using Domain.Models;

namespace DataProcessing;

public class DatasetProcessor: IDatasetGenerator
{
	private const float BinarizationThreshold = 128f; 
	private const int	MaxShift = 4;
	private const int	CountAugmented = 30;
	
    private readonly int _width = GreekSymbolConstants.ImageWidth;
    private readonly int _height = GreekSymbolConstants.ImageHeight;
    private readonly int _inputSize = GreekSymbolConstants.InputVectorSize;
	
    private readonly Random _rng = new Random();
	
	public List<(GreekSymbolImage image, GreekLetter label)> LoadDataset(string basePath)
	{
		var allSamples = new List<(GreekSymbolImage image, GreekLetter label)>();
        
		if (!Directory.Exists(basePath))
		{
			Console.WriteLine($"Error: Dataset path not found at {basePath}");
			return allSamples;
		}

		foreach (var dir in Directory.GetDirectories(basePath))
		{
			if (!Enum.TryParse<GreekLetter>(Path.GetFileName(dir), true, out var label))
			{
				Console.WriteLine($"Skipping directory: {dir}. Does not match GreekLetter enum.");
				continue;
			}

			foreach (var file in Directory.GetFiles(dir, "*.png", SearchOption.TopDirectoryOnly))
			{
				try
				{
					using var image = Image.Load<L8>(file);
					
					if (image.Width != _width || image.Height != _height)
					{
						Console.WriteLine($"Skipping file: {file}. Expected size {_width}x{_height}, but got {image.Width}x{image.Height}.");
						continue;
					}
					
					float[] pixels = ExtractPixels(image);
					allSamples.Add((new GreekSymbolImage(pixels), label));
				}
				catch (Exception ex)
				{
					Console.WriteLine($"Error loading file {file}: {ex.Message}");
				}
			}
		}
        Shuffle(allSamples);

		return allSamples;
	}

    public void ProcessAndExport(string rawDatasetPath, string outputPath)
    {
        var allSamples = new List<(GreekLetter Label, float[] Pixels)>();

        foreach (var dir in Directory.GetDirectories(rawDatasetPath))
        {
            if (!Enum.TryParse<GreekLetter>(Path.GetFileName(dir), true, out var label))
                continue; 

            foreach (var file in Directory.GetFiles(dir, "*.png", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    using var image = Image.Load<L8>(file);
                    
                    if (image.Width != _width || image.Height != _height)
                    {
                         Console.WriteLine($"Skipping file: {file}. Expected size {_width}x{_height}, but got {image.Width}x{image.Height}.");
                         continue;
                    }

                    float[] basePixels = ExtractPixels(image);
                    allSamples.Add((label, basePixels));

                    var augmented = GenerateAugmented(basePixels, CountAugmented);
                    foreach (var augPixels in augmented)
                    {
                        allSamples.Add((label, augPixels));
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing file {file}: {ex.Message}");
                }
            }
        }

        Shuffle(allSamples);

        int trainCount = (int)(allSamples.Count * 0.8);
        var trainSet = allSamples.Take(trainCount).ToList();
        var testSet = allSamples.Skip(trainCount).ToList();

        SaveToDisk(trainSet, Path.Combine(outputPath, "train"));
        SaveToDisk(testSet, Path.Combine(outputPath, "test"));

        Console.WriteLine($"Data processing complete. Total samples: {allSamples.Count}. Train: {trainSet.Count}, Test: {testSet.Count}");
    }

    private float[] ExtractPixels(Image<L8> image)
    {
        float[] pixels = new float[_inputSize];
        
        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < _height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < _width; x++)
                {
                    pixels[y * _width + x] = row[x].PackedValue < BinarizationThreshold ? 1.0f : 0.0f; 
                }
            }
        });
        return pixels;
    }

    private List<float[]> GenerateAugmented(float[] source, int count)
    {
        var (minX, minY, maxX, maxY) = FindBoundingBox(source);
        
        var possibleShifts = new List<(int dx, int dy)>();
        
        for (int dx = -MaxShift; dx <= MaxShift; dx++)
        {
            for (int dy = -MaxShift; dy <= MaxShift; dy++)
            {
                if (dx == 0 && dy == 0)
                    continue;
                
                if (IsShiftValid(minX, minY, maxX, maxY, dx, dy))
                    possibleShifts.Add((dx, dy));
            }
        }
        
        List<(int dx, int dy)> shiftsToApply;
        
        if (possibleShifts.Count <= count)
        {
			Shuffle(possibleShifts);
            shiftsToApply = possibleShifts;
        }
        else
        {
            Shuffle(possibleShifts);
            shiftsToApply = possibleShifts.Take(count).ToList();
        }

        var result = new List<float[]>();
        foreach (var shift in shiftsToApply)
        {
            result.Add(Shift(source, shift.dx, shift.dy));
        }

        return result;
    }

    private (int minX, int minY, int maxX, int maxY) FindBoundingBox(float[] source)
    {
        int minX = _width;
        int minY = _height;
        int maxX = -1;
        int maxY = -1;

        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                if (source[y * _width + x] > 0.0f) // Active pixel
                {
                    minX = Math.Min(minX, x);
                    minY = Math.Min(minY, y);
                    maxX = Math.Max(maxX, x);
                    maxY = Math.Max(maxY, y);
                }
            }
        }

        return (minX, minY, maxX, maxY);
    }

    private bool IsShiftValid(int minX, int minY, int maxX, int maxY, int dx, int dy)
    {
        if (maxX == -1) 
			return true; 

        if (minX + dx < 0)
			return false;
        if (maxX + dx >= _width)
			return false;
        
        if (minY + dy < 0)	
			return false;
        if (maxY + dy >= _height)
			return false;
        
        return true;
    }
	
    private float[] Shift(float[] source, int dx, int dy)
    {
        float[] shifted = new float[_inputSize]; 

        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                int oldX = x - dx;
                int oldY = y - dy;

                if (oldX >= 0 && oldX < _width && oldY >= 0 && oldY < _height)
                {
                    shifted[y * _width + x] = source[oldY * _width + oldX];
                }
            }
        }
        return shifted;
    }


    private void SaveToDisk(List<(GreekLetter Label, float[] Pixels)> data, string basePath)
    {
        if (Directory.Exists(basePath))
            Directory.Delete(basePath, true); 
        
        foreach (var item in data)
        {
            string folder = Path.Combine(basePath, item.Label.ToString());
            Directory.CreateDirectory(folder);

            using var image = new Image<L8>(_width, _height);
            
            image.ProcessPixelRows(accessor => {
                 for (int y = 0; y < _height; y++)
                 {
                     var row = accessor.GetRowSpan(y);
                     for (int x = 0; x < _width; x++)
                     {
						 row[x] = new L8((byte)((1.0f - item.Pixels[y * _width + x]) * 255));
                     }
                 }
            });

            string fileName = $"{Guid.NewGuid()}.png";
            image.SaveAsPng(Path.Combine(folder, fileName));
        }
    }

    private void Shuffle<T>(List<T> list)
    {
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = _rng.Next(n + 1);
            (list[k], list[n]) = (list[n], list[k]);
        }
    }
}