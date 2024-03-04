using System;
using System.Collections.Generic;
using System.Text;

namespace AdaptiveInterpolation
{
    // A LazyDimension_Interpolator is an interpolator that lazily asks datapoints for their values in various dimensions
    // This allows it to not ask for the values of all dimensions of all datapoints
    // This allows it to support extremely high numbers of dimensions (for example, 1600 dimensions and 40,000 datapoints should be fine)
    public class LazyDimension_Interpolator<OutputType>
    {
        public LazyDimension_Interpolator(INumerifier<OutputType> outputConverter)
        {
            this.outputConverter = outputConverter;
            this.root = new LazyDimension_InterpolatorBox<OutputType>(outputConverter, 0);
        }
        public void AddDatapoint(LazyDimension_Datapoint<OutputType> datapoint)
        {
            int numCoordinates = datapoint.GetInputs().GetNumCoordinates();
            if (this.numDimensions < 0)
                this.numDimensions = numCoordinates;
            else
            {
                if (this.numDimensions != numCoordinates)
                    throw new ArgumentException("Changed number of coordinates: previously " + this.numDimensions + ", now " + numCoordinates);
            }
            this.root.AddDatapoint(datapoint);
        }
        public void RemoveDatapoint(LazyDimension_Datapoint<OutputType> datapoint)
        {
            this.root.RemoveDatapoint(datapoint);
        }

        public Distribution Interpolate(LazyInputs input)
        {
            LazyDimension_InterpolatorBox<OutputType> box = this.FindNeighborhood(input);
            Distribution result = this.outputConverter.ConvertToDistribution(box.AggregateOutput);
            return result;
        }
        // finds the LazyDimension_InterpolatorBox that should be used for interpolation around <inputs>
        private LazyDimension_InterpolatorBox<OutputType> FindNeighborhood(LazyInputs input)
        {
            double maxOutputSpread = this.root.GetScoreSpread();
            LazyDimension_InterpolatorBox<OutputType> currentBox = this.root;
            LazyDimension_InterpolatorBox<OutputType> nextBox;
            LazyDimension_InterpolatorBox<OutputType> result = null;
            result = currentBox;
            while (true)
            {
                // consider moving to the child
                if (currentBox.GetScoreSpread() <= 0)
                    return currentBox; // no point in splitting further
                nextBox = currentBox.ChooseChild(input);
                // figure out whether it's time to stop splitting
                if (nextBox == null)
                    return result;
                result = currentBox;
                // the more datapoints we have, the more often that we split
                if (!shouldDescend(nextBox))
                {
                    // if we finally decided that we could split but didn't want to, then return the content of this box
                    return result;
                }

                currentBox = nextBox;
            }
        }

        public Distribution GetAverage()
        {
            return this.outputConverter.ConvertToDistribution(this.root.AggregateOutput);
        }


        // Tells whether it's worth descending into <child> for more analysis
        private bool shouldDescend(LazyDimension_InterpolatorBox<OutputType> child)
        {
            if (child.NumDatapoints <= 1)
                return false;
            double childScoreSpread = child.GetScoreSpread();
            // If we don't have much data it isn't worth descending further
            // If the score spread in this box isn't much smaller than the initial score spread, then we aren't doing well and should stop splitting
            if (this.root.GetScoreSpread() * child.NumDatapoints * child.NumDatapoints < this.root.NumDatapoints * childScoreSpread * 4)
                return false;
            // If none of our datapoints are very confident then it isn't worth descending further
            if (this.root.GetScoreSpread() * child.Weight * child.Weight < this.root.Weight * childScoreSpread * 4)
                return false;
            return true;
        }

        private LazyDimension_InterpolatorBox<OutputType> root;
        private INumerifier<OutputType> outputConverter;
        private int numDimensions = -1;
    }
}