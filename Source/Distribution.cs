﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AdaptiveInterpolation
{
    public class Distribution
    {
        private static Distribution zero;
        public static Distribution Zero
        {
            get
            {
                if (zero == null)
                    zero = new Distribution(0, 0, 0);
                return zero;
            }
        }
        // constructors
        public static Distribution MakeDistribution(double mean, double stdDev, double weight)
        {
            Distribution result = new Distribution(mean * weight, (mean * mean + stdDev * stdDev) * weight, weight);
            return result;
        }
        public Distribution()
        {
        }
        public Distribution(double totalValue, double totalSquaredValue, double totalWeight)
        {
            this.sumValue = totalValue;
            this.sumSquaredValue = totalSquaredValue;
            this.sumWeight = totalWeight;
        }
        public double GetMeanOr(double value)
        {
            if (this.sumWeight != 0)
                return this.Mean;
            return value;
        }
        public double Mean
        {
            get
            {
                if (this.sumWeight != 0)
                {
                    return this.sumValue / this.sumWeight;
                }
                else
                {
                    return 0;
                }
            }
        }
        public double StdDev
        {
            get
            {
                if (this.sumWeight == 0)
                {
                    // no data
                    return 0;
                }
                double temp = (this.sumSquaredValue - this.sumValue * this.sumValue / this.sumWeight) / sumWeight;
                if (temp < 0)
                {
                    // rounding error
                    return 0;
                }
                else
                {
                    return Math.Sqrt(temp);
                }
            }
        }
        public double Weight
        {
            get
            {
                return this.sumWeight;
            }
        }
        public Distribution Plus(Distribution other)
        {
            return new Distribution(this.sumValue + other.sumValue, this.sumSquaredValue + other.sumSquaredValue, this.sumWeight + other.sumWeight);
        }
        public Distribution Plus(double newValue)
        {
            return new Distribution(this.sumValue + newValue, this.sumSquaredValue + newValue * newValue, this.sumWeight + 1);
        }
        public Distribution Minus(Distribution other)
        {
            return new Distribution(this.sumValue - other.sumValue, this.sumSquaredValue - other.sumSquaredValue, this.sumWeight - other.sumWeight);
        }
        public override string ToString()
        {
            string result = "Mean:" + this.Mean.ToString() + " stdDev:" + this.StdDev.ToString() + " weight:" + this.Weight.ToString();
            return result;
        }

        // returns a new Distribution whose values are all multiplied by outputScale
        public Distribution CopyAndStretchBy(double outputScale)
        {
            Distribution result = new Distribution(this.sumValue * outputScale, this.sumValue * outputScale * outputScale, this.sumWeight);
            return result;
        }
        // returns a new Distribution with weight equal to this.sumWeight * weightScale
        public Distribution CopyAndReweightBy(double weightScale)
        {
            Distribution result = new Distribution(this.sumValue * weightScale, this.sumSquaredValue * weightScale, this.sumWeight * weightScale);
            return result;
        }
        // returns a new Distribution with sumWeight equal to newWeight, or 0 if this.sumWeight == 0
        public Distribution CopyAndReweightTo(double newWeight)
        {
            double scale;
            if (this.sumWeight != 0)
            {
                scale = newWeight / this.sumWeight;
            }
            else
            {
                scale = 0;
            }
            return this.CopyAndReweightBy(scale);
        }
        private double sumValue;
        private double sumSquaredValue;
        private double sumWeight;
    }
}
