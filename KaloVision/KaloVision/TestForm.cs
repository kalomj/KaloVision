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
using Accord.Math.Transforms;

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
        double fps;

        VectorOfPointF kpVector;
        VectorOfPointF nextVector;

        IOutputArray statusArray = null;
        IOutputArray errArray = null;

        double minang = 360.0;
        double maxang = 0.0;

        CircularBuffer angleHistory;
        CircularBuffer avgXHistory;
        CircularBuffer avgYHistory;

        int historyLength = 128;

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

            angleHistory = new CircularBuffer(historyLength);
            avgXHistory = new CircularBuffer(historyLength);
            avgYHistory = new CircularBuffer(historyLength);
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

            double rho = 0.0;
            double theta_deg = 0.0;

            perfSw.Restart();

            try
            {
                

                framesProcessed++;

                //store last gray image, pull current image, convert to gray
                lastGray = currentGray;
                currentImage = capture.QueryFrame().ToImage<Bgr, Byte>();
                
                Image<Gray, byte> grayImage = new Image<Gray, byte>(currentImage.Size);
                CvInvoke.CvtColor(currentImage, grayImage, ColorConversion.Bgr2Gray);

                //apply gaussian blur to gray to smooth out noise
                CvInvoke.GaussianBlur(grayImage, grayImage, new Size(15, 15), 1.8);

                currentGray = grayImage;

                //create variables to store optical flow in cart and polar coordinates
                Image<Gray, float> flowX = new Image<Gray, float>(currentGray.Size);
                Image<Gray, float> flowY = new Image<Gray, float>(currentGray.Size);
                Image<Gray, float> mag = new Image<Gray, float>(currentGray.Size);
                Image<Gray, float> ang = new Image<Gray, float>(currentGray.Size);

                //image with all values set to 255
                Image<Gray, byte> fullGray = new Image<Gray, byte>(currentImage.Size);
                fullGray.SetValue(new Gray(255));


                //wait until second frame to get flow
                if (framesProcessed >= 2)
                {
                    int threshold = 2;

                    //get flow images
                    CvInvoke.CalcOpticalFlowFarneback(lastGray, currentGray, flowX, flowY, 0.5, 3, 20, 3, 5, 1.2, OpticalflowFarnebackFlag.Default);

                    //convert x and y flow to magnitude and angle images
                    CvInvoke.CartToPolar(flowX, flowY, mag, ang, true);

                    //threshold the magnitude so we only look at vectors with motion
                    CvInvoke.Threshold(mag, mag, threshold, 1.0, ThresholdType.Binary);

                    //find the total number of pixels in the image that are over the threshold value
                    MCvScalar sumMask = CvInvoke.Sum(mag);

                    //apply the mask to the flow vectors
                    flowX = flowX.Copy(mag.Convert<Gray, byte>());
                    flowY = flowY.Copy(mag.Convert<Gray, byte>());

                    //sum of flow vector components
                    MCvScalar sumX = CvInvoke.Sum(flowX);
                    MCvScalar sumY = CvInvoke.Sum(flowY);

                    double avgX = 0.0;
                    double avgY = 0.0;

                    //avg of flow vector components
                    if(sumMask.V0 > 0.0)
                    {
                        avgX = sumX.V0 / sumMask.V0;
                        avgY = sumY.V0 / sumMask.V0;
                    }

                    //convert to polar radius (rho) and angle (theta)
                    rho = Math.Sqrt(avgX * avgX + avgY * avgY);
                    double theta = Math.Atan2(avgY, avgX);

                    //convert angle from radians to degrees
                    theta_deg = theta * 180 / Math.PI;

                    //clamp values to bytes for HSV image visualization
                    CvInvoke.Normalize(mag, mag, 0, 255, NormType.MinMax);
                    CvInvoke.Normalize(ang, ang, 0, 255, NormType.MinMax);

                    //create hue/saturation/value image to visualize magnitude and angle of flow
                    Image<Hsv, byte> hsv = new Image<Hsv, byte>(new Image<Gray, byte>[] { ang.Convert<Gray, byte>(), fullGray, mag.Convert<Gray, byte>() });

                    //adding average flow components history
                    avgXHistory.Add(avgX);
                    avgYHistory.Add(avgY);

                    double rho_avg = 0.0;
                    double x_avg = 0.0;
                    double y_avg = 0.0;

                    int smoothLength = 3;

                    //get average angle in the history
                    if (avgXHistory.Count() > 0)
                    {
                        x_avg = avgXHistory.Median(smoothLength);
                        y_avg = avgYHistory.Median(smoothLength);
                        rho_avg = Math.Sqrt(x_avg * x_avg + y_avg * y_avg);
                    }

                    //convert hsv to bgr image for bitmap conversion
                    CvInvoke.CvtColor(hsv, currentImage, ColorConversion.Hsv2Bgr);

                    //draw instaneous average flow direction line if magnitude exceeds threshold
                    if (rho > threshold)
                    {
                        currentImage.Draw(new LineSegment2D(new Point(currentImage.Size.Width / 2, currentImage.Height / 2), new Point((int)(avgX * 10) + currentImage.Size.Width / 2, (int)(avgY * 10) + currentImage.Height / 2)), new Bgr(Color.Red), 2);
                    }

                    //draw historical average flow direction line
                    if (rho_avg > threshold)
                    {
                        currentImage.Draw(new LineSegment2D(new Point(currentImage.Size.Width / 2, currentImage.Height / 2), new Point((int)(x_avg * 10) + currentImage.Size.Width / 2, (int)(y_avg * 10) + currentImage.Height / 2)), new Bgr(Color.Blue), 2);
                    }
                        
                    //pull bitmap from image and draw to picture box
                    currentBitmap = currentImage.Bitmap;
                    videoPictureBox.Image = currentBitmap;
                }

                
            }
            catch (Exception exp)
            {
                MessageBox.Show(exp.Message + " " + exp.StackTrace);
            }

            perfSw.Stop();
            fps = ((double)framesProcessed / fpsSw.Elapsed.TotalSeconds);
            statusLabel.Text = framesProcessed.ToString() + " totalms: " + fpsSw.ElapsedMilliseconds.ToString() + " fps: " + ((double)framesProcessed / fpsSw.Elapsed.TotalSeconds).ToString() + " perfms: " + perfSw.ElapsedMilliseconds + " vmag: " + rho + " vang: " + theta_deg;
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

        private void fayButton_Click(object sender, EventArgs e)
        {
            new faForm(avgYHistory,fps).Show();
        }

        private void faxButton_Click(object sender, EventArgs e)
        {
            new faForm(avgXHistory,fps).Show();
        }
    }
}
