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
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;

namespace VSGenero.EditorExtensions.Intellisense
{
    /// <summary>
    /// Represents a completion item that can be individually shown and hidden.
    /// A completion set using these as items needs to provide a filter that
    /// checks the Visible property.
    /// </summary>
    public class DynamicallyVisibleCompletion : Completion
    {
        private Func<string> _lazyDescriptionSource;
        private Func<ImageSource> _lazyIconSource;
        private bool _visible, _previouslyVisible;

        /// <summary>
        /// Creates a default completion item.
        /// </summary>
        public DynamicallyVisibleCompletion()
            : base() { }

        /// <summary>
        /// Creates a completion item with the specifed text, which will be
        /// used both for display and insertion.
        /// </summary>
        public DynamicallyVisibleCompletion(string displayText)
            : base(displayText) { }

        /// <summary>
        /// Initializes a new instance with the specified text and description.
        /// </summary>
        /// <param name="displayText">The text that is to be displayed by an
        /// IntelliSense presenter.</param>
        /// <param name="insertionText">The text that is to be inserted into
        /// the buffer if this completion is committed.</param>
        /// <param name="description">A description that can be displayed with
        /// the display text of the completion.</param>
        /// <param name="iconSource">The icon.</param>
        /// <param name="iconAutomationText">The text to be used as the
        /// automation name for the icon.</param>
        public DynamicallyVisibleCompletion(string displayText, string insertionText, string description, ImageSource iconSource, string iconAutomationText)
            : base(displayText, insertionText, description, iconSource, iconAutomationText) { }


        /// <summary>
        /// Initializes a new instance with the specified text, description and
        /// a lazily initialized icon.
        /// </summary>
        /// <param name="displayText">The text that is to be displayed by an
        /// IntelliSense presenter.</param>
        /// <param name="insertionText">The text that is to be inserted into
        /// the buffer if this completion is committed.</param>
        /// <param name="lazyDescriptionSource">A function returning the
        /// description.</param>
        /// <param name="lazyIconSource">A function returning the icon. It will
        /// be called once and the result is cached.</param>
        /// <param name="iconAutomationText">The text to be used as the
        /// automation name for the icon.</param>
        public DynamicallyVisibleCompletion(string displayText, string insertionText, Func<string> lazyDescriptionSource, Func<ImageSource> lazyIconSource, string iconAutomationText)
            : base(displayText, insertionText, null, null, iconAutomationText)
        {
            _lazyDescriptionSource = lazyDescriptionSource;
            _lazyIconSource = lazyIconSource;
            
        }

        /// <summary>
        /// Gets or sets whether the completion item should be shown to the
        /// user.
        /// </summary>
        internal bool Visible
        {
            get
            {
                return _visible;
            }
            set
            {
                _previouslyVisible = _visible;
                _visible = value;
            }
        }

        /// <summary>
        /// Resets <see cref="Visible"/> to its value before it was last set.
        /// </summary>
        internal void UndoVisible()
        {
            _visible = _previouslyVisible;
        }


        // Summary:
        //     Gets a description that can be displayed together with the display text of
        //     the completion.
        //
        // Returns:
        //     The description.

        /// <summary>
        /// Gets a description that can be displayed together with the display
        /// text of the completion.
        /// </summary>
        /// <value>The description.</value>
        public override string Description
        {
            get
            {
                if (base.Description == null && _lazyDescriptionSource != null)
                {
                    base.Description = _lazyDescriptionSource();
                    _lazyDescriptionSource = null;
                }
                return base.Description.LimitLines();
            }
            set
            {
                base.Description = value;
            }
        }

        /// <summary>
        /// Gets or sets an icon that could be used to describe the completion.
        /// </summary>
        /// <value>The icon.</value>
        public override ImageSource IconSource
        {
            get
            {
                if (base.IconSource == null && _lazyIconSource != null)
                {
                    base.IconSource = _lazyIconSource();
                    _lazyIconSource = null;
                }
                return base.IconSource;
            }
            set
            {
                base.IconSource = value;
            }
        }
    }

    /// <summary>
    /// Represents a set of completions filtered and selected using a
    /// <see cref="FuzzyStringMatcher"/>.
    /// </summary>
    public class FuzzyCompletionSet : CompletionSet
    {
        BulkObservableCollection<Completion> _completions;
        WritableFilteredObservableCollection<Completion> _filteredCompletions;
        Completion _previousSelection;
        readonly FuzzyStringMatcher _comparer;
        readonly bool _shouldFilter;
        readonly bool _shouldHideAdvanced;
        readonly IComparer<Completion> _initialComparer;

        private readonly CompletionOptions _options;
        private readonly HashSet<string> _loadedDeferredCompletionSubsets;
        private readonly Thread _deferredLoadThread;

        Func<string, IEnumerable<DynamicallyVisibleCompletion>> _deferredLoadCallback;

        private readonly static Regex _advancedItemPattern = new Regex(
            @"__\w+__($|\s)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
        );

        /// <summary>
        /// Initializes a new instance with the specified properties.
        /// </summary>
        /// <param name="moniker">The unique, non-localized identifier for the
        /// completion set.</param>
        /// <param name="displayName">The localized name of the completion set.
        /// </param>
        /// <param name="applicableTo">The tracking span to which the
        /// completions apply.</param>
        /// <param name="completions">The list of completions.</param>
        /// <param name="options">The options to use for filtering and
        /// selecting items.</param>
        /// <param name="comparer">The comparer to use to order the provided
        /// completions.</param>
        public FuzzyCompletionSet(string moniker, string displayName, ITrackingSpan applicableTo, 
                                  IEnumerable<DynamicallyVisibleCompletion> completions, 
                                  CompletionOptions options, IComparer<Completion> comparer,
                                  Func<string, IEnumerable<DynamicallyVisibleCompletion>> deferredLoadCallback) :
            base(moniker, displayName, applicableTo, null, null)
        {
            _options = options;
            _initialComparer = comparer;
            _completions = new BulkObservableCollection<Completion>();
            _loadedDeferredCompletionSubsets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _deferredLoadCallback = deferredLoadCallback;
            _completions.AddRange(completions
                .Where(c => c != null && !string.IsNullOrWhiteSpace(c.DisplayText))
                .OrderBy(c => c, comparer)
            );
            _comparer = new FuzzyStringMatcher(options.SearchMode);

            _shouldFilter = options.FilterCompletions;
            _shouldHideAdvanced = options.HideAdvancedMembers && !_completions.All(IsAdvanced);

            if (_shouldFilter | _shouldHideAdvanced)
            {
                _filteredCompletions = new WritableFilteredObservableCollection<Completion>(_completions);

                foreach (var c in _completions.Cast<DynamicallyVisibleCompletion>())
                {
                    c.Visible = !_shouldHideAdvanced || !IsAdvanced(c);
                }
                _filteredCompletions.Filter(IsVisible);
            }
        }

        private static bool IsAdvanced(Completion comp)
        {
            return _advancedItemPattern.IsMatch(comp.DisplayText);
        }

        /// <summary>
        /// Gets or sets the list of completions that are part of this completion set.
        /// </summary>
        /// <value>
        /// A list of <see cref="Completion"/> objects.
        /// </value>
        public override IList<Completion> Completions
        {
            get
            {
                return (IList<Completion>)_filteredCompletions ?? _completions;
            }
        }

        private static bool IsVisible(Completion completion)
        {
            return ((DynamicallyVisibleCompletion)completion).Visible;
        }

        public override void Recalculate()
        {
            base.Recalculate();
        }

        /// <summary>
        /// Restricts the set of completions to those that match the applicability text
        /// of the completion set, and then determines the best match.
        /// </summary>
        public override async void Filter()
        {
            if (_filteredCompletions == null)
            {
                foreach (var c in _completions.Cast<DynamicallyVisibleCompletion>())
                {
                    c.Visible = true;
                }
                return;
            }

            var text = ApplicableTo.GetText(ApplicableTo.TextBuffer.CurrentSnapshot);
            if (text.Length > 0)
            {
                bool anyVisible = false;
                foreach (var c in _completions.Cast<DynamicallyVisibleCompletion>())
                {
                    if (_shouldHideAdvanced && IsAdvanced(c) && !text.StartsWith("__"))
                    {
                        c.Visible = false;
                    }
                    else if (_shouldFilter)
                    {
                        c.Visible = _comparer.IsCandidateMatch(c.DisplayText, text);
                    }
                    else
                    {
                        c.Visible = true;
                    }
                    anyVisible |= c.Visible;
                }
                if (!anyVisible)
                {
                    foreach (var c in _completions.Cast<DynamicallyVisibleCompletion>())
                    {
                        // UndoVisible only works reliably because we always
                        // set Visible in the previous loop.
                        c.UndoVisible();
                    }
                }
                _filteredCompletions.Filter(IsVisible);

                if (_options.DeferredLoadPreCharacters > 0)
                {
                    var useText = text;
                    if(text.Length > _options.DeferredLoadPreCharacters)
                    {
                        useText = text.Substring(0, _options.DeferredLoadPreCharacters);
                        if(_loadedDeferredCompletionSubsets.Contains(useText))
                            return;
                    }
                    else if(text.Length < _options.DeferredLoadPreCharacters)
                    {
                        return;
                    }
                    
                    // check to see if the deferred load has been done already
                    if (_deferredLoadCallback != null && !_loadedDeferredCompletionSubsets.Contains(useText))
                    {
                        _loadedDeferredCompletionSubsets.Add(text);
                        var completions = await Task.Factory.StartNew<IList<DynamicallyVisibleCompletion>>(() => _deferredLoadCallback(text).ToList());
                        _filteredCompletions.AddRange(completions);

                        SelectBestMatch();
                    }
                }
            }
            else if (_shouldHideAdvanced)
            {
                foreach (var c in _completions.Cast<DynamicallyVisibleCompletion>())
                {
                    c.Visible = !IsAdvanced(c);
                }
                _filteredCompletions.Filter(IsVisible);
            }
            else
            {
                foreach (var c in _completions.Cast<DynamicallyVisibleCompletion>())
                {
                    c.Visible = true;
                }
                _filteredCompletions.StopFiltering();
            }
        }

        /// <summary>
        /// Determines the best match in the completion set.
        /// </summary>
        public override void SelectBestMatch()
        {
            var text = ApplicableTo.GetText(ApplicableTo.TextBuffer.CurrentSnapshot);

            Completion bestMatch = _previousSelection;
            int bestValue = 0;
            bool isUnique = true;
            bool allowSelect = true;

            if (!string.IsNullOrWhiteSpace(text))
            {
                // Using the Completions property to only search through visible
                // completions.
                foreach (var comp in Completions)
                {
                    int value = _comparer.GetSortKey(comp.DisplayText, text);
                    if (bestMatch == null || value > bestValue)
                    {
                        bestMatch = comp;
                        bestValue = value;
                        isUnique = true;
                    }
                    else if (value == bestValue)
                    {
                        isUnique = false;
                    }
                }
            }
            else
            {
                foreach (var comp in Completions)
                {
                    int temp;
                    if(IntellisenseExtensions.LastCommittedCompletions.TryGetValue(comp.DisplayText, out temp))
                    {
                        if(bestMatch == null || temp > bestValue)
                        {
                            bestMatch = comp;
                            bestValue = temp;
                            isUnique = true;
                        }
                    }
                }
            }

            if (bestMatch == null)
            {
                bestMatch = Completions[0];
            }

            if (((DynamicallyVisibleCompletion)bestMatch).Visible)
            {
                SelectionStatus = new CompletionSelectionStatus(bestMatch,
                    isSelected: allowSelect && bestValue > 0,
                    isUnique: isUnique);
            }
            else
            {
                SelectionStatus = new CompletionSelectionStatus(null,
                    isSelected: false,
                    isUnique: false);
            }

            _previousSelection = bestMatch;
        }

        /// <summary>
        /// Determines and selects the only match in the completion set.
        /// This ignores the user's filtering preferences.
        /// </summary>
        /// <returns>
        /// True if a match is found and selected; otherwise, false if there
        /// is no single match in the completion set.
        /// </returns> 
        public bool SelectSingleBest()
        {
            var text = ApplicableTo.GetText(ApplicableTo.TextBuffer.CurrentSnapshot);

            Completion bestMatch = null;

            // Using the _completions field to search through all completions
            // and ignore filtering settings.
            foreach (var comp in _completions)
            {
                if (_comparer.IsCandidateMatch(comp.DisplayText, text))
                {
                    if (bestMatch == null)
                    {
                        bestMatch = comp;
                    }
                    else
                    {
                        return false;
                    }
                }
            }

            if (bestMatch != null)
            {
                SelectionStatus = new CompletionSelectionStatus(bestMatch,
                    isSelected: true,
                    isUnique: true);
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
