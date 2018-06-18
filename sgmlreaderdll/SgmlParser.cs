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

    internal static class StringUtilities
    {
        public static bool EqualsIgnoreCase(string a, string b){
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }
    }
}
