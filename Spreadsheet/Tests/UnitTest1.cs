﻿using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SS;
using System.Collections.Generic;
using Formulas;


namespace SS
{
    [TestClass]
    public class SpreadsheetTests
    {
        /// <summary>
        /// Tests null exception for all three SetCellContents methods (string)
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void TestMethod1()
        {
            String empty = null;
            AbstractSpreadsheet sheet = new Spreadsheet();
            sheet.SetCellContents("A1", empty);
        }

        /// <summary>
        /// Tests null name exception for all three SetCellContents method (string)
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidNameException))]
        public void TestMethod2()
        {
            AbstractSpreadsheet sheet = new Spreadsheet();
            sheet.SetCellContents(null, "string");
        }

        /// <summary>
        /// Tests invalid name exception for all three SetCellContents method (string)
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidNameException))]
        public void TestMethod3()
        {
            AbstractSpreadsheet sheet = new Spreadsheet();
            sheet.SetCellContents("4356", "string");

        }

        /// <summary>
        /// Tests null name exception for all three GetCellContents methods
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidNameException))]
        public void TestMethod4()
        {
            AbstractSpreadsheet sheet = new Spreadsheet();
            sheet.GetCellContents(null);

        }

        /// <summary>
        /// Tests invalid name exception for all three GetCellContents methods
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidNameException))]
        public void TestMethod5()
        {
            AbstractSpreadsheet sheet = new Spreadsheet();
            sheet.GetCellContents("45345");

        }

        /// <summary>
        /// Tests null name exception for all three SetCellContents method (Formula)
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidNameException))]
        public void TestMethod7()
        {
            Formula test = new Formula("1 + 1");
            AbstractSpreadsheet sheet = new Spreadsheet();
            sheet.SetCellContents(null, test);
        }

        /// <summary>
        /// Tests invalid name exception for all three SetCellContents method (Formula)
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidNameException))]
        public void TestMethod8()
        {
            Formula test = new Formula("1 + 1");
            AbstractSpreadsheet sheet = new Spreadsheet();
            sheet.SetCellContents("4356", test);
        }

        /// <summary>
        /// Tests null name exception for all three SetCellContents method (double)
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidNameException))]
        public void TestMethod9()
        {
            AbstractSpreadsheet sheet = new Spreadsheet();
            sheet.SetCellContents(null, 42);
        }

        /// <summary>
        /// Tests invalid name exception for all three SetCellContents method (double)
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidNameException))]
        public void TestMethod10()
        {
            AbstractSpreadsheet sheet = new Spreadsheet();
            sheet.SetCellContents("4356", 42);

        }

        /// <summary>
        /// Tests null arg exception for GetDirectDependents
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void TestMethod12()
        {
            PrivateObject accessor = new PrivateObject(new Spreadsheet());
            accessor.Invoke("GetDirectDependents", new Object[] { null });

        }

        /// <summary>
        /// Tests null arg exception for GetDirectDependents
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidNameException))]
        public void TestMethod13()
        {
            PrivateObject accessor = new PrivateObject(new Spreadsheet());
            accessor.Invoke("GetDirectDependents", new Object[] { "4353" });
        }

        /// <summary>
        /// Tests for CircularException 
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(CircularException))]
        public void TestMethod14()
        {
            Formula f1 = new Formula("A1 + B1");
            AbstractSpreadsheet sheet = new Spreadsheet();
            sheet.SetCellContents("A1", f1);
        }

        /// <summary>
        /// A more complex test for CircularException
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(CircularException))]
        public void TestMethod15()
        {
            Formula f1 = new Formula("A1+B1");
            Formula f2 = new Formula("A3*B4");
            Formula f3 = new Formula("E1+C1");
            Formula f4 = new Formula("C1-A3");
            Formula f5 = new Formula("A1");
            AbstractSpreadsheet sheet = new Spreadsheet();
            sheet.SetCellContents("D1", f1);
            sheet.SetCellContents("A1", f2);
            sheet.SetCellContents("B1", f2);
            sheet.SetCellContents("A3", f3);
            sheet.SetCellContents("B4", f4);
            sheet.SetCellContents("E1", 2);
            sheet.SetCellContents("C1", 6);
            sheet.SetCellContents("A3", f5);

        }

        // --------- Non-Exception Tests ------------ //

        /// <summary>
        /// Tests for GetDirectDependents
        /// </summary>
        [TestMethod]
        public void TestMethod16()
        {
            PrivateObject accessor = new PrivateObject(new Spreadsheet());
            accessor.Invoke("GetDirectDependents", new Object[] { "A1" });

        }

        /// <summary>
        /// Tests for SetCellContents (Formula)
        /// </summary>
        [TestMethod]
        public void TestMethod17()
        {
            Formula f1 = new Formula("C1 + B1");
            AbstractSpreadsheet sheet = new Spreadsheet();
            sheet.SetCellContents("A1", f1);
            object value = sheet.GetCellContents("A1");
            Assert.AreEqual(new Formula("C1 + B1"), value);
        }

        /// <summary>
        /// Tests for SetCellContents (double)
        /// </summary>
        [TestMethod]
        public void TestMethod18()
        {
            AbstractSpreadsheet sheet = new Spreadsheet();
            sheet.SetCellContents("A1", 42);
            object value = sheet.GetCellContents("A1");
            Assert.AreEqual(42.0, value);
        }

        /// <summary>
        /// Tests for SetCellContents (string)
        /// </summary>
        [TestMethod]
        public void TestMethod19()
        {
            AbstractSpreadsheet sheet = new Spreadsheet();
            sheet.SetCellContents("A1", "Hello World");
            object value = sheet.GetCellContents("A1");
            Assert.AreEqual("Hello World", value);
        }

        /// <summary>
        /// Tests for GetCellContents
        /// </summary>
        [TestMethod]
        public void TestMethod20()
        {
            AbstractSpreadsheet sheet = new Spreadsheet();
            sheet.SetCellContents("A1", "Hello World");
            object value = sheet.GetCellContents("A1");
            Assert.AreEqual("Hello World", value);
        }

        /// <summary>
        /// Tests for SetCellContents (Formula)
        /// </summary>
        [TestMethod]
        public void TestMethod21()
        {
            Formula f1 = new Formula("c1 + b1", x => x.ToUpper(), x => true);
            AbstractSpreadsheet sheet = new Spreadsheet();
            sheet.SetCellContents("A1", f1);
            object value = sheet.GetCellContents("A1");
            Assert.AreEqual(new Formula("C1 + B1"), value);
        }

        /// <summary>
        /// Tests for replacing contents of a cell
        /// </summary>
        [TestMethod]
        public void TestMethod22()
        {
            Formula f1 = new Formula("c1 + b1", x => x.ToUpper(), x => true);
            AbstractSpreadsheet sheet = new Spreadsheet();
            sheet.SetCellContents("A1", f1);
            sheet.SetCellContents("A1", 42);
            sheet.SetCellContents("A1", f1);
            sheet.SetCellContents("A1", "Hello World");
            HashSet<string> s1 = new HashSet<string>(sheet.GetNamesOfAllNonemptyCells());
            HashSet<string> s2 = new HashSet<string>();
            s2.Add("A1");
            Assert.AreEqual(s2.Count, s1.Count);
        }

        /// <summary>
        /// Tests for replacing contents of a cell
        /// </summary>
        [TestMethod]
        public void TestMethod23()
        {
            Formula f1 = new Formula("c1 + b1", x => x.ToUpper(), x => true);
            AbstractSpreadsheet sheet = new Spreadsheet();
            sheet.SetCellContents("A1", "");
            sheet.SetCellContents("B1", 42);
            sheet.SetCellContents("C1", "Hello World");
            HashSet<string> s1 = new HashSet<string>(sheet.GetNamesOfAllNonemptyCells());
            HashSet<string> s2 = new HashSet<string>();
            s2.Add("B1");
            s2.Add("C1");
            Assert.AreEqual(s2.Count, s1.Count);
        }

    }
}