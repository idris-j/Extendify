using System;
using System.Windows.Forms;

namespace Extendify
{
    class Program
    {
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
            // Launch the main taskbar form first
            Application.Run(new TaskbarForm());
        }
    }
}
