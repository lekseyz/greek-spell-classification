using System.Collections.ObjectModel;

namespace UI.DTOs;

public struct NeuralNetworkConfigDto
{
    public int InputSize = 784;
    public ObservableCollection<int> HiddenLayerNeurons = new();
    public int TrainingSampleSize = 10;
    public int Epochs = 5;
    public int OutputClasses = 24;
    public float AcceptableError = 0.3f;
    public float LearningRate = 0.01f;
    
    public NeuralNetworkConfigDto() {}
}