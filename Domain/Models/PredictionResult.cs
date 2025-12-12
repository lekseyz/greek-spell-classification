namespace Domain.Models;

public class PredictionResult
{
	public			GreekLetter Symbol		{ get; set; }
	public			float		Confidence	{ get; set; }
	public required string		ModelUsed	{ get; set; }
}