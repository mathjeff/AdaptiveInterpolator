using System;
using System.Collections.Generic;
using System.Text;
 
namespace AdaptiveInterpolation
{
    public class ThresholdComparison
    {
        public ThresholdComparison(int dimension, double threshold, double weight, bool expectPositive)
        {
            this.Dimension = dimension;
            this.Value = threshold;
            this.Weight = weight;
            this.ExpectPositive = expectPositive;
        }
        public bool Evaluate(double value)
        {
            if (value > this.Value)
                return this.ExpectPositive;
            if (value < this.Value)
                return !this.ExpectPositive;
            return this.ResultForTiedInput;
        }

        public int Dimension { get; set; }
        public double Value { get; set; }
        public double Weight { get; set; }
        public bool ExpectPositive { get; set; }

        private bool ResultForTiedInput = false;
    }
}
