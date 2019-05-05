using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PhotoBox
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


        public static uint FIRST_COUNT_DOWN_TIME = Properties.Settings.Default.FirstCountDownSeconds;
        public static uint INBETWEEN_COUNT_DOWN_TIME = Properties.Settings.Default.InbetweenCountDownSeconds;

        public String STORAGE_PATH = System.IO.Path.GetFullPath(Properties.Settings.Default.DefaultPictureFolder);
        public static uint MAX_PICTURE_CNT = 30;

        private uint _numberOfPhotosTaken;
        public uint NumberOfPhotosTaken
        {
            get
            {
                return _numberOfPhotosTaken;
            }
            set
            {
                _numberOfPhotosTaken = value;
                OnPropertyChanged("NumberOfPhotosTaken");
            }
        }

        private System.Windows.Visibility _uxImageViewerVisibility;
        public System.Windows.Visibility ImageViewerVisibility
        {
            get
            {
                return _uxImageViewerVisibility;
            }
            set
            {
                _uxImageViewerVisibility = value;
                OnPropertyChanged("ImageViewerVisibility");
            }
        }


        private ObservableCollection<string> _viewerImages;
        public ObservableCollection<string> ViewerImages
        {
            get
            {
                return _viewerImages;
            }
            set
            {
                _viewerImages = value;
                OnPropertyChanged("ViewerImages");
            }
        }

        public List<string> AllImages;


        #region Printer

        public string BackgroundFile
        {
            get
            {
                return System.IO.Path.GetFullPath(Properties.Settings.Default.BackgroundFile); ;
            }
        }

        private static Thickness _marginTop = new Thickness(0, Properties.Settings.Default.PrintoutMarginTop, 0, 0);
        public Thickness PrintoutMarginTop
        {
            get
            {
                return _marginTop;
            }
        }

        private static Thickness _marginInbetween = new Thickness(0, 0, 0, Properties.Settings.Default.PrintoutMarginInbetween);
        public Thickness PrintoutMarginInbetween
        {
            get
            {
                return _marginInbetween;
            }
        }

        #endregion


        public int WindowScaleX
        {
            get { return Properties.Settings.Default.WindowScaleX; }
        }
        public int WindowScaleY
        {
            get { return Properties.Settings.Default.WindowScaleY; }
        }

        public int LiveViewScaleX
        {
            get { return Properties.Settings.Default.LiveViewScaleX; }
        }
        public int LiveViewScaleY
        {
            get { return Properties.Settings.Default.LiveViewScaleY; }
        }

        public SessionViewModel()
        {
            AllImages = new List<string>();
            _viewerImages = new ObservableCollection<string>();
            _uxImageViewerVisibility = System.Windows.Visibility.Collapsed;
            _numberOfPhotosTaken = Properties.Settings.Default.NumberOfPhotosTaken;
        }


    }
}
