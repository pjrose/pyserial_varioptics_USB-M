using VariopticLens;
using VariopticLens.Demo;

namespace VariopticLens.Demo;

/// <summary>
/// Entry point for the console demonstration which illustrates how the class library can be consumed from
/// an MVVM view model.
/// </summary>
public static class Program
{
    public static void Main()
    {
        Console.WriteLine("Varioptic Lens .NET demo\n===========================");
        Console.WriteLine("This sample wires the VariopticLens library into a minimal MVVM style view model.");
        Console.WriteLine("Set the VARIOPTIC_DEMO_PORT environment variable to the serial port that hosts your lens to run the live demo.\n");

        var portName = Environment.GetEnvironmentVariable("VARIOPTIC_DEMO_PORT");
        if (string.IsNullOrWhiteSpace(portName))
        {
            Console.WriteLine("No serial port provided. The program will exit after showing the intended flow.\n");
            Console.WriteLine("Example usage:");
            Console.WriteLine("  export VARIOPTIC_DEMO_PORT=/dev/ttyACM0   # Linux / macOS");
            Console.WriteLine("  set VARIOPTIC_DEMO_PORT=COM3             # Windows (Command Prompt)");
            Console.WriteLine("  $Env:VARIOPTIC_DEMO_PORT = 'COM3'        # Windows (PowerShell)");
            Console.WriteLine("  dotnet run --project samples/VariopticLens.Demo/VariopticLens.Demo.csproj\n");
            return;
        }

        var manager = new LensManager();
        var lens = new VariopticLens.VariopticLens(portName);
        manager.AddLens("Primary", lens);
        var viewModel = new LensViewModel(manager, "Primary");

        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(LensViewModel.CurrentFocus))
            {
                Console.WriteLine($"Focus updated: {viewModel.CurrentFocus}");
            }
        };

        try
        {
            Console.WriteLine("Initializing lens (analog=false, standby=false)...");
            viewModel.InitializeLens(analog: false, standby: false);

            Console.WriteLine("Requesting focus value from the device...");
            viewModel.RefreshFocus();

            Console.WriteLine("Updating focus to 250...");
            viewModel.SetFocus(250);

            Console.WriteLine("Demo completed. Press ENTER to close the serial connection.");
            Console.ReadLine();
        }
        catch (Exception ex)
        {
            Console.WriteLine("An error occurred while communicating with the lens:");
            Console.WriteLine(ex.Message);
        }
        finally
        {
            manager.RemoveLens("Primary", dispose: true);
        }
    }
}
