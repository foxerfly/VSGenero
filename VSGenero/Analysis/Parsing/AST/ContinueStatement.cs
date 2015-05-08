﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VSGenero.Analysis.Parsing.AST
{
    public class ContinueStatement : FglStatement
    {
        public TokenKind ContinueType { get; private set; }

        public static bool TryParseNode(Parser parser, out ContinueStatement node)
        {
            node = null;
            bool result = false;

            if (parser.PeekToken(TokenKind.ContinueKeyword))
            {
                result = true;
                node = new ContinueStatement();
                parser.NextToken();
                node.StartIndex = parser.Token.Span.Start;

                TokenKind tokKind = parser.PeekToken().Kind;
                switch (tokKind)
                {
                    case TokenKind.ForKeyword:
                    case TokenKind.ForeachKeyword:
                    case TokenKind.WhileKeyword:
                    case TokenKind.MenuKeyword:
                    case TokenKind.ConstructKeyword:
                    case TokenKind.InputKeyword:
                    case TokenKind.DialogKeyword:
                        node.ContinueType = tokKind;
                        parser.NextToken();
                        node.EndIndex = parser.Token.Span.End;
                        break;
                    default:
                        parser.ReportSyntaxError("Continue statement must be of form: continue { FOR | FOREACH | WHILE | MENU | CONSTRUCT | INPUT | DIALOG }");
                        break;
                }
            }

            return result;
        }
    }
}
