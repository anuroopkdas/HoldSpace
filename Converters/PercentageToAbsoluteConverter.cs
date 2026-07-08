using System;
using System.Globalization;
using System.Windows.Data;

namespace HoldSpace.Converters
{
    public class PercentageToAbsoluteConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2 && values[0] is double percentage && values[1] is double totalDimension)
            {
                double offset = 0;
                if (parameter != null && double.TryParse(parameter.ToString(), out double parsedOffset))
                {
                    offset = parsedOffset;
                }
                
                // Formula: percentage / 100 * totalDimension - offset
                double absolute = (percentage / 100.0) * totalDimension - offset;

                // Clamp coordinates to keep cards entirely inside the monitor dimensions
                double cardSize = offset * 2.0;
                if (totalDimension > cardSize)
                {
                    return Math.Max(0.0, Math.Min(absolute, totalDimension - cardSize));
                }
                return Math.Max(0.0, absolute);
            }
            return 0.0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
