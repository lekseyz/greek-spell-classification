namespace Domain;

/// <summary>
/// Константы, определяющие размерности изображений и сети.
/// </summary>
public static class GreekSymbolConstants
{
	public const int ImageWidth = 28;
	public const int ImageHeight = 28;

	public const int InputVectorSize = ImageWidth * ImageHeight;
	
	public const int MaxOutputClassCount = 24;
}