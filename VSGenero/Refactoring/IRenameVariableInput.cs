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

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VSGenero.Refactoring
{
    /// <summary>
    /// Provides inputs/UI to the extract method refactoring.  Enables driving of the refactoring programmatically
    /// or via UI.
    /// </summary>
    interface IRenameVariableInput
    {
        RenameVariableRequest GetRenameInfo(string originalName);

        void CannotRename(string message);

        void ClearRefactorPane();

        void OutputLog(string message);

        ITextBuffer GetBufferForDocument(string filename);

        IVsLinkedUndoTransactionManager BeginGlobalUndo();

        void EndGlobalUndo(IVsLinkedUndoTransactionManager undo);

    }
}
