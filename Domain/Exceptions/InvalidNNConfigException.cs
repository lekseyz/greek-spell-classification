namespace Domain.Exceptions;

public class InvalidNNConfigException : RecognitionException
{
	// Свойство, чтобы программно можно было узнать, какой параметр неверный
	public string? ParameterName { get; }

	// Конструктор для общих ошибок конфигурации
	public InvalidNNConfigException(string message) 
		: base(message) { }

	// Конструктор для конкретного поля (Best Practice)
	public InvalidNNConfigException(string paramName, string reason) 
		: base($"Invalid configuration for '{paramName}': {reason}") 
	{
		ParameterName = paramName;
	}

	// Оставляем ваш старый конструктор для ошибок загрузки файла, если он нужен
	public InvalidNNConfigException(string path, Exception innerException) 
		: base($"Failed to load model from path: '{path}'.", innerException) { }
}