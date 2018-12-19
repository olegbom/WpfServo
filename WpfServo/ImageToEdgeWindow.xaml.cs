using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;
using WpfServo.Annotations;
using CsPotrace;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Drawing.Brushes;
using Point = System.Windows.Point;
using Rect = System.Windows.Rect;
using Size = System.Windows.Size;
using Window = System.Windows.Window;

namespace WpfServo
{
    /// <summary>
    /// Логика взаимодействия для ImageToEdgeWindow.xaml
    /// </summary>
    public partial class ImageToEdgeWindow : Window, INotifyPropertyChanged
    {
        [DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteObject([In] IntPtr hObject);

        public ImageSource ImageSourceForBitmap(Bitmap bmp)
        {
            var handle = bmp.GetHbitmap();
            try
            {
                return Imaging.CreateBitmapSourceFromHBitmap(handle, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            }
            finally { DeleteObject(handle); }
        }


        public ImageToEdgeWindow()
        {
            DataContext = this;
            InitializeComponent();
        }

        public static List<List<Curve>> ListOfPaths = new List<List<Curve>>();


        public double RectCutWidth { get; set; } = 200;
        public double RectCutHeight { get; set; } = 200;
        public double RectCutX { get; set; } = 0;
        public double RectCutY { get; set; } = 0;


        public string FileName { get; set; }
        public void OnFileNameChanged()
        {
            gray = new Mat(FileName, ImreadModes.Grayscale);
            OrigImage.Source = gray.ToBitmapSource();
            EdgeDetectionRefresh();
        }

        public double CannyThresh { get; set; } = 200;
        public void OnCannyThreshChanged()
        {
            EdgeDetectionRefresh();
        }


        public double CannyLinking { get; set; } = 300;
        public void OnCannyLinkingChanged()
        {
            EdgeDetectionRefresh();
        }

        public double ProTraceThreshold { get; set; } = 0.5;

        public void OnProTraceThresholdChanged()
        {
            Potrace.Treshold = ProTraceThreshold;
            EdgeDetectionRefresh();
        }

        public bool? CannyEnabled { get; set; } = true;

        public void OnCannyEnabledChanged()
        {
            EdgeDetectionRefresh();
        }

        private void EdgeDetectionRefresh()
        {
            if (gray == null) return;

            Potrace.Clear();
            ListOfPaths.Clear();


            double scale = OrigImage.ActualHeight / gray.Rows;
            if (scale <= 0) return;
            int x0 = (int) (RectCutX / scale);
            int x1 = (int) ((RectCutX + RectCutWidth) / scale);
            int y0 = (int) (RectCutY / scale);
            int y1 = (int) ((RectCutY + RectCutHeight) / scale);
            if (x1 >= gray.Cols) x1 = gray.Cols - 1;
            if (y1 >= gray.Rows) y1 = gray.Rows - 1;

            Mat cutImage = gray[y0, y1, x0, x1];
                
            if (CannyEnabled == true)
            {
                Mat canny = cutImage.Canny(CannyThresh, CannyLinking);
                Potrace.Potrace_Trace(canny.ToBitmap(), ListOfPaths);
            }
            else Potrace.Potrace_Trace(cutImage.ToBitmap(), ListOfPaths);

            EdgePathGeometry.AddListOfPaths(ListOfPaths);
        }
        
        private Mat gray;

        private void ButtonOpenImage_OnClick(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Multiselect = false;
            dialog.Filter = "Файл изображения|*.bmp;*.gif;*.exif;*.jpg;*.jpeg;*.png;*.tiff;*.tif";
            if (dialog.ShowDialog() == true)
            {
                FileName = dialog.FileName;
            }
           
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

         

        private void ButtonTransfer_OnClick(object sender, RoutedEventArgs e)
        {
            DialogResult = MyCanvas.Children.Count > 0;
            Close();
        }

        private Point _mDownPos;
        private Point _mMovePos;
        private bool _isDragging;

        private void OrigImage_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                _isDragging = true;
                _mDownPos = e.GetPosition(OrigImage);
                RectCutX = _mDownPos.X;
                RectCutY = _mDownPos.Y;

            }

        }

        private void OrigImage_OnMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                if (_isDragging)
                {
                    _mMovePos = e.GetPosition(OrigImage);
                    RectCutX = Math.Min(_mDownPos.X, _mMovePos.X);
                    RectCutY = Math.Min(_mDownPos.Y, _mMovePos.Y);
                    RectCutWidth = Math.Abs(_mDownPos.X - _mMovePos.X);
                    RectCutHeight = Math.Abs(_mDownPos.Y - _mMovePos.Y);
                    EdgeDetectionRefresh();
                }
            }

        }

        private void OrigImage_OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
               _isDragging = false;
            }

        }

        private void RectCut_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            
        }
    }
}
