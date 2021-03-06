﻿/* ****************************************************************************
 * Copyright (c) 2015 Greg Fullman 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution.
 * By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/ 

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VSGenero.Analysis.Parsing.AST_4GL
{
    public class SqlBlockNode : FglStatement
    {
        public List<TokenWithSpan> Tokens { get; private set; }

        public static bool TryParseSqlNode(Genero4glParser parser, out SqlBlockNode node)
        {
            node = null;
            bool result = false;

            if (parser.PeekToken(TokenKind.SqlKeyword))
            {
                result = true;
                node = new SqlBlockNode();
                node.Tokens = new List<TokenWithSpan>();
                parser.NextToken();
                node.StartIndex = parser.Token.Span.Start;
                node.DecoratorEnd = parser.Token.Span.End;

                while (!parser.PeekToken(TokenKind.EndOfFile) &&
                       !(parser.PeekToken(TokenKind.EndKeyword) && parser.PeekToken(TokenKind.SqlKeyword, 2)))
                {
                    parser.NextToken();
                    node.Tokens.Add(parser.Token);
                }
                //List<List<TokenKind>> breakSequences = new List<List<TokenKind>>() 
                //    { 
                //        new List<TokenKind> { TokenKind.EndKeyword, TokenKind.SqlKeyword }
                //    };

                //FglStatement sqlStmt;
                //bool matchedBreakSequence = false;
                //// TODO: really any sql statement supported by the SQL server is allowed in the sql block. So we may not want to validate it...
                //if (SqlStatementFactory.TryParseSqlStatement(parser, out sqlStmt, out matchedBreakSequence, TokenKind.EndOfFile, breakSequences) && sqlStmt != null)
                //{
                //    node.Children.Add(sqlStmt.StartIndex, sqlStmt);
                //}
                //else if(parser.PeekToken(TokenKind.CreateKeyword))
                //{
                //    CreateStatement createStmt;
                //    if (CreateStatement.TryParseNode(parser, out createStmt))
                //        node.Children.Add(createStmt.StartIndex, createStmt);
                //    else
                //        parser.ReportSyntaxError("Invalid create statement found in SQL block.");
                //}
                //else
                //{
                //    parser.ReportSyntaxError("No SQL statement found within the SQL block.");
                //}

                if (!parser.PeekToken(TokenKind.EndOfFile))
                {
                    parser.NextToken();
                    if (parser.PeekToken(TokenKind.SqlKeyword))
                    {
                        parser.NextToken();
                        node.EndIndex = parser.Token.Span.End;
                        node.IsComplete = true;
                    }
                    else
                    {
                        parser.ReportSyntaxError(parser.Token.Span.Start, parser.Token.Span.End, "Invalid end of SQL block.");
                    }
                }
                else
                {
                    parser.ReportSyntaxError("Unexpected end of SQL block.");
                }
            }

            return result;
        }

        public override bool CanOutline
        {
            get { return true; }
        }

        public override int DecoratorEnd { get; set; }
    }
}
