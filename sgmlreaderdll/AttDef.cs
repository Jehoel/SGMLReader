/*
 * 
 * Copyright (c) 2007-2013 MindTouch. All rights reserved.
 * www.mindtouch.com  oss@mindtouch.com
 *
 * For community documentation and downloads visit wiki.developer.mindtouch.com;
 * please review the licensing section.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *     http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 * 
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Net;
using System.Runtime.Serialization;
using System.Security.Permissions;
using System.Text;
using System.Xml;

namespace Sgml {

	/// <summary>
    /// An attribute definition in a DTD.
    /// </summary>
    public class AttDef
    {
        private string m_name;
        private AttributeType m_type;
        private string[] m_enumValues;
        private string m_default;
        private AttributePresence m_presence;

        /// <summary>
        /// Initialises a new instance of the <see cref="AttDef"/> class.
        /// </summary>
        /// <param name="name">The name of the attribute.</param>
        public AttDef(string name)
        {
            m_name = name;
        }

        /// <summary>
        /// The name of the attribute declared by this attribute definition.
        /// </summary>
        public string Name
        {
            get
            {
                return m_name;
            }
        }

        /// <summary>
        /// Gets of sets the default value of the attribute.
        /// </summary>
        public string Default
        {
            get
            {
                return m_default;
            }
            set
            {
                m_default = value;
            }
        }

        /// <summary>
        /// The constraints on the attribute's presence on an element.
        /// </summary>
        public AttributePresence AttributePresence
        {
            get
            {
                return m_presence;
            }
        }

        /// <summary>
        /// Gets or sets the possible enumerated values for the attribute.
        /// </summary>
        [SuppressMessage("Microsoft.Performance", "CA1819", Justification = "Changing this would break backwards compatibility with previous code using this library.")]
        public string[] EnumValues
        {
            get
            {
                return m_enumValues;
            }
        }

        /// <summary>
        /// Sets the attribute definition to have an enumerated value.
        /// </summary>
        /// <param name="enumValues">The possible values in the enumeration.</param>
        /// <param name="type">The type to set the attribute to.</param>
        /// <exception cref="ArgumentException">If the type parameter is not either <see cref="AttributeType.ENUMERATION"/> or <see cref="AttributeType.NOTATION"/>.</exception>
        public void SetEnumeratedType(string[] enumValues, AttributeType type)
        {
            if (type != AttributeType.ENUMERATION && type != AttributeType.NOTATION)
                throw new ArgumentException(string.Format(CultureInfo.CurrentUICulture, "AttributeType {0} is not valid for an attribute definition with an enumerated value.", type));

            m_enumValues = enumValues;
            m_type = type;
        }

        /// <summary>
        /// The <see cref="AttributeType"/> of the attribute declaration.
        /// </summary>
        public AttributeType Type
        {
            get
            {
                return m_type;
            }
        }

        /// <summary>
        /// Sets the type of the attribute definition.
        /// </summary>
        /// <param name="type">The string representation of the attribute type, corresponding to the values in the <see cref="AttributeType"/> enumeration.</param>
        public void SetType(string type)
        {
            switch (type) 
            {
                case "CDATA":
                    m_type = AttributeType.CDATA;
                    break;
                case "ENTITY":
                    m_type = AttributeType.ENTITY;
                    break;
                case "ENTITIES":
                    m_type = AttributeType.ENTITIES;
                    break;
                case "ID":
                    m_type = AttributeType.ID;
                    break;
                case "IDREF":
                    m_type = AttributeType.IDREF;
                    break;
                case "IDREFS":
                    m_type = AttributeType.IDREFS;
                    break;
                case "NAME":
                    m_type = AttributeType.NAME;
                    break;
                case "NAMES":
                    m_type = AttributeType.NAMES;
                    break;
                case "NMTOKEN":
                    m_type = AttributeType.NMTOKEN;
                    break;
                case "NMTOKENS":
                    m_type = AttributeType.NMTOKENS;
                    break;
                case "NUMBER":
                    m_type = AttributeType.NUMBER;
                    break;
                case "NUMBERS":
                    m_type = AttributeType.NUMBERS;
                    break;
                case "NUTOKEN":
                    m_type = AttributeType.NUTOKEN;
                    break;
                case "NUTOKENS":
                    m_type = AttributeType.NUTOKENS;
                    break;
                default:
                    throw new SgmlParseException(string.Format(CultureInfo.CurrentUICulture, "Attribute type '{0}' is not supported", type));
            }
        }

        /// <summary>
        /// Sets the attribute presence declaration.
        /// </summary>
        /// <param name="token">The string representation of the attribute presence, corresponding to one of the values in the <see cref="AttributePresence"/> enumeration.</param>
        /// <returns>true if the attribute presence implies the element has a default value.</returns>
        public bool SetPresence(string token)
        {
            bool hasDefault = true;
            if (string.Equals(token, "FIXED", StringComparison.OrdinalIgnoreCase)) 
            {
                m_presence = AttributePresence.Fixed;             
            } 
            else if (string.Equals(token, "REQUIRED", StringComparison.OrdinalIgnoreCase)) 
            {
                m_presence = AttributePresence.Required;
                hasDefault = false;
            }
            else if (string.Equals(token, "IMPLIED", StringComparison.OrdinalIgnoreCase)) 
            {
                m_presence = AttributePresence.Implied;
                hasDefault = false;
            }
            else 
            {
                throw new SgmlParseException(string.Format(CultureInfo.CurrentUICulture, "Attribute value '{0}' not supported", token));
            }

            return hasDefault;
        }
    }
}
