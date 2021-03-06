﻿using System;
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
using CsPotrace;
using HelixToolkit.Wpf;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using WpfColorFontDialog;
using WpfServo.Annotations;
using Brushes = System.Windows.Media.Brushes;
using LineSegment = System.Windows.Media.LineSegment;
using Point = System.Windows.Point;
using Polygon = System.Windows.Shapes.Polygon;

namespace WpfServo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {


        public double ALen { get; private set; } = 150.0;
        public double BLen { get; private set; } = 66.0;

        public double BetaAngle => (Slider1Value - 1000) * 0.09 / 2 - 90;
        public double GammaAngle =>  (Slider3Value - 1590) * 0.108 / 2 - 90 ;   // 0.108 * 2 + 1390
 
        public double PhiAngle => 90 - (Slider4Value - 1350) * 0.11 / 2 ;
        public double ThetaAngle => (Slider5Value - 1050) * 0.086 / 2 - 90;


        public double Beta
        {
            get => (Slider1Value - 1000)/ 180 * Math.PI * 0.09 / 2;
            set => Slider1Value = value * 180 / Math.PI / 0.09 * 2 + 1000;
        }

        public double Gamma
        {
            get => (Slider3Value - 1590)/ 180 * Math.PI * 0.108 / 2;
            set => Slider3Value = value * 180 / Math.PI / 0.108 * 2 + 1590;
        }

        public double Phi
        {
            get => (Slider4Value - 1350)/ 180 * Math.PI * 0.11 / 2;
            set => Slider4Value = value * 180 / Math.PI / 0.11 * 2 + 1350;
        }

        public double Theta
        {
            get => (Slider5Value - 1050)/ 180 * Math.PI * 0.086 / 2;
            set => Slider5Value = value * 180 / Math.PI / 0.086 * 2 + 1050;
        }

        
        public Model3D RoboHandBaseModel { get; set; }
        public Model3D RoboHandServo2Model { get; set; }
        public Model3D RoboHandShoulder1Model { get; set; }

        public TranslateTransform3D RoboHandShoulder1ModelTranlate { get; set; }

        public Point3DCollection Points3D {get; set;} = new Point3DCollection(5000);

        private bool _points3DClearRequire = false;

        private SerialPort _serialPort;

        private Timer _serialPortTimer;
        private bool _programClosing;

        public  MainWindow()
        {
            Points3D.Add(new Point3D(0,0,0));
            OpenComPortAsync();

            DispatcherTimer timer = new DispatcherTimer();
            timer.Interval = new TimeSpan(0, 0, 0, 0, 50);
            timer.Tick += (s, a) =>
            {
                if (!MyLinesQueue.IsEmpty) 
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
                if(!MyPoint3DQueue.IsEmpty)
                    while (MyPoint3DQueue.TryDequeue(out var point))
                    {
                        if (Points3D.Count > 4950 || _points3DClearRequire)
                        { 
                            var last = Points3D.Last();
                            Points3D.Clear();
                            Points3D.Add(last);
                            _points3DClearRequire = false;
                        }
                        Points3D.Add(point);
                        Points3D.Add(point);
                    }



            };
            timer.Start();
           // ModelLoadAsync();


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


            UInt16 oldSlider5Value = 0;
            UInt16 oldSlider1Value = 0;
            UInt16 oldSlider4Value = 0;
            UInt16 oldSlider3Value = 0;
            UInt16 oldSlider2Value = 0;
            UInt16 oldSlider6Value = 0;

            _serialPortTimer = new Timer(o =>
            {
                UInt16 newSlider5Value = (UInt16)Slider5Value;
                if (oldSlider5Value != newSlider5Value)
                {
                    SendServoValue(4, newSlider5Value);
                    oldSlider5Value = newSlider5Value;
                }

                UInt16 newSlider1Value = (UInt16)Slider1Value;
                if (oldSlider1Value != newSlider1Value)
                {
                    SendServoValue(0, newSlider1Value);
                    oldSlider1Value = newSlider1Value;
                }

                UInt16 newSlider4Value = (UInt16)Slider4Value;
                if (oldSlider4Value != newSlider4Value)
                {
                    SendServoValue(3, newSlider4Value);
                    oldSlider4Value = newSlider4Value;
                }

                UInt16 newSlider3Value = (UInt16)Slider3Value;
                if (oldSlider3Value != newSlider3Value)
                {
                    SendServoValue(2, newSlider3Value);
                    oldSlider3Value = newSlider3Value;
                }

                UInt16 newSlider2Value = (UInt16)Slider2Value;
                if (oldSlider2Value != newSlider2Value)
                {
                    SendServoValue(1, newSlider2Value);
                    oldSlider2Value = newSlider2Value;
                }

                UInt16 newSlider6Value = (UInt16)Slider6Value;
                if (oldSlider6Value != newSlider6Value)
                {
                    SendServoValue(5, newSlider6Value);
                    oldSlider6Value = newSlider6Value;
                }
              
            }, null, 100, 20);
            
        }

        public double Slider1Value { get; set; } = 3000;
        public double Slider2Value { get; set; } = 3000;
        public double Slider3Value { get; set; } = 3000;
        public double Slider4Value { get; set; } = 3000;
        public double Slider5Value { get; set; } = 3000;
        public double Slider6Value { get; set; } = 3000;


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

        private Point mDownPos;
        private Point mMovePos;
        private bool isMouseDown;
        private bool isGhostTextDown;
        private Point oldGhostTextDownPos;
        private bool isEdgePathDown;
        private Point oldEdgePathDownPos;

        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
             mDownPos = e.GetPosition(MyCanvas);

            if (!isGhostTextDown && !isGhostTextDown)
            {
                MoveTo(mDownPos.X, -mDownPos.Y, Z);
            }

            isMouseDown = true;
        }
        
        private void Border_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!isGhostTextDown && !isGhostTextDown)
            {
                MoveTo(X, Y, Z);
            }
            isMouseDown = false;
            isGhostTextDown = false;
            isEdgePathDown = false;
        }

        private void Border_MouseMove(object sender, MouseEventArgs e)
        {
            if (isGhostTextDown)
            {
                mMovePos = e.GetPosition(MyCanvas);
                TextPosX = oldGhostTextDownPos.X - mDownPos.X + mMovePos.X;
                TextPosY = oldGhostTextDownPos.Y - mDownPos.Y + mMovePos.Y;
            }
            else if (isEdgePathDown)
            {
                mMovePos = e.GetPosition(MyCanvas);
                EdgePathPosX = oldEdgePathDownPos.X - mDownPos.X + mMovePos.X;
                EdgePathPosY = oldEdgePathDownPos.Y - mDownPos.Y + mMovePos.Y;
            }
            else if (isMouseDown)
            {
                mMovePos = e.GetPosition(MyCanvas);
                MoveTo(mMovePos.X, -mMovePos.Y, Z);
            }
        }

        private void GhostTextBlock_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            mDownPos = e.GetPosition(MyCanvas);
            oldGhostTextDownPos = new Point(TextPosX, TextPosY);
            isGhostTextDown = true;
        }

        private void EdgePath_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            mDownPos = e.GetPosition(MyCanvas);
            oldEdgePathDownPos = new Point(TextPosX, TextPosY);
            isEdgePathDown = true;
        }


        private void Border_OnMouseLeave(object sender, MouseEventArgs e)
        {
            isGhostTextDown = false;
            isEdgePathDown = false;
            isMouseDown = false;
        }

        public void MoveTo(double x, double y, double z)
        {

            _x = x;
            _y = y;
            _z = z;
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
           
            double betaTicks = beta * 180 / Math.PI / 0.09 * 2 + 1000;
            double gammaTicks = gamma * 180 / Math.PI / 0.108 * 2 + 1590;
            double phiTicks = phi * 180 / Math.PI / 0.11 * 2 + 1350;
            double thetaTicks = theta * 180 / Math.PI / 0.086 * 2 + 1050;

            Slider5Value = thetaTicks;
            Slider1Value = betaTicks;
            Slider4Value = phiTicks;
            Slider3Value = gammaTicks;

            /*
            RotateTransform3D transform1 = new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 1, 0), BetaAngle));
            RotateTransform3D transform2 = new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(1, 0, 0), PhiAngle));
            TranslateTransform3D transform3 = new TranslateTransform3D(0, ALen, 0);

            RotateTransform3D transform4 = new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(1,0,0), GammaAngle));
            TranslateTransform3D transform5 = new TranslateTransform3D(0, 0, BLen);
            RotateTransform3D transform6 = new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(1,0,0), ThetaAngle));
            TranslateTransform3D transform7 = new TranslateTransform3D(0, -40, 0);
            Transform3DGroup generalTransform = new Transform3DGroup();
            
            generalTransform.Children.Add(transform7);
            generalTransform.Children.Add(transform6);
            generalTransform.Children.Add(transform5);
            generalTransform.Children.Add(transform4);
            generalTransform.Children.Add(transform3);
            generalTransform.Children.Add(transform2);
            generalTransform.Children.Add(transform1);*/
            // MyPoint3DQueue.Enqueue(new Point3D(generalTransform.Value.OffsetX, generalTransform.Value.OffsetY, generalTransform.Value.OffsetZ));
            
            MyPoint3DQueue.Enqueue(FormAngleToCoordinate());
            //MyPoint3DQueue.Enqueue(new Point3D(-x, z-40, y));
        }

        private Point3D FormAngleToCoordinate()
        {


            double beta = BetaAngle / 180 * Math.PI;
            double gamma = GammaAngle / 180 * Math.PI;
            double phi = PhiAngle / 180 * Math.PI;


            double cosGammaPhi = Math.Cos(gamma + phi);
            double a1 = BLen * cosGammaPhi + ALen * Math.Sin(phi);
            double x = Math.Sin(beta) * a1;
            double z = Math.Cos(beta) * a1;
            double y = ALen * Math.Cos(phi) - BLen * Math.Sin(gamma + phi);

            //  sin(beta)*(BLen*cos(gamma + phi) + ALen*sin(phi))
            // ALen* cos(phi) -BLen * sin(gamma + phi)
            // cos(beta) * (BLen * cos(gamma + phi) + ALen * sin(phi))


            return new Point3D(x,y,z);
        }


        private double _x;
        public double X
        {
            get => _x;
            set => MoveTo(value, _y, _z);
        }

        private double _y = 130;
        public double Y
        {
            get => _y;
            set => MoveTo(_x, value, _z);
        }

        private double _z = 100;
        public double Z
        {
            get => _z;
            set => MoveTo(_x, _y, value);
        }
        



        public double BorderDiameter { get; private set; }
        public double BorderRadiusMinus { get; private set; }

        private void Slider_ZChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Z = ((Slider) sender).Value;
            double l = ALen + BLen;
            double r = Math.Sqrt(l * l - Z * Z);

            BorderDiameter = r*2;
            BorderRadiusMinus = -r;

        }

        public double TextPosX { get; set; } = -130;
        public double TextPosY { get; set; } = -180;

        public double EdgePathPosX { get; set; } = -130;
        public double EdgePathPosY { get; set; } = -180;


        public bool IsDrawing { get; private set; }
        public bool IsButtonDrawEnabled => !IsDrawing;
        public ConcurrentQueue<MyLine> MyLinesQueue { get; } = new ConcurrentQueue<MyLine>();
        public ConcurrentQueue<Point3D> MyPoint3DQueue { get; } = new ConcurrentQueue<Point3D>();

        public void Points3DClear()
        {
            _points3DClearRequire = true;
        }

        public void CanvasClear()
        {
            MyCanvas.Children.Clear();
            MyCanvas.Children.Add(GhostTextBlock);
            MyCanvas.Children.Add(BorderEllipse);
            MyCanvas.Children.Add(BaseEllipse);
            MyCanvas.Children.Add(EdgePath);
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            IsDrawing = true;
            CanvasClear();
            Point3D point = Points3D.Last();
            Points3D.Clear();
            Points3D.Add(point);

            FormattedText text = new FormattedText(MyTextBox.Text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(MyTextBox.FontFamily, MyTextBox.FontStyle, MyTextBox.FontWeight, MyTextBox.FontStretch),
                MyTextBox.FontSize,
                Brushes.Black);


            Task.Run(() =>
            {
                Geometry myGeom = text.BuildGeometry(new Point(TextPosX, TextPosY));

                PathGeometry myPath = myGeom.GetOutlinedPathGeometry();


                DrawPathGeometry(myPath);

                if (ListOfEdgePaths != null)
                    DrawListOfPolygons(ListOfEdgePaths, EdgePathPosX, EdgePathPosY, EdgePathScale);

                IsDrawing = false;

            });


        }

        private void DrawPolygon(List<Point> testPoints)
        {
            PenUp();
            Thread.Sleep(100);

            
            List<MyLine> lines = new List<MyLine>();
            for (double phi = Math.PI / 4; phi <= 3 * Math.PI / 4; phi += Math.PI / 360)
            {
                Point p1 = new Point(100 * Math.Cos(phi), -100 * Math.Sin(phi));
                Point p2 = new Point(300 * Math.Cos(phi), -300 * Math.Sin(phi));
                Point[] dashed = Helper.ClipLineWithLineArray(p1, p2, testPoints);


                if (dashed.Length > 0 && dashed.Length % 2 == 0)
                {
                    lines.AddRange(dashed.ToLines());
                }
            }

           

            foreach (var line in lines)
            {
                SmoothMoveTo(line.From);
                PenDown();
                SmoothMoveTo(line.To);
                PenUp();
            }


            List<Point> polarPoints = testPoints.Select(p => p.ToPolar()).ToList();

           

            for (double r = 100; r <= 300; r+=2)
            {
                Point p1 = new Point(r, 0);
                Point p2 = new Point(r, Math.PI);
                Point[] dashed = Helper.ClipLineWithLineArray(p1, p2, polarPoints);
                
                for (int j = 0; j < dashed.Length / 2; j++)
                {

                    SmoothMoveTo(dashed[j * 2].ToDecart());
                    PenDown();
                    DrawArc(dashed[j*2].X, dashed[j * 2].Y, dashed[j * 2+1].Y,25);
                    PenUp();
                }
            }



            PenDown();
        }

        public void PenUp()
        {
            for (int i = 0; i < 21; i += 2)
            {
                Z += 2;
                Thread.Sleep(25);
            }
        }
        public void PenDown()
        {
            for (int i = 20; i >= 0; i -= 2)
            {
                Z -= 2;
                Thread.Sleep(25);
            }
        }

        private void DrawPathGeometry(PathGeometry myPath, double dx = 0, double dy = 0, double scale = 1)
        {
            PenUp();
            Thread.Sleep(100);
            foreach (var fig in myPath.Figures.ToList().OrderBy(o => o.StartPoint.X))
            {
               
                List<Point> points = new List<Point>();
                points.Add(fig.StartPoint);
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

                            Point lastPoint = points.Last();
                            double bdx = lastPoint.X - bs.Point3.X;
                            double bdy = lastPoint.Y - bs.Point3.X;

                            double len = Math.Sqrt(bdx * bdx + bdy * bdy) / 4;

                            List<Point> bezierPoints = Helper.BezierPoints(
                                (int)len, lastPoint,
                                bs.Point1,
                                bs.Point2,
                                bs.Point3
                            );

                            points.AddRange(bezierPoints);
                            break;
                    }
                }

                points = points.Select(p => new Point(p.X * scale + dx, p.Y * scale + dy)).ToList();
                points.Add(points[0]);

                SmoothMoveTo(points[0]);
                PenDown();
                
                for (var i = 0; i < points.Count-1; i++)
                {
                    MyLinesQueue.Enqueue(new MyLine{From = points[i], To=points[i+1]});
                    DrawLine(points[i], points[i+1]);
                    Thread.Sleep(25);
                }

                PenUp();


            }

            PenDown();

        }


        private void DrawListOfPolygons(List<List<Curve>> listOfPaths, double dx = 0, double dy = 0, double scale = 1)
        {
         

            List<Point> points = new List<Point>();
            foreach (var lc in listOfPaths)
            {
                points.Add(new Point(lc[0].A.x, lc[0].A.y));

                foreach (var c in lc)
                {
                    if (c.Kind == CurveKind.Line)
                    {
                        Point p = new Point(c.B.x, c.B.y);
                        points.Add(p);
                        points.Add(p);
                    }
                    else
                    {
                        double bdx = c.A.x - c.B.x;
                        double bdy = c.A.y - c.B.y;

                        double len = Math.Sqrt(bdx * bdx + bdy * bdy) / 4;
                        List<Point> bezierPoints = Helper.BezierPoints(
                            (int) len, c.A.ToPoint(),
                            c.ControlPointA.ToPoint(),
                            c.ControlPointB.ToPoint(),
                            c.B.ToPoint()
                        );

                        foreach (var bezierPoint in bezierPoints)
                        {
                            points.Add(bezierPoint);
                            points.Add(bezierPoint);
                        }

                    }
                }

                points.RemoveAt(points.Count-1);
            }

            points = points.Select(p => new Point(p.X * scale + dx, p.Y * scale + dy)).ToList();
            DrawPolygon(points);

        }

        private void DrawListOfEdgePaths(List<List<Curve>> listOfPaths, double dx = 0, double dy = 0, double scale = 1)
        {
            PenUp();
            Thread.Sleep(100);
            foreach (var lc in listOfPaths)
            {
                List<Point> points = new List<Point>();
                points.Add(new Point(lc[0].A.x, lc[0].A.y));

                foreach (var c in lc)
                {
                    if (c.Kind == CurveKind.Line)
                    {
                        points.Add(new Point(c.B.x, c.B.y));
                    }
                    else
                    {
                        double bdx = c.A.x - c.B.x;
                        double bdy = c.A.y - c.B.y;

                        double len = Math.Sqrt(bdx * bdx + bdy * bdy) / 4;


                        points.AddRange(
                            Helper.BezierPoints(
                                (int)len, c.A.ToPoint(),
                                c.ControlPointA.ToPoint(),
                                c.ControlPointB.ToPoint(),
                                c.B.ToPoint()
                                )
                        );
                    }
                }

                points = points.Select(p => new Point(p.X * scale + dx, p.Y * scale + dy)).ToList();
              
                points.Add(points[0]);


                if (points.Count < 1) return;

                SmoothMoveTo(points[0]);

                PenDown();


                for (var i = 1; i < points.Count; i++)
                {

                    MyLinesQueue.Enqueue(new MyLine { From = points[i - 1], To = points[i] });
                    SmoothMoveTo(points[i]);
                    
                    Thread.Sleep(25);
                }

                PenUp();
            }
            PenDown();


        }



        public void DrawLine(MyLine line, int msDelay = 10) => DrawLine(line.From, line.To, msDelay);
        private void DrawLine(Point p0, Point p1, int msDelay = 10)
        {
            if (p0.X.Equals(double.NaN)) return;
            if (p0.X < -250 || p0.Y < -250 || p1.X < -250 || p1.Y < -250) return;
            double leng = Math.Sqrt(Math.Pow(p1.X - p0.X, 2) + Math.Pow(p1.Y - p0.Y, 2));
            for (double t = 0; t <= 1; t += 1 / leng)
            {
                double x = p0.X * (1 - t) + p1.X * t;
                double y = -p0.Y * (1 - t) - p1.Y * t;
                MoveTo(x, y, Z);
                Thread.Sleep(msDelay);
            }
        }

        public void DrawArc(double r, double phi1, double phi2, int msDelay = 10)
        {
           
            
            double delta = Math.PI / 360 * Math.Sign(phi2 - phi1);
            if (Math.Abs(delta) < 1e-10) return;
            for (double phi = phi1; phi <= phi2; phi += delta)
            {
                double x = r * Math.Cos(phi);
                double y = r * Math.Sin(phi);
                MoveTo(x, y, Z);
                Thread.Sleep(msDelay);
            }
        }

        public void SmoothMoveTo(double x, double y, double z, int msDelay = 10)
        {
            double x0 = X;
            double y0 = Y;
            double z0 = Z;
            double dx = x0 - x;
            double dy = y0 - y;
            double dz = z0 - z;
            double leng = Math.Sqrt(dx*dx + dy*dy + dz*dz);
            for (double t = 0; t <= 1; t += 1 / leng)
            {
                double ax = x0 * (1 - t) + x * t;
                double ay = y0 * (1 - t) + y * t;
                double az = z0 * (1 - t) + z * t;
                MoveTo(ax, ay, az);
                Thread.Sleep(msDelay);
            }
        }




        private void SmoothMoveTo(Point p, int msDelay = 10)
        {
            DrawLine(new Point(X, -Y), p, msDelay);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
       

        public void Delay(int ms)
        {
            Thread.Sleep(ms);
        }

        private Script _script;
        private void ButtonCompile_OnClick(object sender, RoutedEventArgs e)
        {
            _script = CSharpScript.Create(textEditor.Text,
                globalsType: typeof(MainWindow));
            
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
             Task.Run( () =>  _script.RunAsync(globals: this));
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

        public double EdgePathScale { get; set; } = 1.0;

        private List<List<Curve>> ListOfEdgePaths;
        

        private void MenuAddImage_OnClick(object sender, RoutedEventArgs e)
        {
            ImageToEdgeWindow window = new ImageToEdgeWindow();
            if (window.ShowDialog() == true)
            {
                ListOfEdgePaths = ImageToEdgeWindow.ListOfPaths;
                EdgePathGeometry.AddListOfPaths(ListOfEdgePaths);
            }
        }

       
    }
}
