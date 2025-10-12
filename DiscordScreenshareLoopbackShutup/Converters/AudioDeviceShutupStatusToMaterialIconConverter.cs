using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using DiscordScreenshareLoopbackShutup.Models;
using Material.Icons;

namespace DiscordScreenshareLoopbackShutup.Converters;

public class AudioDeviceShutupStatusToMaterialIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ShutupStatus status)
        {
            return status switch
            {
                ShutupStatus.None => null,
                ShutupStatus.ValidOutput => MaterialIconKind.Audio,
                ShutupStatus.Muted => MaterialIconKind.VolumeMute,
                _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
            };
        }

        return BindingOperations.DoNothing;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}