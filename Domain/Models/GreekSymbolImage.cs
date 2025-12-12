using Domain.Exceptions;

namespace Domain.Models;

public class GreekSymbolImage
{
	public float[] Pixels { get; }
	public int Width { get; } = 28;
	public int Height { get; } = 28;

	public GreekSymbolImage(float[] pixels)
	{
		if (pixels == null) 
			throw new ArgumentNullException(nameof(pixels), "Pixel data cannot be null.");
            
		if (pixels.Length != Width * Height)
			throw new InvalidInputDataException(Width * Height, pixels.Length);

		Pixels = pixels;
	}
}