using Domain.Models;

namespace Domain;

public interface IGreekClassifier
{
	PredictionResult Predict(float[] pixelData);
}