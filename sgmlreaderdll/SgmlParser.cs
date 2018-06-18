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
    /// Thrown if any errors occur while parsing the source.
    /// </summary>
    [Serializable]
    public class SgmlParseException : Exception
    {
        private string m_entityContext;

        /// <summary>
        /// Instantiates a new instance of SgmlParseException with no specific error information.
        /// </summary>
        public SgmlParseException()
        {
        }

        /// <summary>
        /// Instantiates a new instance of SgmlParseException with an error message describing the problem.
        /// </summary>
        /// <param name="message">A message describing the error that occurred</param>
        public SgmlParseException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Instantiates a new instance of SgmlParseException with an error message describing the problem.
        /// </summary>
        /// <param name="message">A message describing the error that occurred</param>
        /// <param name="e">The entity on which the error occurred.</param>
        public SgmlParseException(string message, Entity e)
            : base(message)
        {
            if (e != null)
                m_entityContext = e.Context();
        }

        /// <summary>
        /// Instantiates a new instance of SgmlParseException with an error message describing the problem.
        /// </summary>
        /// <param name="message">A message describing the error that occurred</param>
        /// <param name="innerException">The original exception that caused the problem.</param>
        public SgmlParseException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the SgmlParseException class with serialized data. 
        /// </summary>
        /// <param name="streamInfo">The object that holds the serialized object data.</param>
        /// <param name="streamCtx">The contextual information about the source or destination.</param>
        protected SgmlParseException(SerializationInfo streamInfo, StreamingContext streamCtx)
            : base(streamInfo, streamCtx)
        {
            if (streamInfo != null)
                m_entityContext = streamInfo.GetString("entityContext");
        }

        /// <summary>
        /// Contextual information detailing the entity on which the error occurred.
        /// </summary>
        public string EntityContext
        {
            get
            {
                return m_entityContext;
            }
        }

        /// <summary>
        /// Populates a SerializationInfo with the data needed to serialize the exception.
        /// </summary>
        /// <param name="info">The <see cref="SerializationInfo"/> to populate with data. </param>
        /// <param name="context">The destination (see <see cref="StreamingContext"/>) for this serialization.</param>
        [SecurityPermission(SecurityAction.Demand, SerializationFormatter=true)]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
                throw new ArgumentNullException("info");

            info.AddValue("entityContext", m_entityContext);
            base.GetObjectData(info, context);
        }
    }

    /// <summary>
    /// The different types of literal text returned by the SgmlParser.
    /// </summary>
    public enum LiteralType
    {
        /// <summary>
        /// CDATA text literals.
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1705", Justification = "This capitalisation is appropriate since the value it represents has all upper-case capitalisation.")]
        CDATA,

        /// <summary>
        /// SDATA entities.
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1705", Justification = "This capitalisation is appropriate since the value it represents has all upper-case capitalisation.")]
        SDATA,

        /// <summary>
        /// The contents of a Processing Instruction.
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1705", Justification = "This capitalisation is appropriate since the value it represents has all upper-case capitalisation.")]
        PI
    };

    

    

    internal abstract class Ucs4Decoder : Decoder {
        internal byte[] temp = new byte[4];
        internal int tempBytes = 0;
        public override int GetCharCount(byte[] bytes, int index, int count) {
            return (count + tempBytes) / 4;
        }
        internal abstract int GetFullChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex);
        public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex) {
            int i = tempBytes;

            if (tempBytes > 0) {
                for (; i < 4; i++) {
                    temp[i] = bytes[byteIndex];
                    byteIndex++;
                    byteCount--;
                }
                i = 1;
                GetFullChars(temp, 0, 4, chars, charIndex);
                charIndex++;
            } else
                i = 0;
            i = GetFullChars(bytes, byteIndex, byteCount, chars, charIndex) + i;

            int j = (tempBytes + byteCount) % 4;
            byteCount += byteIndex;
            byteIndex = byteCount - j;
            tempBytes = 0;

            if (byteIndex >= 0)
                for (; byteIndex < byteCount; byteIndex++) {
                    temp[tempBytes] = bytes[byteIndex];
                    tempBytes++;
                }
            return i;
        }
        internal static char UnicodeToUTF16(UInt32 code) {
            byte lowerByte, higherByte;
            lowerByte = (byte)(0xD7C0 + (code >> 10));
            higherByte = (byte)(0xDC00 | code & 0x3ff);
            return ((char)((higherByte << 8) | lowerByte));
        }
    }

    internal class Ucs4DecoderBigEngian : Ucs4Decoder {
        internal override int GetFullChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex) {
            UInt32 code;
            int i, j;
            byteCount += byteIndex;
            for (i = byteIndex, j = charIndex; i + 3 < byteCount; ) {
                code = (UInt32)(((bytes[i + 3]) << 24) | (bytes[i + 2] << 16) | (bytes[i + 1] << 8) | (bytes[i]));
                if (code > 0x10FFFF) {
                    throw new SgmlParseException(string.Format(CultureInfo.CurrentUICulture, "Invalid character 0x{0:x} in encoding", code));
                } else if (code > 0xFFFF) {
                    chars[j] = UnicodeToUTF16(code);
                    j++;
                } else {
                    if (code >= 0xD800 && code <= 0xDFFF) {
                        throw new SgmlParseException(string.Format(CultureInfo.CurrentUICulture, "Invalid character 0x{0:x} in encoding", code));
                    } else {
                        chars[j] = (char)code;
                    }
                }
                j++;
                i += 4;
            }
            return j - charIndex;
        }
    }

    internal class Ucs4DecoderLittleEndian : Ucs4Decoder {
        internal override int GetFullChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex) {
            UInt32 code;
            int i, j;
            byteCount += byteIndex;
            for (i = byteIndex, j = charIndex; i + 3 < byteCount; ) {
                code = (UInt32)(((bytes[i]) << 24) | (bytes[i + 1] << 16) | (bytes[i + 2] << 8) | (bytes[i + 3]));
                if (code > 0x10FFFF) {
                    throw new SgmlParseException(string.Format(CultureInfo.CurrentUICulture, "Invalid character 0x{0:x} in encoding", code));
                } else if (code > 0xFFFF) {
                    chars[j] = UnicodeToUTF16(code);
                    j++;
                } else {
                    if (code >= 0xD800 && code <= 0xDFFF) {
                        throw new SgmlParseException(string.Format(CultureInfo.CurrentUICulture, "Invalid character 0x{0:x} in encoding", code));
                    } else {
                        chars[j] = (char)code;
                    }
                }
                j++;
                i += 4;
            }
            return j - charIndex;
        }
    }

    /// <summary>
    /// An element declaration in a DTD.
    /// </summary>
    public class ElementDecl
    {
        private string m_name;
        private bool m_startTagOptional;
        private bool m_endTagOptional;
        private ContentModel m_contentModel;
        private string[] m_inclusions;
        private string[] m_exclusions;
        private Dictionary<string, AttDef> m_attList;

        /// <summary>
        /// Initialises a new element declaration instance.
        /// </summary>
        /// <param name="name">The name of the element.</param>
        /// <param name="sto">Whether the start tag is optional.</param>
        /// <param name="eto">Whether the end tag is optional.</param>
        /// <param name="cm">The <see cref="ContentModel"/> of the element.</param>
        /// <param name="inclusions"></param>
        /// <param name="exclusions"></param>
        public ElementDecl(string name, bool sto, bool eto, ContentModel cm, string[] inclusions, string[] exclusions)
        {
            m_name = name;
            m_startTagOptional = sto;
            m_endTagOptional = eto;
            m_contentModel = cm;
            m_inclusions = inclusions;
            m_exclusions = exclusions;
        }

        /// <summary>
        /// The element name.
        /// </summary>
        public string Name
        {
            get
            {
                return m_name;
            }
        }

        /// <summary>
        /// The <see cref="Sgml.ContentModel"/> of the element declaration.
        /// </summary>
        public ContentModel ContentModel
        {
            get
            {
                return m_contentModel;
            }
        }

        /// <summary>
        /// Whether the end tag of the element is optional.
        /// </summary>
        /// <value>true if the end tag of the element is optional, otherwise false.</value>
        public bool EndTagOptional
        {
            get
            {
                return m_endTagOptional;
            }
        }

        /// <summary>
        /// Whether the start tag of the element is optional.
        /// </summary>
        /// <value>true if the start tag of the element is optional, otherwise false.</value>
        public bool StartTagOptional
        {
            get
            {
                return m_startTagOptional;
            }
        }

        /// <summary>
        /// Finds the attribute definition with the specified name.
        /// </summary>
        /// <param name="name">The name of the <see cref="AttDef"/> to find.</param>
        /// <returns>The <see cref="AttDef"/> with the specified name.</returns>
        /// <exception cref="InvalidOperationException">If the attribute list has not yet been initialised.</exception>
        public AttDef FindAttribute(string name)
        {
            if (m_attList == null)
                throw new InvalidOperationException("The attribute list for the element declaration has not been initialised.");

            AttDef a;
            m_attList.TryGetValue(name.ToUpperInvariant(), out a);
            return a;
        }

        /// <summary>
        /// Adds attribute definitions to the element declaration.
        /// </summary>
        /// <param name="list">The list of attribute definitions to add.</param>
        public void AddAttDefs(Dictionary<string, AttDef> list)
        {
            if (list == null)
                throw new ArgumentNullException("list");

            if (m_attList == null) 
            {
                m_attList = list;
            } 
            else 
            {
                foreach (AttDef a in list.Values) 
                {
                    if (!m_attList.ContainsKey(a.Name)) 
                    {
                        m_attList.Add(a.Name, a);
                    }
                }
            }
        }

        /// <summary>
        /// Tests whether this element can contain another specified element.
        /// </summary>
        /// <param name="name">The name of the element to check for.</param>
        /// <param name="dtd">The DTD to use to do the check.</param>
        /// <returns>True if the specified element can be contained by this element.</returns>
        public bool CanContain(string name, SgmlDtd dtd)
        {            
            // return true if this element is allowed to contain the given element.
            if (m_exclusions != null) 
            {
                foreach (string s in m_exclusions) 
                {
                    if (string.Equals(s, name, StringComparison.OrdinalIgnoreCase))
                        return false;
                }
            }

            if (m_inclusions != null) 
            {
                foreach (string s in m_inclusions) 
                {
                    if (string.Equals(s, name, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            return m_contentModel.CanContain(name, dtd);
        }
    }

    /// <summary>
    /// Where nested subelements cannot occur within an element, its contents can be declared to consist of one of the types of declared content contained in this enumeration.
    /// </summary>
    public enum DeclaredContent
    {
        /// <summary>
        /// Not defined.
        /// </summary>
        Default,
        
        /// <summary>
        /// Character data (CDATA), which contains only valid SGML characters.
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1705", Justification = "This capitalisation is appropriate since the value it represents has all upper-case capitalisation.")]
        CDATA,
        
        /// <summary>
        /// Replaceable character data (RCDATA), which can contain text, character references and/or general entity references that resolve to character data.
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1705", Justification = "This capitalisation is appropriate since the value it represents has all upper-case capitalisation.")]
        RCDATA,
        
        /// <summary>
        /// Empty element (EMPTY), i.e. having no contents, or contents that can be generated by the program.
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1705", Justification = "This capitalisation is appropriate since the value it represents has all upper-case capitalisation.")]
        EMPTY
    }

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

    /// <summary>
    /// The type of the content model group, defining the order in which child elements can occur.
    /// </summary>
    public enum GroupType
    {
        /// <summary>
        /// No model group.
        /// </summary>
        None,
        
        /// <summary>
        /// All elements must occur, in any order.
        /// </summary>
        And,
        
        /// <summary>
        /// One (and only one) must occur.
        /// </summary>
        Or,
        
        /// <summary>
        /// All element must occur, in the specified order.
        /// </summary>
        Sequence 
    };

    /// <summary>
    /// Qualifies the occurrence of a child element within a content model group.
    /// </summary>
    public enum Occurrence
    {
        /// <summary>
        /// The element is required and must occur only once.
        /// </summary>
        Required,
        
        /// <summary>
        /// The element is optional and must occur once at most.
        /// </summary>
        Optional,
        
        /// <summary>
        /// The element is optional and can be repeated.
        /// </summary>
        ZeroOrMore,
        
        /// <summary>
        /// The element must occur at least once or more times.
        /// </summary>
        OneOrMore
    }

    /// <summary>
    /// Defines a group of elements nested within another element.
    /// </summary>
    public class Group
    {
        private Group m_parent;
        private ArrayList Members;
        private GroupType m_groupType;
        private Occurrence m_occurrence;
        private bool Mixed;

        /// <summary>
        /// The <see cref="Occurrence"/> of this group.
        /// </summary>
        public Occurrence Occurrence
        {
            get
            {
                return m_occurrence;
            }
        }

        /// <summary>
        /// Checks whether the group contains only text.
        /// </summary>
        /// <value>true if the group is of mixed content and has no members, otherwise false.</value>
        public bool TextOnly
        {
            get
            {
                return this.Mixed && Members.Count == 0;
            }
        }

        /// <summary>
        /// The parent group of this group.
        /// </summary>
        public Group Parent
        {
            get
            {
                return m_parent;
            }
        }

        /// <summary>
        /// Initialises a new Content Model Group.
        /// </summary>
        /// <param name="parent">The parent model group.</param>
        public Group(Group parent)
        {
            m_parent = parent;
            Members = new ArrayList();
            m_groupType = GroupType.None;
            m_occurrence = Occurrence.Required;
        }

        /// <summary>
        /// Adds a new child model group to the end of the group's members.
        /// </summary>
        /// <param name="g">The model group to add.</param>
        public void AddGroup(Group g)
        {
            Members.Add(g);
        }

        /// <summary>
        /// Adds a new symbol to the group's members.
        /// </summary>
        /// <param name="sym">The symbol to add.</param>
        public void AddSymbol(string sym)
        {
            if (string.Equals(sym, "#PCDATA", StringComparison.OrdinalIgnoreCase)) 
            {               
                Mixed = true;
            } 
            else 
            {
                Members.Add(sym);
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
            if (!Mixed && Members.Count == 0) 
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

            if (this.m_groupType != GroupType.None && this.m_groupType != gt) 
            {
                throw new SgmlParseException(string.Format(CultureInfo.CurrentUICulture, "Connector '{0}' is inconsistent with {1} group.", c, m_groupType.ToString()));
            }

            m_groupType = gt;
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

            m_occurrence = o;
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
            foreach (object obj in Members) 
            {
                if (obj is string) 
                {
                    if( string.Equals((string)obj, name, StringComparison.OrdinalIgnoreCase))
                        return true;
                } 
            }
            // didn't find it, so do a more expensive search over child elements
            // that have optional start tags and over child groups.
            foreach (object obj in Members) 
            {
                string s = obj as string;
                if (s != null)
                {
                    ElementDecl e = dtd.FindElement(s);
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
                    Group m = (Group)obj;
                    if (m.CanContain(name, dtd)) 
                        return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Defines the different possible attribute types.
    /// </summary>
    public enum AttributeType
    {
        /// <summary>
        /// Attribute type not specified.
        /// </summary>
        Default,

        /// <summary>
        /// The attribute contains text (with no markup).
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1705", Justification = "This capitalisation is appropriate since the value it represents has all upper-case capitalisation.")]
        CDATA,
        
        /// <summary>
        /// The attribute contains an entity declared in a DTD.
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1705", Justification = "This capitalisation is appropriate since the value it represents has all upper-case capitalisation.")]
        ENTITY,

        /// <summary>
        /// The attribute contains a number of entities declared in a DTD.
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1705", Justification = "This capitalisation is appropriate since the value it represents has all upper-case capitalisation.")]
        ENTITIES,
        
        /// <summary>
        /// The attribute is an id attribute uniquely identifie the element it appears on.
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1705", Justification = "This capitalisation is appropriate since the value it represents has all upper-case capitalisation.")]
        [SuppressMessage("Microsoft.Naming", "CA1706", Justification = "This capitalisation is appropriate since the value it represents has all upper-case capitalisation.")]
        ID,
        
        /// <summary>
        /// The attribute value can be any declared subdocument or data entity name.
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1705", Justification = "This capitalisation is appropriate since the value it represents has all upper-case capitalisation.")]
        IDREF,
        
        /// <summary>
        /// The attribute value is a list of (space separated) declared subdocument or data entity names.
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1705", Justification = "This capitalisation is appropriate since the value it represents has all upper-case capitalisation.")]
        IDREFS,
        
        /// <summary>
        /// The attribute value is a SGML Name.
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1705", Justification = "This capitalisation is appropriate since the value it represents has all upper-case capitalisation.")]
        NAME,
        
        /// <summary>
        /// The attribute value is a list of (space separated) SGML Names.
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1705", Justification = "This capitalisation is appropriate since the value it represents has all upper-case capitalisation.")]
        NAMES,
        
        /// <summary>
        /// The attribute value is an XML name token (i.e. contains only name characters, but in this case with digits and other valid name characters accepted as the first character).
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1705", Justification = "This capitalisation is appropriate since the value it represents has all upper-case capitalisation.")]
        NMTOKEN,

        /// <summary>
        /// The attribute value is a list of (space separated) XML NMTokens.
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1705", Justification = "This capitalisation is appropriate since the value it represents has all upper-case capitalisation.")]
        NMTOKENS,

        /// <summary>
        /// The attribute value is a number.
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1705", Justification = "This capitalisation is appropriate since the value it represents has all upper-case capitalisation.")]
        NUMBER,
        
        /// <summary>
        /// The attribute value is a list of (space separated) numbers.
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1705", Justification = "This capitalisation is appropriate since the value it represents has all upper-case capitalisation.")]
        NUMBERS,
        
        /// <summary>
        /// The attribute value is a number token (i.e. a name that starts with a number).
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1705", Justification = "This capitalisation is appropriate since the value it represents has all upper-case capitalisation.")]
        NUTOKEN,
        
        /// <summary>
        /// The attribute value is a list of number tokens.
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1705", Justification = "This capitalisation is appropriate since the value it represents has all upper-case capitalisation.")]
        NUTOKENS,
        
        /// <summary>
        /// Attribute value is a member of the bracketed list of notation names that qualifies this reserved name.
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1705", Justification = "This capitalisation is appropriate since the value it represents has all upper-case capitalisation.")]
        NOTATION,
        
        /// <summary>
        /// The attribute value is one of a set of allowed names.
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1705", Justification = "This capitalisation is appropriate since the value it represents has all upper-case capitalisation.")]
        ENUMERATION
    }

    /// <summary>
    /// Defines the different constraints on an attribute's presence on an element.
    /// </summary>
    public enum AttributePresence
    {
        /// <summary>
        /// The attribute has a default value, and its presence is optional.
        /// </summary>
        Default,

        /// <summary>
        /// The attribute has a fixed value, if present.
        /// </summary>
        Fixed,

        /// <summary>
        /// The attribute must always be present on every element.
        /// </summary>
        Required,
        
        /// <summary>
        /// The element is optional.
        /// </summary>
        Implied
    }

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

/* JB: Replaced this with a Dictionary<string, AttDef>
    public class AttList : IEnumerable
    {
        Hashtable AttDefs;
        
        public AttList()
        {
            AttDefs = new Hashtable();
        }

        public void Add(AttDef a)
        {
            AttDefs.Add(a.Name, a);
        }

        public AttDef this[string name]
        {
            get 
            {
                return (AttDef)AttDefs[name];
            }
        }

        public IEnumerator GetEnumerator()
        {
            return AttDefs.Values.GetEnumerator();
        }
    }
*/
    

    internal static class StringUtilities
    {
        public static bool EqualsIgnoreCase(string a, string b){
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }
    }
}
