using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using mlr;

namespace cpmodel
{
    class Program
    {
        static void Main(string[] args)
        {
            string filename = args[0];

            var data = File.ReadAllLines(Path.GetFullPath(filename), Encoding.UTF8);

            XTransformer transformer = new();

            var pointData = data.Select(s =>
            {
                var split = s.Split(null);

                return new Point(double.Parse(split[0]), double.Parse(split[1]));
            }).ToList();


            ModelRunner runner = new();
            (double cp, RegressionOutputs regOutputs) output = runner.Run4P(pointData);

            Console.WriteLine(output.regOutputs.Coeffs[0]);
            Console.WriteLine(output.regOutputs.Coeffs[1]);
            Console.WriteLine(output.regOutputs.Coeffs[2]);
            Console.WriteLine(output.cp);
        }
    }

    public class XTransformer
    {
        public List<double> TransformedX4P(double cp, double x)
        {
            return new() { Math.Max(0, cp - x), Math.Max(0, x - cp) };
        }
    }


    public class ModelRunner
    {
        public RegressionOutputs Run4PInstance(double cp, List<Point> points)
        {
            XTransformer transformer = new();

            var transformed = points.Select(point => new Observation(transformer.TransformedX4P(cp, point.X), point.Y))
                .ToList();

            double[] ys = transformed.Select(observation => observation.Y).ToArray();
            double[,] transformedXs = transformed.Select(observation => observation.Xs).ToList().To2DArray();

            return Regression.MultipleLinearRegression(ys, transformedXs, false);
        }

        public (double cp, RegressionOutputs regressionOutputs) Run4P(List<Point> points)
        {
            double minX = points.Select(point => point.X).Min();
            double maxX = points.Select(point => point.X).Max();


            double minSearch = Math.Ceiling(minX);
            double maxSearch = Math.Floor(maxX);

            List<double> cps = new RangeBuilder().BuildList(minSearch, maxSearch, 0.125);
            List<(double, RegressionOutputs)> outputs = cps.Select(cp => (cp, Run4PInstance(cp, points))).ToList();
            List<(double, RegressionOutputs)> sorted = outputs.OrderBy(tuple => tuple.Item2.CV).ToList();
            return sorted[0];
        }
    }


    public class RangeBuilder
    {
        public List<double> BuildList(double min, double max, double step)
        {
            List<double> list = new();

            double val = min;

            while (val <= max)
            {
                list.Add(val);
                val += step;
            }

            return list;
        }
    }

    public static class Extensions
    {
        public static T[,] To2DArray<T>(this List<List<T>> inputList)
        {
            T[,] array = new T[inputList.Count, inputList[0].Count];

            int outerIndex = 0;
            foreach (List<T> innerList in inputList)
            {
                int innerIndex = 0;
                foreach (T item in innerList)
                {
                    array[outerIndex, innerIndex] = item;
                    innerIndex++;
                }

                outerIndex++;
            }

            return array;
        }
    }

    public class Point
    {
        public double X;
        public double Y;
        public Point(double x, double y)
        {
            X = x;
            Y = y;
        }
    }

    public class Observation
    {
        public List<double> Xs;
        public double Y;

        public Observation(List<double> xs, double y)
        {
            Xs = xs;
            Y = y;
        }
    }
    
}
