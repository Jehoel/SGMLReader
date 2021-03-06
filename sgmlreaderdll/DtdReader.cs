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

namespace Sgml
{
    /// <summary>
    /// Provides DTD parsing and support for the SgmlParser framework.
    /// </summary>
    public class SgmlDtd
    {
        private Dictionary<string, ElementDecl> m_elements;
        private Dictionary<string, Entity> m_pentities;
        private Dictionary<string, Entity> m_entities;
        private StringBuilder m_sb;
        private Entity m_current;
        private Boolean m_inDocType;

        /// <summary>
        /// Initialises a new instance of the <see cref="SgmlDtd"/> class.
        /// </summary>
        /// <param name="name">The name of the DTD.</param>
        public SgmlDtd(string name)
        {
            this.Name = name;
            this.m_elements = new Dictionary<string,ElementDecl>();
            this.m_pentities = new Dictionary<string, Entity>();
            this.m_entities = new Dictionary<string, Entity>();
            this.m_sb = new StringBuilder();
        }

        public IReadOnlyDictionary<String, ElementDecl> Elements       => this.m_elements;
        public IReadOnlyDictionary<String, Entity>      ParsedEntities => this.m_pentities;
        public IReadOnlyDictionary<String, Entity>      Entities       => this.m_entities;

        /// <summary>
        /// The name of the DTD.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Gets the XmlNameTable associated with this implementation.
        /// </summary>
        /// <value>The XmlNameTable enabling you to get the atomized version of a string within the node.</value>
        public XmlNameTable NameTable
        {
            get
            {
                return null;
            }
        }

        /// <summary>
        /// Parses a DTD and creates a <see cref="SgmlDtd"/> instance that encapsulates the DTD.
        /// </summary>
        /// <param name="baseUri">The base URI of the DTD.</param>
        /// <param name="name">The name of the DTD.</param>
        /// <param name="pubid"></param>
        /// <param name="url"></param>
        /// <param name="subset"></param>
        /// <param name="proxy"></param>
        /// <returns>A new <see cref="SgmlDtd"/> instance that encapsulates the DTD.</returns>
        public static SgmlDtd Parse(Uri baseUri, string name, string pubid, string url, string subset, string proxy)
        {
            SgmlDtd dtd = new SgmlDtd(name);
            if (!string.IsNullOrEmpty(url))
            {
                dtd.PushEntity(baseUri, new Entity(dtd.Name, pubid, url, proxy));
            }

            if (!string.IsNullOrEmpty(subset))
            {
                dtd.PushEntity(baseUri, new Entity(name, subset));
            }

            try 
            {
                dtd.Parse();
            } 
            catch (ApplicationException e)
            {
                throw new SgmlParseException(e.Message + dtd.m_current.Context());
            }

            return dtd;
        }

        /// <summary>
        /// Parses a DTD and creates a <see cref="SgmlDtd"/> instance that encapsulates the DTD.
        /// </summary>
        /// <param name="baseUri">The base URI of the DTD.</param>
        /// <param name="name">The name of the DTD.</param>
        /// <param name="input">The reader to load the DTD from.</param>
        /// <param name="subset"></param>
        /// <param name="proxy">The proxy server to use when loading resources.</param>
        /// <returns>A new <see cref="SgmlDtd"/> instance that encapsulates the DTD.</returns>
        [SuppressMessage("Microsoft.Reliability", "CA2000", Justification = "The entities created here are not temporary and should not be disposed here.")]
        public static SgmlDtd Parse(Uri baseUri, string name, TextReader input, string subset, string proxy)
        {
            SgmlDtd dtd = new SgmlDtd(name);
            dtd.PushEntity(baseUri, new Entity(dtd.Name, baseUri, input, proxy));
            if (!string.IsNullOrEmpty(subset))
            {
                dtd.PushEntity(baseUri, new Entity(name, subset));
            }

            try
            {
                dtd.Parse();
            } 
            catch (ApplicationException e)
            {
                throw new SgmlParseException(e.Message + dtd.m_current.Context());
            }

            return dtd;
        }

        /// <summary>
        /// Finds an entity in the DTD with the specified name.
        /// </summary>
        /// <param name="name">The name of the <see cref="Entity"/> to find.</param>
        /// <returns>The specified Entity from the DTD.</returns>
        public Entity FindEntity(string name)
        {
            Entity e;
            this.m_entities.TryGetValue(name, out e);
            return e;
        }

        /// <summary>
        /// Finds an element declaration in the DTD with the specified name.
        /// </summary>
        /// <param name="name">The name of the <see cref="ElementDecl"/> to find and return.</param>
        /// <returns>The <see cref="ElementDecl"/> matching the specified name.</returns>
        public ElementDecl FindElement(string name)
        {
            ElementDecl el;
            m_elements.TryGetValue(name.ToUpperInvariant(), out el);
            return el;
        }

        //-------------------------------- Parser -------------------------
        private void PushEntity(Uri baseUri, Entity e)
        {
            e.Open(this.m_current, baseUri);
            this.m_current = e;
            this.m_current.ReadChar();
        }

        private void PopEntity()
        {
            if (this.m_current != null) this.m_current.Close();
            if (this.m_current.Parent != null) 
            {
                this.m_current = this.m_current.Parent;
            } 
            else 
            {
                this.m_current = null;
            }
        }

        private void Parse()
        {
            char ch = this.m_current.Lastchar;
            while (true) 
            {
                switch (ch) 
                {
                    case Entity.EOF:
                        PopEntity();
                        if (this.m_current == null)
                            return;
                        ch = this.m_current.Lastchar;
                        break;
                    case ' ':
                    case '\n':
                    case '\r':
                    case '\t':
                        ch = this.m_current.ReadChar();
                        break;
                    case '<':
                        ParseMarkup();
                        ch = this.m_current.ReadChar();
                        break;
                    case '%':
                        Entity e = ParseParameterEntity(SgmlDtd.WhiteSpace);
                        try 
                        {
                            PushEntity(this.m_current.ResolvedUri, e);
                        } 
                        catch (Exception ex) 
                        {
                            // BUG: need an error log.
                            Console.WriteLine(ex.Message + this.m_current.Context());
                        }
                        ch = this.m_current.Lastchar;
                        break;
                    case ']':
                        if( this.m_inDocType )
                        {
                            return;
                        }
                        else
                        {
                            goto default;
                        }
                    default:
                        this.m_current.Error("Unexpected character '{0}'", ch);
                        break;
                }               
            }
        }

        void ParseMarkup()
        {
            char ch = this.m_current.ReadChar();
            if (ch != '!') 
            {
                this.m_current.Error("Found '{0}', but expecing declaration starting with '<!'");
                return;
            }
            ch = this.m_current.ReadChar();
            if (ch == '-') 
            {
                ch = this.m_current.ReadChar();
                if (ch != '-') this.m_current.Error("Expecting comment '<!--' but found {0}", ch);
                this.m_current.ScanToEnd(this.m_sb, "Comment", "-->");
            } 
            else if (ch == '[') 
            {
                ParseMarkedSection();
            }
            else 
            {
                string token = this.m_current.ScanToken(this.m_sb, SgmlDtd.WhiteSpace, true);
                switch (token) 
                {
                    case "ENTITY":
                        ParseEntity();
                        break;
                    case "ELEMENT":
                        ParseElementDecl();
                        break;
                    case "ATTLIST":
                        ParseAttList();
                        break;
                    case "DOCTYPE":
                        ParseDoctype();
                        break;
                    default:
                        this.m_current.Error("Invalid declaration '<!{0}'.  Expecting 'ENTITY', 'ELEMENT', 'DOCTYPE' or 'ATTLIST'.", token);
                        break;
                }
            }
        }

        char ParseDeclComments()
        {
            char ch = this.m_current.Lastchar;
            while (ch == '-') 
            {
                ch = ParseDeclComment(true);
            }
            return ch;
        }

        char ParseDeclComment(bool full)
        {
            // This method scans over a comment inside a markup declaration.
            char ch = this.m_current.ReadChar();
            if (full && ch != '-') this.m_current.Error("Expecting comment delimiter '--' but found {0}", ch);
            this.m_current.ScanToEnd(this.m_sb, "Markup Comment", "--");
            return this.m_current.SkipWhitespace();
        }

        void ParseMarkedSection()
        {
            // <![^ name [ ... ]]>
            this.m_current.ReadChar(); // move to next char.
            string name = ScanName("[");
            if (string.Equals(name, "INCLUDE", StringComparison.OrdinalIgnoreCase)) 
            {
                ParseIncludeSection();
            } 
            else if (string.Equals(name, "IGNORE", StringComparison.OrdinalIgnoreCase)) 
            {
                ParseIgnoreSection();
            }
            else 
            {
                this.m_current.Error("Unsupported marked section type '{0}'", name);
            }
        }

        [SuppressMessage("Microsoft.Performance", "CA1822", Justification = "This is not yet implemented and will use 'this' in the future.")]
        [SuppressMessage("Microsoft.Globalization", "CA1303", Justification = "The use of a literal here is only due to this not yet being implemented.")]
        private void ParseIncludeSection()
        {
            throw new NotImplementedException("Include Section");
        }

        void ParseIgnoreSection()
        {
            char ch = this.m_current.SkipWhitespace();
            if (ch != '[') this.m_current.Error("Expecting '[' but found {0}", ch);
            this.m_current.ScanToEnd(this.m_sb, "Conditional Section", "]]>");
        }

        string ScanName(string term)
        {
            // skip whitespace, scan name (which may be parameter entity reference
            // which is then expanded to a name)
            char ch = this.m_current.SkipWhitespace();
            if (ch == '%') 
            {
                Entity e = ParseParameterEntity(term);
                ch = this.m_current.Lastchar;
                // bugbug - need to support external and nested parameter entities
                if (!e.IsInternal) throw new NotSupportedException("External parameter entity resolution");
                return e.Literal.Trim();
            } 
            else 
            {
                return this.m_current.ScanToken(this.m_sb, term, true);
            }
        }

        private Entity ParseParameterEntity(string term)
        {
            // almost the same as this.current.ScanToken, except we also terminate on ';'
            this.m_current.ReadChar();
            string name =  this.m_current.ScanToken(this.m_sb, ";"+term, false);
            if (this.m_current.Lastchar == ';') 
                this.m_current.ReadChar();
            Entity e = GetParameterEntity(name);
            return e;
        }

        private Entity GetParameterEntity(string name)
        {
            Entity e = null;
            m_pentities.TryGetValue(name, out e);
            if (e == null)
                this.m_current.Error("Reference to undefined parameter entity '{0}'", name);

            return e;
        }

        /// <summary>
        /// Returns a dictionary for looking up entities by their <see cref="Entity.Literal"/> value.
        /// </summary>
        /// <returns>A dictionary for looking up entities by their <see cref="Entity.Literal"/> value.</returns>
        [SuppressMessage("Microsoft.Design", "CA1024", Justification = "This method creates and copies a dictionary, so exposing it as a property is not appropriate.")]
        public Dictionary<string, Entity> GetEntitiesLiteralNameLookup()
        {
            Dictionary<string, Entity> hashtable = new Dictionary<string, Entity>();
            foreach (Entity entity in this.m_entities.Values)
                hashtable[entity.Literal] = entity;

            return hashtable;
        }
        
        private const string WhiteSpace = " \r\n\t";

        private void ParseEntity()
        {
            char ch = this.m_current.SkipWhitespace();
            bool pe = (ch == '%');
            if (pe)
            {
                // parameter entity.
                this.m_current.ReadChar(); // move to next char
                ch = this.m_current.SkipWhitespace();
            }
            string name = this.m_current.ScanToken(this.m_sb, SgmlDtd.WhiteSpace, true);
            ch = this.m_current.SkipWhitespace();
            Entity e = null;
            if (ch == '"' || ch == '\'') 
            {
                string literal = this.m_current.ScanLiteral(this.m_sb, ch);
                e = new Entity(name, literal);                
            } 
            else 
            {
                string pubid = null;
                string extid = null;
                string tok = this.m_current.ScanToken(this.m_sb, SgmlDtd.WhiteSpace, true);
                if (Entity.IsLiteralType(tok))
                {
                    ch = this.m_current.SkipWhitespace();
                    string literal = this.m_current.ScanLiteral(this.m_sb, ch);
                    e = new Entity(name, literal);
                    e.SetLiteralType(tok);
                }
                else 
                {
                    extid = tok;
                    if (string.Equals(extid, "PUBLIC", StringComparison.OrdinalIgnoreCase)) 
                    {
                        ch = this.m_current.SkipWhitespace();
                        if (ch == '"' || ch == '\'') 
                        {
                            pubid = this.m_current.ScanLiteral(this.m_sb, ch);
                        } 
                        else 
                        {
                            this.m_current.Error("Expecting public identifier literal but found '{0}'",ch);
                        }
                    } 
                    else if (!string.Equals(extid, "SYSTEM", StringComparison.OrdinalIgnoreCase)) 
                    {
                        this.m_current.Error("Invalid external identifier '{0}'.  Expecing 'PUBLIC' or 'SYSTEM'.", extid);
                    }
                    string uri = null;
                    ch = this.m_current.SkipWhitespace();
                    if (ch == '"' || ch == '\'') 
                    {
                        uri = this.m_current.ScanLiteral(this.m_sb, ch);
                    } 
                    else if (ch != '>')
                    {
                        this.m_current.Error("Expecting system identifier literal but found '{0}'",ch);
                    }
                    e = new Entity(name, pubid, uri, this.m_current.Proxy);
                }
            }
            ch = this.m_current.SkipWhitespace();
            if (ch == '-') 
                ch = ParseDeclComments();
            if (ch != '>') 
            {
                this.m_current.Error("Expecting end of entity declaration '>' but found '{0}'", ch);  
            }           
            if (pe)
                this.m_pentities.Add(e.Name, e);
            else
                this.m_entities.Add(e.Name, e);
        }

        private void ParseDoctype()
        {
            this.m_inDocType = true;

            char ch = this.m_current.SkipWhitespace();
            String name = this.m_current.ScanToken(this.m_sb, SgmlDtd.WhiteSpace, true);
            if( this.Name != null )
            {
                this.m_current.Error( "This DTD has already been named. If the <!DOCTYPE> specifies a name then set name to null in the constructor." );
            }
            this.Name = name;

            ch = this.m_current.SkipWhitespace();
            if (ch != '[')
            {
                this.m_current.Error("Expected opening square-bracket of DOCTYPE. Invalid syntax at '{0}'", ch);  
            }
            this.m_current.ReadChar(); // move to next char
            ch = this.m_current.SkipWhitespace();
            this.Parse(); // Resume as though the `<!DOCTYPE` didn't exist.
            // but catch the trailing `]>`

            ch = this.m_current.Lastchar;
            if( ch != ']' )
            {
                this.m_current.Error("Expected ']' closing square-bracket of DOCTYPE. Invalid syntax at '{0}'", ch);  
            }

            ch = this.m_current.ReadChar();
            if( ch != '>' )
            {
                this.m_current.Error("Expected '>' closing angle-bracket of DOCTYPE. Invalid syntax at '{0}'", ch);  
            }
            this.m_inDocType = false;
        }

        private void ParseElementDecl()
        {
            char ch = this.m_current.SkipWhitespace();
            string[] names = ParseNameGroup(ch, true);
            ch = char.ToUpperInvariant(this.m_current.SkipWhitespace());
            bool sto = false;
            bool eto = false;
            if (ch == 'O' || ch == '-') {
                sto = (ch == 'O'); // start tag optional?   
                this.m_current.ReadChar();
                ch = char.ToUpperInvariant(this.m_current.SkipWhitespace());
                if (ch == 'O' || ch == '-'){
                    eto = (ch == 'O'); // end tag optional? 
                    ch = this.m_current.ReadChar();
                }
            }
            ch = this.m_current.SkipWhitespace();
            ContentModel cm = ParseContentModel(ch);
            ch = this.m_current.SkipWhitespace();

            string [] exclusions = null;
            string [] inclusions = null;

            if (ch == '-') 
            {
                ch = this.m_current.ReadChar();
                if (ch == '(') 
                {
                    exclusions = ParseNameGroup(ch, true);
                    ch = this.m_current.SkipWhitespace();
                }
                else if (ch == '-') 
                {
                    ch = ParseDeclComment(false);
                } 
                else 
                {
                    this.m_current.Error("Invalid syntax at '{0}'", ch);  
                }
            }

            if (ch == '-') 
                ch = ParseDeclComments();

            if (ch == '+') 
            {
                ch = this.m_current.ReadChar();
                if (ch != '(') 
                {
                    this.m_current.Error("Expecting inclusions name group", ch);  
                }
                inclusions = ParseNameGroup(ch, true);
                ch = this.m_current.SkipWhitespace();
            }

            if (ch == '-') 
                ch = ParseDeclComments();


            if (ch != '>') 
            {
                this.m_current.Error("Expecting end of ELEMENT declaration '>' but found '{0}'", ch); 
            }

            foreach (string name in names) 
            {
                string atom = name.ToUpperInvariant();
                this.m_elements.Add(atom, new ElementDecl(atom, sto, eto, cm, inclusions, exclusions));
            }
        }

        static string ngterm = " \r\n\t|,)";

        string[] ParseNameGroup(char ch, bool nmtokens)
        {
            List<string> names = new List<string>();
            if (ch == '(') 
            {
                ch = this.m_current.ReadChar();
                ch = this.m_current.SkipWhitespace();
                while (ch != ')') 
                {
                    // skip whitespace, scan name (which may be parameter entity reference
                    // which is then expanded to a name)                    
                    ch = this.m_current.SkipWhitespace();
                    if (ch == '%') 
                    {
                        Entity e = ParseParameterEntity(SgmlDtd.ngterm);
                        PushEntity(this.m_current.ResolvedUri, e);
                        ParseNameList(names, nmtokens);
                        PopEntity();
                        ch = this.m_current.Lastchar;
                    }
                    else 
                    {
                        string token = this.m_current.ScanToken(this.m_sb, SgmlDtd.ngterm, nmtokens);
                        token = token.ToUpperInvariant();
                        names.Add(token);
                    }
                    ch = this.m_current.SkipWhitespace();
                    if (ch == '|' || ch == ',') ch = this.m_current.ReadChar();
                }
                this.m_current.ReadChar(); // consume ')'
            } 
            else 
            {
                string name = this.m_current.ScanToken(this.m_sb, SgmlDtd.WhiteSpace, nmtokens);
                name = name.ToUpperInvariant();
                names.Add(name);
            }
            return names.ToArray();
        }

        void ParseNameList(List<string> names, bool nmtokens)
        {
            char ch = this.m_current.Lastchar;
            ch = this.m_current.SkipWhitespace();
            while (ch != Entity.EOF) 
            {
                string name;
                if (ch == '%') 
                {
                    Entity e = ParseParameterEntity(SgmlDtd.ngterm);
                    PushEntity(this.m_current.ResolvedUri, e);
                    ParseNameList(names, nmtokens);
                    PopEntity();
                    ch = this.m_current.Lastchar;
                } 
                else 
                {
                    name = this.m_current.ScanToken(this.m_sb, SgmlDtd.ngterm, true);
                    name = name.ToUpperInvariant();
                    names.Add(name);
                }
                ch = this.m_current.SkipWhitespace();
                if (ch == '|') 
                {
                    ch = this.m_current.ReadChar();
                    ch = this.m_current.SkipWhitespace();
                }
            }
        }

        static string dcterm = " \r\n\t>";

        private ContentModel ParseContentModel(char ch)
        {
            ContentModel cm = new ContentModel();
            if (ch == '(') 
            {
                this.m_current.ReadChar();
                ParseModel(')', cm);
                ch = this.m_current.ReadChar();
                if (ch == '?' || ch == '+' || ch == '*') 
                {
                    cm.AddOccurrence(ch);
                    this.m_current.ReadChar();
                }
            } 
            else if (ch == '%') 
            {
                Entity e = ParseParameterEntity(SgmlDtd.dcterm);
                PushEntity(this.m_current.ResolvedUri, e);
                cm = ParseContentModel(this.m_current.Lastchar);
                cm.Entity = e;

                PopEntity(); // bugbug should be at EOF.
            }
            else
            {
                string dc = ScanName(SgmlDtd.dcterm);
                cm.SetDeclaredContent(dc);
            }
            return cm;
        }

        static string cmterm = " \r\n\t,&|()?+*";

        void ParseModel(char cmt, ContentModel cm)
        {
            // Called when part of the model is made up of the contents of a parameter entity
            int depth = cm.CurrentDepth;
            char ch = this.m_current.Lastchar;
            ch = this.m_current.SkipWhitespace();
            while (ch != cmt || cm.CurrentDepth > depth) // the entity must terminate while inside the content model.
            {
                if (ch == Entity.EOF) 
                {
                    this.m_current.Error("Content Model was not closed");
                }
                if (ch == '%') 
                {
                    Entity e = ParseParameterEntity(SgmlDtd.cmterm);
                    
                    if( cm.Group != null && cm.Group.CurrentEntity == null ) cm.Group.CurrentEntity = e;

                    PushEntity(this.m_current.ResolvedUri, e);
                    ParseModel(Entity.EOF, cm);
                    PopEntity();                    
                    ch = this.m_current.SkipWhitespace();
                } 
                else if (ch == '(') 
                {
                    cm.PushGroup();
                    //if(this.m_current is Entity currentEntity)
                    //{
                    //    cm.Group.CurrentEntity = currentEntity;
                    //}
                    this.m_current.ReadChar();// consume '('
                    ch = this.m_current.SkipWhitespace();
                }
                else if (ch == ')') 
                {
                    ch = this.m_current.ReadChar();// consume ')'
                    if (ch == '*' || ch == '+' || ch == '?') 
                    {
                        cm.AddOccurrence(ch);
                        ch = this.m_current.ReadChar();
                    }
                    if (cm.PopGroup() < depth)
                    {
                        this.m_current.Error("Parameter entity cannot close a paren outside it's own scope");
                    }
                    ch = this.m_current.SkipWhitespace();
                }
                else if (ch == ',' || ch == '|' || ch == '&') 
                {
                    cm.AddConnector(ch);
                    this.m_current.ReadChar(); // skip connector
                    ch = this.m_current.SkipWhitespace();
                }
                else
                {
                    string token;
                    if (ch == '#') 
                    {
                        ch = this.m_current.ReadChar();
                        token = "#" + this.m_current.ScanToken(this.m_sb, SgmlDtd.cmterm, true); // since '#' is not a valid name character.
                    } 
                    else 
                    {
                        token = this.m_current.ScanToken(this.m_sb, SgmlDtd.cmterm, true);
                    }

                    token = token.ToUpperInvariant();
                    ch = this.m_current.Lastchar;
                    if (ch == '?' || ch == '+' || ch == '*') 
                    {
                        cm.PushGroup();
                        cm.AddSymbol(token);
                        cm.AddOccurrence(ch);
                        cm.PopGroup();
                        this.m_current.ReadChar(); // skip connector
                        ch = this.m_current.SkipWhitespace();
                    } 
                    else 
                    {
                        cm.AddSymbol(token);
                        ch = this.m_current.SkipWhitespace();
                    }                   
                }
            }
        }

        #region AttList

        void ParseAttList()
        {
            char ch = this.m_current.SkipWhitespace();
            string[] names = ParseNameGroup(ch, true);          
            Dictionary<string, AttDef> attlist = new Dictionary<string, AttDef>();
            ParseAttList(attlist, '>');
            foreach (string name in names)
            {
                ElementDecl e;
                if (!m_elements.TryGetValue(name, out e)) 
                {
                    this.m_current.Error("ATTLIST references undefined ELEMENT {0}", name);
                }

                e.AddAttDefs(attlist);
            }
        }

        static string peterm = " \t\r\n>";

        void ParseAttList(Dictionary<string, AttDef> list, char term)
        {
            char ch = this.m_current.SkipWhitespace();
            while (ch != term) 
            {
                if (ch == '%') 
                {
                    Entity e = ParseParameterEntity(SgmlDtd.peterm);
                    PushEntity(this.m_current.ResolvedUri, e);
                    ParseAttList(list, Entity.EOF);
                    PopEntity();                    
                    ch = this.m_current.SkipWhitespace();
                } 
                else if (ch == '-') 
                {
                    ch = ParseDeclComments();
                }
                else
                {
                    AttDef a = ParseAttDef(ch);
                    list.Add(a.Name, a);
                }
                ch = this.m_current.SkipWhitespace();
            }
        }

        AttDef ParseAttDef(char ch)
        {
            ch = this.m_current.SkipWhitespace();
            string name = ScanName(SgmlDtd.WhiteSpace);
            name = name.ToUpperInvariant();
            AttDef attdef = new AttDef(name);

            ch = this.m_current.SkipWhitespace();
            if (ch == '-') 
                ch = ParseDeclComments();               

            ParseAttType(ch, attdef);

            ch = this.m_current.SkipWhitespace();
            if (ch == '-') 
                ch = ParseDeclComments();               

            ParseAttDefault(ch, attdef);

            ch = this.m_current.SkipWhitespace();
            if (ch == '-') 
                ch = ParseDeclComments();               

            return attdef;

        }

        void ParseAttType(char ch, AttDef attdef)
        {
            if (ch == '%')
            {
                Entity e = ParseParameterEntity(SgmlDtd.WhiteSpace);
                PushEntity(this.m_current.ResolvedUri, e);
                ParseAttType(this.m_current.Lastchar, attdef);
                PopEntity(); // bugbug - are we at the end of the entity?
                ch = this.m_current.Lastchar;
                return;
            }

            if (ch == '(') 
            {
                //attdef.EnumValues = ParseNameGroup(ch, false);  
                //attdef.Type = AttributeType.ENUMERATION;
                attdef.SetEnumeratedType(ParseNameGroup(ch, false), AttributeType.ENUMERATION);
            } 
            else 
            {
                string token = ScanName(SgmlDtd.WhiteSpace);
                if (string.Equals(token, "NOTATION", StringComparison.OrdinalIgnoreCase)) 
                {
                    ch = this.m_current.SkipWhitespace();
                    if (ch != '(') 
                    {
                        this.m_current.Error("Expecting name group '(', but found '{0}'", ch);
                    }
                    //attdef.Type = AttributeType.NOTATION;
                    //attdef.EnumValues = ParseNameGroup(ch, true);
                    attdef.SetEnumeratedType(ParseNameGroup(ch, true), AttributeType.NOTATION);
                } 
                else 
                {
                    attdef.SetType(token);
                }
            }
        }

        void ParseAttDefault(char ch, AttDef attdef)
        {
            if (ch == '%')
            {
                Entity e = ParseParameterEntity(SgmlDtd.WhiteSpace);
                PushEntity(this.m_current.ResolvedUri, e);
                ParseAttDefault(this.m_current.Lastchar, attdef);
                PopEntity(); // bugbug - are we at the end of the entity?
                ch = this.m_current.Lastchar;
                return;
            }

            bool hasdef = true;
            if (ch == '#') 
            {
                this.m_current.ReadChar();
                string token = this.m_current.ScanToken(this.m_sb, SgmlDtd.WhiteSpace, true);
                hasdef = attdef.SetPresence(token);
                ch = this.m_current.SkipWhitespace();
            } 
            if (hasdef) 
            {
                if (ch == '\'' || ch == '"') 
                {
                    string lit = this.m_current.ScanLiteral(this.m_sb, ch);
                    attdef.Default = lit;
                    ch = this.m_current.SkipWhitespace();
                }
                else
                {
                    string name = this.m_current.ScanToken(this.m_sb, SgmlDtd.WhiteSpace, false);
                    name = name.ToUpperInvariant();
                    attdef.Default = name; // bugbug - must be one of the enumerated names.
                    ch = this.m_current.SkipWhitespace();
                }
            }
        }

        #endregion
    }
}
