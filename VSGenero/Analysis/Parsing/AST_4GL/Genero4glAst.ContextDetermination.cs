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
    public delegate IEnumerable<MemberResult> ContextSetProvider(int index);

    public partial class Genero4glAst
    {
        private static object _contextMapLock = new object();
        private static ContextCompletionMap _contextMap;
        private static Genero4glAst _instance;
        private static bool _includePublicFunctions;
        private static bool _includeDatabaseTables;
        private static string _contextString;

        #region Context Map Init

        public static async Task<bool> ReloadContextMap(bool downloadLatest = false)
        {
            lock (_contextMapLock)
            {
                if (_contextMap == null)
                    _contextMap = new ContextCompletionMap();
            }

            if (downloadLatest)
            {
                if (!await _contextMap.DownloadLatestFile())
                    return false;
            }

            lock(_contextMapLock)
            {
                return _contextMap.LoadFromXML();
            }
        }

        #endregion

        #region Context Provider Functions

        private static IEnumerable<MemberResult> GetPostUnaryOperators(int index)
        {
            if (_instance != null)
            {
                return _postUnaryOperators.Select(x => new MemberResult(Tokens.TokenKinds[x], GeneroMemberType.Keyword, _instance));
            }
            return new MemberResult[0];
        }

        private static IEnumerable<MemberResult> GetExpressionComponents(int index)
        {
            if (_instance != null)
            {
                return _instance.GetInstanceExpressionComponents(index);
            }
            return new MemberResult[0];
        }

        private static IEnumerable<MemberResult> GetTypes(int index)
        {
            if (_instance != null)
            {
                return _instance.GetInstanceTypes(index);
            }
            return new MemberResult[0];
        }

        private static IEnumerable<MemberResult> GetSystemTypes(int index)
        {
            return new MemberResult[0];
        }

        private static IEnumerable<MemberResult> GetFunctions(int index)
        {
            if (_instance != null)
            {
                return _instance.GetInstanceFunctions(index);
            }
            return new MemberResult[0];
        }

        private static IEnumerable<MemberResult> GetLabels(int index)
        {
            if (_instance != null)
            {
                return _instance.GetInstanceLabels(index);
            }
            return new MemberResult[0];
        }

        private static IEnumerable<MemberResult> GetVariables(int index)
        {
            if (_instance != null)
            {
                return _instance.GetInstanceVariables(index);
            }
            return new MemberResult[0];
        }

        private static IEnumerable<MemberResult> GetOptionsStartKeywords(int index)
        {
            return OptionsStartTokens
                .Select(x =>
                new MemberResult(Tokens.TokenKinds[x], GeneroMemberType.Keyword, _instance));
        }

        private static IEnumerable<MemberResult> GetDeclaredDialogs(int index)
        {
            if (_instance != null)
            {
                return _instance.GetInstanceDeclaredDialogs(index);
            }
            return new MemberResult[0];
        }

        private static IEnumerable<MemberResult> GetReports(int index)
        {
            if (_instance != null)
            {
                return _instance.GetInstanceReports(index);
            }
            return new MemberResult[0];
        }

        private static IEnumerable<MemberResult> GetConstants(int index)
        {
            if (_instance != null)
            {
                return _instance.GetInstanceConstants(index);
            }
            return new MemberResult[0];
        }

        private static IEnumerable<MemberResult> GetDatabaseTables(int index)
        {
            if (_instance != null)
            {
                return _instance.GetDatabaseTables(index, null);
            }
            return new MemberResult[0];
        }

        private static IEnumerable<MemberResult> GetAvailableImportModules(int index)
        {
            if (_instance != null)
            {
                return _instance.GetInstanceImportModules(index);
            }
            return new MemberResult[0];
        }

        private static IEnumerable<MemberResult> GetStatementStartKeywords(int index)
        {
            TokenKind[] accessMods = new TokenKind[] { TokenKind.PublicKeyword, TokenKind.PrivateKeyword };
            return ValidStatementKeywords
                .Union(accessMods)
                .Select(x =>
                new MemberResult(Tokens.TokenKinds[x], GeneroMemberType.Keyword, _instance));
        }

        private static IEnumerable<MemberResult> GetCursors(int index)
        {
            if (_instance != null)
            {
                return _instance.GetInstanceCursors(index, AstMemberType.Cursors);
            }
            return new MemberResult[0];
        }

        private static IEnumerable<MemberResult> GetPreparedCursors(int index)
        {
            if (_instance != null)
            {
                return _instance.GetInstanceCursors(index, AstMemberType.PreparedCursors);
            }
            return new MemberResult[0];
        }

        private static IEnumerable<MemberResult> GetDeclaredCursors(int index)
        {
            if (_instance != null)
            {
                return _instance.GetInstanceCursors(index, AstMemberType.DeclaredCursors);
            }
            return new MemberResult[0];
        }

        private static IEnumerable<MemberResult> GetBinaryOperatorKeywords(int index)
        {
            return _binaryOperators.Where(x => x > TokenKind.LastOperator)
              .Select(x =>
                new MemberResult(Tokens.TokenKinds[x], GeneroMemberType.Keyword, _instance));
        }

        #endregion

        public override IEnumerable<MemberResult> GetContextMembers(int index, IReverseTokenizer revTokenizer, IFunctionInformationProvider functionProvider,
                                                           IDatabaseInformationProvider databaseProvider, IProgramFileProvider programFileProvider,
                                                           out bool includePublicFunctions, out bool includeDatabaseTables, string contextStr, 
                                                           GetMemberOptions options = GetMemberOptions.IntersectMultipleResults)
        {
            _instance = this;
            _functionProvider = functionProvider;
            _databaseProvider = databaseProvider;
            _programFileProvider = programFileProvider;
            _includePublicFunctions = includePublicFunctions = false;    // this is a flag that the context determination logic sets if public functions should eventually be included in the set
            _includeDatabaseTables = includeDatabaseTables = false;
            _contextString = contextStr;

            List<MemberResult> members = new List<MemberResult>();
            // First see if we have a member completion
            if (TryMemberAccess(index, revTokenizer, out members))
            {
                _includePublicFunctions = false;
                _includeDatabaseTables = false;
                return members;
            }
            
            if (!DetermineContext(index, revTokenizer, members) && members.Count == 0)
            {
                // TODO: do we want to put in the statement keywords?
                members.AddRange(GetStatementStartKeywords(index));
            }

            includePublicFunctions = _includePublicFunctions;
            includeDatabaseTables = _includeDatabaseTables;
            _includePublicFunctions = false;    // reset the flag
            _includeDatabaseTables = false;
            _contextString = null;
            return members;
        }

        private bool DetermineContext(int index, IReverseTokenizer revTokenizer, List<MemberResult> memberList, bool onlyVerifyEmptyContext = false)
        {
            var enumerator = revTokenizer.GetReversedTokens().Where(x => x.SourceSpan.Start.Index < index).GetEnumerator();
            while (true)
            {
                if (!enumerator.MoveNext())
                {
                    return false;
                }

                var tokInfo = enumerator.Current;
                if (tokInfo.Equals(default(TokenInfo)) ||
                    tokInfo.Token.Kind == TokenKind.NewLine ||
                    tokInfo.Token.Kind == TokenKind.NLToken ||
                    tokInfo.Token.Kind == TokenKind.Comment)
                    continue;   // linebreak

                // Look for the token in the context map
                IEnumerable<ContextPossibilities> possibilities;
                if (_contextMap.TryGetValue(tokInfo.Token.Kind, out possibilities) ||
                    _contextMap.TryGetValue(tokInfo.Category, out possibilities))
                {
                    var matchContainer = new ContextPossibilityMatchContainer(possibilities);
                    IEnumerable<ContextPossibilities> matchingPossibilities;
                    if (matchContainer.TryMatchContextPossibility(tokInfo.SourceSpan.Start.Index, revTokenizer, out matchingPossibilities))
                    {
                        if (onlyVerifyEmptyContext)
                        {
                            return matchingPossibilities.All(x => x.SingleTokens.Length == 0 && x.SetProviders.Length == 0);
                        }
                        else
                        {
                            foreach (var matchedPossible in matchingPossibilities)
                                LoadPossibilitySet(index, matchedPossible, memberList);
                        }
                    }

                    return true;
                }
                else
                {
                    // we don't have the token in our context map, so return
                    return false;
                }
            }
        }

        private void LoadPossibilitySet(int index, ContextPossibilities matchedPossibility, List<MemberResult> members)
        {
            members.AddRange(matchedPossibility.SingleTokens.Select(x =>
                new MemberResult(Tokens.TokenKinds[x], GeneroMemberType.Keyword, _instance)));
            foreach (var provider in matchedPossibility.SetProviders)
                members.AddRange(provider(index));
        }

        private class ContextPossibilityMatchContainer
        {
            private Dictionary<object, List<BackwardTokenSearchItem>> _flatMatchingSet;
            private HashSet<object> _flatNonMatchingSet;
            private List<ContextPossibilities> _possibilitiesWithNoBackwardSearch;
            private int _matchingRound;

            public ContextPossibilityMatchContainer(IEnumerable<ContextPossibilities> possibilities)
            {
                _matchingRound = 0;
                _flatMatchingSet = new Dictionary<object, List<BackwardTokenSearchItem>>();
                _flatNonMatchingSet = new HashSet<object>();
                _possibilitiesWithNoBackwardSearch = new List<ContextPossibilities>();
                InitializeQueues(possibilities);
            }

            private void InitializeQueues(IEnumerable<ContextPossibilities> possibilities)
            {
                foreach (var possibility in possibilities)
                {
                    if (possibility.BackwardSearchItems.Length > 0)
                    {
                        foreach (var searchItem in possibility.BackwardSearchItems)
                        {
                            searchItem.ParentContext = possibility;
                            object key = TokenKind.EndOfFile;
                            if (searchItem.TokenSet == null)
                            {
                                key = searchItem.SingleToken;
                            }
                            else
                            {
                                key = searchItem.TokenSet.Set[0];
                            }

                            if (_flatMatchingSet.ContainsKey(key))
                            {
                                _flatMatchingSet[key].Add(searchItem);
                            }
                            else
                            {
                                _flatMatchingSet.Add(key, new List<BackwardTokenSearchItem> { searchItem });
                            }

                            if (!searchItem.Match)
                                _flatNonMatchingSet.Add(key);
                        }
                    }
                    else
                    {
                        _possibilitiesWithNoBackwardSearch.Add(possibility);
                    }
                }
            }

            public bool TryMatchContextPossibility(int index, IReverseTokenizer revTokenizer, out IEnumerable<ContextPossibilities> matchingPossibilities)
            {
                List<ContextPossibilities> retList = new List<ContextPossibilities>();
                bool isMatch = false;

                if (_flatMatchingSet.Count > 0)
                {
                    // start reverse parsing
                    var enumerator = revTokenizer.GetReversedTokens().Where(x => x.SourceSpan.Start.Index < index).GetEnumerator();
                    while (true)
                    {
                        if (!enumerator.MoveNext())
                        {
                            isMatch = false;
                            break;
                        }
                        var tokInfo = enumerator.Current;
                        if (tokInfo.Equals(default(TokenInfo)) || tokInfo.Token.Kind == TokenKind.NewLine || tokInfo.Token.Kind == TokenKind.NLToken || tokInfo.Token.Kind == TokenKind.Comment)
                            continue;   // linebreak

                        // look for the token in the matching dictionary
                        List<BackwardTokenSearchItem> matchList;
                        if (_flatMatchingSet.TryGetValue(tokInfo.Token.Kind, out matchList) ||
                            _flatMatchingSet.TryGetValue(tokInfo.Category, out matchList))
                        {
                            // need to attempt matching the match list
                            // 1) grab the potential matches with an ordered set and attempt to completely match each set. If one of the sets completely matches, we have a winner
                            foreach (var potentialMatch in matchList.Where(x => x.TokenSet != null))
                            {
                                if (AttemptOrderedSetMatch(tokInfo.SourceSpan.Start.Index, revTokenizer, potentialMatch.TokenSet))
                                {
                                    retList.Add(potentialMatch.ParentContext);
                                    isMatch = true;
                                    break;
                                }
                            }
                            if (isMatch)
                                break;      // if we have a match from the for loop above, we're done.

                            // 2) If we have any single token matches, they win.
                            var singleMatches = matchList.Where(x =>
                                (x.SingleToken is TokenKind && (TokenKind)x.SingleToken != TokenKind.EndOfFile) ||
                                (x.SingleToken is TokenCategory && (TokenCategory)x.SingleToken != TokenCategory.None)).ToList();
                            if (singleMatches.Count > 0)
                            {
                                retList.AddRange(singleMatches.Select(x => x.ParentContext));
                                isMatch = true;// singleMatches.All(x => x.Match);  // TODO: the match flag isn't being used....need to figure that out.
                                break;
                            }

                            // At this point, nothing was matched correctly, so we continue
                        }
                        else if (_flatNonMatchingSet.Count > 0 &&
                                (!_flatNonMatchingSet.Contains(tokInfo.Token.Kind) ||
                                !_flatNonMatchingSet.Contains(tokInfo.Category)))
                        {
                            // need to attempt matching the match list
                            // 1) grab the potential matches with an ordered set and attempt to completely match each set. If one of the sets completely matches, we have a winner
                            foreach (var potentialMatch in _flatNonMatchingSet.SelectMany(x => _flatMatchingSet[x]).Where(x => x.TokenSet != null))
                            {
                                if (AttemptOrderedSetMatch(tokInfo.SourceSpan.Start.Index, revTokenizer, potentialMatch.TokenSet, false))
                                {
                                    retList.Add(potentialMatch.ParentContext);
                                    isMatch = true;
                                    break;
                                }
                            }
                            if (isMatch)
                                break;      // if we have a match from the for loop above, we're done.

                            // 2) If we have any single token matches, they win.
                            var singleMatches = _flatNonMatchingSet.SelectMany(x => _flatMatchingSet[x]).Where(x =>
                                (x.SingleToken is TokenKind && (TokenKind)x.SingleToken != TokenKind.EndOfFile) ||
                                (x.SingleToken is TokenCategory && (TokenCategory)x.SingleToken != TokenCategory.None)).ToList();
                            if (singleMatches.Count > 0)
                            {
                                retList.AddRange(singleMatches.Select(x => x.ParentContext));
                                isMatch = true;
                                break;
                            }

                            // At this point, nothing was matched correctly, so we continue
                        }
                        else
                        {
                            if (Genero4glAst.ValidStatementKeywords.Contains(tokInfo.Token.Kind))
                            {
                                isMatch = false;
                                break;
                            }
                        }
                    }
                }

                if (!isMatch && _possibilitiesWithNoBackwardSearch.Count > 0)
                {
                    retList.AddRange(_possibilitiesWithNoBackwardSearch);
                    isMatch = true;
                }

                matchingPossibilities = retList;
                return isMatch;
            }

            private bool AttemptOrderedSetMatch(int index, IReverseTokenizer revTokenizer, OrderedTokenSet tokenSet, bool doMatch = true)
            {
                bool isMatch = false;
                int tokenIndex = 1;

                // start reverse parsing
                var enumerator = revTokenizer.GetReversedTokens().Where(x => x.SourceSpan.Start.Index < index).GetEnumerator();
                while (true)
                {
                    if (!enumerator.MoveNext())
                    {
                        isMatch = false;
                        break;
                    }
                    var tokInfo = enumerator.Current;
                    if (tokInfo.Equals(default(TokenInfo)) || tokInfo.Token.Kind == TokenKind.NewLine || tokInfo.Token.Kind == TokenKind.NLToken || tokInfo.Token.Kind == TokenKind.Comment)
                        continue;   // linebreak

                    if (doMatch &&
                        (tokenSet.Set[tokenIndex] is TokenKind && (TokenKind)tokenSet.Set[tokenIndex] == tokInfo.Token.Kind) ||
                        (tokenSet.Set[tokenIndex] is TokenCategory && (TokenCategory)tokenSet.Set[tokenIndex] == tokInfo.Category))
                    {
                        tokenIndex++;
                        if (tokenSet.Set.Count == tokenIndex)
                        {
                            isMatch = true;
                            break;
                        }
                    }
                    else if (!doMatch &&
                        (tokenSet.Set[tokenIndex] is TokenKind && (TokenKind)tokenSet.Set[tokenIndex] != tokInfo.Token.Kind) ||
                        (tokenSet.Set[tokenIndex] is TokenCategory && (TokenCategory)tokenSet.Set[tokenIndex] != tokInfo.Category))
                    {
                        tokenIndex++;
                        if (tokenSet.Set.Count == tokenIndex)
                        {
                            isMatch = true;
                            break;
                        }
                    }
                    else
                    {
                        if (Genero4glAst.ValidStatementKeywords.Contains(tokInfo.Token.Kind) ||
                            tokInfo.Token.Kind == TokenKind.EndOfFile)
                        {
                            isMatch = false;
                            break;
                        }
                    }
                }

                return isMatch;
            }
        }
    }

    internal class ContextPossibilities
    {
        public TokenKind[] SingleTokens { get; private set; }
        public ContextSetProvider[] SetProviders { get; private set; }
        public BackwardTokenSearchItem[] BackwardSearchItems { get; private set; }

        public ContextPossibilities(TokenKind[] singleTokens,
            ContextSetProvider[] setProviders,
            BackwardTokenSearchItem[] backwardSearchItems)
        {
            SingleTokens = singleTokens;
            SetProviders = setProviders;
            BackwardSearchItems = backwardSearchItems;
        }
    }

    internal class BackwardTokenSearchItem
    {
        public OrderedTokenSet TokenSet { get; private set; }
        public object SingleToken { get; private set; }
        public bool Match { get; private set; }
        public ContextPossibilities ParentContext { get; set; }

        public BackwardTokenSearchItem(OrderedTokenSet tokenSet, bool match = true)
        {
            TokenSet = tokenSet;
            SingleToken = TokenKind.EndOfFile;
            Match = match;
        }

        public BackwardTokenSearchItem(TokenKind singleToken, bool match = true)
        {
            SingleToken = singleToken;
            TokenSet = null;
            Match = match;
        }
    }

    internal class OrderedTokenSet
    {
        public List<object> Set { get; private set; }

        public OrderedTokenSet(IEnumerable<object> tokenSet)
        {
            Set = new List<object>(tokenSet);
        }
    }
}
