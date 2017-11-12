using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.CV.CvEnum;
using Emgu.CV.Features2D;
using Emgu.CV.Util;

namespace KaloVision
{
    public partial class TestForm : Form
    {
        Timer updateTimer;
        VideoCapture capture;
        Stopwatch fpsSw;
        Stopwatch perfSw;
        Image<Bgr, Byte> currentImage;
        Image<Gray, Byte> currentGray;
        Image<Gray, Byte> lastGray;
        Bitmap currentBitmap;
        long framesProcessed = 0;
        Feature2D featureDetector;
        MKeyPoint[] keyPoints;

        VectorOfPointF kpVector;
        VectorOfPointF nextVector;

        IOutputArray statusArray = null;
        IOutputArray errArray = null;

        double minang = 360.0;
        double maxang = 0.0;

        CircularBuffer angleHistory;

        public TestForm()
        {
            InitializeComponent();
            updateTimer = new Timer();
            updateTimer.Tick += UpdateTimer_Tick;
            capture = new VideoCapture();
            fpsSw = new Stopwatch();
            perfSw = new Stopwatch();
            updateTimer.Interval = 100;
            //featureDetector = new ORBDetector(500);
            featureDetector = new Emgu.CV.Features2D.GFTTDetector(500);

            angleHistory = new CircularBuffer(10);
        }

        private void UpdateTimer_Tick(object sender, EventArgs e)
        {
            if (
                false  && 
                framesProcessed == 2)
            {
                updateTimer.Stop();
                return;
            }

            double vmag = 0.0;
            double vang = 0.0;
            double rho = 0.0;
            double theta_deg = 0.0;

            try
            {
                framesProcessed++;
                lastGray = currentGray;
                currentImage = capture.QueryFrame().ToImage<Bgr, Byte>();
                perfSw.Restart();
                Image<Gray, byte> grayImage = new Image<Gray, byte>(currentImage.Size);
                CvInvoke.CvtColor(currentImage, grayImage, ColorConversion.Bgr2Gray);

                CvInvoke.GaussianBlur(grayImage, grayImage, new Size(15, 15), 1.8);

                currentGray = grayImage;

                //VisualizeDenseFlow_Means(ref vmag, ref vang);

                Image<Gray, float> flowX = new Image<Gray, float>(currentGray.Size);
                Image<Gray, float> flowY = new Image<Gray, float>(currentGray.Size);
                Image<Gray, float> mag = new Image<Gray, float>(currentGray.Size);
                Image<Gray, float> ang = new Image<Gray, float>(currentGray.Size);

                Image<Gray, byte> fullGray = new Image<Gray, byte>(currentImage.Size);
                fullGray.SetValue(new Gray(255));

                if (framesProcessed >= 2)
                {
                    int threshold = 3;

                    CvInvoke.CalcOpticalFlowFarneback(lastGray, currentGray, flowX, flowY, 0.5, 3, 20, 3, 5, 1.2, OpticalflowFarnebackFlag.Default);

                    CvInvoke.CartToPolar(flowX, flowY, mag, ang, true);

                    //sum of flow vector components
                    MCvScalar sumX = CvInvoke.Sum(flowX);
                    MCvScalar sumY = CvInvoke.Sum(flowY);
                    //convert to polar radius (rho) and angle (theta)
                    rho = Math.Sqrt(sumX.V0 * sumX.V0 + sumY.V0 * sumY.V0);
                    double theta = Math.Atan2(sumY.V0, sumX.V0);
                    //convert angle from radians to degrees
                    theta_deg = theta * 180 / Math.PI;

                    CvInvoke.Threshold(mag, mag, threshold, float.MaxValue, ThresholdType.ToZero);

                    CvInvoke.Normalize(mag, mag, 0, 255, NormType.MinMax);
                    CvInvoke.Normalize(ang, ang, 0, 255, NormType.MinMax);

                    Image<Hsv, byte> hsv = new Image<Hsv, byte>(new Image<Gray, byte>[] { ang.Convert<Gray, byte>(), fullGray, mag.Convert<Gray, byte>() });

                    if (rho > 40000)
                    {
                        angleHistory.Add(theta_deg);
                    }
                    else
                    {
                        if(angleHistory.Count() > 0)
                        {
                            angleHistory.Read();
                        }
                    }

                    double theta_avg = 0.0;
                    if(angleHistory.Count() > 0)
                    {
                        theta_avg = angleHistory.Avg();
                    }
                    
                    double x_avg = Math.Cos(theta_avg * (Math.PI / 180.0));
                    double y_avg = Math.Sin(theta_avg * (Math.PI / 180.0));

                    double x = Math.Cos(theta_deg * (Math.PI / 180.0));
                    double y = Math.Sin(theta_deg * (Math.PI / 180.0));

                    CvInvoke.CvtColor(hsv, currentImage, ColorConversion.Hsv2Bgr);

                    currentImage.Erode(5);
                    currentImage.Dilate(5);

                    if (rho > 40000)
                    {
                        currentImage.Draw(new LineSegment2D(new Point(currentImage.Size.Width / 2, currentImage.Height / 2), new Point((int)(x * 100) + currentImage.Size.Width / 2, (int)(y * 100) + currentImage.Height / 2)), new Bgr(Color.Red), 2);
                    }

                    currentImage.Draw(new LineSegment2D(new Point(currentImage.Size.Width / 2, currentImage.Height / 2), new Point((int)(x_avg * 100) + currentImage.Size.Width / 2, (int)(y_avg * 100) + currentImage.Height / 2)), new Bgr(Color.Blue), 2);

                    currentBitmap = currentImage.Bitmap;
                    videoPictureBox.Image = currentBitmap;
                }

                perfSw.Stop();
            }
            catch (Exception exp)
            {
                MessageBox.Show(exp.Message + " " + exp.StackTrace);
            }
            statusLabel.Text = framesProcessed.ToString() + " totalms: " + fpsSw.ElapsedMilliseconds.ToString() + " fps: " + ((double)framesProcessed / fpsSw.Elapsed.TotalSeconds).ToString() + " perfms: " + perfSw.ElapsedMilliseconds + " vmag: " + rho + " vang: " + theta_deg + " minang: " + minang + " maxang: " + maxang;
        }

        private void VisualizeDenseFlow_Means(ref double vmag, ref double vang)
        {
            Image<Gray, float> flowX = new Image<Gray, float>(currentGray.Size);
            Image<Gray, float> flowY = new Image<Gray, float>(currentGray.Size);
            Image<Gray, float> mag = new Image<Gray, float>(currentGray.Size);
            Image<Gray, float> ang = new Image<Gray, float>(currentGray.Size);

            Image<Gray, byte> fullGray = new Image<Gray, byte>(currentImage.Size);
            fullGray.SetValue(new Gray(255));

            if (framesProcessed >= 2)
            {
                CvInvoke.CalcOpticalFlowFarneback(lastGray, currentGray, flowX, flowY, 0.5, 3, 50, 3, 5, 1.2, OpticalflowFarnebackFlag.Default);
                CvInvoke.CartToPolar(flowX, flowY, mag, ang, true);

                //CvInvoke.MinMaxIdx(ang, out minang, out maxang, new int[] { }, new int[] { });

                CvInvoke.Threshold(mag, mag, 4, float.MaxValue, ThresholdType.ToZero);

                MCvScalar meanang = CvInvoke.Mean(ang.Mat, mag.Convert<Gray, byte>());
                MCvScalar meanmag = CvInvoke.Mean(mag.Mat, mag.Convert<Gray, byte>());

                vmag = meanmag.V0;
                vang = meanang.V0;

                if (vmag > 4)
                {
                    if (minang > vang)
                    {
                        minang = vang;
                    }

                    if (maxang < vang)
                    {
                        maxang = vang;
                    }
                }

                CvInvoke.Normalize(mag, mag, 0, 255, NormType.MinMax);
                CvInvoke.Normalize(ang, ang, 0, 255, NormType.MinMax);

                Image<Hsv, byte> hsv = new Image<Hsv, byte>(new Image<Gray, byte>[] { ang.Convert<Gray, byte>(), fullGray, mag.Convert<Gray, byte>() });

                double x = Math.Cos(vang * (Math.PI / 180.0));
                double y = Math.Sin(vang * (Math.PI / 180.0));

                CvInvoke.CvtColor(hsv, currentImage, ColorConversion.Hsv2Bgr);

                currentImage.Erode(5);
                currentImage.Dilate(5);

                currentImage.Draw(new LineSegment2D(new Point(currentImage.Size.Width / 2, currentImage.Height / 2), new Point((int)(x * 100) + currentImage.Size.Width / 2, (int)(y * 100) + currentImage.Height / 2)), new Bgr(Color.Red), 2);
            }
        }

        private void TrackFeatures(Image<Gray, byte> grayImage)
        {
            if (framesProcessed == 1)
            {
                keyPoints = featureDetector.Detect(grayImage);
                kpVector = new VectorOfPointF((keyPoints.Select(p => p.Point).ToArray()));
                nextVector = new VectorOfPointF(kpVector.Size);
                statusArray = new VectorOfByte(kpVector.Size);
                errArray = new VectorOfFloat(kpVector.Size);
            }
            else if (framesProcessed > 2)
            {
                kpVector = nextVector;
            }

            if (framesProcessed % 50 == 0)
            {
                kpVector = CreateGrid(currentImage);
            }

            if (framesProcessed >= 2)
            {
                CvInvoke.CalcOpticalFlowPyrLK(lastGray, grayImage, kpVector, nextVector, statusArray, errArray, new Size(trackBar1.Value * 2 + 2, trackBar1.Value * 2 + 2), trackBar4.Value, new MCvTermCriteria(trackBar2.Value, trackBar3.Value / 100.0));
                DrawPoints(nextVector, Color.Blue);
            }
        }

        private void DrawPoints(VectorOfPointF kpVector, Color color)
        {
            for (int i = 0; i < kpVector.Size; i++)
            {
                currentImage.Draw(new Cross2DF(kpVector[i], 5, 5), new Bgr(color), 1);
            }
        }

        private Image<Gray, byte> EdgeDetect(Image<Bgr, Byte> img)
        {
            Image<Gray, Byte> grayImage = new Image<Gray, byte>(img.Size);
            
            CvInvoke.CvtColor(img, grayImage, ColorConversion.Bgr2Gray);
            CvInvoke.GaussianBlur(grayImage, grayImage, new Size(5, 5), 1.8);
            CvInvoke.Canny(grayImage, grayImage, trackBar1.Value, trackBar1.Value * 3);

            return grayImage;
        }

        private void stopButton_Click(object sender, EventArgs e)
        {
            updateTimer.Stop();
            fpsSw.Stop();
        }

        private void startButton_Click(object sender, EventArgs e)
        {
            updateTimer.Start();
            fpsSw.Restart();
            framesProcessed = 0;
        }

        VectorOfPointF CreateGrid(Image<Bgr,Byte> image)
        {
            int s = 25;
            VectorOfPointF vf = new VectorOfPointF();

            for(int i = 0; i < image.Size.Width/s; i++)
            {
                for (int j = 0; j < image.Size.Height/s; j++)
                {
                    vf.Push(new PointF[] { new PointF(i*s, j*s) });
                }
            }

            return vf;
        }
    }
}
