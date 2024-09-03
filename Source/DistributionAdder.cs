using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using StatLists;

// The DistributionAdder class adds and subtracts distributions, to allow for use in Generics
namespace AdaptiveInterpolation
{
    public class DistributionAdder : ICombiner<Distribution>, INumerifier<Distribution>
    {
        public Distribution Combine(Distribution d1, Distribution d2)
        {
            return d1.Plus(d2);
        }
        public Distribution Default()
        {
            return Distribution.Zero;
        }

        public Distribution ConvertToDistribution(Distribution distribution)
        {
            return distribution;
        }

        public Distribution Remove(Distribution sum, Distribution itemToSubtract)
        {
            return sum.Minus(itemToSubtract);
        }

    }
}
