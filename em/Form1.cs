using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Emgu.CV.Util;
using System.Globalization;
using Emgu.CV.Features2D;
using System.Security.Cryptography.X509Certificates;
using System.CodeDom;
using System.Drawing.Text;
using System.Net;

namespace em
{
    public partial class Form1 : Form
    {
        
        public Form1()
        {
            InitializeComponent();
        }
        
        private void closeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {

                OpenFileDialog open = new OpenFileDialog();
                if (open.ShowDialog() != DialogResult.OK) return;

                Image<Bgr, byte> imgInput = new Image<Bgr, byte>(open.FileName);
                pictureBox1.Image = imgInput.ToBitmap();

                Image<Gray, byte> imgGray = imgInput.Convert<Gray, byte>();

                Image<Gray, byte> imgBinarize = imgGray.ThresholdBinaryInv(new Gray(100), new Gray(255));
                pictureBox1.Image = imgBinarize.ToBitmap();

                // Find Contours
                var contours = new Emgu.CV.Util.VectorOfVectorOfPoint();
                var hierarchy = new Mat();
                CvInvoke.FindContours(imgBinarize, contours, hierarchy, Emgu.CV.CvEnum.RetrType.External, Emgu.CV.CvEnum.ChainApproxMethod.ChainApproxSimple);

                VectorOfPoint approx = new VectorOfPoint();
                Dictionary<int, double> shapes = new Dictionary<int, double>();

                for (int i = 0; i < contours.Size; i++)
                {
                    approx.Clear();
                    double perimeter = CvInvoke.ArcLength(contours[i], true);
                    CvInvoke.ApproxPolyDP(contours[i], approx, 0.04 * perimeter, true);
                    double area = CvInvoke.ContourArea(contours[i]);
                    if (approx.Size == 4)
                    {
                        shapes.Add(i, area);
                        Point[] points = approx.ToArray();

                        Point[] sortedByX = points.OrderBy(p => p.X).ThenBy(p => p.Y).ToArray();
                        Point leftTop = sortedByX[0];
                        Point leftBottom = sortedByX[1];
                        Point rightTop = sortedByX[2];
                        Point rightBottom = sortedByX[3];

                        //Calculate midpoint
                        Point midpointLeft = new Point((leftTop.X + leftBottom.X) / 2, (leftTop.Y + leftBottom.Y) / 2);
                        Point midpointRight = new Point((rightTop.X + rightBottom.X) / 2, (rightTop.Y + rightBottom.Y) / 2);


                        // Define the size of the square
                        int squareSize = 40;

                        // Draw squares around the midpoints for visualization
                        Rectangle squareLeft = new Rectangle(midpointLeft.X - squareSize / 2, midpointLeft.Y - squareSize / 2, squareSize, squareSize);
                      

                        CvInvoke.Rectangle(imgInput, squareLeft, new MCvScalar(200,200,230), 2); // Draw the left square
                       
                        LineSegment2D[] rectangleEdges = { new LineSegment2D(leftTop, leftBottom), new LineSegment2D(leftBottom, rightBottom), new LineSegment2D(rightBottom, rightTop), new LineSegment2D(rightTop, leftTop) };

                        // Check for intersection with each side of the squares
                        foreach (Rectangle square in  new[]{squareLeft })          
                        {
                            Point[] squareCorners = new Point[]
                            {new Point(square.Left, square.Top),new Point(square.Right, square.Top),new Point(square.Right, square.Bottom),new Point(square.Left, square.Bottom)};

                            // Create lines for the square edges
                            LineSegment2D[] squareEdges = {new LineSegment2D(squareCorners[0], squareCorners[1]), // Top edge
                                                           new LineSegment2D(squareCorners[1], squareCorners[2]), // Right edge
                                                           new LineSegment2D(squareCorners[2], squareCorners[3]), // Bottom edge
                                                           new LineSegment2D(squareCorners[3], squareCorners[0])};  // Left edge

                            List<Point> intersectionPoints = new List<Point>();

                            foreach (LineSegment2D squareEdge in squareEdges)
                            {
                                foreach (LineSegment2D rectEdge in rectangleEdges)
                                {
                                    if (FindIntersection(squareEdge, rectEdge, out Point intersection))
                                    {
                                        // Draw the intersection point
                                        CvInvoke.Circle(imgInput, intersection, 1, new MCvScalar(255, 0, 255), -1);
                                        MessageBox.Show($"Intersection at: {intersection}");

                                        // Add the intersection point to the list
                                        intersectionPoints.Add(intersection);
                                    }
                                }
                            }
                            if (intersectionPoints.Count == 2)
                            {
                                CvInvoke.Line(imgInput, intersectionPoints[0], intersectionPoints[1], new MCvScalar(255, 0, 255), 2);

                                // Calculate the midpoint of the intersection line
                                Point intersectMidpoint = new Point(
                                    (intersectionPoints[0].X + intersectionPoints[1].X) / 2,
                                    (intersectionPoints[0].Y + intersectionPoints[1].Y) / 2
                                );
                                
                                // Calculate perpendicular slope
                                double gradient = CalculateGradient(intersectionPoints[0], intersectionPoints[1]);
                                
                                Point endPoint1, endPoint2;
                                if (gradient == 0 || double.IsInfinity(gradient)) // Horizontal and vertical
                                {
                                    // Create a vertical line by varying the y-coordinates
                                    endPoint1 = midpointLeft;
                                    endPoint2 = midpointRight;
                                    double lineWidth = CalculatelineWidth(endPoint1, endPoint2);
                                    MessageBox.Show("Perpendicular Line Width: " + lineWidth);
                                }                              
                                else
                                {
                                    double invGradient = -1 / gradient;
                                    double interceptMid = intersectMidpoint.Y - (invGradient * intersectMidpoint.X);

                                    // Calculate the intersection point with the right edge of the rectangle
                                    double rectGradient = CalculateGradient(rightTop, rightBottom);
                                    double interceptRect = rightTop.Y - (rectGradient * rightTop.X);

                                    double xIntercept = (interceptRect - interceptMid) / (invGradient - rectGradient);
                                    double yIntercept = (invGradient * xIntercept) + interceptMid;

                                    endPoint1 = intersectMidpoint;
                                    endPoint2 = new Point((int)xIntercept, (int)yIntercept);
                                    double lineWidth = CalculatelineWidth(endPoint1, endPoint2);
                                    MessageBox.Show("Perpendicular Line Width: " + lineWidth);
                                }

                                CvInvoke.Line(imgInput, endPoint1, endPoint2, new MCvScalar(0, 255, 0), 2);

                            }
                          
                        }

                    }
                    break;
                }
                
                pictureBox1.Image = imgInput.ToBitmap();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

        }
        
        private double CalculateGradient(Point P1, Point P2)
        {
            return (double)(P2.Y - P1.Y) / (P2.X - P1.X);
        }
        private double CalculatelineWidth(Point P1, Point P2)
        {
            return Math.Sqrt(Math.Pow(P2.X - P1.X, 2) + Math.Pow(P2.Y - P1.Y, 2));
        }
        private bool FindIntersection(LineSegment2D line1, LineSegment2D line2, out Point intersection)
        {
            float a1 = line1.P2.Y - line1.P1.Y;
            float b1 = line1.P1.X - line1.P2.X;
            float c1 = a1 * line1.P1.X + b1 * line1.P1.Y;

            float a2 = line2.P2.Y - line2.P1.Y;
            float b2 = line2.P1.X - line2.P2.X;
            float c2 = a2 * line2.P1.X + b2 * line2.P1.Y;

            float determinant = a1 * b2 - a2 * b1;

            if (determinant == 0)
            {
                // The lines are parallel.
                intersection = Point.Empty;
                return false;
            }
            else
            {
                float x = (b2 * c1 - b1 * c2) / determinant;
                float y = (a1 * c2 - a2 * c1) / determinant;
                intersection = new Point((int)x, (int)y);

                // Check if the intersection point is within both line segments
                if (IsPointOnLineSegment(line1, intersection) && IsPointOnLineSegment(line2, intersection))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }
        private bool IsPointOnLineSegment(LineSegment2D line, Point point)
        {
            return point.X >= Math.Min(line.P1.X, line.P2.X) && point.X <= Math.Max(line.P1.X, line.P2.X) &&
                   point.Y >= Math.Min(line.P1.Y, line.P2.Y) && point.Y <= Math.Max(line.P1.Y, line.P2.Y);
        }
       
       
            
    }
}









