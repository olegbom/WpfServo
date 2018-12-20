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
using Microsoft.CodeAnalysis.CSharp;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Drawing.Brushes;
using Image = System.Drawing.Image;
using Point = System.Windows.Point;
using Rect = System.Windows.Rect;
using Rectangle = System.Drawing.Rectangle;
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


        public int RectCutWidth { get; set; } = 200;
        public int RectCutHeight { get; set; } = 200;
        public int RectCutX { get; set; } = 0;
        public int RectCutY { get; set; } = 0;


        public string FileName { get; set; }
        public void OnFileNameChanged()
        {
           
            image = Image.FromFile(FileName) as Bitmap;
            OrigImage.Source = new BitmapImage(new Uri(FileName));
            EdgeDetectionRefresh();
        }


        public double ProTraceThreshold { get; set; } = 0.5;

        public void OnProTraceThresholdChanged()
        {
            Potrace.Treshold = ProTraceThreshold;
            EdgeDetectionRefresh();
        }



        private void EdgeDetectionRefresh()
        {
            if (image == null) return;

            Potrace.Clear();
            ListOfPaths.Clear();


            double scale = OrigImage.ActualHeight / image.Height;
            if (scale <= 0) return;
            int x = (int) (RectCutX / scale);
            int w = (int) (RectCutWidth / scale);
            int y = (int) (RectCutY / scale);
            int h = (int) (RectCutHeight / scale);

            Bitmap cutImage = CropBitmap(image, new Rectangle(x,y,w,h));
                
            Potrace.Potrace_Trace(cutImage, ListOfPaths);

            EdgePathGeometry.AddListOfPaths(ListOfPaths);
        }

        public static Bitmap CropBitmap(Bitmap src, Rectangle cropRect)
        {
            
            Bitmap target = new Bitmap(cropRect.Width, cropRect.Height);

            using (Graphics g = Graphics.FromImage(target))
            {
                g.DrawImage(src, new Rectangle(0, 0, target.Width, target.Height),
                    cropRect,
                    GraphicsUnit.Pixel);
            }

            return target;
        }


        private Bitmap image;

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
                RectCutX = (int)_mDownPos.X;
                RectCutY = (int)_mDownPos.Y;

            }

        }

        private void OrigImage_OnMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                if (_isDragging)
                {
                    _mMovePos = e.GetPosition(OrigImage);
                    RectCutX = (int) Math.Min(_mDownPos.X, _mMovePos.X);
                    RectCutY = (int) Math.Min(_mDownPos.Y, _mMovePos.Y);
                    RectCutWidth = (int) Math.Abs(_mDownPos.X - _mMovePos.X);
                    RectCutHeight = (int) Math.Abs(_mDownPos.Y - _mMovePos.Y);
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
