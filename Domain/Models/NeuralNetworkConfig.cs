namespace Domain.Models;

public class NeuralNetworkConfig
{
	/// <summary>
	/// The size of the input vector (number of pixels).
	/// For 28x28 images, this value should be 784.
	/// </summary>
	public int InputSize { get; set; } = 784;
	
	/// <summary>
	/// An array defining the number of neurons in each HIDDEN layer.
	/// The length of the array determines the number of hidden layers.
	/// </summary>
	public int[] HiddenLayerNeurons { get; set; } = [];

	/// <summary>
	/// The size of the training sample (the number of images used for training).
	/// </summary>
	public int TrainingSampleSize { get; set; } = 10;

	/// <summary>
	/// The number of training epochs.
	/// </summary>
	public int Epochs { get; set; } = 5;

	/// <summary>
	/// The number of output classes (outputs). For the Greek alphabet, this is 25.
	/// </summary>
	public int OutputClasses { get; set; } = 24;

	/// <summary>
	/// The acceptable error (Loss) threshold at which training can be stopped early.
	/// </summary>
	public float AcceptableError { get; set; } = 0.3f;

	/// <summary>
	/// The learning rate.
	/// Included as this is a critical parameter for any network configuration.
	/// </summary>
	public float LearningRate { get; set; } = 0.01f;
}