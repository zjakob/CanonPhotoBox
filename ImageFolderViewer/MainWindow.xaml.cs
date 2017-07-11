using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ImageFolderViewer
{


    public class SessionViewModel : INotifyPropertyChanged
    {

        void OnPropertyChanged(String prop)
        {
            PropertyChangedEventHandler handler = PropertyChanged;

            if (handler != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(prop));
            }
        }
        public event PropertyChangedEventHandler PropertyChanged;


        private ObservableCollection<string> _singleSessionImages;
        public ObservableCollection<string> SingleSessionImages
        {
            get
            {
                return _singleSessionImages;
            }
            set
            {
                _singleSessionImages = value;
                OnPropertyChanged("SingleSessionImages");
            }
        }

        public SessionViewModel()
        {
            this.SingleSessionImages = new ObservableCollection<string>();
        }
    }


        /// <summary>
        /// Interaction logic for MainWindow.xaml
        /// </summary>
        public partial class MainWindow : Window
    {

        static System.Windows.Forms.Timer nextImageTimer = new System.Windows.Forms.Timer();

        FileSystemWatcher watcher = new FileSystemWatcher();
        public string STORAGE_PATH = System.IO.Path.GetFullPath(Properties.Settings.Default.DefaultPictureFolder);


        uint CountDownRepeat = Properties.Settings.Default.CountDownRepeat;

        SessionViewModel _vm = new SessionViewModel();

        public MainWindow()
        {
            InitializeComponent();

            this.DataContext = _vm;

            if (!Directory.Exists(STORAGE_PATH))
            {
                ReportError("Picture folder does not exist!", false);
            }
            else
            {
                ShowRandomImage();

                // init next image timer
                nextImageTimer.Tick += (o, ea) =>
                {
                    uxRandomImage.Visibility = Visibility.Visible;
                    uxSingleSessionImagesContainer.Visibility = Visibility.Collapsed;

                    ShowRandomImage();
                };
                nextImageTimer.Interval = Properties.Settings.Default.ImageDisplayTimeMS;
                nextImageTimer.Start();

                // watch folder for new images
                watchFolder();
            }
        }

        private void watchFolder()
        {

            FileSystemWatcher watcher = new FileSystemWatcher();
            watcher.Path = STORAGE_PATH;
            watcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite
                                   | NotifyFilters.FileName | NotifyFilters.DirectoryName;
            watcher.Filter = "*.*";
            watcher.Changed += new FileSystemEventHandler(OnChanged);
            watcher.EnableRaisingEvents = true;
        }

        private void OnChanged(object source, FileSystemEventArgs e)
        {
            Dispatcher.Invoke(new Action(() => {

                // reset image timer
                nextImageTimer.Stop();
                nextImageTimer.Start();

                _vm.SingleSessionImages.Clear();

                _vm.SingleSessionImages.Add(e.FullPath);

                string[] splitNewImagPath = e.FullPath.Split('\\').Last().Split('_');

                if (splitNewImagPath.Length <= 4)
                    return;

                    string newSessionId = splitNewImagPath[2];
                string newCycleId = splitNewImagPath[3];
                string newRepeatCnt = splitNewImagPath[4];
                

                string[] imagePaths = Directory.GetFiles(STORAGE_PATH);
                for (int i = imagePaths.Length-1; i >= 0; --i)
                {
                    string[] splitImagPath = imagePaths[i].Split('_');
                    if (splitImagPath.Length > 4)
                    {
                        string sessionId = splitImagPath[2];
                        string cycleId = splitImagPath[3];
                        string repeatCnt = splitImagPath[4];

                        if (sessionId.CompareTo(newSessionId) == 0 &&
                        cycleId.CompareTo(newCycleId) == 0 &&
                        repeatCnt.CompareTo(newRepeatCnt) != 0)
                        {
                            _vm.SingleSessionImages.Add(imagePaths[i]);
                        }

                        if (_vm.SingleSessionImages.Count == CountDownRepeat)
                            break;
                    }
                }

                uxRandomImage.Visibility = Visibility.Collapsed;
                uxSingleSessionImagesContainer.Visibility = Visibility.Visible;


                ShowNewImage(e.FullPath);
            }), DispatcherPriority.ContextIdle);
        }

        private void ShowRandomImage()
        {
            string[] imagePaths = Directory.GetFiles(STORAGE_PATH);
            if (imagePaths.Length > 0)
            {
                Random r = new Random();
                int rInt = r.Next(0, imagePaths.Length); //for ints
                string imagePath = imagePaths[rInt];

                ShowNewImage(imagePath);
            }
        }

        private void ShowNewImage(String imagePath)
        {
            String stringPath = imagePath;
            Uri imageUri = new Uri(stringPath);
            BitmapImage imageBitmap = new BitmapImage(imageUri);
            uxRandomImage.Source = imageBitmap;
        }





        int ErrCount;
        object ErrLock = new object();

        private void WriteToErrorLog(string msg)
        {
            String date = DateTime.Now.ToString("yyyy-MM-dd_hh-mm-ss", System.Globalization.CultureInfo.GetCultureInfo("de-DE"));
            string path = @".\error" + date + ".log";
            File.AppendAllText(path, msg + Environment.NewLine);
        }

        private void ReportError(string message, bool lockdown)
        {
            int errc;
            lock (ErrLock) { errc = ++ErrCount; }

            if (errc < 4)
            {
                //MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                WriteToErrorLog(message);
            }
            else if (errc == 4)
            {
                WriteToErrorLog(message);
                MessageBox.Show("Many errors happened!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            lock (ErrLock) { ErrCount--; }
        }

    }
}
