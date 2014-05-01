﻿/* ****************************************************************************
 * Copyright (c) 2014 Greg Fullman 
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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VSGenero.Navigation;

namespace VSGenero.VS2013_Specific
{
    class PeekableItem : IPeekableItem
    {
        private readonly PeekableItemSourceProvider _factory;
        private readonly GoToDefinitionLocation _location;

        public PeekableItem(GoToDefinitionLocation location, PeekableItemSourceProvider factory)
        {
            _location = location;
            _factory = factory;
        }

        public string DisplayName
        {
            get { return null; }
        }

        public IPeekResultSource GetOrCreateResultSource(string relationshipName)
        {
            return new PeekResultSource(this._location, this._factory);
        }

        public IEnumerable<IPeekRelationship> Relationships
        {
            get { yield return PredefinedPeekRelationships.Definitions; }
        }
    }
}