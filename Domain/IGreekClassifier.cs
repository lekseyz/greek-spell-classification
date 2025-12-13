using Domain.Models;

namespace Domain;

public interface IGreekClassifier
{
	PredictionResult Predict(GreekSymbolImage image);

	void Train(List<(GreekSymbolImage image, GreekLetter label)> dataset);
	
	float Test(List<(GreekSymbolImage image, GreekLetter label)> dataset);
}