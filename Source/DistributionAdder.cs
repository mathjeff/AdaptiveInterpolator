﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using StatLists;

// The DistributionAdder class adds and subtracts distributions, to allow for use in Generics
namespace AdaptiveInterpolation
{
    class DistributionAdder : ICombiner<Distribution>
    {
        public Distribution Combine(Distribution d1, Distribution d2)
        {
            return d1.Plus(d2);
        }
        public Distribution Default()
        {
            return Distribution.Zero;
        }

    }
}
