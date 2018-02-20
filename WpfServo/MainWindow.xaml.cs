using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO.Ports;
using System.Threading;

namespace WpfServo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        const double ALen = 150.0;
        const double BLen = 69.0;
        const double CLen = 60;

        SerialPort _serialPort = new SerialPort("COM3",
                                        115200,
                                        Parity.None,
                                        8,
                                        StopBits.One);
        public MainWindow()
        {
            _serialPort.Open();
            InitializeComponent();

            DataContext = this;
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
            _y = (1-mDownPos.Y / height) * 100 + CLen;

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
                _y = (1-mDownPos.Y / height) * 100+CLen;

               
                CalcServoAngles(_x, _y, Z);

            }
           
        }

        private void CalcServoAngles(double x, double y, double z)
        {
            y *= 1.2;
            z = z + (y - 120) / 8;
            double beta = Math.Atan2(Math.Abs(y), x);

           // x -= 60 * Math.Cos(beta);
           // y -= 60 * Math.Sin(beta);

            double buff0 = x * x + y * y;
            double x1 = Math.Sqrt(buff0)-CLen;

            double lsqr = x1*x1 + z * z;
            double l = Math.Sqrt(lsqr);

            double n = ALen * ALen - BLen * BLen;

            double b1 = (lsqr - n) / 2 / l;
            double a1 = (lsqr + n) / 2 / l;

            double m = Math.Sqrt(BLen * BLen - b1 * b1);

            double anglebb1 = Math.Atan2(b1, m);
            double angleaa1 = Math.Atan2(a1, m);
            double anglelx1 = Math.Atan2(z, x1);
            double phi = anglelx1 - angleaa1 + Math.PI/2;
            double gamma =  Math.PI - anglebb1 - angleaa1;

            
            double theta = anglelx1 + anglebb1 + phi/50;
            //theta = Math.PI/2;
            double betaTicks = beta * 180 / Math.PI / 0.09 * 2 + 1000;
            double gammaTicks = gamma * 180 / Math.PI / 0.108 * 2 + 1390;
            double phiTicks = phi * 180 / Math.PI / 0.11 * 2 + 1350;
            double thetaTicks = theta * 180 / Math.PI / 0.086 * 2 + 1000;
            Slider1.Value = betaTicks;
            Slider2.Value = phiTicks;
            Slider3.Value = gammaTicks;
            Slider4.Value = thetaTicks;
            

        }

        private double Z = 150, _x = 0,_y = CLen;

        private void Slider_ZChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Z = ((Slider) sender).Value;
          
            CalcServoAngles(_x, _y, Z);
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            string MyText = MyTextBox.Text;

            GlyphTypeface myGlyph = new GlyphTypeface(new Uri("file:///C:\\WINDOWS\\Fonts\\cour.ttf"));
            if (MyText.Length > 8)
            {
                MessageBox.Show("Длина слова ограниченна 8 буквами");
                return;

            }

            for (var i = 0; i < MyText.Length; i++)
            {
                var ch = MyText[i];
                Geometry myGeom = myGlyph.GetGlyphOutline(myGlyph.CharacterToGlyphMap[ch], 40, 10);

                PathGeometry myPath = myGeom.GetOutlinedPathGeometry();
               // Path.Data = myPath;
                CalcServoAngles(_x, _y, Z+20);
                Thread.Sleep(500);
                DrawPathGeometry(myPath,i*22-45);
                CalcServoAngles(_x, _y, Z + 20);


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

        private void DrawPathGeometry(PathGeometry myPath, double x0)
        {
           
            

            foreach (var fig in myPath.Figures)
            {
                List<Point> points = new List<Point>();
                foreach (var seg in fig.Segments)
                {
                    if (seg is LineSegment ls)
                    {
                        double x = ls.Point.X - 50 + x0;
                        double y = ls.Point.Y;
                        points.Add(new Point(x,y));

                    }

                    if (seg is ArcSegment arcs)
                    {
                        
                        double x = arcs.Point.X - 50 + x0;
                        double y = arcs.Point.Y;
                        points.Add(new Point(x, y));

                    }

                    if (seg is PolyLineSegment pls)
                    {
                        foreach (var point in pls.Points)
                        {
                            double x = point.X - 50 + x0;
                            double y = point.Y;
                            points.Add(new Point(x, y));

                        }
                    }

                    if (seg is PolyBezierSegment pbs)
                    {
                        foreach (var point in pbs.Points)
                        {
                            double x = point.X - 50 + x0;
                            double y = point.Y;
                            points.Add(new Point(x, y));

                        }
                    }

                    if (seg is BezierSegment bs)
                    {
                        double x = bs.Point1.X - 50 + x0;
                        double y = bs.Point1.Y;
                        points.Add(new Point(x, y));

                        x = bs.Point2.X - 50 + x0;
                        y = bs.Point2.Y;
                        points.Add(new Point(x, y));

                        x = bs.Point3.X - 50 + x0;
                        y = bs.Point3.Y;
                        points.Add(new Point(x, y));

                    }
                }
                points.Add(points[0]);
                if (points.Count < 1) return;
                for (int i = 20; i >= 0; i--)
                {
                    CalcServoAngles(points[0].X, -points[0].Y + CLen + 20, Z + i);
                    Thread.Sleep(25);
                }
                
               
                for (var i = 0; i < points.Count-1; i++)
                {
                    DrawLine(points[i].X, points[i].Y, points[i+1].X, points[i+1].Y);
                }

                for (int i = 0; i < 21; i++)
                {
                    CalcServoAngles(points[0].X, -points[0].Y + CLen + 20, Z + i);
                    Thread.Sleep(25);
                }

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
                _y = -y0 * (1 - t) - y1 * t + CLen+20;
                CalcServoAngles(_x, _y , Z);
                Thread.Sleep(20);
            }
        }
    }
}
