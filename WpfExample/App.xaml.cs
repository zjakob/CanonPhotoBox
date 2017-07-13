using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Windows;

namespace PhotoBox
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App() : base()
        {
            this.Dispatcher.UnhandledException += OnDispatcherUnhandledException;
        }

        void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            string errorMessage = string.Format("An unhandled exception occurred: {0}", e.Exception.Message);


            String date = DateTime.Now.ToString("yyyy-MM-dd_hh-mm-ss", System.Globalization.CultureInfo.GetCultureInfo("de-DE"));
            string path = @".\error_unhandled_" + date + ".log";
            System.IO.File.AppendAllText(path, errorMessage + Environment.NewLine);

            e.Handled = true;
        }
    }
}
