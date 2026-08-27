using System;
using System.IO;
using Microsoft.UI.Xaml.Data;

namespace DLSS_Swapper.Converters;

internal class BitmapImageUriConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string stringValue)
        {
            if (string.IsNullOrEmpty(stringValue))
            {
                return null;
            }

            // The file's write time rides along as a query, so the URI names the file's CONTENT
            // rather than its path. Cover files are replaced in place when a new cover is applied,
            // which is why every cover image used to carry IgnoreImageCache - the only way a stale
            // path-keyed cache entry could be got rid of. That made every scroll re-realisation a
            // fresh disk read and decode of art that had not changed. With the version in the URI,
            // an unchanged cover is a cache hit across the whole session, and a replaced one is a
            // different URI and misses. SetCoverImage's null-then-set is what re-runs this.
            try
            {
                var version = File.GetLastWriteTimeUtc(stringValue).Ticks;

                return new Uri($"{stringValue}?v={version}");
            }
            catch (Exception)
            {
                // A path that cannot be stat-ed loads the old way; the image control deals with it.
                return new Uri(stringValue);
            }
        }

        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
