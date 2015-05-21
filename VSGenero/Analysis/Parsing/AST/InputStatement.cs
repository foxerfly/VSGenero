﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VSGenero.Analysis.Parsing.AST
{
    public class InputBlock : FglStatement, IOutlinableResult
    {
        public bool IsArray { get; private set; }
        public NameExpression ArrayName { get; private set; }
        public bool IsImplicitMapping { get; private set; }
        public List<NameExpression> VariableList { get; private set; }
        public List<NameExpression> FieldList { get; private set; }
        public List<InputAttribute> Attributes { get; private set; }
        public ExpressionNode HelpNumber { get; private set; }

        public static bool TryParseNode(Parser parser, out InputBlock node,
                                 Func<string, PrepareStatement> prepStatementResolver = null,
                                 Action<PrepareStatement> prepStatementBinder = null,
                                 List<TokenKind> validExitKeywords = null)
        {
            node = null;
            bool result = false;

            if(parser.PeekToken(TokenKind.InputKeyword))
            {
                result = true;
                node = new InputBlock();
                parser.NextToken();
                node.StartIndex = parser.Token.Span.Start;
                node.VariableList = new List<NameExpression>();
                node.FieldList = new List<NameExpression>();
                node.Attributes = new List<InputAttribute>();

                if(parser.PeekToken(TokenKind.ByKeyword))
                {
                    parser.NextToken();
                    if (parser.PeekToken(TokenKind.NameKeyword))
                    {
                        parser.NextToken();

                        // Implicit field mapping
                        node.IsImplicitMapping = true;
                    }
                    else
                        parser.ReportSyntaxError("Expected \"name\" token in input statement.");
                }
                else if(parser.PeekToken(TokenKind.ArrayKeyword))
                {
                    parser.NextToken();
                    node.IsArray = true;
                    NameExpression arrName;
                    if (NameExpression.TryParseNode(parser, out arrName))
                        node.ArrayName = arrName;
                    else
                        parser.ReportSyntaxError("Invalid array name found in input statement.");
                }

                node.DecoratorEnd = parser.Token.Span.End;

                NameExpression nameExpr;
                if (!node.IsArray)
                {
                    // read the variable list
                    while (NameExpression.TryParseNode(parser, out nameExpr))
                    {
                        node.VariableList.Add(nameExpr);
                        if (!parser.PeekToken(TokenKind.Comma))
                            break;
                        parser.NextToken();
                    }
                } 

                if (parser.PeekToken(TokenKind.WithoutKeyword))
                {
                    parser.NextToken();
                    if (parser.PeekToken(TokenKind.DefaultsKeyword))
                    {
                        parser.NextToken();
                    }
                    else
                        parser.ReportSyntaxError("Expected \"defaults\" token in input statement.");
                }

                if(!node.IsImplicitMapping || node.IsArray)
                {
                    if(parser.PeekToken(TokenKind.FromKeyword))
                    {
                        parser.NextToken();

                        // read the field list
                        while (NameExpression.TryParseNode(parser, out nameExpr))
                        {
                            node.FieldList.Add(nameExpr);
                            if (node.IsArray || !parser.PeekToken(TokenKind.Comma))
                                break;
                            parser.NextToken();
                        }
                    }
                    else
                        parser.ReportSyntaxError("Expected \"from\" token in input statement.");
                }

                if(parser.PeekToken(TokenKind.AttributesKeyword) || parser.PeekToken(TokenKind.AttributeKeyword))
                {
                    parser.NextToken();
                    if (parser.PeekToken(TokenKind.LeftParenthesis))
                    {
                        parser.NextToken();

                        // get the list of display or control attributes
                        InputAttribute attrib;
                        while (InputAttribute.TryParseNode(parser, out attrib, node.IsArray))
                        {
                            node.Attributes.Add(attrib);
                            if (!parser.PeekToken(TokenKind.Comma))
                                break;
                            parser.NextToken();
                        }

                        if (parser.PeekToken(TokenKind.RightParenthesis))
                            parser.NextToken();
                        else
                            parser.ReportSyntaxError("Expecting right-paren in input attributes section.");
                    }
                    else
                        parser.ReportSyntaxError("Expecting left-paren in input attributes section.");
                }

                if(parser.PeekToken(TokenKind.HelpKeyword))
                {
                    parser.NextToken();

                    // get the help number
                    ExpressionNode optionNumber;
                    if (ExpressionNode.TryGetExpressionNode(parser, out optionNumber))
                        node.HelpNumber = optionNumber;
                    else
                        parser.ReportSyntaxError("Invalid help-number found in input statement.");
                }

                List<TokenKind> validExits = new List<TokenKind>();
                if (validExitKeywords != null)
                    validExits.AddRange(validExits);
                validExits.Add(TokenKind.InputKeyword);

                // get the dialog control blocks
                while (!parser.PeekToken(TokenKind.EndOfFile) &&
                       !(parser.PeekToken(TokenKind.EndKeyword) && parser.PeekToken(TokenKind.InputKeyword, 2)))
                {
                    InputControlBlock icb;
                    if (InputControlBlock.TryParseNode(parser, out icb, node.IsArray, prepStatementResolver, prepStatementBinder, validExits))
                        node.Children.Add(icb.StartIndex, icb);
                    else
                        parser.NextToken();
                }

                if (!(parser.PeekToken(TokenKind.EndKeyword) && parser.PeekToken(TokenKind.InputKeyword, 2)))
                {
                    parser.ReportSyntaxError("A input block must be terminated with \"end input\".");
                }
                else
                {
                    parser.NextToken(); // advance to the 'end' token
                    parser.NextToken(); // advance to the 'input' token
                    node.EndIndex = parser.Token.Span.End;
                }
            }

            return result;
        }

        public bool CanOutline
        {
            get { return true; }
        }

        public int DecoratorStart
        {
            get
            {
                return StartIndex;
            }
            set
            {
            }
        }

        public int DecoratorEnd { get; set; }
    }

    public enum InputControlBlockType
    {
        None,
        Field,
        Input,
        Delete,
        Row,
        Change,
        Idle,
        Action,
        Key,
        Insert
    }

    public class InputControlBlock : AstNode
    {
        public ExpressionNode IdleSeconds { get; private set; }
        public NameExpression ActionName { get; private set; }
        public NameExpression ActionField { get; private set; }

        public List<NameExpression> FieldSpecList { get; private set; }
        public List<NameExpression> KeyNameList { get; private set; }

        public InputControlBlockType Type { get; private set; }

        public static bool TryParseNode(Parser parser, out InputControlBlock node,
                                 bool isArray = false,
                                 Func<string, PrepareStatement> prepStatementResolver = null,
                                 Action<PrepareStatement> prepStatementBinder = null,
                                 List<TokenKind> validExitKeywords = null)
        {
            node = new InputControlBlock();
            bool result = true;
            node.StartIndex = parser.Token.Span.Start;
            node.FieldSpecList = new List<NameExpression>();
            node.KeyNameList = new List<NameExpression>();

            switch(parser.PeekToken().Kind)
            {
                case TokenKind.BeforeKeyword:
                case TokenKind.AfterKeyword:
                    {
                        parser.NextToken();
                        if (parser.PeekToken(TokenKind.FieldKeyword))
                        {
                            parser.NextToken();
                            node.Type = InputControlBlockType.Field;
                            // get the list of field specs
                            NameExpression fieldSpec;
                            while (NameExpression.TryParseNode(parser, out fieldSpec))
                            {
                                node.FieldSpecList.Add(fieldSpec);
                                if (!parser.PeekToken(TokenKind.Comma))
                                    break;
                                parser.NextToken();
                            }
                        }
                        else if (parser.PeekToken(TokenKind.InputKeyword))
                        {
                            parser.NextToken();
                            node.Type = InputControlBlockType.Input;
                        }
                        else if(isArray &&
                                parser.PeekToken(TokenKind.DeleteKeyword))
                        {
                            parser.NextToken();
                            node.Type = InputControlBlockType.Delete;
                        }
                        else if(isArray && parser.PeekToken(TokenKind.RowKeyword))
                        {
                            parser.NextToken();
                            node.Type = InputControlBlockType.Row;
                        }
                        else if(isArray && parser.PeekToken(TokenKind.InsertKeyword))
                        {
                            parser.NextToken();
                            node.Type = InputControlBlockType.Insert;
                        }
                        else
                        {
                            parser.ReportSyntaxError("Unexpected token found in input control block.");
                            result = false;
                        }
                        break;
                    }
                case TokenKind.OnKeyword:
                    {
                        parser.NextToken();
                        switch(parser.PeekToken().Kind)
                        {
                            case TokenKind.ChangeKeyword:
                                parser.NextToken();
                                node.Type = InputControlBlockType.Change;
                                // get the list of field specs
                                NameExpression fieldSpec;
                                while (NameExpression.TryParseNode(parser, out fieldSpec))
                                {
                                    node.FieldSpecList.Add(fieldSpec);
                                    if (!parser.PeekToken(TokenKind.Comma))
                                        break;
                                    parser.NextToken();
                                }
                                break;
                            case TokenKind.IdleKeyword:
                                parser.NextToken();
                                node.Type = InputControlBlockType.Idle;
                                // get the idle seconds
                                ExpressionNode idleExpr;
                                if (ExpressionNode.TryGetExpressionNode(parser, out idleExpr))
                                    node.IdleSeconds = idleExpr;
                                else
                                    parser.ReportSyntaxError("Invalid idle-seconds found in input statement.");
                                break;
                            case TokenKind.ActionKeyword:
                                parser.NextToken();
                                node.Type = InputControlBlockType.Action;
                                // get the action name
                                NameExpression actionName;
                                if (NameExpression.TryParseNode(parser, out actionName))
                                    node.ActionName = actionName;
                                else
                                    parser.ReportSyntaxError("Invalid action-name found in input statement.");
                                if(parser.PeekToken(TokenKind.InfieldKeyword))
                                {
                                    parser.NextToken();
                                    // get the field-spec
                                    if (NameExpression.TryParseNode(parser, out actionName))
                                        node.ActionField = actionName;
                                    else
                                        parser.ReportSyntaxError("Invalid field-spec found in input statement.");
                                }
                                break;
                            case TokenKind.KeyKeyword:
                                parser.NextToken();
                                node.Type = InputControlBlockType.Key;
                                if (parser.PeekToken(TokenKind.LeftParenthesis))
                                {
                                    parser.NextToken();
                                    // get the list of key names
                                    NameExpression keyName;
                                    while (NameExpression.TryParseNode(parser, out keyName))
                                    {
                                        node.KeyNameList.Add(keyName);
                                        if (!parser.PeekToken(TokenKind.Comma))
                                            break;
                                        parser.NextToken();
                                    }
                                    if (parser.PeekToken(TokenKind.RightParenthesis))
                                        parser.NextToken();
                                    else
                                        parser.ReportSyntaxError("Expected right-paren in input control block.");
                                }
                                else
                                    parser.ReportSyntaxError("Expected left-paren in input control block.");
                                break;
                            case TokenKind.RowKeyword:
                                if (isArray)
                                {
                                    parser.NextToken();
                                    node.Type = InputControlBlockType.Row;
                                    if (parser.PeekToken(TokenKind.ChangeKeyword))
                                        parser.NextToken();
                                    else
                                        parser.ReportSyntaxError("Expected \"change\" keyword in input statement.");
                                }
                                else
                                    parser.ReportSyntaxError("\"on row\" syntax is only allowed in input array statement.");
                                break;

                            default:
                                parser.ReportSyntaxError("Unexpected token found in input control block.");
                                //result = false;
                                break;
                        }
                        break;
                    }
                default:
                    //result = false;
                    break;
            }

            if(result)
            {
                // get the dialog statements
                FglStatement inputStmt;
                while (InputDialogStatementFactory.TryGetStatement(parser, out inputStmt, isArray, prepStatementResolver, prepStatementBinder, validExitKeywords))
                    node.Children.Add(inputStmt.StartIndex, inputStmt);

                if (node.Type == InputControlBlockType.None && node.Children.Count == 0)
                    result = false;
            }

            return result;
        }
    }

    public class InputDialogStatementFactory
    {
        public static bool TryGetStatement(Parser parser, out FglStatement node, bool isArray,
                                 Func<string, PrepareStatement> prepStatementResolver = null,
                                 Action<PrepareStatement> prepStatementBinder = null,
                                 List<TokenKind> validExitKeywords = null)
        {
            bool result = false;
            node = null;

            InputDialogStatement inputStmt;
            if ((result = InputDialogStatement.TryParseNode(parser, out inputStmt, isArray)))
            {
                node = inputStmt;
            }
            else
            {
                result = parser.StatementFactory.TryParseNode(parser, out node, prepStatementResolver, prepStatementBinder, false, validExitKeywords);
            }

            return result;
        }
    }

    public class InputDialogStatement : FglStatement
    {
        public NameExpression FieldSpec { get; private set; }

        public static bool TryParseNode(Parser parser, out InputDialogStatement node, bool isArray)
        {
            node = new InputDialogStatement();
            bool result = true;

            switch(parser.PeekToken().Kind)
            {
                case TokenKind.AcceptKeyword:
                case TokenKind.ContinueKeyword:
                case TokenKind.ExitKeyword:
                    {
                        if (parser.PeekToken(TokenKind.InputKeyword, 2))
                        {
                            parser.NextToken();
                            parser.NextToken();
                        }
                        else
                            result = false;
                        break;
                    }
                case TokenKind.NextKeyword:
                    {
                        parser.NextToken();
                        if (parser.PeekToken(TokenKind.FieldKeyword))
                        {
                            parser.NextToken();
                            switch(parser.PeekToken().Kind)
                            {
                                case TokenKind.CurrentKeyword:
                                case TokenKind.NextKeyword:
                                case TokenKind.PreviousKeyword:
                                    parser.NextToken();
                                    break;
                                default:
                                    {
                                        // get the field-spec
                                        NameExpression fieldSpec;
                                        if (NameExpression.TryParseNode(parser, out fieldSpec))
                                            node.FieldSpec = fieldSpec;
                                        else
                                            parser.ReportSyntaxError("Invalid field-spec found in input statement.");
                                        break;
                                    }
                            }
                        }
                        else
                            parser.ReportSyntaxError("Expecting \"field\" keyword in input statement.");
                        break;
                    }
                case TokenKind.CancelKeyword:
                    {
                        if (isArray)
                        {
                            parser.NextToken();
                            if (parser.PeekToken(TokenKind.DeleteKeyword) || parser.PeekToken(TokenKind.InsertKeyword))
                                parser.NextToken();
                            else
                                parser.ReportSyntaxError("Expected \"delete\" or \"insert\" keyword in input statement.");
                        }
                        else
                            parser.ReportSyntaxError("Keyword \"cancel\" can only exist in an input array statement.");
                        break;
                    }
                default:
                    {
                        result = false;
                        break;
                    }
            }

            return result;
        }
    }

    public class InputAttribute : AstNode
    {
        public static bool TryParseNode(Parser parser, out InputAttribute node, bool isArray)
        {
            node = new InputAttribute();
            node.StartIndex = parser.Token.Span.Start;
            bool result = true;

            switch(parser.PeekToken().Kind)
            {
                case TokenKind.BlackKeyword:
                case TokenKind.BlueKeyword:
                case TokenKind.CyanKeyword:
                case TokenKind.GreenKeyword:
                case TokenKind.MagentaKeyword:
                case TokenKind.RedKeyword:
                case TokenKind.WhiteKeyword:
                case TokenKind.YellowKeyword:
                case TokenKind.BoldKeyword:
                case TokenKind.DimKeyword:
                case TokenKind.InvisibleKeyword:
                case TokenKind.NormalKeyword:
                case TokenKind.ReverseKeyword:
                case TokenKind.BlinkKeyword:
                case TokenKind.UnderlineKeyword:
                    parser.NextToken();
                    break;
                case TokenKind.AcceptKeyword:
                case TokenKind.CancelKeyword:
                case TokenKind.UnbufferedKeyword:
                    {
                        parser.NextToken();
                        if(parser.PeekToken(TokenKind.Equals))
                        {
                            parser.NextToken();
                            ExpressionNode boolExpr;
                            if (!ExpressionNode.TryGetExpressionNode(parser, out boolExpr, new List<TokenKind> { TokenKind.Comma, TokenKind.RightParenthesis }))
                                parser.ReportSyntaxError("Invalid boolean expression found in input attribute.");
                        }
                        break;
                    }
                case TokenKind.CountKeyword:
                case TokenKind.MaxCountKeyword:
                    {
                        if (isArray)
                        {
                            parser.NextToken();
                            if (parser.PeekToken(TokenKind.Equals))
                            {
                                parser.NextToken();
                                ExpressionNode boolExpr;
                                if (!ExpressionNode.TryGetExpressionNode(parser, out boolExpr, new List<TokenKind> { TokenKind.Comma, TokenKind.RightParenthesis }))
                                    parser.ReportSyntaxError("Invalid expression found in input attribute.");
                            }
                            else
                                parser.ReportSyntaxError("Expected integer expression in input array attribute.");
                        }
                        else
                            parser.ReportSyntaxError("Attribute can only be used for an input array statement.");
                        break;
                    }
                case TokenKind.WithoutKeyword:
                    {
                        parser.NextToken();
                        if (parser.PeekToken(TokenKind.DefaultsKeyword))
                            parser.NextToken();
                        else
                            parser.ReportSyntaxError("Expected \"defaults\" keyword in input attribute.");
                        if (parser.PeekToken(TokenKind.Equals))
                        {
                            parser.NextToken();
                            ExpressionNode boolExpr;
                            if (!ExpressionNode.TryGetExpressionNode(parser, out boolExpr, new List<TokenKind> { TokenKind.Comma, TokenKind.RightParenthesis }))
                                parser.ReportSyntaxError("Invalid boolean expression found in input attribute.");
                        }
                        break;
                    }
                case TokenKind.HelpKeyword:
                    {
                        parser.NextToken();
                        if (parser.PeekToken(TokenKind.Equals))
                            parser.NextToken();
                        else
                            parser.ReportSyntaxError("Expected equals token in input attribute.");

                        // get the help number
                        ExpressionNode optionNumber;
                        if (!ExpressionNode.TryGetExpressionNode(parser, out optionNumber))
                            parser.ReportSyntaxError("Invalid help-number found in input attribute.");
                        break;
                    }
                case TokenKind.NameKeyword:
                    {
                        if (!isArray)
                        {
                            parser.NextToken();
                            if (parser.PeekToken(TokenKind.Equals))
                                parser.NextToken();
                            else
                                parser.ReportSyntaxError("Expected equals token in input attribute.");

                            // get the help number
                            ExpressionNode optionNumber;
                            if (!ExpressionNode.TryGetExpressionNode(parser, out optionNumber))
                                parser.ReportSyntaxError("Invalid dialog name found in input attribute.");
                        }
                        else
                            parser.ReportSyntaxError("The name attribute can only be used for an input statement (not an input array statement).");
                        break;
                    }
                case TokenKind.FieldKeyword:
                    {
                        parser.NextToken();
                        if (parser.PeekToken(TokenKind.OrderKeyword))
                            parser.NextToken();
                        else
                            parser.ReportSyntaxError("Expected \"order\" keyword in input attribute.");
                        if (parser.PeekToken(TokenKind.FormKeyword))
                            parser.NextToken();
                        else
                            parser.ReportSyntaxError("Expected \"form\" keyword in input attribute.");
                        break;
                    }
                case TokenKind.AppendKeyword:
                case TokenKind.DeleteKeyword:
                case TokenKind.InsertKeyword:
                    {
                        parser.NextToken();
                        if(parser.PeekToken(TokenKind.RowKeyword))
                        {
                            parser.NextToken();
                            if (parser.PeekToken(TokenKind.Equals))
                            {
                                parser.NextToken();
                                ExpressionNode boolExpr;
                                if (!ExpressionNode.TryGetExpressionNode(parser, out boolExpr, new List<TokenKind> { TokenKind.Comma, TokenKind.RightParenthesis }))
                                    parser.ReportSyntaxError("Invalid boolean expression found in input array attribute.");
                            }
                        }
                        else
                            parser.ReportSyntaxError("Expected \"row\" keyword in input array attribute.");
                        break;
                    }
                case TokenKind.KeepKeyword:
                    {
                        parser.NextToken();
                        if (parser.PeekToken(TokenKind.CurrentKeyword))
                        {
                            parser.NextToken();
                            if (parser.PeekToken(TokenKind.RowKeyword))
                            {
                                parser.NextToken();
                                if (parser.PeekToken(TokenKind.Equals))
                                {
                                    parser.NextToken();
                                    ExpressionNode boolExpr;
                                    if (!ExpressionNode.TryGetExpressionNode(parser, out boolExpr, new List<TokenKind> { TokenKind.Comma, TokenKind.RightParenthesis }))
                                        parser.ReportSyntaxError("Invalid boolean expression found in input array attribute.");
                                }
                            }
                            else
                                parser.ReportSyntaxError("Expected \"row\" keyword in input array attribute.");
                        }
                        else
                            parser.ReportSyntaxError("Expected \"current\" keyword in input array attribute.");
                        break;
                    }
                case TokenKind.AutoKeyword:
                    {
                        parser.NextToken();
                        if (parser.PeekToken(TokenKind.AppendKeyword))
                        {
                            parser.NextToken();
                            if (parser.PeekToken(TokenKind.Equals))
                            {
                                parser.NextToken();
                                ExpressionNode boolExpr;
                                if (!ExpressionNode.TryGetExpressionNode(parser, out boolExpr, new List<TokenKind> { TokenKind.Comma, TokenKind.RightParenthesis }))
                                    parser.ReportSyntaxError("Invalid boolean expression found in input array attribute.");
                            }
                        }
                        else
                            parser.ReportSyntaxError("Expected \"append\" keyword in input array attribute.");
                        break;
                    }
                default:
                    result = false;
                    break;
            }

            return result;
        }
    }
}
