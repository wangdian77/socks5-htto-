using System;
using System.Globalization;
using System.Windows.Data;

namespace Socks5Client.UI.Converters
{
    /// <summary>
    /// 布尔值到连接状态文本转换器
    /// </summary>
    public class BoolToConnectedConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isConnected)
            {
                return isConnected ? "已连接" : "未连接";
            }
            return "未连接";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

