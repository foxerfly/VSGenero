﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using VSGenero.Analysis;
using VSGenero.Analysis.Parsing.AST;

namespace VSGenero.EditorExtensions.Intellisense
{
    /// <summary>
    /// Provides various completion services after the text around the current location has been
    /// processed. The completion services are specific to the current context
    /// </summary>
    public class CompletionAnalysis
    {
        private readonly ITrackingSpan _span;
        private readonly ITextBuffer _textBuffer;
        internal readonly CompletionOptions _options;
        internal const Int64 TooMuchTime = 50;
        protected static Stopwatch _stopwatch = MakeStopWatch();

        internal static CompletionAnalysis EmptyCompletionContext = new CompletionAnalysis(null, null, null);

        internal CompletionAnalysis(ITrackingSpan span, ITextBuffer textBuffer, CompletionOptions options)
        {
            _span = span;
            _textBuffer = textBuffer;
            _options = (options == null) ? new CompletionOptions() : options.Clone();
        }

        public ITextBuffer TextBuffer
        {
            get
            {
                return _textBuffer;
            }
        }

        public ITrackingSpan Span
        {
            get
            {
                return _span;
            }
        }

        public virtual CompletionSet GetCompletions(IGlyphService glyphService)
        {
            return null;
        }

        internal static bool IsKeyword(ClassificationSpan token, string keyword)
        {
            return token.ClassificationType.Classification == PredefinedClassificationTypeNames.Keyword && token.Span.GetText() == keyword;
        }

        internal static DynamicallyVisibleCompletion GeneroCompletion(IGlyphService service, MemberResult memberResult)
        {
            return new DynamicallyVisibleCompletion(memberResult.Name,
                memberResult.Completion,
                () => memberResult.Documentation,
                () => service.GetGlyph(memberResult.MemberType.ToGlyphGroup(), StandardGlyphItem.GlyphItemPublic),
                Enum.GetName(typeof(GeneroMemberType), memberResult.MemberType)
            );
        }

        internal static DynamicallyVisibleCompletion PythonCompletion(IGlyphService service, string name, string tooltip, StandardGlyphGroup group)
        {
            var icon = new IconDescription(group, StandardGlyphItem.GlyphItemPublic);

            var result = new DynamicallyVisibleCompletion(name,
                name,
                tooltip,
                service.GetGlyph(group, StandardGlyphItem.GlyphItemPublic),
                Enum.GetName(typeof(StandardGlyphGroup), group));
            result.Properties.AddProperty(typeof(IconDescription), icon);
            return result;
        }

        internal static DynamicallyVisibleCompletion PythonCompletion(IGlyphService service, string name, string completion, string tooltip, StandardGlyphGroup group)
        {
            var icon = new IconDescription(group, StandardGlyphItem.GlyphItemPublic);

            var result = new DynamicallyVisibleCompletion(name,
                completion,
                tooltip,
                service.GetGlyph(group, StandardGlyphItem.GlyphItemPublic),
                Enum.GetName(typeof(StandardGlyphGroup), group));
            result.Properties.AddProperty(typeof(IconDescription), icon);
            return result;
        }

        internal GeneroAst GetAnalysisEntry()
        {
            var entry = (IGeneroProjectEntry)TextBuffer.GetAnalysis();
            return entry != null ? entry.Analysis : null;
        }

        private static Stopwatch MakeStopWatch()
        {
            var res = new Stopwatch();
            res.Start();
            return res;
        }

        protected IEnumerable<MemberResult> GetModules(string[] package, bool modulesOnly = true)
        {
            var analysis = GetAnalysisEntry();

            if (package == null)
            {
                package = new string[0];
            }

            var modules = Enumerable.Empty<MemberResult>();
            if (analysis != null)
            {
                modules = modules.Concat(package.Length > 0 ?
                    analysis.GetModuleMembers(package, !modulesOnly) :
                    analysis.GetModules(true).Distinct(CompletionComparer.MemberEquality)
                );
            }

            return modules;
        }

        public override string ToString()
        {
            if (Span == null)
            {
                return "CompletionContext.EmptyCompletionContext";
            };
            var snapSpan = Span.GetSpan(TextBuffer.CurrentSnapshot);
            return String.Format("CompletionContext({0}): {1} @{2}", GetType().Name, snapSpan.GetText(), snapSpan.Span);
        }
    }
}
