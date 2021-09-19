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
            string filename = "";
            string type = "4";
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-p")
                {
                    type = args[i + 1];
                }
                else
                {
                    filename = args[i];
                }
            }

            string[] data = File.ReadAllLines(Path.GetFullPath(filename), Encoding.UTF8);

            XTransformer transformer = new();

            var pointData = data.Select(s =>
            {
                var split = s.Split(null);

                return new Point(double.Parse(split[0]), double.Parse(split[1]));
            }).ToList();

            ModelRunner runner = new();
            if (type == "3h")
            {
                (double cp, RegressionOutputs regressionOutputs) = runner.Run3PH(pointData);
                foreach (double coeff in regressionOutputs.Coeffs)
                {
                    Console.WriteLine(coeff);
                }
                Console.WriteLine(cp);
            }
            else if (type == "3h_new")
            {
                RegressionOutputs outputs = runner.Run3PHNew(pointData);
                foreach (double outputsCoeff in outputs.Coeffs)
                {
                    Console.WriteLine(outputsCoeff);
                }
            }
            else
            {
                (double cp, RegressionOutputs regOutputs) output = runner.Run4P(pointData);

                Console.WriteLine(output.regOutputs.Coeffs[0]);
                Console.WriteLine(output.regOutputs.Coeffs[1]);
                Console.WriteLine(output.regOutputs.Coeffs[2]);
                Console.WriteLine(output.cp);
            }
        }
    }

    public class XTransformer
    {
        public List<double> TransformedX4P(double cp, double x)
        {
            return new() { Math.Max(0, cp - x), Math.Max(0, x - cp) };
        }

        public List<double> TransformedX3PH(double cp, double x) => new(){ Math.Max(cp - x, 0) };
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

        public RegressionOutputs Run3PHInstance(double cp, List<Point> points)
        {
            XTransformer transformer = new();

            List<Observation> transformed = points
                .Select(point => new Observation(transformer.TransformedX3PH(cp, point.X), point.Y))
                .ToList();

            double[] ys = transformed.Select(observation => observation.Y).ToArray();
            double[,] transformedXs = transformed.Select(observation => observation.Xs).ToList().To2DArray();

            return Regression.MultipleLinearRegression(ys, transformedXs, false);
        }

        public (double cp, RegressionOutputs regressionOutputs) Run3PH(List<Point> points)
        {
            var sortedXs = points.Select(point => point.X).OrderBy(d => d);

            double minSearch = Math.Ceiling(sortedXs.Skip(1).First());
            double maxSearch = Math.Floor(points.Select(point => point.X).Max());

            List<double> cps = new RangeBuilder().BuildList(minSearch, maxSearch, 0.125);

            List<(double, RegressionOutputs)> outputs = cps.Select(cp => (cp, Run3PHInstance(cp, points))).ToList();
            List<(double, RegressionOutputs)> sorted = outputs.OrderBy(tuple => tuple.Item2.CV).ToList();
            return sorted[0];
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

        public RegressionOutputs Run3PHNew(List<Point> points)
        {
            var sortedPoints = points.OrderBy(point => point.X);

            var ys = sortedPoints.Select(point => point.Y).ToList();
            var xs = sortedPoints.Select(point => point.X).ToList();

            var n = points.Count;
            var sumY = ys.Sum();

            double sse = double.MaxValue;

            double bestb0 = 0;
            double bestb1 = 0;
            double bestb2 = 0;

            for (var m = 2; m < n; m++)
            {
                var b0 = ys.Skip(m).Average();

                List<double> lowXs = xs.Take(m).ToList();
                List<double> lowYs = ys.Take(m).ToList();
                var sumXLow = lowXs.Sum();
                var sumYLow = lowYs.Sum();
                var sumXYLow = lowXs.Zip(lowYs).Select((pair) => pair.First * pair.Second).Sum();
                var sumX2Low = lowXs.Select(d => d * d).Sum();
                var nl = m;

                var b1 = (sumXLow * sumYLow - nl * sumXYLow) / (nl * sumX2Low - sumXLow * sumXLow);

                var numerator = n * sumXLow * sumXYLow - nl * sumXLow * sumXYLow + nl * sumX2Low * sumY -
                    sumY * sumXLow * sumXLow - n * sumX2Low * sumYLow + sumYLow * sumXLow * sumXLow;

                var denominator = (n - nl) * (nl * sumXYLow - sumXLow * sumYLow);

                var b2 = numerator / denominator;

                var tempPortion = xs.Select(x => Math.Max(b2 - x, 0));
                var predictions = tempPortion.Select(d => b0 + b1 * d).ToList();

                List<double> residuals = new();
                for (int i = 0; i < n; i++) residuals.Add(ys[i] - predictions[i]);

                var newSse = residuals.Select(r => r * r).Sum();

                if (newSse < sse)
                {
                    sse = newSse;
                    bestb0 = b0;
                    bestb1 = b1;
                    bestb2 = b2;
                }
            }

            RegressionOutputs outputs = new()
            {
                Coeffs = new[] { bestb0, bestb1, bestb2 }
            };
            return outputs;
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
