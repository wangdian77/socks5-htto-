using System;
using System.Globalization;
using System.Windows.Data;

namespace Socks5Client.UI.Converters
{
    /// <summary>
    /// 编辑模式到标题转换器
    /// </summary>
    public class EditModeToTitleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isEditMode)
            {
                return isEditMode ? "编辑节点" : "添加节点";
            }
            return "添加节点";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

