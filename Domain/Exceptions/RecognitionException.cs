namespace Domain.Exceptions;

public abstract class RecognitionException : Exception
{
	protected RecognitionException(string message) : base(message) { }
        
	protected RecognitionException(string message, Exception innerException) : base(message, innerException) { }
}