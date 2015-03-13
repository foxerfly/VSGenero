﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VSGenero.Analysis.Parsing.AST;

namespace VSGenero.Analysis
{
    public struct MemberResult
    {
        private readonly string _name;
        private string _completion;
        private readonly Func<GeneroMemberType> _type;
        private readonly Func<AstNode> _var;
        private readonly GeneroAst _ast;

        public MemberResult(string name, AstNode var, GeneroAst ast)
        {
            _name = _completion = name;
            _var = () => var;
            _type = null;
            _ast = ast;
            _type = GetMemberType;
        }

        public MemberResult(string name, GeneroMemberType type, GeneroAst ast)
        {
            _name = _completion = name;
            _type = () => type;
            _var = () => Empty;
            _ast = ast;
        }

        internal MemberResult(string name, string completion, AstNode node, GeneroMemberType? type, GeneroAst ast)
        {
            _name = name;
            _completion = completion;
            _var = () => node;
            _ast = ast;
            if (type != null)
            {
                _type = () => type.Value;
            }
            else
            {
                _type = null;
                _type = GetMemberType;
            }
        }

        internal MemberResult(string name, Func<AstNode> var, Func<GeneroMemberType> type, GeneroAst ast)
        {
            _name = _completion = name;
            _var = var;
            _type = type;
            _ast = ast;
        }

        public MemberResult FilterCompletion(string completion)
        {
            return new MemberResult(Name, completion, Namespace, MemberType, null);
        }

        internal AstNode Namespace
        {
            get
            {
                return _var();
            }
        }

        private static AstNode Empty = null;

        public string Name
        {
            get { return _name; }
        }

        public string Completion
        {
            get { return _completion; }
        }

        public string Documentation
        {
            get
            {
                var docSeen = new HashSet<string>();
                var typeSeen = new HashSet<string>();
                var docs = new List<string>();
                var types = new List<string>();

                var doc = new StringBuilder();
                var ns = _var();
                var docString = ns == null ? "" : ns.Documentation;
                if (docSeen.Add(docString))
                {
                    docs.Add(docString);
                }
                var typeString = ns == null ? "" : ns.ShortDescription;
                if (typeSeen.Add(typeString))
                {
                    types.Add(typeString);
                }

                var mt = MemberType;
                if (mt == GeneroMemberType.Instance || mt == GeneroMemberType.Constant)
                {
                    switch (mt)
                    {
                        case GeneroMemberType.Instance:
                            doc.Append("Instance of ");
                            break;
                        case GeneroMemberType.Constant:
                            doc.Append("Constant ");
                            break;
                        default:
                            doc.Append("Value of ");
                            break;
                    }
                    if (types.Count == 0)
                    {
                        doc.AppendLine("unknown type");
                    }
                    else if (types.Count == 1)
                    {
                        doc.AppendLine(types[0]);
                    }
                    else
                    {
                        var orStr = types.Count == 2 ? " or " : ", or ";
                        doc.AppendLine(string.Join(", ", types.Take(types.Count - 1)) + orStr + types.Last());
                    }
                    doc.AppendLine();
                }
                foreach (var str in docs.OrderBy(s => s))
                {
                    doc.AppendLine(str);
                    doc.AppendLine();
                }
                return Utils.CleanDocumentation(doc.ToString());
            }
        }

        public GeneroMemberType MemberType
        {
            get
            {
                return _type();
            }
        }

        private GeneroMemberType GetMemberType()
        {
            bool includesNone = false;
            GeneroMemberType result = GeneroMemberType.Unknown;
            var ns = _var();
            var nsType = ns.MemberType;
            if (result == GeneroMemberType.Unknown)
            {
                result = nsType;
            }
            else if (result == nsType)
            {
                // No change
            }
            else if (result == GeneroMemberType.Constant && nsType == GeneroMemberType.Instance)
            {
                // Promote from Constant to Instance
                result = GeneroMemberType.Instance;
            }

            if (result == GeneroMemberType.Unknown)
            {
                return includesNone ? GeneroMemberType.Constant : GeneroMemberType.Instance;
            }
            return result;
        }

        /// <summary>
        /// Gets the location(s) for the member(s) if they are available.
        /// 
        /// New in 1.5.
        /// </summary>
        public LocationInfo Location
        {
            get
            {
                var ns = _var();
                if(_ast != null)
                {
                    return _ast.ResolveLocation(ns);
                }
                return null;
            }
        }

        public override bool Equals(object obj)
        {
            if (!(obj is MemberResult))
            {
                return false;
            }

            return Name == ((MemberResult)obj).Name;
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }
    }
}
