using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using OmniRemote.Core.Models;

namespace OmniRemote.App.Views;

public sealed class ConnectionStateToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is ConnectionState state ? StateToColor(state) : Colors.Gray;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static Color StateToColor(ConnectionState state) => state switch
    {
        ConnectionState.Connected    => Color.Parse("#A6E3A1"), // green
        ConnectionState.Connecting   => Color.Parse("#F9E2AF"), // yellow
        ConnectionState.Failed       => Color.Parse("#F38BA8"), // red
        ConnectionState.Disconnected => Color.Parse("#585B70"), // grey
        _ => Colors.Gray,
    };
}
