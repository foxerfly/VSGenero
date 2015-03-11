﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VSGenero.Analysis.Parsing.AST
{
    public class DeclareStatement : FglStatement
    {
        public string Identifier { get; private set; }
        public bool Scroll { get; private set; }
        public bool WithHold { get; private set; }

        public string PreparedStatementId { get; private set; }

        public static bool TryParseNode(Parser parser, out DeclareStatement defNode)
        {
            defNode = null;
            bool result = false;

            if(parser.PeekToken(TokenKind.DeclareKeyword))
            {
                result = true;
                defNode = new DeclareStatement();
                parser.NextToken();
                defNode.StartIndex = parser.Token.Span.Start;

                if (parser.PeekToken(TokenCategory.Identifier) || parser.PeekToken(TokenCategory.Keyword))
                {
                    parser.NextToken();
                    defNode.Identifier = parser.Token.Token.Value.ToString();

                    if(parser.PeekToken(TokenKind.ScrollKeyword))
                    {
                        parser.NextToken();
                        defNode.Scroll = true;
                    }

                    if(parser.PeekToken(TokenKind.CursorKeyword))
                    {
                        parser.NextToken();

                        if(parser.PeekToken(TokenKind.WithKeyword))
                        {
                            parser.NextToken();
                            if(parser.PeekToken(TokenKind.HoldKeyword))
                            {
                                parser.NextToken();
                                defNode.WithHold = true;
                            }
                            else
                            {
                                parser.ReportSyntaxError("SQL declare statement missing \"hold\" keyword.");
                            }
                        }

                        if(parser.PeekToken(TokenKind.FromKeyword))
                        {
                            parser.NextToken();
                            // We have a string expression declare
                            ExpressionNode exprNode;
                            if(ExpressionNode.TryGetExpressionNode(parser, out exprNode) && exprNode is StringExpressionNode)
                            {
                                defNode.Children.Add(exprNode.StartIndex, exprNode);
                                defNode.EndIndex = exprNode.EndIndex;
                            }
                            else
                            {
                                parser.ReportSyntaxError("String expression not found for SQl declare statement");
                            }
                        }
                        else if(parser.PeekToken(TokenKind.ForKeyword))
                        {
                            parser.NextToken();
                            if(parser.PeekToken(TokenKind.SqlKeyword))
                            {
                                // we have a sql block declare
                                SqlBlockNode sqlBlock;
                                if(SqlBlockNode.TryParseSqlNode(parser, out sqlBlock))
                                {
                                    defNode.Children.Add(sqlBlock.StartIndex, sqlBlock);
                                    defNode.EndIndex = sqlBlock.EndIndex;
                                }
                            }
                            else if(parser.PeekToken(TokenKind.SelectKeyword))
                            {
                                // we have a static sql select statement
                                SqlStatement sqlStmt;
                                bool dummy;
                                if(SqlStatement.TryParseNode(parser, out sqlStmt, out dummy, TokenKind.SelectKeyword))
                                {
                                    defNode.Children.Add(sqlStmt.StartIndex, sqlStmt);
                                    defNode.EndIndex = sqlStmt.EndIndex;
                                }
                                else
                                {
                                    parser.ReportSyntaxError("Static SQL declare statement must specify a SELECT statement.");
                                }
                            }
                            else if(parser.PeekToken(TokenCategory.Identifier) || parser.PeekToken(TokenCategory.Keyword))
                            {
                                // we have a prepared statment
                                parser.NextToken();
                                defNode.PreparedStatementId = parser.Token.Token.Value.ToString();
                                defNode.EndIndex = parser.Token.Span.End;
                            }
                            else
                            {
                                parser.ReportSyntaxError("Invalid token found in SQL declare statment.");
                            }
                        }
                        else
                        {
                            parser.ReportSyntaxError("SQL declare statement must have either \"for\" or \"from\" keyword.");
                        }
                    }
                    else
                    {
                        parser.ReportSyntaxError("SQL declare statement missing \"cursor\" keyword.");
                    }
                }
                else
                {
                    parser.ReportSyntaxError("SQL declare statement must specify an identifier to declare.");
                }
            }

            return result;
        }
    }
}
