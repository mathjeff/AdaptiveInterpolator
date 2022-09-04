using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

// the current version of the AdaptiveLinearInterpolator does a 0th order approximation of the datapoints near the coordinates in question
// It intelligently determines a reasonable neighborhood of points nearby, and so if any dimensions don't help in certain areas, it won't worry about them
// Improving it to a 1st order approximation would be a big improvement because it allows the error to drop much faster
namespace AdaptiveInterpolation
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
                throw new ArgumentException("the number of dimensions is incorrect. Expected " + this.root.NumDatapoints + ", got " + newDatapoint.NumInputDimensions);
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
                double expectedDepth = Math.Log(this.root.NumDatapoints, 2);
                double numEasySplits = this.root.NumDimensions;
                double numHardSplits = expectedDepth - numEasySplits;
                if (numHardSplits > 0)
                    this.root.ForceSplits(0, (int)(Math.Sqrt(numHardSplits)));
            }
        }
        public Distribution Interpolate(double[] coordinates)
        {
            SmartInterpolationBox<SummaryType> box = this.FindNeighborhood(coordinates);
            Distribution result = this.itemCombiner.ConvertToDistribution(box.ItemSummary);
            return result;
        }
        public Distribution GetAverage()
        {
            //DateTime start = DateTime.Now;
            SmartInterpolationBox<SummaryType> box = this.root;
            Distribution result = this.itemCombiner.ConvertToDistribution(this.root.ItemSummary);
            //DateTime end = DateTime.Now;
            //System.Diagnostics.Debug.WriteLine("Spent " + end.Subtract(start) + " getting average of interpolation box");
            return result;
        }
        public IEnumerable<IDatapoint<SummaryType>> JustifyInterpolation(double[] coordinates)
        {
            SmartInterpolationBox<SummaryType> box = this.FindNeighborhood(coordinates);
            return box.Datapoints;
        }

        // Finds a bunch of points that are representative of the inputs that we've observed
        // There will be more points in areas that have more changes, and fewer points in other areas
        public IEnumerable<double[]> FindRepresentativePoints()
        {
            List<double[]> points = new List<double[]>();
            List<SmartInterpolationBox<SummaryType>> boxesToCheck = new List<SmartInterpolationBox<SummaryType>>();
            boxesToCheck.Add(this.root);
            while (boxesToCheck.Count > 0)
            {
                SmartInterpolationBox<SummaryType> box = boxesToCheck.Last();
                boxesToCheck.RemoveAt(boxesToCheck.Count - 1);
                if (this.shouldDescend(box) && box.ChildrenExist())
                {
                    boxesToCheck.Add(box.UpperChild);
                    boxesToCheck.Add(box.LowerChild);
                }
                else
                {
                    if (box.ObservedBoundary != null)
                        points.Add(box.ObservedBoundary.Middle);
                }
            }
            return points;
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
                throw new ArgumentException("the number of dimensions is incorrect. Expected " + this.root.NumDatapoints + ", got " + coordinates.Length);

            // figure out how much room there was to start with
            int numSplits = 0;
            if (!this.root.ChildrenExist())
                numSplits++;
            double maxOutputSpread = this.root.GetScoreSpread();
            SmartInterpolationBox<SummaryType> currentBox = this.root;
            SmartInterpolationBox<SummaryType> nextBox;
            SmartInterpolationBox<SummaryType> result = null;
            result = currentBox;
            while (true)
            {
                if (!currentBox.ChildrenExist())
                    numSplits++;
                // consider moving to the child
                nextBox = currentBox.ChooseChild(coordinates);
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

                if (numSplits == maxNumIterations)
                {
                    // ran out of time; return the content of this box
                    return result;
                }
                currentBox = nextBox;
            }
        }

        public int NumDatapoints
        {
            get
            {
                return this.root.NumDatapoints;
            }
        }

        // tells whether it's worth descending into <child> for more analysis
        private bool shouldDescend(SmartInterpolationBox<SummaryType> child)
        {
            if (child.NumDatapoints <= 1)
                return false;
            double childScoreSpread = child.GetScoreSpread();
            if (childScoreSpread <= 0)
                return false;
            return (this.root.GetScoreSpread() * child.NumDatapoints * child.NumDatapoints > this.root.NumDatapoints * childScoreSpread * 4);
        }

        private SmartInterpolationBox<SummaryType> root;
        private INumerifier<SummaryType> itemCombiner;
    }
}
