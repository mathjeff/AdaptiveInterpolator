using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using StatLists;
using AdaptiveInterpolator;

// the SmartInterpolationBox will figure out which dimensions are worth splitting and when
namespace AdaptiveLinearInterpolation
{
    class SmartInterpolationBox<SummaryType> // : IComparer<Datapoint>, ICombiner<Datapoint>
    {
        public SmartInterpolationBox(HyperBox<SummaryType> boundary, INumerifier<SummaryType> scoreHandler, int depthFromRoot = 0)
        {
            //int i;
            this.scoreHandler = scoreHandler;
            this.currentBoundary = boundary;
            this.observedBoundary = null;
            this.splitDimension = -1;
            this.datapoints = new LinkedList<IDatapoint<SummaryType>>();
            this.pendingDatapoints = new LinkedList<IDatapoint<SummaryType>>();
            this.itemSummary = scoreHandler.Default();
            this.depthFromRoot = depthFromRoot;
#if MIN_SPLIT_COUNTS
            this.minSplitCounts = new int[boundary.NumDimensions];
#endif
#if CACHE_STDDEV
            this.inputs = new Distribution[boundary.NumDimensions];
            for (i = 0; i < this.inputs.Length; i++)
            {
                this.inputs[i] = new Distribution();
            }
#endif
        }
        // adds a datapoint
        public void AddDatapoint(IDatapoint<SummaryType> newDatapoint)
        {
            this.pendingDatapoints.AddLast(newDatapoint);
        }
        public bool RemoveDatapoint(IDatapoint<SummaryType> datapoint)
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
            this.itemSummary = this.scoreHandler.Remove(this.itemSummary, datapoint.Item);
            // We should update the observed boundary here, but it's not too much of a problem if we don't
            // Furthermore, the only project that currently (2012-10-20) uses this project will always re-insert the datapoint with the same inputs anyway
            if (this.lowerChild != null)
                this.lowerChild.RemoveDatapoint(datapoint);
            if (this.upperChild != null)
                this.upperChild.RemoveDatapoint(datapoint);
            return true;
        }
        // adds a datapoint and does not consider splitting
        private void AddPointNowWithoutSplitting(IDatapoint<SummaryType> newDatapoint)
        {
            // keep track of the outputs we've observed
            this.itemSummary = this.scoreHandler.Combine(this.itemSummary, newDatapoint.Item);
            //this.scores = this.scores.Plus(newDatapoint.Score);
#if CACHE_STDDEV
            int i;
            for (i = 0; i < this.inputs.Length; i++)
            {
                this.inputs[i] = this.inputs[i].Plus(newDatapoint.InputCoordinates[i]);
            }
#endif

            // if this datapoint falls outside our promised boundary, then expand our boundary to include it
            if (this.observedBoundary == null)
                this.observedBoundary = new HyperBox<SummaryType>(newDatapoint);
            else
                this.observedBoundary.ExpandToInclude(newDatapoint);
            if (this.currentBoundary == null)
                this.currentBoundary = new HyperBox<SummaryType>(newDatapoint);
            else
                this.currentBoundary.ExpandToInclude(newDatapoint);
            // add it to our set
            //this.datapointsByOutput.Add(newDatapoint, Distribution.MakeDistribution(newDatapoint.Output, 0, 1));
            this.datapoints.AddLast(newDatapoint);
            //if (this.datapointsByInput != null)
            //    this.datapointsByInput.Add(newDatapoint, newDatapoint);
            /*
            foreach (SimpleInterpolationBox box in this.simpleChildren)
            {
                box.AddDatapoint(newDatapoint);
            }
            */
            if (this.lowerChild != null)
            {
                if (newDatapoint.InputCoordinates[this.splitDimension] > this.lowerChild.currentBoundary.Coordinates[this.splitDimension].HighCoordinate)
                    this.upperChild.AddDatapoint(newDatapoint);
                else
                    this.lowerChild.AddDatapoint(newDatapoint);
#if MIN_SPLIT_COUNTS
                this.UpdateSplitCounts();
#endif
            }
        }
        // Exactly the same as calling AddDatapoint a bunch of times, but potentially faster
        public void AddDatapoints(IEnumerable<IDatapoint<SummaryType>> newDatapoints)
        {
            foreach (IDatapoint<SummaryType> newDatapoint in newDatapoints)
            {
                this.AddDatapoint(newDatapoint);
            }
        }
        public Distribution Interpolate(double[] location)
        {
            this.ApplyPendingPoints();
            // finally, return the 
            return this.scoreHandler.ConvertToDistribution(this.itemSummary);
            //return this.scores;
        }
        // moves any points from the list of pendingPoints into the main list, and updates any stats
        private void ApplyPendingPoints()
        {
            int totalNumPoints = this.datapoints.Count + this.pendingDatapoints.Count;

            // Now actually add those points and do the split
            foreach (IDatapoint<SummaryType> datapoint in this.pendingDatapoints)
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
        // estimates the product of amount of variation in each dimension
        public double GetInputVariation()
        {
            this.ApplyPendingPoints();
#if CACHE_STDDEV
            double variation = 1;
            double currentVariation;
            int i;
            //foreach (Distribution distribution in this.inputs)
            for (i = 0; i < this.NumDimensions; i++)
            {
                //currentVariation = Math.Min(this.inputs[i].StdDev, this.currentBoundary.Coordinates[i].Width / 4);
                //currentVariation = (this.inputs[i].StdDev + this.currentBoundary.Coordinates[i].Width / this.NumDatapoints);
                currentVariation = this.inputs[i].StdDev;
                variation *= currentVariation;
            }
            return variation;
#else
            return this.observedBoundary.Area;
#endif
        }
        public double GetInputArea()
        {
            this.ApplyPendingPoints();
            return this.currentBoundary.Area;
        }
        public double GetScoreSpread()
        {
            this.ApplyPendingPoints();
            //Distribution outputs = this.datapointsByOutput.CombineAll();
            //double spread = outputs.StdDev;
            double spread = this.scoreHandler.ConvertToDistribution(this.itemSummary).StdDev;
            return spread;
        }
        public int NumDatapoints
        {
            get
            {
                return this.datapoints.Count + this.pendingDatapoints.Count;
            }
        }
        public int NumDimensions
        {
            get
            {
                return this.currentBoundary.NumDimensions;
            }
        }
        public bool ChildrenExist()
        {
            return (this.lowerChild != null) && (this.upperChild != null);
        }
        public SmartInterpolationBox<SummaryType> ChooseChild(double[] coordinates)
        {
            this.ApplyPendingPoints();
            if (this.lowerChild == null)
                return null;
            if (this.lowerChild.currentBoundary.Contains(coordinates))
                return this.lowerChild;
            if (this.upperChild.currentBoundary.Contains(coordinates))
                return this.upperChild;
            if (this.lowerChild.currentBoundary.Coordinates[this.splitDimension].HighCoordinate < coordinates[this.splitDimension])
                return this.upperChild;
            else
                return this.lowerChild;
        }

        /*
        #region Functions for IComparer<Datapoint>
        public int Compare(Datapoint a, Datapoint b)
        {
            return a.Score.CompareTo(b.Score);
        }
        #endregion

        #region Functions for IAdder<Datapoint>
        public Datapoint Default()
        {
            return new Datapoint(null, 0);
        }
        public Datapoint Combine(Datapoint a, Datapoint b)
        {
            Datapoint result = new Datapoint(null, a.Score + b.Score);
            return result;
        }
        #endregion

        */

        // tells whether it is worth considering a split, given the current number of datapoints and also the number that we had at the last split
        private int RequiredNumPointsToSplit(int numPointsAtLastConsideredSplit)
        {
            // The reason we decrease the splitting frequency as our ancestry increases is to avoid redudant splits that are going to get thrown out soon.
            // If our parent uses the same split factor as us and is receiving random inputs, then our parent is expected to want to split at about the same time we do, or maybe slightly later.
            // If our parent splits slightly after us, then that means we did a bunch of effort splitting that soon became irrelevant.
            // So, we put our split threshold such that it should be slightly after our parent is expected to split.
            // Our split threshold is mostly only intended to trigger if lots of points are getting concentrated specifically into this box.
            int result = (int)((double)numPointsAtLastConsideredSplit * ((double)this.depthFromRoot + 1.5));
            if (result <= numPointsAtLastConsideredSplit)
                result = numPointsAtLastConsideredSplit + 1;
            return result;
        }
        private void PermitSplitting()
        {
            this.numPointsAtNextSplit = this.NumDatapoints;
        }
        private void ConsiderSplitting()
        {
            if (this.datapoints.Count <= 1)
                return;
            int i, j;
            if (this.numPreplannedSplits > 0)
            {
                this.Split(this.splitDimension);
                return;
            }
            if (this.NumDimensions == 1)
            {
                // If there's only one dimension, it has to be the one to split
                if (this.observedBoundary.Coordinates[0].Width > 0)
                    this.Split(0);
                return;
            }
            // setup a bunch of SimpleInterpolationBox to calculate the error we'd get if we couldn't split any particular dimension
            // setup the simple children
            SimpleInterpolationBox<SummaryType>[] simpleChildren = new SimpleInterpolationBox<SummaryType>[this.NumDimensions];
            for (i = 0; i < simpleChildren.Length; i++)
            {
                // tell it to split each dimension in order, except for one
                LinkedList<int> dimensionsToSplit = new LinkedList<int>();
                for (j = 0; j < this.NumDimensions; j++)
                {
                    if (j != i)
                    {
                        dimensionsToSplit.AddLast(j);
                    }
                }
                SimpleInterpolationBox<SummaryType> box = new SimpleInterpolationBox<SummaryType>(this.currentBoundary, dimensionsToSplit, i, this.scoreHandler);
                box.AddDatapoints(this.datapoints);
                simpleChildren[i] = box;
            }

            double bestError = -1;
            int dimension = -1;

            // identify the dimension such that if we cannot split that dimension, then we get the most error
            for (i = 0; i < this.NumDimensions; i++)
            {
                if (this.observedBoundary.Coordinates[i].Width > 0)
                {
                    double currentError = simpleChildren[i].GetError();
                    if (currentError > bestError)
                    {
                        bestError = currentError;
                        dimension = i;
                    }
                }
            }
            // if we can't split, then give up
            if (dimension < 0)
                return;
            // if the worst child didn't split in every dimension, then we don't have enough data to worry about longterm convergence
            if (simpleChildren[dimension].Depth <= this.NumDimensions - 1)
            {
                // so, we simply split the first split dimension of the best child
                for (i = 0; i < this.NumDimensions; i++)
                {
                    int d = simpleChildren[i].SplitDimension;
                    if (this.observedBoundary.Coordinates[d].Width > 0)
                    {
                        if (simpleChildren[i].GetError() < bestError)
                        {
                            dimension = d;
                            bestError = simpleChildren[i].GetError();
                        }
                    }
                }
            }
            this.Split(dimension);
        }
        public void Split(int dimension)
        {
            this.splitDimension = dimension;

            // make sure that datapointsByInput exists, and that it sorts in the correct dimension
            //this.datapointsByInput = new StatList<Datapoint, Datapoint>(new DatapointComparer(dimension), this);

            // compute the coordinates of each child
#if true
            LinkedList<double> inputs = new LinkedList<double>();
            foreach (IDatapoint<SummaryType> datapoint in this.datapoints)
            {
                inputs.AddLast(datapoint.InputCoordinates[dimension]);
            }
            double splitValue = MedianUtils.EstimateMedian(inputs);
            // check for the possibility that MedianUtils was unlucky and found something on the edge of observedBoundary
            if (splitValue >= this.observedBoundary.Coordinates[dimension].HighCoordinate || splitValue <= this.observedBoundary.Coordinates[dimension].LowCoordinate)
            {
                splitValue = (this.observedBoundary.Coordinates[dimension].LowCoordinate + this.observedBoundary.Coordinates[dimension].HighCoordinate) / 2;
                // check for the possibility that rounding error is preventing a split
                if (splitValue >= this.observedBoundary.Coordinates[dimension].HighCoordinate || splitValue <= this.observedBoundary.Coordinates[dimension].LowCoordinate)
                {
                    this.lowerChild = this.upperChild = null;
                    return;
                }
            }
#else
            double splitValue = (this.observedBoundary.Coordinates[dimension].LowCoordinate + this.observedBoundary.Coordinates[dimension].HighCoordinate) / 2;
            if (splitValue == this.currentBoundary.Coordinates[dimension].HighCoordinate || splitValue == this.currentBoundary.Coordinates[dimension].LowCoordinate)
                splitValue = (this.currentBoundary.Coordinates[dimension].LowCoordinate + this.currentBoundary.Coordinates[dimension].HighCoordinate) / 2;
#endif
            HyperBox<SummaryType> lowerBoundary = new HyperBox<SummaryType>(this.currentBoundary);
            lowerBoundary.Coordinates[dimension].HighCoordinate = splitValue;
            lowerBoundary.Coordinates[dimension].HighInclusive = true;
            HyperBox<SummaryType> upperBoundary = new HyperBox<SummaryType>(this.currentBoundary);
            upperBoundary.Coordinates[dimension].LowCoordinate = splitValue;
            upperBoundary.Coordinates[dimension].LowInclusive = false;

            //if (this.NumDimensions == 3 && dimension == 0 && splitValue == 900 && this.currentBoundary.Coordinates[0].LowCoordinate == 0)
            //    upperBoundary = upperBoundary;

            // decide which data goes in which child
            LinkedList<IDatapoint<SummaryType>> lowerPoints = new LinkedList<IDatapoint<SummaryType>>();
            LinkedList<IDatapoint<SummaryType>> upperPoints = new LinkedList<IDatapoint<SummaryType>>();
            foreach (IDatapoint<SummaryType> datapoint in this.datapoints)
            {
                if (lowerBoundary.Contains(datapoint))
                    lowerPoints.AddLast(datapoint);
                if (upperBoundary.Contains(datapoint))
                    upperPoints.AddLast(datapoint);
            }
            this.lowerChild = new SmartInterpolationBox<SummaryType>(lowerBoundary, this.scoreHandler, this.depthFromRoot + 1);
            this.upperChild = new SmartInterpolationBox<SummaryType>(upperBoundary, this.scoreHandler, this.depthFromRoot + 1);
            // If we're told that we don't yet have to spend a lot of effort choosing a split dimension, then just ask the children to split the next dimension
            if (this.numPreplannedSplits > 1)
            {
                int nextSplitDimension = (this.splitDimension + 1) % this.NumDimensions;
                this.lowerChild.ForceSplits(nextSplitDimension, this.numPreplannedSplits - 1);
                this.upperChild.ForceSplits(nextSplitDimension, this.numPreplannedSplits - 1);
            }

            this.lowerChild.AddDatapoints(lowerPoints);
            // this child was constructed all at once using a known size and couldn't have been queried in the meanwhile,
            // so the child can use all of its points for determining where to split (this won't cause inconsistencies across runs, even as more data gets added)
            this.lowerChild.PermitSplitting();

            this.upperChild.AddDatapoints(upperPoints);
            // this child was constructed all at once using a known size and couldn't have been queried in the meanwhile,
            // so the child can use all of its points for determining where to split (this won't cause inconsistencies across runs, even as more data gets added)
            this.upperChild.PermitSplitting();
#if false
            this.lowerChild.ApplyPendingPoints();
            this.upperChild.ApplyPendingPoints();
#endif
#if MIN_SPLIT_COUNTS
            this.UpdateSplitCounts();
#endif
        }
#if MIN_SPLIT_COUNTS
        public void UpdateSplitCounts()
        {
            int i;
            for (i = 0; i < this.NumDimensions; i++)
            {
                this.minSplitCounts[i] = Math.Min(this.lowerChild.minSplitCounts[i], this.upperChild.minSplitCounts[i]);
            }
            if (this.splitDimension >= 0)
                this.minSplitCounts[this.splitDimension]++;
        }
#endif
        public void ForceSplits(int startingDimension, int numSplits)
        {
            this.splitDimension = startingDimension;
            this.numPreplannedSplits = numSplits;
        }
        public SummaryType ItemSummary
        {
            get
            {
                this.ApplyPendingPoints();
                return itemSummary;
            }
            set
            {
                itemSummary = value;
            }
        }
        public IEnumerable<IDatapoint<SummaryType>> Datapoints { get { return this.datapoints.Concat(this.pendingDatapoints); } }
        public HyperBox<SummaryType> ObservedBoundary { get { return this.observedBoundary; } }
        public SmartInterpolationBox<SummaryType> UpperChild
        {
            get
            {
                return this.upperChild;
            }
        } 
        public SmartInterpolationBox<SummaryType> LowerChild
        {
            get
            {
                return this.lowerChild;
            }
        }

        private HyperBox<SummaryType> currentBoundary;
        private HyperBox<SummaryType> observedBoundary;
        private int splitDimension;
        private int numPreplannedSplits;
        private SmartInterpolationBox<SummaryType> lowerChild;
        private SmartInterpolationBox<SummaryType> upperChild;
        //private SimpleInterpolationBox[] simpleChildren;
        private SummaryType itemSummary;
        private LinkedList<IDatapoint<SummaryType>> datapoints;
        private LinkedList<IDatapoint<SummaryType>> pendingDatapoints;    // any points that are waiting in a queue to be added. Their values aren't included in anything yet
        //private StatList<Datapoint, Distribution> datapointsByOutput;
        //private StatList<Datapoint, Datapoint> datapointsByInput;
        //private double totalError;
        private int numPointsAtNextSplit = 1;
        private int depthFromRoot;
#if MIN_SPLIT_COUNTS
        private int[] minSplitCounts;  // the minimum number of times that dimension[index] was split, over any path through the children
#endif
#if CACHE_STDDEV
        private Distribution[] inputs;
#endif
        INumerifier<SummaryType> scoreHandler;
    }
}
