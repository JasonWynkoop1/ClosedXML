﻿using ClosedXML.Excel.CalcEngine;
using NUnit.Framework;
using System.Collections.Generic;
using System.Text;
using ScalarValue = OneOf.OneOf<bool, double, string, ClosedXML.Excel.CalcEngine.Error>;
using AnyValue = OneOf.OneOf<bool, double, string, ClosedXML.Excel.CalcEngine.Error, ClosedXML.Excel.CalcEngine.Array, ClosedXML.Excel.CalcEngine.Reference>;
using System.Globalization;
using ClosedXML.Excel;

namespace ClosedXML.Tests.Excel.CalcEngine
{
    // TODO: Once array formulas are supported, remove internal API and replace with workbook formulas.
    /// <summary>
    /// Tests for arrays and reference operators.
    /// </summary>
    [TestFixture]
    public class OperatorsCollectionTests
    {
        private static readonly CalcContext Ctx = new(null, CultureInfo.InvariantCulture, null, null, null);

        [Test]
        public void ArrayOperandSameSizeArray_ElementsAreCalculatedAsScalarValues()
        {
            var typesPerColumn = new ConstArray(new ScalarValue[5, 5]
            {
                { true, 1, "1", "one", Error.CellReference },
                { true, 1, "1", "one", Error.CellReference },
                { true, 1, "1", "one", Error.CellReference },
                { true, 1, "1", "one", Error.CellReference },
                { true, 1, "1", "one", Error.CellReference }
            });
            var typesPerRow = new ConstArray(new ScalarValue[5, 5]
            {
                { true, true, true, true, true },
                { 2,2,2,2,2 },
                { "2", "2", "2", "2", "2"},
                { "two", "two", "two", "two", "two"},
                { Error.NumberInvalid, Error.NumberInvalid, Error.NumberInvalid, Error.NumberInvalid, Error.NumberInvalid }
            });
            var result = ((AnyValue)typesPerColumn).Concat(typesPerRow, Ctx).AsT4;

            for (var row = 0; row < 5; ++row)
            {
                for (var col = 0; col < 5; ++col)
                {
                    var lhs = typesPerColumn[row, col].ToAnyValue();
                    var rhs = typesPerRow[row, col].ToAnyValue();
                    lhs.Concat(rhs, Ctx).TryPickScalar(out var expectedResult, out var _);
                    var actualValue = result[row, col];
                    Assert.AreEqual(expectedResult, actualValue);
                }
            }
        }

        [Test]
        public void ArrayOperandDifferentSizedArray_ResizeAndUseNAForMissingValues()
        {
            AnyValue lhs = new ConstArray(new ScalarValue[2, 1] { { 1 }, { 2 } });
            AnyValue rhs = new ConstArray(new ScalarValue[1, 2] { { 3, 4 } });

            var result = lhs.BinaryPlus(rhs, Ctx).AsT4;

            Assert.AreEqual(result.Width, 2);
            Assert.AreEqual(result.Height, 2);
            Assert.AreEqual(result[0, 0], ScalarValue.FromT1(4));
            Assert.AreEqual(result[0, 1], ScalarValue.FromT3(Error.NoValueAvailable));
            Assert.AreEqual(result[1, 0], ScalarValue.FromT3(Error.NoValueAvailable));
            Assert.AreEqual(result[1, 1], ScalarValue.FromT3(Error.NoValueAvailable));
        }

        [Test]
        public void ArrayOperandScalar_ScalarUpscaledToArray()
        {
            AnyValue array = new ConstArray(new ScalarValue[1, 2] { { 1, 2 } });
            AnyValue scalar = ScalarValue.FromT0(true).ToAnyValue();

            var arrayPlusScalarResult = array.BinaryPlus(scalar, Ctx).AsT4;
            Assert.AreEqual(arrayPlusScalarResult[0, 0], ScalarValue.FromT1(2));
            Assert.AreEqual(arrayPlusScalarResult[0, 1], ScalarValue.FromT1(3));

            var scalarPlusArrayResult = scalar.BinaryPlus(array, Ctx).AsT4;
            Assert.AreEqual(scalarPlusArrayResult[0, 0], ScalarValue.FromT1(2));
            Assert.AreEqual(scalarPlusArrayResult[0, 1], ScalarValue.FromT1(3));
        }

        [Test]
        public void ArrayOperandSingleCellReference_ReferencedCellValueUpscaledToArray()
        {
            var wb = new XLWorkbook();
            var ws = wb.AddWorksheet() as XLWorksheet;
            ws.Cell("A1").Value = "5";
            AnyValue array = new ConstArray(new ScalarValue[1, 2] { { 10, 5 } });
            AnyValue singleCellReference = new Reference(new XLRangeAddress(XLAddress.Create("A1"), XLAddress.Create("A1")));
            var ctx = new CalcContext(null, CultureInfo.InvariantCulture, wb, ws, null);

            var arrayDividedByReference = array.BinaryDiv(singleCellReference, ctx).AsT4;
            Assert.AreEqual(2, arrayDividedByReference.Width);
            Assert.AreEqual(1, arrayDividedByReference.Height);
            Assert.AreEqual(arrayDividedByReference[0, 0], ScalarValue.FromT1(2));
            Assert.AreEqual(arrayDividedByReference[0, 1], ScalarValue.FromT1(1));

            var referenceDividedByArray = singleCellReference.BinaryDiv(array, ctx).AsT4;
            Assert.AreEqual(2, referenceDividedByArray.Width);
            Assert.AreEqual(1, referenceDividedByArray.Height);
            Assert.AreEqual(referenceDividedByArray[0, 0], ScalarValue.FromT1(0.5));
            Assert.AreEqual(referenceDividedByArray[0, 1], ScalarValue.FromT1(1));
        }

        [Test]
        public void ArrayOperandAreaReference_ReferenceBehavesAsArray()
        {
            var wb = new XLWorkbook();
            var ws = wb.AddWorksheet() as XLWorksheet;
            ws.Cell("A1").Value = "5";
            ws.Cell("B1").Value = 1;
            ws.Cell("C1").Value = 2;
            AnyValue array = new ConstArray(new ScalarValue[1, 2] { { 10, 5 } });
            AnyValue areaReference = new Reference(new XLRangeAddress(XLAddress.Create("A1"), XLAddress.Create("C1")));

            var arrayMultArea = array.BinaryMult(areaReference, new CalcContext(null, CultureInfo.InvariantCulture, wb, ws, null)).AsT4;

            Assert.AreEqual(3, arrayMultArea.Width);
            Assert.AreEqual(1, arrayMultArea.Height);
            Assert.AreEqual((ScalarValue)50, arrayMultArea[0, 0]);
            Assert.AreEqual((ScalarValue)5, arrayMultArea[0, 1]);
            Assert.AreEqual((ScalarValue)Error.NoValueAvailable, arrayMultArea[0, 2]);

            var areaMultArray = areaReference.BinaryMult(array, new CalcContext(null, CultureInfo.InvariantCulture, wb, ws, null)).AsT4;

            Assert.AreEqual(3, areaMultArray.Width);
            Assert.AreEqual(1, areaMultArray.Height);
            Assert.AreEqual((ScalarValue)50, areaMultArray[0, 0]);
            Assert.AreEqual((ScalarValue)5, areaMultArray[0, 1]);
            Assert.AreEqual((ScalarValue)Error.NoValueAvailable, areaMultArray[0, 2]);
        }
    }
}