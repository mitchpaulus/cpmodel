using System;
using System.Collections.Generic;
using System.Linq;

namespace mlr
{
    public static class Regression
    {
        /// <summary>
        /// SimpleLinearRegression returns the regression outputs for a simple y=mx+b model.
        /// </summary>
        /// <param name="y">List of double dependent variables</param>
        /// <param name="x">List of double independent variables</param>
        /// <returns>Regression outputs</returns>
        public static RegressionOutputs SimpleLinearRegression(List<double> y, List<double> x)
        {
            RegressionOutputs result = new RegressionOutputs();
            //Make the sure counts for Y and X are the same
            if (y.Count != x.Count)
            {
                throw new Exception("Matrices must be the same size for regression");
            }

            double yMean = y.Average();
            double xMean = x.Average();

            double[] xSquared = new double[y.Count];
            double[] xy = new double[y.Count];
            double[] ySquared = new double[y.Count];

            for (int i = 0; i < y.Count; i++)
            {
                xSquared[i] = x[i] * x[i];
                ySquared[i] = y[i] * y[i];
                xy[i] = x[i] * y[i];
            }

            double ssXY = xy.Sum() - x.Count * xMean * yMean;
            double ssXX = xSquared.Sum() - x.Count * xMean * xMean;
            double ssYY = ySquared.Sum() - y.Count * yMean * yMean;

            double slope = ssXY / ssXX;
            double constant = yMean - slope * xMean;

            double sse = ssYY - slope * ssXY;

            result.Coeffs = new double[2];
            result.Coeffs[0] = constant;
            result.Coeffs[1] = slope;

            result.StandardError = Math.Sqrt(sse / (y.Count - 2));
            result.Rsquared = 1 - (sse / ssYY);


            return result;
        }

        /// <summary>
        /// MultipleLinearRegression runs a MLR on y and x data.  A column of 1's is added for a constant value.  
        /// </summary>
        /// <param name="y">1-D array of double dependent variable values</param>
        /// <param name="x">n x p array of independent variable observations.  Do not add a column of 1's</param>
        /// <param name="advancedStats">true will return: 
        ///                                 predictions
        ///                                 residuals
        ///                                 standardized residuals
        ///                                 Cook's distance
        ///                                 standard error for coefficients
        ///                                 t-stats
        ///                                 F-statistic
        ///                                 adjusted R^2</param>
        /// <returns>RegressionOutputs class</returns>
        public static RegressionOutputs MultipleLinearRegression(double[] y, double[,] x, bool advancedStats)
        {

            RegressionOutputs toReturn = new RegressionOutputs();


            //Make sure that the length for y and x are the same
            if (y.Count() != x.GetLength(0))
            {
                throw new Exception("Matrices must be the same size for regression");
            }


            //Add a column of 1's to the X matrix to calculate a constant in the regression
            double[,] xFull = new double[x.GetLength(0), x.GetLength(1) + 1];
            for (int i = 0; i < x.GetLength(0); i++)
            {
                xFull[i, 0] = 1;
                for (int j = 0; j < x.GetLength(1); j++)
                {
                    xFull[i, j + 1] = x[i, j];
                }
            }
            //Have to make y a two dimensional array to pass to my matrix methods.
            double[,] yAdjusted = new double[y.Length, 1];
            for (int i = 0; i < y.Length; i++)
            {
                yAdjusted[i, 0] = y[i];
            }

            double yAve = y.Average();
            int n = y.Count();
            int p = xFull.GetLength(1);



            //Calculate X'X
            var xTrans = MatrixMethods.MatTranspose(xFull);
            var xTransX = MatrixMethods.MatMultiply(xTrans, xFull);
            //Calculate X'Y

            var xTransY = MatrixMethods.MatMultiply(xTrans, yAdjusted);

            //Calculate [X'X]^-1
            var choleskyDecomp = MatrixMethods.CholeskyDecomp(xTransX);
            double[,] c = MatrixMethods.MatrixInv(xTransX, choleskyDecomp);

            for (int i = 0; i < c.GetLength(0); i++)
            {
                for (int j = 0; j < c.GetLength(1); j++)
                {
                    if (double.IsNaN(c[i, j]))
                    {
                        throw new InvalidOperationException("Matrix inverse not defined. Check for constant input array.");
                    }
                }
            }

            double[,] coeffs = MatrixMethods.MatMultiply(c, xTransY);

            // Calculate Y'Y
            double[,] yTransY = MatrixMethods.MatMultiply(MatrixMethods.MatTranspose(yAdjusted), yAdjusted);
            // Calculate B'X'Y
            double[,] betaTransXtransY = MatrixMethods.MatMultiply(MatrixMethods.MatTranspose(coeffs), xTransY);





            double sse = yTransY[0, 0] - betaTransXtransY[0, 0];    //Sum of squared errors (sum of squared residuals)
            //Check if sse is less than zero.  Ran into round off problem when passed constant data.
            if (sse < 0)
            {
                sse = 0;
            }


            double[] ssmArray = new double[n];
            for (int i = 0; i < ssmArray.Count(); i++)
            {
                ssmArray[i] = (y[i] - yAve) * (y[i] - yAve);
            }
            double ssm = ssmArray.Sum();            //"Sum of squares regression" or "Sum of squares model" or simply (y - y_ave)^2

            double mse = sse / (n - p);   //mean squared error


            //Returning Basic Statistics
            //==========================================================================================

            toReturn.Coeffs = new double[p];
            for (int i = 0; i < coeffs.GetLength(0); i++)
            {
                toReturn.Coeffs[i] = coeffs[i, 0];
            }

            toReturn.NMBE = 0;                      //This is defined to be zero, other than round-off errors.
            toReturn.StandardError = Math.Sqrt(sse / (y.GetLength(0) - p));
            toReturn.CV = toReturn.StandardError / yAve;
            toReturn.Tstats = new double[p];
            toReturn.Rsquared = 1 - (sse / ssm);


            //Advanced Statistics
            //============================================================================================
            if (advancedStats == true)
            {

                //Send back original data and modified data
                //-----------------------------------------
                toReturn.Ydata = y;
                toReturn.Xdata = x;


                //Calculate Predictions and Residuals
                //-----------------------------------
                double[] predictions = new double[n];
                double[] residuals = new double[n];
                for (int i = 0; i < n; i++)
                {
                    predictions[i] = 0;
                    for (int j = 0; j < coeffs.GetLength(0); j++)
                    {
                        predictions[i] = predictions[i] + coeffs[j, 0] * xFull[i, j];
                    }
                    residuals[i] = y[i] - predictions[i];
                }



                //Calculate standardized residuals
                //--------------------------------
                double tempSum = 0;
                double residAve = residuals.Average();

                for (int i = 0; i < n; i++)
                {
                    tempSum += ((residuals[i] - residAve) * (residuals[i] - residAve));
                }
                double residualStandDev = Math.Sqrt(tempSum / (n - 1));

                double[] standResiduals = new double[residuals.Count()];
                for (int i = 0; i < residuals.Count(); i++)
                {
                    standResiduals[i] = residuals[i] / residualStandDev;
                }



                ////Calculate Hat Matrix X*(X'X)^-1*X'
                //double[,] hatMatrix = MatrixMethods.MatMultiply(MatrixMethods.MatMultiply(xFull, c), MatrixMethods.MatTranspose(xFull));


                ////Calculate Cook's distance
                //double[] cooksDistance = new double[n];
                //for (int i = 0; i < n; i++)
                //{
                //    cooksDistance[i] = (residuals[i] * residuals[i] * hatMatrix[i, i]) /
                //        (p * mse * (1 - hatMatrix[i, i]) * (1 - hatMatrix[i, i]));
                //}

                toReturn.CoeffsStandardErrors = new double[p];
                for (int i = 0; i < p; i++)
                {
                    toReturn.CoeffsStandardErrors[i] = toReturn.StandardError * Math.Sqrt(c[i, i]);
                    toReturn.Tstats[i] = toReturn.Coeffs[i] / toReturn.CoeffsStandardErrors[i];
                }

                toReturn.Residuals = residuals;
                toReturn.StandResiduals = standResiduals;
                toReturn.Yhat = predictions;
                toReturn.Fstatistic = ((toReturn.Rsquared) / (p - 1)) / ((1 - toReturn.Rsquared) / (n - p));
                //toReturn.CooksDistance = cooksDistance;

                toReturn.AdjRsquared = 1 - ((((double)n - 1) / ((double)n - (double)p)) * (1 - toReturn.Rsquared));
            }
            return toReturn;


        }

        public static RegressionOutputs CloneRegressionOutputs(RegressionOutputs regOutputToClone)
        {
            return regOutputToClone;
        }
        /// <summary>
        /// Calculates the root-mean-squared error (RMSE) between two datasets.  Returns -99 if the two datasets are not of equal length
        /// </summary>
        /// <param name="yPredicted"></param>
        /// <param name="yMeasured"></param>
        /// <returns>Root Mean Squared Error.</returns>
        public static double CalculateRMSE(List<double> yPredicted, List<double> yMeasured)
        {
            double sse = 0;

            if (yPredicted.Count != yMeasured.Count) return -99;

            for (int i = 0; i < yMeasured.Count; i++)
            {
                sse += (yPredicted[i] - yMeasured[i]) * (yPredicted[i] - yMeasured[i]);
            }
            return Math.Sqrt(sse / yMeasured.Count);
        }
    }





    /// <summary>
    /// This class holds all the coefficients and statistics for multiple linear regression. Used 
    /// mostly with Validator.
    /// </summary>
    public class RegressionOutputs
    {
        //Basic statistics
        //------------------------------------------------------------------------------------------

        /// <summary>
        /// Coefficients
        /// </summary>
        public double[] Coeffs { get; set; }
        /// <summary>
        /// R^2 value betwewen 0-1
        /// </summary>
        public double Rsquared { get; set; }
        /// <summary>
        /// Adjusted R^2 Value
        /// </summary>
        public double AdjRsquared { get; set; }
        /// <summary>
        /// Standard error for the linear regression model. Sqrt(SSE/n - p), essentially the RMSE of the model.
        /// </summary>
        public double StandardError { get; set; }
        /// <summary>
        /// Array of the t-statistics corresponding to the coefficients.
        /// </summary>
        public double[] Tstats { get; set; }
        /// <summary>
        /// Standard errors corresponding to the coefficients.
        /// </summary>
        public double[] CoeffsStandardErrors { get; set; }

        //Advanced Statistics
        //------------------------------------------------------------------------------------------

        /// <summary>
        /// Coefficient of Variation (not in units of percent, expect to be between 0-1)
        /// </summary>
        public double CV { get; set; }
        /// <summary>
        /// Normalized mean bias error (not in units of percent, expected to be between 0-1)
        /// </summary>
        public double NMBE { get; set; }
        /// <summary>
        /// Predicted Y values
        /// </summary>
        public double[] Yhat { get; set; }
        /// <summary>
        /// Array of residuals from the regression
        /// </summary>
        public double[] Residuals { get; set; }
        /// <summary>
        /// Array of standardized residuals, or studentized residuals.
        /// </summary>
        public double[] StandResiduals { get; set; }
        /// <summary>
        /// F statistic for the model. F = (SSyy - SSE)/p / (SSE/(n-p))
        /// </summary>
        public double Fstatistic { get; set; }
        /// <summary>
        /// n-length array of Cooks distance
        /// </summary>
        public double[] CooksDistance { get; set; }
        /// <summary>
        /// Y data used in the regression
        /// </summary>
        public double[] Ydata { get; set; }
        /// <summary>
        /// Two dimensional array of x data used in the regression.
        /// </summary>
        public double[,] Xdata { get; set; }


        public RegressionOutputs CloneBasicStatistics()
        {
            RegressionOutputs newOutputs = new RegressionOutputs();


            if (Coeffs != null)
            {
                newOutputs.Coeffs = new double[Coeffs.Length];
                Coeffs.CopyTo(newOutputs.Coeffs, 0);
            }
            newOutputs.Rsquared = Rsquared;
            newOutputs.AdjRsquared = AdjRsquared;
            newOutputs.StandardError = StandardError;
            if (Tstats != null)
            {
                newOutputs.Tstats = new double[Tstats.Length];
                Tstats.CopyTo(newOutputs.Tstats, 0);
            }
            if (CoeffsStandardErrors != null)
            {
                newOutputs.CoeffsStandardErrors = new double[CoeffsStandardErrors.Length];
                CoeffsStandardErrors.CopyTo(newOutputs.CoeffsStandardErrors, 0);
            }
            newOutputs.CV = CV;
            newOutputs.NMBE = NMBE;



            return newOutputs;
        }

    }
}
