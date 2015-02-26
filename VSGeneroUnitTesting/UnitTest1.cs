﻿using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Data.Schema.ScriptDom.Sql;
using Microsoft.Data.Schema.ScriptDom;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Text;
using VSGenero.SqlSupport;
using VSGenero.Analysis;

namespace VSGeneroUnitTesting
{
    [TestClass]
    public class SqlExtraction
    {
        [TestMethod]
        public void SimpleStaticTest()
        {
            string text = "select * from testtable where txre_info = 3";
            foreach (var fragment in SqlStatementExtractor.ExtractStatements(text))
            {
                Assert.IsTrue(fragment != null);
                Assert.IsTrue(fragment.FragmentLength > 0);
            }
        }

        [TestMethod]
        public void SimpleDynamicTest()
        {
            string text = "\"select * from testtable where txre_info = 3\"";
            foreach (var fragment in SqlStatementExtractor.ExtractStatements(text))
            {
                Assert.IsTrue(fragment != null);
                Assert.IsTrue(fragment.FragmentLength > 0);
            }
        }

        [TestMethod]
        public void SimpleDynamicTestWithPlaceholder()
        {
            string text = "\"select * from testtable where txre_info = ?\"";
            foreach (var fragment in SqlStatementExtractor.ExtractStatements(text))
            {
                Assert.IsTrue(fragment != null);
                Assert.IsTrue(fragment.FragmentLength > 0);
            }
        }

        [TestMethod]
        public void SimpleIncompleteDynamicTest()
        {
            string text = "\"select * from testtable where txre_info = 3";
            foreach (var fragment in SqlStatementExtractor.ExtractStatements(text))
            {
                // no valid statement should be recognized here.
                Assert.Fail();
            }
        }

        [TestMethod]
        public void EmbeddedStaticTest()
        {
            string text = "if(n == 1) then\nlet n = 2\nend if\nselect * from testtable where txre_info = 3\ndisplay record";
            foreach (var fragment in SqlStatementExtractor.ExtractStatements(text))
            {
                Assert.IsTrue(fragment != null);
                Assert.IsTrue(fragment.FragmentLength > 0);
            }
        }

        [TestMethod]
        public void EmbeddedMultilineStaticTest()
        {
            string text = "if(n == 1) then\nlet n = 2\nend if\nselect * from testtable where txre_info = 3\n and txre_mult = 4\ndisplay record";
            foreach (var fragment in SqlStatementExtractor.ExtractStatements(text))
            {
                Assert.IsTrue(fragment != null);
                Assert.IsTrue(fragment.FragmentLength > 0);
            }
        }

        [TestMethod]
        public void EmbeddedMultilineStaticTest2()
        {
            string text = "if(n == 1) then\nlet n = 2\nend if\nselect * from testtable inner join othertable on testtable.txre_id = othertable.arbq_id where txre_info = 3\n and txre_mult = 4\ndisplay record\nupdate othertable set arbq_id = 1\nlet x = 0";
            foreach (var fragment in SqlStatementExtractor.ExtractStatements(text))
            {
                Assert.IsTrue(fragment != null);
                Assert.IsTrue(fragment.FragmentLength > 0);
            }
        }

        [TestMethod]
        public void EmbeddedMultilineMultiresultDynamicTest()
        {
            string text = "if(n == 1) then\nlet n = 2\nend if\nlet prepare = \"select * from testtable inner join othertable\",\n    \" on testtable.txre_id = othertable.arbq_id\",\n   \" where txre_info = ? and txre_mult = ?\"\ndisplay record\nupdate othertable set arbq_id = 1\nlet x = 0";
            foreach (var fragment in SqlStatementExtractor.ExtractStatements(text))
            {
                Assert.IsTrue(fragment != null);
                Assert.IsTrue(fragment.FragmentLength > 0);
            }
        }

        [TestMethod]
        public void TokenizerTest1()
        {
            Tokenizer t = new Tokenizer(null, options: TokenizerOptions.Verbatim | TokenizerOptions.VerbatimCommentsAndLineJoins);

            using (TextReader tr = new StringReader("let x = 3 # this is a comment"))
            {
                t.Initialize(tr);
                var tokens = t.ReadTokens(int.MaxValue);
                Assert.IsTrue(tokens.Count == 5);
            }
        }


        [TestMethod]
        public void ParserTest1()
        {
            ParserOptions po = new ParserOptions();
            po.ErrorSink = new TestErrorSink();
            //string codeSample = "globals\n\tdefine x, y record\n\tz smallint, a int\nend record\nend globals";
            string codeSample = "globals\n\tdefine x, y record like tablename.*\nend globals";
            
            using (TextReader tr = new StringReader(codeSample))
            {
                VSGenero.Analysis.Parser p = VSGenero.Analysis.Parser.CreateParser(tr, po);
                var node = p.ParseFile();
                int i = 0;
            }
        }
    }

    public class TestErrorSink : ErrorSink
    {
        public override void Add(string message, int[] lineLocations, int startIndex, int endIndex, int errorCode, Severity severity)
        {
            int i = 0;
        }
    }
}
