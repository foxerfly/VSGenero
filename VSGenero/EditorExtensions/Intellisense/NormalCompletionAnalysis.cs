﻿using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VSGenero.Analysis;

namespace VSGenero.EditorExtensions.Intellisense
{
    internal class NormalCompletionAnalysis : CompletionAnalysis
    {
        private readonly ITextSnapshot _snapshot;
        private readonly GeneroProjectAnalyzer _analyzer;

        internal NormalCompletionAnalysis(GeneroProjectAnalyzer analyzer, ITextSnapshot snapshot, ITrackingSpan span, ITextBuffer textBuffer, CompletionOptions options)
            : base(span, textBuffer, options)
        {
            _snapshot = snapshot;
            _analyzer = analyzer;
        }

        private string FixupCompletionText(string exprText)
        {
            if (exprText.EndsWith("."))
            {
                exprText = exprText.Substring(0, exprText.Length - 1);
                if (exprText.Length == 0)
                {
                    // don't return all available members on empty dot.
                    return null;
                }
            }
            else
            {
                int cut = exprText.LastIndexOfAny(new[] { '.', ']', ')' });
                if (cut != -1)
                {
                    exprText = exprText.Substring(0, cut);
                }
                else
                {
                    exprText = String.Empty;
                }
            }
            return exprText;
        }

        internal string PrecedingExpression
        {
            get
            {
                var startSpan = _snapshot.CreateTrackingSpan(Span.GetSpan(_snapshot).Start.Position, 0, SpanTrackingMode.EdgeInclusive);
                var parser = new Genero4glReverseParser(_snapshot, _snapshot.TextBuffer, startSpan);
                var sourceSpan = parser.GetExpressionRange();
                if (sourceSpan.HasValue && sourceSpan.Value.Length > 0)
                {
                    return sourceSpan.Value.GetText();
                }
                return string.Empty;
            }
        }

        public override CompletionSet GetCompletions(IGlyphService glyphService)
        {
            var start1 = _stopwatch.ElapsedMilliseconds;

            var members = Enumerable.Empty<MemberResult>();

            var analysis = GetAnalysisEntry();
            var text = PrecedingExpression;
            if (!string.IsNullOrEmpty(text))
            {
                string fixedText = FixupCompletionText(text);
                if (analysis != null && fixedText != null)
                {
                    lock (_analyzer)
                    {
                        members = members.Concat(analysis.GetMembersByIndex(
                            fixedText,
                            GeneroProjectAnalyzer.TranslateIndex(
                                Span.GetEndPoint(_snapshot).Position,
                                _snapshot,
                                analysis
                            ),
                            _options.MemberOptions
                        ).ToArray());
                    }
                }
            }
            else
            {
                members = analysis.GetAllAvailableMembersByIndex(
                    GeneroProjectAnalyzer.TranslateIndex(
                        Span.GetStartPoint(_snapshot).Position,
                        _snapshot,
                        analysis
                    ),
                    _options.MemberOptions
                );
            }

            var end = _stopwatch.ElapsedMilliseconds;

            if (/*Logging &&*/ (end - start1) > TooMuchTime)
            {
                var memberArray = members.ToArray();
                members = memberArray;
                Trace.WriteLine(String.Format("{0} lookup time {1} for {2} members", this, end - start1, members.Count()));
            }

            var start = _stopwatch.ElapsedMilliseconds;

            var result = new FuzzyCompletionSet(
                "Python",
                "Python",
                Span,
                members.Select(m => GeneroCompletion(glyphService, m)),
                _options,
                CompletionComparer.UnderscoresLast);

            end = _stopwatch.ElapsedMilliseconds;

            if (/*Logging &&*/ (end - start1) > TooMuchTime)
            {
                Trace.WriteLine(String.Format("{0} completion set time {1} total time {2}", this, end - start, end - start1));
            }

            return result;
        }

    }
}