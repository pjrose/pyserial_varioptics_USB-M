using System.ComponentModel;
using System.IO.Ports;
using System.Runtime.CompilerServices;

namespace VariopticLens;

/// <summary>
/// Provides a .NET friendly API for communicating with a Varioptic USB-M liquid lens driver.
/// </summary>
/// <remarks>
/// <para>
/// The device expects framed commands where the CRC is calculated by summing all bytes and keeping the
/// least significant 8 bits. The class exposes high level helpers that perform the framing and validation
/// for common operations such as initialization and focus management.
/// </para>
/// <para>
/// Instances can be consumed directly or through an MVVM friendly wrapper such as a <c>ViewModel</c>.
/// The <see cref="CurrentFocus"/> property raises <see cref="INotifyPropertyChanged.PropertyChanged"/>
/// events which allows bindings to react to focus changes.
/// </para>
/// </remarks>
public class VariopticLens : INotifyPropertyChanged, IDisposable
{
    private readonly SerialPort _serialPort;
    private bool _isInitialized;
    private ushort _currentFocus;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="VariopticLens"/> class.
    /// </summary>
    /// <param name="portName">The name of the serial port (for example <c>COM3</c> on Windows or <c>/dev/ttyACM0</c> on Linux).</param>
    /// <param name="baudRate">The baud rate to use. Defaults to <c>9600</c> which matches the device specification.</param>
    /// <param name="parity">The parity bits to use for the serial connection.</param>
    /// <param name="dataBits">The number of data bits to use for the serial connection.</param>
    /// <param name="stopBits">The number of stop bits to use for the serial connection.</param>
    public VariopticLens(string portName, int baudRate = 9600, Parity parity = Parity.None, int dataBits = 8, StopBits stopBits = StopBits.One)
    {
        _serialPort = new SerialPort(portName, baudRate, parity, dataBits, stopBits)
        {
            ReadTimeout = 500,
            WriteTimeout = 500
        };
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Gets the friendly name of the serial port associated with this lens.
    /// </summary>
    public string PortName => _serialPort.PortName;

    /// <summary>
    /// Gets a value indicating whether the underlying <see cref="SerialPort"/> is currently open.
    /// </summary>
    public bool IsOpen => _serialPort.IsOpen;

    /// <summary>
    /// Gets a value indicating whether the device has been initialized during this session.
    /// </summary>
    public bool IsInitialized => _isInitialized;

    /// <summary>
    /// Gets the most recently reported focus value from the device.
    /// </summary>
    public ushort CurrentFocus
    {
        get => _currentFocus;
        private set
        {
            if (_currentFocus != value)
            {
                _currentFocus = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Initializes the lens with the provided analog and standby configuration.
    /// </summary>
    /// <param name="analog">If set to <see langword="true"/>, the driver is put into analog mode.</param>
    /// <param name="standby">If set to <see langword="true"/>, the driver is put into standby mode.</param>
    /// <exception cref="InvalidOperationException">Thrown when initialization fails or the device returns an unexpected acknowledgement.</exception>
    public void Initialize(bool analog, bool standby)
    {
        EnsureNotDisposed();
        EnsurePortOpen();

        byte data = analog switch
        {
            true when standby => 0x03,
            true => 0x02,
            false when standby => 0x01,
            _ => 0x00
        };

        Span<byte> command = stackalloc byte[6];
        command[0] = 0x02; // STX
        command[1] = 0x37; // CDE
        command[2] = 0x03; // ADD
        command[3] = 0x01; // N_DATA
        command[4] = data; // DATA
        command[5] = CalcCrc(command[..5]);

        WriteCommand(command);
        var acknowledgement = ReadResponse(4);
        ValidateAcknowledgement(acknowledgement, "initialization");
        _isInitialized = true;
    }

    /// <summary>
    /// Enables or disables the persistent saving of the current configuration on the device.
    /// </summary>
    /// <param name="enable">Set to <see langword="true"/> to enable saving or <see langword="false"/> to disable it.</param>
    /// <exception cref="InvalidOperationException">Thrown when the operation fails or an unexpected acknowledgement is received.</exception>
    public void EnableSave(bool enable)
    {
        EnsureInitialized();

        Span<byte> command = stackalloc byte[6];
        command[0] = 0x02; // STX
        command[1] = 0x37; // CDE
        command[2] = 0x02; // ADD
        command[3] = 0x01; // N_DATA
        command[4] = enable ? (byte)0x01 : (byte)0x00;
        command[5] = CalcCrc(command[..5]);

        WriteCommand(command);
        var acknowledgement = ReadResponse(4);
        ValidateAcknowledgement(acknowledgement, "save enable");
    }

    /// <summary>
    /// Sends a new focus value to the driver.
    /// </summary>
    /// <param name="value">The desired focus value. Accepts the range <c>0</c> - <c>65535</c>.</param>
    /// <returns>The acknowledgement returned by the device.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="value"/> lies outside the allowed range.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the device refuses the update or answers with an unexpected acknowledgement.</exception>
    public byte[] SetFocusValue(ushort value)
    {
        EnsureInitialized();

        byte lsb = (byte)(value & 0xFF);
        byte msb = (byte)((value >> 8) & 0xFF);

        Span<byte> command = stackalloc byte[7];
        command[0] = 0x02; // STX
        command[1] = 0x37; // CDE
        command[2] = 0x00; // ADD_LSB
        command[3] = 0x02; // N_DATA
        command[4] = lsb;  // DATA_LSB
        command[5] = msb;  // DATA_MSB
        command[6] = CalcCrc(command[..6]);

        WriteCommand(command);
        var acknowledgement = ReadResponse(4);
        ValidateAcknowledgement(acknowledgement, "focus update");
        CurrentFocus = value;
        return acknowledgement;
    }

    /// <summary>
    /// Reads the current focus value from the device.
    /// </summary>
    /// <returns>The 16-bit focus value reported by the device.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the response is malformed or shorter than expected.</exception>
    public ushort GetFocusValue()
    {
        EnsureInitialized();

        Span<byte> command = stackalloc byte[5];
        command[0] = 0x02; // STX
        command[1] = 0x38; // CDE (read command)
        command[2] = 0x00; // ADD_LSB
        command[3] = 0x02; // N_DATA
        command[4] = CalcCrc(command[..4]);

        WriteCommand(command);
        var response = ReadResponse(7);

        if (response.Length < 6)
        {
            throw new InvalidOperationException("Unexpected response length while reading focus value.");
        }

        byte lsb = response[4];
        byte msb = response[5];
        ushort focus = (ushort)((msb << 8) | lsb);
        CurrentFocus = focus;
        return focus;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_serialPort.IsOpen)
        {
            try
            {
                _serialPort.Close();
            }
            catch
            {
                // Ignore exceptions during disposal to avoid masking more relevant errors.
            }
        }

        _serialPort.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Calculates the CRC byte by summing the provided bytes and keeping the least significant eight bits.
    /// </summary>
    /// <param name="bytes">The data to include in the CRC calculation.</param>
    /// <returns>The calculated CRC.</returns>
    private static byte CalcCrc(ReadOnlySpan<byte> bytes)
    {
        int sum = 0;
        foreach (var value in bytes)
        {
            sum += value;
        }

        return (byte)(sum & 0xFF);
    }

    private void EnsureInitialized()
    {
        EnsureNotDisposed();
        if (!_isInitialized)
        {
            throw new InvalidOperationException("The lens must be initialized before performing this operation.");
        }

        EnsurePortOpen();
    }

    private void EnsurePortOpen()
    {
        if (!_serialPort.IsOpen)
        {
            _serialPort.Open();
        }
    }

    private void EnsureNotDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(VariopticLens));
        }
    }

    private byte[] ReadResponse(int length)
    {
        var buffer = new byte[length];
        int total = 0;

        while (total < length)
        {
            int read = _serialPort.Read(buffer, total, length - total);
            if (read <= 0)
            {
                throw new TimeoutException("The device did not answer within the configured timeout.");
            }

            total += read;
        }

        if (total < length)
        {
            Array.Resize(ref buffer, total);
        }

        return buffer;
    }

    private void ValidateAcknowledgement(byte[] acknowledgement, string operationName)
    {
        if (acknowledgement.Length < 3 || acknowledgement[2] != 0x06)
        {
            throw new InvalidOperationException($"The device returned an unexpected acknowledgement for the {operationName} command.");
        }
    }

    private void WriteCommand(ReadOnlySpan<byte> command)
    {
        _serialPort.Write(command);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
