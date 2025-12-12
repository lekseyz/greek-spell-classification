using Domain.Exceptions;

namespace Domain.Models;

public class NeuralNetworkConfig
{
	/// <summary>
	/// The size of the input vector (number of pixels).
	/// For 28x28 images, this value should be 784.
	/// </summary>
	public int InputSize { get; set; }
	
	/// <summary>
	/// An array defining the number of neurons in each HIDDEN layer.
	/// The length of the array determines the number of hidden layers.
	/// </summary>
	public int[] HiddenLayerNeurons { get; set; } = [];

	/// <summary>
	/// The size of the training sample (the number of images used for training).
	/// </summary>
	public int TrainingSampleSize { get; set; }

	/// <summary>
	/// The number of training epochs.
	/// </summary>
	public int Epochs { get; set; }

	/// <summary>
	/// The number of output classes (outputs). For the Greek alphabet, this is 25.
	/// </summary>
	public int OutputClasses { get; set; }

	/// <summary>
	/// The acceptable error (Loss) threshold at which training can be stopped early.
	/// </summary>
	public float AcceptableError { get; set; }

	/// <summary>
	/// The learning rate.
	/// Included as this is a critical parameter for any network configuration.
	/// </summary>
	public float LearningRate { get; set; } = 0.01f;
	
	
	
	/// <summary>
    /// Validates the configuration state.
    /// </summary>
    /// <exception cref="InvalidNNConfigException">Thrown if any parameter is invalid.</exception>
    public void Validate()
    {
        if (InputSize <= 0)
        {
            throw new InvalidNNConfigException(
                nameof(InputSize), 
                "Input size must be greater than 0.");
        }

        if (OutputClasses <= 0)
        {
            throw new InvalidNNConfigException(
                nameof(OutputClasses), 
                "Number of output classes must be greater than 0.");
        }

        if (HiddenLayerNeurons == null)
        {
            throw new InvalidNNConfigException(
                nameof(HiddenLayerNeurons), 
                "Hidden layers array cannot be null.");
        }

        for (int i = 0; i < HiddenLayerNeurons.Length; i++)
        {
            if (HiddenLayerNeurons[i] <= 0)
            {
                throw new InvalidNNConfigException(
                    nameof(HiddenLayerNeurons), 
                    $"Hidden layer at index {i} has invalid neuron count ({HiddenLayerNeurons[i]}). Must be > 0.");
            }
        }

        if (LearningRate <= 0.0f || LearningRate > 1.0f)
        {
            throw new InvalidNNConfigException(
                nameof(LearningRate), 
                $"Value {LearningRate} is invalid. It must be between 0 (exclusive) and 1.");
        }

        if (Epochs <= 0)
        {
            throw new InvalidNNConfigException(
                nameof(Epochs), 
                "Epoch count must be greater than 0.");
        }

        if (TrainingSampleSize < 0)
        {
            throw new InvalidNNConfigException(
                nameof(TrainingSampleSize), 
                "Training sample size cannot be negative.");
        }

        if (AcceptableError < 0.0f)
        {
            throw new InvalidNNConfigException(
                nameof(AcceptableError), 
                "Acceptable error threshold cannot be negative.");
        }
    }
}