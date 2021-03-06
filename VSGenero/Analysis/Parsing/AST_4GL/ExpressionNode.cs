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
    public delegate ExpressionNode ExpressionParser(IParser parser);

    public class ExpressionParsingOptions
    {
        public bool AllowStarParam { get; set; }
        public bool AllowAnythingForFunctionParams { get; set; }
        public bool AllowQuestionMark { get; set; }
        public bool AllowNestedSelectStatement { get; set; }
        public IEnumerable<ExpressionParser> AdditionalExpressionParsers { get; set; }
    }

    public abstract class FglExpressionNode : ExpressionNode
    {
        protected static List<TokenKind> _preExpressionTokens = new List<TokenKind> 
        { 
            TokenKind.NotKeyword, TokenKind.ColumnKeyword, TokenKind.Subtract, TokenKind.AsciiKeyword, TokenKind.Add
        };

        public static bool TryGetExpressionNode(IParser parser, out ExpressionNode node, List<TokenKind> breakTokens = null, ExpressionParsingOptions options = null)
        {
            if (options == null)
                options = new ExpressionParsingOptions();
            node = null;
            bool result = false;
            bool start = true;
            bool requireExpression = false;

            TokenExpressionNode startingToken = null;

            while (true)
            {
                // First check for allowed pre-expression tokens
                if (_preExpressionTokens.Contains(parser.PeekToken().Kind))
                {
                    parser.NextToken();
                    if (node == null)
                        node = new TokenExpressionNode(parser.Token);
                    else
                        node.AppendExpression(new TokenExpressionNode(parser.Token));
                    requireExpression = true;
                }

                if (parser.PeekToken(TokenKind.LeftParenthesis))
                {
                    ParenWrappedExpressionNode parenExpr;
                    if (ParenWrappedExpressionNode.TryParseExpression(parser, out parenExpr, options))
                    {
                        if (node == null)
                            node = parenExpr;
                        else
                            node.AppendExpression(parenExpr);
                        result = true;
                    }
                    else
                    {
                        parser.ReportSyntaxError("Paren-nested expression expected.");
                    }
                }
                else if (parser.PeekToken(TokenKind.LeftBracket))
                {
                    BracketWrappedExpressionNode brackNode;
                    if (BracketWrappedExpressionNode.TryParseNode(parser, out brackNode))
                    {
                        if (node == null)
                            node = brackNode;
                        else
                            node.AppendExpression(brackNode);
                        result = true;
                    }
                    else
                    {
                        parser.ReportSyntaxError("Bracket-nested expression expected.");
                    }
                }
                else if (parser.PeekToken(TokenCategory.StringLiteral) ||
                         parser.PeekToken(TokenCategory.CharacterLiteral) ||
                         parser.PeekToken(TokenCategory.IncompleteMultiLineStringLiteral))
                {
                    result = true;
                    parser.NextToken();
                    if (node == null)
                        node = new StringExpressionNode(parser.Token);
                    else
                        node.AppendExpression(new StringExpressionNode(parser.Token));
                }
                else if (parser.PeekToken(TokenCategory.NumericLiteral))
                {
                    result = true;
                    parser.NextToken();
                    if (node == null)
                        node = new TokenExpressionNode(parser.Token);
                    else
                        node.AppendExpression(new TokenExpressionNode(parser.Token));
                }
                else if (parser.PeekToken(TokenKind.CurrentKeyword))
                {
                    result = true;
                    parser.NextToken();
                    int currentExprStart = parser.Token.Span.Start;
                    string currentTypeConstraint;
                    if (TypeConstraints.VerifyValidConstraint(parser, out currentTypeConstraint, TokenKind.CurrentKeyword, true))
                    {
                        result = true;
                        StringExpressionNode strExpr = new StringExpressionNode(string.Format("current {0}", currentTypeConstraint));
                        strExpr.StartIndex = currentExprStart;
                        strExpr.EndIndex = parser.Token.Span.End;
                        if (node == null)
                            node = strExpr;
                        else
                            node.AppendExpression(strExpr);
                    }
                    else
                    {
                        if (node == null)
                            node = new TokenExpressionNode(parser.Token);
                        else
                            node.AppendExpression(new TokenExpressionNode(parser.Token));
                    }
                }
                else if (parser.PeekToken(TokenKind.IntervalKeyword))
                {
                    parser.NextToken();
                    TokenExpressionNode intervalNode = new TokenExpressionNode(parser.Token);
                    if (node == null)
                        node = intervalNode;
                    else
                        node.AppendExpression(intervalNode);
                    if (parser.PeekToken(TokenKind.LeftParenthesis))
                    {
                        parser.NextToken();
                        node.AppendExpression(new TokenExpressionNode(parser.Token));
                        while (!parser.PeekToken(TokenKind.EndOfFile))
                        {
                            parser.NextToken();
                            node.AppendExpression(new TokenExpressionNode(parser.Token));
                            if (parser.Token.Token.Kind == TokenKind.RightParenthesis)
                                break;
                        }
                        string intervalString;
                        if (TypeConstraints.VerifyValidConstraint(parser, out intervalString, TokenKind.IntervalKeyword, true))
                        {
                            result = true;
                            node.AppendExpression(new StringExpressionNode(intervalString));
                        }
                        else
                            parser.ReportSyntaxError("Invalid interval expression found.");
                    }
                }
                else if (parser.PeekToken(TokenKind.SelectKeyword) && options.AllowNestedSelectStatement)
                {
                    FglStatement selStmt;
                    bool dummy;
                    if (SqlStatementFactory.TryParseSqlStatement(parser, out selStmt, out dummy))
                    {
                        result = true;
                        if (node == null)
                            node = new FglStatementExpression(selStmt);
                        else
                            node.AppendExpression(new FglStatementExpression(selStmt));
                    }
                    else
                        parser.ReportSyntaxError("Invalid select statement found in expression.");
                }
                else if (parser.PeekToken(TokenCategory.Identifier) ||
                        parser.PeekToken(TokenCategory.Keyword))
                {
                    bool isCustomExpression = false;
                    if (options != null &&
                        options.AdditionalExpressionParsers != null)
                    {
                        ExpressionNode parsedExpr;
                        foreach (var exprParser in options.AdditionalExpressionParsers)
                        {
                            if ((parsedExpr = exprParser(parser)) != null)
                            {
                                result = true;
                                if (node == null)
                                    node = parsedExpr;
                                else
                                    node.AppendExpression(parsedExpr);
                                isCustomExpression = true;
                            }
                        }
                    }
                    if (!isCustomExpression)
                    {
                        FunctionCallExpressionNode funcCall;
                        FglNameExpression nonFuncCallName;
                        if (FunctionCallExpressionNode.TryParseExpression(parser, out funcCall, out nonFuncCallName, false, options))
                        {
                            result = true;
                            if (node == null)
                                node = funcCall;
                            else
                                node.AppendExpression(funcCall);
                        }
                        else if (nonFuncCallName != null)
                        {
                            bool isDatetime = false;
                            var dtToken = Tokens.GetToken(nonFuncCallName.Name);
                            if (dtToken != null)
                            {
                                if (TypeConstraints.DateTimeQualifiers.Contains(dtToken.Kind))
                                {
                                    string dtString;
                                    isDatetime = true;
                                    if (TypeConstraints.VerifyValidConstraint(parser, out dtString, TokenKind.DatetimeKeyword, true, dtToken.Kind))
                                    {
                                        result = true;
                                        var strExpr = new StringExpressionNode(dtString);
                                        if (node == null)
                                            node = strExpr;
                                        else
                                            node.AppendExpression(strExpr);
                                    }
                                    else
                                    {
                                        isDatetime = false;
                                    }
                                }
                            }

                            if (!isDatetime)
                            {
                                // it's a name expression
                                result = true;
                                if (node == null)
                                    node = nonFuncCallName;
                                else
                                    node.AppendExpression(nonFuncCallName);
                            }
                        }
                        else
                        {
                            result = true;
                            parser.NextToken();
                            if (node == null)
                                node = new TokenExpressionNode(parser.Token);
                            else
                                node.AppendExpression(new TokenExpressionNode(parser.Token));
                        }
                    }
                }
                else if (parser.PeekToken(TokenKind.Multiply) && options.AllowStarParam)
                {
                    result = true;
                    parser.NextToken();
                    if (node == null)
                        node = new TokenExpressionNode(parser.Token);
                    else
                        node.AppendExpression(new TokenExpressionNode(parser.Token));
                }
                else if (parser.PeekToken(TokenKind.QuestionMark) && options.AllowQuestionMark)
                {
                    parser.NextToken();
                    if (node == null)
                        parser.ReportSyntaxError("Invalid token '?' found in expression.");
                    else
                        node.AppendExpression(new TokenExpressionNode(parser.Token));
                }
                else
                {
                    if (requireExpression)
                    {
                        var tok = parser.PeekToken();
                        if (breakTokens != null && !breakTokens.Contains(tok.Kind))
                            parser.ReportSyntaxError("Invalid token type found in expression.");
                        else
                            parser.ReportSyntaxError("Expression required.");
                    }
                    break;
                }
                requireExpression = false;

                Token nextTok = parser.PeekToken();
                bool isOperator = true;
                while (isOperator && !requireExpression)
                {
                    if ((breakTokens == null ||
                         (breakTokens != null && !breakTokens.Contains(nextTok.Kind))) &&
                        nextTok.Kind >= TokenKind.FirstOperator &&
                        nextTok.Kind <= TokenKind.LastOperator)
                    {
                        parser.NextToken();
                        // TODO: not sure if we want to do more analysis on what operators can start an expression
                        if (node == null)
                            node = new TokenExpressionNode(parser.Token);
                        else
                            node.AppendExpression(new TokenExpressionNode(parser.Token));

                        switch (parser.Token.Token.Kind)
                        {
                            case TokenKind.LessThan:
                                // check for '<=' or '<>'
                                if (parser.PeekToken(TokenKind.Equals) ||
                                   parser.PeekToken(TokenKind.GreaterThan))
                                {
                                    parser.NextToken();
                                    node.AppendExpression(new TokenExpressionNode(parser.Token));
                                }
                                break;
                            case TokenKind.GreaterThan:
                                // check for '>='
                                if (parser.PeekToken(TokenKind.Equals))
                                {
                                    parser.NextToken();
                                    node.AppendExpression(new TokenExpressionNode(parser.Token));
                                }
                                break;
                            case TokenKind.Exclamation:
                                // check for '!='
                                if (parser.PeekToken(TokenKind.Equals))
                                {
                                    parser.NextToken();
                                    node.AppendExpression(new TokenExpressionNode(parser.Token));
                                }
                                else
                                {
                                    parser.ReportSyntaxError("Invalid token '!' found in expression.");
                                }
                                break;
                            case TokenKind.Equals:
                                // check for '=='
                                if (parser.PeekToken(TokenKind.Equals))
                                {
                                    parser.NextToken();
                                    node.AppendExpression(new TokenExpressionNode(parser.Token));
                                }
                                break;
                            case TokenKind.SingleBar:
                                //  check for '||'
                                if (parser.PeekToken(TokenKind.SingleBar))
                                {
                                    parser.NextToken();
                                    node.AppendExpression(new TokenExpressionNode(parser.Token));
                                }
                                else
                                {
                                    parser.ReportSyntaxError("Invalid token '|' found in expression.");
                                }
                                break;
                        }
                        requireExpression = true;
                    }
                    else
                    {
                        // check for non-symbol operators
                        switch (nextTok.Kind)
                        {
                            case TokenKind.DoubleBar:
                            case TokenKind.AsKeyword:
                            case TokenKind.AndKeyword:
                            case TokenKind.OrKeyword:
                            case TokenKind.ModKeyword:
                            case TokenKind.UsingKeyword:
                            case TokenKind.InstanceOfKeyword:
                            case TokenKind.UnitsKeyword:
                            case TokenKind.LikeKeyword:
                            case TokenKind.MatchesKeyword:
                            case TokenKind.ThroughKeyword:
                            case TokenKind.ThruKeyword:
                            case TokenKind.BetweenKeyword:
                                {
                                    // require another expression
                                    requireExpression = true;
                                    parser.NextToken();
                                    node.AppendExpression(new TokenExpressionNode(parser.Token));
                                }
                                break;
                            case TokenKind.ClippedKeyword:
                            case TokenKind.SpacesKeyword:
                                {
                                    parser.NextToken();
                                    node.AppendExpression(new TokenExpressionNode(parser.Token));
                                }
                                break;
                            case TokenKind.IsKeyword:
                                {
                                    parser.NextToken();
                                    node.AppendExpression(new TokenExpressionNode(parser.Token));
                                    if (parser.PeekToken(TokenKind.NotKeyword))
                                    {
                                        parser.NextToken();
                                        node.AppendExpression(new TokenExpressionNode(parser.Token));
                                    }
                                    if (parser.PeekToken(TokenKind.NullKeyword))
                                    {
                                        parser.NextToken();
                                        node.AppendExpression(new TokenExpressionNode(parser.Token));
                                    }
                                    else
                                    {
                                        parser.ReportSyntaxError("NULL keyword required in expression.");
                                    }
                                }
                                break;
                            case TokenKind.NotKeyword:
                                {
                                    parser.NextToken();
                                    node.AppendExpression(new TokenExpressionNode(parser.Token));
                                    if (parser.PeekToken(TokenKind.LikeKeyword) ||
                                       parser.PeekToken(TokenKind.MatchesKeyword) ||
                                       parser.PeekToken(TokenKind.InKeyword))
                                    {
                                        // require another expression
                                        requireExpression = true;
                                        parser.NextToken();
                                        node.AppendExpression(new TokenExpressionNode(parser.Token));
                                    }
                                    else
                                    {
                                        parser.ReportSyntaxError("LIKE or MATCHES keyword required in expression.");
                                    }
                                }
                                break;
                            default:
                                {
                                    isOperator = false;
                                    break;
                                }
                        }
                        if (!isOperator)
                            break;
                        else
                        {
                            nextTok = parser.PeekToken();
                            if (nextTok.Kind == TokenKind.EndOfFile)
                                break;
                        }
                    }
                }
                if (!requireExpression)
                {
                    break;
                }
            }

            if (result && node != null)
            {
                node.EndIndex = parser.Token.Span.End;
            }

            return result;
        }
    }

    public class FunctionCallExpressionNode : FglExpressionNode
    {
        public FglNameExpression Function { get; private set; }

        private List<ExpressionNode> _params;
        public List<ExpressionNode> Parameters
        {
            get
            {
                if (_params == null)
                    _params = new List<ExpressionNode>();
                return _params;
            }
        }

        private List<Token> _anythingParameters;
        public List<Token> AnythingParameters
        {
            get
            {
                if (_anythingParameters == null)
                    _anythingParameters = new List<Token>();
                return _anythingParameters;
            }
        }

        public static bool TryParseExpression(IParser parser, out FunctionCallExpressionNode node, out FglNameExpression nonFunctionCallName, bool leftParenRequired = false, ExpressionParsingOptions options = null)
        {
            if (options == null)
                options = new ExpressionParsingOptions();
            node = null;
            nonFunctionCallName = null;
            bool result = false;

            FglNameExpression name;
            if (FglNameExpression.TryParseNode(parser, out name, TokenKind.LeftParenthesis))
            {
                node = new FunctionCallExpressionNode();
                node.StartIndex = name.StartIndex;
                node.Function = name;

                // get the left paren
                if (parser.PeekToken(TokenKind.LeftParenthesis))
                {
                    result = true;
                    parser.NextToken();

                    if (!options.AllowAnythingForFunctionParams)
                    {
                        // Parameters can be any expression, comma seperated
                        ExpressionNode expr;
                        while (FglExpressionNode.TryGetExpressionNode(parser, out expr, new List<TokenKind> { TokenKind.Comma, TokenKind.RightParenthesis }, options))
                        {
                            node.Parameters.Add(expr);
                            if (!parser.PeekToken(TokenKind.Comma))
                                break;
                            parser.NextToken();
                        }

                        // get the right paren
                        if (parser.PeekToken(TokenKind.RightParenthesis))
                        {
                            parser.NextToken(); // TODO: not sure if this is needed
                        }
                        else
                        {
                            parser.ReportSyntaxError("Call statement missing right parenthesis.");
                        }

                        if (parser.PeekToken(TokenKind.Dot))
                        {
                            parser.NextToken();
                            // get the dotted member access (which could end up being another function call, so this needs to allow recursion)
                            if (parser.PeekToken(TokenKind.Multiply))
                            {
                                parser.NextToken();
                                node.Children.Add(parser.Token.Span.Start, new TokenExpressionNode(parser.Token));
                            }
                            else
                            {
                                FunctionCallExpressionNode funcCall;
                                FglNameExpression nonFuncCallName;
                                if (FunctionCallExpressionNode.TryParseExpression(parser, out funcCall, out nonFuncCallName, false, options))
                                {
                                    node.Children.Add(funcCall.StartIndex, funcCall);
                                }
                                else if (nonFuncCallName != null)
                                {
                                    node.Children.Add(nonFuncCallName.StartIndex, nonFuncCallName);
                                }
                                else
                                {
                                    parser.NextToken();
                                    node.Children.Add(parser.Token.Span.Start, new TokenExpressionNode(parser.Token));
                                }
                            }
                        }
                    }
                    else
                    {
                        // just 
                        int rightParenLevel = 1;
                        Token tok;
                        bool done = false;
                        while (true)
                        {
                            tok = parser.NextToken();
                            switch (tok.Kind)
                            {
                                case TokenKind.LeftParenthesis:
                                    rightParenLevel++;
                                    break;
                                case TokenKind.RightParenthesis:
                                    rightParenLevel--;
                                    if (rightParenLevel == 0)
                                        done = true;
                                    break;
                                case TokenKind.EndOfFile:
                                    done = true;
                                    break;
                                default:
                                    node.AnythingParameters.Add(tok);
                                    break;
                            }
                            if (done)
                                break;
                        }
                    }
                    node.EndIndex = parser.Token.Span.End;
                }
                else
                {
                    if (leftParenRequired)
                    {
                        result = true;
                        parser.ReportSyntaxError("Call statement missing left parenthesis.");
                    }
                    else
                    {
                        nonFunctionCallName = name;
                        node = null;
                    }
                }
            }

            return result;
        }

        protected override string GetStringForm()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(Function.Name);
            sb.Append("(");
            for (int i = 0; i < Parameters.Count; i++)
            {
                sb.Append(Parameters[i].ToString());
                if (i + 1 < Parameters.Count)
                    sb.Append(", ");
            }
            sb.Append(")");
            return sb.ToString();
        }

        public override string GetExpressionType(GeneroAst ast)
        {
            // need to determine the return type for the function call
            IGeneroProject dummyProj;
            IProjectEntry dummyProjEntry;
            bool dummy;
            var result = ast.GetValueByIndex(Function.Name,
                                             Function.IndexSpan.Start,
                                             ast._functionProvider,
                                             ast._databaseProvider,
                                             ast._programFileProvider,
                                             true,
                                             out dummy,
                                             out dummyProj,
                                             out dummyProjEntry,
                                             FunctionProviderSearchMode.Search);
            if (result != null)
            {
                return result.Typename;
            }
            return null;
        }

        // For right now, there are certain "functions" that don't really behave like functions (specifically, in the parameters they take).
        // We don't want to error check those yet.
        private static HashSet<string> _skipValidationFunctionNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "cast",
            "get_fldbuf",
            "field_touched",
            "sfmt"
        };

        private static HashSet<string> _allowedNonStarRecordParamFunctions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "base.typeinfo.create",
            "com.WebOperation.CreateRPCStyle"
        };

        public override void CheckForErrors(GeneroAst ast, Action<string, int, int> errorFunc,
                                            Dictionary<string, List<int>> deferredFunctionSearches,
                                            FunctionProviderSearchMode searchInFunctionProvider = FunctionProviderSearchMode.NoSearch, bool isFunctionCallOrDefinition = false)
        {
            // 1) Check parameters for errors
            //      - undefined identifier
            //      - record without .*
            if (_skipValidationFunctionNames.Contains(Function.Name))
                return;

            // Check for the function name
            if (Function != null)
            {
                Function.CheckForErrors(ast, errorFunc, deferredFunctionSearches, FunctionProviderSearchMode.Deferred, true);
            }

            if (Parameters != null)
            {
                if (Function != null &&
                   Function.ResolvedResult != null &&
                   (Function.ResolvedResult is IFunctionResult))
                {
                    // TODO: if a parameter is a record, then the number of required input arguments increases by the number of fields in that record.
                    int totalRequiredParams = 0;
                    IGeneroProject proj;
                    IProjectEntry projEntry;
                    bool isDeferred;
                    foreach (var param in (Function.ResolvedResult as IFunctionResult).Parameters)
                    {
                        TypeReference typeRef = null;
                        var typeName = param.Type;
                        int index = typeName.IndexOf("record", StringComparison.OrdinalIgnoreCase);
                        // TODO: need to look into how importable projects populate their parameter result types
                        if(index == 0 && (typeName.Length == 6 || char.IsWhiteSpace(typeName[6])))
                        {
                            if (Function.ResolvedResult is AstNode4gl &&
                               (Function.ResolvedResult as AstNode4gl).StartIndex >= 0)
                            {
                                var useAst = ast as Genero4glAst;
                                if((Function.ResolvedResult as AstNode4gl).SyntaxTree != null &&
                                    (Function.ResolvedResult as AstNode4gl).SyntaxTree != ast)
                                {
                                    useAst = (Function.ResolvedResult as AstNode4gl).SyntaxTree as Genero4glAst;
                                }
                                // need to retrieve the variable and then get its type
                                var resVar = Genero4glAst.GetValueByIndex(param.Name, (Function.ResolvedResult as AstNode4gl).StartIndex, useAst, out proj, out projEntry, out isDeferred);
                                if (resVar != null &&
                                   resVar is VariableDef)
                                {
                                    typeRef = ((resVar as VariableDef).ResolvedType as TypeReference) ?? (resVar as VariableDef).Type;
                                }
                            }
                        }
                        else
                        {
                            // TODO: eventually, this needs to handle system types
                            var resType = Genero4glAst.GetValueByIndex(typeName, StartIndex, ast as Genero4glAst, out proj, out projEntry, out isDeferred);
                            if (resType != null &&
                               resType is TypeDefinitionNode &&
                               (resType as TypeDefinitionNode).TypeRef != null)
                            {
                                typeRef = (resType as TypeDefinitionNode).TypeRef;
                            }
                        }

                        if (typeRef != null && typeRef.IsRecord && (typeRef.Children[typeRef.Children.Keys[0]] is RecordDefinitionNode))
                        {
                            int recFieldCount = (typeRef.Children[typeRef.Children.Keys[0]] as RecordDefinitionNode).GetMembers(ast as Genero4glAst, Analysis.MemberType.All, false).Count();
                            if (recFieldCount > 0)
                                totalRequiredParams += recFieldCount;
                            else
                                totalRequiredParams++;
                        }
                        else
                        {
                            totalRequiredParams++;
                        }
                    }

                    int totalParameters = 0;
                    foreach (var param in Parameters)
                    {
                        // The genero compiler does not do error checking of types, so I guess anything goes...
                        // That makes things easier...
                        if (param is FglNameExpression)
                        {
                            var nameParam = param as FglNameExpression;
                            nameParam.CheckForErrors(ast, errorFunc, deferredFunctionSearches, FunctionProviderSearchMode.Deferred);
                            if (nameParam.ResolvedResult != null)
                            {
                                if (nameParam.ResolvedResult is TypeDefinitionNode) // Check for any invalid parameters TODO: others
                                {
                                    errorFunc("Invalid parameter found.", param.StartIndex, param.EndIndex);
                                }
                                else
                                {
                                    TypeReference typeRef = null;
                                    if (nameParam.ResolvedResult is VariableDef)
                                    {
                                        if ((nameParam.ResolvedResult as VariableDef).ResolvedType != null &&
                                            (nameParam.ResolvedResult as VariableDef).ResolvedType is TypeDefinitionNode)
                                        {
                                            var typeDef = (nameParam.ResolvedResult as VariableDef).ResolvedType as TypeDefinitionNode;
                                            if (typeDef?.TypeRef != null)
                                            {
                                                typeRef = typeDef.TypeRef;
                                            }
                                        }
                                        else
                                        {
                                            typeRef = (nameParam.ResolvedResult as VariableDef).Type;
                                        }
                                    }

                                    if (typeRef != null && typeRef.IsRecord && (typeRef.Children[typeRef.Children.Keys[0]] is RecordDefinitionNode))
                                    {
                                        if ((nameParam.ResolvedResult as VariableDef).Type.IsArray)
                                        {
                                            if((param as FglNameExpression).Name.EndsWith(".*"))
                                            {
                                                // need to get the number of fields in the record, as they count toward our passed parameter total
                                                int recFieldCount = ((RecordDefinitionNode) typeRef.Children[typeRef.Children.Keys[0]]).GetMembers(ast as Genero4glAst, Analysis.MemberType.All, false).Count();
                                                if (recFieldCount > 0)
                                                    totalParameters += (recFieldCount - 1); // minus 1 so we can do the increment below   
                                            }
                                        }
                                        else
                                        {
                                            if (!(param as FglNameExpression).Name.EndsWith(".*") &&
                                                !_allowedNonStarRecordParamFunctions.Contains(Function.Name))
                                            {
                                                errorFunc("Records must be specified with a '.*' ending when passed as a function parameter.", param.StartIndex, param.EndIndex);
                                            }
                                            else
                                            {
                                                if (!_allowedNonStarRecordParamFunctions.Contains(Function.Name))
                                                {
                                                    // need to get the number of fields in the record, as they count toward our passed parameter total
                                                    int recFieldCount = ((RecordDefinitionNode) typeRef.Children[typeRef.Children.Keys[0]]).GetMembers(ast as Genero4glAst, Analysis.MemberType.All, false).Count();
                                                    if (recFieldCount > 0)
                                                        totalParameters += (recFieldCount - 1); // minus 1 so we can do the increment below    
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        param.CheckForErrors(ast, errorFunc, deferredFunctionSearches);
                        totalParameters++;
                    }

                    // need to determine if any of the passed in parameters are records, and if so, adjust the passed count
                    if (totalParameters != totalRequiredParams)
                    {
                        errorFunc(string.Format("Unexpected number of parameters ({0}) found, expected {1} variables.", totalParameters, totalRequiredParams), StartIndex, EndIndex);
                    }
                }
            }

            // TODO: should we do something with the anything parameters

            base.CheckForErrors(ast, errorFunc, deferredFunctionSearches);
        }
    }

    public class BracketWrappedExpressionNode : FglExpressionNode
    {
        private List<ExpressionNode> _parameters;
        public List<ExpressionNode> Parameters
        {
            get
            {
                if (_parameters == null)
                    _parameters = new List<ExpressionNode>();
                return _parameters;
            }
        }

        public static bool TryParseNode(IParser parser, out BracketWrappedExpressionNode node)
        {
            node = null;
            bool result = false;

            if (parser.PeekToken(TokenKind.LeftBracket))
            {
                result = true;
                node = new BracketWrappedExpressionNode();
                parser.NextToken();
                node.StartIndex = parser.Token.Span.Start;

                ExpressionNode expr;
                while (FglExpressionNode.TryGetExpressionNode(parser, out expr, new List<TokenKind> { TokenKind.Comma, TokenKind.LeftBracket }))
                {
                    node.Parameters.Add(expr);
                    if (!parser.PeekToken(TokenKind.Comma))
                        break;
                    parser.NextToken();
                }

                // get the right paren
                if (parser.PeekToken(TokenKind.RightBracket))
                {
                    parser.NextToken(); // TODO: not sure if this is needed
                }
                else
                {
                    parser.ReportSyntaxError("Call statement missing right bracket.");
                }
                node.EndIndex = parser.Token.Span.End;
            }
            return result;
        }

        protected override string GetStringForm()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("[");
            for (int i = 0; i < Parameters.Count; i++)
            {
                sb.Append(Parameters[i].ToString());
                if (i + 1 < Parameters.Count)
                    sb.Append(", ");
            }
            sb.Append("]");
            return sb.ToString();
        }

        public override string GetExpressionType(GeneroAst ast)
        {
            return "[arg-list]";
        }
    }

    public class ParenWrappedExpressionNode : FglExpressionNode
    {
        public ExpressionNode InnerExpression { get; private set; }

        private List<Token> _anythingTokens;
        public List<Token> AnythingTokens
        {
            get
            {
                if (_anythingTokens == null)
                    _anythingTokens = new List<Token>();
                return _anythingTokens;
            }
        }

        public static bool TryParseExpression(IParser parser, out ParenWrappedExpressionNode node, ExpressionParsingOptions options = null)
        {
            if (options == null)
                options = new ExpressionParsingOptions();
            node = null;
            bool result = false;

            if (parser.PeekToken(TokenKind.LeftParenthesis))
            {
                parser.NextToken();
                node = new ParenWrappedExpressionNode();
                result = true;
                node.StartIndex = parser.Token.Span.Start;

                if (!options.AllowAnythingForFunctionParams)
                {
                    ExpressionNode exprNode;
                    if (!FglExpressionNode.TryGetExpressionNode(parser, out exprNode, new List<TokenKind> { TokenKind.RightParenthesis }, options))
                    {
                        parser.ReportSyntaxError("Invalid expression found within parentheses.");
                    }
                    else
                    {
                        node.InnerExpression = exprNode;
                    }

                    if (parser.PeekToken(TokenKind.RightParenthesis))
                    {
                        parser.NextToken();
                        node.EndIndex = parser.Token.Span.End;
                    }
                    else
                    {
                        parser.ReportSyntaxError("Right parenthesis not found.");
                    }
                }
                else
                {
                    // just 
                    int rightParenLevel = 1;
                    Token tok;
                    bool done = false;
                    while (true)
                    {
                        tok = parser.NextToken();
                        switch (tok.Kind)
                        {
                            case TokenKind.LeftParenthesis:
                                rightParenLevel++;
                                break;
                            case TokenKind.RightParenthesis:
                                rightParenLevel--;
                                if (rightParenLevel == 0)
                                    done = true;
                                break;
                            case TokenKind.EndOfFile:
                                done = true;
                                break;
                            default:
                                node.AnythingTokens.Add(tok);
                                break;
                        }
                        if (done)
                            break;
                    }
                }
            }

            return result;
        }

        protected override string GetStringForm()
        {
            if (InnerExpression != null)
                return string.Format("({0})", InnerExpression.ToString());
            return null;
        }

        public override string GetExpressionType(GeneroAst ast)
        {
            return InnerExpression.GetExpressionType(ast);
        }
    }

    public class FglStatementExpression : FglExpressionNode
    {
        private FglStatement _statement;

        public FglStatementExpression(FglStatement statement)
        {
            _statement = statement;
        }

        protected override string GetStringForm()
        {
            return _statement.ToString();
        }

        public override string GetExpressionType(GeneroAst ast)
        {
            return null;
        }
    }
}
