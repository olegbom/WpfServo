using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Globalization;
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
using System.Windows.Controls.Primitives;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using HelixToolkit.Wpf;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using WpfColorFontDialog;
using WpfServo.Annotations;
using LineSegment = System.Windows.Media.LineSegment;

namespace WpfServo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {


        const double ALen = 150.0;
        const double BLen = 71.0;

        public double BetaAngle => (Slider1Value - 1000) * 0.09 / 2 - 90;
        public double GammaAngle => (Slider3Value - 1390) * 0.108 / 2 - 90;   // 0.108 * 2 + 1390

        public Model3D RoboHandBaseModel { get; set; }
        public Model3D RoboHandServo2Model { get; set; }
        public Model3D RoboHandShoulder1Model { get; set; }

        public TranslateTransform3D RoboHandShoulder1ModelTranlate { get; set; }

        private SerialPort _serialPort;

        private Timer _serialPortTimer;
        private bool _programClosing;

        public  MainWindow()
        {
           
            OpenComPortAsync();

            DispatcherTimer timer = new DispatcherTimer();
            timer.Interval = new TimeSpan(0, 0, 0, 0, 50);
            timer.Tick += (s, a) =>
            {
                if (MyLinesQueue.IsEmpty) return;
                while (MyLinesQueue.TryDequeue(out var myLine))
                {
                    Line line = new Line()
                    {
                        X1 = myLine.From.X,
                        Y1 = myLine.From.Y,
                        X2 = myLine.To.X,
                        Y2 = myLine.To.Y,
                        Stroke = Brushes.Black,
                        StrokeThickness = 1
                    };
                    MyCanvas.Children.Add(line);
                }

                    
            };
            timer.Start();
            ModelLoadAsync();


            InitializeComponent();

            DataContext = this;
           
        }

        private async void ModelLoadAsync()
        {
            RoboHandBaseModel = await Task.Factory.StartNew(() =>
            {
                var mi = new ModelImporter();
                return mi.Load(AppDomain.CurrentDomain.BaseDirectory + "Models\\MRobotHand.STL", null, true);

            });

            RoboHandServo2Model = await Task.Factory.StartNew(() =>
            {
                var mi = new ModelImporter();
                return mi.Load(AppDomain.CurrentDomain.BaseDirectory + "Models\\Servo2.STL", null, true);
                
            });
            Rect3D rect = RoboHandServo2Model.Bounds;
            RoboHandShoulder1ModelTranlate = new TranslateTransform3D(-rect.SizeX / 2, 55, -rect.SizeZ / 2);

        }

        public bool IsFindComPort { get; set; } = false;

        public string WindowTitle => IsFindComPort ? "Порт открыт" : "Порт не найден";
        public bool IsScriptCorrect { get; set; } = false;
        public Visibility ButtonRunVisible => IsScriptCorrect ? Visibility.Visible : Visibility.Hidden;
        public string CompilingInfo { get; set; } = "";

        private SerialPort FindComPort()
        {
            SerialPort serialPort = null;
            while (!_programClosing)
            {
                ManagementObjectSearcher searcher =
                    new ManagementObjectSearcher("root\\CIMV2",
                        "SELECT * FROM Win32_SerialPort");

                foreach (ManagementObject queryObj in searcher.Get())
                {
                    string name = queryObj["Name"].ToString();
                    if (name.Contains("STLink Virtual COM Port"))
                    {
                        serialPort = new SerialPort(queryObj["DeviceID"].ToString(), 921600,
                            Parity.None,
                            8,
                            StopBits.One);
                        break;
                    }
                }

                if (serialPort != null) break;
                Thread.Sleep(1000);
            }

            IsFindComPort = true;
            return serialPort;
        }

        private async void OpenComPortAsync()
        {
            _serialPort = await Task.Run(() => FindComPort());
            _serialPort.Open();

            _serialPortTimer = new Timer((o) =>
            {
                SendServoValue(4, (UInt16)Slider5Value);
                SendServoValue(0, (UInt16)Slider1Value);
                SendServoValue(3, (UInt16)Slider4Value);
                SendServoValue(2, (UInt16)Slider3Value);
                SendServoValue(1, (UInt16)Slider2Value);
                SendServoValue(5, (UInt16)Slider6Value);
            }, null, 100, 20);
            
        }

        public double Slider1Value { get; set; } = 3000;

        //public void OnSlider1ValueChanged()
        //{
        //    SendServoValue(0, (UInt16)Slider1Value);
        //}

        public double Slider2Value { get; set; } = 3000;

        //public void OnSlider2ValueChanged()
        //{
        //    SendServoValue(1, (UInt16)Slider2Value);
        //}

        public double Slider3Value { get; set; } = 3000;

        //public void OnSlider3ValueChanged()
        //{
        //    SendServoValue(2, (UInt16)Slider3Value);
        //}

        public double Slider4Value { get; set; } = 3000;

        //public void OnSlider4ValueChanged()
        //{
        //    SendServoValue(3, (UInt16)Slider4Value);
        //}

        public double Slider5Value { get; set; } = 3000;

        //public void OnSlider5ValueChanged()
        //{
        //    SendServoValue(4, (UInt16)Slider5Value);
        //}

        public double Slider6Value { get; set; } = 3000;

        //public void OnSlider6ValueChanged()
        //{
        //    SendServoValue(5, (UInt16)Slider6Value);
        //}

        private void SendServoValue(int channel, UInt16 value)
        {
            byte servo = (byte)(channel + 31);
            byte a = (byte)(value >> 8);
            byte b = (byte)(0xFF & value);
            _serialPort?.Write(new [] { servo, a, b }, 0, 3);
        }
      

      

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _serialPort?.Close();
            _serialPortTimer?.Dispose();
            _programClosing = true;
        }
        
        double old1, old2;
        Point mDownPos;
        Point mUpPos;
        bool isDown;
      

        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var border = (Border) sender;
            mDownPos = e.GetPosition(border);

            var width = border.ActualWidth;
            var height = border.ActualHeight;

            _x = (mDownPos.X / width -0.5)*150;
            _y = (1-mDownPos.Y / height) * 100;

            isDown = true;
            CalcServoAngles(_x, _y, Z+5);
        }
        
        private void Border_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (isDown)
            {
                isDown = false;
                CalcServoAngles(_x, _y, Z+5);

            }
        }

        private void Border_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDown)
            {

                var canvas = (Border)sender;
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
            z = z + (y - 120) / 8;
            double beta = Math.Atan2(Math.Abs(y), x);

            double x1 = Math.Sqrt(x * x + y * y);

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


            Slider5Value = thetaTicks;
            Slider1Value = betaTicks;
            Slider4Value = phiTicks;
            Slider3Value = gammaTicks;

        }

        private double Z = 150, _x = 0, _y = 100, _pickUp = 0;

        private void Slider_ZChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Z = ((Slider) sender).Value;
          
            CalcServoAngles(_x, _y, Z);
        }



        public class MyLine
        {
            public Point From;
            public Point To;
        }
        

        public bool IsDrawing { get; set; }
        public bool IsButtonDrawEnabled => !IsDrawing;
        public ConcurrentQueue<MyLine> MyLinesQueue { get; set; } = new ConcurrentQueue<MyLine>();



        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            IsDrawing = true;
            MyCanvas.Children.Clear();
           

            FormattedText text = new FormattedText(MyTextBox.Text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(MyTextBox.FontFamily, MyTextBox.FontStyle, MyTextBox.FontWeight, MyTextBox.FontStretch),
                MyTextBox.FontSize,
                Brushes.Black);

            Task.Run(() =>
            {
                Geometry myGeom = text.BuildGeometry(new Point(-130, -120));

                PathGeometry myPath = myGeom.GetOutlinedPathGeometry();

                PickUpPen();
                Thread.Sleep(100);
                DrawPathGeometry(myPath);
                PickUpPen();
                IsDrawing = false;
            });


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

        private void DrawPathGeometry(PathGeometry myPath)
        {
            foreach (var fig in myPath.Figures.ToList().OrderBy(o => o.StartPoint.X))
            {
                
                List<Point> points = new List<Point>();
                foreach (var seg in fig.Segments)
                {
                    switch (seg)
                    {
                        case LineSegment ls:
                            points.Add(ls.Point);
                            break;
                        case ArcSegment arcs:
                            points.Add(arcs.Point);
                            break;
                        case PolyLineSegment pls:
                            foreach (var point in pls.Points)
                                points.Add(point);
                            break;
                        
                        case PolyBezierSegment pbs:
                            foreach (var point in pbs.Points)
                                points.Add(point);
                            break;
                        case BezierSegment bs:
                            points.Add(bs.Point1);
                            points.Add(bs.Point2);
                            points.Add(bs.Point3);
                            break;
                    }
                }
                points.Add(points[0]);

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
                    MyLinesQueue.Enqueue(new MyLine{From = points[i], To=points[i+1]});
                    DrawLine(points[i].X, points[i].Y, points[i+1].X, points[i+1].Y);
                }
                _pickUp = 50;
                for (int i = 0; i < 21; i++)
                {
                    CalcServoAngles(points[0].X, -points[0].Y, Z + i);
                    Thread.Sleep(25);
                }
                _pickUp = 0;
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
        public class Globals
        {
            public SerialPort serialPort;

            public void TestMethod()
            {
                MessageBox.Show("Запуск из скрипта!", "test");
            }
        }

        private Script _script;
        private void ButtonCompile_OnClick(object sender, RoutedEventArgs e)
        {
            _script = CSharpScript.Create(textEditor.Text, globalsType: typeof(Globals));
            var diagnostics = _script.Compile();
            string message = "";
            foreach(var diagnostic in diagnostics)
            { 
                message += diagnostic + Environment.NewLine;
            }

            if (!String.IsNullOrEmpty(message))
            {
                CompilingInfo = message;
            }
            else
            {
                CompilingInfo = "Скрипт успешно скомпилирован";
                IsScriptCorrect = true;
            }
        }

        private void ButtonRun_OnClick(object sender, RoutedEventArgs e)
        {
            Globals newGlobals = new Globals(){serialPort = _serialPort};
            _script.RunAsync(globals: newGlobals);
        }

        private void TextEditor_OnTextChanged(object sender, EventArgs e)
        {
            IsScriptCorrect = false;
        }

        private void ButtonFontChoise_OnClick(object sender, RoutedEventArgs e)
        {
            ColorFontDialog dialog = new ColorFontDialog(true, true, false);
       
            dialog.Font = FontInfo.GetControlFont(MyTextBox);
            bool val = dialog.ShowDialog() ?? false;
            if (val)
            {
                FontInfo font = dialog.Font;
                if (font != null)
                {
                    FontInfo.ApplyFont(MyTextBox, font);
                }
            }
           
        }
    }
}
