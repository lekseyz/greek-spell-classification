using Avalonia;
using System;
using Avalonia.ReactiveUI;
using DataProcessing;
using Domain;

namespace UI;

// TODO: Maybe cool DI
public static class ServiceLocator
{
	public static IDatasetGenerator DatasetGenerator { get; private set; }

	public static void Initialize()
	{
		// Concrete realization of the dependency
		DatasetGenerator = new DatasetProcessor();
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
		ServiceLocator.Initialize();
		
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
