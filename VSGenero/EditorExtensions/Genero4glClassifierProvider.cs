﻿using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VSGenero.Analysis;

namespace VSGenero.EditorExtensions
{
    [Export(typeof(IClassifierProvider)), ContentType(VSGeneroConstants.ContentType4GL)]
    internal class Genero4glClassifierProvider : IClassifierProvider
    {
        private Dictionary<TokenCategory, IClassificationType> _categoryMap;
        private IClassificationType _comment;
        private IClassificationType _stringLiteral;
        private IClassificationType _keyword;
        private IClassificationType _operator;
        private IClassificationType _groupingClassification;
        private IClassificationType _dotClassification;
        private IClassificationType _commaClassification;
        private readonly IContentType _type;

        [ImportingConstructor]
        public Genero4glClassifierProvider(IContentTypeRegistryService contentTypeRegistryService)
        {
            _type = contentTypeRegistryService.GetContentType(VSGeneroConstants.ContentType4GL);
        }

        /// <summary>
        /// Import the classification registry to be used for getting a reference
        /// to the custom classification type later.
        /// </summary>
        [Import]
        public IClassificationTypeRegistryService _classificationRegistry = null; // Set via MEF

        #region Python Classification Type Definitions

        [Export]
        [Name(Genero4glPredefinedClassificationTypeNames.Grouping)]
        [BaseDefinition(Genero4glPredefinedClassificationTypeNames.Operator)]
        internal static ClassificationTypeDefinition GroupingClassificationDefinition = null; // Set via MEF

        [Export]
        [Name(Genero4glPredefinedClassificationTypeNames.Dot)]
        [BaseDefinition(Genero4glPredefinedClassificationTypeNames.Operator)]
        internal static ClassificationTypeDefinition DotClassificationDefinition = null; // Set via MEF

        [Export]
        [Name(Genero4glPredefinedClassificationTypeNames.Comma)]
        [BaseDefinition(Genero4glPredefinedClassificationTypeNames.Operator)]
        internal static ClassificationTypeDefinition CommaClassificationDefinition = null; // Set via MEF

        [Export]
        [Name(Genero4glPredefinedClassificationTypeNames.Operator)]
#if VS120
        [BaseDefinition(PredefinedClassificationTypeNames.Operator)]
#else
        [BaseDefinition(PredefinedClassificationTypeNames.FormalLanguage)]
#endif
        internal static ClassificationTypeDefinition OperatorClassificationDefinition = null; // Set via MEF

        #endregion

         #region IDlrClassifierProvider

        public IClassifier GetClassifier(ITextBuffer buffer) {
            if (_categoryMap == null) {
                _categoryMap = FillCategoryMap(_classificationRegistry);
            }

            Genero4glClassifier res;
            if (!buffer.Properties.TryGetProperty<Genero4glClassifier>(typeof(Genero4glClassifier), out res) &&
                buffer.ContentType.IsOfType(ContentType.TypeName)) {
                res = new Genero4glClassifier(this, buffer);
                buffer.Properties.AddProperty(typeof(Genero4glClassifier), res);
            }

            return res;
        }

        public virtual IContentType ContentType {
            get { return _type; }
        }

        public IClassificationType Comment {
            get { return _comment; }
        }

        public IClassificationType StringLiteral {
            get { return _stringLiteral; }
        }

        public IClassificationType Keyword {
            get { return _keyword; }
        }

        public IClassificationType Operator {
            get { return _operator; }
        }

        public IClassificationType GroupingClassification {
            get { return _groupingClassification; }
        }

        public IClassificationType DotClassification {
            get { return _dotClassification; }
        }

        public IClassificationType CommaClassification {
            get { return _commaClassification; }
        }

        #endregion

        internal Dictionary<TokenCategory, IClassificationType> CategoryMap {
            get { return _categoryMap; }
        }

        private Dictionary<TokenCategory, IClassificationType> FillCategoryMap(IClassificationTypeRegistryService registry) {
            var categoryMap = new Dictionary<TokenCategory, IClassificationType>();

            //categoryMap[TokenCategory.DocComment] = _comment = registry.GetClassificationType(PredefinedClassificationTypeNames.Comment);
            categoryMap[TokenCategory.LineComment] = registry.GetClassificationType(PredefinedClassificationTypeNames.Comment);
            categoryMap[TokenCategory.Comment] = _comment = registry.GetClassificationType(PredefinedClassificationTypeNames.Comment);
            categoryMap[TokenCategory.NumericLiteral] = registry.GetClassificationType(PredefinedClassificationTypeNames.Literal);
            categoryMap[TokenCategory.CharacterLiteral] = registry.GetClassificationType(PredefinedClassificationTypeNames.Character);
            categoryMap[TokenCategory.StringLiteral] = _stringLiteral = registry.GetClassificationType(PredefinedClassificationTypeNames.String);
            categoryMap[TokenCategory.Keyword] = _keyword = registry.GetClassificationType(PredefinedClassificationTypeNames.Keyword);
            //categoryMap[TokenCategory.Directive] = registry.GetClassificationType(PredefinedClassificationTypeNames.Keyword);
            categoryMap[TokenCategory.Identifier] = registry.GetClassificationType(PredefinedClassificationTypeNames.Identifier);
            categoryMap[TokenCategory.Operator] = _operator = registry.GetClassificationType(Genero4glPredefinedClassificationTypeNames.Operator);
            categoryMap[TokenCategory.Delimiter] = registry.GetClassificationType(Genero4glPredefinedClassificationTypeNames.Operator);
            categoryMap[TokenCategory.Grouping] = registry.GetClassificationType(Genero4glPredefinedClassificationTypeNames.Operator);
            categoryMap[TokenCategory.WhiteSpace] = registry.GetClassificationType(PredefinedClassificationTypeNames.WhiteSpace);
            //categoryMap[TokenCategory.RegularExpressionLiteral] = registry.GetClassificationType(PredefinedClassificationTypeNames.Literal);
            _groupingClassification = registry.GetClassificationType(Genero4glPredefinedClassificationTypeNames.Grouping);
            _commaClassification = registry.GetClassificationType(Genero4glPredefinedClassificationTypeNames.Comma);
            _dotClassification = registry.GetClassificationType(Genero4glPredefinedClassificationTypeNames.Dot);

            return categoryMap;
        }

        #region Editor Format Definitions

        [Export(typeof(EditorFormatDefinition))]
        [ClassificationType(ClassificationTypeNames = Genero4glPredefinedClassificationTypeNames.Operator)]
        [Name(Genero4glPredefinedClassificationTypeNames.Operator)]
        [DisplayName(Genero4glPredefinedClassificationTypeNames.Operator)]
        [UserVisible(true)]
        [Order(After = LanguagePriority.NaturalLanguage, Before = LanguagePriority.FormalLanguage)]
        internal sealed class OperatorFormat : ClassificationFormatDefinition
        {
            public OperatorFormat() { }
        }

        [Export(typeof(EditorFormatDefinition))]
        [ClassificationType(ClassificationTypeNames = Genero4glPredefinedClassificationTypeNames.Grouping)]
        [Name(Genero4glPredefinedClassificationTypeNames.Grouping)]
        [DisplayName(Genero4glPredefinedClassificationTypeNames.Grouping)]
        [UserVisible(true)]
        [Order(After = LanguagePriority.NaturalLanguage, Before = LanguagePriority.FormalLanguage)]
        internal sealed class GroupingFormat : ClassificationFormatDefinition
        {
            public GroupingFormat() { }
        }

        [Export(typeof(EditorFormatDefinition))]
        [ClassificationType(ClassificationTypeNames = Genero4glPredefinedClassificationTypeNames.Comma)]
        [Name(Genero4glPredefinedClassificationTypeNames.Comma)]
        [DisplayName(Genero4glPredefinedClassificationTypeNames.Comma)]
        [UserVisible(true)]
        [Order(After = LanguagePriority.NaturalLanguage, Before = LanguagePriority.FormalLanguage)]
        internal sealed class CommaFormat : ClassificationFormatDefinition
        {
            public CommaFormat() { }
        }

        [Export(typeof(EditorFormatDefinition))]
        [ClassificationType(ClassificationTypeNames = Genero4glPredefinedClassificationTypeNames.Dot)]
        [Name(Genero4glPredefinedClassificationTypeNames.Dot)]
        [DisplayName(Genero4glPredefinedClassificationTypeNames.Dot)]
        [UserVisible(true)]
        [Order(After = LanguagePriority.NaturalLanguage, Before = LanguagePriority.FormalLanguage)]
        internal sealed class DotFormat : ClassificationFormatDefinition
        {
            public DotFormat() { }
        }

        #endregion
    }
}
