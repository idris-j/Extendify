/*
 * Extendify - A lightweight Windows taskbar enhancement utility
 * 
 * Author: Idris Jimoh
 * Created: January 2024
 * License: MIT
 * Repository: https://github.com/idris-j/Extendify
 * 
 * Description:
 * Extendify provides a sleek, customizable secondary taskbar for Windows
 * that helps manage and switch between running applications efficiently.
 */

using System;
using System.Windows.Forms;

namespace Extendify
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
            var taskbarForm = new TaskbarForm();
            Application.Run(taskbarForm);
        }
    }
}
