using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AddInManager
{
    internal class ExceptionHandler
    {
        public static void ShowException(System.Exception e)
        {
            System.Windows.MessageBox.Show(e.ToString(), "AddInManager exception", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }
}
