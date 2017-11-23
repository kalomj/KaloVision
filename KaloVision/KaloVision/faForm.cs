using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Accord.Math.Transforms;
using System.Windows.Forms.DataVisualization.Charting;

namespace KaloVision
{
    public partial class faForm : Form
    {
        CircularBuffer cb;
        System.Windows.Forms.Timer updateTimer;
        double fps;

        public faForm(CircularBuffer cb, double fps)
        {
            InitializeComponent();
            this.cb = cb;
            this.fps = fps;
            updateTimer = new System.Windows.Forms.Timer();
            updateTimer.Interval = 100;
            updateTimer.Tick += UpdateTimer_Tick;
        }

        private void UpdateTimer_Tick(object sender, EventArgs e)
        {
            double[] real;
            lock(cb)
            {
                real = cb.ToArray();   
            }
            double[] imag = real.Select(s => 0.0).ToArray();

            FourierTransform2.FFT(real, imag, Accord.Math.FourierTransform.Direction.Forward);

            double[] frequencyVector = Accord.Math.Vector.Interval(0.0, (double)(real.Length / 2));
            frequencyVector = frequencyVector.Select(s => s * fps / real.Length).ToArray();

            faChart.Series[0].Points.Clear();
            for(int i = 1; i < real.Length/2; i++)
            {
                faChart.Series[0].Points.AddXY(frequencyVector[i], 2 * ComplexAbs(real[i], imag[i]) / real.Length);
            }
            faChart.ResetAutoValues();
        }

        private double ComplexAbs(double real, double imag)
        {
            return Math.Pow(Math.Pow(real, 2) + Math.Pow(imag, 2), 0.5);
        }

        private void faForm_Closing(object sender, FormClosingEventArgs e)
        {
            updateTimer.Stop();
        }

        private void faForm_Shown(object sender, EventArgs e)
        {
            updateTimer.Start();
        }
    }
}
