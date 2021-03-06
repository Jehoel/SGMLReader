﻿/*
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

    public class GroupMember
	{
        public GroupMember(Group group)
		{
            if( group == null ) throw new ArgumentNullException(nameof(group));

            this.Group = group;
		}

        public GroupMember(String symbol)
		{
            if( String.IsNullOrWhiteSpace(symbol) ) throw new ArgumentNullException(nameof(symbol));

            this.Symbol = symbol;
		}

        public Group Group { get; }
        public String Symbol { get; }
	}

	/// <summary>
    /// Defines a group of elements nested within another element.
    /// </summary>
    public class Group
    {
        private readonly List<GroupMember> members;

        /// <summary>
        /// Initialises a new Content Model Group.
        /// </summary>
        /// <param name="parent">The parent model group.</param>
        public Group(Group parent)
        {
            this.Parent     = parent;
            this.members    = new List<GroupMember>();
            this.GroupType  = GroupType.None;
            this.Occurrence = Occurrence.Required;
        }

        public IReadOnlyList<GroupMember> Members => this.members;

        public Occurrence Occurrence { get; private set; }

        public Entity CurrentEntity { get; set; }

        /// <summary>
        /// Checks whether the group contains only text.
        /// </summary>
        /// <value>true if the group is of mixed content and has no members, otherwise false.</value>
        public bool TextOnly
        {
            get
            {
                return this.Mixed && this.Members.Count == 0;
            }
        }

        /// <summary>
        /// The parent group of this group.
        /// </summary>
        public Group Parent { get; private set; }

        public GroupType GroupType { get; private set; }

        public Boolean Mixed { get; private set; }

        /// <summary>
        /// Adds a new child model group to the end of the group's members.
        /// </summary>
        /// <param name="g">The model group to add.</param>
        public void AddGroup(Group g)
        {
            this.members.Add( new GroupMember( g ) );
        }

        /// <summary>
        /// Adds a new symbol to the group's members.
        /// </summary>
        /// <param name="sym">The symbol to add.</param>
        public void AddSymbol(string sym)
        {
            if (string.Equals(sym, "#PCDATA", StringComparison.OrdinalIgnoreCase)) 
            {               
                this.Mixed = true;
            } 
            else 
            {
                this.members.Add( new GroupMember( sym ) );
            }
        }

        /// <summary>
        /// Adds a connector onto the member list.
        /// </summary>
        /// <param name="c">The connector character to add.</param>
        /// <exception cref="SgmlParseException">
        /// If the content is not mixed and has no members yet, or if the group type has been set and the
        /// connector does not match the group type.
        /// </exception>
        public void AddConnector(char c)
        {
            if (!this.Mixed && this.members.Count == 0) 
            {
                throw new SgmlParseException(string.Format(CultureInfo.CurrentUICulture, "Missing token before connector '{0}'.", c));
            }

            GroupType gt = GroupType.None;
            switch (c) 
            {
                case ',': 
                    gt = GroupType.Sequence;
                    break;
                case '|':
                    gt = GroupType.Or;
                    break;
                case '&':
                    gt = GroupType.And;
                    break;
            }

            if (this.GroupType != GroupType.None && this.GroupType != gt) 
            {
                throw new SgmlParseException( String.Format( CultureInfo.CurrentUICulture, "Connector '{0}' is inconsistent with {1} group.", c, this.GroupType.ToString() ) );
            }

            this.GroupType = gt;
        }

        /// <summary>
        /// Adds an occurrence character for this group, setting it's <see cref="Occurrence"/> value.
        /// </summary>
        /// <param name="c">The occurrence character.</param>
        public void AddOccurrence(char c)
        {
            Occurrence o = Occurrence.Required;
            switch (c) 
            {
                case '?': 
                    o = Occurrence.Optional;
                    break;
                case '+':
                    o = Occurrence.OneOrMore;
                    break;
                case '*':
                    o = Occurrence.ZeroOrMore;
                    break;
            }

            this.Occurrence = o;
        }

        /// <summary>
        /// Checks whether an element using this group can contain a specified element.
        /// </summary>
        /// <param name="name">The name of the element to look for.</param>
        /// <param name="dtd">The DTD to use during the checking.</param>
        /// <returns>true if an element using this group can contain the element, otherwise false.</returns>
        /// <remarks>
        /// Rough approximation - this is really assuming an "Or" group
        /// </remarks>
        public bool CanContain(string name, SgmlDtd dtd)
        {
            if (dtd == null)
                throw new ArgumentNullException("dtd");

            // Do a simple search of members.
            foreach (GroupMember obj in members) 
            {
                if (obj.Symbol != null)
                {
                    if( string.Equals(obj.Symbol, name, StringComparison.OrdinalIgnoreCase))
                        return true;
                } 
            }
            // didn't find it, so do a more expensive search over child elements
            // that have optional start tags and over child groups.
            foreach (GroupMember obj in members) 
            {
                if (obj.Symbol != null)
                {
                    ElementDecl e = dtd.FindElement(obj.Symbol);
                    if (e != null) 
                    {
                        if (e.StartTagOptional) 
                        {
                            // tricky case, the start tag is optional so element may be
                            // allowed inside this guy!
                            if (e.CanContain(name, dtd))
                                return true;
                        }
                    }
                } 
                else 
                {
                    Group m = obj.Group;
                    if (m.CanContain(name, dtd)) 
                        return true;
                }
            }

            return false;
        }
    }
}
