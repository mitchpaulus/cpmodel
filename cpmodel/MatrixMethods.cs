using System;
using System.Linq;

namespace mlr
{

    /// <summary>
    /// This class holds the matrix methods that are used in the multiple linear regression code.
    /// Has methods for matrix transpose, matrix multiplication, Cholesky decomposition, 
    /// Matrix inversion, and substitution (required for taking the inverse).
    /// </summary>
    public static class MatrixMethods
    {
        public static double[,] ToTwoDimensions(this double[] array)
        {
            double[,] newMatrix = new double[array.Length, 1];
            for (int i = 0; i < array.Length; i++)
            {
                newMatrix[i, 0] = array[i];
            }
            return newMatrix;
        }

        public static double[,] AddColumnToEnd(double[,] xOriginal, double[] additionalColumn)
        {
            //Check that the sizes are the same.
            if (xOriginal.GetLength(0) != additionalColumn.Length) throw new ArgumentException("The matrix sizes do not match");

            double[,] newMatrix = new double[xOriginal.GetLength(0), xOriginal.GetLength(1) + 1];

            for (int i = 0; i < xOriginal.GetLength(0); i++)
            {
                for (int j = 0; j < xOriginal.GetLength(1); j++)
                {
                    newMatrix[i, j] = xOriginal[i, j];
                }
                newMatrix[i, xOriginal.GetLength(1)] = additionalColumn[i];
            }

            return newMatrix;
        }

        /// <summary>
        /// MatTranspose returns the transpose of a 2 dimensional matrix.
        /// </summary>
        /// <param name="x">double array of values</param>
        /// <returns>double array of the 2 dimensional matrix transpose</returns>
        public static double[,] MatTranspose(double[,] x)
        {
            double[,] result = new double[x.GetLength(1), x.GetLength(0)];
            for (int i = 0; i < x.GetLength(0); i++)
            {
                for (int j = 0; j < x.GetLength(1); j++)
                {
                    result[j, i] = x[i, j];
                }
            }
            return result;
        }

        /// <summary>
        /// MatMultiply returns the result of a matrix multiplication operation.  The number of columns in y 
        /// must equal the number of rows in x.
        /// </summary>
        /// <param name="y">double array for the left matrix</param>
        /// <param name="x">double array for the right matrix</param>
        /// <returns>multiplied double array</returns>
        public static double[,] MatMultiply(double[,] y, double[,] x)
        {
            if (y.GetLength(1) != x.GetLength(0))
            {
                throw new Exception("The size of the matrices needs to be correct");
            }

            double[,] result = new double[y.GetLength(0), x.GetLength(1)];

            for (int i = 0; i < y.GetLength(0); i++)
            {
                for (int j = 0; j < x.GetLength(1); j++)
                {
                    double tempSum = 0;
                    for (int k = 0; k < y.GetLength(1); k++)
                    {
                        tempSum += y[i, k] * x[k, j];
                    }
                    result[i, j] = tempSum;
                }
            }
            return result;
        }
        /// <summary>
        /// This method performs the Cholesky decomposition of a symmetric matrix.  The matrix 
        /// also shoudl be positive definite.
        /// </summary>
        /// <param name="x">double square symetric matrix</param>
        /// <returns>The lower triangular matrix for the Cholesky decomposition</returns>
        public static double[,] CholeskyDecomp(double[,] x)
        {
            double[,] choleskyDecomp = new double[x.GetLength(0), x.GetLength(1)];

            if (x.GetLength(0) != x.GetLength(1))
            {
                throw new Exception("Matrix must be square");
            }

            for (int k = 0; k < x.GetLength(0); k++)
            {
                double tempSum = 0;
                for (int i = 0; i < k; i++)
                {
                    tempSum = 0;
                    for (int j = 0; j < i; j++)
                    {
                        tempSum += (choleskyDecomp[i, j] * choleskyDecomp[k, j]);
                    }
                    choleskyDecomp[k, i] = (x[k, i] - tempSum) / choleskyDecomp[i, i];
                }
                tempSum = 0;
                for (int j = 0; j < k; j++)
                {
                    tempSum += (choleskyDecomp[k, j] * choleskyDecomp[k, j]);
                }

                choleskyDecomp[k, k] = Math.Sqrt(x[k, k] - tempSum);
            }

            return choleskyDecomp;
        }

        public static double[,] MatrixInv(double[,] x, double[,] chomskyDec)
        {
            //Make sure the matrix is square
            if (x.GetLength(0) != x.GetLength(1))
            {
                throw new Exception("Matrix must be square to take inverse.");
            }

            double[,] xInv = new double[x.GetLength(0), x.GetLength(1)];
            double[] d = new double[x.GetLength(0)];
            double[] b = new double[x.GetLength(0)];
            for (int i = 0; i < x.GetLength(0); i++)
            {
                for (int j = 0; j < x.GetLength(0); j++)
                {
                    if (j == i)
                    {
                        b[j] = 1;
                    }
                    else
                    {
                        b[j] = 0;
                    }
                }

                d = Substitute(chomskyDec, MatTranspose(chomskyDec), b);

                for (int j = 0; j < x.GetLength(0); j++)
                {
                    xInv[j, i] = d[j];
                }


            }
            return xInv;
        }

        /// <summary>
        /// Performs the forward and backward substitution assuming you have given a decomposed lower and upper triangular matrix.
        /// Works with either LU decomposition or Cholesky deompostion
        /// </summary>
        /// <param name="a"></param>
        /// <param name="upperTri"></param>
        /// <param name="b"></param>
        /// <param name="lowerTri"></param>
        /// <returns></returns>
        private static double[] Substitute(double[,] lowerTri, double[,] upperTri, double[] b)
        {
            double tempSum = 0;
            double[] d = new double[b.GetLength(0)];
            double[] x = new double[b.GetLength(0)];

            //forward substitution
            d[0] = b[0] / lowerTri[0, 0];
            for (int i = 1; i < b.GetLength(0); i++)
            {
                tempSum = b[i];
                for (int j = 0; j < i; j++)
                {
                    tempSum -= (lowerTri[i, j] * d[j]);
                }
                d[i] = tempSum / lowerTri[i, i];
            }

            //back substitution
            x[b.GetLength(0) - 1] = d.Last() / upperTri[b.GetLength(0) - 1, b.GetLength(0) - 1];
            for (int i = b.GetLength(0) - 2; i >= 0; i--)
            {
                tempSum = 0;
                for (int j = i + 1; j < b.GetLength(0); j++)
                {
                    tempSum += (upperTri[i, j] * x[j]);
                }
                x[i] = (d[i] - tempSum) / upperTri[i, i];
            }
            return x;
        }
    }
}
