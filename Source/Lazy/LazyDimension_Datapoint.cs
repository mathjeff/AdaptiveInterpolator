using System;
using System.Collections.Generic;
using System.Text;

namespace AdaptiveInterpolation
{
    public interface LazyDimension_Datapoint<OutputType>
    {
        LazyInputs GetInputs();
        OutputType GetOutput();
    }
}
