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
		
		// Gray
		Cv2.CvtColor(mat, gray, ColorConversionCodes.BGR2GRAY);
		
		Cv2.GaussianBlur(gray, gray, new OpenCvSharp.Size(5, 5), 0);

		// Crop
		int size = Math.Min(gray.Width, gray.Height);
		int x = (gray.Width - size) / 2;
		int y = (gray.Height - size) / 2;
		using var cropped = new Mat(gray, new OpenCvSharp.Rect(x, y, size, size));
		cropped.SaveImage("cropped.png");
		
		// Binary Source
		using var binarySource = new Mat();
		//Cv2.Threshold(cropped, binarySource, 0, 255, ThresholdTypes.BinaryInv | ThresholdTypes.Otsu);
		Cv2.AdaptiveThreshold(cropped, binarySource, 255, 
			AdaptiveThresholdTypes.GaussianC, 
			ThresholdTypes.BinaryInv, 
			blockSize: 15, 
			c: 10);

		int kernelSize = Math.Max(3, size / 35);
		if (kernelSize % 2 == 0) 
			kernelSize++;
		using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(kernelSize, kernelSize));
		Cv2.Dilate(binarySource, binarySource, kernel);
		binarySource.SaveImage("binarySource.png");
		
		// Resize
		using var resized = new Mat();
		Cv2.Resize(binarySource, resized, new OpenCvSharp.Size(28, 28), 0 ,0);
		resized.SaveImage("resized.png");
		
		// Binarization
		using var binary = new Mat();
		Cv2.Threshold(resized, binary, 0, 255, ThresholdTypes.Binary);
		binary.SaveImage("binary.png");

		float[] pixels = new float[28 * 28];
		for (int r = 0; r < 28; r++)
		{
			for (int c = 0; c < 28; c++)
			{
				byte val = binary.At<byte>(r, c);
				pixels[r * 28 + c] = val / 255.0f;
			}
		}
		
		return new GreekSymbolImage(pixels);
	}
}