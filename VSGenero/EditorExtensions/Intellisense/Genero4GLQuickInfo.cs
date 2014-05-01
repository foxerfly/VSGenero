﻿/* ****************************************************************************
 * 
 * Copyright (c) 2014 Greg Fullman 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution.
 * By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 * 
 * Contents of this file are based on the MSDN walkthrough here:
 * http://msdn.microsoft.com/en-us/library/ee197646.aspx
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.Editor;

namespace VSGenero.EditorExtensions.Intellisense
{
    [Export(typeof(IQuickInfoSourceProvider)), ContentType(VSGeneroConstants.ContentType4GL), Order, Name("Genero4GL Quick Info Source")]
    internal class Genero4GLQuickInfoSourceProvider : IQuickInfoSourceProvider
    {
        [Import]
        internal ITextStructureNavigatorSelectorService NavigatorService { get; set; }

        [Import]
        internal ITextBufferFactoryService TextBufferFactoryService { get; set; }

        [Import(AllowDefault = true)]
        internal IDatabaseInformationProvider DbInformationProvider { get; set; }

        [Import(AllowDefault = true)]
        internal IPublicFunctionProvider PublicFunctionProvider { get; set; }

        public IQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer)
        {
            return new Genero4GLQuickInfoSource(this, textBuffer);
        }
    }

    internal class Genero4GLQuickInfoSource : IQuickInfoSource
    {
        private Genero4GLQuickInfoSourceProvider m_provider;
        private ITextBuffer m_subjectBuffer;

        public Genero4GLQuickInfoSource(Genero4GLQuickInfoSourceProvider provider, ITextBuffer subjectBuffer)
        {
            m_provider = provider;
            m_subjectBuffer = subjectBuffer;
        }

        public void AugmentQuickInfoSession(IQuickInfoSession session, IList<object> qiContent, out ITrackingSpan applicableToSpan)
        {
            applicableToSpan = null;
            // Map the trigger point down to our buffer.
            SnapshotPoint? subjectTriggerPoint = session.GetTriggerPoint(m_subjectBuffer.CurrentSnapshot);
            if (!subjectTriggerPoint.HasValue)
            {
                return;
            }
            if (subjectTriggerPoint.Value.IsWithinComment() ||
                subjectTriggerPoint.Value.IsWithinString())
            {
                return;
            }

            ITextSnapshot currentSnapshot = subjectTriggerPoint.Value.Snapshot;
            SnapshotSpan querySpan = new SnapshotSpan(subjectTriggerPoint.Value, 0);

            //look for occurrences of our QuickInfo words in the span
            SnapshotSpan textSpan;

            // get the word we're hovering over
            ITextStructureNavigator navigator = m_provider.NavigatorService.GetTextStructureNavigator(m_subjectBuffer);
            TextExtent extent = navigator.GetExtentOfWord(subjectTriggerPoint.Value);
            string searchText = extent.Span.GetText();
            textSpan = extent.Span;

            SnapshotSpan dummySpan;
            // try to get any member access associated with the text we're hovering over
            searchText = subjectTriggerPoint.Value.GetQuickInfoMemberOrMemberAccess(out dummySpan, searchText);
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                string[] splitTokens = searchText.Split(new[] { '.' });
                // TODO: check for any record variables, tables, package names, or class variables in splitTokens[0]
                // TODO: if found, take a look at the members
                GeneroPackage tmpPackage = null;
                GeneroClass tmpClass = null;
                GeneroClassMethod tmpMethod = null;
                VariableDefinition tempDef = null;
                FunctionDefinition funcDef = null;
                CursorPreparation cursorPrep = null;
                TempTableDefinition tempTableDef = null;
                string publicFunctionQuickInfo = null;
                string context = null;
                GeneroTableColumn columnOrRecordField = null;
                for (int i = 0; i < splitTokens.Length; i++)
                {
                    // Take care of packages, classes, and methods
                    if (tmpClass != null && i == 2)
                    {
                        tmpClass.Methods.TryGetValue(splitTokens[i], out tmpMethod);
                    }
                    if (tmpPackage != null && i == 1)
                    {
                        tmpPackage.Classes.TryGetValue(splitTokens[i], out tmpClass);
                    }
                    if (i == 0)
                        GeneroSingletons.LanguageSettings.Packages.TryGetValue(splitTokens[i], out tmpPackage);

                    if (tmpPackage == null &&
                       tmpClass == null &&
                       tmpMethod == null)
                    {
                        if (i == 0)
                        {
                            // check globals and module variables first
                            var fpm = m_subjectBuffer.Properties.GetProperty(typeof(GeneroFileParserManager)) as GeneroFileParserManager;
                            if (fpm != null)
                            {
                                // look at the variables in the current function
                                funcDef = IntellisenseExtensions.DetermineContainingFunction(subjectTriggerPoint.Value, fpm);
                                if (funcDef != null)
                                {
                                    if (funcDef.Variables.TryGetValue(splitTokens[i], out tempDef))
                                    {
                                        context = "local";
                                    }
                                }
                                if (tempDef == null)
                                {
                                    if (!fpm.ModuleContents.ModuleVariables.TryGetValue(splitTokens[i], out tempDef))
                                    {
                                        if (fpm.ModuleContents.GlobalVariables.TryGetValue(splitTokens[i], out tempDef))
                                        {
                                            context = "global";
                                        }
                                        else
                                        {
                                            int recordElementPos = extent.Span.Start.Position;
                                            // Could be hovering over a record variable's child element in the definition
                                            var owningRecordVar = fpm.ModuleContents.ModuleVariables.FirstOrDefault(x =>
                                                    x.Value.IsRecordType &&
                                                    x.Value.RecordElements.Any(
                                                        y => y.Value.Position == recordElementPos));
                                            if (!owningRecordVar.Equals(default(KeyValuePair<string, VariableDefinition>)))
                                            {
                                                tempDef = owningRecordVar.Value.RecordElements[splitTokens[i]];
                                            }
                                        }
                                    }
                                    else
                                    {
                                        context = "module";
                                    }
                                }
                                if (tempDef != null)
                                {
                                    applicableToSpan = currentSnapshot.CreateTrackingSpan
                                        (
                                            textSpan.Start.Position, tempDef.Name.Length, SpanTrackingMode.EdgeInclusive
                                        );
                                }
                                if (tempDef == null)
                                {
                                    // If we got down to here, it might be a function?
                                    if (fpm.ModuleContents.FunctionDefinitions.TryGetValue(splitTokens[i], out funcDef))
                                    {
                                        string functionName = funcDef.Name;
                                        if (functionName == null && funcDef.Main)
                                            functionName = "main";
                                        applicableToSpan = currentSnapshot.CreateTrackingSpan
                                                (
                                                    textSpan.Start.Position, functionName.Length, SpanTrackingMode.EdgeInclusive
                                                );
                                    }
                                    else
                                    {
                                        if (m_provider.PublicFunctionProvider != null)
                                        {
                                            publicFunctionQuickInfo = m_provider.PublicFunctionProvider.GetPublicFunctionQuickInfo(splitTokens[i]);
                                            if (publicFunctionQuickInfo != null)
                                            {
                                                applicableToSpan = currentSnapshot.CreateTrackingSpan
                                                        (
                                                            textSpan.Start.Position, splitTokens[i].Length, SpanTrackingMode.EdgeInclusive
                                                        );
                                            }
                                        }

                                        // look at the cursor definitions
                                        CursorDeclaration cursorDecl;
                                        string searchName = splitTokens[i];
                                        if (fpm.ModuleContents.SqlCursors.TryGetValue(searchName, out cursorDecl))
                                        {
                                            // TODO: will have to rework this a bit when we support other types of cursor declarations
                                            searchName = cursorDecl.PreparationVariable;
                                        }
                                        if (fpm.ModuleContents.SqlPrepares.TryGetValue(searchName, out cursorPrep))
                                        {
                                            applicableToSpan = currentSnapshot.CreateTrackingSpan
                                                (
                                                    textSpan.Start.Position, searchName.Length, SpanTrackingMode.EdgeInclusive
                                                );
                                        }

                                        if (fpm.ModuleContents.TempTables.TryGetValue(searchName, out tempTableDef))
                                        {
                                            applicableToSpan = currentSnapshot.CreateTrackingSpan
                                                (
                                                    textSpan.Start.Position, searchName.Length, SpanTrackingMode.EdgeInclusive
                                                );
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            // we're on a member access, so we're limited to mimic types, record types, and (when supported) system types (i.e. sqlca)
                            if (tempDef != null)
                            {
                                if (tempDef.IsMimicType && i == 1)  // can't go more than one level deep
                                {
                                    if (m_provider.DbInformationProvider != null)
                                    {
                                        columnOrRecordField = m_provider.DbInformationProvider.GetTableColumn(tempDef.MimicTypeTable, splitTokens[i]);
                                    }
                                }
                                else if (tempDef.IsRecordType)  // possibly could go many levels deep...in theory the logic here should work.
                                {
                                    // look for splitTokens[i] within the record's element variables
                                    VariableDefinition tempRecDef;
                                    if (tempDef.RecordElements.TryGetValue(splitTokens[i], out tempRecDef))
                                    {
                                        tempDef = tempRecDef;
                                    }
                                }
                                applicableToSpan = currentSnapshot.CreateTrackingSpan
                                            (
                                                textSpan.Start.Position, splitTokens[i].Length, SpanTrackingMode.EdgeInclusive
                                            );
                            }
                        }
                    }
                }

                // Now let's see what we have
                if (tmpMethod != null)
                {
                    qiContent.Add("(class method) " + tmpMethod.Name);
                }
                else if (tmpClass != null)
                {
                    qiContent.Add("(class) " + tmpClass.Name);
                }
                else if (tmpPackage != null)
                {
                    qiContent.Add("(package) " + tmpPackage.Name);
                }
                else if (columnOrRecordField != null)
                {
                    qiContent.Add(columnOrRecordField.Type + " " + (tempDef.MimicTypeTable ?? "") + "." + columnOrRecordField.Name);
                }
                else if (tempDef != null)
                {
                    qiContent.Add(tempDef.GetIntellisenseText(context));
                }
                else if (funcDef != null)
                {
                    qiContent.Add(funcDef.GetIntellisenseText());
                }
                else if (cursorPrep != null)
                {
                    qiContent.Add(cursorPrep.GetIntellisenseText());
                }
                else if (tempTableDef != null)
                {
                    qiContent.Add("(temp table) " + tempTableDef.Name);
                }
                else if (publicFunctionQuickInfo != null)
                {
                    qiContent.Add(publicFunctionQuickInfo);
                }
            }
        }

        private bool m_isDisposed;
        public void Dispose()
        {
            if (!m_isDisposed)
            {
                GC.SuppressFinalize(this);
                m_isDisposed = true;
            }
        }
    }
}