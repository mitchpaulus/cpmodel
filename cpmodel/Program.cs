using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using mlr;

namespace cpmodel
{
    class Program
    {
        static int Main(string[] args)
        {
            string filename = "";
            string type = "4";

            bool printModelCoords = false;
            bool printHelp = false;
            bool json = false;

            int skipRows = 0;
            int xColumn = 0;
            int yColumn = 1;

            string delimeter = null;

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
                else if (args[i] == "--skip")
                {
                    skipRows = int.Parse(args[i + 1]);
                    i++;
                }
                else if (args[i] == "--x-col")
                {
                    xColumn = int.Parse(args[i + 1]) - 1 ;
                    i++;
                }
                else if (args[i] == "--y-col")
                {
                    yColumn = int.Parse(args[i + 1]) - 1;
                    i++;
                }
                else if (args[i] == "-d" || args[i] == "--delimeter")
                {
                    delimeter = args[i + 1];
                    i++;
                }
                else if (args[i] == "--json")
                {
                    json = true;
                }
                else
                {
                    filename = args[i];
                }
            }

            if (printHelp)
            {
                WriteLine("cpmodel");
                WriteLine();
                WriteLine("USAGE:");
                WriteLine("cpmodel <options>... <file>");
                WriteLine();
                WriteLine("OPTIONS");
                WriteLine();
                WriteLine(" -c                       Print model coordinates, not coefficients");
                WriteLine(" -d, --delimeter <delim>  Delimiter to split lines by [whitespace]");
                WriteLine(" --json                   Print outputs in JSON format");
                WriteLine(" -p <model type>          Model type: 3h, 3c, 3h_new, 3c_new, 4, 5 [4]");
                WriteLine(" --skip n                 Skip n header records [0]");
                WriteLine(" --x-col <int col>        Set X column number, 1-based integer [1]");
                WriteLine(" --y-col <int col>        Set Y column number, 1-based integer [2]");
                WriteLine(" -h, --help               Show this help message and exit");
                WriteLine();
                WriteLine("It is assumed that the first column is X and the second column is Y.");
                WriteLine("The default delimiter is whitespace.");
                WriteLine("The file can be '-' to read from standard input.");
                return 0;
            }

            IEnumerable<string> data;
            if (filename == "-")
            {
                data = Console.In.ReadToEnd().SplitLines();
            }
            else
            {
                if (string.IsNullOrWhiteSpace(filename))
                {
                    Console.Error.WriteLine("No input file was specified. Use '-' for standard input.");
                    return 1;
                }

                try
                {
                    data = File.ReadAllLines(Path.GetFullPath(filename), Encoding.UTF8);
                }
                catch
                {
                    Console.Error.WriteLine("Could not read input file '{filename}'.");
                    return 1;
                }
            }

            bool success = true;
            List<string> errors = [];
            List<Point> pointData = data.Skip(skipRows).Select((s, idx) =>
            {
                string[] split = delimeter is null ? s.Split() : s.Split(delimeter);

                var maxSpecifiedColumn = Math.Max(xColumn, yColumn);
                if (split.Length < maxSpecifiedColumn)
                {
                    var error = $"There are fewer columns ({split.Length}) than the max specified column ({maxSpecifiedColumn + 1}).\n";
                    errors.Add(error);
                    success = false;
                    return new Point(0, 0);
                }

                string xValueString = split[xColumn];
                if (!double.TryParse(xValueString, out double x))
                {
                    errors.Add($"Could not parse x value '{xValueString}' on line {idx}.\n");
                    success = false;
                    return new Point(0, 0);
                }

                return new Point(x, double.Parse(split[yColumn]));
            }).ToList();

            if (!success)
            {
                // Print first 10 errors
                for (int i = 0; i < Math.Min(10, errors.Count); i++)
                {
                    Console.Error.Write(errors[i]);
                }
                if (errors.Count > 10)
                {
                    Console.Error.Write("...\n");
                }

                return 1;
            }

            // Fail if no data
            if (!pointData.Any())
            {
                Console.Error.WriteLine("No data found.");
                return 1;
            }

            ModelRunner runner = new();
            if (type == "3h")
            {
                (double cp, RegressionOutputs outputs) = runner.Run3PH(pointData);

                if (json)
                {
                    var dict = outputs.ToJsonDict();
                    (dict["Coeffs"] as List<double>)?.Add(cp);
                    dict["lambda"] = $"=LAMBDA(oat, {outputs.Coeffs[0]} + {outputs.Coeffs[1]} * MAX(0, {cp} - oat))";
                    string jsonStr = JsonSerializer.Serialize(dict);
                    Console.Write(jsonStr);
                }
                else if (printModelCoords)
                {
                    var minXValue = Math.Floor(pointData.Select(point => point.X).Min());
                    var maxXValue = Math.Ceiling(pointData.Select(point => point.X).Max());

                    WriteLine($"{minXValue}\t{outputs.Coeffs[0] + outputs.Coeffs[1] * Math.Max(0, cp - minXValue)}");
                    WriteLine($"{cp}\t{outputs.Coeffs[0]}");
                    WriteLine($"{maxXValue}\t{outputs.Coeffs[0]}");
                }
                else
                {
                    foreach (double coeff in outputs.Coeffs)
                    {
                        WriteLine(coeff);
                    }
                    WriteLine(cp);
                }

            }
            else if (type == "3c")
            {
                (double cp, RegressionOutputs regressionOutputs) = runner.Run3PC(pointData);

                if (json)
                {
                    var dict = regressionOutputs.ToJsonDict();
                    (dict["Coeffs"] as List<double>)?.Add(cp);
                    dict["lambda"] = $"=LAMBDA(oat, {regressionOutputs.Coeffs[0]} + {regressionOutputs.Coeffs[1]} * MAX(0, oat - {cp}))";
                    string jsonStr = JsonSerializer.Serialize(dict);
                    Console.Write(jsonStr);
                }
                else if (printModelCoords)
                {
                    var minXValue = Math.Floor(pointData.Select(point => point.X).Min());
                    var maxXValue = Math.Ceiling(pointData.Select(point => point.X).Max());

                    WriteLine($"{minXValue}\t{regressionOutputs.Coeffs[0]}");
                    WriteLine($"{cp}\t{regressionOutputs.Coeffs[0]}");
                    WriteLine($"{maxXValue}\t{regressionOutputs.Coeffs[0] + regressionOutputs.Coeffs[1] * (maxXValue - cp)}");
                }
                else
                {
                    foreach (double coeff in regressionOutputs.Coeffs)
                    {
                        WriteLine(coeff);
                    }
                    WriteLine(cp);
                }
            }
            else if (type == "3h_new")
            {
                RegressionOutputs outputs = runner.Run3PHNew(pointData);

                if (json)
                {
                    var dict = outputs.ToJsonDict();
                    dict["lambda"] = $"=LAMBDA(oat, {outputs.Coeffs[0]} + {outputs.Coeffs[1]} * MAX(0, {outputs.Coeffs[2]} - oat))";
                    string jsonStr = JsonSerializer.Serialize(dict);
                    Console.Write(jsonStr);
                }
                else if (printModelCoords)
                {
                    var minXValue = Math.Floor(pointData.Select(point => point.X).Min());
                    var maxXValue = Math.Ceiling(pointData.Select(point => point.X).Max());

                    WriteLine($"{minXValue}\t{outputs.Coeffs[0] + outputs.Coeffs[1] * Math.Max(0, outputs.Coeffs[2] - minXValue)}");
                    WriteLine($"{outputs.Coeffs[2]}\t{outputs.Coeffs[0]}");
                    WriteLine($"{maxXValue}\t{outputs.Coeffs[0]}");
                }
                else
                {
                    foreach (double outputsCoeff in outputs.Coeffs)
                    {
                        WriteLine(outputsCoeff);
                    }
                }
            }
            else if (type == "3c_new")
            {
                RegressionOutputs outputs = runner.Run3PCNew(pointData);

                if (json)
                {
                    var dict = outputs.ToJsonDict();
                    dict["lambda"] = $"=LAMBDA(oat, {outputs.Coeffs[0]} + {outputs.Coeffs[1]} * MAX(0, oat - {outputs.Coeffs[2]}))";
                    string jsonStr = JsonSerializer.Serialize(outputs);
                    Console.Write(jsonStr);
                }
                else if (printModelCoords)
                {
                    var minXValue = Math.Floor(pointData.Select(point => point.X).Min());
                    var maxXValue = Math.Ceiling(pointData.Select(point => point.X).Max());

                    WriteLine($"{minXValue}\t{outputs.Coeffs[0]}");
                    WriteLine($"{outputs.Coeffs[2]}\t{outputs.Coeffs[0]}");
                    WriteLine($"{maxXValue}\t{outputs.Coeffs[0] + outputs.Coeffs[1] * (maxXValue - outputs.Coeffs[2])}");
                }
                else
                {
                    foreach (double outputsCoeff in outputs.Coeffs)
                    {
                        WriteLine(outputsCoeff);
                    }
                }
            }
            else if (type == "4")
            {
                (double cp, RegressionOutputs regOutputs) = runner.Run4P(pointData);
                string lambda = $"=LAMBDA(oat, {regOutputs.Coeffs[0]} + {regOutputs.Coeffs[1]} * MAX(0, {cp} - oat) + {regOutputs.Coeffs[2]} * MAX(0, oat - {cp}))";

                if (json)
                {
                    var dict = regOutputs.ToJsonDict();
                    (dict["Coeffs"] as List<double>)?.Add(cp);
                    dict["lambda"] = lambda;
                    string jsonStr = JsonSerializer.Serialize(dict);
                    Console.Write(jsonStr);
                }
                else if (printModelCoords)
                {
                    var minXValue = Math.Floor(pointData.Select(point => point.X).Min());
                    var maxXValue = Math.Ceiling(pointData.Select(point => point.X).Max());

                    double yAtXmin = regOutputs.Coeffs[0] + (cp - minXValue) * regOutputs.Coeffs[1];
                    double yAtCp = regOutputs.Coeffs[0];
                    double yAtXmax = regOutputs.Coeffs[0] + (maxXValue - cp) * regOutputs.Coeffs[2];

                    WriteLine($"{minXValue}\t{yAtXmin}");
                    WriteLine($"{cp}\t{yAtCp}");
                    WriteLine($"{maxXValue}\t{yAtXmax}");
                }
                else
                {
                    WriteLine(regOutputs.Coeffs[0]);
                    WriteLine(regOutputs.Coeffs[1]);
                    WriteLine(regOutputs.Coeffs[2]);
                    WriteLine(cp);
                }

            }
            else if (type == "5")
            {
                (double lowCp, double highCp, RegressionOutputs regOutputs) = runner.Run5P(pointData);

                if (json)
                {
                    var dict = regOutputs.ToJsonDict();
                    (dict["Coeffs"] as List<double>)?.Add(lowCp);
                    (dict["Coeffs"] as List<double>)?.Add(highCp);
                    dict["lambda"] = $"=LAMBDA(oat, {regOutputs.Coeffs[0]} + {regOutputs.Coeffs[1]} * MAX(0, {lowCp} - oat) + {regOutputs.Coeffs[2]} * MAX(0, oat - {highCp}))";
                    string jsonStr = JsonSerializer.Serialize(dict);
                    Console.Write(jsonStr);
                }
                else if (printModelCoords)
                {
                    var minXValue = Math.Floor(pointData.Select(point => point.X).Min());
                    var maxXValue = Math.Ceiling(pointData.Select(point => point.X).Max());

                    double yAtXmin = regOutputs.Coeffs[0] + (lowCp - minXValue) * regOutputs.Coeffs[1];
                    double yAtLowCp = regOutputs.Coeffs[0];
                    double yAtXmax = regOutputs.Coeffs[0] + (maxXValue - highCp) * regOutputs.Coeffs[1];

                    WriteLine($"{minXValue}\t{yAtXmin}");
                    WriteLine($"{lowCp}\t{yAtLowCp}");
                    WriteLine($"{highCp}\t{yAtLowCp}");
                    WriteLine($"{maxXValue}\t{yAtXmax}");
                }
                else
                {
                    WriteLine(regOutputs.Coeffs[0]);
                    WriteLine(regOutputs.Coeffs[1]);
                    WriteLine(regOutputs.Coeffs[2]);
                    WriteLine(lowCp);
                    WriteLine(highCp);
                }
            }
            else
            {
                WriteLineError($"The model type '{type}' has not been implemented.");
                return 1;
            }

            return 0;
        }

        public static void WriteLine() => Console.Write('\n');

        public static void WriteLine(string text)
        {
            if (!text.Any() || text.Last() != '\n') Console.Write($"{text}\n");
            else Console.Write(text);
        }

        public static void WriteLineError(string text)
        {
            if (!text.Any() || text.Last() != '\n') Console.Error.Write($"{text}\n");
            else Console.Error.Write(text);
        }

        public static void WriteLine(double value) => WriteLine(value.ToString(CultureInfo.CurrentCulture));
    }

    public class XTransformer
    {
        public List<double> TransformedX4P(double cp, double x)
        {
            return [Math.Max(0, cp - x), Math.Max(0, x - cp)];
        }

        public List<double> TransformedX5P(double lowCp, double highCp, double x)
        {
            return [Math.Max(0, lowCp - x), Math.Max(0, x - highCp)];
        }

        public List<double> TransformedX3PH(double cp, double x) => [Math.Max(cp - x, 0)];
        public List<double> TransformedX3PC(double cp, double x) => [Math.Max(x - cp, 0)];
    }


    public class ModelRunner
    {
        public RegressionOutputs RunInstance(List<Observation> observations)
        {
            double[] ys = observations.Select(observation => observation.Y).ToArray();
            double[,] transformedXs = observations.Select(observation => observation.Xs).ToList().To2DArray();
            return Regression.MultipleLinearRegression(ys, transformedXs, false);
        }

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

        public RegressionOutputs Run3PCInstance(double cp, List<Point> points)
        {
            XTransformer transformer = new();

            List<Observation> transformed = points
                .Select(point => new Observation(transformer.TransformedX3PC(cp, point.X), point.Y))
                .ToList();

            double[] ys = transformed.Select(observation => observation.Y).ToArray();
            double[,] transformedXs = transformed.Select(observation => observation.Xs).ToList().To2DArray();

            return Regression.MultipleLinearRegression(ys, transformedXs, false);
        }

        public (double cp, RegressionOutputs regressionOutputs) Run3PC(List<Point> points)
        {
            var sortedXs = points.Select(point => point.X).OrderBy(d => d).ToList();

            double minSearch = Math.Ceiling(sortedXs.Skip(1).First());
            double maxSearch = Math.Floor(sortedXs.SkipLast(2).Last());

            List<double> cps = new RangeBuilder().BuildList(minSearch, maxSearch, 0.125);

            List<(double, RegressionOutputs)> outputs = cps.Select(cp => (cp, Run3PCInstance(cp, points))).ToList();
            List<(double, RegressionOutputs)> sorted = outputs.OrderBy(tuple => tuple.Item2.CV).ToList();
            return sorted[0];
        }



        public (double cp, RegressionOutputs regressionOutputs) Run4P(List<Point> points)
        {
            double step = 0.125;
            double minX = points.Select(point => point.X).Min();
            double maxX = points.Select(point => point.X).Max();

            double minSearch = Math.Ceiling(minX);
            if (Math.Abs(minX - minSearch) < 0.0000000001)
            {
                minSearch += step;
            }

            double maxSearch = Math.Floor(maxX);
            if (Math.Abs(maxX - maxSearch) < 0.0000000001)
            {
                maxSearch -= step;
            }

            List<double> cps = new RangeBuilder().BuildList(minSearch, maxSearch, step);
            List<(double, RegressionOutputs)> outputs = cps.Select(cp => (cp, Run4PInstance(cp, points))).ToList();

            List<(double, RegressionOutputs)> sorted = outputs.OrderBy(tuple => tuple.Item2.CV).ToList();
            return sorted[0];
        }

        public (double lowCp, double highCp, RegressionOutputs regressionOutputs) Run5P(List<Point> points)
        {
            double minX = points.Select(point => point.X).Min();
            double maxX = points.Select(point => point.X).Max();

            double minSearch = Math.Ceiling(minX + 0.0001);
            double maxSearch = Math.Floor(maxX - 0.0001);

            double ChangePointStepSize = 0.125;

            var outputs = new List<(double lowCp, double highCp, RegressionOutputs regressionOutput)>();
            for (double lowChangepoint = minSearch; lowChangepoint < maxSearch - ChangePointStepSize; lowChangepoint += ChangePointStepSize)
            {
                for (double highChangepoint = lowChangepoint + ChangePointStepSize; highChangepoint < maxSearch; highChangepoint += ChangePointStepSize)
                {
                    var observations = points.Select(point => new Observation([Math.Max(lowChangepoint - point.X, 0), Math.Max(point.X - highChangepoint, 0)], point.Y)).ToList();
                    var output = RunInstance(observations);
                    outputs.Add((lowChangepoint, highChangepoint, output));
                }
            }

            /*
            List<double> cps = new RangeBuilder().BuildList(minSearch, maxSearch, 0.125);
            List<(double, RegressionOutputs)> outputs = cps.Select(cp => (cp, Run4PInstance(cp, points))).ToList();
            */
            List<(double, double, RegressionOutputs)> sorted = outputs.OrderBy(tuple => tuple.regressionOutput.CV).ToList();
            return sorted[0];
        }


        public RegressionOutputs Run3PHNew(List<Point> points)
        {
            var sortedPoints = points.OrderBy(point => point.X).ToList();

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

                List<double> residuals = [];
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
                Coeffs = [bestb0, bestb1, bestb2]
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

            for (int m = 1; m < n - 2; m++)
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

                List<double> residuals = [];
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
                Coeffs = [bestb0, bestb1, bestb2]
            };
            return outputs;
        }

    }

    public class RangeBuilder
    {
        public List<double> BuildList(double min, double max, double step)
        {
            List<double> list = [];

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
            List<string> output = [];
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
