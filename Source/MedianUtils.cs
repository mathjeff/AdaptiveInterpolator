using StatLists;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdaptiveInterpolation
{
    class MedianUtils
    {
        public static double EstimateMedian(IEnumerable<double> items)
        {
            bool debug = false;
            if (items.Count() <= 1)
            {
                if (items.Count() == 1)
                {
                    return items.First();
                }
                throw new ArgumentException("Cannot find the median of an empty list");
            }
            SortNodePair<double> relevantItems = EstimateMedian<double>(items, new DoubleComparer());
            double estimatedMedian;
            if (relevantItems.LowerItem.NumEqualItems > items.Count() / 2)
            {
                estimatedMedian = relevantItems.LowerItem.Item;
            }
            else
            {
                if (relevantItems.UpperItem.NumEqualItems > items.Count() / 2)
                {
                    estimatedMedian = relevantItems.UpperItem.Item;
                }
                else
                {
                    // the common case
                    estimatedMedian = (relevantItems.LowerItem.Item + relevantItems.UpperItem.Item) / 2;
                }
            }
            if (debug)
            {
                int numLowerItems = 0;
                int numHigherItems = 0;
                foreach (double value in items)
                {
                    if (value < estimatedMedian)
                    {
                        numLowerItems++;
                    }
                    else
                    {
                        if (value > estimatedMedian)
                            numHigherItems++;
                    }
                }
                if (Math.Abs(numHigherItems - numLowerItems) > items.Count() * 3 / 4 + 1)
                {
                    System.Diagnostics.Debug.WriteLine("Estimated median " + estimatedMedian + " had " + numLowerItems + " lower items and " + numHigherItems + " higher items");
                    System.Diagnostics.Debug.WriteLine("Estimated median was incorrect by too large of a margin");
                    EstimateMedian(items);
                }
            }
            return estimatedMedian;
        }

        public static SortNodePair<T> EstimateMedian<T>(IEnumerable<T> items, IComparer<T> comparer)
        {
            // split into sets of two points
            IEnumerable<SortNodePair<T>> currentPairs = PairOff(items, comparer);
            List<SortNodePair<T>> nextPairs = new List<SortNodePair<T>>(currentPairs.Count() / 2);
            // find some pretty good bounds on the median
            while (currentPairs.Count() > 1)
            {
                SortNodePair<T> previousPair = null;
                foreach (SortNodePair<T> currentPair in currentPairs)
                {
                    if (previousPair == null)
                    {
                        previousPair = currentPair;
                    }
                    else
                    {
                        // combine currentPair with previousPair, make a new pair, and add it to nextPairs
                        SortNode<T> lowestItem, middleItem1, middleItem2, highestItem;
                        // find the highest and lowest nodes
                        if (comparer.Compare(previousPair.LowerItem.Item, currentPair.LowerItem.Item) < 0)
                        {
                            lowestItem = previousPair.LowerItem;
                            middleItem1 = currentPair.LowerItem;
                        }
                        else
                        {
                            lowestItem = currentPair.LowerItem;
                            middleItem1 = previousPair.LowerItem;
                        }
                        if (comparer.Compare(previousPair.UpperItem.Item, currentPair.UpperItem.Item) < 0)
                        {
                            middleItem2 = previousPair.UpperItem;
                            highestItem = currentPair.UpperItem;
                        }
                        else
                        {
                            middleItem2 = currentPair.UpperItem;
                            highestItem = previousPair.UpperItem;
                        }
                        // make sure middleItem1 < middleItem2
                        if (comparer.Compare(middleItem1.Item, middleItem2.Item) > 0)
                        {
                            SortNode<T> temp = middleItem2;
                            middleItem2 = middleItem1;
                            middleItem1 = temp;
                        }
                        // make an updated low-middle node
                        SortNode<T> new_lowMiddle = new SortNode<T>(middleItem1.Item);
                        new_lowMiddle.NumEqualItems = middleItem1.NumEqualItems;
                        new_lowMiddle.NumLowerItems = lowestItem.NumLowerItems + middleItem1.NumLowerItems;
                        if (comparer.Compare(lowestItem.Item, new_lowMiddle.Item) < 0)
                            new_lowMiddle.NumLowerItems += lowestItem.NumEqualItems;
                        else
                            new_lowMiddle.NumEqualItems += lowestItem.NumEqualItems;
                        new_lowMiddle.NumHigherItems = middleItem1.NumHigherItems; // don't need to mark the higher items that new_highMiddle knows about

                        // make an updated high-middle node
                        SortNode<T> new_highMiddle = new SortNode<T>(middleItem2.Item);
                        new_highMiddle.NumEqualItems = middleItem2.NumEqualItems;
                        new_highMiddle.NumLowerItems = middleItem2.NumLowerItems; // don't need to mark the lower items that new_lowerMiddle knows about
                        new_highMiddle.NumHigherItems = middleItem2.NumHigherItems + highestItem.NumHigherItems;
                        if (comparer.Compare(new_highMiddle.Item, highestItem.Item) < 0)
                            new_highMiddle.NumHigherItems += highestItem.NumEqualItems;
                        else
                            new_highMiddle.NumEqualItems += highestItem.NumEqualItems;
                        

                        nextPairs.Add(new SortNodePair<T>(new_lowMiddle, new_highMiddle));

                        previousPair = null;
                    }
                }
                // If a pair remains, we ignore it
                //if (previousPair != null)
                //    nextPairs.AddFirst(previousPair);

                currentPairs = nextPairs;
                nextPairs = new List<SortNodePair<T>>(currentPairs.Count() / 2);
            }
            return currentPairs.First();
        }
        public static IEnumerable<SortNodePair<T>> PairOff<T>(IEnumerable<T> items, IComparer<T> comparer)
        {
            List<SortNodePair<T>> pairs = new List<SortNodePair<T>>(items.Count() / 2);
            SortNode<T> previousNode = null;
            foreach (T currentItem in items)
            {
                if (previousNode == null)
                {
                    previousNode = new SortNode<T>(currentItem);
                }
                else
                {
                    SortNode<T> currentNode = new SortNode<T>(currentItem);
                    int comparison = comparer.Compare(previousNode.Item, currentItem);
                    if (comparison >= 0)
                    {
                        //previousNode.NumLowerItems = currentNode.NumHigherItems = 1;
                        pairs.Add(new SortNodePair<T>(currentNode, previousNode));
                    }
                    else
                    {
                        //previousNode.NumHigherItems = currentNode.NumLowerItems = 1;
                        pairs.Add(new SortNodePair<T>(previousNode, currentNode));
                    }
                    previousNode = null;
                }
                // Note that if there is an odd number of items then we don't use the last one
            }
            return pairs;
        }
    }
    class SortNode<T>
    {
        public SortNode(T item)
        {
            this.Item = item;
            this.NumEqualItems = 1;
        }
        public int NumLowerItems;
        public int NumEqualItems;
        public int NumHigherItems;
        public T Item;
    }
    class SortNodePair<T>
    {
        public SortNodePair(SortNode<T> lowerItem, SortNode<T> upperItem)
        {
            this.LowerItem  = lowerItem;
            this.UpperItem = upperItem;
        }
        public SortNode<T> LowerItem;
        public SortNode<T> UpperItem;
    }
}
