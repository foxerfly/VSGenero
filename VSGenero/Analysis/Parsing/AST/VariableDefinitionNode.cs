﻿using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VSGenero.Analysis.Parsing.AST
{
    public class VariableDef : IAnalysisResult
    {
        public string Name { get; private set; }
        public TypeReference Type { get; private set; }
        private string _filename;

        private bool _isPublic = false;
        public bool IsPublic
        {
            get { return _isPublic; }
        }

        public void SetIsPublic(bool value)
        {
            _isPublic = value;
        }

        public VariableDef(string name, TypeReference type, int location, bool isPublic = false, string filename = null)
        {
            Name = name;
            Type = type;
            _locationIndex = location;
            _isPublic = isPublic;
            _filename = filename;
        }

        public string Documentation
        {
            get
            {
                StringBuilder sb = new StringBuilder();
                if(!string.IsNullOrWhiteSpace(Scope))
                {
                    sb.AppendFormat("({0}) ", Scope);
                }
                sb.AppendFormat("{0} {1}", Name, Type.ToString());
                return sb.ToString();
            }
        }

        private string _scope;
        public string Scope
        {
            get
            {
                return _scope;
            }
            set
            {
                _scope = value;
            }
        }

        private int _locationIndex;
        public int LocationIndex
        {
            get { return _locationIndex; }
        }

        public LocationInfo Location 
        { 
            get 
            { 
                if(!string.IsNullOrWhiteSpace(_filename))
                {
                    return new LocationInfo(_filename, _locationIndex);
                }
                return null; 
            } 
        }

        public IAnalysisResult GetMember(string name, GeneroAst ast, out IGeneroProject definingProject, out IProjectEntry projEntry)
        {
            return Type.GetMember(name, ast, out definingProject, out projEntry);
        }

        public IEnumerable<MemberResult> GetMembers(GeneroAst ast, MemberType memberType)
        {
            return Type.GetMembers(ast, memberType);
        }

        public bool HasChildFunctions(GeneroAst ast)
        {
            return Type.HasChildFunctions(ast);
        }

        public bool CanGetValueFromDebugger
        {
            get { return true; }
        }
    }

    /// <summary>
    /// Format:
    /// identifier [,...] <see cref="TypeReference"/> [<see cref="AttributeSpecifier"/>]
    /// 
    /// For more info, see: http://www.4js.com/online_documentation/fjs-fgl-manual-html/index.html#c_fgl_variables_DEFINE.html
    /// </summary>
    public class VariableDefinitionNode : AstNode
    {
        private List<Tuple<string, int>> _identifiers;
        public List<Tuple<string, int>> Identifiers
        {
            get
            {
                if(_identifiers == null)
                {
                    _identifiers = new List<Tuple<string, int>>();
                }
                return _identifiers;
            }
            set
            {
                _identifiers = value;
            }
        }

        public LocationInfo Location { get; private set; }

        private List<VariableDef> _varDefs;
        public List<VariableDef> VariableDefinitions
        {
            get
            {
                if (_varDefs == null)
                    _varDefs = new List<VariableDef>();
                return _varDefs;
            }
        }

        public static bool TryParseNode(IParser parser, out VariableDefinitionNode defNode, Action<VariableDef> binder = null)
        {
            defNode = null;
            bool result = false;
            uint peekaheadCount = 1;
            List<Tuple<string, int>> identifiers = new List<Tuple<string, int>>();

            var tok = parser.PeekTokenWithSpan(peekaheadCount);
            var tokInfo = Tokenizer.GetTokenInfo(tok.Token);
            while (tokInfo.Category == TokenCategory.Identifier || tokInfo.Category == TokenCategory.Keyword)
            {
                identifiers.Add(new Tuple<string, int>(tok.Token.Value.ToString(), tok.Span.Start));
                peekaheadCount++;
                if (!parser.PeekToken(TokenKind.Comma, peekaheadCount))
                    break;
                peekaheadCount++;
                tok = parser.PeekTokenWithSpan(peekaheadCount);
                tokInfo = Tokenizer.GetTokenInfo(tok.Token);
            }

            if (identifiers.Count > 0)
            {
                result = true;
                defNode = new VariableDefinitionNode();
                defNode.StartIndex = parser.Token.Span.Start;
                defNode.Location = parser.TokenLocation;
                defNode.Identifiers = new List<Tuple<string, int>>(identifiers);
                while (peekaheadCount > 1)
                {
                    parser.NextToken();
                    peekaheadCount--;
                }

                TypeReference typeRef;
                if(TypeReference.TryParseNode(parser, out typeRef))
                {
                    defNode.EndIndex = typeRef.EndIndex;
                    defNode.Children.Add(typeRef.StartIndex, typeRef);
                    if(defNode.Children.Last().Value.IsComplete)
                    {
                        defNode.IsComplete = true;
                    }
                }
                else
                {
                    parser.ReportSyntaxError("No type defined for variable(s).");
                    result = false;
                }

                if(typeRef != null)
                {
                    foreach (var ident in defNode.Identifiers)
                    {
                        var varDef = new VariableDef(ident.Item1, typeRef, ident.Item2, false, (defNode.Location != null ? defNode.Location.FilePath : null));
                        defNode.VariableDefinitions.Add(varDef);
                        if(binder != null)
                        {
                            binder(varDef);
                        }
                    }
                }
            }

            return result;
        }
    }
}
