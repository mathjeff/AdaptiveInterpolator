﻿using AdaptiveInterpolation;
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
            this.datapoints = new List<ILazyDatapoint<OutputType>>();
            this.pendingDatapoints = new List<ILazyDatapoint<OutputType>>();
            this.aggregateOutput = this.outputConverter.Default();
            this.depthFromRoot = depthFromRoot;
            nextBoxId++;
            this.boxId = nextBoxId;
        }

        public void AddDatapoint(ILazyDatapoint<OutputType> datapoint)
        {
            this.pendingDatapoints.Add(datapoint);
            // System.Diagnostics.Debug.WriteLine("AddDatapoint in box at depth " + this.depthFromRoot + ", num points = " + this.NumDatapoints + ", num coordinates = " + datapoint.GetInputs().GetNumCoordinates());
        }
        public void AddDatapoints(IEnumerable<ILazyDatapoint<OutputType>> newDatapoints)
        {
            foreach (ILazyDatapoint<OutputType> newDatapoint in newDatapoints)
            {
                this.AddDatapoint(newDatapoint);
            }
        }
        public bool RemoveDatapoint(ILazyDatapoint<OutputType> datapoint)
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
            bool upper = this.chooseUpperChild(inputs);
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
        private bool chooseUpperChild(ILazyDatapoint<OutputType> datapoint)
        {
            return this.chooseUpperChild(datapoint.GetInputs());
        }
        private bool chooseUpperChild(LazyInputs inputs)
        {
            double lowerWeight = 0;
            double upperWeight = 0;
            foreach (ThresholdComparison threshold in this.splits)
            {
                double value = inputs.GetInput(threshold.Dimension);
                if (threshold.Evaluate(value))
                    upperWeight += threshold.Weight;
                else
                    lowerWeight += threshold.Weight;
            }
            return upperWeight > lowerWeight;
        }

        // moves any points from the list of pendingPoints into the main list, and updates any stats
        private void ApplyPendingPoints()
        {
            int eventualNumAdditions = this.numAdditions + this.pendingDatapoints.Count;
            // Now actually add those points and do the split
            foreach (ILazyDatapoint<OutputType> datapoint in this.pendingDatapoints)
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
                if (this.numAdditions >= this.numAdditionsAtNextSplit)
                {
                    this.numAdditionsAtNextSplit = this.RequiredNumAdditionsToSplit(this.numAdditionsAtNextSplit);

                    // only actually do the split if this is the last planned split
                    if (this.numAdditionsAtNextSplit > eventualNumAdditions)
                        this.ConsiderSplitting();
                }
            }
            this.pendingDatapoints.Clear();
        }

        private void AddPointNowWithoutSplitting(ILazyDatapoint<OutputType> newDatapoint)
        {
            this.numAdditions++;

            // keep track of the outputs we've observed
            this.aggregateOutput = this.outputConverter.Combine(this.aggregateOutput, newDatapoint.GetOutput());

            // add it to our set
            this.datapoints.Add(newDatapoint);
            if (this.lowerChild != null)
            {
                if (this.chooseUpperChild(newDatapoint))
                    this.upperChild.AddDatapoint(newDatapoint);
                else
                    this.lowerChild.AddDatapoint(newDatapoint);
            }
        }

        // tells whether it is worth considering a split, given the current number of datapoints and also the number that we had at the last split
        private int RequiredNumAdditionsToSplit(int numAdditionsAtLastConsideredSplit)
        {
            // The reason we decrease the splitting frequency as our ancestry increases is to avoid redudant splits that are going to get thrown out soon.
            // If our parent uses the same split factor as us and is receiving random inputs, then our parent is expected to want to split at about the same time we do, or maybe slightly later.
            // If our parent splits slightly after us, then that means we did a bunch of effort splitting that soon became irrelevant.
            // So, we put our split threshold such that it should be slightly after our parent is expected to split.
            // Our split threshold is mostly only intended to trigger if lots of points are getting concentrated specifically into this box.

            int result = (int)((double)numAdditionsAtLastConsideredSplit * 2 + Math.Sqrt(numAdditionsAtLastConsideredSplit));
            if (result <= numAdditionsAtLastConsideredSplit)
                result = numAdditionsAtLastConsideredSplit + 1;
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
            int maxNumDatapointsToCheck = this.datapoints.Count / 2;
            int initialNumDatapointsToCheck = 4;
            int targetNumDimensionsToUse = Math.Max(1, (int)Math.Log(maxDimensions, 2) * 2);

            double fractionOfDimensionsToUse = (double)targetNumDimensionsToUse / (double)maxDimensions;
            double initialFractionDatapointsToUse = (double)initialNumDatapointsToCheck / (double)maxNumDatapointsToCheck;
            // Determine how quickly to grow the number of datapoints used based on how many dimensions we're considering in this round
            double logDatapointsPerLogDimensions = Math.Log(initialFractionDatapointsToUse, fractionOfDimensionsToUse);
            // Don't need to grow the number of datapoints faster than the number of dimensions
            if (logDatapointsPerLogDimensions > 1)
                logDatapointsPerLogDimensions = 1;

            List<int> candidateDimensions = new List<int>();
            for (int dimension = 0; dimension < maxDimensions; dimension++)
            {
                candidateDimensions.Add(dimension);
            }
            // look for two adjacent datapoints with different outputs
            int interestingDatapointIndex = this.datapoints.Count - 1;
            double lastValue = 0;
            for (int i = 0; i < this.datapoints.Count; i++)
            {
                int candidate = this.datapoints.Count - 1 - i;
                double value = this.outputConverter.ConvertToDistribution(this.datapoints[candidate].GetOutput()).Mean;
                if (i == 0)
                {
                    lastValue = value;
                }
                else
                {
                    if (value != lastValue)
                    {
                        interestingDatapointIndex = candidate;
                        break;
                    }
                }
            }
            List<ThresholdComparison> splits = new List<ThresholdComparison>();
            while (true)
            {
                // Determine how many datapoints to check based on the number of dimensions in this round
                // When there are fewer dimensions, we can afford to spend more time on each
                int numDatapointsToCheck = (int)(initialNumDatapointsToCheck * Math.Pow((double)maxDimensions / (double)candidateDimensions.Count, logDatapointsPerLogDimensions));

                // If we've reached the goal number of dimensions, use as many datapoints as allowed for determining the final split values
                if (candidateDimensions.Count <= targetNumDimensionsToUse)
                    numDatapointsToCheck = maxNumDatapointsToCheck;

                // clamp to valid number of dimensions
                if (numDatapointsToCheck > maxNumDatapointsToCheck)
                    numDatapointsToCheck = maxNumDatapointsToCheck;
                if (numDatapointsToCheck < initialNumDatapointsToCheck)
                    numDatapointsToCheck = initialNumDatapointsToCheck;

                // Determine which datapoints to check
                int lastDatapointIndexExclusive = this.datapoints.Count - 1;
                if (lastDatapointIndexExclusive - numDatapointsToCheck > interestingDatapointIndex)
                {
                    // If we check the last few datapoints, all the output values will be the same
                    lastDatapointIndexExclusive = interestingDatapointIndex + 2;
                }
                int firstDatapointIndex = Math.Max(0, lastDatapointIndexExclusive - numDatapointsToCheck);

                List<ThresholdComparison> candidateSplits = new List<ThresholdComparison>();
                List<double> weights = new List<double>();
                foreach (int dimension in candidateDimensions)
                {
                    ThresholdComparison candidateSplit = getCandidateSplit(dimension, firstDatapointIndex, lastDatapointIndexExclusive);
                    candidateSplits.Add(candidateSplit);
                    weights.Add(candidateSplit.Weight);
                }
                double medianWeight = MedianUtils.EstimateMedian(weights);
                // If we've reached the target number of dimensions, we're done
                if (candidateSplits.Count <= targetNumDimensionsToUse)
                {
                    splits = candidateSplits;
                    break;
                }

                // select about half of dimensions based on their score
                List<int> goodDimensions = new List<int>();
                for (int i = 0; i < candidateDimensions.Count; i++)
                {
                    if (weights[i] > medianWeight)
                        goodDimensions.Add(i);
                }

                // update list of dimensions to check
                if (goodDimensions.Count > 0)
                {
                    // We found at least one dimension better than at least one other dimension, so we keep the good dimensions
                    candidateDimensions = goodDimensions;
                }
                else
                {
                    // We didn't notice any dimensions better than any others so we just arbitrarily remove some
                    List<int> arbitraryDimensions = new List<int>();
                    for (int i = 0; i < candidateDimensions.Count / 2; i++)
                    {
                        arbitraryDimensions.Add(i);
                    }
                    candidateDimensions = arbitraryDimensions;
                }
            }

            // get the value to split at
            this.Split(splits);
        }


        private ThresholdComparison getCandidateSplit(int dimension, int firstDatapointIndex, int lastDatapointIndex)
        {
            List<double> outputs = new List<double>();
            // get the inputs for the given datapoints in this dimension
            List<double> inputs = new List<double>();
            double minInput = double.PositiveInfinity;
            double maxInput = double.NegativeInfinity;
            for (int i = firstDatapointIndex; i < lastDatapointIndex; i++)
            {
                ILazyDatapoint<OutputType> datapoint = this.datapoints[i];
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
            double inputThreshold = (minInput + maxInput) / 2;

            // Now we split points based on the threshold
            Distribution low = new Distribution();
            Distribution high = new Distribution();
            for (int i = 0; i < outputs.Count; i++)
            {
                if (inputs[i] <= inputThreshold)
                    low = low.Plus(outputs[i]);
                else
                    high = high.Plus(outputs[i]);
            }
            double dimensionScore;
            if (low.Weight <= 0 || high.Weight <= 0)
            {
                dimensionScore = 0;
            }
            else
            {
                // We want to split boxes in a way that even if some other, unexpected datapoints get added later, this is still a good split.
                // Currently we compute the amount of unexpected change that must be added before the box with higher output becomes the box with lower output
                dimensionScore = Math.Abs(high.Mean - low.Mean) * Math.Min(high.Weight, low.Weight);
            }

            return new ThresholdComparison(dimension, inputThreshold, dimensionScore, high.Mean > low.Mean);
        }

        private void Split(List<ThresholdComparison> splits)
        {
            //Console.WriteLine("Splitting box at depth " + this.depthFromRoot + " having " + this.datapoints.Count + " points using " + splits.Count + " dimensions");
            this.splits = splits;

            List<ILazyDatapoint<OutputType>> lowerPoints = new List<ILazyDatapoint<OutputType>>();
            List<ILazyDatapoint<OutputType>> upperPoints = new List<ILazyDatapoint<OutputType>>();
            foreach (ILazyDatapoint<OutputType> datapoint in this.datapoints)
            {
                if (chooseUpperChild(datapoint))
                    upperPoints.Add(datapoint);
                else
                    lowerPoints.Add(datapoint);
            }

            if (lowerPoints.Count < 1 || upperPoints.Count < 1)
            {
                // If all the points fell on the same side of the split, then there's no need to split anymore
                return;
            }

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
            this.numAdditionsAtNextSplit = this.numAdditions;
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

        private List<ILazyDatapoint<OutputType>> pendingDatapoints = new List<ILazyDatapoint<OutputType>>();

        private List<ThresholdComparison> splits;

        private LazyDimension_InterpolatorBox<OutputType> lowerChild;
        private LazyDimension_InterpolatorBox<OutputType> upperChild;
        private OutputType aggregateOutput;
        private List<ILazyDatapoint<OutputType>> datapoints;
        private int numAdditionsAtNextSplit = 1;
        private int depthFromRoot;
        INumerifier<OutputType> outputConverter;
        private int boxId;

        // The number of times a datapoint was added to this box
        // Because datapoints can be removed, this might be larger than the number of datapoints currently in this box
        private int numAdditions;
    }
}
