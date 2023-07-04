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

            if (this.datapoints.Count <= 1)
                return;

            int maxDimensions = this.datapoints[this.datapoints.Count - 1].GetInputs().GetNumCoordinates();
            if (maxDimensions < 1)
                return;
            // If we check all dimensions of all datapoints and choose the dimension that minimizes the error, that can cause two problems:
            // 1. It can take a long time
            // 2. It can contribute to overfitting, where we considered lots of models and chose only the best one, and used its past uncertainty to estimate its future uncertainty
            // Instead, we check all coordinates of only a few datapoints, which is much faster.
            // We still compute uncertainty using all points
            int numDatapointsToCheck = (int)Math.Min(Math.Log(maxDimensions, 2) * 2, this.datapoints.Count / 2);

            // get the outputs for the given datapoints
            List<double> outputs = new List<double>();
            double maxOutput = double.NegativeInfinity;
            double minOutput = double.PositiveInfinity;
            for (int i = this.datapoints.Count - 1; i >= this.datapoints.Count - numDatapointsToCheck; i--)
            {
                double output = this.outputConverter.ConvertToDistribution(this.datapoints[i].GetOutput()).Mean;
                if (output < minOutput)
                    minOutput = output;
                if (output > maxOutput)
                    maxOutput = output;
                outputs.Add(output);
            }
            // Decide what output threshold to use
            // We don't want one child box to be very small because that increases the risk of overfitting
            // The risk of overfitting should be minimal if we try to split datapoints into above- and below-median outputs
            // However, if the data is skewed, we can get a lower average error if we split based on the average output
            // We combine these two estimates into one threshold here
            double medianOutput = MedianUtils.EstimateMedian(outputs);
            double outputRangeMiddle = (minOutput + maxOutput) / 2;
            double middleOutput = (medianOutput + outputRangeMiddle) / 2;

            int bestDimensionToSplit = 0;
            int bestDimensionScore = -1;
            double splitValue = 0;
            // check each input dimension
            for (int dimension = 0; dimension < maxDimensions; dimension++)
            {
                // get the inputs for the given datapoints in this dimension
                List<double> inputs = new List<double>();
                double minInput = double.PositiveInfinity;
                double maxInput = double.NegativeInfinity;
                for (int i = this.datapoints.Count - 1; i >= this.datapoints.Count - numDatapointsToCheck; i--)
                {
                    double input = this.datapoints[i].GetInputs().GetInput(dimension);
                    if (input < minInput)
                        minInput = input;
                    if (input > maxInput)
                        maxInput = input;
                    inputs.Add(input);
                }
                // Group the inputs into those having high outputs and those having low outputs
                Distribution inputsForLowOutput = new Distribution();
                Distribution inputsForHighOutput = new Distribution();
                for (int i = 0; i < inputs.Count; i++)
                {
                    Distribution thisPoint = Distribution.MakeDistribution(inputs[i], 0, 1);
                    if (outputs[i] > middleOutput)
                    {
                        inputsForHighOutput = inputsForHighOutput.Plus(thisPoint);
                    }
                    else
                    {
                        inputsForLowOutput = inputsForLowOutput.Plus(thisPoint);
                    }
                }
                // Now we choose an input split threshold based on the datapoints
                // Note that this isn't necessarily the best split value for the inputs that we did analyze, but it should be a good split value for all of the inputs overall, anyway
                double middleInput = (minInput + maxInput) / 2;
                double lowMiddle = inputsForLowOutput.GetMeanOr(middleInput);
                double highMiddle = inputsForHighOutput.GetMeanOr(middleInput);
                double inputThreshold = (lowMiddle + highMiddle) / 2;
                // count the number of cases where larger input is correlated with larger output
                int numPolarityMatches = countNumPolarityMatches(inputs, outputs, inputThreshold, middleOutput);

                if (numPolarityMatches > bestDimensionScore)
                {
                    bestDimensionScore = numPolarityMatches;
                    bestDimensionToSplit = dimension;
                    splitValue = inputThreshold;
                }
                // If this dimension was perfect, we can stop
                //if (numPolarityMatches >= outputs.Count)
                //    break;
            }

            this.Split(bestDimensionToSplit, splitValue);
        }
        private int countNumPolarityMatches(List<double> inputs, List<double> outputs, double inputThreshold, double outputThreshold)
        {
            int numPolarityMatches = 0;
            for (int i = 0; i < outputs.Count; i++)
            {
                if ((outputs[i] > outputThreshold) == (inputs[i] > inputThreshold))
                {
                    numPolarityMatches++;
                }
            }
            // if the polarity is usually backwards, that also counts as a good correlation
            if (numPolarityMatches * 2 < outputs.Count)
                numPolarityMatches = outputs.Count - numPolarityMatches;
            return numPolarityMatches;
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
