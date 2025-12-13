namespace Domain.Models;

public class NetworkState
{
	public NeuralNetworkConfig Config { get; set; }
	public List<float[]> Weights { get; set; }
	public List<float[]> Biases { get; set; }
}