using System;
using DLSS_Swapper.Data;
using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace DLSS_Swapper.Converters;

/// <summary>
/// Colours an update badge by the vendor whose dll is out of date.
/// </summary>
internal class DllVendorToBrushConverter : IValueConverter
{
    // Brand colours darkened until white badge text clears 5:1 contrast against each of them. The
    // badge carries its meaning as text, so these only need to be recognisable, not exact.
    // Because the badge has its own fill it reads the same on a dark game cover and in the light
    // theme list view.
    static readonly SolidColorBrush _nvidiaBrush = new SolidColorBrush(Color.FromArgb(255, 0x4A, 0x75, 0x00));
    static readonly SolidColorBrush _amdBrush = new SolidColorBrush(Color.FromArgb(255, 0xC1, 0x12, 0x1F));
    static readonly SolidColorBrush _intelBrush = new SolidColorBrush(Color.FromArgb(255, 0x0A, 0x6B, 0xA8));
    static readonly SolidColorBrush _unknownBrush = new SolidColorBrush(Colors.Gray);

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is DllVendor vendor)
        {
            return vendor switch
            {
                DllVendor.Nvidia => _nvidiaBrush,
                DllVendor.Amd => _amdBrush,
                DllVendor.Intel => _intelBrush,
                _ => _unknownBrush,
            };
        }

        return _unknownBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
