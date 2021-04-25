using System;
using System.Collections.Generic;
using System.Text;

namespace AdaptiveInterpolation
{
    public interface LazyInputs
    {
        int GetNumCoordinates();
        double GetInput(int coordinate);
        string GetDescription(int coordinate);
    }

    public interface LazyCoordinate
    {
        double GetCoordinate();
        string GetDescription();
    }

    public class EagerInput : LazyCoordinate
    {
        public EagerInput(double value, string description)
        {
            this.value = value;
            this.description = description;
        }
        public double GetCoordinate()
        {
            return this.value;
        }
        public string GetDescription()
        {
            return this.description;
        }
        private double value;
        private string description;
    }

    public class LazyInputList : LazyInputs
    {
        public LazyInputList(List<LazyCoordinate> coordinates)
        {
            this.coordinates = coordinates;
        }
        public int GetNumCoordinates()
        {
            return this.coordinates.Count;
        }
        public double GetInput(int index)
        {
            return this.coordinates[index].GetCoordinate();
        }
        public string GetDescription(int index)
        {
            return this.coordinates[index].GetDescription();
        }
        List<LazyCoordinate> coordinates;
    }

    public class ConcatInputs : LazyInputs
    {
        public ConcatInputs(List<LazyInputs> children)
        {
            this.children = children;
        }
        public ConcatInputs(LazyInputs a, LazyInputs b) : this(new List<LazyInputs>() { a, b })
        {
        }
        public int GetNumCoordinates()
        {
            int count = 0;
            foreach (LazyInputs child in this.children)
            {
                count += child.GetNumCoordinates();
            }
            return count;
        }
        public double GetInput(int index)
        {
            int shiftedIndex = index;
            for (int i = 0; i < this.children.Count; i++)
            {
                LazyInputs child = this.children[i];
                int childNumCoordinates = child.GetNumCoordinates();
                if (shiftedIndex < childNumCoordinates)
                {
                    return child.GetInput(shiftedIndex);
                }
                shiftedIndex -= childNumCoordinates;
            }
            throw new ArgumentException("Index too large: " + index);
        }

        public string GetDescription(int index)
        {
            int shiftedIndex = index;
            for (int i = 0; i < this.children.Count; i++)
            {
                LazyInputs child = this.children[i];
                int childNumCoordinates = child.GetNumCoordinates();
                if (shiftedIndex < childNumCoordinates)
                {
                    return child.GetDescription(shiftedIndex);
                }
                shiftedIndex -= childNumCoordinates;
            }
            throw new ArgumentException("Index too large: " + index);
        }

        List<LazyInputs> children;
    }

}
