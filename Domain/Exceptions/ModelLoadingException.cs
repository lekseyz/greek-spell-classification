namespace Domain.Exceptions;

public class ModelLoadingException : RecognitionException
{
	public ModelLoadingException(string path) 
		: base($"Failed to load model from path: '{path}'.") { }

	public ModelLoadingException(string path, Exception innerException) 
		: base($"Failed to load model from path: '{path}'. See inner exception for details.", innerException) { }
}