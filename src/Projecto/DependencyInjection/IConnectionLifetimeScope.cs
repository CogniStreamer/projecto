﻿/*
 * Copyright 2017 Wouter Huysentruit
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *   http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;

namespace Projecto.DependencyInjection
{
    /// <summary>
    /// Interface that defines a lifetime scope for resolving connections.
    /// </summary>
    public interface IConnectionLifetimeScope : IDisposable
    {
        /// <summary>
        /// Property dictionary for sharing data between different event handlers.
        /// </summary>
        IDictionary<object, object> Properties { get; }

        /// <summary>
        /// Called when a certain connection needs to be resolved.
        /// </summary>
        /// <param name="connectionType">The type of the connection.</param>
        /// <returns>An instance of the requested connection type or null when the type was not found.</returns>
        object ResolveConnection(Type connectionType);

        /// <summary>
        /// Event triggered when a connection was resolved from the lifetime scope.
        /// </summary>
        event ConnectionResolvedEvent ConnectionResolved;

        /// <summary>
        /// Event triggered when the lifetime scope is about to end (right before disposal).
        /// </summary>
        event ConnectionLifetimeScopeEndingEvent ScopeEnding;
    }
}
