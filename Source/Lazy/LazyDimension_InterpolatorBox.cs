using AdaptiveInterpolation;
using System;
using System.Collections.Generic;
using System.Text;

namespace AdaptiveInterpolation
{
    // A LazyDimension_InterpolatorBox is an internal helper class for LazyDimension_Interpolator
    // It is a node in a binary tree for interpolating
    class LazyDimension_InterpolatorBox<OutputType>
    {
        private static int nextBoxId;
        public LazyDimension_InterpolatorBox(INumerifier<OutputType> outputConverter, int depthFromRoot)
        {
            this.outputConverter = outputConverter;
            this.splitDimension = -1;
            this.datapoints = new List<LazyDimension_Datapoint<OutputType>>();
            this.pendingDatapoints = new List<LazyDimension_Datapoint<OutputType>>();
            this.aggregateOutput = this.outputConverter.Default();
            this.depthFromRoot = depthFromRoot;
            nextBoxId++;
            this.boxId = nextBoxId;
        }

        public void AddDatapoint(LazyDimension_Datapoint<OutputType> datapoint)
        {
            this.pendingDatapoints.Add(datapoint);
            // System.Diagnostics.Debug.WriteLine("AddDatapoint in box at depth " + this.depthFromRoot + ", num points = " + this.NumDatapoints + ", num coordinates = " + datapoint.GetInputs().GetNumCoordinates());
        }
        public void AddDatapoints(IEnumerable<LazyDimension_Datapoint<OutputType>> newDatapoints)
        {
            foreach (LazyDimension_Datapoint<OutputType> newDatapoint in newDatapoints)
            {
                this.AddDatapoint(newDatapoint);
            }
        }
        public bool RemoveDatapoint(LazyDimension_Datapoint<OutputType> datapoint)
        {
            if (this.pendingDatapoints.Contains(datapoint))
            {
                this.pendingDatapoints.Remove(datapoint);
                return true;    // removed successfully
            }
            if (!this.datapoints.Remove(datapoint))
            {
                return false;   // datapoint was not removed
            }
            this.aggregateOutput = this.outputConverter.Remove(this.aggregateOutput, datapoint.GetOutput());
            if (this.lowerChild != null)
                this.lowerChild.RemoveDatapoint(datapoint);
            if (this.upperChild != null)
                this.upperChild.RemoveDatapoint(datapoint);
            return true;
        }


        public double GetScoreSpread()
        {
            this.ApplyPendingPoints();
            double spread = this.outputConverter.ConvertToDistribution(this.aggregateOutput).StdDev;
            return spread;
        }

        public LazyDimension_InterpolatorBox<OutputType> ChooseChild(LazyInputs inputs)
        {
            this.ApplyPendingPoints();
            if (this.lowerChild == null)
                return null;
            bool upper = inputs.GetInput(this.splitDimension) > this.splitValue;
            /*if (upper)
                System.Diagnostics.Debug.WriteLine("Box at depth " + this.depthFromRoot + " chose upper child because " + inputs.GetDescription(this.splitDimension) + " > " + this.splitValue);
            else
                System.Diagnostics.Debug.WriteLine("Box at depth " + this.depthFromRoot + " chose lower child because " + inputs.GetDescription(this.splitDimension) + " < " + this.splitValue);
            */
            if (upper)
                return this.upperChild;
            else
                return this.lowerChild;
        }


        // moves any points from the list of pendingPoints into the main list, and updates any stats
        private void ApplyPendingPoints()
        {
            int totalNumPoints = this.datapoints.Count + this.pendingDatapoints.Count;

            // Now actually add those points and do the split
            foreach (LazyDimension_Datapoint<OutputType> datapoint in this.pendingDatapoints)
            {
                this.AddPointNowWithoutSplitting(datapoint);

                // We don't check all the time about splitting, because splitting takes a long time
                // We also don't wait to the end to check about splitting, because that means:
                //  that this order of calls:
                //   adding n points
                //   requesting an interpolation
                //  might result in a different split than this order of calls:
                //   adding (n-1) points
                //   requesting an interpolation
                //   adding 1 point
                // So, we preplan how many points are required for the next split
                if (this.datapoints.Count >= this.numPointsAtNextSplit)
                {
                    this.numPointsAtNextSplit = this.RequiredNumPointsToSplit(this.numPointsAtNextSplit);

                    // only actually do the split if this is the last planned split
                    if (this.numPointsAtNextSplit > totalNumPoints)
                        this.ConsiderSplitting();
                }
            }
            this.pendingDatapoints.Clear();
        }

        private void AddPointNowWithoutSplitting(LazyDimension_Datapoint<OutputType> newDatapoint)
        {
            // keep track of the outputs we've observed
            this.aggregateOutput = this.outputConverter.Combine(this.aggregateOutput, newDatapoint.GetOutput());

            // add it to our set
            this.datapoints.Add(newDatapoint);
            if (this.lowerChild != null)
            {
                if (newDatapoint.GetInputs().GetInput(this.splitDimension) > this.splitValue)
                    this.upperChild.AddDatapoint(newDatapoint);
                else
                    this.lowerChild.AddDatapoint(newDatapoint);
            }
        }

        // tells whether it is worth considering a split, given the current number of datapoints and also the number that we had at the last split
        private int RequiredNumPointsToSplit(int numPointsAtLastConsideredSplit)
        {
            // The reason we decrease the splitting frequency as our ancestry increases is to avoid redudant splits that are going to get thrown out soon.
            // If our parent uses the same split factor as us and is receiving random inputs, then our parent is expected to want to split at about the same time we do, or maybe slightly later.
            // If our parent splits slightly after us, then that means we did a bunch of effort splitting that soon became irrelevant.
            // So, we put our split threshold such that it should be slightly after our parent is expected to split.
            // Our split threshold is mostly only intended to trigger if lots of points are getting concentrated specifically into this box.

            int result = (int)((double)numPointsAtLastConsideredSplit * 1.5 + Math.Sqrt(numPointsAtLastConsideredSplit));
            if (result <= numPointsAtLastConsideredSplit)
                result = numPointsAtLastConsideredSplit + 1;
            return result;
        }

        private void ConsiderSplitting()
        {
            //System.Diagnostics.Debug.WriteLine("ConsiderSplitting in box at depth " + this.depthFromRoot + ", num points = " + this.NumDatapoints);

            if (this.datapoints.Count <= 8)
                return;

            int maxDimensions = this.datapoints[this.datapoints.Count - 1].GetInputs().GetNumCoordinates();
            if (maxDimensions < 1)
                return;
            // If we check all dimensions of all datapoints and choose the dimension that minimizes the error, that can cause two problems:
            // 1. It can take a long time
            // 2. It can contribute to overfitting, where we considered lots of models and chose only the best one, and used its past uncertainty to estimate its future uncertainty
            // Instead, we check all coordinates of only a few datapoints, which is much faster.
            // We still compute uncertainty using all points
            int maxNumDatapointsToCheck = this.datapoints.Count;
            int numDatapointsToCheck = 4;

            List<int> candidateDimensions = new List<int>();
            for (int dimension = 0; dimension < maxDimensions; dimension++)
            {
                candidateDimensions.Add(dimension);
            }
            int bestDimension = 0;
            while (true)
            {
                // if there's only one candidate dimension left, it's the best
                if (candidateDimensions.Count == 1)
                {
                    bestDimension = candidateDimensions[0];
                    break;
                }
                List<double> dimensionPenalties = new List<double>();
                foreach (int dimension in candidateDimensions)
                {
                    double penalty = getCandidateSplit(dimension, numDatapointsToCheck).Penalty;
                    dimensionPenalties.Add(penalty);
                }

                // select the dimensions having at least the median score
                double medianPenalty = MedianUtils.EstimateMedian(dimensionPenalties);
                List<int> goodDimensions = new List<int>();
                for (int i = 0; i < candidateDimensions.Count; i++)
                {
                    if (dimensionPenalties[i] < medianPenalty)
                        goodDimensions.Add(candidateDimensions[i]);
                }

                // check more points in the next iteration
                numDatapointsToCheck = (int)(numDatapointsToCheck * 1.5 + 1);
                // if we've used all the available points, then we just choose the dimension that did the best on these datapoints
                if (numDatapointsToCheck > maxNumDatapointsToCheck)
                {
                    double bestPenalty = double.PositiveInfinity;
                    for (int i = 0; i < candidateDimensions.Count; i++)
                    {
                        if (dimensionPenalties[i] < bestPenalty)
                        {
                            bestPenalty = dimensionPenalties[i];
                            bestDimension = candidateDimensions[i];
                        }
                    }
                    break;
                }

                // If not all dimensions had the same score, filter the results to the ones with good scores
                if (goodDimensions.Count > 0)
                    candidateDimensions = goodDimensions;
            }

            // get the value to split at
            double splitValue = this.getCandidateSplit(bestDimension, maxNumDatapointsToCheck).SplitValue;
            this.Split(bestDimension, splitValue);
        }


        private CandidateSplit getCandidateSplit(int dimension, int numDatapointsToCheck)
        {
            List<double> outputs = new List<double>();
            // get the inputs for the given datapoints in this dimension
            List<double> inputs = new List<double>();
            double minInput = double.PositiveInfinity;
            double maxInput = double.NegativeInfinity;
            for (int i = this.datapoints.Count - 1; i >= this.datapoints.Count - numDatapointsToCheck; i--)
            {
                LazyDimension_Datapoint<OutputType> datapoint = this.datapoints[i];
                double input = datapoint.GetInputs().GetInput(dimension);
                if (input < minInput)
                    minInput = input;
                if (input > maxInput)
                    maxInput = input;
                inputs.Add(input);
                outputs.Add(this.outputConverter.ConvertToDistribution(datapoint.GetOutput()).Mean);
            }
            // Now we choose an input split threshold based on the datapoints
            // Note that this isn't necessarily the best split value for the inputs that we did analyze, but it should be a good split value for all of the inputs overall, anyway
            double middleInput = (minInput + maxInput) / 2;
            // count the number of cases where larger input is correlated with larger output
            double stddev = countSplitStddev(inputs, outputs, middleInput);
            return new CandidateSplit(stddev, middleInput);
        }

        private double countSplitStddev(List<double> inputs, List<double> outputs, double inputThreshold)
        {
            Distribution low = new Distribution();
            Distribution high = new Distribution();
            for (int i = 0; i < outputs.Count; i++)
            {
                if (inputs[i] <= inputThreshold)
                    low = low.Plus(outputs[i]);
                else
                    high = high.Plus(outputs[i]);
            }
            double totalWeight = low.Weight + high.Weight;
            return low.StdDev * low.Weight / totalWeight + high.StdDev * high.Weight / totalWeight;
        }

        private void Split(int dimension, double splitValue)
        {

            List<LazyDimension_Datapoint<OutputType>> lowerPoints = new List<LazyDimension_Datapoint<OutputType>>();
            List<LazyDimension_Datapoint<OutputType>> upperPoints = new List<LazyDimension_Datapoint<OutputType>>();
            foreach (LazyDimension_Datapoint<OutputType> datapoint in this.datapoints)
            {
                if (datapoint.GetInputs().GetInput(dimension) > splitValue)
                    upperPoints.Add(datapoint);
                else
                    lowerPoints.Add(datapoint);
            }

            if (lowerPoints.Count < 1 || upperPoints.Count < 1)
            {
                // If all the points fell on the same side of the split, then there's no need to split anymore
                return;
            }


            this.splitDimension = dimension;
            this.splitValue = splitValue;

            this.lowerChild = new LazyDimension_InterpolatorBox<OutputType>(this.outputConverter, this.depthFromRoot + 1);
            this.upperChild = new LazyDimension_InterpolatorBox<OutputType>(this.outputConverter, this.depthFromRoot + 1);

            this.lowerChild.AddDatapoints(lowerPoints);
            // this child was constructed all at once using a known size and couldn't have been queried in the meanwhile,
            // so the child can use all of its points for determining where to split (this won't cause inconsistencies across runs, even as more data gets added)
            this.lowerChild.PermitSplitting();

            this.upperChild.AddDatapoints(upperPoints);
            // this child was constructed all at once using a known size and couldn't have been queried in the meanwhile,
            // so the child can use all of its points for determining where to split (this won't cause inconsistencies across runs, even as more data gets added)
            this.upperChild.PermitSplitting();
        }

        private void PermitSplitting()
        {
            this.numPointsAtNextSplit = this.NumDatapoints;
        }
        public int NumDatapoints
        {
            get
            {
                return this.datapoints.Count + this.pendingDatapoints.Count;
            }
        }
        public double Weight
        {
            get
            {
                return this.outputConverter.ConvertToDistribution(this.aggregateOutput).Weight;
            }
        }
        
        public OutputType AggregateOutput
        {
            get
            {
                return this.aggregateOutput;
            }
        }

        private List<LazyDimension_Datapoint<OutputType>> pendingDatapoints = new List<LazyDimension_Datapoint<OutputType>>();

        private int splitDimension;
        private double splitValue;
        private LazyDimension_InterpolatorBox<OutputType> lowerChild;
        private LazyDimension_InterpolatorBox<OutputType> upperChild;
        private OutputType aggregateOutput;
        private List<LazyDimension_Datapoint<OutputType>> datapoints;
        private int numPointsAtNextSplit = 1;
        private int depthFromRoot;
        INumerifier<OutputType> outputConverter;
        private int boxId;
    }
}
