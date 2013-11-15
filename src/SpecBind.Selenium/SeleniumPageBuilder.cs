﻿// <copyright file="SeleniumPageBuilder.cs">
//    Copyright © 2013 Dan Piessens.  All rights reserved.
// </copyright>
namespace SpecBind.Selenium
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;

    using OpenQA.Selenium;
    using OpenQA.Selenium.Support.PageObjects;

    using SpecBind.Pages;

    /// <summary>
    /// A page builder class that follows Selenium rules for page building.
    /// </summary>
    public class SeleniumPageBuilder : PageBuilderBase<ISearchContext, object, IWebElement>
    {
        /// <summary>
        /// Creates the page.
        /// </summary>
        /// <param name="pageType">Type of the page.</param>
        /// <returns>The created page class.</returns>
        public Func<ISearchContext, Action<object>, object> CreatePage(Type pageType)
        {
            return this.CreateElementInternal(pageType);
        }

        /// <summary>
        /// Gets the element locators.
        /// </summary>
        /// <param name="attribute">The attribute.</param>
        /// <returns>The list of locators to use.</returns>
        internal static List<By> GetElementLocators(ElementLocatorAttribute attribute)
        {
            var locators = new List<By>(3);
            SetProperty(locators, attribute, a => By.Id(a.Id), a => a.Id != null);
            SetProperty(locators, attribute, a => By.Name(a.Name), a => a.Name != null);
            SetProperty(locators, attribute, a => By.TagName(a.TagName), a => a.TagName != null);
            SetProperty(locators, attribute, a => By.ClassName(a.Class), a => a.Class != null);
            SetProperty(locators, attribute, a => By.LinkText(a.Text), a => a.Text != null);

            return locators;
        }

        /// <summary>
        /// Assigns the element attributes.
        /// </summary>
        /// <param name="control">The control.</param>
        /// <param name="attribute">The attribute.</param>
        /// <param name="nativeAttributes">The native attributes.</param>
        protected override void AssignElementAttributes(IWebElement control, ElementLocatorAttribute attribute, object[] nativeAttributes)
        {
            var proxy = control as WebElement;
            if (proxy == null)
            {
                return;
            }

            // Convert any locator property to "find by" classes
            var locators = attribute != null ? GetElementLocators(attribute) : new List<By>();

            // Also try to parse the native attributes
            var nativeItems = nativeAttributes != null ?  nativeAttributes.OfType<FindsByAttribute>().ToList() : null;

            if (nativeItems != null && nativeItems.Count > 0)
            {
                var localLocators = locators;
                locators.AddRange(nativeItems.Where(a => a.Using != null)
                                             .OrderBy(n => n.Priority)
                                             .Select(NativeAttributeBuilder.GetLocator)
                                             .Where(l => l != null && !localLocators.Any(c => Equals(c, l))));
            }

            locators = locators.Count > 1 ? new List<By> { new ByChained(locators.ToArray()) } : locators;
            proxy.UpdateLocators(locators);
        }

        /// <summary>
        /// Gets the custom attributes.
        /// </summary>
        /// <param name="propertyInfo">Type of the item.</param>
        /// <returns>A collection of custom attributes.</returns>
        protected override object[] GetCustomAttributes(PropertyInfo propertyInfo)
        {
            return propertyInfo.GetCustomAttributes(typeof(FindsByAttribute), true);
        }

        /// <summary>
        /// Gets the type of the property proxy.
        /// </summary>
        /// <param name="propertyType">Type of the property.</param>
        /// <returns>The created type.</returns>
        protected override Type GetPropertyProxyType(Type propertyType)
        {
            if (typeof(IWebElement) == propertyType)
            {
                return typeof(WebElement);
            }

            return propertyType;
        }

        /// <summary>
        /// Gets the constructor.
        /// </summary>
        /// <param name="itemType">Type of the item.</param>
        /// <param name="parentArgument">The parent argument.</param>
        /// <param name="rootLocator">The root locator if different from the parent.</param>
        /// <returns>The constructor information that matches.</returns>
        protected override Tuple<ConstructorInfo, IEnumerable<Expression>> GetConstructor(Type itemType, ExpressionData parentArgument, ExpressionData rootLocator)
        {
            foreach (var constructorInfo in itemType.GetConstructors(BindingFlags.CreateInstance | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                var paramters = constructorInfo.GetParameters();
                if (paramters.Length != 1)
                {
                    continue;
                }

                var firstParameter = paramters.First();
                if (typeof(IWebDriver).IsAssignableFrom(firstParameter.ParameterType) ||
                    typeof(ISearchContext).IsAssignableFrom(firstParameter.ParameterType))
                {
                    // Need a build page or context here see if the parent matches
                    var parentArg = (rootLocator != null && !typeof(ISearchContext).IsAssignableFrom(parentArgument.Type))
                                        ? rootLocator.Expression
                                        : parentArgument.Expression;

                    return new Tuple<ConstructorInfo, IEnumerable<Expression>>(constructorInfo, new[] { Expression.Convert(parentArg, firstParameter.ParameterType) });
                }
            }

            var emptyConstructor = itemType.GetConstructors().FirstOrDefault(c => c.GetParameters().Length == 0);
            return emptyConstructor != null
                       ? new Tuple<ConstructorInfo, IEnumerable<Expression>>(emptyConstructor, new List<Expression>(0))
                       : null;
        }

        /// <summary>
        /// Gets the type of the element collection.
        /// </summary>
        /// <returns>The collection type.</returns>
        protected override Type GetElementCollectionType()
        {
            return typeof(SeleniumListElementWrapper<,>);
        }

        /// <summary>
        /// Sets the property of the locator by the filter.
        /// </summary>
        /// <typeparam name="T">The type of the element being set.</typeparam>
        /// <param name="locators">The locator collection.</param>
        /// <param name="item">The item.</param>
        /// <param name="setterFunc">The setter function.</param>
        /// <param name="filterFunc">The filter function.</param>
        private static void SetProperty<T>(ICollection<By> locators, T item, Func<T, By> setterFunc, Func<T, bool> filterFunc = null)
        {
            if (filterFunc == null || filterFunc(item))
            {
                locators.Add(setterFunc(item));
            }
        }
    }
}