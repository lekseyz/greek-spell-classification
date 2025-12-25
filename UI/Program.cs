using Avalonia;
using System;
using Avalonia.ReactiveUI;
using DataProcessing;
using Domain;
using Domain.Models;
using Ml;

namespace UI;

// TODO: Maybe cool DI
public static class ServiceLocator
{
	public static IDatasetGenerator		DatasetGenerator	{ get; private set; } = new DatasetProcessor();
	public static IPhotoProcessor		PhotoProcessor		{ get; private set; } = new PhotoProcessor();
	private static	IGreekClassifier	_currentGreekClassifier;
	
	public static IGreekClassifier GetClassifier(NeuralNetworkConfig config, int modelIndex)
	{
		// Перезаписываем текущий экземпляр сети, чтобы в дальнейшем 
		// при вызове GreekClassifier всегда возвращалась последняя обученная/сконфигурированная сеть.
		if (modelIndex == 0)
			_currentGreekClassifier = new CustomNeuralNetwork(config);
		else
			_currentGreekClassifier = new MlNetGreekClassifier();
		return _currentGreekClassifier;
	}
}

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
	{
		BuildAvaloniaApp()
			.StartWithClassicDesktopLifetime(args);
	}

	// Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();
}
