# pyserial_varioptics_USB-M

UART protocol control using USB-M board for Varioptic liquid lens tuning on a USB-M Flexiboard with a MAX14574 driver.

The repository now includes both the original Python scripts and a cross-platform .NET 8 class library that exposes a
strongly typed API for C# applications.

## Contributor

* Md Redwan Islam – initial work
* Paul Tsai

## Future work

* Direct implementation to MAX14574 board through the I2C protocol
* Debug and resolve when the command value sent does not reflect linearly to the actual value set

## .NET 8 `VariopticLens` class library

The `src/VariopticLens` project targets .NET 8 and provides a reusable abstraction over the serial commands needed to
configure the lens. The library includes XML documentation comments, MVVM-friendly notifications, and a
`LensManager` helper that can track up to four lens instances.

### Prerequisites

Install the [.NET 8 SDK](https://dotnet.microsoft.com/download) to build or reference the project.

### Building the library

```bash
dotnet build src/VariopticLens/VariopticLens.csproj
```

### Using the library

```csharp
using System;
using VariopticLens;

var manager = new LensManager();
using var lens = new VariopticLens.VariopticLens("COM3");
manager.AddLens("Main", lens);

lens.Initialize(analog: false, standby: false);
lens.EnableSave(enable: true);
lens.SetFocusValue(250);
ushort current = lens.GetFocusValue();

Console.WriteLine($"Current focus: {current}");
```

The `VariopticLens` class raises `PropertyChanged` events when the focus value changes, making it straightforward to
bind in MVVM applications.

### Demo project

A lightweight console demonstration that mimics an MVVM setup is available under `samples/VariopticLens.Demo`.

Run the demo by providing the serial port via the `VARIOPTIC_DEMO_PORT` environment variable:

```bash
# Linux / macOS
export VARIOPTIC_DEMO_PORT=/dev/ttyACM0

# Windows (Command Prompt)
set VARIOPTIC_DEMO_PORT=COM3

# Windows (PowerShell)
$Env:VARIOPTIC_DEMO_PORT = "COM3"

dotnet run --project samples/VariopticLens.Demo/VariopticLens.Demo.csproj
```

The program will initialize the lens, query the current focus, and apply a sample update while reporting focus changes
through the view model.
