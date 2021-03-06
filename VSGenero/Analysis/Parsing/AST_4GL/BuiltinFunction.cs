﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VSGenero.Analysis.Parsing.AST_4GL
{
    public class BuiltinFunction : IFunctionResult
    {
        private readonly string _name;
        private readonly List<ParameterResult> _parameters;
        private readonly List<string> _returns;
        private readonly string _description;
        private readonly string _namespace;

        public bool IsPublic { get { return true; } }

        public bool CanGetValueFromDebugger
        {
            get { return false; }
        }

        public BuiltinFunction(string name, string nameSpace, IEnumerable<ParameterResult> parameters, IEnumerable<string> returns, string description, GeneroLanguageVersion minimumBdlVersion = GeneroLanguageVersion.None)
        {
            _name = name;
            _namespace = nameSpace;
            _description = description;
            _parameters = new List<ParameterResult>(parameters);
            _returns = new List<string>(returns);
            _minBdlVersion = minimumBdlVersion;
        }

        private readonly GeneroLanguageVersion _minBdlVersion;
        public GeneroLanguageVersion MinimumBdlVersion
        {
            get
            {
                return _minBdlVersion;
            }
        }

        public ParameterResult[] Parameters
        {
            get { return _parameters.ToArray(); }
        }

        public AccessModifier AccessModifier
        {
            get { return Analysis.AccessModifier.Public; }
        }

        public string FunctionDocumentation
        {
            get { return _description; }
        }

        private Dictionary<string, IAnalysisResult> _dummyDict = new Dictionary<string, IAnalysisResult>();
        public IDictionary<string, IAnalysisResult> Variables
        {
            get { return _dummyDict; }
        }

        public IDictionary<string, IAnalysisResult> Types
        {
            get { return _dummyDict; }
        }

        public IDictionary<string, IAnalysisResult> Constants
        {
            get { return _dummyDict; }
        }

        private string _scope = "system function";
        public string Scope
        {
            get
            {
                return _scope;
            }
            set
            {
            }
        }

        public string Name
        {
            get
            {
                return _name;
            }
        }

        public string Documentation
        {
            get
            {
                StringBuilder sb = new StringBuilder();

                if (!string.IsNullOrWhiteSpace(Scope))
                {
                    sb.AppendFormat("({0}) ", Scope);
                }

                if (_returns.Count == 0)
                {
                    sb.Append("void ");
                }
                else if (_returns.Count == 1)
                {
                    sb.AppendFormat("{0} ", _returns[0]);
                }

                if (!string.IsNullOrWhiteSpace(_namespace))
                    sb.AppendFormat("{0}.", _namespace);
                sb.Append(Name);
                sb.Append('(');

                // if there are any parameters put them in
                int total = _parameters.Count;
                int i = 0;
                foreach (var varDef in _parameters)
                {
                    sb.AppendFormat("{0} {1}", varDef.Type, varDef.Name);
                    if (i + 1 < total)
                    {
                        sb.Append(", ");
                    }
                    i++;
                }

                sb.Append(')');

                if (_returns.Count > 1)
                {
                    sb.AppendLine();
                    sb.Append("returning ");
                    foreach (var ret in _returns)
                    {
                        sb.Append(ret);
                        if (i + 1 < total)
                        {
                            sb.Append(", ");
                        }
                        i++;
                    }
                }
                return sb.ToString();
            }
        }

        public int LocationIndex
        {
            get { return -1; }
        }

        public LocationInfo Location { get { return null; } }

        public IAnalysisResult GetMember(string name, Genero4glAst ast, out IGeneroProject definingProject, out IProjectEntry projEntry, bool function)
        {
            definingProject = null;
            projEntry = null;
            if (_returns != null && _returns.Count == 1)
            {
                var typeRef = new TypeReference(_returns[0]);
                return typeRef.GetMember(name, ast, out definingProject, out projEntry, function);
            }
            return null;
        }

        public IEnumerable<MemberResult> GetMembers(Genero4glAst ast, MemberType memberType, bool function)
        {
            if (_returns != null && _returns.Count == 1)
            {
                var typeRef = new TypeReference(_returns[0]);
                return typeRef.GetMembers(ast, memberType, function);
            }
            return new MemberResult[0];
        }

        public bool HasChildFunctions(Genero4glAst ast)
        {
            return false;
        }

        public void SetCommentDocumentation(string commentDoc)
        {
        }

        public bool CanOutline
        {
            get { return false; }
        }

        public int StartIndex
        {
            get
            {
                return -1;
            }
            set
            {
            }
        }

        public int EndIndex
        {
            get
            {
                return -1;
            }
            set
            {
            }
        }

        public int DecoratorEnd
        {
            get
            {
                return -1;
            }
            set
            {
            }
        }

        public string CompletionParentName
        {
            get { return null; }
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

        public string Typename
        {
            get
            {
                if (_returns != null && _returns.Count == 1)
                {
                    var typeRef = new TypeReference(_returns[0]);
                    return typeRef.ToString();
                }
                return null;
            }
        }


        public string[] Returns
        {
            get { return _returns.ToArray(); }
        }

        private Dictionary<string, List<Tuple<IAnalysisResult, IndexSpan>>> _dummyLimitDict = new Dictionary<string, List<Tuple<IAnalysisResult, IndexSpan>>>();
        public IDictionary<string, List<Tuple<IAnalysisResult, IndexSpan>>> LimitedScopeVariables
        {
            get { return _dummyLimitDict; }
        }

        private SortedList<int, int> _additionalDecoratorRanges;
        public SortedList<int, int> AdditionalDecoratorRanges
        {
            get
            {
                if (_additionalDecoratorRanges == null)
                    _additionalDecoratorRanges = new SortedList<int, int>();
                return _additionalDecoratorRanges;
            }
        }
    }
}
