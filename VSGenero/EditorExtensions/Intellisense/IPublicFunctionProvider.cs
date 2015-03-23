﻿/* ****************************************************************************
 * Copyright (c) 2014 Greg Fullman 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution.
 * By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VSGenero.Analysis;

namespace VSGenero.EditorExtensions.Intellisense
{
    public interface IPublicFunctionProvider
    {
        IEnumerable<IGeneroFunctionCollection> GetPublicFunctionCompletions(string completionText, ITextBuffer currentBuffer);
        IFunctionResult GetPublicFunction(string functionName, ITextBuffer currentBuffer);
    }
}
