namespace Domain;

public interface IDatasetGenerator
{
	/// <summary>
	/// Generates the processed, augmented, and split dataset and exports it to the specified directory.
	/// </summary>
	/// <param name="rawDatasetPath">The path to the raw dataset folder (e.g., /dataset).</param>
	/// <param name="outputPath">The path where the processed and split data will be saved.</param>
	void ProcessAndExport(string rawDatasetPath, string outputPath);
}