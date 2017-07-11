using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using EOSDigital.API;
using EOSDigital.SDK;

namespace PhotoBox
{

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Variables


        bool inStandby = false;
        int standbyAfterMin = 5;
        System.Windows.Threading.DispatcherTimer standbyTimer;

        CanonAPI APIHandler;
        Camera MainCamera = null;
        CameraValue[] AvList;
        CameraValue[] TvList;
        CameraValue[] ISOList;
        List<Camera> CamList;
        bool IsInit = false;
        int BulbTime = 30;
        ImageBrush bgbrush = new ImageBrush();
        Action<BitmapImage> SetImageAction;
        System.Windows.Forms.FolderBrowserDialog SaveFolderBrowser = new System.Windows.Forms.FolderBrowserDialog();
        System.Windows.Threading.DispatcherTimer countdownTimer;


        int ErrCount;
        object ErrLock = new object();

        static string uniqueSessionId = Guid.NewGuid().ToString().Split('-')[0];

        private SessionViewModel _vm;

        private bool takingFirstPicture = true;

        #endregion

        public MainWindow()
        {
            InitializeComponent();

            _vm = new SessionViewModel();
            this.DataContext = _vm;
            
            if (!Directory.Exists(_vm.STORAGE_PATH))
            {
                ReportError("Picture folder does not exist!", false);
            }

            countdownTimer = new System.Windows.Threading.DispatcherTimer();
            countdownTimer.Tick += new EventHandler(OnCountDownTick);
            countdownTimer.Interval = new TimeSpan(0, 0, 1);


            StartCamera();

            standbyTimer = new System.Windows.Threading.DispatcherTimer();
            standbyTimer.Tick += new EventHandler(OnGoToStandby);
            ResetStandbyTimer();
        }

        private void StartCamera()
        {
            try
            {
                APIHandler = new CanonAPI();
                APIHandler.CameraAdded += APIHandler_CameraAdded;

                ErrorHandler.SevereErrorHappened += ErrorHandler_SevereErrorHappened;
                ErrorHandler.NonSevereErrorHappened += ErrorHandler_NonSevereErrorHappened;
                SetImageAction = (BitmapImage img) => {
                    bgbrush.ImageSource = img;
                    img = null;
                };
                SaveFolderBrowser.Description = "Save Images To...";
                RefreshCamera();
                IsInit = true;
                OpenSession();

                // store pics on pc
                MainCamera.SetSetting(PropertyID.SaveTo, (int)SaveTo.Both);
                MainCamera.SetCapacity(4096, int.MaxValue);

                StartLV();
                SetLVAf();
            }
            catch (DllNotFoundException) { ReportError("Canon DLLs not found!", true); Application.Current.Shutdown(); }
            catch (Exception ex) { ReportError("Error starting camera: " + ex.Message, true); Application.Current.Shutdown(); }
        }

        private void StopCamera()
        {
            IsInit = false;

            CloseSession();
            try
            {

                MainCamera?.Dispose();
                MainCamera = null;

                APIHandler?.Dispose();
                APIHandler.CameraAdded -= APIHandler_CameraAdded;
                APIHandler = null;

                ErrorHandler.SevereErrorHappened -= ErrorHandler_SevereErrorHappened;
                ErrorHandler.NonSevereErrorHappened -= ErrorHandler_NonSevereErrorHappened;
            }
            catch (Exception ex)
            {
                ReportError("Error stopping camera: " + ex.Message, false);
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            StopCamera();
        }

        #region API Events

        private void APIHandler_CameraAdded(CanonAPI sender)
        {
            try { Dispatcher.Invoke((Action)delegate { RefreshCamera(); }); }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        private void MainCamera_StateChanged(Camera sender, StateEventID eventID, int parameter)
        {
            if (eventID == StateEventID.Shutdown)
            {
                try { Dispatcher.Invoke((Action)delegate { CloseSession(); }); }
                catch (Exception ex) { ReportError(ex.Message, false); }
            }
            else
            {
                SetLVAf();
            }
                
        }

        private void MainCamera_ProgressChanged(object sender, int progress)
        {
            try { MainProgressBar.Dispatcher.Invoke((Action)delegate { MainProgressBar.Value = progress; }); }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        private void MainCamera_LiveViewUpdated(Camera sender, Stream img)
        {
            try
            {
                using (WrapStream s = new WrapStream(img))
                {
                    img.Position = 0;
                    BitmapImage EvfImage = new BitmapImage();
                    EvfImage.BeginInit();
                    EvfImage.StreamSource = s;
                    EvfImage.CacheOption = BitmapCacheOption.OnLoad;
                    EvfImage.EndInit();
                    EvfImage.Freeze();
                    Application.Current.Dispatcher.Invoke(SetImageAction, EvfImage);
                    s.Close();
                    s.Dispose();
                    EvfImage = null;
                }
            }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        private void MainCamera_DownloadReady(Camera sender, DownloadInfo Info)
        {
            string dir = null;
            try
            {
                dir = _vm.STORAGE_PATH;
                String date = DateTime.Now.ToString("yyyy-MM-dd_hh-mm", System.Globalization.CultureInfo.GetCultureInfo("de-DE"));
                Info.FileName = date + "_" + uniqueSessionId + "_" +  imageId + "_" + repeatCount + "_" + Info.FileName;
                sender.DownloadFile(Info, dir);
                MainProgressBar.Dispatcher.Invoke((Action)delegate { MainProgressBar.Value = 0; });

            }
            catch (Exception ex) { ReportError(ex.Message, false); }

            SetLVAf();


            // invoke WPF-UI-Thread, since this method is called by the DLL
            this.Dispatcher.Invoke((Action)(() =>
            {
                if (dir != null)
                {
                    _vm.ViewerImages.Add(dir + "\\" + Info.FileName);
                    _vm.AllImages.Add(dir + "\\" + Info.FileName);
                    if (_vm.AllImages.Count > SessionViewModel.MAX_PICTURE_CNT)
                    {
                        _vm.AllImages.RemoveAt(0);
                    }
                }

                TakingPictureFinished();
            }));
        }

        private void ErrorHandler_NonSevereErrorHappened(object sender, ErrorCode ex)
        {
            ReportError($"SDK Error code: {ex} ({((int)ex).ToString("X")})", false);
        }

        private void ErrorHandler_SevereErrorHappened(object sender, Exception ex)
        {
            ReportError(ex.Message, true);
        }

        #endregion

        #region Session

        static IntPtr ApplicationMessageFilter(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            return IntPtr.Zero;
        }
        int printId = 1;
        private void PrintPhotos()
        {
            string printerFolder = Properties.Settings.Default.PrinterFolder;
            if (String.IsNullOrEmpty(printerFolder) || String.IsNullOrWhiteSpace(printerFolder) || !Directory.Exists(printerFolder))
            {
                ReportError("Cannot Print Photo: Printer folder does not exist!", false);
                return;
            }

            string printoutBackground = Properties.Settings.Default.BackgroundFile;
            if (String.IsNullOrEmpty(printoutBackground) || String.IsNullOrWhiteSpace(printoutBackground) || !File.Exists(printoutBackground))
            {
                ReportError("Cannot Print Photo: Printout background file does not exist!", false);
                return;
            }

            var printLayout = new PrintLayout { Width = 1200, Height = 1800 };
            printLayout.DataContext = _vm;
            printLayout.Measure(new System.Windows.Size(printLayout.Width, printLayout.Height));
            printLayout.Arrange(new Rect(new System.Windows.Size(printLayout.Width, printLayout.Height)));
            printLayout.UpdateLayout();

            RenderTargetBitmap rtb = new RenderTargetBitmap((int)printLayout.ActualWidth, (int)printLayout.ActualHeight, 96d, 96d, System.Windows.Media.PixelFormats.Default);
            rtb.Render(printLayout);
            //var crop = new CroppedBitmap(rtb, new Int32Rect(50, 50, 250, 250));
            BitmapEncoder jpgEncoder = new JpegBitmapEncoder();
            jpgEncoder.Frames.Add(BitmapFrame.Create(rtb));


            String date = DateTime.Now.ToString("yyyy-MM-dd_hh-mm", System.Globalization.CultureInfo.GetCultureInfo("de-DE"));
            String fileName = "print_" + date + "_" + uniqueSessionId + "_" + printId++ + ".jpg";
            String printerFolderPath = System.IO.Path.GetFullPath(printerFolder) + @"\" + fileName;

            bool successfullySaved = false;
            using (var fs = System.IO.File.OpenWrite(printerFolderPath))
            {
                jpgEncoder.Save(fs);
                successfullySaved = true;
            }

            if (!successfullySaved)
            {
                ReportError("Cannot Print Photo: Printout could not be saved!", false);
                return;
            }


            string printerName = Properties.Settings.Default.PrinterName;
            if (String.IsNullOrEmpty(printerName) || String.IsNullOrWhiteSpace(printerName))
            {
                ReportError("Cannot Print Photo: Printer not found!", false);
                return;
            }
            
            {
                new System.Threading.Thread(delegate () {
                    try
                    {
                        System.Drawing.Printing.PrintDocument pd = new System.Drawing.Printing.PrintDocument();
                        pd.PrinterSettings.PrinterName = printerName;
                        System.Drawing.Printing.PaperSize ps = new System.Drawing.Printing.PaperSize("First custom size", (int)printLayout.ActualWidth, (int)printLayout.ActualHeight);
                        pd.DefaultPageSettings.PaperSize = ps;
                        pd.PrintPage += (sender, args) =>
                        {
                            using (FileStream readFs = new FileStream(printerFolderPath, FileMode.Open, System.IO.FileAccess.Read))
                            {
                                System.Drawing.Image i = System.Drawing.Image.FromStream(readFs);
                                //System.Drawing.Point p = new System.Drawing.Point(0, 0);
                            
                                /*
                                int printedImageX = Properties.Settings.Default.PrintedImageX;
                                int printedImageY = Properties.Settings.Default.PrintedImageY;
                                // Create rectangle for source image.
                                System.Drawing.Rectangle srcRect = new System.Drawing.Rectangle(0, 0, Properties.Settings.Default.PrintedImageWidth, Properties.Settings.Default.PrintedImageHeight);
                                System.Drawing.GraphicsUnit units = System.Drawing.GraphicsUnit.Millimeter;
                                args.Graphics.DrawImage(i, printedImageX, printedImageY, Properties.Settings.Default.PrintedImageWidth, Properties.Settings.Default.PrintedImageHeight);
                                */
                                args.Graphics.DrawImage(i, Properties.Settings.Default.PrintedImageX, Properties.Settings.Default.PrintedImageY, Properties.Settings.Default.PrintedImageWidth, Properties.Settings.Default.PrintedImageHeight); // in inch * 100?
                                //i.Dispose();
                            }
                        };
                        pd.Print();
                    }
                    catch (Exception ex) { ReportError("Printing error: " + ex.Message, false); }
                }).Start();
            }
            
            
            /*
            int printLayoutWidth = 1200;
            int printLayoutHeight = 1800;
            var printLayout = new PrintLayout { Width = printLayoutWidth, Height = printLayoutHeight };
            printLayout.DataContext = _vm;
            printLayout.Measure(new System.Windows.Size(printLayout.Width, printLayout.Height));
            printLayout.Arrange(new Rect(new System.Windows.Size(printLayout.Width, printLayout.Height)));
            printLayout.UpdateLayout();


            System.Windows.Forms.UserControl controlContainer = new System.Windows.Forms.UserControl();
            controlContainer.Width = printLayoutWidth;
            controlContainer.Height = printLayoutHeight;
            controlContainer.Load += delegate (object sender, EventArgs e)
            {
                printLayout.Dispatcher.BeginInvoke((Action)delegate
                {
                    using (FileStream fs = new FileStream("print2.jpg", FileMode.Create))
                    {
                        RenderTargetBitmap bmp = new RenderTargetBitmap(printLayoutWidth, printLayoutHeight, 96, 96, PixelFormats.Pbgra32);
                        printLayout.UpdateLayout();
                        bmp.Render(printLayout);
                        BitmapEncoder encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(bmp));
                        encoder.Save(fs);
                        controlContainer.Dispose();
                    }
                }, System.Windows.Threading.DispatcherPriority.Background);
            };

            // ElementHost = %ProgramFiles%\Reference Assemblies\Microsoft\Framework\v3.0\WindowsFormsIntegration.dll
            controlContainer.Controls.Add(new System.Windows.Forms.Integration.ElementHost() { Child = printLayout, Dock = System.Windows.Forms.DockStyle.Fill });
            IntPtr handle = controlContainer.Handle;
            */

        }

        private void TakingPictureFinished()
        {
            if (repeatCount == 0) // after finishing the full series of pictures
            {
                countdownTimer.Stop();
                CountDownTxt.Text = "";
                CircularCountDown.Percentage = 100.0;
                CircularCountDownGrid.Visibility = Visibility.Hidden;
                TakePhotoButtonMain.Visibility = Visibility.Visible;

                _vm.ImageViewerVisibility = Visibility.Visible;


                PrintPhotos();
            }
            else // after taking only one picture out of a series
            {
                takingFirstPicture = false;
                if (!countdownTimer.IsEnabled)
                {
                    countdownTimer.Start();
                }
                countDown = Math.Max(1, SessionViewModel.INBETWEEN_COUNT_DOWN_TIME); // below 2 secs time inbetween photos, camera is too slow to recover and timer is not shown
                CountDownTxt.Text = countDown.ToString();
                //CountDownTxt.Text = "Ready?";
                //CountDownTxt.Foreground = Brushes.Red;
                CircularCountDown.Percentage = 100.0;
                CircularCountDownGrid.Visibility = Visibility.Visible;
            }
        }

        private void OpenSessionButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (MainCamera?.SessionOpen == true) CloseSession();
                else OpenSession();
            }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            try { RefreshCamera(); }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        #endregion

        #region Settings

        private void AvCoBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (AvCoBox.SelectedIndex < 0) return;
                MainCamera.SetSetting(PropertyID.Av, AvValues.GetValue((string)AvCoBox.SelectedItem).IntValue);
            }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        private void TvCoBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (TvCoBox.SelectedIndex < 0) return;

                MainCamera.SetSetting(PropertyID.Tv, TvValues.GetValue((string)TvCoBox.SelectedItem).IntValue);
                if ((string)TvCoBox.SelectedItem == "Bulb")
                {
                    BulbBox.IsEnabled = true;
                    BulbSlider.IsEnabled = true;
                }
                else
                {
                    BulbBox.IsEnabled = false;
                    BulbSlider.IsEnabled = false;
                }
            }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        private void ISOCoBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (ISOCoBox.SelectedIndex < 0) return;
                MainCamera.SetSetting(PropertyID.ISO, ISOValues.GetValue((string)ISOCoBox.SelectedItem).IntValue);
            }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        uint repeatCount = 0;
        uint countDown = SessionViewModel.FIRST_COUNT_DOWN_TIME;
        uint imageId = 0;
        private void TakePhotoButton_Click(object sender, RoutedEventArgs e)
        {
            ResetStandbyTimer();

            imageId++;
            StartTimer();
        }
        
        private void StartTimer()
        {
            repeatCount = _vm.NumberOfPhotosTaken;
            TakePhotoButtonMain.Visibility = Visibility.Hidden;
            countDown = SessionViewModel.FIRST_COUNT_DOWN_TIME;
            CountDownTxt.Text = countDown.ToString();
            //CountDownTxt.Foreground = Brushes.Red;
            CircularCountDown.Percentage = (countDown / (double)SessionViewModel.FIRST_COUNT_DOWN_TIME) * 100.0;
            CircularCountDownGrid.Visibility = Visibility.Visible;

            takingFirstPicture = true;
            countdownTimer.Start();

            _vm.ViewerImages.Clear();
        }

        private void OnGoToStandby(object sender, EventArgs e)
        {
            uxStandbyMessage.Visibility = Visibility.Visible;
            inStandby = true;

            //StopCamera();
            
            StopLV();
        }

        private void WakeupFromStandby()
        {
            uxStandbyMessage.Visibility = Visibility.Collapsed;
            inStandby = false;

            //StartCamera();
            
            StartLV();
        }

        private void OnCountDownTick(object sender, EventArgs e)
        {
            --countDown;
            if (countDown == 0)
            {
                countdownTimer.Stop();
                repeatCount--;
                CircularCountDownGrid.Visibility = Visibility.Hidden;
                try
                {
                    if ((string)TvCoBox.SelectedItem == "Bulb") MainCamera.TakePhotoBulbAsync(BulbTime);
                    else MainCamera.TakePhotoAsync();
                }
                catch (Exception ex) { ReportError(ex.Message, false); }
            }
            else
            {
                CountDownTxt.Text = countDown.ToString();
                /*
                if (countDown == 1)
                    CountDownTxt.Foreground = Brushes.Green;
                else if(countDown == 3)
                    CountDownTxt.Foreground = Brushes.Orange;
                */
                if (takingFirstPicture)
                    CircularCountDown.Percentage = (countDown / (double)SessionViewModel.FIRST_COUNT_DOWN_TIME) * 100.0;
                else
                    CircularCountDown.Percentage = 100.0; // just set it to 100%, this would be correct: (countDown / (double)SessionViewModel.INBETWEEN_COUNT_DOWN_TIME) * 100
            }
        }

        private void BulbSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try { if (IsInit) BulbBox.Text = BulbSlider.Value.ToString(); }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        private void BulbBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (IsInit)
                {
                    int b;
                    if (int.TryParse(BulbBox.Text, out b) && b != BulbTime)
                    {
                        BulbTime = b;
                        BulbSlider.Value = BulbTime;
                    }
                    else BulbBox.Text = BulbTime.ToString();
                }
            }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        #endregion

        #region Live view

        private void StartLV()
        {
            try
            {
                if (!MainCamera.IsLiveViewOn)
                {
                    LVCanvas.Background = bgbrush;
                    MainCamera.StartLiveView();
                }
            }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        private void StopLV()
        {
            try
            {
                if (MainCamera.IsLiveViewOn)
                {
                    MainCamera.StopLiveView();
                    LVCanvas.Background = Brushes.LightGray;
                }
            }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        private void SetLVAf()
        {
            try { MainCamera.SendCommand(CameraCommand.DoEvfAf, (int)EvfAFMode.Quick); }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        #endregion

        #region Subroutines

        private void CloseSession()
        {
            StopLV();
            if (MainCamera != null)
            {
                MainCamera.LiveViewUpdated -= MainCamera_LiveViewUpdated;
                MainCamera.ProgressChanged -= MainCamera_ProgressChanged;
                MainCamera.StateChanged -= MainCamera_StateChanged;
                MainCamera.DownloadReady -= MainCamera_DownloadReady;
                MainCamera.CloseSession();
            }
            AvCoBox.Items.Clear();
            TvCoBox.Items.Clear();
            ISOCoBox.Items.Clear();
            uxSettingsGroupBox.IsEnabled = false;
        }

        private void RefreshCamera()
        {
            CamList = APIHandler.GetCameraList();
        }

        private void OpenSession()
        {
            if (CamList.Count > 0)
            {
                MainCamera = CamList[0];
                MainCamera.OpenSession();
                MainCamera.LiveViewUpdated += MainCamera_LiveViewUpdated;
                MainCamera.ProgressChanged += MainCamera_ProgressChanged;
                MainCamera.StateChanged += MainCamera_StateChanged;
                MainCamera.DownloadReady += MainCamera_DownloadReady;
                
                AvList = MainCamera.GetSettingsList(PropertyID.Av);
                TvList = MainCamera.GetSettingsList(PropertyID.Tv);
                ISOList = MainCamera.GetSettingsList(PropertyID.ISO);
                foreach (var Av in AvList) AvCoBox.Items.Add(Av.StringValue);
                foreach (var Tv in TvList) TvCoBox.Items.Add(Tv.StringValue);
                foreach (var ISO in ISOList) ISOCoBox.Items.Add(ISO.StringValue);
                AvCoBox.SelectedIndex = AvCoBox.Items.IndexOf(AvValues.GetValue(MainCamera.GetInt32Setting(PropertyID.Av)).StringValue);
                TvCoBox.SelectedIndex = TvCoBox.Items.IndexOf(TvValues.GetValue(MainCamera.GetInt32Setting(PropertyID.Tv)).StringValue);
                ISOCoBox.SelectedIndex = ISOCoBox.Items.IndexOf(ISOValues.GetValue(MainCamera.GetInt32Setting(PropertyID.ISO)).StringValue);
                uxSettingsGroupBox.IsEnabled = true;
            }
        }

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

            if (lockdown) EnableUI(false);

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

        private void EnableUI(bool enable)
        {
            if (!Dispatcher.CheckAccess()) Dispatcher.Invoke((Action)delegate { EnableUI(enable); });
            else
            {
                uxSettingsGroupBox.IsEnabled = enable;
            }
        }

        private void ShowSettings_Click(object sender, RoutedEventArgs e)
        {
            uxSettingsGroupBox.Visibility = uxSettingsGroupBox.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        }

        private void BrowseAllPictures_Click(object sender, RoutedEventArgs e)
        {
            _vm.ViewerImages.Clear();
            int cnt = 0;
            System.Collections.ObjectModel.ObservableCollection<string> imgsCopy = new System.Collections.ObjectModel.ObservableCollection<string>();
            for (int i = _vm.AllImages.Count-1; i >= 0; --i)
            {
                imgsCopy.Add(_vm.AllImages[i]);
                cnt++;
                if (cnt > SessionViewModel.MAX_PICTURE_CNT)
                    break;
            }
            _vm.ViewerImages = imgsCopy;

            _vm.ImageViewerVisibility = Visibility.Visible;
            uxBrowseAllPicturesBtn.Visibility = Visibility.Collapsed;
        }

        #endregion

        private void StandbyMessage_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (inStandby)
            {
                WakeupFromStandby();
            }
        }

        private void StandbyMessage_TouchDown(object sender, System.Windows.Input.TouchEventArgs e)
        {
            if (inStandby)
            {
                WakeupFromStandby();
            }
        }

        private void ResetStandbyTimer()
        {
            standbyTimer.Stop();
            standbyTimer.Interval = new TimeSpan(0, standbyAfterMin, 0);
            standbyTimer.Start();
        }
    }
}
