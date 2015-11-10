using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

// the current version of the AdaptiveLinearInterpolator does a 0th order approximation of the datapoints near the coordinates in question
// It intelligently determines a reasonable neighborhood of points nearby, and so if any dimensions don't help in certain areas, it won't worry about them
// Improving it to a 1st order approximation would be a big improvement because it allows the error to drop much faster
namespace AdaptiveLinearInterpolation
{
    public class AdaptiveLinearInterpolator<SummaryType>
    {
        // SummaryType is the type of object to put in each box (it's a summary of the datapoints that the box represents)
        public AdaptiveLinearInterpolator(HyperBox<SummaryType> inputBoundary, INumerifier<SummaryType> itemCombiner)
        {
            this.itemCombiner = itemCombiner;
            this.root = new SmartInterpolationBox<SummaryType>(inputBoundary, itemCombiner);
        }
        public void AddDatapoint(IDatapoint<SummaryType> newDatapoint)
        {
            if (newDatapoint.NumInputDimensions != this.root.NumDimensions)
                throw new ArgumentException("the number of dimensions is incorrect");
            this.root.AddDatapoint(newDatapoint);

            // Inform the root node that it may now be allowed to delegate the intensive analysis to a further descendent
            this.updateNumExemptSplits();
        }
        public void RemoveDatapoint(IDatapoint<SummaryType> datapoint)
        {
            this.root.RemoveDatapoint(datapoint);
        }
        private void updateNumExemptSplits()
        {
            // We want this prediction algorithm to run quickly (ideally linear (O(n)) time)
            // It's also reasonable to split each dimension a little bit, because people shouldn't be adding dimensions that are completely useless, and even if they do, it's ok to double-check
            // So, for the first few splits that we do (starting at the root), we don't do a long analysis about which dimension to split and we just choose the next dimension

            if (this.root.NumDatapoints > 2)
            {
                double numExemptSplits = Math.Log(Math.Log(this.root.NumDatapoints, 2), 2);
                this.root.ForceSplits(0, (int)numExemptSplits);
            }
        }
        public Distribution Interpolate(double[] coordinates)
        {
            SmartInterpolationBox<SummaryType> box = this.FindNeighborhood(coordinates);
            Distribution result = this.itemCombiner.ConvertToDistribution(box.ItemSummary);
            return result;
        }
        public IEnumerable<IDatapoint<SummaryType>> JustifyInterpolation(double[] coordinates)
        {
            SmartInterpolationBox<SummaryType> box = this.FindNeighborhood(coordinates);
            return box.Datapoints;
        }
        public HyperBox<SummaryType> FindNeighborhoodCoordinates(double[] coordinates)
        {
            SmartInterpolationBox<SummaryType> box = this.FindNeighborhood(coordinates);
            return box.ObservedBoundary;
        }
        private SmartInterpolationBox<SummaryType> FindNeighborhood(double[] coordinates)
        {
            return this.FindNeighborhood(coordinates, -1);
        }
        private SmartInterpolationBox<SummaryType> FindNeighborhood(double[] coordinates, int maxNumIterations)
        {
            if (coordinates.Length != this.root.NumDimensions)
                throw new ArgumentException("the number of dimensions is incorrect");

            // figure out how much room there was to start with
            //double maxInputArea = this.root.GetInputArea();
            int numSplits = 0;
            if (!this.root.ChildrenExist())
                numSplits++;
            //double maxInputSpread = this.root.GetInputVariation();
            double maxOutputSpread = this.root.GetScoreSpread();
            SmartInterpolationBox<SummaryType> currentBox = this.root;
            SmartInterpolationBox<SummaryType> nextBox;
            SmartInterpolationBox<SummaryType> result = null;
            //Distribution result = new Distribution();
            //double inputFraction;
            //double outputFraction;
            //double datapointFraction;
            result = currentBox;
            while (true)
            {
                if (!currentBox.ChildrenExist())
                    numSplits++;
                // decay towards the next component
                //result = result.CopyAndReweightBy(0.5);
                //result = result.Plus(currentBox.Interpolate(coordinates));
                // consider moving to the child
                nextBox = currentBox.ChooseChild(coordinates);
                // figure out whether it's time to stop splitting
                if (nextBox == null)
                {
                    return result;
                }
                result = currentBox;
                if (nextBox.NumDatapoints <= 1)
                {
                    return result;
                }
                // the more datapoints we have, the more often that we split
                double nextOutputSpread = nextBox.GetScoreSpread();
                if (nextOutputSpread <= 0)
                {
                    return result;
                }
                //double nextInputSpread = nextBox.GetInputVariation();
                //inputFraction = nextInputSpread / maxInputSpread;
                //outputFraction = nextOutputSpread / maxOutputSpread;
                //datapointFraction = (double)nextBox.NumDatapoints / (double)this.root.NumDatapoints;

                //if (maxOutputSpread * maxOutputSpread * nextBox.NumDatapoints * nextBox.NumDatapoints * nextInputSpread <= this.root.NumDatapoints * maxInputSpread * nextOutputSpread * nextOutputSpread)
                //if ((inputFraction + datapointFraction) * nextBox.NumDatapoints <= outputFraction)
                //if (maxOutputSpread * nextBox.NumDatapoints * nextInputSpread <= maxInputSpread * nextOutputSpread)
                if (maxOutputSpread * nextBox.NumDatapoints * nextBox.NumDatapoints <= this.root.NumDatapoints * nextOutputSpread)
                {
                    // if we finally decided that we could split but didn't want to, then return the content of this box
                    return result;
                }

                if (numSplits == maxNumIterations)
                {
                    // ran out of time; return the content of this box
                    return result;
                }
                currentBox = nextBox;
            }
            // now interpolate using the box of appropriate granularity
            //Distribution result = currentBox.Interpolate(coordinates);
        }
        public int NumDatapoints
        {
            get
            {
                return this.root.NumDatapoints;
            }
        }
        private SmartInterpolationBox<SummaryType> root;
        private INumerifier<SummaryType> itemCombiner;
        //FloatRange outputSpan;
    }
}
