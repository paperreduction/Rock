// <copyright>
// Copyright by the Spark Development Network
//
// Licensed under the Rock Community License (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.rockrms.com/license
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>
//
using System.Collections.Generic;
using Rock.Data;

namespace Rock.Field.Types
{
    /// <summary>
    /// Actions that can be used when copying Entity Attributes
    /// </summary>
    public enum EntityCopyAction
    {
        /// <summary>
        /// The do not copy action will create an attribute with an empty value.
        /// </summary>
        DoNotCopy,
        /// <summary>
        /// The use original action will use the original entity so changes made to this entity will affect other objects that use the same entity.
        /// </summary>
        UseOriginal,
        /// <summary>
        /// The duplicate original action will make a copy of the original entity and use the copy so that changes made to this entity done affect any other object.
        /// </summary>
        DuplicateOriginal
    }

    /// <summary>
    /// Represents any class that supports having copying the referenced values.
    /// </summary>
    public interface IEntityFieldTypeEntityCopy
    {
        /// <summary>
        /// The list of actions that are handled by the copy attribute method.
        /// </summary>
        /// <value>
        /// The actions handled.
        /// </value>
        IEnumerable<EntityCopyAction> ActionsHandled { get; }

        /// <summary>
        /// Copies the attribute.
        /// </summary>
        /// <param name="originalValue">The original value.</param>
        /// <param name="copyAction">The copy action.</param>
        /// <param name="rockContext">The rock context.</param>
        /// <returns></returns>
        string CopyAttribute( string originalValue, EntityCopyAction copyAction, RockContext rockContext );
    }
}
