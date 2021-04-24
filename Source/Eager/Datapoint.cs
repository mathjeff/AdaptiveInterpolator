using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AdaptiveInterpolation
{
    // The IDatapoint interface requires that the object have coordinates
    public interface IDatapoint<ItemType>
    {
        double[] InputCoordinates
        {
            get;
        }
        int NumInputDimensions
        {
            get;
        }
        ItemType Item
        {
            get;
        }
        double[] OutputCoordinates
        {
            get;
        }
    }
    public class Datapoint<ItemType> : IDatapoint<ItemType>
    {
        static int nextID = 0;
        public Datapoint(double[] inputs, ItemType startingItem)
        {
            this.inputCoordinates = inputs;
            this.item = startingItem;
            this.Initialize();
        }
        public Datapoint(double input1, ItemType startingItem)
        {
            this.inputCoordinates = new double[1];
            this.inputCoordinates[0] = input1;
            this.item = startingItem;
            this.Initialize();
        }
        public Datapoint(double input1, double input2, ItemType startingItem)
        {
            this.inputCoordinates = new double[2];
            this.inputCoordinates[0] = input1;
            this.inputCoordinates[1] = input2;
            this.item = startingItem;
            this.Initialize();
        }
        public Datapoint(double input1, double input2, double input3, ItemType startingItem)
        {
            this.inputCoordinates = new double[3];
            this.inputCoordinates[0] = input1;
            this.inputCoordinates[1] = input2;
            this.inputCoordinates[2] = input3;
            this.item = startingItem;
            this.Initialize();
        }
        public Datapoint(double[] inputs, double[] outputs, ItemType startingItem)
        {
            this.inputCoordinates = inputs;
            this.outputCoordinates = outputs;
            this.item = startingItem;
            this.Initialize();
        }
        private void Initialize()
        {
            this.id = nextID;
            nextID++;
        }
        public double[] InputCoordinates
        {
            get
            {
                return this.inputCoordinates;
            }
        }
        public int NumInputDimensions
        {
            get
            {
                return this.inputCoordinates.Length;
            }
        }
        public double[] OutputCoordinates
        {
            get
            {
                return this.outputCoordinates;
            }
        }
        public bool InputEquals(Datapoint<ItemType> other)
        {
            int i;
            for (i = 0; i < this.inputCoordinates.Length; i++)
            {
                if (this.inputCoordinates[i] != other.InputCoordinates[i])
                    return false;
            }
            return true;
        }
        public override string ToString()
        {
            string result = "coordinates:(";
            int i;
            for (i = 0; i < this.inputCoordinates.Length; i++)
            {
                result += this.inputCoordinates[i].ToString() + ",";
            }
            result += ") score=" + this.Item.ToString();
            return result;
        }
        public ItemType Item
        {
            get
            {
                return this.item;
            }
        }
        public int ID
        {
            get
            {
                return this.id;
            }
        }
        private double[] inputCoordinates;
        private double[] outputCoordinates;
        private ItemType item;
        private int id;
    }
}
