using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using StatLists;
using AdaptiveInterpolator;

// the SimpleInterpolationBox will split in exactly the dimension it is told to split in
namespace AdaptiveLinearInterpolation
{
    class SimpleInterpolationBox<ScoreType> : ICombiner<IDatapoint<ScoreType>>
    {

        public SimpleInterpolationBox(HyperBox<ScoreType> boundary, LinkedList<int> dimensionSplitOrder, int dimToSort, INumerifier<ScoreType> scoreHandler)
        {
            this.scoreHandler = scoreHandler;
            this.currentBoundary = boundary;
            this.observedBoundary = null;
            this.dimensionsToSplit = dimensionSplitOrder;
            //this.datapointsByInput = new StatList<Datapoint, Datapoint>(new DatapointComparer(dimToSort), this);
            this.unpropogatedDatapoints = new LinkedList<IDatapoint<ScoreType>>();
            this.dimensionToSort = dimToSort;
            this.numDatapoints = 0;
            this.splitDimension = dimensionSplitOrder.First.Value;
            this.depth = 0;
            //this.splitDimension_inputs = new Distribution();

        }
        public void AddDatapoints(IEnumerable<IDatapoint<ScoreType>> newDatapoints)
        {
            if (newDatapoints.Count() <= 0)
                return;
            if (this.observedBoundary == null)
            {
                this.observedBoundary = new HyperBox<ScoreType>(newDatapoints.First());
            }
            foreach (IDatapoint<ScoreType> newDatapoint in newDatapoints)
            {
                // if this datapoint falls outside our promised boundary, then expand our boundary to include it
                this.observedBoundary.ExpandToInclude(newDatapoint);
                this.currentBoundary.ExpandToInclude(newDatapoint);
                /* // move towards the median
                double adjustment = Math.Min(this.observedBoundary.Coordinates[this.SplitDimension].HighCoordinate - this./splitDimension_inputs.Mean,
                    this.splitDimension_inputs.Mean - this.observedBoundary.Coordinates[this.splitDimension].LowCoordinate);
                double pretendInput;
                if (newDatapoint.InputCoordinates[this.SplitDimension] > this.splitDimension_inputs.Mean)
                    pretendInput = this.splitDimension_inputs.Mean + adjustment;
                else
                    pretendInput = this.splitDimension_inputs.Mean - adjustment;
                this.splitDimension_inputs = this.splitDimension_inputs.Plus(pretendInput);
                //this.splitDimension_inputs = this.splitDimension_inputs.Plus(newDatapoint.InputCoordinates[this.dimensionsToSplit.First.Value]);
                */

                // keep track of a datapoint on each extreme
                if (this.maxPoint == null || this.maxPoint.InputCoordinates[this.dimensionToSort] <= newDatapoint.InputCoordinates[this.dimensionToSort])
                    this.maxPoint = newDatapoint;
                // keep track of a datapoint on each extreme
                if (this.minPoint == null || this.minPoint.InputCoordinates[this.dimensionToSort] >= newDatapoint.InputCoordinates[this.dimensionToSort])
                    this.minPoint = newDatapoint;
            }
            this.numDatapoints += newDatapoints.Count();
            if (this.lowerChild == null && this.upperChild == null)
            {
                // considering subdividing into more boxes
                this.unpropogatedDatapoints = this.unpropogatedDatapoints.Concat(newDatapoints);
                this.ConsiderSplitting();
            }
            else
            {
                // add these points to the existing child boxes
                LinkedList<IDatapoint<ScoreType>> lowerPoints = new LinkedList<IDatapoint<ScoreType>>();
                LinkedList<IDatapoint<ScoreType>> upperPoints = new LinkedList<IDatapoint<ScoreType>>();
                foreach (IDatapoint<ScoreType> newDatapoint in newDatapoints)
                {
                    if (this.lowerChild.currentBoundary.Contains(newDatapoint))
                        lowerPoints.AddLast(newDatapoint);
                    if (this.upperChild.currentBoundary.Contains(newDatapoint))
                        upperPoints.AddLast(newDatapoint);
                }
                this.lowerChild.AddDatapoints(lowerPoints);
                this.upperChild.AddDatapoints(upperPoints);
            }
            this.UpdateFromChildren();
        }
        public void ConsiderSplitting()
        {
            // make sure there is a splittable dimension where the spread is nonzero
            int dimension = -1;
            foreach (int coordinate in this.dimensionsToSplit)
            {
                if (this.observedBoundary.Coordinates[coordinate].IsSplittable)
                {
                    dimension = coordinate;
                }
            }
            if (dimension < 0)
                return;
            // now split

            // compute the coordinates of each child
#if true
            LinkedList<double> inputs = new LinkedList<double>();
            foreach (IDatapoint<ScoreType> datapoint in this.unpropogatedDatapoints)
            {
                inputs.AddLast(datapoint.InputCoordinates[dimension]);
            }
            double splitValue = MedianUtils.EstimateMedian(inputs);
            if (splitValue == this.currentBoundary.Coordinates[dimension].HighCoordinate || splitValue == this.currentBoundary.Coordinates[dimension].LowCoordinate)
                splitValue = (this.currentBoundary.Coordinates[dimension].LowCoordinate + this.currentBoundary.Coordinates[dimension].HighCoordinate) / 2;
#else
            //double splitValue = (this.currentBoundary.Coordinates[dimension].LowCoordinate + this.currentBoundary.Coordinates[dimension].HighCoordinate) / 2;
            double splitValue = (this.observedBoundary.Coordinates[dimension].LowCoordinate + this.observedBoundary.Coordinates[dimension].HighCoordinate) / 2;
#endif
            // TODO: consider splitting at the median, since that might run slightly faster
            // double splitValue = this.splitDimension_inputs.Mean;
            HyperBox<ScoreType> lowerBoundary = new HyperBox<ScoreType>(this.currentBoundary);
            lowerBoundary.Coordinates[dimension].HighCoordinate = splitValue;
            lowerBoundary.Coordinates[dimension].HighInclusive = true;
            HyperBox<ScoreType> upperBoundary = new HyperBox<ScoreType>(this.currentBoundary);
            upperBoundary.Coordinates[dimension].LowCoordinate = splitValue;
            upperBoundary.Coordinates[dimension].LowInclusive = false;
            // determine the split order for the children
            LinkedList<int> childSplitOrder = new LinkedList<int>(this.dimensionsToSplit);
            childSplitOrder.RemoveFirst();
            childSplitOrder.AddLast(dimension);

            // fill data into the children
            this.lowerChild = new SimpleInterpolationBox<ScoreType>(lowerBoundary, childSplitOrder, this.dimensionToSort, this.scoreHandler);
            this.upperChild = new SimpleInterpolationBox<ScoreType>(upperBoundary, childSplitOrder, this.dimensionToSort, this.scoreHandler);
            LinkedList<IDatapoint<ScoreType>> lowerPoints = new LinkedList<IDatapoint<ScoreType>>();
            LinkedList<IDatapoint<ScoreType>> upperPoints = new LinkedList<IDatapoint<ScoreType>>();
            // skip half of the datapoints because it saves a lot of time (the skipping compounds in grandchildren etc) and shouldn't make much difference in our decision of which dim to split
#if false
            int desiredNumPointsPerChild = this.unpropogatedDatapoints.Count();
#else
            int desiredNumPointsPerChild = this.unpropogatedDatapoints.Count() / 4;
#endif
            foreach (IDatapoint<ScoreType> newDatapoint in this.unpropogatedDatapoints)
            {
                if (newDatapoint.InputCoordinates[dimension] >= splitValue)
                {
                    if (upperPoints.Count < desiredNumPointsPerChild)
                        upperPoints.AddLast(newDatapoint);
                }
                else
                {
                    if (lowerPoints.Count < desiredNumPointsPerChild)
                        lowerPoints.AddLast(newDatapoint);
                }
            }
            this.unpropogatedDatapoints = new LinkedList<IDatapoint<ScoreType>>();
            this.lowerChild.AddDatapoints(lowerPoints);
            this.upperChild.AddDatapoints(upperPoints);
        }
        public int SplitDimension
        {
            get
            {
                return this.splitDimension;
            }
        }
        public double GetError()
        {
            double result = this.totalError / this.numDatapoints;
            return result;
        }
        public void UpdateFromChildren()
        {
            if (this.lowerChild == null || this.upperChild == null)
            {
                this.depth = 0;
                this.totalError = 0;
                return;
            }
            double lowerError = this.lowerChild.totalError;
            double upperError = this.upperChild.totalError;
            double difference;
            IDatapoint<ScoreType> nextPoint = this.upperChild.minPoint;
            IDatapoint<ScoreType> previousPoint = this.lowerChild.maxPoint;
            if (nextPoint != null && previousPoint != null)
            {
                double nextScore = this.scoreHandler.ConvertToDistribution(nextPoint.Item).Mean;
                double previousScore = this.scoreHandler.ConvertToDistribution(previousPoint.Item).Mean;
                difference = Math.Abs(nextScore - previousScore);
                difference *= difference;
            }
            else
            {
                difference = 0;
            }
            this.totalError = lowerError + difference + upperError;

            this.depth = Math.Max(this.lowerChild.depth, this.upperChild.depth) + 1;
        }
        public int NumDimensions
        {
            get
            {
                return this.currentBoundary.NumDimensions;
            }
        }
        public int Depth
        {
            get
            {
                return this.depth;
            }
        }
        #region Functions for ICombiner<IDatapoint>
        public IDatapoint<ScoreType> Combine(IDatapoint<ScoreType> a, IDatapoint<ScoreType> b)
        {
            return null;
        }
        public IDatapoint<ScoreType> Default()
        {
            return null;
        }
        #endregion

        //StatList<Datapoint, Datapoint> datapointsByInput;
        IEnumerable<IDatapoint<ScoreType>> unpropogatedDatapoints;
        int numDatapoints;
        IDatapoint<ScoreType> minPoint;
        IDatapoint<ScoreType> maxPoint;
        private LinkedList<int> dimensionsToSplit;
        private int splitDimension;
        private HyperBox<ScoreType> currentBoundary;
        private HyperBox<ScoreType> observedBoundary;
        private SimpleInterpolationBox<ScoreType> lowerChild;
        private SimpleInterpolationBox<ScoreType> upperChild;
        private double totalError;
        private int dimensionToSort;
        private int depth;
        private INumerifier<ScoreType> scoreHandler;
        //private Distribution splitDimension_inputs;
    }
}
