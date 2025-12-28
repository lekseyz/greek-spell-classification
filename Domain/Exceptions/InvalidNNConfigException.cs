namespace Domain.Exceptions;

public class InvalidNNConfigException : RecognitionException
{
	public string? ParameterName { get; }

	public InvalidNNConfigException(string message) 
		: base(message) { }

	public InvalidNNConfigException(string paramName, string reason) 
		: base($"Invalid configuration for '{paramName}': {reason}") 
	{
		ParameterName = paramName;
	}

	public InvalidNNConfigException(string path, Exception innerException) 
		: base($"Failed to load model from path: '{path}'.", innerException) { }
}