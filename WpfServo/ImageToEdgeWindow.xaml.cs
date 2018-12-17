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
using Emgu.CV;
using Emgu.CV.Structure;
using Microsoft.Win32;
using WpfServo.Annotations;
using CsPotrace;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Drawing.Brushes;
using Point = System.Windows.Point;

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


        public string FileName { get; set; }
        public void OnFileNameChanged()
        {
            gray = new Image<Gray, byte>(FileName);
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
           
            OrigImage.Source = ImageSourceForBitmap(gray.ToBitmap());
            
            Potrace.Clear();
            ListOfPaths.Clear();
            if (CannyEnabled == true)
            {
                Image<Gray, byte> canny = gray.Canny(CannyThresh, CannyLinking);
                Potrace.Potrace_Trace(canny.ToBitmap(), ListOfPaths);
            }
            else Potrace.Potrace_Trace(gray.ToBitmap(), ListOfPaths);

            EdgePathGeometry.AddListOfPaths(ListOfPaths);
            
            

          
        }
        
        private Image<Gray, byte> gray;

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

      
    }
}
