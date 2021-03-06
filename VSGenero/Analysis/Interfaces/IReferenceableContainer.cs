﻿
/* ****************************************************************************
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

namespace VSGenero.Analysis
{
    interface IReferenceableContainer
    {
        IEnumerable<IReferenceable> GetDefinitions(string name);
    }

    interface IReferenceable
    {
        IEnumerable<KeyValuePair<IProjectEntry, LocationInfo>> Definitions
        {
            get;
        }
        IEnumerable<KeyValuePair<IProjectEntry, LocationInfo>> References
        {
            get;
        }
    }
}
