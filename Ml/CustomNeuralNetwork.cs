using System.Text.Json;
using Domain;
using Domain.Exceptions;
using Domain.Models;

namespace Ml;

/// <summary>
/// A custom implementation of a Neural Network for classifying Greek letters.
/// It uses a fully connected architecture (Feed Forward) with Sigmoid activation for hidden layers
/// and Softmax for the output layer.
/// </summary>
public class CustomNeuralNetwork : IGreekClassifier
{
	public NeuralNetworkConfig Config { get; private set; }
	
    private readonly List<float[]> _weights = new();
    private readonly List<float[]> _biases = new();
    private readonly List<int> _layerSizes = new();
	private readonly float _learningRate;
	private readonly int _epochs;
	private readonly float _acceptableError;

    private const float BinarizationThreshold = 0.5f;

	// --------------------------------------------------- Initialization ---------------------------------------------------
	#region Initialization
	
    /// <summary>
    /// Initializes a new instance of the <see cref="CustomNeuralNetwork"/> class based on the provided configuration.
    /// </summary>
    /// <param name="config">The configuration object defining the topology (input size, hidden layers, output classes).</param>
    /// <exception cref="ArgumentException">Thrown when InputSize or OutputClasses are invalid (less than or equal to 0).</exception>
	public CustomNeuralNetwork(NeuralNetworkConfig config)
	{
		if (config == null) 
			throw new ArgumentNullException(nameof(config));
		config.Validate();
		
		Config = config;

		_learningRate = config.LearningRate;
		_epochs = config.Epochs;
		_acceptableError = config.AcceptableError;

		// 1. Define topology: [Input -> Hidden... -> Output]
		_layerSizes.Add(config.InputSize); 
		_layerSizes.AddRange(config.HiddenLayerNeurons);
		_layerSizes.Add(config.OutputClasses);

		// 2. Initialize weights (Xavier/Glorot initialization)
		InitializeWeights();
	}
	
	/// <summary>
	/// Initializes the weights and biases for all layers in the neural network.
	/// Uses Xavier (Glorot) initialization to set weight values within a range that prevents 
	/// gradients from vanishing or exploding during training.
	/// </summary>
	private void InitializeWeights()
	{
		Random rnd = new Random(123); // Fixed seed for reproducibility

		for (int i = 0; i < _layerSizes.Count - 1; i++)
		{
			int inputSize = _layerSizes[i];
			int outputSize = _layerSizes[i + 1];

			float[] w = new float[inputSize * outputSize];
			float[] b = new float[outputSize];

			// Xavier/Glorot Initialization limit
			float limit = (float)Math.Sqrt(6.0 / (inputSize + outputSize));

			for (int j = 0; j < w.Length; j++)
			{
				w[j] = (float)(rnd.NextDouble() * 2 * limit - limit);
			}
            
			_weights.Add(w);
			_biases.Add(b);
		}
	}
	#endregion

	/// <summary>
	/// Predicts the Greek letter based on the input image.
	/// </summary>
	/// <param name="image">The object containing the normalized pixel data of the symbol.</param>
	/// <returns>A <see cref="PredictionResult"/> containing the predicted symbol and confidence level.</returns>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="image"/> is null.</exception>
	/// <exception cref="InvalidInputDataException">Thrown if the size of the image data does not match the configured network input size.</exception>
	public PredictionResult Predict(GreekSymbolImage image)
	{
		if (image == null) throw new ArgumentNullException(nameof(image));
    
		// Достаем пиксели из объекта
		float[] pixelData = image.Pixels;

		// Проверка совместимости размера сети и картинки
		if (pixelData.Length != _layerSizes[0]) 
			throw new InvalidInputDataException(_layerSizes[0], pixelData.Length);

		// 1. Preprocessing
		float[] currentSignal = pixelData;

		// 2. Forward Pass
		for (int i = 0; i < _weights.Count; i++)
		{
			int inputSize = _layerSizes[i];
			int outputSize = _layerSizes[i + 1];

			float[] nextSignal = MatrixVectorMultiplyParallel(currentSignal, _weights[i], _biases[i], inputSize, outputSize);

			if (i == _weights.Count - 1)
				currentSignal = Softmax(nextSignal);
			else
				currentSignal = ApplyActivation(nextSignal, Tanh);
		}

		// 3. Result
		int maxIndex = ArgMax(currentSignal);
    
		return new PredictionResult
		{
			Symbol = (GreekLetter)(maxIndex + 1),
			Confidence = currentSignal[maxIndex],
			ModelUsed = $"Custom DNN ({_weights.Count} layers)"
		};
	}
	
	/// <summary>
	/// Tests the neural network using the provided dataset and returns the accuracy (percentage of correct predictions).
	/// </summary>
	/// <param name="dataset">A list of testing pairs.</param>
	/// <returns>The accuracy of the model as a float (0.0 to 1.0).</returns>
	public float Test(List<(GreekSymbolImage image, GreekLetter label)> dataset)
	{
		if (dataset == null || dataset.Count == 0) return 0.0f;

		int correctCount = 0;
		int totalCount = dataset.Count;

		// Использование существующего метода Predict для каждого образца
		foreach (var sample in dataset)
		{
			// Predict уже выполняет предобработку (BinarizeInput)
			PredictionResult prediction = Predict(sample.image);
            
			// Проверка совпадения предсказанного символа с истинной меткой
			if (prediction.Symbol == sample.label)
			{
				correctCount++;
			}
		}

		// Расчет точности (правильные / общее)
		return (float)correctCount / totalCount;
	}
	
	private void Shuffle(List<(GreekSymbolImage image, GreekLetter label)> list)
	{
		Random rng = new Random(); // Можно вынести в поле класса
		int n = list.Count;
		while (n > 1)
		{
			n--;
			int k = rng.Next(n + 1);
			(list[k], list[n]) = (list[n], list[k]);
		}
	}

	// --------------------------------------------------- TRAINING ---------------------------------------------------
	#region Training

	/// <summary>
	/// Trains the neural network using the provided dataset.
	/// </summary>
	/// <remarks>
	/// This method uses Stochastic Gradient Descent (SGD) with Backpropagation.
	/// </remarks>
	/// <param name="dataset">A list of training pairs, where each pair consists of a <see cref="GreekSymbolImage"/> and its expected <see cref="GreekLetter"/> label.</param>
	public void Train(List<(GreekSymbolImage image, GreekLetter label)> dataset)
	{
		if (dataset == null || dataset.Count == 0) return;

		int inputSize = _layerSizes[0];

		for (int epoch = 0; epoch < _epochs; epoch++)
		{
			Shuffle(dataset);
			float totalError = 0;

			foreach (var sample in dataset)
			{
				// Извлекаем пиксели через sample.image.Pixels
				if (sample.image.Pixels.Length != inputSize) continue;

				// 1. Prepare Input (Binarize) and Target
				float[] input = sample.image.Pixels;
				float[] target = OneHotEncode(sample.label, _layerSizes.Last());

				// 2. Forward Pass with Caching
				List<float[]> layerActivations = FeedForwardWithCache(input);

				// 3. Calculate Loss and Backpropagate
				float[] output = layerActivations.Last();
				totalError += CalculateCrossEntropyLoss(output, target);

				Backpropagate(layerActivations, target);
			}

			// ... остальной код логгирования ошибки без изменений ...
			float meanError = totalError / dataset.Count;
			Console.WriteLine($"Epoch {epoch + 1}/{_epochs}, Loss: {meanError:F4}");

			if (meanError < _acceptableError)
			{
				Console.WriteLine($"Target accuracy reached. Stopping early at epoch {epoch + 1}.");
				break;
			}
		}
	}

    private List<float[]> FeedForwardWithCache(float[] input)
    {
        var activations = new List<float[]> { input };
        float[] currentSignal = input;

        for (int i = 0; i < _weights.Count; i++)
        {
            int layerInputSize = _layerSizes[i];
            int layerOutputSize = _layerSizes[i + 1];

            // W * x + b
            float[] weightedSum = MatrixVectorMultiplyParallel(currentSignal, _weights[i], _biases[i], layerInputSize, layerOutputSize);

            // Activation
            float[] activatedSignal;
            if (i == _weights.Count - 1)
                activatedSignal = Softmax(weightedSum); // Output
            else
                activatedSignal = ApplyActivation(weightedSum, Tanh); // Hidden

            activations.Add(activatedSignal);
            currentSignal = activatedSignal;
        }

        return activations;
    }

    private void Backpropagate(List<float[]> activations, float[] target)
    {
        // activations[0] = input
        // activations[1] = hidden1 ...
        // activations[last] = output

        int layersCount = _weights.Count; // number of weight matrices
        float[] error = new float[_layerSizes.Last()];

        // 1. Calculate Output Error
        // For Softmax + CrossEntropy, the derivative is simply (Output - Target)
        float[] outputLayer = activations.Last();
        for (int i = 0; i < error.Length; i++)
        {
            error[i] = outputLayer[i] - target[i];
        }

        // 2. Iterate backwards from the last layer to the first
        for (int i = layersCount - 1; i >= 0; i--)
        {
            float[] currentWeights = _weights[i];
            float[] currentBiases = _biases[i];
            float[] prevLayerActivation = activations[i]; // Input to this layer
            
            int inputSize = _layerSizes[i];
            int outputSize = _layerSizes[i + 1];

            // We need to calculate the error for the *previous* layer (to be used in the next iteration)
            // BEFORE we update the weights of the *current* layer.
            float[] prevError = new float[inputSize];
            if (i > 0) // No need to calc error for input layer
            {
                // Backpropagate error: prevError = (Weights * error) * Derivative(prevActivation)
                // Parallelizing error propagation
                Parallel.For(0, inputSize, row =>
                {
                    float sum = 0;
                    for (int col = 0; col < outputSize; col++)
                    {
                        // Transposed multiplication effectively
                        sum += currentWeights[row * outputSize + col] * error[col];
                    }
                    // Multiply by derivative of Sigmoid: f'(x) = f(x) * (1 - f(x))
                    // prevLayerActivation[row] holds the sigmoid output
                    float val = prevLayerActivation[row];
                    //prevError[row] = sum * (val * (1.0f - val));
					prevError[row] = sum * (1.0f - val * val);
                });
            }

            // 3. Update Weights and Biases (Gradient Descent)
            // Weight_new = Weight_old - LearningRate * Gradient
            // Gradient_W = prevActivation^T * error
            // Gradient_b = error

            // Parallelizing weight updates
            Parallel.For(0, inputSize, row =>
            {
                for (int col = 0; col < outputSize; col++)
                {
                    float gradient = prevLayerActivation[row] * error[col];
                    currentWeights[row * outputSize + col] -= _learningRate * gradient;
                }
            });

            // Update Biases
            for (int col = 0; col < outputSize; col++)
            {
                currentBiases[col] -= _learningRate * error[col];
            }

            // Move error backward
            error = prevError;
        }
    }

	#endregion

	// --------------------------------------------------- SAVE/LOAD ---------------------------------------------------
	#region SaveLoad

	/// <summary>
	/// Сохраняет текущее состояние сети в файл JSON.
	/// </summary>
	public void Save(string filePath)
	{
		var state = new NetworkState
		{
			Config = this.Config,
			Weights = this._weights,
			Biases = this._biases
		};

		var options = new JsonSerializerOptions { WriteIndented = true };
		string json = JsonSerializer.Serialize(state, options);
		File.WriteAllText(filePath, json);
	}

	/// <summary>
	/// Загружает сеть из файла JSON.
	/// </summary>
	public static CustomNeuralNetwork Load(string filePath)
	{
		if (!File.Exists(filePath)) 
			throw new FileNotFoundException("Файл модели не найден", filePath);

		string json = File.ReadAllText(filePath);
		var state = JsonSerializer.Deserialize<NetworkState>(json);

		if (state == null || state.Config == null)
			throw new Exception("Ошибка десериализации файла модели.");

		// Создаем новую сеть на основе загруженного конфига
		var network = new CustomNeuralNetwork(state.Config);

		// Принудительно устанавливаем загруженные веса и смещения
		// (очищаем те, что создались рандомно в конструкторе)
		network._weights.Clear();
		network._weights.AddRange(state.Weights);

		network._biases.Clear();
		network._biases.AddRange(state.Biases);

		return network;
	}

	#endregion

	// --------------------------------------------------- MATH ---------------------------------------------------
	#region Math

    /// <summary>
    /// Performs matrix-vector multiplication using parallel processing.
    /// </summary>
    private float[] MatrixVectorMultiplyParallel(float[] inputVector, float[] weightsMatrix, float[] biases, int rows, int cols)
    {
        float[] result = new float[cols];

        // The main computational load is here. Parallel.For is effective.
        Parallel.For(0, cols, j =>
        {
            float sum = 0;
            // Linear memory access is cache-friendly if the matrix is stored row-major.
            // However, since we iterate `j` (column) in the outer loop and `i` (row) in the inner loop,
            // we are jumping through memory (weightsMatrix[i * cols + j]).
            // For standard fully connected layers this is acceptable, but for very large matrices, 
            // transposing the weight matrix upon initialization would accelerate this operation.
            // Kept as-is for readability.
            for (int i = 0; i < rows; i++)
            {
                sum += inputVector[i] * weightsMatrix[i * cols + j];
            }
            result[j] = sum + biases[j];
        });

        return result;
    }
	
	private float[] OneHotEncode(GreekLetter label, int size)
	{
		float[] target = new float[size];
		// Сдвигаем: Alpha(1)->0 ... Omega(24)->23
		int index = (int)label - 1; 

		if (index >= 0 && index < size)
		{
			target[index] = 1.0f;
		}
		return target;
	}

	private float CalculateCrossEntropyLoss(float[] predicted, float[] target)
	{
		float sum = 0;
		// Small epsilon to avoid log(0)
		float epsilon = 1e-15f; 
		for (int i = 0; i < predicted.Length; i++)
		{
			// Only the correct class contributes to Loss in One-Hot
			if (target[i] > 0.5f) 
			{
				float val = Math.Max(predicted[i], epsilon);
				sum -= (float)Math.Log(val);
			}
		}
		return sum;
	}

	/// <summary>
	/// Computes the sigmoid activation function for a single scalar value.
	/// Formula: f(x) = 1 / (1 + e^(-x)).
	/// </summary>
	/// <param name="x">The input value.</param>
	/// <returns>A value between 0.0 and 1.0.</returns>
    private float Sigmoid(float x) => 1.0f / (1.0f + (float)Math.Exp(-x));
	
	/// <summary>
	/// Computes the Hyperbolic Tangent (Tanh) activation function for a single scalar value.
	/// Formula: f(x) = tanh(x). Output range is [-1.0, 1.0].
	/// </summary>
	private float Tanh(float x) => (float)Math.Tanh(x);

	/// <summary>
	/// Applies a specified activation function to every element of the input vector in parallel.
	/// </summary>
	/// <param name="vector">The input vector (e.g., the result of a matrix multiplication).</param>
	/// <param name="activationFunc">The activation function delegate (e.g., Sigmoid).</param>
	/// <returns>A new vector containing the activated values.</returns>
    private float[] ApplyActivation(float[] vector, Func<float, float> activationFunc)
    {
        float[] result = new float[vector.Length];
        Parallel.For(0, vector.Length, i => result[i] = activationFunc(vector[i]));
        return result;
    }

	/// <summary>
	/// Computes the Softmax function for the input vector, converting raw scores (logits) into probabilities.
	/// Subtracts the maximum value from elements before exponentiation to improve numerical stability.
	/// </summary>
	/// <param name="vector">The input vector of logits (raw predictions).</param>
	/// <returns>A probability distribution vector where all elements sum to 1.0.</returns>
    private float[] Softmax(float[] vector)
    {
        float max = vector.Max();
        float[] exps = new float[vector.Length];
        float sum = 0;

        // Parallelization is risky here due to summation (race condition),
        // and the data volume (e.g., 25 classes) is too small to justify complex locking.
        for (int i = 0; i < vector.Length; i++)
        {
            exps[i] = (float)Math.Exp(vector[i] - max);
            sum += exps[i];
        }

        for (int i = 0; i < vector.Length; i++)
        {
            exps[i] /= sum;
        }
        return exps;
    }

	/// <summary>
	/// Finds the index of the element with the maximum value in the array.
	/// </summary>
	/// <param name="vector">The array of float values (typically probabilities).</param>
	/// <returns>The zero-based index of the maximum value.</returns>
    private int ArgMax(float[] vector)
    {
        int maxIndex = 0;
        float maxValue = vector[0];
        for (int i = 1; i < vector.Length; i++)
        {
            if (vector[i] > maxValue)
            {
                maxValue = vector[i];
                maxIndex = i;
            }
        }
        return maxIndex;
    }
	
	#endregion

	
}