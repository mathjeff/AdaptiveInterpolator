using System;
using System.Collections.Generic;
using System.Text;

namespace AdaptiveInterpolation
{
    public class LazyDatapoint<OutputType> : ILazyDatapoint<OutputType>
    {
        public LazyDatapoint(LazyInputs inputs, OutputType output) {
          this.inputs = inputs;
          this.output = output;
        }

        private LazyInputs inputs;
        private OutputType output;

        public LazyInputs GetInputs() {
          return inputs;
        }

        public OutputType GetOutput() {
          return output;
        }
    }
}
