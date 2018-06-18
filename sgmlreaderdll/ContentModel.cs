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
    /// Defines the content model for an element.
    /// </summary>
    public class ContentModel
    {
        private DeclaredContent m_declaredContent;
        private int m_currentDepth;
        private Group m_model;

        /// <summary>
        /// Initialises a new instance of the <see cref="ContentModel"/> class.
        /// </summary>
        public ContentModel()
        {
            m_model = new Group(null);
        }

        /// <summary>
        /// The number of groups on the stack.
        /// </summary>
        public int CurrentDepth
        {
            get
            {
                return m_currentDepth;
            }
        }

        /// <summary>
        /// The allowed child content, specifying if nested children are not allowed and if so, what content is allowed.
        /// </summary>
        public DeclaredContent DeclaredContent
        {
            get
            {
                return m_declaredContent;
            }
        }

        /// <summary>
        /// Begins processing of a nested model group.
        /// </summary>
        public void PushGroup()
        {
            m_model = new Group(m_model);
            m_currentDepth++;
        }

        /// <summary>
        /// Finishes processing of a nested model group.
        /// </summary>
        /// <returns>The current depth of the group nesting, or -1 if there are no more groups to pop.</returns>
        public int PopGroup()
        {
            if (m_currentDepth == 0)
                return -1;

            m_currentDepth--;
            m_model.Parent.AddGroup(m_model);
            m_model = m_model.Parent;
            return m_currentDepth;
        }

        /// <summary>
        /// Adds a new symbol to the current group's members.
        /// </summary>
        /// <param name="sym">The symbol to add.</param>
        public void AddSymbol(string sym)
        {
            m_model.AddSymbol(sym);
        }

        /// <summary>
        /// Adds a connector onto the member list for the current group.
        /// </summary>
        /// <param name="c">The connector character to add.</param>
        /// <exception cref="SgmlParseException">
        /// If the content is not mixed and has no members yet, or if the group type has been set and the
        /// connector does not match the group type.
        /// </exception>
        public void AddConnector(char c)
        {
            m_model.AddConnector(c);
        }

        /// <summary>
        /// Adds an occurrence character for the current model group, setting it's <see cref="Occurrence"/> value.
        /// </summary>
        /// <param name="c">The occurrence character.</param>
        public void AddOccurrence(char c)
        {
            m_model.AddOccurrence(c);
        }

        /// <summary>
        /// Sets the contained content for the content model.
        /// </summary>
        /// <param name="dc">The text specified the permissible declared child content.</param>
        public void SetDeclaredContent(string dc)
        {
            // TODO: Validate that this can never combine with nexted groups?
            switch (dc)
            {
                case "EMPTY":
                    this.m_declaredContent = DeclaredContent.EMPTY;
                    break;
                case "RCDATA":
                    this.m_declaredContent = DeclaredContent.RCDATA;
                    break;
                case "CDATA":
                    this.m_declaredContent = DeclaredContent.CDATA;
                    break;
                default:
                    throw new SgmlParseException(string.Format(CultureInfo.CurrentUICulture, "Declared content type '{0}' is not supported", dc));
            }
        }

        /// <summary>
        /// Checks whether an element using this group can contain a specified element.
        /// </summary>
        /// <param name="name">The name of the element to look for.</param>
        /// <param name="dtd">The DTD to use during the checking.</param>
        /// <returns>true if an element using this group can contain the element, otherwise false.</returns>
        public bool CanContain(string name, SgmlDtd dtd)
        {
            if (m_declaredContent != DeclaredContent.Default)
                return false; // empty or text only node.

            return m_model.CanContain(name, dtd);
        }
    }
}
