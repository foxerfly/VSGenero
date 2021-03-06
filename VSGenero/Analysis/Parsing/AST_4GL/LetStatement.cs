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

using Microsoft.VisualStudio.Text;
using VSGenero.EditorExtensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VSGenero.Analysis.Parsing.AST_4GL
{
    /// <summary>
    /// LET target = expr [,...]
    /// 
    /// For more info, see: http://www.4js.com/online_documentation/fjs-fgl-manual-html/index.html#c_fgl_variables_LET.html
    /// </summary>
    public class LetStatement : FglStatement
    {
        public FglNameExpression Variable { get; private set; }

        public string GetLiteralValue()
        {
            if(Children.Count == 1)
            {
                ExpressionNode strExpr = Children[Children.Keys[0]] as ExpressionNode;
                if(strExpr != null)
                {
                    StringBuilder sb = new StringBuilder();
                    // TODO: come up with a way of formatting a sql statement that has genero code mixed in...
                    foreach (var line in strExpr.ToString().SplitToLines(80))
                        sb.AppendLine(line);
                    return sb.ToString();
                }
                else
                {
                    return "(unable to evaluate expression)";
                }
            }
            else
            {
                return "";
            }
        }

        public static bool TryParseNode(Genero4glParser parser, out LetStatement defNode, ExpressionParsingOptions expressionOptions = null)
        {
            defNode = null;
            bool result = false;

            if (parser.PeekToken(TokenKind.LetKeyword))
            {
                result = true;
                defNode = new LetStatement();
                parser.NextToken();
                defNode.StartIndex = parser.Token.Span.Start;

                FglNameExpression name;
                if (!FglNameExpression.TryParseNode(parser, out name, TokenKind.Equals))
                {
                    parser.ReportSyntaxError("Unexpected token found in let statement, expecting name expression.");
                }
                else
                {
                    defNode.Variable = name;
                }

                if (!parser.PeekToken(TokenKind.Equals))
                {
                    parser.ReportSyntaxError("Assignment statement is missing an assignment operator.");
                }
                else
                {
                    parser.NextToken();

                    // get the expression(s)
                    ExpressionNode mainExpression = null;
                    while (true)
                    {
                        ExpressionNode expr;
                        if (!FglExpressionNode.TryGetExpressionNode(parser, out expr, null, expressionOptions))
                        {
                            parser.ReportSyntaxError("Assignment statement must have one or more comma-separated expressions.");
                            break;
                        }
                        if (mainExpression == null)
                        {
                            mainExpression = expr;
                        }
                        else
                        {
                            mainExpression.AppendExpression(expr);
                        }

                        if (!parser.PeekToken(TokenKind.Comma))
                        {
                            break;
                        }
                        parser.NextToken();
                    }

                    if (mainExpression != null)
                    {
                        defNode.Children.Add(mainExpression.StartIndex, mainExpression);
                        defNode.EndIndex = mainExpression.EndIndex;
                        defNode.IsComplete = true;
                    }
                }
            }

            return result;
        }

        public override void CheckForErrors(GeneroAst ast, Action<string, int, int> errorFunc,
                                            Dictionary<string, List<int>> deferredFunctionSearches, 
                                            FunctionProviderSearchMode searchInFunctionProvider = FunctionProviderSearchMode.NoSearch, bool isFunctionCallOrDefinition = false)
        {
            // check the variable
            if (Variable != null)
                Variable.CheckForErrors(ast, errorFunc, deferredFunctionSearches);

            // check the expression
            base.CheckForErrors(ast, errorFunc, deferredFunctionSearches, searchInFunctionProvider, isFunctionCallOrDefinition);
        }
    }
}
