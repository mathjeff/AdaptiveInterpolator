using System;
using System.Collections.Generic;
using System.Text;

namespace AdaptiveInterpolation
{
    public interface ILazyDatapoint<OutputType>
    {
        LazyInputs GetInputs();
        OutputType GetOutput();
    }
}
