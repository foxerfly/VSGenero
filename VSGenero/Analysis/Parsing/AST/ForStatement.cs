﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VSGenero.Analysis.Parsing.AST
{
    public class ForStatement : FglStatement
    {
        public ExpressionNode CounterVariable { get; private set; }
        public ExpressionNode StartValueExpresison { get; private set; }
        public ExpressionNode EndValueExpression { get; private set; }
        public int StepValue { get; private set; }

        public static bool TryParserNode(Parser parser, out ForStatement node, 
                                        Func<string, PrepareStatement> prepStatementResolver = null,
                                        Action<PrepareStatement> prepStatementBinder = null)
        {
            node = null;
            bool result = false;

            if(parser.PeekToken(TokenKind.ForKeyword))
            {
                result = true;
                node = new ForStatement();
                parser.NextToken();
                node.StartIndex = parser.Token.Span.Start;
                node.StepValue = 1; // default value

                ExpressionNode counterVar;
                if(ExpressionNode.TryGetExpressionNode(parser, out counterVar, new List<TokenKind> { TokenKind.Equals }))
                    node.CounterVariable = counterVar;
                else
                    parser.ReportSyntaxError("Invalid expression found in for statement");

                if (parser.PeekToken(TokenKind.Equals))
                    parser.NextToken();
                else
                    parser.ReportSyntaxError("For statement missing counter variable assignment.");

                ExpressionNode startValue;
                if(ExpressionNode.TryGetExpressionNode(parser, out startValue, new List<TokenKind> { TokenKind.ToKeyword }))
                    node.StartValueExpresison = startValue;
                else
                    parser.ReportSyntaxError("Invalid expression found in for statement");

                if (parser.PeekToken(TokenKind.ToKeyword))
                    parser.NextToken();
                else
                    parser.ReportSyntaxError("For statement missing \"to\" keyword.");

                List<TokenKind> keywords = new List<TokenKind>(GeneroAst.ValidStatementKeywords);
                keywords.Add(TokenKind.StepKeyword);
                ExpressionNode endValue;
                if (ExpressionNode.TryGetExpressionNode(parser, out endValue, keywords))
                    node.EndValueExpression = endValue;
                else
                    parser.ReportSyntaxError("Invalid expression found in for statement");

                if(parser.PeekToken(TokenKind.StepKeyword))
                {
                    parser.NextToken();
                    if(parser.PeekToken(TokenCategory.NumericLiteral))
                    {
                        parser.NextToken();
                        int temp;
                        if(int.TryParse(parser.Token.Token.Value.ToString(), out temp))
                        {
                            node.StepValue = temp;
                        }
                        else
                        {
                            parser.ReportSyntaxError("Invalid step value found.");
                        }
                    }
                    else
                    {
                        parser.ReportSyntaxError("Invalid step value found.");
                    }
                }

                while (!parser.PeekToken(TokenKind.EndOfFile) &&
                      !(parser.PeekToken(TokenKind.EndKeyword) && parser.PeekToken(TokenKind.ForKeyword, 2)))
                {
                    FglStatement statement;
                    if (parser.StatementFactory.TryParseNode(parser, out statement, prepStatementResolver, prepStatementBinder))
                    {
                        AstNode stmtNode = statement as AstNode;
                        node.Children.Add(stmtNode.StartIndex, stmtNode);

                        if (statement is ExitStatement &&
                           (statement as ExitStatement).ExitType != TokenKind.ForKeyword)
                        {
                            parser.ReportSyntaxError("Invalid exit statement for for loop detected.");
                        }

                        if (statement is ContinueStatement &&
                           (statement as ContinueStatement).ContinueType != TokenKind.ForKeyword)
                        {
                            parser.ReportSyntaxError("Invalid continue statement for for loop detected.");
                        }
                    }
                    else
                    {
                        parser.NextToken();
                    }
                }

                if (!(parser.PeekToken(TokenKind.EndKeyword) && parser.PeekToken(TokenKind.ForKeyword, 2)))
                {
                    parser.ReportSyntaxError("A for statement must be terminated with \"end for\".");
                }
                else
                {
                    parser.NextToken(); // advance to the 'end' token
                    parser.NextToken(); // advance to the 'for' token
                    node.EndIndex = parser.Token.Span.End;
                }
            }

            return result;
        }
    }
}