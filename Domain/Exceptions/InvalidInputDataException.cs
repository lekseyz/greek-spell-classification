namespace Domain.Exceptions;

public class InvalidInputDataException : RecognitionException
{
	public InvalidInputDataException(int expected, int actual) 
		: base($"Invalid input vector size. Expected {expected} items, but received {actual}.") { }
}