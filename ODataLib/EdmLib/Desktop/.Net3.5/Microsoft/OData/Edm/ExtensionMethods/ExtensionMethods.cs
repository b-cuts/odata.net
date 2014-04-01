//   OData .NET Libraries
//   Copyright (c) Microsoft Corporation
//   All rights reserved. 

//   Licensed under the Apache License, Version 2.0 (the ""License""); you may not use this file except in compliance with the License. You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0 

//   THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE, MERCHANTABLITY OR NON-INFRINGEMENT. 

//   See the Apache Version 2.0 License for specific language governing permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Microsoft.OData.Edm.Annotations;
using Microsoft.OData.Edm.Csdl;
using Microsoft.OData.Edm.Csdl.Serialization;
using Microsoft.OData.Edm.Expressions;
using Microsoft.OData.Edm.Library;
using Microsoft.OData.Edm.Validation;
using Microsoft.OData.Edm.Values;
using ErrorStrings = Microsoft.OData.Edm.Strings;

namespace Microsoft.OData.Edm
{
    /// <summary>
    /// Contains extension methods for <see cref="IEdmModel"/> interfaces.
    /// </summary>
    public static class ExtensionMethods
    {
        private const string CollectionTypeFormat = EdmConstants.Type_Collection + "({0})";

        #region IEdmModel

        private static readonly Func<IEdmModel, string, IEdmSchemaType> findType = (model, qualifiedName) => model.FindDeclaredType(qualifiedName);
        private static readonly Func<IEdmModel, string, IEdmValueTerm> findValueTerm = (model, qualifiedName) => model.FindDeclaredValueTerm(qualifiedName);
        private static readonly Func<IEdmModel, string, IEnumerable<IEdmOperation>> findOperations = (model, qualifiedName) => model.FindDeclaredOperations(qualifiedName);
        private static readonly Func<IEdmModel, string, IEdmEntityContainer> findEntityContainer = (model, qualifiedName) => { return model.ExistsContainer(qualifiedName) ? model.EntityContainer : null; };
        private static readonly Func<IEnumerable<IEdmOperation>, IEnumerable<IEdmOperation>, IEnumerable<IEdmOperation>> mergeFunctions = (f1, f2) => Enumerable.Concat(f1, f2);

        /// <summary>
        /// Gets the value for the EDM version of the <paramref name="model"/>.
        /// </summary>
        /// <param name="model">Model the version has been set for.</param>
        /// <returns>The version.</returns>
        public static Version GetEdmVersion(this IEdmModel model)
        {
            EdmUtil.CheckArgumentNull(model, "model");
            return model.GetAnnotationValue<Version>(model, EdmConstants.InternalUri, EdmConstants.EdmVersionAnnotation);
        }

        /// <summary>
        /// Sets a value of EDM version attribute of the <paramref name="model"/>.
        /// </summary>
        /// <param name="model">The model the version should be set for.</param>
        /// <param name="version">The version.</param>
        public static void SetEdmVersion(this IEdmModel model, Version version)
        {
            EdmUtil.CheckArgumentNull(model, "model");
            model.SetAnnotationValue(model, EdmConstants.InternalUri, EdmConstants.EdmVersionAnnotation, version);
        }

        /// <summary>
        /// Searches for a type with the given name in this model and all referenced models and returns null if no such type exists.
        /// </summary>
        /// <param name="model">The model to search.</param>
        /// <param name="qualifiedName">The qualified name of the type being found.</param>
        /// <returns>The requested type, or null if no such type exists.</returns>
        public static IEdmSchemaType FindType(this IEdmModel model, string qualifiedName)
        {
            EdmUtil.CheckArgumentNull(model, "model");
            EdmUtil.CheckArgumentNull(qualifiedName, "qualifiedName");

            return FindAcrossModels(model, qualifiedName, findType, RegistrationHelper.CreateAmbiguousTypeBinding);
        }

        /// <summary>
        /// Searches for a value term with the given name in this model and all referenced models and returns null if no such value term exists.
        /// </summary>
        /// <param name="model">The model to search.</param>
        /// <param name="qualifiedName">The qualified name of the value term being found.</param>
        /// <returns>The requested value term, or null if no such value term exists.</returns>
        public static IEdmValueTerm FindValueTerm(this IEdmModel model, string qualifiedName)
        {
            EdmUtil.CheckArgumentNull(model, "model");
            EdmUtil.CheckArgumentNull(qualifiedName, "qualifiedName");

            return FindAcrossModels(model, qualifiedName, findValueTerm, RegistrationHelper.CreateAmbiguousValueTermBinding);
        }

        /// <summary>
        /// Searches for operations with the given name in this model and all referenced models and returns an empty enumerable if no such operations exist.
        /// </summary>
        /// <param name="model">The model to search.</param>
        /// <param name="qualifiedName">The qualified name of the operations being found.</param>
        /// <returns>The requested operations.</returns>
        public static IEnumerable<IEdmOperation> FindOperations(this IEdmModel model, string qualifiedName)
        {
            EdmUtil.CheckArgumentNull(model, "model");
            EdmUtil.CheckArgumentNull(qualifiedName, "qualifiedName");

            return FindAcrossModels(model, qualifiedName, findOperations, mergeFunctions);
        }

        /// <summary>
        /// If the container name in the model is the same as the input name. The input name maybe full qualified name.
        /// </summary>
        /// <param name="model">The model to search.</param>
        /// <param name="containerName">Input container name to be searched. The container name may be full qualified with namesapce prefix.</param>
        /// <returns>True if the model has a container called input name, otherwise false.</returns>
        public static bool ExistsContainer(this IEdmModel model, string containerName)
        {
            if (model.EntityContainer == null)
            {
                return false;
            }

            string fullQulifiedName = (model.EntityContainer.Namespace ?? String.Empty) + "." + (containerName ?? String.Empty);

            if (string.Equals(model.EntityContainer.FullName(), fullQulifiedName, StringComparison.Ordinal)
                || string.Equals(model.EntityContainer.FullName(), containerName, StringComparison.Ordinal))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Searches for an entity container with the given name in this model and all referenced models and returns null if no such entity container exists.
        /// </summary>
        /// <param name="model">The model to search.</param>
        /// <param name="qualifiedName">The qualified name of the entity container being found.</param>
        /// <returns>The requested entity container, or null if no such entity container exists.</returns>
        public static IEdmEntityContainer FindEntityContainer(this IEdmModel model, string qualifiedName)
        {
            EdmUtil.CheckArgumentNull(model, "model");
            EdmUtil.CheckArgumentNull(qualifiedName, "qualifiedName");

            return FindAcrossModels(model, qualifiedName, findEntityContainer, RegistrationHelper.CreateAmbiguousEntityContainerBinding);
        }

        /// <summary>
        /// Gets an annotatable element's vocabulary annotations defined in a specific model and models referenced by that model.
        /// </summary>
        /// <param name="model">The model to search.</param>
        /// <param name="element">Element to check for annotations.</param>
        /// <returns>Annotations attached to the element (or, if the element is a type, to its base types) by this model or by models referenced by this model.</returns>
        public static IEnumerable<IEdmVocabularyAnnotation> FindVocabularyAnnotationsIncludingInheritedAnnotations(this IEdmModel model, IEdmVocabularyAnnotatable element)
        {
            EdmUtil.CheckArgumentNull(model, "model");
            EdmUtil.CheckArgumentNull(element, "element");

            IEnumerable<IEdmVocabularyAnnotation> result = model.FindDeclaredVocabularyAnnotations(element);

            IEdmStructuredType typeElement = element as IEdmStructuredType;
            if (typeElement != null)
            {
                typeElement = typeElement.BaseType;
                while (typeElement != null)
                {
                    IEdmVocabularyAnnotatable annotatableElement = typeElement as IEdmVocabularyAnnotatable;
                    if (annotatableElement != null)
                    {
                        result = result.Concat(model.FindDeclaredVocabularyAnnotations(annotatableElement));
                    }

                    typeElement = typeElement.BaseType;
                }
            }

            return result;
        }

        /// <summary>
        /// Gets an annotatable element's vocabulary annotations defined in a specific model and models referenced by that model.
        /// </summary>
        /// <param name="model">The model to search.</param>
        /// <param name="element">Element to check for annotations.</param>
        /// <returns>Annotations attached to the element by this model or by models referenced by this model.</returns>
        public static IEnumerable<IEdmVocabularyAnnotation> FindVocabularyAnnotations(this IEdmModel model, IEdmVocabularyAnnotatable element)
        {
            EdmUtil.CheckArgumentNull(model, "model");
            EdmUtil.CheckArgumentNull(element, "element");

            IEnumerable<IEdmVocabularyAnnotation> result = model.FindVocabularyAnnotationsIncludingInheritedAnnotations(element);
            foreach (IEdmModel referencedModel in model.ReferencedModels)
            {
                result = result.Concat(referencedModel.FindVocabularyAnnotationsIncludingInheritedAnnotations(element));
            }

            return result;
        }

        /// <summary>
        /// Gets an annotatable element's vocabulary annotations that bind a particular term.
        /// </summary>
        /// <typeparam name="T">Type of the annotation being returned.</typeparam>
        /// <param name="model">Model to search.</param>
        /// <param name="element">Element to check for annotations.</param>
        /// <param name="term">Term to search for.</param>
        /// <returns>Annotations attached to the element by this model or by models referenced by this model that bind the term.</returns>
        public static IEnumerable<T> FindVocabularyAnnotations<T>(this IEdmModel model, IEdmVocabularyAnnotatable element, IEdmTerm term) where T : IEdmVocabularyAnnotation
        {
            EdmUtil.CheckArgumentNull(model, "model");
            EdmUtil.CheckArgumentNull(element, "element");
            EdmUtil.CheckArgumentNull(term, "term");

            return FindVocabularyAnnotations<T>(model, element, term, null);
        }

        /// <summary>
        /// Gets an annotatable element's vocabulary annotations that bind a particular term.
        /// </summary>
        /// <typeparam name="T">Type of the annotation being returned.</typeparam>
        /// <param name="model">Model to search.</param>
        /// <param name="element">Element to check for annotations.</param>
        /// <param name="term">Term to search for.</param>
        /// <param name="qualifier">Qualifier to apply.</param>
        /// <returns>Annotations attached to the element by this model or by models referenced by this model that bind the term with the given qualifier.</returns>
        public static IEnumerable<T> FindVocabularyAnnotations<T>(this IEdmModel model, IEdmVocabularyAnnotatable element, IEdmTerm term, string qualifier) where T : IEdmVocabularyAnnotation
        {
            EdmUtil.CheckArgumentNull(model, "model");
            EdmUtil.CheckArgumentNull(element, "element");
            EdmUtil.CheckArgumentNull(term, "term");

            List<T> result = null;

            foreach (T annotation in model.FindVocabularyAnnotations(element).OfType<T>())
            {
                if (annotation.Term == term && (qualifier == null || qualifier == annotation.Qualifier))
                {
                    if (result == null)
                    {
                        result = new List<T>();
                    }

                    result.Add(annotation);
                }
            }

            return result ?? Enumerable.Empty<T>();
        }

        /// <summary>
        /// Gets an annotatable element's vocabulary annotations that bind a particular term.
        /// </summary>
        /// <typeparam name="T">Type of the annotation being returned.</typeparam>
        /// <param name="model">Model to search.</param>
        /// <param name="element">Element to check for annotations.</param>
        /// <param name="termName">Name of the term to search for.</param>
        /// <returns>Annotations attached to the element by this model or by models referenced by this model that bind the term.</returns>
        public static IEnumerable<T> FindVocabularyAnnotations<T>(this IEdmModel model, IEdmVocabularyAnnotatable element, string termName) where T : IEdmVocabularyAnnotation
        {
            EdmUtil.CheckArgumentNull(model, "model");
            EdmUtil.CheckArgumentNull(element, "element");
            EdmUtil.CheckArgumentNull(termName, "termName");

            return FindVocabularyAnnotations<T>(model, element, termName, null);
        }

        /// <summary>
        /// Gets an annotatable element's vocabulary annotations that bind a particular term.
        /// </summary>
        /// <typeparam name="T">Type of the annotation being returned.</typeparam>
        /// <param name="model">Model to search.</param>
        /// <param name="element">Element to check for annotations.</param>
        /// <param name="termName">Name of the term to search for.</param>
        /// <param name="qualifier">Qualifier to apply.</param>
        /// <returns>Annotations attached to the element by this model or by models referenced by this model that bind the term with the given qualifier.</returns>
        public static IEnumerable<T> FindVocabularyAnnotations<T>(this IEdmModel model, IEdmVocabularyAnnotatable element, string termName, string qualifier) where T : IEdmVocabularyAnnotation
        {
            EdmUtil.CheckArgumentNull(model, "model");
            EdmUtil.CheckArgumentNull(element, "element");
            EdmUtil.CheckArgumentNull(termName, "termName");
            
            // Look up annotations on the element by name. There's no particular advantage in searching for a term first.
            string name;
            string namespaceName;

            if (EdmUtil.TryGetNamespaceNameFromQualifiedName(termName, out namespaceName, out name))
            {
                foreach (T annotation in model.FindVocabularyAnnotations(element).OfType<T>())
                {
                    IEdmTerm annotationTerm = annotation.Term;
                    if (annotationTerm.Namespace == namespaceName && annotationTerm.Name == name && (qualifier == null || qualifier == annotation.Qualifier))
                    {
                        yield return annotation;
                    }
                }
            }
        }

        /// <summary>
        /// Gets the <see cref="IEdmValue "/> of a vocabulary term that has been applied to the type of a value.
        /// </summary>
        /// <param name="model">Model to search for term annotations.</param>
        /// <param name="context">Value to use as context in evaluation.</param>
        /// <param name="termName">Name of the term to evaluate.</param>
        /// <param name="expressionEvaluator">Evaluator to use to perform expression evaluation.</param>
        /// <returns>Value of the term evaluated against the supplied value.</returns>
        public static IEdmValue GetTermValue(this IEdmModel model, IEdmStructuredValue context, string termName, Evaluation.EdmExpressionEvaluator expressionEvaluator)
        {
            EdmUtil.CheckArgumentNull(model, "model");
            EdmUtil.CheckArgumentNull(context, "context");
            EdmUtil.CheckArgumentNull(termName, "termName");
            EdmUtil.CheckArgumentNull(expressionEvaluator, "expressionEvaluator");

            return GetTermValue<IEdmValue>(model, context, context.Type.AsEntity().EntityDefinition(), termName, null, expressionEvaluator.Evaluate);
        }

        /// <summary>
        /// Gets the <see cref="IEdmValue "/> of a vocabulary term that has been applied to the type of a value.
        /// </summary>
        /// <param name="model">Model to search for term annotations.</param>
        /// <param name="context">Value to use as context in evaluation.</param>
        /// <param name="termName">Name of the term to evaluate.</param>
        /// <param name="qualifier">Qualifier to apply.</param>
        /// <param name="expressionEvaluator">Evaluator to use to perform expression evaluation.</param>
        /// <returns>Value of the term evaluated against the supplied value.</returns>
        public static IEdmValue GetTermValue(this IEdmModel model, IEdmStructuredValue context, string termName, string qualifier, Evaluation.EdmExpressionEvaluator expressionEvaluator)
        {
            EdmUtil.CheckArgumentNull(model, "model");
            EdmUtil.CheckArgumentNull(context, "context");
            EdmUtil.CheckArgumentNull(termName, "termName");
            EdmUtil.CheckArgumentNull(expressionEvaluator, "expressionEvaluator");

            return GetTermValue<IEdmValue>(model, context, context.Type.AsEntity().EntityDefinition(), termName, qualifier, expressionEvaluator.Evaluate);
        }

        /// <summary>
        /// Gets the <see cref="IEdmValue "/> of a vocabulary term that has been applied to the type of a value.
        /// </summary>
        /// <param name="model">Model to search for term annotations.</param>
        /// <param name="context">Value to use as context in evaluation.</param>
        /// <param name="term">Term to evaluate.</param>
        /// <param name="expressionEvaluator">Evaluator to use to perform expression evaluation.</param>
        /// <returns>Value of the term evaluated against the supplied value.</returns>
        public static IEdmValue GetTermValue(this IEdmModel model, IEdmStructuredValue context, IEdmValueTerm term, Evaluation.EdmExpressionEvaluator expressionEvaluator)
        {
            EdmUtil.CheckArgumentNull(model, "model");
            EdmUtil.CheckArgumentNull(context, "context");
            EdmUtil.CheckArgumentNull(term, "term");
            EdmUtil.CheckArgumentNull(expressionEvaluator, "expressionEvaluator");

            return GetTermValue<IEdmValue>(model, context, context.Type.AsEntity().EntityDefinition(), term, null, expressionEvaluator.Evaluate);
        }

        /// <summary>
        /// Gets the <see cref="IEdmValue "/> of a vocabulary term that has been applied to the type of a value.
        /// </summary>
        /// <param name="model">Model to search for term annotations.</param>
        /// <param name="context">Value to use as context in evaluation.</param>
        /// <param name="term">Term to evaluate.</param>
        /// <param name="qualifier">Qualifier to apply.</param>
        /// <param name="expressionEvaluator">Evaluator to use to perform expression evaluation.</param>
        /// <returns>Value of the term evaluated against the supplied value.</returns>
        public static IEdmValue GetTermValue(this IEdmModel model, IEdmStructuredValue context, IEdmValueTerm term, string qualifier, Evaluation.EdmExpressionEvaluator expressionEvaluator)
        {
            EdmUtil.CheckArgumentNull(model, "model");
            EdmUtil.CheckArgumentNull(context, "context");
            EdmUtil.CheckArgumentNull(term, "term");
            EdmUtil.CheckArgumentNull(expressionEvaluator, "expressionEvaluator");

            return GetTermValue<IEdmValue>(model, context, context.Type.AsEntity().EntityDefinition(), term, qualifier, expressionEvaluator.Evaluate);
        }

        /// <summary>
        /// Gets the CLR value of a vocabulary term that has been applied to the type of a value.
        /// </summary>
        /// <typeparam name="T">The CLR type of the value to be returned.</typeparam>
        /// <param name="model">Model to search for term annotations.</param>
        /// <param name="context">Value to use as context in evaluation.</param>
        /// <param name="termName">Name of the term to evaluate.</param>
        /// <param name="evaluator">Evaluator to use to perform expression evaluation.</param>
        /// <returns>Value of the term evaluated against the supplied value.</returns>
        public static T GetTermValue<T>(this IEdmModel model, IEdmStructuredValue context, string termName, Evaluation.EdmToClrEvaluator evaluator)
        {
            EdmUtil.CheckArgumentNull(model, "model");
            EdmUtil.CheckArgumentNull(context, "context");
            EdmUtil.CheckArgumentNull(termName, "termName");
            EdmUtil.CheckArgumentNull(evaluator, "evaluator");

            return GetTermValue<T>(model, context, context.Type.AsEntity().EntityDefinition(), termName, null, evaluator.EvaluateToClrValue<T>);
        }

        /// <summary>
        /// Gets the CLR value of a vocabulary term that has been applied to the type of a value.
        /// </summary>
        /// <typeparam name="T">The CLR type of the value to be returned.</typeparam>
        /// <param name="model">Model to search for term annotations.</param>
        /// <param name="context">Value to use as context in evaluation.</param>
        /// <param name="termName">Name of the term to evaluate.</param>
        /// <param name="qualifier">Qualifier to apply.</param>
        /// <param name="evaluator">Evaluator to use to perform expression evaluation.</param>
        /// <returns>Value of the term evaluated against the supplied value.</returns>
        public static T GetTermValue<T>(this IEdmModel model, IEdmStructuredValue context, string termName, string qualifier, Evaluation.EdmToClrEvaluator evaluator)
        {
            EdmUtil.CheckArgumentNull(model, "model");
            EdmUtil.CheckArgumentNull(context, "context");
            EdmUtil.CheckArgumentNull(termName, "termName");
            EdmUtil.CheckArgumentNull(evaluator, "evaluator");

            return GetTermValue<T>(model, context, context.Type.AsEntity().EntityDefinition(), termName, qualifier, evaluator.EvaluateToClrValue<T>);
        }

        /// <summary>
        /// Gets the CLR value of a vocabulary term that has been applied to the type of a value.
        /// </summary>
        /// <typeparam name="T">The CLR type of the value to be returned.</typeparam>
        /// <param name="model">Model to search for term annotations.</param>
        /// <param name="context">Value to use as context in evaluation.</param>
        /// <param name="term">Term to evaluate.</param>
        /// <param name="evaluator">Evaluator to use to perform expression evaluation.</param>
        /// <returns>Value of the term evaluated against the supplied value.</returns>
        public static T GetTermValue<T>(this IEdmModel model, IEdmStructuredValue context, IEdmValueTerm term, Evaluation.EdmToClrEvaluator evaluator)
        {
            EdmUtil.CheckArgumentNull(model, "model");
            EdmUtil.CheckArgumentNull(context, "context");
            EdmUtil.CheckArgumentNull(term, "term");
            EdmUtil.CheckArgumentNull(evaluator, "evaluator");

            return GetTermValue<T>(model, context, context.Type.AsEntity().EntityDefinition(), term, null, evaluator.EvaluateToClrValue<T>);
        }

        /// <summary>
        /// Gets the CLR value of a vocabulary term that has been applied to the type of a value.
        /// </summary>
        /// <typeparam name="T">The CLR type of the value to be returned.</typeparam>
        /// <param name="model">Model to search for term annotations.</param>
        /// <param name="context">Value to use as context in evaluation.</param>
        /// <param name="term">Term to evaluate.</param>
        /// <param name="qualifier">Qualifier to apply.</param>
        /// <param name="evaluator">Evaluator to use to perform expression evaluation.</param>
        /// <returns>Value of the term evaluated against the supplied value.</returns>
        public static T GetTermValue<T>(this IEdmModel model, IEdmStructuredValue context, IEdmValueTerm term, string qualifier, Evaluation.EdmToClrEvaluator evaluator)
        {
            EdmUtil.CheckArgumentNull(model, "model");
            EdmUtil.CheckArgumentNull(context, "context");
            EdmUtil.CheckArgumentNull(term, "term");
            EdmUtil.CheckArgumentNull(evaluator, "evaluator");

            return GetTermValue<T>(model, context, context.Type.AsEntity().EntityDefinition(), term, qualifier, evaluator.EvaluateToClrValue<T>);
        }

        /// <summary>
        /// Gets the <see cref="IEdmValue "/> of a vocabulary term that has been applied to an element.
        /// </summary>
        /// <param name="model">Model to search for term annotations.</param>
        /// <param name="element">Annotated element.</param>
        /// <param name="termName">Name of the term to evaluate.</param>
        /// <param name="expressionEvaluator">Evaluator to use to perform expression evaluation.</param>
        /// <returns>Value of the term evaluated against the supplied value.</returns>
        public static IEdmValue GetTermValue(this IEdmModel model, IEdmVocabularyAnnotatable element, string termName, Evaluation.EdmExpressionEvaluator expressionEvaluator)
        {
            EdmUtil.CheckArgumentNull(model, "model");
            EdmUtil.CheckArgumentNull(element, "element");
            EdmUtil.CheckArgumentNull(termName, "termName");
            EdmUtil.CheckArgumentNull(expressionEvaluator, "evaluator");

            return GetTermValue<IEdmValue>(model, element, termName, null, expressionEvaluator.Evaluate);
        }

        /// <summary>
        /// Gets the <see cref="IEdmValue "/> of a vocabulary term that has been applied to an element.
        /// </summary>
        /// <param name="model">Model to search for term annotations.</param>
        /// <param name="element">Annotated element.</param>
        /// <param name="termName">Name of the term to evaluate.</param>
        /// <param name="qualifier">Qualifier to apply.</param>
        /// <param name="expressionEvaluator">Evaluator to use to perform expression evaluation.</param>
        /// <returns>Value of the term evaluated against the supplied value.</returns>
        public static IEdmValue GetTermValue(this IEdmModel model, IEdmVocabularyAnnotatable element, string termName, string qualifier, Evaluation.EdmExpressionEvaluator expressionEvaluator)
        {
            EdmUtil.CheckArgumentNull(model, "model");
            EdmUtil.CheckArgumentNull(element, "element");
            EdmUtil.CheckArgumentNull(termName, "termName");
            EdmUtil.CheckArgumentNull(expressionEvaluator, "evaluator");

            return GetTermValue<IEdmValue>(model, element, termName, qualifier, expressionEvaluator.Evaluate);
        }

        /// <summary>
        /// Gets the <see cref="IEdmValue "/> of a vocabulary term that has been applied to an element.
        /// </summary>
        /// <param name="model">Model to search for term annotations.</param>
        /// <param name="element">Annotated element.</param>
        /// <param name="term">Term to evaluate.</param>
        /// <param name="expressionEvaluator">Evaluator to use to perform expression evaluation.</param>
        /// <returns>Value of the term evaluated against the supplied value.</returns>
        public static IEdmValue GetTermValue(this IEdmModel model, IEdmVocabularyAnnotatable element, IEdmValueTerm term, Evaluation.EdmExpressionEvaluator expressionEvaluator)
        {
            EdmUtil.CheckArgumentNull(model, "model");
            EdmUtil.CheckArgumentNull(element, "element");
            EdmUtil.CheckArgumentNull(term, "term");
            EdmUtil.CheckArgumentNull(expressionEvaluator, "evaluator");

            return GetTermValue<IEdmValue>(model, element, term, null, expressionEvaluator.Evaluate);
        }

        /// <summary>
        /// Gets the <see cref="IEdmValue "/> of a vocabulary term that has been applied to an element.
        /// </summary>
        /// <param name="model">Model to search for term annotations.</param>
        /// <param name="element">Annotated element.</param>
        /// <param name="term">Term to evaluate.</param>
        /// <param name="qualifier">Qualifier to apply.</param>
        /// <param name="expressionEvaluator">Evaluator to use to perform expression evaluation.</param>
        /// <returns>Value of the term evaluated against the supplied value.</returns>
        public static IEdmValue GetTermValue(this IEdmModel model, IEdmVocabularyAnnotatable element, IEdmValueTerm term, string qualifier, Evaluation.EdmExpressionEvaluator expressionEvaluator)
        {
            EdmUtil.CheckArgumentNull(model, "model");
            EdmUtil.CheckArgumentNull(element, "element");
            EdmUtil.CheckArgumentNull(term, "term");
            EdmUtil.CheckArgumentNull(expressionEvaluator, "evaluator");

            return GetTermValue<IEdmValue>(model, element, term, qualifier, expressionEvaluator.Evaluate);
        }

        /// <summary>
        /// Gets the CLR value of a vocabulary term that has been applied to an element.
        /// </summary>
        /// <typeparam name="T">The CLR type of the value to be returned.</typeparam>
        /// <param name="model">Model to search for term annotations.</param>
        /// <param name="element">Annotated element.</param>
        /// <param name="termName">Name of the term to evaluate.</param>
        /// <param name="evaluator">Evaluator to use to perform expression evaluation.</param>
        /// <returns>Value of the term evaluated against the supplied value.</returns>
        public static T GetTermValue<T>(this IEdmModel model, IEdmVocabularyAnnotatable element, string termName, Evaluation.EdmToClrEvaluator evaluator)
        {
            EdmUtil.CheckArgumentNull(model, "model");
            EdmUtil.CheckArgumentNull(element, "element");
            EdmUtil.CheckArgumentNull(termName, "termName");
            EdmUtil.CheckArgumentNull(evaluator, "evaluator");

            return GetTermValue<T>(model, element, termName, null, evaluator.EvaluateToClrValue<T>);
        }

        /// <summary>
        /// Gets the CLR value of a vocabulary term that has been applied to an element.
        /// </summary>
        /// <typeparam name="T">The CLR type of the value to be returned.</typeparam>
        /// <param name="model">Model to search for term annotations.</param>
        /// <param name="element">Annotated element.</param>
        /// <param name="termName">Name of the term to evaluate.</param>
        /// <param name="qualifier">Qualifier to apply.</param>
        /// <param name="evaluator">Evaluator to use to perform expression evaluation.</param>
        /// <returns>Value of the term evaluated against the supplied value.</returns>
        public static T GetTermValue<T>(this IEdmModel model, IEdmVocabularyAnnotatable element, string termName, string qualifier, Evaluation.EdmToClrEvaluator evaluator)
        {
            EdmUtil.CheckArgumentNull(model, "model");
            EdmUtil.CheckArgumentNull(element, "element");
            EdmUtil.CheckArgumentNull(termName, "termName");
            EdmUtil.CheckArgumentNull(evaluator, "evaluator");

            return GetTermValue<T>(model, element, termName, qualifier, evaluator.EvaluateToClrValue<T>);
        }

        /// <summary>
        /// Gets the CLR value of a vocabulary term that has been applied to an element.
        /// </summary>
        /// <typeparam name="T">The CLR type of the value to be returned.</typeparam>
        /// <param name="model">Model to search for term annotations.</param>
        /// <param name="element">Annotated element.</param>
        /// <param name="term">Term to evaluate.</param>
        /// <param name="evaluator">Evaluator to use to perform expression evaluation.</param>
        /// <returns>Value of the term evaluated against the supplied value.</returns>
        public static T GetTermValue<T>(this IEdmModel model, IEdmVocabularyAnnotatable element, IEdmValueTerm term, Evaluation.EdmToClrEvaluator evaluator)
        {
            EdmUtil.CheckArgumentNull(model, "model");
            EdmUtil.CheckArgumentNull(element, "element");
            EdmUtil.CheckArgumentNull(term, "term");
            EdmUtil.CheckArgumentNull(evaluator, "evaluator");

            return GetTermValue<T>(model, element, term, null, evaluator.EvaluateToClrValue<T>);
        }

        /// <summary>
        /// Gets the CLR value of a vocabulary term that has been applied to an element.
        /// </summary>
        /// <typeparam name="T">The CLR type of the value to be returned.</typeparam>
        /// <param name="model">Model to search for term annotations.</param>
        /// <param name="element">Annotated element.</param>
        /// <param name="term">Term to evaluate.</param>
        /// <param name="qualifier">Qualifier to apply.</param>
        /// <param name="evaluator">Evaluator to use to perform expression evaluation.</param>
        /// <returns>Value of the term evaluated against the supplied value.</returns>
        public static T GetTermValue<T>(this IEdmModel model, IEdmVocabularyAnnotatable element, IEdmValueTerm term, string qualifier, Evaluation.EdmToClrEvaluator evaluator)
        {
            EdmUtil.CheckArgumentNull(model, "model");
            EdmUtil.CheckArgumentNull(element, "element");
            EdmUtil.CheckArgumentNull(term, "term");
            EdmUtil.CheckArgumentNull(evaluator, "evaluator");

            return GetTermValue<T>(model, element, term, qualifier, evaluator.EvaluateToClrValue<T>);
        }

        /// <summary>
        /// Gets an annotation value corresponding to the given namespace and name provided.
        /// </summary>
        /// <param name="model">The model containing the annotation.</param>
        /// <param name="element">The annotated element.</param>
        /// <param name="namespaceName">Namespace of the annotation.</param>
        /// <param name="localName">Name of the annotation inside the namespace.</param>
        /// <returns>The requested annotation value, if it exists. Otherwise, null.</returns>
        public static object GetAnnotationValue(this IEdmModel model, IEdmElement element, string namespaceName, string localName)
        {
            EdmUtil.CheckArgumentNull(model, "model");
            EdmUtil.CheckArgumentNull(element, "element");

            return model.DirectValueAnnotationsManager.GetAnnotationValue(element, namespaceName, localName);
        }

        /// <summary>
        /// Gets an annotation value corresponding to the given namespace and name provided.
        /// </summary>
        /// <typeparam name="T">Type of the annotation being returned.</typeparam>
        /// <param name="model">The model containing the annotation.</param>
        /// <param name="element">The annotated element.</param>
        /// <param name="namespaceName">Namespace of the annotation.</param>
        /// <param name="localName">Name of the annotation inside the namespace.</param>
        /// <returns>The requested annotation value, if it exists. Otherwise, null.</returns>
        public static T GetAnnotationValue<T>(this IEdmModel model, IEdmElement element, string namespaceName, string localName) where T : class
        {
            EdmUtil.CheckArgumentNull(model, "model");
            EdmUtil.CheckArgumentNull(element, "element");

            return AnnotationValue<T>(model.GetAnnotationValue(element, namespaceName, localName));
        }

        /// <summary>
        /// Gets an annotation value from an annotatable element.
        /// </summary>
        /// <typeparam name="T">Type of the annotation being returned.</typeparam>
        /// <param name="model">The model containing the annotation.</param>
        /// <param name="element">The annotated element.</param>
        /// <returns>The requested annotation, if it exists. Otherwise, null.</returns>
        /// <remarks>
        /// Strongly-typed wrappers for unnamed annotations keyed by CLR type.
        /// </remarks>
        public static T GetAnnotationValue<T>(this IEdmModel model, IEdmElement element) where T : class
        {
            EdmUtil.CheckArgumentNull(model, "model");
            EdmUtil.CheckArgumentNull(element, "element");

            return model.GetAnnotationValue<T>(element, EdmConstants.InternalUri, TypeName<T>.LocalName);
        }

        /// <summary>
        /// Sets an annotation value for an EDM element. If the value is null, no annotation is added and an existing annotation with the same name is removed.
        /// </summary>
        /// <param name="model">The model containing the annotation.</param>
        /// <param name="element">The annotated element.</param>
        /// <param name="namespaceName">Namespace that the annotation belongs to.</param>
        /// <param name="localName">Name of the annotation within the namespace.</param>
        /// <param name="value">Value of the new annotation.</param>
        public static void SetAnnotationValue(this IEdmModel model, IEdmElement element, string namespaceName, string localName, object value)
        {
            EdmUtil.CheckArgumentNull(model, "model");
            EdmUtil.CheckArgumentNull(element, "element");

            model.DirectValueAnnotationsManager.SetAnnotationValue(element, namespaceName, localName, value);
        }

        /// <summary>
        /// Gets all schema elements from the model, and models referenced by it.
        /// </summary>
        /// <param name="model">Model to search for elements</param>
        /// <returns>Schema elements from the model, and models referenced by it.</returns>
        public static IEnumerable<IEdmSchemaElement> SchemaElementsAcrossModels(this IEdmModel model)
        {
            EdmUtil.CheckArgumentNull(model, "model");

            IEnumerable<IEdmSchemaElement> result = new IEdmSchemaElement[] { };
            foreach (IEdmModel referencedModel in model.ReferencedModels)
            {
                result = result.Concat(referencedModel.SchemaElements);
            }

            result = result.Concat(model.SchemaElements);
            return result;
        }

        /// <summary>
        /// Finds a list of types that derive from the supplied type directly or indirectly, and across models.
        /// </summary>
        /// <param name="model">The model types are being found on.</param>
        /// <param name="baseType">The base type that derived types are being searched for.</param>
        /// <returns>A list of types that derive from the type.</returns>
        public static IEnumerable<IEdmStructuredType> FindAllDerivedTypes(this IEdmModel model, IEdmStructuredType baseType)
        {
            List<IEdmStructuredType> result = new List<IEdmStructuredType>();
            if (baseType is IEdmSchemaElement)
            {
                model.DerivedFrom(baseType, new HashSetInternal<IEdmStructuredType>(), result);
            }

            return result;
        }

        /// <summary>
        /// Sets an annotation value on an annotatable element.
        /// </summary>
        /// <typeparam name="T">Type of the annotation being set.</typeparam>
        ///  <param name="model">The model containing the annotation.</param>
        /// <param name="element">The annotated element.</param>
        /// <param name="value">Value of the new annotation.</param>
        public static void SetAnnotationValue<T>(this IEdmModel model, IEdmElement element, T value) where T : class
        {
            EdmUtil.CheckArgumentNull(model, "model");
            EdmUtil.CheckArgumentNull(element, "element");

            model.SetAnnotationValue(element, EdmConstants.InternalUri, TypeName<T>.LocalName, value);
        }

        /// <summary>
        /// Retrieves a set of annotation values. For each requested value, returns null if no annotation with the given name exists for the given element.
        /// </summary>
        /// <param name="model">The model in which to find the annotations.</param>
        /// <param name="annotations">The set of requested annotations.</param>
        /// <returns>Returns values that correspond to the provided annotations. A value is null if no annotation with the given name exists for the given element.</returns>
        public static object[] GetAnnotationValues(this IEdmModel model, IEnumerable<IEdmDirectValueAnnotationBinding> annotations)
        {
            EdmUtil.CheckArgumentNull(model, "model");
            EdmUtil.CheckArgumentNull(annotations, "annotations");

            return model.DirectValueAnnotationsManager.GetAnnotationValues(annotations);
        }

        /// <summary>
        /// Sets a set of annotation values. If a supplied value is null, no annotation is added and an existing annotation with the same name is removed.
        /// </summary>
        /// <param name="model">The model in which to set the annotations.</param>
        /// <param name="annotations">The annotations to set.</param>
        public static void SetAnnotationValues(this IEdmModel model, IEnumerable<IEdmDirectValueAnnotationBinding> annotations)
        {
            EdmUtil.CheckArgumentNull(model, "model");
            EdmUtil.CheckArgumentNull(annotations, "annotations");

            model.DirectValueAnnotationsManager.SetAnnotationValues(annotations);
        }

        /// <summary>
        /// Gets the direct value annotations for an element.
        /// </summary>
        /// <param name="model">The model containing the annotations.</param>
        /// <param name="element">The annotated element.</param>
        /// <returns>The immediate value annotations of the element.</returns>
        public static IEnumerable<IEdmDirectValueAnnotation> DirectValueAnnotations(this IEdmModel model, IEdmElement element)
        {
            EdmUtil.CheckArgumentNull(model, "model");
            EdmUtil.CheckArgumentNull(element, "element");
           
            return model.DirectValueAnnotationsManager.GetDirectValueAnnotations(element);
        }

        /// <summary>
        /// Finds the entity set.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <param name="containerQualifiedEntitySetName">Name of the container qualified element, can be an OperationImport or an EntitySet.</param>
        /// <param name="entitySet">The Entity Set that was found.</param>
        /// <returns>True if an entityset was found from the qualified container name, false if none were found.</returns>
        public static bool TryFindContainerQualifiedEntitySet(this IEdmModel model, string containerQualifiedEntitySetName, out IEdmEntitySet entitySet)
        {
            entitySet = null;
            string containerName = null;
            string simpleEntitySetName = null;
            
            if (containerQualifiedEntitySetName != null && 
                containerQualifiedEntitySetName.IndexOf(".", StringComparison.Ordinal) > -1 && 
                EdmUtil.TryParseContainerQualifiedElementName(containerQualifiedEntitySetName, out containerName, out simpleEntitySetName))
            {
                if (model.ExistsContainer(containerName))
                {
                    entitySet = model.EntityContainer.FindEntitySet(simpleEntitySetName);

                    if (entitySet != null)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Finds the singleton.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <param name="containerQualifiedSingletonName">Name of the container qualified singleton element.</param>
        /// <param name="singleton">The singleton that was found.</param>
        /// <returns>True if an singleton was found from the qualified container name, false if none were found.</returns>
        public static bool TryFindContainerQualifiedSingleton(this IEdmModel model, string containerQualifiedSingletonName, out IEdmSingleton singleton)
        {
            singleton = null;
            string containerName = null;
            string simpleSingletonName = null;

            if (containerQualifiedSingletonName != null &&
                containerQualifiedSingletonName.IndexOf(".", StringComparison.Ordinal) > -1 &&
                EdmUtil.TryParseContainerQualifiedElementName(containerQualifiedSingletonName, out containerName, out simpleSingletonName))
            {
                if (model.ExistsContainer(containerName))
                {
                    singleton = model.EntityContainer.FindSingleton(simpleSingletonName);

                    if (singleton != null)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Tries the find container qualified operation imports.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <param name="containerQualifiedOperationImportName">Name of the container qualified operation import.</param>
        /// <param name="operationImports">The operation imports.</param>
        /// <returns>True if OperationImports are found, false if none were found.</returns>
        public static bool TryFindContainerQualifiedOperationImports(this IEdmModel model, string containerQualifiedOperationImportName, out IEnumerable<IEdmOperationImport> operationImports)
        {
            operationImports = null;
            string containerName = null;
            string simpleOperationName = null;

            if (containerQualifiedOperationImportName.IndexOf(".", StringComparison.Ordinal) > -1 && EdmUtil.TryParseContainerQualifiedElementName(containerQualifiedOperationImportName, out containerName, out simpleOperationName))
            {
                if (model.ExistsContainer(containerName))
                {
                    operationImports = model.EntityContainer.FindOperationImports(simpleOperationName);

                    if (operationImports != null && operationImports.Count() > 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Searches for entity set by the given name that may be container qualified. If no container name is provided, then default container will be searched.
        /// </summary>
        /// <param name="model">The model to search.</param>
        /// <param name="qualifiedName">The name which might be container qualified. If no container name is provided, then default container will be searched.</param>
        /// <returns>The entity set found or empty if none found.</returns>
        public static IEdmEntitySet FindDeclaredEntitySet(this IEdmModel model, string qualifiedName)
        {
            IEdmEntitySet foundEntitySet = null;
            if (model.TryFindContainerQualifiedEntitySet(qualifiedName, out foundEntitySet))
            {
                return foundEntitySet;
            }

            if (model.EntityContainer != null)
            {
                return model.EntityContainer.FindEntitySet(qualifiedName);
            }

            return null;
        }

        /// <summary>
        /// Searches for singleton by the given name that may be container qualified. If no container name is provided, then default container will be searched.
        /// </summary>
        /// <param name="model">The model to search.</param>
        /// <param name="qualifiedName">The name which might be container qualified. If no container name is provided, then default container will be searched.</param>
        /// <returns>The singleton found or empty if none found.</returns>
        public static IEdmSingleton FindDeclaredSingleton(this IEdmModel model, string qualifiedName)
        {
            IEdmSingleton foundSingleton = null;
            if (model.TryFindContainerQualifiedSingleton(qualifiedName, out foundSingleton))
            {
                return foundSingleton;
            }

            if (model.EntityContainer != null)
            {
                return model.EntityContainer.FindSingleton(qualifiedName);
            }

            return null;
        }

        /// <summary>
        /// Searches for entity set or singleton by the given name that may be container qualified. If no container name is provided, then default container will be searched.
        /// </summary>
        /// <param name="model">The model to search.</param>
        /// <param name="qualifiedName">The name which might be container qualified. If no container name is provided, then default container will be searched.</param>
        /// <returns>The entity set or singleton found or empty if none found.</returns>
        public static IEdmNavigationSource FindDeclaredNavigationSource(this IEdmModel model, string qualifiedName)
        {
            IEdmEntitySet entitySet = model.FindDeclaredEntitySet(qualifiedName);
            if (entitySet != null)
            {
                return entitySet;
            }

            return model.FindDeclaredSingleton(qualifiedName);
        }


        /// <summary>
        /// Searches for the operation imports by the specified name, returns an empty enumerable if no operation import exists.
        /// </summary>
        /// <param name="model">The model to search.</param>
        /// <param name="qualifiedName">The qualified name of the operation import which may or may not include the container name.</param>
        /// <returns>All operation imports that can be found by the specified name, returns an empty enumerable if no operation import exists.</returns>
        public static IEnumerable<IEdmOperationImport> FindDeclaredOperationImports(this IEdmModel model, string qualifiedName)
        {
            IEnumerable<IEdmOperationImport> foundOperationImports = null;
            if (!model.TryFindContainerQualifiedOperationImports(qualifiedName, out foundOperationImports))
            {
                IEdmEntityContainer container = model.EntityContainer;
                if (container != null)
                {
                    foundOperationImports = container.FindOperationImports(qualifiedName);
                }
            }

            return foundOperationImports;
        }

        #endregion

        #region IEdmElement

        /// <summary>
        /// Gets the location of this element.
        /// </summary>
        /// <param name="item">Reference to the calling object.</param>
        /// <returns>The location of the element.</returns>
        public static EdmLocation Location(this IEdmElement item)
        {
            EdmUtil.CheckArgumentNull(item, "item");
            IEdmLocatable locatable = item as IEdmLocatable;
            return locatable != null && locatable.Location != null ? locatable.Location : new ObjectLocation(item);
        }

        #endregion

        #region IEdmVocabularyAnnotatable

        /// <summary>
        /// Gets an annotatable element's vocabulary annotations as seen from a particular model.
        /// </summary>
        /// <param name="element">Reference to the calling object.</param>
        /// <param name="model">Model to check for annotations.</param>
        /// <returns>Annotations attached to the element by the model or by models referenced by the model.</returns>
        public static IEnumerable<IEdmVocabularyAnnotation> VocabularyAnnotations(this IEdmVocabularyAnnotatable element, IEdmModel model)
        {
            EdmUtil.CheckArgumentNull(element, "element");
            EdmUtil.CheckArgumentNull(model, "model");
            return model.FindVocabularyAnnotations(element);
        }

        #endregion

        #region IEdmSchemaElement
        /// <summary>
        /// Gets the full name of the element.
        /// </summary>
        /// <param name="element">Reference to the calling object.</param>
        /// <returns>The full name of the element.</returns>
        public static string FullName(this IEdmSchemaElement element)
        {
            EdmUtil.CheckArgumentNull(element, "element");
            return (element.Namespace ?? String.Empty) + "." + (element.Name ?? String.Empty);
        }

        /// <summary>
        /// Gets the Short Qualified name of the element.
        /// </summary>
        /// <param name="element">Reference to the calling object.</param>
        /// <returns>The short qualified name of the element.</returns>
        public static string ShortQualifiedName(this IEdmSchemaElement element)
        {
            EdmUtil.CheckArgumentNull(element, "element");
            if (element.Namespace != null && element.Namespace.Equals("Edm"))
            {
                return (element.Name ?? String.Empty);
            }

            return FullName(element);
        }

        #endregion

        #region IEdmEntityContainer

        /// <summary>
        /// Returns entity sets belonging to an IEdmEntityContainer.
        /// </summary>
        /// <param name="container">Reference to the calling object.</param>
        /// <returns>Entity sets belonging to an IEdmEntityContainer.</returns>
        public static IEnumerable<IEdmEntitySet> EntitySets(this IEdmEntityContainer container)
        {
            EdmUtil.CheckArgumentNull(container, "container");
            return container.Elements.OfType<IEdmEntitySet>();
        }

        /// <summary>
        /// Returns singletons belonging to an IEdmEntityContainer.
        /// </summary>
        /// <param name="container">Reference to the calling object.</param>
        /// <returns>Singletons belonging to an IEdmEntityContainer.</returns>
        public static IEnumerable<IEdmSingleton> Singletons(this IEdmEntityContainer container)
        {
            EdmUtil.CheckArgumentNull(container, "container");
            return container.Elements.OfType<IEdmSingleton>();
        }

        /// <summary>
        /// Returns operation imports belonging to an IEdmEntityContainer.
        /// </summary>
        /// <param name="container">Reference to the calling object.</param>
        /// <returns>Operation imports belonging to an IEdmEntityContainer.</returns>
        public static IEnumerable<IEdmOperationImport> OperationImports(this IEdmEntityContainer container)
        {
            EdmUtil.CheckArgumentNull(container, "container");
            return container.Elements.OfType<IEdmOperationImport>();
        }

        #endregion

        #region IEdmTypeReference
        /// <summary>
        /// Gets the type kind of the type references definition.
        /// </summary>
        /// <param name="type">Reference to the calling object.</param>
        /// <returns>The type kind of the reference.</returns>
        public static EdmTypeKind TypeKind(this IEdmTypeReference type)
        {
            EdmUtil.CheckArgumentNull(type, "type");
            IEdmType typeDefinition = type.Definition;
            return typeDefinition != null ? typeDefinition.TypeKind : EdmTypeKind.None;
        }

        /// <summary>
        /// Gets the full name of the definition referred to by the type reference.
        /// </summary>
        /// <param name="type">Reference to the calling object.</param>
        /// <returns>The full name of this references definition.</returns>
        public static string FullName(this IEdmTypeReference type)
        {
            EdmUtil.CheckArgumentNull(type, "type");
            return type.Definition.FullTypeName();
        }

        /// <summary>
        /// Gets the short qualified name of the definition referred to by the type reference.
        /// </summary>
        /// <param name="type">Reference to the calling object.</param>
        /// <returns>The short qualified name of this references definition.</returns>
        public static string ShortQualifiedName(this IEdmTypeReference type)
        {
            EdmUtil.CheckArgumentNull(type, "type");
            var namedDefinition = type.Definition as IEdmSchemaElement;
            return namedDefinition != null ? namedDefinition.ShortQualifiedName() : null;
        }
        #endregion

        #region IEdmType

        /// <summary>
        /// Gets the full name of the definition referred to by the type reference.
        /// </summary>
        /// <param name="type">Reference to the calling object.</param>
        /// <returns>The full name of this references definition.</returns>
        public static string FullTypeName(this IEdmType type)
        {
            EdmUtil.CheckArgumentNull(type, "type");
            var namedDefinition = type as IEdmSchemaElement;
            var collectionType = type as IEdmCollectionType;
            if (collectionType == null)
            {
                return namedDefinition != null ? namedDefinition.FullName() : null;
            }

            // Handle collection case.
            namedDefinition = collectionType.ElementType.Definition as IEdmSchemaElement;

            return namedDefinition != null ? string.Format(CultureInfo.InvariantCulture, CollectionTypeFormat, namedDefinition.FullName()) : null;
        }

        #endregion

        #region IEdmPrimitiveTypeReference
        /// <summary>
        /// Gets the definition of this primitive type reference.
        /// </summary>
        /// <param name="type">Reference to the calling object.</param>
        /// <returns>Definition of this primitive type reference.</returns>
        public static IEdmPrimitiveType PrimitiveDefinition(this IEdmPrimitiveTypeReference type)
        {
            EdmUtil.CheckArgumentNull(type, "type");
            return (IEdmPrimitiveType)type.Definition;
        }

        /// <summary>
        /// Gets the primitive kind of the definition referred to by this type reference.
        /// </summary>
        /// <param name="type">Reference to the calling object.</param>
        /// <returns>Primitive kind of the definition of this reference.</returns>
        public static EdmPrimitiveTypeKind PrimitiveKind(this IEdmPrimitiveTypeReference type)
        {
            EdmUtil.CheckArgumentNull(type, "type");
            IEdmPrimitiveType primitive = type.PrimitiveDefinition();
            return primitive != null ? primitive.PrimitiveKind : EdmPrimitiveTypeKind.None;
        }
        #endregion

        #region IEdmStructuredTypeDefinition
        /// <summary>
        /// Gets all properties of the structured type definition and its base types.
        /// </summary>
        /// <param name="type">Reference to the calling object.</param>
        /// <returns>Properties of this type.</returns>
        public static IEnumerable<IEdmProperty> Properties(this IEdmStructuredType type)
        {
            EdmUtil.CheckArgumentNull(type, "type");
            if (type.BaseType != null)
            {
                foreach (IEdmProperty baseProperty in type.BaseType.Properties())
                {
                    yield return baseProperty;
                }
            }

            if (type.DeclaredProperties != null)
            {
                foreach (IEdmProperty declaredProperty in type.DeclaredProperties)
                {
                    yield return declaredProperty;
                }
            }
        }

        /// <summary>
        /// Gets all structural properties declared in the IEdmStructuredTypeDefinition.
        /// </summary>
        /// <param name="type">Reference to the calling object.</param>
        /// <returns>All structural properties declared in the IEdmStructuredTypeDefinition.</returns>
        public static IEnumerable<IEdmStructuralProperty> DeclaredStructuralProperties(this IEdmStructuredType type)
        {
            EdmUtil.CheckArgumentNull(type, "type");
            return type.DeclaredProperties.OfType<IEdmStructuralProperty>();
        }

        /// <summary>
        /// Gets the structural properties declared in this type definition and all base types.
        /// </summary>
        /// <param name="type">Reference to the calling object.</param>
        /// <returns>The structural properties declared in this type definition and all base types.</returns>
        public static IEnumerable<IEdmStructuralProperty> StructuralProperties(this IEdmStructuredType type)
        {
            EdmUtil.CheckArgumentNull(type, "type");
            return type.Properties().OfType<IEdmStructuralProperty>();
        }
        #endregion

        #region IEdmStructuredTypeReference
        /// <summary>
        /// Gets the definition of this structured type reference.
        /// </summary>
        /// <param name="type">Reference to the calling object.</param>
        /// <returns>The definition of this structured type reference.</returns>
        public static IEdmStructuredType StructuredDefinition(this IEdmStructuredTypeReference type)
        {
            EdmUtil.CheckArgumentNull(type, "type");
            return (IEdmStructuredType)type.Definition;
        }

        /// <summary>
        /// Returns true if the definition of this reference is abstract.
        /// </summary>
        /// <param name="type">Reference to the calling object.</param>
        /// <returns>If the definition of this reference is abstract.</returns>
        public static bool IsAbstract(this IEdmStructuredTypeReference type)
        {
            EdmUtil.CheckArgumentNull(type, "type");
            return type.StructuredDefinition().IsAbstract;
        }

        /// <summary>
        /// Returns true if the definition of this reference is open.
        /// </summary>
        /// <param name="type">Reference to the calling object.</param>
        /// <returns>If the definition of this reference is open.</returns>
        public static bool IsOpen(this IEdmStructuredTypeReference type)
        {
            EdmUtil.CheckArgumentNull(type, "type");
            return type.StructuredDefinition().IsOpen;
        }

        /// <summary>
        /// Returns the base type of the definition of this reference. 
        /// </summary>
        /// <param name="type">Reference to the calling object.</param>
        /// <returns>The base type of the definition of this reference. </returns>
        public static IEdmStructuredType BaseType(this IEdmStructuredTypeReference type)
        {
            EdmUtil.CheckArgumentNull(type, "type");
            return type.StructuredDefinition().BaseType;
        }

        /// <summary>
        /// Gets all structural properties declared in the definition of this reference.
        /// </summary>
        /// <param name="type">Reference to the calling object.</param>
        /// <returns>All structural properties declared in the definition of this reference.</returns>
        public static IEnumerable<IEdmStructuralProperty> DeclaredStructuralProperties(this IEdmStructuredTypeReference type)
        {
            EdmUtil.CheckArgumentNull(type, "type");
            return type.StructuredDefinition().DeclaredStructuralProperties();
        }

        /// <summary>
        /// Gets all structural properties declared in the definition of this reference and all its base types.
        /// </summary>
        /// <param name="type">Reference to the calling object.</param>
        /// <returns>All structural properties declared in the definition of this reference and all its base types.</returns>
        public static IEnumerable<IEdmStructuralProperty> StructuralProperties(this IEdmStructuredTypeReference type)
        {
            EdmUtil.CheckArgumentNull(type, "type");
            return type.StructuredDefinition().StructuralProperties();
        }

        /// <summary>
        /// Finds a property from the definition of this reference.
        /// </summary>
        /// <param name="type">Reference to the calling object.</param>
        /// <param name="name">Name of the property to find.</param>
        /// <returns>The requested property if it exists. Otherwise, null.</returns>
        public static IEdmProperty FindProperty(this IEdmStructuredTypeReference type, string name)
        {
            EdmUtil.CheckArgumentNull(type, "type");
            EdmUtil.CheckArgumentNull(name, "name");
            return type.StructuredDefinition().FindProperty(name);
        }
        #endregion

        #region IEdmEntityTypeDefinition
        /// <summary>
        /// Gets the base type of this entity type definition.
        /// </summary>
        /// <param name="type">Reference to the calling object.</param>
        /// <returns>The base type of this entity type definition.</returns>
        public static IEdmEntityType BaseEntityType(this IEdmEntityType type)
        {
            EdmUtil.CheckArgumentNull(type, "type");
            return type.BaseType as IEdmEntityType;
        }

        /// <summary>
        /// Gets the navigation properties declared in this entity definition.
        /// </summary>
        /// <param name="type">Reference to the calling object.</param>
        /// <returns>The navigation properties declared in this entity definition.</returns>
        public static IEnumerable<IEdmNavigationProperty> DeclaredNavigationProperties(this IEdmEntityType type)
        {
            EdmUtil.CheckArgumentNull(type, "type");
            return type.DeclaredProperties.OfType<IEdmNavigationProperty>();
        }

        /// <summary>
        /// Get the navigation properties declared in this entity type and all base types.
        /// </summary>
        /// <param name="type">Reference to the calling object.</param>
        /// <returns>The navigation properties declared in this entity type and all base types.</returns>
        public static IEnumerable<IEdmNavigationProperty> NavigationProperties(this IEdmEntityType type)
        {
            EdmUtil.CheckArgumentNull(type, "type");
            return type.Properties().OfType<IEdmNavigationProperty>();
        }

        /// <summary>
        /// Gets the declared key of the most defined entity with a declared key present.
        /// </summary>
        /// <param name="type">Reference to the calling object.</param>
        /// <returns>Key of this type.</returns>
        public static IEnumerable<IEdmStructuralProperty> Key(this IEdmEntityType type)
        {
            EdmUtil.CheckArgumentNull(type, "type");
            IEdmEntityType checkingType = type;
            while (checkingType != null)
            {
                if (checkingType.DeclaredKey != null)
                {
                    return checkingType.DeclaredKey;
                }

                checkingType = checkingType.BaseEntityType();
            }

            return Enumerable.Empty<IEdmStructuralProperty>();
        }

        /// <summary>
        /// Checks whether the given entity type has the <paramref name="property"/> as one of the key properties.
        /// </summary>
        /// <param name="entityType">Given entity type.</param>
        /// <param name="property">Property to be searched for.</param>
        /// <returns><c>true</c> if the type or base types has given property declared as key. <c>false</c> otherwise.</returns>
        public static bool HasDeclaredKeyProperty(this IEdmEntityType entityType, IEdmProperty property)
        {
            EdmUtil.CheckArgumentNull(entityType, "entityType");
            EdmUtil.CheckArgumentNull(property, "property");

            while (entityType != null)
            {
                if (entityType.DeclaredKey != null && entityType.DeclaredKey.Any(k => k == property))
                {
                    return true;
                }

                entityType = entityType.BaseEntityType();
            }

            return false;
        }

        #endregion

        #region IEdmEntityTypeReference
        /// <summary>
        /// Gets the definition of this entity reference.
        /// </summary>
        /// <param name="type">Reference to the calling object.</param>
        /// <returns>The definition of this entity reference.</returns>
        public static IEdmEntityType EntityDefinition(this IEdmEntityTypeReference type)
        {
            EdmUtil.CheckArgumentNull(type, "type");
            return (IEdmEntityType)type.Definition;
        }

        /// <summary>
        /// Gets the base type of the definition of this reference.
        /// </summary>
        /// <param name="type">Reference to the calling object.</param>
        /// <returns>The base type of the definition of this reference.</returns>
        public static IEdmEntityType BaseEntityType(this IEdmEntityTypeReference type)
        {
            EdmUtil.CheckArgumentNull(type, "type");
            return type.EntityDefinition().BaseEntityType();
        }

        /// <summary>
        /// Gets the entity key of the definition of this reference.
        /// </summary>
        /// <param name="type">Reference to the calling object.</param>
        /// <returns>The entity key of the definition of this reference.</returns>
        public static IEnumerable<IEdmStructuralProperty> Key(this IEdmEntityTypeReference type)
        {
            EdmUtil.CheckArgumentNull(type, "type");
            return type.EntityDefinition().Key();
        }

        /// <summary>
        /// Gets the navigation properties declared in the definition of this reference and its base types.
        /// </summary>
        /// <param name="type">Reference to the calling object.</param>
        /// <returns>The navigation properties declared in the definition of this reference and its base types.</returns>
        public static IEnumerable<IEdmNavigationProperty> NavigationProperties(this IEdmEntityTypeReference type)
        {
            EdmUtil.CheckArgumentNull(type, "type");
            return type.EntityDefinition().NavigationProperties();
        }

        /// <summary>
        /// Gets the navigation properties declared in the definition of this reference.
        /// </summary>
        /// <param name="type">Reference to the calling object.</param>
        /// <returns>The navigation properties declared in the definition of this reference.</returns>
        public static IEnumerable<IEdmNavigationProperty> DeclaredNavigationProperties(this IEdmEntityTypeReference type)
        {
            EdmUtil.CheckArgumentNull(type, "type");
            return type.EntityDefinition().DeclaredNavigationProperties();
        }

        /// <summary>
        /// Finds a navigation property declared in the definition of this reference by name.
        /// </summary>
        /// <param name="type">Reference to the calling object.</param>
        /// <param name="name">Name of the navigation property to find.</param>
        /// <returns>The requested navigation property if it exists. Otherwise, null.</returns>
        public static IEdmNavigationProperty FindNavigationProperty(this IEdmEntityTypeReference type, string name)
        {
            EdmUtil.CheckArgumentNull(type, "type");
            EdmUtil.CheckArgumentNull(name, "name");
            return type.EntityDefinition().FindProperty(name) as IEdmNavigationProperty;
        }
        #endregion

        #region IEdmComplexTypeDefinition
        /// <summary>
        /// Gets the base type of this references definition.
        /// </summary>
        /// <param name="type">Reference to the calling object.</param>
        /// <returns>The base type of this references definition.</returns>
        public static IEdmComplexType BaseComplexType(this IEdmComplexType type)
        {
            EdmUtil.CheckArgumentNull(type, "type");
            return type.BaseType as IEdmComplexType;
        }
        #endregion

        #region IEdmComplexTypeReference
        /// <summary>
        /// Gets the definition of this reference typed as an IEdmComplexTypeDefinition.
        /// </summary>
        /// <param name="type">Reference to the calling object.</param>
        /// <returns>The definition of this reference typed as an IEdmComplexTypeDefinition.</returns>
        public static IEdmComplexType ComplexDefinition(this IEdmComplexTypeReference type)
        {
            EdmUtil.CheckArgumentNull(type, "type");
            return (IEdmComplexType)type.Definition;
        }

        /// <summary>
        /// Gets the base type of this reference.
        /// </summary>
        /// <param name="type">Reference to the calling object.</param>
        /// <returns>The base type of this reference.</returns>
        public static IEdmComplexType BaseComplexType(this IEdmComplexTypeReference type)
        {
            EdmUtil.CheckArgumentNull(type, "type");
            return type.ComplexDefinition().BaseComplexType();
        }
        #endregion

        #region IEdmEntityReferenceTypeReference
        /// <summary>
        /// Gets the definition of this entity reference type reference.
        /// </summary>
        /// <param name="type">Reference to the calling object.</param>
        /// <returns>The definition of this entity reference type reference.</returns>
        public static IEdmEntityReferenceType EntityReferenceDefinition(this IEdmEntityReferenceTypeReference type)
        {
            EdmUtil.CheckArgumentNull(type, "type");
            return (IEdmEntityReferenceType)type.Definition;
        }

        /// <summary>
        /// Gets the entity type referred to by the definition of this entity reference type reference.
        /// </summary>
        /// <param name="type">Reference to the calling object.</param>
        /// <returns>The entity type referred to by the definition of this entity reference type reference.</returns>
        public static IEdmEntityType EntityType(this IEdmEntityReferenceTypeReference type)
        {
            EdmUtil.CheckArgumentNull(type, "type");
            return type.EntityReferenceDefinition().EntityType;
        }
        #endregion

        #region IEdmCollectionTypeReference
        /// <summary>
        /// Gets the definition of this collection reference.
        /// </summary>
        /// <param name="type">Reference to the calling object.</param>
        /// <returns>The definition of this collection reference.</returns>
        public static IEdmCollectionType CollectionDefinition(this IEdmCollectionTypeReference type)
        {
            EdmUtil.CheckArgumentNull(type, "type");
            return (IEdmCollectionType)type.Definition;
        }

        /// <summary>
        /// Gets the element type of the definition of this collection reference.
        /// </summary>
        /// <param name="type">Reference to the calling object.</param>
        /// <returns>The element type of the definition of this collection reference.</returns>
        public static IEdmTypeReference ElementType(this IEdmCollectionTypeReference type)
        {
            EdmUtil.CheckArgumentNull(type, "type");
            return type.CollectionDefinition().ElementType;
        }

        #endregion

        #region IEdmEnumTypeReference

        /// <summary>
        /// Gets the definition of this enumeration reference.
        /// </summary>
        /// <param name="type">Reference to the calling object.</param>
        /// <returns>The definition of this enumeration reference.</returns>
        public static IEdmEnumType EnumDefinition(this IEdmEnumTypeReference type)
        {
            EdmUtil.CheckArgumentNull(type, "type");
            return (IEdmEnumType)type.Definition;
        }

        #endregion

        #region IEdmNavigationProperty

        /// <summary>
        /// Gets the multiplicity of the target of this navigation.
        /// </summary>
        /// <param name="property">Reference to the calling object.</param>
        /// <returns>The multiplicity of the target end of the relationship.</returns>
        public static EdmMultiplicity TargetMultiplicity(this IEdmNavigationProperty property)
        {
            EdmUtil.CheckArgumentNull(property, "property");

            IEdmTypeReference type = property.Type;
            if (type.IsCollection())
            {
                return EdmMultiplicity.Many;
            }

            return type.IsNullable ? EdmMultiplicity.ZeroOrOne : EdmMultiplicity.One;
        }

        /// <summary>
        /// Gets the entity type targeted by this navigation property.
        /// </summary>
        /// <param name="property">Reference to the calling object.</param>
        /// <returns>The entity type targeted by this navigation property.</returns>
        public static IEdmEntityType ToEntityType(this IEdmNavigationProperty property)
        {
            IEdmType target = property.Type.Definition;
            if (target.TypeKind == EdmTypeKind.Collection)
            {
                target = ((IEdmCollectionType)target).ElementType.Definition;
            }

            if (target.TypeKind == EdmTypeKind.EntityReference)
            {
                target = ((IEdmEntityReferenceType)target).EntityType;
            }

            return target as IEdmEntityType;
        }

        /// <summary>
        /// Gets the entity type declaring this navigation property.
        /// </summary>
        /// <param name="property">Reference to the calling object.</param>
        /// <returns>The entity type that declares this navigation property.</returns>
        public static IEdmEntityType DeclaringEntityType(this IEdmNavigationProperty property)
        {
            return (IEdmEntityType)property.DeclaringType;
        }

        /// <summary>
        /// Gets whether this navigation property originates at the principal end of an association.
        /// </summary>
        /// <param name="navigationProperty">The navigation property.</param>
        /// <returns>Whether this navigation property originates at the principal end of an association.</returns>
        public static bool IsPrincipal(this IEdmNavigationProperty navigationProperty)
        {
            return navigationProperty.ReferentialConstraint == null && navigationProperty.Partner != null && navigationProperty.Partner.ReferentialConstraint != null;
        }

        /// <summary>
        /// Gets the dependent properties of this navigation property, returning null if this is the principal entity or if there is no referential constraint.
        /// </summary>
        /// <param name="navigationProperty">The navigation property.</param>
        /// <returns>The dependent properties of this navigation property, returning null if this is the principal entity or if there is no referential constraint.</returns>
        public static IEnumerable<IEdmStructuralProperty> DependentProperties(this IEdmNavigationProperty navigationProperty)
        {
            return navigationProperty.ReferentialConstraint == null ? null : navigationProperty.ReferentialConstraint.PropertyPairs.Select(p => p.DependentProperty);
        }

        /// <summary>
        /// Gets the principal properties of this navigation property, returning null if this is the principal entity or if there is no referential constraint.
        /// </summary>
        /// <param name="navigationProperty">The navigation property.</param>
        /// <returns>The principal properties of this navigation property, returning null if this is the principal entity or if there is no referential constraint.</returns>
        public static IEnumerable<IEdmStructuralProperty> PrincipalProperties(this IEdmNavigationProperty navigationProperty)
        {
            return navigationProperty.ReferentialConstraint == null ? null : navigationProperty.ReferentialConstraint.PropertyPairs.Select(p => p.PrincipalProperty);
        }

        #endregion

        #region IEdmValueAnnotation
        /// <summary>
        /// Gets the value term of this value annotation.
        /// </summary>
        /// <param name="annotation">Reference to the calling object.</param>
        /// <returns>The value term of this value annotation.</returns>
        public static IEdmValueTerm ValueTerm(this IEdmValueAnnotation annotation)
        {
            EdmUtil.CheckArgumentNull(annotation, "annotation");
            return (IEdmValueTerm)annotation.Term;
        }
        #endregion

        #region IEdmOperationImport
        /// <summary>
        /// Tries to get the relative entity set path.
        /// </summary>
        /// <param name="operation">The operation to resolve the entitySet path.</param>
        /// <param name="model">The model.</param>
        /// <param name="parameter">The parameter.</param>
        /// <param name="relativePath">The relative path.</param>
        /// <param name="lastEntityType">Last type of the entity.</param>
        /// <param name="errors">The errors.</param>
        /// <returns>True if a Entity set path is found, false otherwise.</returns>
        public static bool TryGetRelativeEntitySetPath(this IEdmOperation operation, IEdmModel model, out IEdmOperationParameter parameter, out IEnumerable<IEdmNavigationProperty> relativePath, out IEdmEntityType lastEntityType, out IEnumerable<EdmError> errors)
        {
            errors = Enumerable.Empty<EdmError>();
            parameter = null;
            relativePath = null;
            lastEntityType = null;

            Debug.Assert(operation != null, "expected non null operation");

            // If a value does not exist just return as there is nothing to validate.
            if (operation.EntitySetPath == null)
            {
                return false;
            }

            Collection<EdmError> foundErrors = new Collection<EdmError>();
            errors = foundErrors;
            if (!operation.IsBound)
            {
                foundErrors.Add(
                    new EdmError(
                        operation.Location(),
                        EdmErrorCode.OperationCannotHaveEntitySetPathWithUnBoundOperation,
                        Strings.EdmModel_Validator_Semantic_OperationCannotHaveEntitySetPathWithUnBoundOperation(operation.Name)));
            }

            return TryGetRelativeEntitySetPath(operation, foundErrors, operation.EntitySetPath, model, operation.Parameters, out parameter, out relativePath, out lastEntityType);
        }


        /// <summary>
        /// Determines whether [is action import] [the specified operation import].
        /// </summary>
        /// <param name="operationImport">The operation import.</param>
        /// <returns>
        ///   <c>true</c> if [is action import] [the specified operation import]; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsActionImport(this IEdmOperationImport operationImport)
        {
            return operationImport.ContainerElementKind == EdmContainerElementKind.ActionImport;
        }

        /// <summary>
        /// Determines whether [is function import] [the specified operation import].
        /// </summary>
        /// <param name="operationImport">The operation import.</param>
        /// <returns>
        ///   <c>true</c> if [is function import] [the specified operation import]; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsFunctionImport(this IEdmOperationImport operationImport)
        {
            return operationImport.ContainerElementKind == EdmContainerElementKind.FunctionImport;
        }

        /// <summary>
        /// Analyzes <see cref="IEdmOperationImport"/>.EntitySet expression and returns a static <see cref="IEdmEntitySet"/> reference if available.
        /// </summary>
        /// <param name="operationImport">The operation import containing the entity set expression.</param>
        /// <param name="entitySet">The static entity set of the operation import.</param>
        /// <returns>True if the entity set expression of the <paramref name="operationImport"/> contains a static reference to an <see cref="IEdmEntitySet"/>, otherwise false.</returns>
        public static bool TryGetStaticEntitySet(this IEdmOperationImport operationImport, out IEdmEntitySet entitySet)
        {
            var entitySetReference = operationImport.EntitySet as IEdmEntitySetReferenceExpression;
            entitySet = entitySetReference != null ? entitySetReference.ReferencedEntitySet : null;
            return entitySet != null;
        }

        /// <summary>
        /// Analyzes <see cref="IEdmOperationImport"/>.EntitySet expression and returns a relative path to an <see cref="IEdmEntitySet"/> if available.
        /// The path starts with the <paramref name="parameter"/> and may have optional sequence of <see cref="IEdmNavigationProperty"/> and type casts segments.
        /// </summary>
        /// <param name="operationImport">The operation import containing the entity set expression.</param>
        /// <param name="model">The model containing the operation import.</param>
        /// <param name="parameter">The operation import parameter from which the relative entity set path starts.</param>
        /// <param name="relativePath">The optional sequence of navigation properties.</param>
        /// <param name="edmErrors">The errors that were found when attempting to get the relative path.</param>
        /// <returns>True if the entity set expression of the <paramref name="operationImport"/> contains a relative path an <see cref="IEdmEntitySet"/>, otherwise false.</returns>
        public static bool TryGetRelativeEntitySetPath(this IEdmOperationImport operationImport, IEdmModel model, out IEdmOperationParameter parameter, out IEnumerable<IEdmNavigationProperty> relativePath, out IEnumerable<EdmError> edmErrors)
        {
            EdmUtil.CheckArgumentNull(operationImport, "operationImport");
            EdmUtil.CheckArgumentNull(model, "model");

            parameter = null;
            relativePath = null;
            edmErrors = new ReadOnlyCollection<EdmError>(new List<EdmError>());

            IEdmPathExpression pathExpression = operationImport.EntitySet as IEdmPathExpression;
            if (pathExpression != null)
            {
                IEdmEntityType entityType = null;
                Collection<EdmError> foundErrors = new Collection<EdmError>();
                bool result = TryGetRelativeEntitySetPath(operationImport, foundErrors, pathExpression, model, operationImport.Operation.Parameters, out parameter, out relativePath, out entityType);
                edmErrors = new ReadOnlyCollection<EdmError>(foundErrors);

                return result;
            }

            return false;
        }

        #endregion

        #region IEdmOperation

        /// <summary>
        /// Determines whether the specified operation is action.
        /// </summary>
        /// <param name="operation">The operation.</param>
        /// <returns>
        ///   <c>true</c> if the specified operation is action; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsAction(this IEdmOperation operation)
        {
            return operation.SchemaElementKind == EdmSchemaElementKind.Action;
        }

        /// <summary>
        /// Determines whether the specified operation is function.
        /// </summary>
        /// <param name="operation">The operation.</param>
        /// <returns>
        ///   <c>true</c> if the specified operation is function; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsFunction(this IEdmOperation operation)
        {
            return operation.SchemaElementKind == EdmSchemaElementKind.Function;
        }

        /// <summary>
        /// Checks whether all operations have the same return type 
        /// </summary>
        /// <param name="operations">the list to check</param>
        /// <param name="forceFullyQualifiedNameFilter">Ensures that the Where filter clause applies the Full name,</param>
        /// <param name="operationName">The operation name to filter by.</param>
        /// <returns>true if the list of operation imports all have the same return type</returns>
        public static IEnumerable<IEdmOperation> FilterByName(this IEnumerable<IEdmOperation> operations, bool forceFullyQualifiedNameFilter, string operationName)
        {
            EdmUtil.CheckArgumentNull(operations, "operations");
            EdmUtil.CheckArgumentNull(operationName, "operationName");

            if (forceFullyQualifiedNameFilter || operationName.IndexOf(".", StringComparison.Ordinal) > -1)
            {
                return operations.Where(o => o.FullName() == operationName);
            }
            else
            {
                return operations.Where(o => o.Name == operationName);
            }
        }

        /// <summary>
        /// Determines whether the bound operation's  binding type is equivalent to the specified binding type.
        /// </summary>
        /// <param name="operation">The operation.</param>
        /// <param name="bindingType">Type of the binding.</param>
        /// <returns>
        ///   <c>true</c> if [is operation binding type equivalent to] [the specified operation]; otherwise, <c>false</c>.
        /// </returns>
        public static bool HasEquivalentBindingType(this IEdmOperation operation, IEdmType bindingType)
        {
            EdmUtil.CheckArgumentNull(operation, "operation");
            EdmUtil.CheckArgumentNull(bindingType, "bindingType");

            if (!operation.IsBound || !operation.Parameters.Any())
            {
                return false;
            }

            IEdmOperationParameter parameter = operation.Parameters.First();
            IEdmType parameterType = parameter.Type.Definition;

            if (parameterType.TypeKind != bindingType.TypeKind)
            {
                return false;
            }

            if (parameterType.TypeKind == EdmTypeKind.Collection)
            {
                 // covariance applies here, so IEnumerable<A> is applicable to IEnumerable<B> where B:A
                IEdmCollectionType parameterCollectionType = (IEdmCollectionType)parameterType;
                IEdmCollectionType bindingCollectionType = (IEdmCollectionType)bindingType;

                return bindingCollectionType.ElementType.Definition.IsOrInheritsFrom(parameterCollectionType.ElementType.Definition);
            }
            else
            {
                return bindingType.IsOrInheritsFrom(parameterType);
            }
        }

        #endregion

        #region IEdmRecordExpression

        /// <summary>
        /// Finds a property of a record expression.
        /// </summary>
        /// <param name="expression">The record expression.</param>
        /// <param name="name">Name of the property to find.</param>
        /// <returns>The property, if found, otherwise null.</returns>
        public static IEdmPropertyConstructor FindProperty(this IEdmRecordExpression expression, string name)
        {
            foreach (IEdmPropertyConstructor propertyConstructor in expression.Properties)
            {
                if (propertyConstructor.Name == name)
                {
                    return propertyConstructor;
                }
            }

            return null;
        }

        #endregion

        #region IEdmNavigationSource

        /// <summary>
        /// Return the navigation kind of the navigation source.
        /// </summary>
        /// <param name="navigationSource">The navigation source.</param>
        /// <returns>The kind of the navigation source.</returns>
        public static EdmNavigationSourceKind NavigationSourceKind(this IEdmNavigationSource navigationSource)
        {
            if (navigationSource is IEdmEntitySet)
            {
                return EdmNavigationSourceKind.EntitySet;
            }

            if (navigationSource is IEdmSingleton)
            {
                return EdmNavigationSourceKind.Singleton;
            }

            if (navigationSource is IEdmContainedEntitySet)
            {
                return EdmNavigationSourceKind.ContainedEntitySet;
            }

            if (navigationSource is IEdmUnknownEntitySet)
            {
                return EdmNavigationSourceKind.UnknownEntitySet;
            }

            return EdmNavigationSourceKind.None;
        }

        /// <summary>
        /// Return the entity type of the navigation source.
        /// </summary>
        /// <param name="navigationSource">The navigation source.</param>
        /// <returns>The entity type of the navigation source.</returns>
        public static IEdmEntityType EntityType(this IEdmNavigationSource navigationSource)
        {
            var entitySetBase = navigationSource as IEdmEntitySetBase;
            if (entitySetBase != null)
            {
                IEdmCollectionType collectionType = entitySetBase.Type as IEdmCollectionType;
                
                if (collectionType != null)
                {
                    return collectionType.ElementType.Definition as IEdmEntityType;
                }

                return null;
            }

            var singleton = navigationSource as IEdmSingleton;
            if (singleton != null)
            {
                return singleton.Type as IEdmEntityType;
            }

            return null;
        }

        #endregion

        internal static bool TryGetRelativeEntitySetPath(IEdmElement element, Collection<EdmError> foundErrors, IEdmPathExpression pathExpression, IEdmModel model, IEnumerable<IEdmOperationParameter> parameters, out IEdmOperationParameter parameter, out IEnumerable<IEdmNavigationProperty> relativePath, out IEdmEntityType lastEntityType)
        {
            parameter = null;
            relativePath = null;
            lastEntityType = null;

            var pathItems = pathExpression.Path.ToList();
            if (pathItems.Count < 1)
            {
                foundErrors.Add(new EdmError(element.Location(), EdmErrorCode.OperationWithInvalidEntitySetPathMissingCompletePath, Strings.EdmModel_Validator_Semantic_InvalidEntitySetPathMissingBindingParameterName(CsdlConstants.Attribute_EntitySetPath)));
                return false;
            }

            // If there is no parameter then this will fail in BoundOperationMustHaveParameters rule so skip validating this here.
            if (!parameters.Any())
            {
                return false;
            }

            bool foundRelativePath = true;

            string bindingParameterName = pathItems.First();
            parameter = parameters.FirstOrDefault();
            Debug.Assert(parameter != null, "Should never be null");
            if (parameter.Name != bindingParameterName)
            {
                foundErrors.Add(
                    new EdmError(
                        element.Location(), 
                        EdmErrorCode.InvalidPathFirstPathParameterNotMatchingFirstParameterName, 
                        Strings.EdmModel_Validator_Semantic_InvalidEntitySetPathWithFirstPathParameterNotMatchingFirstParameterName(CsdlConstants.Attribute_EntitySetPath, EdmModelCsdlSchemaWriter.PathAsXml(pathExpression.Path), bindingParameterName, parameter.Name)));
                
                foundRelativePath = false;
            }

            lastEntityType = parameter.Type.Definition as IEdmEntityType;
            if (lastEntityType == null)
            {
                var collectionReference = parameter.Type as IEdmCollectionTypeReference;
                if (collectionReference != null && collectionReference.ElementType().IsEntity())
                {
                    lastEntityType = collectionReference.ElementType().Definition as IEdmEntityType;
                }
                else
                {
                    foundErrors.Add(
                        new EdmError(
                            element.Location(),
                            EdmErrorCode.InvalidPathWithNonEntityBindingParameter,
                            Strings.EdmModel_Validator_Semantic_InvalidEntitySetPathWithNonEntityBindingParameter(CsdlConstants.Attribute_EntitySetPath, EdmModelCsdlSchemaWriter.PathAsXml(pathExpression.Path), bindingParameterName)));

                    return false;
                }
            }

            List<IEdmNavigationProperty> navigationProperties = new List<IEdmNavigationProperty>();

            // Now check that the next paths are valid parameters.
            foreach (string pathSegment in pathItems.Skip(1))
            {
                if (EdmUtil.IsQualifiedName(pathSegment))
                {
                    IEdmSchemaType foundType = model.FindDeclaredType(pathSegment);
                    if (foundType == null)
                    {
                        foundErrors.Add(
                            new EdmError(
                                element.Location(),
                                EdmErrorCode.InvalidPathUnknownTypeCastSegment,
                                Strings.EdmModel_Validator_Semantic_InvalidEntitySetPathUnknownTypeCastSegment(CsdlConstants.Attribute_EntitySetPath, EdmModelCsdlSchemaWriter.PathAsXml(pathExpression.Path), pathSegment)));

                        foundRelativePath = false;
                        break;
                    }

                    IEdmEntityType foundEntityTypeCast = foundType as IEdmEntityType;

                    if (foundEntityTypeCast == null)
                    {
                        foundErrors.Add(
                            new EdmError(
                                element.Location(), 
                                EdmErrorCode.InvalidPathTypeCastSegmentMustBeEntityType, 
                                Strings.EdmModel_Validator_Semantic_InvalidEntitySetPathTypeCastSegmentMustBeEntityType(CsdlConstants.Attribute_EntitySetPath, EdmModelCsdlSchemaWriter.PathAsXml(pathExpression.Path), foundType.FullName())));
                        
                        foundRelativePath = false;
                        break;
                    }

                    if (!foundEntityTypeCast.IsOrInheritsFrom(lastEntityType))
                    {
                        foundErrors.Add(
                            new EdmError(
                                element.Location(), 
                                EdmErrorCode.InvalidPathInvalidTypeCastSegment, 
                                Strings.EdmModel_Validator_Semantic_InvalidEntitySetPathInvalidTypeCastSegment(CsdlConstants.Attribute_EntitySetPath, EdmModelCsdlSchemaWriter.PathAsXml(pathExpression.Path), lastEntityType.FullName(), foundEntityTypeCast.FullName())));
                        
                        foundRelativePath = false;
                        break;
                    }

                    lastEntityType = foundEntityTypeCast;
                }
                else
                {
                    IEdmNavigationProperty navigationProperty = lastEntityType.FindProperty(pathSegment) as IEdmNavigationProperty;
                    if (navigationProperty == null)
                    {
                        foundErrors.Add(
                            new EdmError(
                                element.Location(), 
                                EdmErrorCode.InvalidPathUnknownNavigationProperty, 
                                Strings.EdmModel_Validator_Semantic_InvalidEntitySetPathUnknownNavigationProperty(CsdlConstants.Attribute_EntitySetPath, EdmModelCsdlSchemaWriter.PathAsXml(pathExpression.Path), pathSegment)));
                        
                        foundRelativePath = false;
                        break;
                    }

                    navigationProperties.Add(navigationProperty);
                    lastEntityType = navigationProperty.ToEntityType();
                }
            }

            relativePath = navigationProperties;
            return foundRelativePath;
        }
       
        /// <summary>
        /// This method is only used for the operation import entity set path parsing.
        /// </summary>
        /// <param name="segmentType">The type of the segment.</param>
        /// <returns>Non-null entity type that may be bad.</returns>
        internal static IEdmEntityType GetPathSegmentEntityType(IEdmTypeReference segmentType)
        {
            return (segmentType.IsCollection() ? segmentType.AsCollection().ElementType() : segmentType).AsEntity().EntityDefinition();
        }

        /// <summary>
        /// Gets documentation for a specified element.
        /// </summary>
        /// <param name="model">The model containing the documentation.</param>
        /// <param name="element">The element.</param>
        /// <returns>Documentation that exists on the element. Otherwise, null.</returns>
        internal static IEdmDocumentation GetDocumentation(this IEdmModel model, IEdmElement element)
        {
            EdmUtil.CheckArgumentNull(model, "model");
            EdmUtil.CheckArgumentNull(element, "element");

            return (IEdmDocumentation)model.GetAnnotationValue(element, EdmConstants.DocumentationUri, EdmConstants.DocumentationAnnotation);
        }

        /// <summary>
        /// Sets documentation for a specified element.
        /// </summary>
        /// <param name="model">The model containing the documentation.</param>
        /// <param name="element">The element.</param>
        /// <param name="documentation">Documentation to set.</param>
        internal static void SetDocumentation(this IEdmModel model, IEdmElement element, IEdmDocumentation documentation)
        {
            EdmUtil.CheckArgumentNull(model, "model");
            EdmUtil.CheckArgumentNull(element, "element");

            model.SetAnnotationValue(element, EdmConstants.DocumentationUri, EdmConstants.DocumentationAnnotation, documentation);
        }

        private static T FindAcrossModels<T>(this IEdmModel model, string qualifiedName, Func<IEdmModel, string, T> finder, Func<T, T, T> ambiguousCreator)
        {
            T candidate = finder(model, qualifiedName);

            foreach (IEdmModel reference in model.ReferencedModels)
            {
                T fromReference = finder(reference, qualifiedName);
                if (fromReference != null)
                {
                    candidate = candidate == null ? fromReference : ambiguousCreator(candidate, fromReference);
                }
            }

            return candidate;
        }

        private static T GetTermValue<T>(this IEdmModel model, IEdmStructuredValue context, IEdmEntityType contextType, IEdmValueTerm term, string qualifier, Func<IEdmExpression, IEdmStructuredValue, IEdmTypeReference, T> evaluator)
        {
            IEnumerable<IEdmValueAnnotation> annotations = model.FindVocabularyAnnotations<IEdmValueAnnotation>(contextType, term, qualifier);

            if (annotations.Count() != 1)
            {
                throw new InvalidOperationException(Edm.Strings.Edm_Evaluator_NoValueAnnotationOnType(contextType.ToTraceString(), term.ToTraceString()));
            }

            return evaluator(annotations.Single().Value, context, term.Type);
        }

        private static T GetTermValue<T>(this IEdmModel model, IEdmStructuredValue context, IEdmEntityType contextType, string termName, string qualifier, Func<IEdmExpression, IEdmStructuredValue, IEdmTypeReference, T> evaluator)
        {
            IEnumerable<IEdmValueAnnotation> annotations = model.FindVocabularyAnnotations<IEdmValueAnnotation>(contextType, termName, qualifier);

            if (annotations.Count() != 1)
            {
                throw new InvalidOperationException(Edm.Strings.Edm_Evaluator_NoValueAnnotationOnType(contextType.ToTraceString(), termName));
            }

            IEdmValueAnnotation valueAnnotation = annotations.Single();
            return evaluator(valueAnnotation.Value, context, valueAnnotation.ValueTerm().Type);
        }

        private static T GetTermValue<T>(this IEdmModel model, IEdmVocabularyAnnotatable element, IEdmValueTerm term, string qualifier, Func<IEdmExpression, IEdmStructuredValue, IEdmTypeReference, T> evaluator)
        {
            IEnumerable<IEdmValueAnnotation> annotations = model.FindVocabularyAnnotations<IEdmValueAnnotation>(element, term, qualifier);

            if (annotations.Count() != 1)
            {
                throw new InvalidOperationException(Edm.Strings.Edm_Evaluator_NoValueAnnotationOnElement(term.ToTraceString()));
            }

            return evaluator(annotations.Single().Value, null, term.Type);
        }

        private static T GetTermValue<T>(this IEdmModel model, IEdmVocabularyAnnotatable element, string termName, string qualifier, Func<IEdmExpression, IEdmStructuredValue, IEdmTypeReference, T> evaluator)
        {
            IEnumerable<IEdmValueAnnotation> annotations = model.FindVocabularyAnnotations<IEdmValueAnnotation>(element, termName, qualifier);

            if (annotations.Count() != 1)
            {
                throw new InvalidOperationException(Edm.Strings.Edm_Evaluator_NoValueAnnotationOnElement(termName));
            }

            IEdmValueAnnotation valueAnnotation = annotations.Single();
            return evaluator(valueAnnotation.Value, null, valueAnnotation.ValueTerm().Type);
        }

        private static T AnnotationValue<T>(object annotation) where T : class
        {
            if (annotation != null)
            {
                T specificAnnotation = annotation as T;
                if (specificAnnotation != null)
                {
                    return specificAnnotation;
                }

                IEdmValue valueAnnotation = annotation as IEdmValue;
                if (valueAnnotation != null)
                {
                }

                throw new InvalidOperationException(Edm.Strings.Annotations_TypeMismatch(annotation.GetType().Name, typeof(T).Name));
            }

            return null;
        }

        private static void DerivedFrom(this IEdmModel model, IEdmStructuredType baseType, HashSetInternal<IEdmStructuredType> visited, List<IEdmStructuredType> derivedTypes)
        {
            if (visited.Add(baseType))
            {
                IEnumerable<IEdmStructuredType> candidates = model.FindDirectlyDerivedTypes(baseType);
                if (candidates != null && candidates.Any())
                {
                    foreach (IEdmStructuredType derivedType in candidates)
                    {
                        derivedTypes.Add(derivedType);
                        model.DerivedFrom(derivedType, visited, derivedTypes);
                    }
                }

                foreach (IEdmModel referenced in model.ReferencedModels)
                {
                    candidates = referenced.FindDirectlyDerivedTypes(baseType);
                    if (candidates != null && candidates.Any())
                    {
                        foreach (IEdmStructuredType derivedType in candidates)
                        {
                            derivedTypes.Add(derivedType);
                            model.DerivedFrom(derivedType, visited, derivedTypes);
                        }
                    }
                }
            }
        }

        internal static class TypeName<T>
        {
            // Use the name of the type as its local name for annotations.
            // Filter out special characters to produce a valid name:
            // '.'                      Appears in qualified names.
            // '`', '[', ']', ','       Appear in generic instantiations.
            // '+'                      Appears in names of local classes.
            public static readonly string LocalName = typeof(T).ToString().Replace("_", "_____").Replace('.', '_').Replace("[", "").Replace("]", "").Replace(",", "__").Replace("`", "___").Replace("+", "____");
        }
    }
}