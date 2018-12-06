using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;
using System.Management;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO.Ports;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using WpfServo.Annotations;

namespace WpfServo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {

        const double ALen = 150.0;
        const double BLen = 71.0;


        private SerialPort _serialPort;
        public MainWindow()
        {

            OpenComPortAsync();


            InitializeComponent();

            DataContext = this;
           
        }

        private bool _isFindComPort = false;

        private SerialPort FindComPort()
        {
            SerialPort serialPort = null;
            while (true)
            {
                ManagementObjectSearcher searcher =
                    new ManagementObjectSearcher("root\\CIMV2",
                        "SELECT * FROM Win32_SerialPort");

                foreach (ManagementObject queryObj in searcher.Get())
                {
                    string name = queryObj["Name"].ToString();
                    if (name.Contains("STLink Virtual COM Port"))
                    {
                        serialPort = new SerialPort(queryObj["DeviceID"].ToString(), 115200,
                            Parity.None,
                            8,
                            StopBits.One);
                        break;
                    }
                }

                if (serialPort != null) break;
                Thread.Sleep(1000);
            }

            _isFindComPort = true;
            return serialPort;
        }

        private async void OpenComPortAsync()
        {
            _serialPort = await Task.Run(() => FindComPort());
            _serialPort.Open();
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Slider mySlider = (Slider)sender;

            byte servo = (byte)(Grid.GetRow(mySlider)+ 31);

            byte a = 0, b = 0;
            var val = (UInt16)e.NewValue;
            a = (byte)(val >> 8);
            b = (byte)(0xFF & val);
            _serialPort.Write(new byte[] { servo , a, b }, 0, 3);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _serialPort.Close();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Q)
            {
                Slider1.Value += 40;
            }
            if (e.Key == Key.E)
            {
                Slider1.Value -= 40;
            }

        }

        double old1, old2;
        Point mDownPos;
        Point mUpPos;
        bool isDown;
        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            
        }

        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var canvas = (Canvas) sender;
            mDownPos = e.GetPosition(canvas);

            var width = canvas.ActualWidth;
            var height = canvas.ActualHeight;

            _x = (mDownPos.X / width -0.5)*150;
            _y = (1-mDownPos.Y / height) * 100;

            isDown = true;
            CalcServoAngles(_x, _y, Z+5);
        }
        
        private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (isDown)
            {
                isDown = false;
                CalcServoAngles(_x, _y, Z+5);

            }
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDown)
            {

                var canvas = (Canvas)sender;
                mDownPos = e.GetPosition(canvas);

                var width = canvas.ActualWidth;
                var height = canvas.ActualHeight;

                _x = (mDownPos.X / width - 0.5) * 150;
                _y = (1-mDownPos.Y / height) * 100;

               
                CalcServoAngles(_x, _y, Z);

            }
           
        }

        private void CalcServoAngles(double x, double y, double z)
        {
            y *= 1.2;
            z = z + (y - 120) / 8;
            double beta = Math.Atan2(Math.Abs(y), x);

            x -= 90 * Math.Cos(beta);
            y -= 90 * Math.Sin(beta);

            double buff0 = x * x + y * y;
            double x1 = Math.Sqrt(buff0);

            double lsqr = x1*x1 + z * z;
            double l = Math.Sqrt(lsqr);

            double n = ALen * ALen - BLen * BLen;

            double b1 = (lsqr - n) / 2 / l;
            double a1 = (lsqr + n) / 2 / l;

            double m = Math.Sqrt(BLen * BLen   - b1 * b1);

            double anglebb1 = Math.Atan2(b1, m);
            double angleaa1 = Math.Atan2(a1, m);
            double anglelx1 = Math.Atan2(z, x1);
            double phi = anglelx1 - angleaa1 + Math.PI/2;
            double gamma =  Math.PI - anglebb1 - angleaa1;

            
            double theta = anglelx1 + anglebb1;
           // theta -= Math.PI/2;
            double betaTicks = beta * 180 / Math.PI / 0.09 * 2 + 1000;
            double gammaTicks = gamma * 180 / Math.PI / 0.108 * 2 + 1390;
            double phiTicks = phi * 180 / Math.PI / 0.11 * 2 + 1350;
            double thetaTicks = theta * 180 / Math.PI / 0.086 * 2 + 1000;

            thetaTicks -= _pickUp;

         /*   double[] doubleValues = new double[6]
            {
                betaTicks, phiTicks, gammaTicks, thetaTicks, 1500, 1500
            };
            byte[] bytes = new byte[18];
            for (int i = 0; i < 6; i++)
            {
                var val = (UInt16)doubleValues[i];
                bytes[i * 2] = (byte)(i + 31);
                bytes[i*2 + 1] = (byte)(val >> 8);
                bytes[i*2 + 2] = (byte)(0xFF & val);
                _serialPort.Write(bytes, 0, 18);
            }*/
            

            Slider5.Value = thetaTicks;
          
            Slider1.Value = betaTicks;

            Slider4.Value = phiTicks;

            Slider3.Value = gammaTicks;



        }

        private double Z = 150, _x = 0, _y = 100, _pickUp = 0;

        private void Slider_ZChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Z = ((Slider) sender).Value;
          
            CalcServoAngles(_x, _y, Z);
        }

        public Task PraseTask;
        public ConcurrentQueue<Line> Lines = new ConcurrentQueue<Line>(); 

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            string[] MyText = MyTextBox.Text.Split(new []{"\r\n"}, StringSplitOptions.None);

            GlyphTypeface myGlyph = new GlyphTypeface(new Uri("file:///C:\\WINDOWS\\Fonts\\times.ttf"));
            MyCanvas.Children.Clear();

            
                for (int j = 0; j < MyText.Length; j++)
                {
                    string myString = MyText[j];


                    for (var i = 0; i < myString.Length; i++)
                    {
                        var ch = myString[i];
                        Geometry myGeom = myGlyph.GetGlyphOutline(myGlyph.CharacterToGlyphMap[ch], 40, 10);

                        PathGeometry myPath = myGeom.GetOutlinedPathGeometry();
                        // Path.Data = myPath;
                  
                        PickUpPen();
                        Thread.Sleep(100);
                        DrawPathGeometry(myPath, i * 28 - 80, j * 30 - 120);
                        PickUpPen();
                    }
                }
           
           

           

            /*double xOld = 0, yOld = 0;
            for (double i = 0; i < Math.PI*20+1; i += Math.PI/40)
            {
                double x = Math.Sin(i) * 25;
                double y = CLen + 80 + Math.Cos(i) * 25;
                DrawLine(xOld, yOld, x, y);
                xOld = x;
                yOld = y;
            }
            */
        }


        private void PickUpPen()
        {
            _pickUp = 50;
            CalcServoAngles(_x, _y, Z + 20);
            _pickUp = 0;
        }

        private void DrawPathGeometry(PathGeometry myPath, double x0, double y0)
        {
           
            

            foreach (var fig in myPath.Figures)
            {
                List<Point> points = new List<Point>();
                foreach (var seg in fig.Segments)
                {
                    if (seg is LineSegment ls)
                    {
                        points.Add(ls.Point);
                    }

                    if (seg is ArcSegment arcs)
                    {
                        points.Add(arcs.Point);
                    }

                    if (seg is PolyLineSegment pls)
                    {
                        foreach (var point in pls.Points)
                        {
                            points.Add(point);
                        }
                    }

                    if (seg is PolyBezierSegment pbs)
                    {
                        foreach (var point in pbs.Points)
                        {
                            points.Add(point);
                        }
                    }

                    if (seg is BezierSegment bs)
                    {
                        points.Add(bs.Point1);
                        points.Add(bs.Point2);
                        points.Add(bs.Point3);
                    }
                }
                points.Add(points[0]);
                points = points.Select(p => p + new Vector(x0 - 50, y0)).ToList();

              
             

                if (points.Count < 1) return;
                _pickUp = 50;
                for (int i = 20; i >= 0; i--)
                {
                    CalcServoAngles(points[0].X, -points[0].Y, Z + i);
                    Thread.Sleep(25);
                }

                _pickUp = 0;
                for (var i = 0; i < points.Count-1; i++)
                {
                    DrawLine(points[i].X, points[i].Y, points[i+1].X, points[i+1].Y);
                }
                _pickUp = 50;
                for (int i = 0; i < 21; i++)
                {
                    CalcServoAngles(points[0].X, -points[0].Y, Z + i);
                    Thread.Sleep(25);
                }
                _pickUp = 0;
                for (var i = 0; i < points.Count -1; i++)
                {
                    MyCanvas.Children.Add(new Line()
                    {
                        X1 = points[i].X+100,
                        Y1 = points[i].Y+100,
                        X2 = points[i + 1].X+100,
                        Y2 = points[i + 1].Y+100,
                        Stroke = Brushes.Black,
                        });
                }

            }
           

           
        }

        private void DrawLine(double x0, double y0, double x1, double y1)
        {
            if (x0.Equals(double.NaN)) return;
            if (x0 < -250 || y0 < -250 || x1 < -250 || y0 < -250) return;
            double leng = Math.Sqrt(Math.Pow(x1 - x0, 2) + Math.Pow(y1 - y0, 2));
            for (double t = 0; t <= 1; t += 1 / leng)
            {
                _x = x0 * (1 - t) + x1 * t;
                _y = -y0 * (1 - t) - y1 * t;
                CalcServoAngles(_x, _y , Z);
                Thread.Sleep(10);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
