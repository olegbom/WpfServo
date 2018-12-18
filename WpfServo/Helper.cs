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

        public static bool PointInPolygon(double x, double y, List<Point> points)
        {
            // Get the angle between the point and the
            // first and last vertices.
            int maxPoint = points.Count - 1;
            double totalAngle = GetAngle(
                points[maxPoint].X, points[maxPoint].Y,
                x, y,
                points[0].X, points[0].Y);

            // Add the angles from the point
            // to each other pair of vertices.
            for (int i = 0; i < maxPoint; i++)
            {
                totalAngle += GetAngle(
                    points[i].X, points[i].Y,
                    x, y,
                    points[i + 1].X, points[i + 1].Y);
            }

            // The total angle should be 2 * PI or -2 * PI if
            // the point is in the polygon and close to zero
            // if the point is outside the polygon.
            return (Math.Abs(totalAngle) > 0.000001);
        }

        public static double GetAngle(double ax, double ay,
            double bx, double by, double cx, double cy)
        {
            // Get the dot product.
            double dotProduct = DotProduct(ax, ay, bx, by, cx, cy);

            // Get the cross product.
            double crossProduct = CrossProductLength(ax, ay, bx, by, cx, cy);

            // Calculate the angle.
            return Math.Atan2(crossProduct, dotProduct);
        }

        private static double DotProduct(double ax, double ay,
            double bx, double by, double cx, double cy)
        {
            // Get the vectors' coordinates.
            double bAx = ax - bx;
            double bAy = ay - by;
            double bCx = cx - bx;
            double bCy = cy - by;

            // Calculate the dot product.
            return (bAx * bCx + bAy * bCy);
        }

        public static double CrossProductLength(double ax, double ay,
            double bx, double by, double cx, double cy)
        {
            // Get the vectors' coordinates.
            double bAx = ax - bx;
            double bAy = ay - by;
            double bCx = cx - bx;
            double bCy = cy - by;

            // Calculate the Z coordinate of the cross product.
            return (bAx * bCy - bAy * bCx);
        }

        public static Point[] ClipLineWithPolygon(
            out bool starts_outside_polygon,
            Point point1, Point point2,
            List<Point> polygon_points)
        {
            // Make lists to hold points of
            // intersection and their t values.
            List<Point> intersections = new List<Point>();
            List<double> t_values = new List<double>();

            // Add the segment's starting point.
            intersections.Add(point1);
            t_values.Add(0f);
            starts_outside_polygon =
                !PointInPolygon(point1.X, point1.Y,
                    polygon_points);

            // Examine the polygon's edges.
            for (int i1 = 0; i1 < polygon_points.Count; i1++)
            {
                // Get the end points for this edge.
                int i2 = (i1 + 1) % polygon_points.Count;

                // See where the edge intersects the segment.
                bool lines_intersect, segments_intersect;
                Point intersection, close_p1, close_p2;
                double t1, t2;
                FindIntersection(point1, point2,
                    polygon_points[i1], polygon_points[i2],
                    out lines_intersect, out segments_intersect,
                    out intersection, out close_p1, out close_p2,
                    out t1, out t2);

                // See if the segment intersects the edge.
                if (segments_intersect)
                {
                    // See if we need to record this intersection.

                    // Record this intersection.
                    intersections.Add(intersection);
                    t_values.Add(t1);
                }
            }

            // Add the segment's ending point.
            intersections.Add(point2);
            t_values.Add(1f);

            // Sort the points of intersection by t value.
            Point[] intersections_array = intersections.ToArray();
            double[] t_array = t_values.ToArray();
            Array.Sort(t_array, intersections_array);

            // Return the intersections.
            return intersections_array;
        }

        public static Point[] ClipLineWithLineArray(
           Point point1, Point point2,
           List<Point> linesPoints)
        {
            // Make lists to hold points of
            // intersection and their t values.
            List<Point> intersections = new List<Point>();
            List<double> tValues = new List<double>();



            // Examine the polygon's edges.
            for (int i = 0; i < linesPoints.Count/2; i++)
            {
                // Get the end points for this edge.
                

                // See where the edge intersects the segment.
                FindIntersection(point1, point2,
                    linesPoints[i*2], linesPoints[i * 2+1],
                    out _, out var segmentsIntersect,
                    out var intersection, out _, out _,
                    out var t1, out _);

                // See if the segment intersects the edge.
                if (segmentsIntersect)
                {
                    // See if we need to record this intersection.

                    // Record this intersection.
                    intersections.Add(intersection);
                    tValues.Add(t1);
                }
            }


            // Sort the points of intersection by t value.
            Point[] intersectionsArray = intersections.ToArray();
            double[] tArray = tValues.ToArray();
            Array.Sort(tArray, intersectionsArray);

            // Return the intersections.
            return intersectionsArray;
        }

        public static Point ToPolar(this Point p)
        {
            return new Point(
                Math.Sqrt(p.X * p.X + p.Y * p.Y),
                Math.Atan2(-p.Y, p.X)
                );
        }

        public static Point ToDecart(this Point p)
        {
            return new Point(
                p.X * Math.Cos(p.Y),
                -p.X * Math.Sin(p.Y)
            );
        }

        private static void FindIntersection(
            Point p1, Point p2, Point p3, Point p4,
            out bool lines_intersect, out bool segments_intersect,
            out Point intersection,
            out Point close_p1, out Point close_p2, out double t1, out double t2)
        {
            // Get the segments' parameters.
            double dx12 = p2.X - p1.X;
            double dy12 = p2.Y - p1.Y;
            double dx34 = p4.X - p3.X;
            double dy34 = p4.Y - p3.Y;

            // Solve for t1 and t2
            double denominator = (dy12 * dx34 - dx12 * dy34);

            t1 =
                ((p1.X - p3.X) * dy34 + (p3.Y - p1.Y) * dx34)
                / denominator;
            t2 = 0;
            if (double.IsInfinity(t1))
            {
                // The lines are parallel (or close enough to it).
                lines_intersect = false;
                segments_intersect = false;
                intersection = new Point(float.NaN, float.NaN);
                close_p1 = new Point(float.NaN, float.NaN);
                close_p2 = new Point(float.NaN, float.NaN);
                return;
            }
            lines_intersect = true;

            t2 =
                ((p3.X - p1.X) * dy12 + (p1.Y - p3.Y) * dx12)
                / -denominator;

            // Find the point of intersection.
            intersection = new Point(p1.X + dx12 * t1, p1.Y + dy12 * t1);

            // The segments intersect if t1 and t2 are between 0 and 1.
            segments_intersect =
            ((t1 >= 0) && (t1 <= 1) &&
             (t2 >= 0) && (t2 <= 1));

            // Find the closest points on the segments.
            if (t1 < 0)
            {
                t1 = 0;
            }
            else if (t1 > 1)
            {
                t1 = 1;
            }

            if (t2 < 0)
            {
                t2 = 0;
            }
            else if (t2 > 1)
            {
                t2 = 1;
            }

            close_p1 = new Point(p1.X + dx12 * t1, p1.Y + dy12 * t1);
            close_p2 = new Point(p3.X + dx34 * t2, p3.Y + dy34 * t2);
        }

        public static MyLine[] ToLines(this Point[] points)
        {
            var linesCount = points.Length / 2;
            var result = new MyLine[linesCount];
            for (int i = 0; i < linesCount; i++)
            {
                result[i] = new MyLine{
                    From = points[i*2],
                    To = points[i*2 + 1]

                };
            }
            return result;
        }


    }
}
