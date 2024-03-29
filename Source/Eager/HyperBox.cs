﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AdaptiveInterpolation
{
    // represents an n-dimensional rectangle with a reference to an object of type SummaryType
    public class HyperBox<SummaryType>
    {
        public HyperBox(FloatRange[] coordinates)
        {
            this.Coordinates = new FloatRange[coordinates.Length];
            int i;
            for (i = 0; i < coordinates.Length; i++)
            {
                if (coordinates[i] != null)
                    this.Coordinates[i] = new FloatRange(coordinates[i]);
            }
        }
        public HyperBox(HyperBox<SummaryType> source)
        {
            this.Coordinates = new FloatRange[source.NumDimensions];
            int i;
            for (i = 0; i < source.NumDimensions; i++)
            {
                this.Coordinates[i] = new FloatRange(source.Coordinates[i]);
            }
        }
        public HyperBox(IDatapoint<SummaryType> onlyPoint)
        {
            this.Coordinates = new FloatRange[onlyPoint.NumInputDimensions];
            int i;
            for (i = 0; i < onlyPoint.NumInputDimensions; i++)
            {
                this.Coordinates[i] = new FloatRange(onlyPoint.InputCoordinates[i], true, onlyPoint.InputCoordinates[i], true);
            }
        }
        public bool Contains(HyperBox<SummaryType> other)
        {
            int i;
            for (i = 0; i < this.NumDimensions; i++)
            {
                if (!this.Coordinates[i].Contains(other.Coordinates[i]))
                    return false;
            }
            return true;
        }
        public bool Contains(IDatapoint<SummaryType> datapoint)
        {
            return this.Contains(datapoint.InputCoordinates);
        }
        public bool Contains(double[] coordinates)
        {
            int i;
            for (i = 0; i < this.NumDimensions; i++)
            {
                if (!this.Coordinates[i].Contains(coordinates[i]))
                    return false;
            }
            return true;
        }
        public bool Intersects(HyperBox<SummaryType> other)
        {
            int i;
            for (i = 0; i < this.NumDimensions; i++)
            {
                if (!this.Coordinates[i].Intersects(other.Coordinates[i]))
                    return false;
            }
            return true;
        }
        public void ExpandToInclude(IDatapoint<SummaryType> newPoint)
        {
            int i;
            for (i = 0; i < this.NumDimensions; i++)
            {
                if (this.Coordinates[i] == null)
                    this.Coordinates[i] = new FloatRange(newPoint.InputCoordinates[i]);
                else
                    this.Coordinates[i].ExpandToInclude(newPoint.InputCoordinates[i]);
            }
        }
        public double Area
        {
            get
            {
                double area = 1;
                foreach (FloatRange range in this.Coordinates)
                {
                    area *= range.Width;
                }
                return area;
            }
        }
        public FloatRange[] Coordinates { get; set; }
        public int NumDimensions
        {
            get
            {
                return this.Coordinates.Length;
            }
        }
        public double[] Middle
        {
            get
            {
                double[] result = new double[this.Coordinates.Length];
                for (int i = 0; i < this.Coordinates.Length; i++)
                {
                    result[i] = this.Coordinates[i].Middle;
                }
                return result;
            }
        }
    }
}
