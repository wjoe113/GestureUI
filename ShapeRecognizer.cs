using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;

namespace GestureUI3._0
{
    ///GestureUI by Joe Wileman
    ///10-03-16 CAP6105:Pen-based User Interfaces
    class ShapeRecognizer
    {
        private enum myShapes
        {
            None,
            Square,
            Rectangle,
            Triangle,
            Ellipse,
            Circle,
            Arrow
        }

        private static class Parameters
        {
            //Threshold for selecting a primitive
            public const double MinimumConfidence = 0.3; 
            //Distance corners can be to be considered closed
            public const double HookDistance = 0.1;
            //The penalty for having too many corners for that primitive
            public const double CornerOverflow = 0.25;
            //Since corner values can vary by 1-2 in error, lower the weight
            //so the confidence isn't as sensitive to 1-2 bad corners
            public const double CornerWeight = 0.5;
            //Ratio weight is only used to differeniate square/rectangle 
            //and circle/ellipses so lower the weight
            //The penalty for having too many/little intersections
            public const double RatioWeight = 0.5;
            public const double IntersectionOverflow = 0.25;
            //Like corners, intersections could be slightly off
            public const double IntersectionWeight = 0.5;
        }

        //Each recognizer follows this delegates structure
        delegate double RecognizeDelegate(Stroke stroke, List<Point> intersections, List<Point> corners);

        private Dictionary<myShapes, RecognizeDelegate> recognizers;

        public ShapeRecognizer()
        {
            //Add all of our recognizers
            recognizers = new Dictionary<myShapes, RecognizeDelegate>();
            recognizers.Add(myShapes.Square, new RecognizeDelegate(SquareRecognizer));
            recognizers.Add(myShapes.Rectangle, new RecognizeDelegate(RectangleRecognizer));
            recognizers.Add(myShapes.Triangle, new RecognizeDelegate(TriangleRecognizer));
            recognizers.Add(myShapes.Ellipse, new RecognizeDelegate(EllipseRecognizer));
            recognizers.Add(myShapes.Circle, new RecognizeDelegate(CircleRecognizer));
            recognizers.Add(myShapes.Arrow, new RecognizeDelegate(ArrowRecognizer));
        }

        //Takes in a stroke and processes to find intersections and corners.
        //Each recognizer then tries to build a confidence level, and the highest
        //above the threshold is considered a match.
        public string Recognize(Stroke stroke)
        {
            //Ignore tiny line strokes
            if(stroke.StylusPoints.Count < 3)
            {
                return myShapes.None.ToString();
            }

            //Find the intersections
            List<Point> intersections = FindIntersections(stroke);

            //Find the corners using ShortStraw
            List<Point> corners = MainWindow.FindCorners(stroke);

            //Save the guess confidence values of each recognizer
            double bestGuessValue = double.MinValue;
            myShapes bestGuess = myShapes.None;

            //Iterate through all myShapes and find a matching recognizer
            foreach (myShapes primitive in System.Enum.GetValues(typeof(myShapes)))
            {
                if (recognizers.ContainsKey(primitive))
                {
                    //Invoke the recognizer and check if it has the best guess
                    double result = recognizers[primitive].Invoke(stroke, intersections, corners);
                    if (result > bestGuessValue)
                    {
                        bestGuessValue = result;
                        bestGuess = primitive;
                    }
                }
            }

            //Only allow recognitions that meet the minimum confidence threshold
            if(bestGuessValue < Parameters.MinimumConfidence)
            {
                bestGuess = myShapes.None;
            }

            return bestGuess.ToString();
        }

        //Recognizers a square using quadrilateral heuristics and a ratio heuristic
        //The ratio of width/height will separate squares and rectangles
        private static double SquareRecognizer(Stroke stroke, List<Point> intersections, List<Point> corners)
        {
            double quadrilateralConfidence = QuadrilateralRecognizer(stroke, intersections, corners);
            double ratioConfidence = RatioConfidence(stroke, 1);
            double totalConfidence = (quadrilateralConfidence + ratioConfidence) / 5;
            return totalConfidence;
        }

        //Recognizers a rectangle using quadrilateral heuristics and a ratio heuristic
        //The ratio of width/height will separate squares and rectangles
        private static double RectangleRecognizer(Stroke stroke, List<Point> intersections, List<Point> corners)
        {
            double quadrilateralConfidence = QuadrilateralRecognizer(stroke, intersections, corners);
            double ratioConfidence = RatioConfidence(stroke, 0.6);
            double totalConfidence = (quadrilateralConfidence + ratioConfidence) / 5;
            return totalConfidence;
        }

        //Recognizers a quadrilateral using heuristics
        private static double QuadrilateralRecognizer(Stroke stroke, List<Point> intersections, List<Point> corners)
        {
            double closedConfidence = ClosedShapeConfidence(stroke);
            double cornerConfidence = CornerConfidence(corners, 5);
            double angleConfidence = AngleConfidence(corners, 90);
            double intersectionConfidence = IntersectionConfidence(intersections, 1);
            return closedConfidence + (cornerConfidence * Parameters.CornerWeight)  + 
                   angleConfidence + (intersectionConfidence * Parameters.IntersectionWeight);
        }

        //Recognizers a triangle using heuristics
        private static double TriangleRecognizer(Stroke stroke, List<Point> intersections, List<Point> corners)
        {
            double closedConfidence = ClosedShapeConfidence(stroke);
            double cornerConfidence = CornerConfidence(corners, 4);
            double angleConfidence = AngleConfidence(corners, 60);
            double intersectionConfidence = IntersectionConfidence(intersections, 1);
            double totalConfidence = (closedConfidence + (cornerConfidence * Parameters.CornerWeight) + angleConfidence + (intersectionConfidence * Parameters.IntersectionWeight)) / 4;
            return totalConfidence;
        }
        
        //Recognizers a circle using ellipse heuristics
        //The ratio of width/height will separate circles and ellipses
        private static double EllipseRecognizer(Stroke stroke, List<Point> intersections, List<Point> corners)
        {
            double totalConfidence = EllipsesRecognizer(stroke, intersections, corners, 0.6);            
            return totalConfidence;
        }

        // Recognizers a circle using ellipse heuristics
        // The ratio of width/height will separate circles and ellipses
        private static double CircleRecognizer(Stroke stroke, List<Point> intersections, List<Point> corners)
        {
            double totalConfidence = EllipsesRecognizer(stroke, intersections, corners, 1);
            return totalConfidence;
        }

        //Recognizers an ellipse using heuristics
        //"Ellipses" includes circles since a circle is an ellipse
        //Takes in a ratio parameter to separate circles from standard ellipses
        private static double EllipsesRecognizer(Stroke stroke, List<Point> intersections, List<Point> corners, double ratio)
        {
            double closedConfidence = ClosedShapeConfidence(stroke);
            double cornerConfidence = CornerConfidence(corners, 2);
            double angleConfidence = AngleConfidence(corners, 0);
            double intersectionConfidence = IntersectionConfidence(intersections, 1);
            double ratioConfidence = RatioConfidence(stroke, ratio);
            //Give closed confidence a high weight since ellipse must be closed
            //Its value is used to scale the other confidence values
            return closedConfidence * 
                   (((cornerConfidence * Parameters.CornerWeight) + angleConfidence +
                   (intersectionConfidence * Parameters.IntersectionWeight) + ratioConfidence)/ 4);
        }

        //Recognizers an arrow using heuristics
        //This is a single stroke arrow, a single stroke containing a line 
        //and a triangle, where the line starts at the tip of the triangle
        private static double ArrowRecognizer(Stroke stroke, List<Point> intersections, List<Point> corners)
        {
            //The arrow is not closed so invert the closed value
            double notClosedConfidence = 1 - ClosedShapeConfidence(stroke);
            double cornerConfidence = CornerConfidence(corners, 5);
            double angleConfidence = AngleConfidence(corners, 45);
            double intersectionConfidence = IntersectionConfidence(intersections, 2);
            double totalConfidence = (notClosedConfidence + (cornerConfidence * Parameters.CornerWeight) + angleConfidence + (intersectionConfidence * Parameters.IntersectionWeight)) / 4;
            return totalConfidence;
        }

        //Finds any self-intersections of a stroke
        private static List<Point> FindIntersections(Stroke stroke)
        {
            List<Point> intersections = new List<Point>();
            for (int i = 0; i < stroke.StylusPoints.Count - 1; i++)
            {
                for (int j = 0; j < stroke.StylusPoints.Count - 1; j++)
                {
                    //Line 1
                    Vector point1A = new Vector(stroke.StylusPoints[i].X, stroke.StylusPoints[i].Y);
                    Vector point1B = new Vector(stroke.StylusPoints[i+1].X, stroke.StylusPoints[i+1].Y);
                    //Line 2
                    Vector point2A = new Vector(stroke.StylusPoints[j].X, stroke.StylusPoints[j].Y);
                    Vector point2B = new Vector(stroke.StylusPoints[j+1].X, stroke.StylusPoints[j+1].Y);

                    //Make sure we don't check lines with the same points
                    if (!point1A.Equals(point2A) && !point1A.Equals(point2B) &&
                        !point1B.Equals(point2A) && !point1B.Equals(point2B))
                    {
                        //Create the lines, AB, CD
                        Vector line1 = point1B - point1A;
                        Vector line2 = point2B - point2A;

                        //Get the AC line to test for intersection
                        Vector line3 = point2A - point1A;

                        //Get the cross products
                        double cross1_2 = Vector.CrossProduct(line1, line2);
                        double cross1_3 = Vector.CrossProduct(line1, line3);

                        //Check for collinear lines
                        if(cross1_2 == 0 && cross1_3 == 0)
                        {
                            continue;
                        }

                        //Check for parellel lines
                        if( cross1_2 == 0 && cross1_3 != 0)
                        {
                            continue;
                        }
                        double t = Vector.CrossProduct(line3, line2) / cross1_2;
                        double u = Vector.CrossProduct(line3, line1) / cross1_2;

                        if (cross1_2 != 0 && 0 <= t && t <= 1 && 0 <= u && u <= 1)
                        {
                            //Intersection: p + t r = q + u s.
                            Vector intersection = point1A + t * line1;
                            intersections.Add(new Point(intersection.X, intersection.Y));
                        }
                    }
                }
            }
            return intersections;
        }

        //Gives a value on how confident it is a shape is closed
        private static double ClosedShapeConfidence(Stroke stroke)
        {
            //Get the hook distance by finding the distance between the start and end
            StylusPoint startPoint = stroke.StylusPoints[0];
            StylusPoint endPoint = stroke.StylusPoints[stroke.StylusPoints.Count - 1];
            double hookDistance = Distance(startPoint.ToPoint(), endPoint.ToPoint());         

            //Get the bounds of the stroke
            Point topLeft = stroke.GetBounds().TopLeft;
            Point bottomRight = stroke.GetBounds().BottomRight;
            double diagonal = Distance(bottomRight, topLeft);

            //Find how big the hook is compared to the size of the stroke
            double hookSize = hookDistance / diagonal;

            //Get the confidence based on the hook distance parameter
            //If the hook is bigger then the hook size parameter, sqaure the hook size to lower the confidence
            double confidence = (hookSize > (Parameters.HookDistance - 1)) ? 1 - hookSize : 1 - Math.Sqrt(hookSize);

            return confidence;
        }

        //Gives a value on how confident it is a shape
        //contains the target amount of corners
        private static double CornerConfidence(List<Point> corners, int target)
        {
            double cornerConfidence = 0;
            int nCorners = corners.Count;

            //Perfect match
            if (nCorners == target)
            {
                cornerConfidence = 1;
            }
            else if (nCorners < target)
            {
                //No underflow allowed
                cornerConfidence = 0;
            }
            else
            {
                //Scale overflow by the paramter value
                cornerConfidence = 1 - (Parameters.CornerOverflow * (nCorners - target));
                cornerConfidence = (cornerConfidence < 0) ? 0 : cornerConfidence;
            }
            return cornerConfidence;
        }

        //Gives a value on how confident it is a shape 
        //has corners made up of the target angle
        private static double AngleConfidence(List<Point> corners, int target)
        {
            List<double> angles = new List<double>();
            double averageAngle = 0;
            //Find the angle between each consecutive 3 corners
            for (int i = 0; i < corners.Count - 2; i++)
            {
                Point corner2 = corners[i];
                Point corner1 = corners[i+1];
                Point corner3 = corners[i+2];

                //Law of cosines arccos((P12^2 + P13^2 - P23^2) / (2 * P12 * P13))
                double distance1_2 = Distance(corner1, corner2);
                double distance1_3 = Distance(corner1, corner3);
                double distance2_3 = Distance(corner2, corner3);

                double numerator = Math.Pow(distance1_2, 2) + Math.Pow(distance1_3, 2) - Math.Pow(distance2_3, 2);
                double denomenator = 2 * distance1_2 * distance1_3;
                double angle = Math.Acos(numerator / denomenator) * (180 / Math.PI);

                angles.Add(angle);
                averageAngle += angle;
            }

            double foundAngle;
            if (angles.Count == 0)
            {
                foundAngle = 0;
            }
            else if (angles.Count > 2)
            {
                //Use median instead of average so 1-2 bad corners
                //won't throw off this value
                foundAngle = Median(angles);
            }
            else
            {
                foundAngle = averageAngle / angles.Count;
            }            

            //Scale our confidence value based on how 
            //far away it is from our target value
            double distance = Math.Abs(target - foundAngle);
            double confidence;
            if (distance == 0)
            {
                confidence = 1;
            }
            else
            {
                confidence = 1 / distance;
                confidence = (confidence > 1) ? 1 : confidence;
            }
            return confidence;
        }

        //Gives a value on how confident it is a shape matches the target aspect ratio
        private static double RatioConfidence(Stroke stroke, double target)
        {
            Rect bounds = stroke.GetBounds();
            double height = bounds.Height;
            double width = bounds.Width;

            double ratio = (height < width) ? height / width :
                                              width / height;
            double distance = Math.Abs(target - ratio);
            double confidence = 1 - (distance / target);
            return confidence;
        }

        //Gives a value on how confident it is a shape
        //contains the target amount of intersection
        private static double IntersectionConfidence(List<Point> intersections, int target)
        {
            double intersectionConfidence = 0;
            int nIntersections = intersections.Count;

            if (nIntersections == target)
            {
                intersectionConfidence = 1;
            }
            else
            {
                //Scale the confidence based on overflow/underflow parameter
                intersectionConfidence = 1 - (Parameters.IntersectionOverflow * Math.Abs(nIntersections - target));
                intersectionConfidence = (intersectionConfidence < 0) ? 0 : intersectionConfidence;
            }
            return intersectionConfidence;
        }

        //Euclidean distance between two points
        private static double Distance(Point point1, Point point2)
        {
            return Point.Subtract(point1, point2).Length;
        }

        //Median value of a list
        private static double Median(List<double> list)
        {
            List<double> sortedList = new List<double>(list);
            sortedList.Sort();
            return sortedList[sortedList.Count / 2];
        }
    }
}
