using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Ink;
using System.IO;
using System.Collections.Specialized;

namespace GestureUI3._0
{
    ///GestureUI by Joe Wileman
    ///10-03-16 CAP6105:Pen-based User Interfaces
    public partial class MainWindow : Window
    {
        ShapeRecognizer shapeRecognizer;
        //StylusPointCollection newStroke;

        public MainWindow()
        {
            InitializeComponent();
            shapeRecognizer = new ShapeRecognizer();
        }

        private void myInkCanvas_StrokeCollected(object sender, InkCanvasStrokeCollectedEventArgs e)
        {
            var shapeFound = shapeRecognizer.Recognize(e.Stroke);
            int cornersFound = FindCorners(e.Stroke).Count;
            MessageBox.Show("Shape: " + shapeFound + "\nCorners Found: " + cornersFound, "Recognized", MessageBoxButton.OK, MessageBoxImage.None);
        }

        //Returns a list of corners from the given stroke
        public static List<Point> FindCorners(Stroke stroke)
        {
            double resampleSpace = DetermineResampleSpacing(stroke);

            List<Point> resampledPoints = ResamplePoints(stroke, resampleSpace);

            StylusPointCollection newPoints = new StylusPointCollection();
            foreach (Point point in resampledPoints)
            {
                newPoints.Add(new StylusPoint(point.X, point.Y));
            }
            stroke.StylusPoints = newPoints;

            List<Point> corners = GetCorners(resampledPoints);

            return corners;
        }

        //Determines the interspacing pixel distance between resampled points
        private static double DetermineResampleSpacing(Stroke stroke)
        {
            Point topLeft = stroke.GetBounds().TopLeft;
            Point bottomRight = stroke.GetBounds().BottomRight;

            double diagonal = Distance(bottomRight, topLeft);

            return diagonal / 40.0;
        }

        //Resamples the points in a stroke to be interspaced S pixel
        //distance away from each other
        private static List<Point> ResamplePoints(Stroke stroke, double S)
        {
            double D = 0;

            List<Point> resampled = new List<Point>();
            resampled.Add(stroke.StylusPoints[0].ToPoint());

            for (int i = 1; i < stroke.StylusPoints.Count; i++)
            {
                Point point1 = stroke.StylusPoints[i - 1].ToPoint();
                Point point2 = stroke.StylusPoints[i].ToPoint();
                double distance = Distance(point1, point2);

                if (distance + D >= S)
                {
                    double a = (S - D) / distance;
                    Point q = new Point();
                    q.X = point1.X + (a * (point2.X - point1.X));
                    q.Y = point1.Y + (a * (point2.Y - point1.Y));
                    resampled.Add(q);
                    stroke.StylusPoints.Insert(i, new StylusPoint(q.X, q.Y));
                    D = 0;
                }
                else
                {
                    D += distance;
                }
            }
            return resampled;

        }

        //Modified to have a lower median constant
        private static List<Point> GetCorners(List<Point> points)
        {         
            List<Point> corners = new List<Point>();
            corners.Add(points[0]);
            int W = 3;
            List<double> straws = new List<double>();

            for (int i = W; i < points.Count - W; i++)
            {
                straws.Add(Distance(points[i - W], points[i + W]));
            }

            //6 t <- MEDIAN(straws) * 0:95
            //This constant was lowered to 0.9
            //I found this constant to be too high causing
            //straws close to the median to be considered corners
            //Lowering this increases the threshold for a corner
            double t = Median(straws) * 0.9;

            for (int i = W; i < points.Count - W; i++)
            {              
                if (straws[i - W] < t)
                {
                    double localMin = int.MaxValue;
                    int localMinIndex = i;

                    while (i < straws.Count && straws[i] < t)
                    {
                        if (straws[i] < localMin)
                        {
                            localMin = straws[i];
                            localMinIndex = i;
                        }
                        i++;
                    }
                    corners.Add(points[localMinIndex]);
                    Ellipse myEllipse = new Ellipse();
                    double left = points[localMinIndex].X - 10;
                    double top = points[localMinIndex].Y - 10;
                    myEllipse.Margin = new Thickness(left, top, 0, 0);
                    myEllipse.Width = 20;
                    myEllipse.Height = 20;
                    SolidColorBrush mySolidColorBrush = new SolidColorBrush();
                    mySolidColorBrush.Color = Color.FromArgb(255, 255, 255, 0);
                    myEllipse.Fill = mySolidColorBrush;
                    myEllipse.StrokeThickness = 2;
                }
            }
            corners.Add(points[points.Count - 1]);
            PostProcessCorners(points, ref corners, straws);
            return corners;
        }

        //Checks the corner candidates to see if any corners can be
        //removed or added based on higher-level polyline rules
        //Didn't have much luck with halfway corners so this version
        //just removes corners that form a line.
        //As well as a part I added that removes corners very close to 
        //each other.
        //I noticed, especially with mouse input, it was common to have
        //two corners very near each other where only one should exist.
        private static void PostProcessCorners(List<Point> points, ref List<Point> corners, List<double> straws)
        {
            //Remove corners that form a line
            for (int i = 1; i < corners.Count - 1; i++)
            {
                Point corner1 = corners[i - 1];
                Point corner2 = corners[i + 1];

                if (IsLine(points, corner1, corner2))
                {
                    corners.RemoveAt(i);
                    i--;
                }
            }

            //Remove close together corners
            if (corners.Count > 2)
            {
                List<Point> newCorners = new List<Point>();
                for (int i = 0; i < corners.Count - 1; i++)
                {
                    Point corner1 = corners[i];
                    Point corner2 = corners[i + 1];
                    double distance1_2 = Distance(corner1, corner2);
					
                    if (distance1_2 < 10.0)
                    {
                        Point newCorner = new Point((corner1.X + corner2.X) / 2,
                                                     (corner1.Y + corner2.Y) / 2);
                        newCorners.Add(newCorner);
                        i++;
                    }
                    else
                    {
                        newCorners.Add(corner1);
                    }
                    if (i == corners.Count - 2)
                    {
                        newCorners.Add(corners[i + 1]);
                    }
                }
                corners = newCorners;
            }
        }

        //Determines if the stroke segment between the points at indices
        //a and b form a line
        private static bool IsLine(List<Point> points, Point point1, Point point2)
        {
            double threshhold = 0.95;
            double distance = Distance(point1, point2);
            double pathDistance = PathDistance(points, point1, point2);
            if (distance / pathDistance > threshhold)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        // Computes the Euclidean path distance between the points
        // at indices a and b
        private static double PathDistance(List<Point> points, Point point1, Point point2)
        {
            double d = 0;
            int a = points.IndexOf(point1);
            int b = points.IndexOf(point2);
            for (int i = a; i < b; i++)
            {
                d += Distance(points[i], points[i + 1]);
            }
            return d;
        }

        #region Utility Functions

        // Computes the chord(Euclidean) distance between the points
        private static double Distance(Point point1, Point point2)
        {
            return Point.Subtract(point1, point2).Length;
        }

        private static double Median(List<double> list)
        {
            List<double> sortedList = new List<double>(list);
            sortedList.Sort();
            return sortedList[sortedList.Count / 2];
        }
        #endregion Utility Functions

        private void buttonClear_Click(object sender, RoutedEventArgs e)
        {
            myInkCanvas.Strokes.Clear();
        }

        private void buttonExit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}
