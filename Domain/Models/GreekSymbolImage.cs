using Domain.Exceptions;

namespace Domain.Models;

public class GreekSymbolImage
{
	public float[] Pixels { get; }
	
	public int Width { get; } = GreekSymbolConstants.ImageWidth;
	public int Height { get; } = GreekSymbolConstants.ImageHeight;

	public GreekSymbolImage(float[] pixels)
	{
		if (pixels == null) 
			throw new ArgumentNullException(nameof(pixels), "Pixel data cannot be null.");
            
		if (pixels.Length != GreekSymbolConstants.InputVectorSize)
			throw new InvalidInputDataException(GreekSymbolConstants.InputVectorSize, pixels.Length);

		Pixels = pixels;
	}
}