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

            bool printModelCoords = false;
            bool printHelp = false;

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-p")
                {
                    type = args[i + 1];
                    i++;
                }
                else if (args[i] == "-c")
                {
                    printModelCoords = true;
                }
                else if (args[i] == "-h" || args[i] == "--help")
                {
                    printHelp = true;
                }
                else
                {
                    filename = args[i];
                }
            }

            if (printHelp)
            {
                Console.Write("cpmodel\n");
                Console.Write("\n");
                Console.Write("USAGE:\n");
                Console.Write("cpmodel <options>... <file> \n");
                Console.Write("\n");
                Console.Write("OPTIONS\n");
                Console.Write("\n");
                Console.Write(" -p <parameter type>, Model type:  \n");
                Console.Write(" -c                 , Print model coordinates, not coeffs\n");
                return;
            }

            IEnumerable<string> data;
            if (filename == "-")
            {
                data = Console.In.ReadToEnd().SplitLines();
            }
            else
            {
                data = File.ReadAllLines(Path.GetFullPath(filename), Encoding.UTF8);
            }


            XTransformer transformer = new();

            var pointData = data.Select((s, idx) =>
            {
                var split = s.Split(null);

                if (!double.TryParse(split[0], out double x))
                {
                    Console.Error.WriteLine($"Could not parse x value '{split[0]}' on line {idx}.");
                    throw new InvalidDataException("");
                }

                return new Point(x, double.Parse(split[1]));
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
            else if (type == "3c_new")
            {
                RegressionOutputs outputs = runner.Run3PCNew(pointData);

                if (printModelCoords)
                {
                    var minXValue = Math.Floor(pointData.Select(point => point.X).Min());
                    var maxXValue = Math.Ceiling(pointData.Select(point => point.X).Max());

                    Console.WriteLine($"{minXValue}\t{outputs.Coeffs[0]}");
                    Console.WriteLine($"{outputs.Coeffs[2]}\t{outputs.Coeffs[0]}");
                    Console.WriteLine($"{maxXValue}\t{outputs.Coeffs[0] + outputs.Coeffs[1] * (maxXValue - outputs.Coeffs[2])}");
                }
                else
                {
                    foreach (double outputsCoeff in outputs.Coeffs)
                    {
                        Console.WriteLine(outputsCoeff);
                    }
                }

            }
            else if (type == "4")
            {
                (double cp, RegressionOutputs regOutputs) output = runner.Run4P(pointData);

                Console.WriteLine(output.regOutputs.Coeffs[0]);
                Console.WriteLine(output.regOutputs.Coeffs[1]);
                Console.WriteLine(output.regOutputs.Coeffs[2]);
                Console.WriteLine(output.cp);
            }
            else
            {
                Console.WriteLine($"The model type '{type}' has not been implemented.");
                return;
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

        public RegressionOutputs Run3PCNew(List<Point> points)
        {
            IOrderedEnumerable<Point> sortedPoints = points.OrderBy(point => point.X);

            List<double> ys = sortedPoints.Select(point => point.Y).ToList();
            List<double> xs = sortedPoints.Select(point => point.X).ToList();

            int n = points.Count;
            double sumY = ys.Sum();

            double sse = double.MaxValue;

            double bestb0 = 0;
            double bestb1 = 0;
            double bestb2 = 0;

            for (int m = 1; m < n - 1; m++)
            {
                double b0 = ys.Take(m).Average();

                List<double> highXs = xs.Skip(m).ToList();
                List<double> highYs = ys.Skip(m).ToList();

                double sumXHigh = highXs.Sum();
                double sumYHigh = highYs.Sum();
                double sumXYHigh = highXs.Zip(highYs).Select((pair) => pair.First * pair.Second).Sum();
                double sumX2High = highXs.Select(d => d * d).Sum();
                int ng = n - m;

                // Equation 18
                double b1 = (ng * sumXYHigh -  sumXHigh * sumYHigh) / (ng * sumX2High - sumXHigh * sumXHigh);

                // Equation 20
                double numerator = (n - ng) * sumXYHigh * sumXHigh + sumX2High * (ng * sumY - n * sumYHigh) +
                                   (sumXHigh * sumXHigh) * (sumYHigh - sumY);

                // Equation 21
                double denominator = (n - ng) * (ng * sumXYHigh - sumXHigh * sumYHigh);

                double b2 = numerator / denominator;

                IEnumerable<double> tempPortion = xs.Select(x => Math.Max(x - b2, 0));
                List<double> predictions = tempPortion.Select(d => b0 + b1 * d).ToList();

                List<double> residuals = new();
                for (int i = 0; i < n; i++) residuals.Add(ys[i] - predictions[i]);

                double newSse = residuals.Select(r => r * r).Sum();

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
        public static List<string> SplitLines(this string input)
        {
            List<string> output = new List<string>();
            using (StringReader sr = new StringReader(input)) {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    output.Add(line);
                }
            }

            return output;
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
