using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using CsPotrace;

namespace WpfServo
{
    public static class Helper
    {
        public static Point ToPoint(this dPoint p)
        {
            return new Point(p.x, p.y);
        }

        public static void AddListOfPaths(this PathGeometry pathGeometry, List<List<Curve>> listOfPaths)
        {
            pathGeometry.Figures.Clear();

            foreach (var lc in listOfPaths)
            {
                PathFigure figure = new PathFigure()
                {
                    StartPoint = new Point(lc[0].A.x, lc[0].A.y),
                    IsClosed = true,
                    IsFilled = false
                };


                foreach (var c in lc)
                {
                    if (c.Kind == CurveKind.Line)
                    {
                        figure.Segments.Add(new LineSegment(new Point(c.B.x, c.B.y), true));
                    }
                    else
                    {
                        figure.Segments.Add(
                            new BezierSegment(
                                point1: new Point(c.ControlPointA.x, c.ControlPointA.y),
                                point2: new Point(c.ControlPointB.x, c.ControlPointB.y),
                                point3: new Point(c.B.x, c.B.y),
                                isStroked: true
                            )
                        );
                    }
                }
                pathGeometry.Figures.Add(figure);

            }


        }

        private static double X(double t,
            double x0, double x1, double x2, double x3)
        {
            return x0 * Math.Pow((1 - t), 3) +
                x1 * 3 * t * Math.Pow((1 - t), 2) +
                x2 * 3 * Math.Pow(t, 2) * (1 - t) +
                x3 * Math.Pow(t, 3);
        }
        private static double Y(double t,
            double y0, double y1, double y2, double y3)
        {
            return y0 * Math.Pow((1 - t), 3) +
                y1 * 3 * t * Math.Pow((1 - t), 2) +
                y2 * 3 * Math.Pow(t, 2) * (1 - t) +
                y3 * Math.Pow(t, 3);
        }

        public static List<Point> BezierPoints(int nPoints, Point pt0, Point pt1, Point pt2, Point pt3)
        {
            // Draw the curve.
            List<Point> points = new List<Point>(nPoints);
            for (int i = 1; i <= nPoints; i++)
            {
                double t = (double) i / nPoints;
                points.Add(new Point(
                    X(t, pt0.X, pt1.X, pt2.X, pt3.X),
                    Y(t, pt0.Y, pt1.Y, pt2.Y, pt3.Y)));
            }

            return points;
        }
    }
}
