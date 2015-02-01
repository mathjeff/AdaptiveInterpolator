using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using AdaptiveLinearInterpolation;

namespace InterpolatorTester
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class Window1 : Window
    {
        public Window1()
        {
            this.Loaded += new RoutedEventHandler(Window1_Loaded);
            InitializeComponent();
        }

        void Window1_Loaded(object sender, RoutedEventArgs e)
        {
            this.RunTest();
        }
        public void RunTest()
        {
            FloatRange[] coordinates = new FloatRange[3];
            int width = 30;
            coordinates[0] = new FloatRange(0, true, width, true);
            coordinates[1] = new FloatRange(0, true, width, true);
            coordinates[2] = new FloatRange(0, true, width, true);

            HyperBox boundary = new HyperBox(coordinates);
            AdaptiveLinearInterpolator interpolator = new AdaptiveLinearInterpolator(boundary);
            int i, j, k;
            double x, y, z;
            Random generator = new Random();
            // fill data into it
            for (i = 0; i < width; i++)
            {
                for (j = 0; j < width; j++)
                {
                    for (k = 0; k < width; k++)
                    {
                        x = (double)i;// +generator.NextDouble();
                        y = (double)j;// +generator.NextDouble();
                        z = (double)k;
                        Datapoint point = new Datapoint(x, y, z, this.MakeOutput(x, y, z, generator));
                        interpolator.AddDatapoint(point);
                    }
                }
            }
            // test the interpolator
            double[] location = new double[3];
            double error2 = 0;
            for (i = 0; i < width; i++)
            {
                location[0] = (double)i;
                for (j = 0; j < width; j++)
                {
                    location[1] = (double)j;
                    for (k = 0; k < width; k++)
                    {
                        location[2] = (double)k;
                        double guess = interpolator.Interpolate(location).Mean;
                        double correctValue = this.MakeOutput(location[0], location[1], location[2], generator);
                        double error = guess - correctValue;
                        error2 += error * error;
                    }
                }
            }
            double MSE = (error2 / interpolator.NumDatapoints);
            Console.WriteLine("MSE = " + MSE.ToString());
            return;
            //0.0730243457105064
            //Datapoint point = new Datapoint(

        }
        public double MakeOutput(double x, double y, double z, Random generator)
        {
            //int newY = (int)y;
            //double result = x;// +10 * generator.NextDouble();
            //double result = x * Math.Sin(x / 6) + y * Math.Sin(y / 6) / 10;
            //double result = Math.Sqrt(x) + Math.Sqrt(y);
            double result = 1 / (x + 1) + 1 / (y + 1) + 1 / (z + 1);
            return result;
        }
    }
}
