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
    private readonly int _width = GreekSymbolConstants.ImageWidth;
    private readonly int _height = GreekSymbolConstants.ImageHeight;
    private readonly int _inputSize = GreekSymbolConstants.InputVectorSize;
    
    // Binarization threshold for 8-bit grayscale image
    private const float BinarizationThreshold = 128f; 
    
    // Max displacement for random shifting
    private const int MaxShift = 4; // Maximum shift of +/- 2 pixels
    
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
					using var image = Image.Load<L8>(file); // Load as Grayscale (8 bit)
                    
					// !!! Strict size check (resizing is forbidden) !!!
					if (image.Width != _width || image.Height != _height)
					{
						Console.WriteLine($"Skipping file: {file}. Expected size {_width}x{_height}, but got {image.Width}x{image.Height}.");
						continue;
					}

					// Binarization and conversion to float[] using the existing ExtractPixels logic
					// (Assuming ExtractPixels is accessible, possibly by changing its modifier to public if necessary, but private is fine here as LoadDataset is in the same class and calls it)
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

        // 1. Read, binarize, and augment
        foreach (var dir in Directory.GetDirectories(rawDatasetPath))
        {
            if (!Enum.TryParse<GreekLetter>(Path.GetFileName(dir), true, out var label))
                continue; // Skip folders that do not match GreekLetter enum

            foreach (var file in Directory.GetFiles(dir, "*.png", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    using var image = Image.Load<L8>(file); // Load as Grayscale (8 bit)
                    
                    // !!! Strict size check (resizing is forbidden) !!!
                    if (image.Width != _width || image.Height != _height)
                    {
                         Console.WriteLine($"Skipping file: {file}. Expected size {_width}x{_height}, but got {image.Width}x{image.Height}.");
                         continue;
                    }

                    // Binarization and conversion to float[]
                    float[] basePixels = ExtractPixels(image);
                    allSamples.Add((label, basePixels));

                    // Augmentation (Random Shifting)
                    var augmented = GenerateAugmented(basePixels, 30); // Generate 5 unique and valid augmented samples
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

        // 2. Shuffle
        Shuffle(allSamples);

        // 3. Split into Train/Test (80% / 20%)
        int trainCount = (int)(allSamples.Count * 0.8);
        var trainSet = allSamples.Take(trainCount).ToList();
        var testSet = allSamples.Skip(trainCount).ToList();

        // 4. Save to disk for Python
        SaveToDisk(trainSet, Path.Combine(outputPath, "train"));
        SaveToDisk(testSet, Path.Combine(outputPath, "test"));

        Console.WriteLine($"Data processing complete. Total samples: {allSamples.Count}. Train: {trainSet.Count}, Test: {testSet.Count}");
    }

    /// <summary>
    /// Extracts pixels and applies binarization (byte 0..255 -> float 0.0/1.0).
    /// </summary>
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
                    // If brightness (L8) > threshold, set to 1.0f (active pixel), otherwise 0.0f (background).
                    pixels[y * _width + x] = row[x].PackedValue < BinarizationThreshold ? 1.0f : 0.0f; 
                }
            }
        });
        return pixels;
    }

    /// <summary>
    /// Generates augmented versions of the image by random shifting.
    /// It first finds all possible unique and valid shifts, then randomly selects the required 'count'.
    /// This avoids inefficient looping caused by generating invalid/duplicate random shifts.
    /// </summary>
    private List<float[]> GenerateAugmented(float[] source, int count)
    {
        // Find bounding box once for the source image
        var (minX, minY, maxX, maxY) = FindBoundingBox(source);
        
        // 1. Pre-calculate all possible valid, unique, non-zero shifts.
        var possibleShifts = new List<(int dx, int dy)>();
        
        for (int dx = -MaxShift; dx <= MaxShift; dx++)
        {
            for (int dy = -MaxShift; dy <= MaxShift; dy++)
            {
                // Skip the (0, 0) shift
                if (dx == 0 && dy == 0)
                {
                    continue;
                }
                
                // Check if the shift is valid (does not push the symbol out of bounds)
                if (IsShiftValid(minX, minY, maxX, maxY, dx, dy))
                {
                    possibleShifts.Add((dx, dy));
                }
            }
        }
        
        // 2. Select the final list of shifts to apply
        List<(int dx, int dy)> shiftsToApply;
        
        if (possibleShifts.Count <= count)
        {
            // If there are fewer possible shifts than requested, use all of them.
            shiftsToApply = possibleShifts;
        }
        else
        {
            // If there are more possible shifts, randomly select 'count' unique ones.
            // Shuffle the list and take the first 'count' elements.
            Shuffle(possibleShifts);
            shiftsToApply = possibleShifts.Take(count).ToList();
        }

        // 3. Apply the selected shifts and generate augmented images
        var result = new List<float[]>();
        foreach (var shift in shiftsToApply)
        {
            result.Add(Shift(source, shift.dx, shift.dy));
        }

        return result;
    }

    /// <summary>
    /// Finds the bounding box (min/max X and Y) of active (non-zero) pixels.
    /// </summary>
    /// <returns>A tuple (minX, minY, maxX, maxY). maxX=-1 indicates an empty image.</returns>
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

    /// <summary>
    /// Checks if applying the shift (dx, dy) to the bounding box will keep all active pixels
    /// within the image boundaries (0 to Width-1/Height-1).
    /// </summary>
    private bool IsShiftValid(int minX, int minY, int maxX, int maxY, int dx, int dy)
    {
        // If the image is empty (no active pixels), any shift is technically valid.
        if (maxX == -1) return true; 

        // Check X constraints
        // New leftmost X must be >= 0
        if (minX + dx < 0) return false;
        // New rightmost X must be < _width
        if (maxX + dx >= _width) return false;
        
        // Check Y constraints
        // New topmost Y must be >= 0
        if (minY + dy < 0) return false;
        // New bottommost Y must be < _height
        if (maxY + dy >= _height) return false;
        
        return true;
    }


    /// <summary>
    /// Shifts the pixel vector by (dx, dy). Pixels shifted off the edge are lost, 
    /// and the new area introduced by the shift is filled with the background value (0.0f).
    /// NOTE: This method assumes the shift validity check has already passed.
    /// </summary>
    private float[] Shift(float[] source, int dx, int dy)
    {
        float[] shifted = new float[_inputSize]; // Initialized to zeros (background)

        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                int newX = x + dx;
                int newY = y + dy;

                // We calculate the source coordinates (oldX, oldY) that map to the new (x, y) location.
                // The pixel at source[oldY * _width + oldX] moves to shifted[y * _width + x].
                int oldX = x - dx;
                int oldY = y - dy;

                // Boundary check on the SOURCE coordinates
                if (oldX >= 0 && oldX < _width && oldY >= 0 && oldY < _height)
                {
                    // Copy pixel from old location to new location
                    shifted[y * _width + x] = source[oldY * _width + oldX];
                }
            }
        }
        return shifted;
    }


    /// <summary>
    /// Saves the data to disk in the /basePath/Label/file.png structure, ready for Python.
    /// </summary>
    private void SaveToDisk(List<(GreekLetter Label, float[] Pixels)> data, string basePath)
    {
        // Delete directory contents if it already exists
        if (Directory.Exists(basePath))
            Directory.Delete(basePath, true); 
        
        foreach (var item in data)
        {
            string folder = Path.Combine(basePath, item.Label.ToString());
            Directory.CreateDirectory(folder);

            // Create an image from the float[] vector
            using var image = new Image<L8>(_width, _height);
            
            image.ProcessPixelRows(accessor => {
                 for (int y = 0; y < _height; y++)
                 {
                     var row = accessor.GetRowSpan(y);
                     for (int x = 0; x < _width; x++)
                     {
                         // Convert 0.0/1.0 back to byte 0/255
						 row[x] = new L8((byte)((1.0f - item.Pixels[y * _width + x]) * 255));
                     }
                 }
            });

            string fileName = $"{Guid.NewGuid()}.png";
            image.SaveAsPng(Path.Combine(folder, fileName));
        }
    }

    /// <summary>
    /// Shuffles the list (Fisher-Yates algorithm).
    /// </summary>
    private void Shuffle<T>(List<T> list)
    {
        // Use the internal Random instance
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = _rng.Next(n + 1);
            (list[k], list[n]) = (list[n], list[k]);
        }
    }
}