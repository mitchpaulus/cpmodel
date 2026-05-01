# cpmodel

`cpmodel` is a command-line tool for fitting simple linear and change-point regression models to two-column numeric data. It is intended for quick model fitting from tabular files or standard input, with optional JSON output for downstream automation.

## Requirements

- .NET 9 SDK

## Build

From the repository root:

```sh
dotnet build
```

Run the CLI without publishing:

```sh
dotnet run --project cpmodel -- --help
```

Create a release build:

```sh
dotnet publish cpmodel -c Release
```

## Usage

```sh
cpmodel <options> <file>
```

Use `-` as the file name to read from standard input.

```sh
dotnet run --project cpmodel -- -p 2p cpmodel/slr_test.tsv
```

By default, the tool reads the first column as `x`, the second column as `y`, splits fields on whitespace, and fits a `4` parameter change-point model.

## Input Format

Input files should contain numeric observations with one record per line:

```text
1.47    52.21
1.50    53.12
1.52    54.48
```

Whitespace-delimited files work by default. To use another delimiter, pass `-d` or `--delimeter`:

```sh
dotnet run --project cpmodel -- -d "," --x-col 1 --y-col 3 data.csv
```

Header rows can be skipped:

```sh
dotnet run --project cpmodel -- --skip 1 data.tsv
```

## Model Types

Select a model with `-p <model type>`.

| Type | Model |
| --- | --- |
| `2p` | Simple linear regression: `y = b0 + b1 * x` |
| `3h` | Three-parameter heating change-point model: `y = b0 + b1 * max(0, cp - x)` |
| `3c` | Three-parameter cooling change-point model: `y = b0 + b1 * max(0, x - cp)` |
| `3h_new` | Alternate three-parameter heating fit |
| `3c_new` | Alternate three-parameter cooling fit |
| `4` | Four-parameter heating and cooling model with one change point |
| `5` | Five-parameter model with low and high change points |

For `3h`, `3c`, `4`, and `5`, change points are searched in `0.125` increments across the input `x` range.

## Output

Without additional flags, `cpmodel` prints one value per line:

```sh
dotnet run --project cpmodel -- -p 2p cpmodel/slr_test.tsv
```

```text
-39.06195591883866
61.272186542107434
```

For change-point models, the final printed values include the selected change point or change points.

Use `-c` to print model coordinates instead of coefficients. This is useful for plotting fitted model lines:

```sh
dotnet run --project cpmodel -- -p 2p -c cpmodel/slr_test.tsv
```

Use `--json` to print coefficients, fit statistics, residuals, predictions, and an Excel `LAMBDA` expression where available:

```sh
dotnet run --project cpmodel -- -p 2p --json cpmodel/slr_test.tsv
```

## Options

```text
-c                       Print model coordinates, not coefficients
-d, --delimeter <delim>  Delimiter to split lines by [whitespace]
--json                   Print outputs in JSON format
-p <model type>          Model type: 2p, 3h, 3c, 3h_new, 3c_new, 4, 5 [4]
--skip n                 Skip n header records [0]
--x-col <int col>        Set X column number, 1-based integer [1]
--y-col <int col>        Set Y column number, 1-based integer [2]
-h, --help               Show help and exit
```

Note: the option name is currently spelled `--delimeter` in the CLI.
