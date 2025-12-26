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

		// Crop
		int size = Math.Min(gray.Width, gray.Height);
		int x = (gray.Width - size) / 2;
		int y = (gray.Height - size) / 2;
		using var cropped = new Mat(gray, new OpenCvSharp.Rect(x, y, size, size));

		// Resize
		using var resized = new Mat();
		Cv2.Resize(cropped, resized, new OpenCvSharp.Size(28, 28));

		// Binarization
		using var binary = new Mat();
		Cv2.Threshold(resized, binary, 0, 255, ThresholdTypes.BinaryInv | ThresholdTypes.Otsu);

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