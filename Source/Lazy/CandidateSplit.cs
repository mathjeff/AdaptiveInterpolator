using System;
using System.Collections.Generic;
using System.Text;

namespace AdaptiveInterpolation
{
    public class CandidateSplit
    {
        public CandidateSplit(double penalty, double splitValue)
        {
            this.Penalty = penalty;
            this.SplitValue = splitValue;
        }
        public double Penalty { get; set; }
        public double SplitValue { get; set; }
    }
}
