using System;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using Avalonia;
using Avalonia.Data.Converters;

namespace TorrentFlow;

public class EnumDescriptionConverter : IValueConverter
{
    private string GetEnumDescription(Enum enumObj)
    {
        var fieldInfo = enumObj.GetType().GetField(enumObj.ToString());
        var attribArray = fieldInfo.GetCustomAttributes(false);

        if (attribArray.Length == 0)
        {
            return enumObj.ToString();
        }
        else
        {
            DescriptionAttribute attrib = null;

            foreach (var att in attribArray)
                if (att is DescriptionAttribute)
                    attrib = att as DescriptionAttribute;

            if (attrib != null)
                return attrib.Description;

            return enumObj.ToString();
        }
    }

    object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Enum myEnum) return GetEnumDescription(myEnum);
        return string.Empty;
    }

    object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string description)
        {
            var enumType = parameter as Type ?? targetType;

            if (enumType != null && enumType.IsEnum)
                foreach (Enum enumValue in Enum.GetValues(enumType))
                    if (GetEnumDescription(enumValue).Equals(description, StringComparison.OrdinalIgnoreCase))
                        return enumValue;
        }

        return AvaloniaProperty.UnsetValue;
    }
}