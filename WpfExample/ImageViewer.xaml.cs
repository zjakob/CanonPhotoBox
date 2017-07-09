using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace PhotoBox
{

    /// <summary>
    /// Interaction logic for ImageViewer.xaml
    /// </summary>
    public partial class ImageViewer : UserControl
    {
        int collapseDelay = Properties.Settings.Default.ReviewTimeMS; // 15s delay
        System.Windows.Forms.Timer collapseTimer;

        public ImageViewer()
        {
            InitializeComponent();

            collapseTimer = new System.Windows.Forms.Timer();
            collapseTimer.Interval = collapseDelay;
            collapseTimer.Stop();
            collapseTimer.Tick += TickEventHandler;
        }

        ~ImageViewer()
        {
            collapseTimer.Stop();
            collapseTimer.Tick -= TickEventHandler;
        }

        private void ResetCollapseTimer(int interval)
        {
            collapseTimer.Stop();
            collapseTimer.Interval = interval;
            collapseTimer.Start();
        }

        private void BackToCamera_Click(object sender, RoutedEventArgs e)
        {
            BackToCamera();
        }

        private void TickEventHandler(object sender, EventArgs e)
        {
            collapseTimer.Stop();
            BackToCamera();
        }

        private void BackToCamera()
        {
            HideMaxImage();

            if (this != null && this.DataContext != null)
            {
                ((SessionViewModel)this.DataContext).ImageViewerVisibility = Visibility.Collapsed;
                ((SessionViewModel)this.DataContext).ViewerImages.Clear();
            }
        }

        private void HideMaxImage()
        {
            uxMaxImageContainer.Visibility = Visibility.Collapsed;
        }

        private void CloseMaxImage_TouchDown(object sender, TouchEventArgs e)
        {
            HideMaxImage();

            ResetCollapseTimer(collapseDelay);

            e.Handled = true;
        }

        private void CloseMaxImage_MouseClick(object sender, MouseButtonEventArgs e)
        {
            HideMaxImage();

            ResetCollapseTimer(collapseDelay);

            e.Handled = true;
        }

        private void CloseMaxImage_Click(object sender, RoutedEventArgs e)
        {
            HideMaxImage();

            ResetCollapseTimer(collapseDelay);

            e.Handled = true;
        }


        private void EnlargeImage(ImageSource imgSrc)
        {
            uxMaxImage.Source = imgSrc;
            uxMaxImageContainer.Visibility = Visibility.Visible;
        }

        double lastDownPosX = Double.NegativeInfinity;
        double lastDownPosY = Double.NegativeInfinity;
        private void EnlargeImage_DownClick(object sender, MouseButtonEventArgs e)
        {
            lastDownPosX = e.GetPosition(this).X;
            lastDownPosY = e.GetPosition(this).Y;

            ResetCollapseTimer(collapseDelay);
        }

        private void EnlargeImage_DownTouch(object sender, TouchEventArgs e)
        {
            lastDownPosX = e.GetTouchPoint(this).Position.X;
            lastDownPosY = e.GetTouchPoint(this).Position.Y;

            ResetCollapseTimer(collapseDelay);
        }

        private void EnlargeImage_UpClick(object sender, MouseButtonEventArgs e)
        {
            double currentUpPosX = e.GetPosition(this).X;
            double currentUpPosY = e.GetPosition(this).Y;
            if (Math.Abs(currentUpPosX-lastDownPosX) + Math.Abs(currentUpPosY - lastDownPosY) < 20)
            {
                EnlargeImage(((Image)sender).Source);
                lastDownPosX = Double.NegativeInfinity;
                lastDownPosY = Double.NegativeInfinity;
            }

            ResetCollapseTimer(collapseDelay);
        }

        private void EnlargeImage_UpTouch(object sender, TouchEventArgs e)
        {
            double currentUpPosX = e.GetTouchPoint(this).Position.X;
            double currentUpPosY = e.GetTouchPoint(this).Position.Y;
            if (Math.Abs(currentUpPosX - lastDownPosX) + Math.Abs(currentUpPosY - lastDownPosY) < 20)
            {
                EnlargeImage(((Image)sender).Source);
                lastDownPosX = Double.NegativeInfinity;
                lastDownPosY = Double.NegativeInfinity;
            }

            ResetCollapseTimer(collapseDelay);
            
        }

        /**
        * set invisible if no user interaction for more than 5 secs
        */
        private void UserControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (this.Visibility == Visibility.Visible)
            {
                ResetCollapseTimer(collapseDelay);
            }
            else
            {
                collapseTimer.Stop();
            }
        }

        /*
        void OnPropertyChanged(String prop)
        {
            PropertyChangedEventHandler handler = PropertyChanged;

            if (handler != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(prop));
            }
        }
        public event PropertyChangedEventHandler PropertyChanged;
        
        public string ImageSource1
        {
            get {
                return (string)GetValue(ImageSource1Property);
            }
            set {
                SetValue(ImageSource1Property, value);
                OnPropertyChanged("ImageSource1");
            }
        }

        /// <summary>
        /// Identified the Label dependency property
        /// </summary>
        public static readonly DependencyProperty ImageSource1Property = DependencyProperty.Register("ImageSource1", typeof(string), typeof(ImageViewer), new PropertyMetadata(null, OnPropertyChanged));

        public static void OnPropertyChanged(object sender, DependencyPropertyChangedEventArgs args)
        {
            var source = (ImageViewer)sender;
            source.OnPropertyChanged("ImageSource1");
        }
        */
    }
}
