﻿/* ****************************************************************************
 * Copyright (c) 2015 Greg Fullman 
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution.
 * By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VSGenero.Analysis;
using VSGenero.Analysis.Parsing;

namespace VSGenero.EditorExtensions.Intellisense
{
    public class LiveCompletionAnalysis : CompletionAnalysis
    {
        private readonly IEnumerable<MemberResult> _result;
        private readonly List<Func<string, IEnumerable<MemberResult>>> _deferredLoadCallbacks;

        public LiveCompletionAnalysis(IEnumerable<MemberResult> result, ITrackingSpan span, ITextBuffer buffer, CompletionOptions options, List<Func<string, IEnumerable<MemberResult>>> deferredLoadCallback = null)
            : base(span, buffer, options)
        {
            _result = result;
            _deferredLoadCallbacks = deferredLoadCallback;
        }

        private IEnumerable<DynamicallyVisibleCompletion> DeferredLoadCompletions(string searchStr)
        {
            if(_deferredLoadCallbacks != null)
            {
                // call the load callbacks in parallel
                return _deferredLoadCallbacks.AsParallel().SelectMany(x => x(searchStr).Select(m =>
                    {
                        var c = GeneroCompletion(GlyphService, m);
                        c.Visible = true;
                        return c;
                    }));
            }
            return new DynamicallyVisibleCompletion[0];
        }

        public override CompletionSet GetCompletions(IGlyphService glyphService)
        {
            var result = new FuzzyCompletionSet(
                "Genero",
                "Genero",
                Span,
                _result.Select(m => GeneroCompletion(glyphService, m)),
                _options,
                CompletionComparer.UnderscoresLast,
                ((_deferredLoadCallbacks == null || _deferredLoadCallbacks.Count == 0) ? (Func<string, IEnumerable<DynamicallyVisibleCompletion>>) null : DeferredLoadCompletions));
            return result;
        }
    }

    public class TestCompletionAnalysis : CompletionAnalysis
    {
        private static IEnumerable<MemberResult> _result;

        public TestCompletionAnalysis(ITrackingSpan span, ITextBuffer buffer, CompletionOptions options)
            : base(span, buffer, options)
        {
        }

        public static void InitializeResults()
        {
            var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            var random = new Random();
            var list = new List<MemberResult>();

            for(int i = 0; i < 300000; i++)
            {
                list.Add(new MemberResult(new string(
    Enumerable.Repeat(chars, 8)
              .Select(s => s[random.Next(s.Length)])
              .ToArray()), GeneroMemberType.Variable, null));
            }
            _result = list;
        }

        public override CompletionSet GetCompletions(IGlyphService glyphService)
        {
            var result = new FuzzyCompletionSet(
                "Genero",
                "Genero",
                Span,
                _result.Select(m => GeneroCompletion(glyphService, m)),
                _options,
                CompletionComparer.UnderscoresLast, null);
            return result;
        }
    }
}
