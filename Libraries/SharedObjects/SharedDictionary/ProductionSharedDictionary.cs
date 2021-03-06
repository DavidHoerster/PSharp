﻿//-----------------------------------------------------------------------
// <copyright file="ProductionSharedDictionary.cs">
//      Copyright (c) Microsoft Corporation. All rights reserved.
// 
//      THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
//      EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
//      MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
//      IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY
//      CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
//      TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
//      SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
// </copyright>
//-----------------------------------------------------------------------

using System.Collections.Generic;
using System.Collections.Concurrent;

namespace Microsoft.PSharp.SharedObjects
{
    /// <summary>
    /// Implements a shared dictionary to be used in production.
    /// </summary>
    internal sealed class ProductionSharedDictionary<TKey, TValue> : ISharedDictionary<TKey, TValue>
    {
        /// <summary>
        /// The dictionary.
        /// </summary>
        ConcurrentDictionary<TKey, TValue> Dictionary;

        /// <summary>
        /// Initializes the shared dictionary.
        /// </summary>
        public ProductionSharedDictionary()
        {
            Dictionary = new ConcurrentDictionary<TKey, TValue>();
        }

        /// <summary>
        /// Initializes the shared dictionary.
        /// </summary>
        public ProductionSharedDictionary(IEqualityComparer<TKey> comparer)
        {
            Dictionary = new ConcurrentDictionary<TKey, TValue>(comparer);
        }

        /// <summary>
        /// Adds a new key to the dictionary, if it doesn’t already exist in the dictionary.
        /// </summary>
        /// <param name="key">Key</param>
        /// <param name="value">Value</param>
        /// <returns>True or false depending on whether the new key/value pair was added.</returns>
        public bool TryAdd(TKey key, TValue value)
        {
            return Dictionary.TryAdd(key, value);
        }

        /// <summary>
        /// Updates the value for an existing key in the dictionary, if that key has a specific value.
        /// </summary>
        /// <param name="key">Key</param>
        /// <param name="newValue">New value</param>
        /// <param name="comparisonValue">Old value</param>
        /// <returns>True if the value with key was equal to comparisonValue and was replaced with newValue; otherwise, false.</returns>
        public bool TryUpdate(TKey key, TValue newValue, TValue comparisonValue)
        {
            return Dictionary.TryUpdate(key, newValue, comparisonValue);
        }

        /// <summary>
        /// Gets or sets the value associated with the specified key.
        /// </summary>
        /// <param name="key">Key</param>
        /// <returns>Value</returns>
        public TValue this[TKey key]
        {
            get
            {
                return Dictionary[key];
            }
            set
            {
                Dictionary[key] = value;
            }
        }

        /// <summary>
        /// Removes the specified key from the dictionary.
        /// </summary>
        /// <param name="key">Key</param>
        /// <param name="value">Value associated with the key if present, or the default value otherwise.</param>
        /// <returns>True if the element is successfully removed; otherwise, false.</returns>
        public bool TryRemove(TKey key, out TValue value)
        {
            return Dictionary.TryRemove(key, out value);
        }

        /// <summary>
        /// Gets the number of elements in the dictionary.
        /// </summary>
        /// <returns>Size</returns>
        public int Count
        {
            get
            {
                return Dictionary.Count;
            }
        }
    }
}
