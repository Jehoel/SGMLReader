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
using System.Xml;
using System.IO;
using System.Net;
using System.Text;
using System.Collections;

using Sgml;
using System.Globalization;
using System.Collections.Generic;

using r = System.Text.RegularExpressions;

namespace SgmlClasses
{
	public class Program
	{
		public static void Main(String[] args)
		{
			if( args.Length != 2 )
			{
				Console.WriteLine( "Usage SgmlClasses.exe <dtdFileName> <outputFileName> [<access> = 'public']" );
				return;
			}

			String dtdFileName = args[0];
			String outputFileName = args[1];

			if( !File.Exists( dtdFileName ) )
			{
				Console.WriteLine( "File \"{0}\" does not exist.", dtdFileName );
				return;
			}

			Run( dtdFileName, outputFileName );
		}

		private static void Run(String dtdFileName, String outputFileName)
		{
			if( !Path.IsPathRooted( dtdFileName ) ) dtdFileName = Path.GetFullPath( dtdFileName );
			if( !Path.IsPathRooted( outputFileName ) ) outputFileName = Path.GetFullPath( outputFileName );

			SgmlDtd dtd;
			Uri dtdUri = new Uri( "file://" + dtdFileName );
			using( StreamReader rdr = new StreamReader( dtdFileName ) )
			{
				dtd = SgmlDtd.Parse( dtdUri, null, rdr, null, null );
			}

			////////////////////////

			using( StreamWriter w = new StreamWriter( outputFileName, append: false, encoding: Encoding.UTF8 ) )
			{
				w.WriteLine( "using System;" );
				w.WriteLine();
				w.WriteLine( "namespace MyNamespace" );
				w.WriteLine( "{" );
				w.WriteLine();

                Dictionary<String,Group> groupsAsClasses = new Dictionary<String,Group>();

				foreach( ElementDecl el in dtd.Elements.Values )
				{
					w.WriteLine( "\tpublic class {0}", el.GetCSharpName() );
					w.WriteLine( "\t{" );

					w.WriteLine();

					if( el.Attributes != null )
					{
						foreach( AttDef attrib in el.Attributes.Values )
						{
							String csharpType = GetCSharpType( attrib.Type );

							w.WriteLine( "\t\tpublic {0} {1} {{ get; set; }}", csharpType, attrib.GetCSharpName() );
						}
					}

					if( el.ContentModel != null )
					{
						switch( el.ContentModel.DeclaredContent )
						{
						case DeclaredContent.Default:

							RenderGroup( el.ContentModel.Group, w );
							break;

						case DeclaredContent.EMPTY:
							break;
						case DeclaredContent.CDATA:
							w.WriteLine();
							w.WriteLine( "\t\tpublic String CDATA { get; set; }" );
							break;
						case DeclaredContent.RCDATA:
							w.WriteLine();
							w.WriteLine( "\t\tpublic String RCDATA { get; set; }" );
							break;
						}
					}

					w.WriteLine();

					w.WriteLine( "\t}" );
				}

				w.WriteLine( "}" );
				w.WriteLine();
			}
		}

		
		private static readonly Regex _metadataElementRegex         = new Regex( @"<!--#ELEMENT\s+(\S+)\s+(.+?(?=-->))"   , RegexOptions.Singleline | RegexOptions.Compiled );

		

		private static List<> GetMetadata()
        {

        }

		private static void RenderGroup(Group g, StreamWriter w)
		{
			if( g == null ) return;

			w.WriteLine( "\t\t// Begin Group. {0} members. Occurrence: {1}. TextOnly: {2}.", g.Members.Count, g.Occurrence, g.TextOnly );

			// Handle 0:1 children for now:
			if( g.Occurrence == Occurrence.Optional )
			{
				w.WriteLine( "\t\t// Optional" );
				foreach( GroupMember m in g.Members )
				{
					if( m.Symbol != null )
					{
						w.WriteLine( "\t\tpublic {0} {0} {{ get; set; }}", m.GetCSharpSymbol() );
					}
					else if( m.Group != null )
					{
						RenderGroup( m.Group, w );
					}
				}
				w.WriteLine();
			}
			else if( g.Occurrence == Occurrence.Required )
			{
				w.WriteLine( "\t\t// Required" );
				foreach( GroupMember m in g.Members )
				{
					if( m.Symbol != null )
					{
						w.WriteLine( "\t\tpublic {0} {0} {{ get; set; }}", m.GetCSharpSymbol() );
					}
					else if( m.Group != null )
					{
						RenderGroup( m.Group, w );
					}
				}
				w.WriteLine();
			}
			else if( g.Occurrence == Occurrence.OneOrMore )
			{
				// TODO: Define a class for this grouping, then put it in a List<TGroup>
				w.WriteLine( "\t\t// OneOrMore - TODO" );
			}
			else if( g.Occurrence == Occurrence.ZeroOrMore )
			{
				// TODO: Define a class for this grouping, then put it in a List<TGroup>
				w.WriteLine( "\t\t// ZeroOrMore - TODO" );
			}

			w.WriteLine( "\t\t// End Group." );
		}

		private static String GetCSharpType(AttributeType at)
		{
			switch( at )
			{
			case AttributeType.Default:
				return "String";
			case AttributeType.CDATA:
				return "String";
			case AttributeType.ENTITY:
				return "Object";  // TODO: Strongly-typed
			case AttributeType.ENTITIES:
				return "List<Object>";  // TODO: Strongly-typed
			case AttributeType.ID:
				return "String";
			case AttributeType.IDREF:
				return "String";
			case AttributeType.NAME:
				return "String";
			case AttributeType.NAMES:
				return "List<String>";
			case AttributeType.NMTOKEN:
				return "String";
			case AttributeType.NMTOKENS:
				return "List<String>";
			case AttributeType.NUMBER:
				return "Int32";
			case AttributeType.NUMBERS:
				return "List<Int32>";
			case AttributeType.NUTOKEN:
				return "String";
			case AttributeType.NUTOKENS:
				return "List<String>";
			case AttributeType.NOTATION:
				return "List<String>";
			case AttributeType.ENUMERATION:
				return "List<String>"; // TODO: Strongly-typed
			default:
				throw new ArgumentOutOfRangeException( nameof( at ), at, "Unrecognized." );
			}
		}
	}

	internal static class Extensions
	{
		public static String GetCSharpName(this AttDef attDef)
		{
			return attDef.Name.Replace(".", "");
		}

		public static String GetCSharpName(this ElementDecl elementDecl)
		{
			return elementDecl.Name.Replace(".", "");
		}

		public static String GetCSharpSymbol(this GroupMember groupMember)
		{
			return groupMember.Symbol.Replace(".", "");
		}
	}

	public class ParameterEntityMetadata
    {
		public static Regex Regex => _metadataRegex;

		private static readonly Regex _metadataRegex           = new Regex( @"<!--#ENTITY\s+%\s+(\w+)\s+(.+?(?=-->))", RegexOptions.Compiled | RegexOptions.Singleline ); // [1] = Name. [2] = Type expression.
		private static readonly Regex _dataTypeWithLengthRegex = new Regex( @"#DataType\(([AINR])\-(\d+)\)"          , RegexOptions.Compiled | RegexOptions.IgnoreCase ); // [1] = Type name. [2] = Length
		private static readonly Regex _dataTypeElseRegex       = new Regex( @"#DataType\((.+?)\)"        , RegexOptions.Compiled | RegexOptions.IgnoreCase ); // [1] = Type name
		private static readonly Regex _enumTypeRegex           = new Regex( @"#Enum\((?:""\w+""(?:,\s*)?)+\)"        , RegexOptions.Compiled | RegexOptions.IgnoreCase ); // [1] = Enum values (repeating group)

		public static ParameterEntityMetadata Create(Match match)
        {
			String nameStr = match.Groups[1].Value;
			String typeStr = match.Groups[2].Value;

			Match dataTypeWithLengthMatch = _dataTypeWithLengthRegex.Match( typeStr );
			if( dataTypeWithLengthMatch.Success )
            {
				String typeName  = dataTypeWithLengthMatch.Groups[1].Value.ToUpperInvariant();
				String lengthStr = dataTypeWithLengthMatch.Groups[2].Value;
				Int32  length    = Int32.Parse( lengthStr, NumberStyles.Integer, CultureInfo.InvariantCulture );

				ParameterEnumDataType type = ParameterEnumDataType.None;
				switch( typeName )
                {
				case "A":
					type = ParameterEnumDataType.UnicodeText;
					if( length == 1 ) type = ParameterEnumDataType.Char;
					break;
				case "I":
					type = ParameterEnumDataType.Integer;
					break;
				case "N":
					type = ParameterEnumDataType.Numeric;
					break;
				case "R":
					type = ParameterEnumDataType.Html;
					break;
				default:
					throw new FormatException( "Unrecognized data type with length: " + typeStr );
                }

				return new ParameterEntityMetadata( nameStr, type, length, null );
            }

			Match dataTypeElseMatch = _dataTypeElseRegex.Match( typeStr );
			if( dataTypeElseMatch.Success )
            {
				String typeName  = dataTypeWithLengthMatch.Groups[1].Value.ToUpperInvariant();

				ParameterEnumDataType type = ParameterEnumDataType.None;
				switch( typeName )
                {
				case "BOOL":
					type = ParameterEnumDataType.Bool;
					break;
				case "DATE":
					type = ParameterEnumDataType.Date;
					break;
				case "TIME":
					type = ParameterEnumDataType.Time;
					break;
				case "A0-0":
					type = ParameterEnumDataType.Empty;
					break;
				default:
					throw new FormatException( "Unrecognized data type: " + typeStr );
                }

				return new ParameterEntityMetadata( nameStr, type, null, null );
            }

			Match enumMatch = _enumTypeRegex.Match( typeStr );
			if( enumMatch.Success )
            {
				CaptureCollection captures = enumMatch.Groups[1].Captures;
				String[] values = new String[ captures.Count ];
				for( Int32 i = 0; i < values.Length; i++ ) values[i] = captures[i].Value;

				return new ParameterEntityMetadata( nameStr, ParameterEnumDataType.Enum, null, values );
            }

			throw new FormatException( "Unrecognized ENTITY metadata: " + typeStr );
        }

        private ParameterEntityMetadata(String name, ParameterEnumDataType dataType, Int32? length, IReadOnlyList<String> enumValues)
        {
            this.Name = name;
            this.DataType = dataType;
            this.Length = length;
            this.EnumValues = enumValues;
        }

        public String Name { get; }
		
		public ParameterEnumDataType DataType { get; }

		public Int32? Length { get; }

		public IReadOnlyList<String> EnumValues { get; }

		private static ParameterEnumDataType ParseTypeName(String name)
        {
			// I think these types come from MIFST, the ofx160.dtd file says "Profile data types brought from MIFST 1.0".
			// "Microsoft Internet Finance Server Toolkit", which includes an OFX library:
			// https://news.microsoft.com/1998/12/03/microsoft-internet-finance-server-toolkit-licensed-to-software-companies/
			// http://www.ucd.ie/sys-mgt/y2k/tech/ms/user_allproductsvol14.htm
			// 

			name = name.ToUpperInvariant();
			switch( name )
            {
			case "A": return ParameterEnumDataType.UnicodeText;
			case "I": return ParameterEnumDataType.Integer;
			case "N": return ParameterEnumDataType.Numeric;
			case "R": return ParameterEnumDataType.Html;
			case "BOOL": return ParameterEnumDataType.Bool;
			case "DATE": return ParameterEnumDataType.
            }
        }
    }

	public enum ParameterEnumDataType
    {
		None,
		Empty, // "A0-0" ('0' appears twice)

		// No length specified:
		Date, // DATE
		Time, // TIME
		Bool, // BOOL - "'Y' = yes or true, 'N' = no or false

		// Specifies length:


		Char, // Single character, "CHARTYPE" aka "A-1".
		UnicodeText, // "A-(n)" - "Character fields are identified with a data type of “A-n”, where n is the maximum number of allowed Unicode characters."
		Integer, // "I-(n)" - "INTTYPE" and others, This is mentioned in the PDF, but the ofx160.dtd explicitly lists it for INTTYPE as "Integer", presumably "n" is digits, not bytes because there are non-power-of-2 values like "I-3" and "I-6".
		
		Enum, // #ENUM 
		Numeric, // N-(n), "N-n identifies an element of numeric type where n is the maximum number of characters in the value. Values of this type are generally whole numbers, but the data type allows negative numbers."
		// Except in ofx160.dtd, AMTTYPE is described as N-32, but with the comment "Current Amount: Used for specifying an amount. may be signed; comma or period for decimal point"
		// And in my Chase QFX file, the values are indeed signed 2-digit decimal numbers. e.g. `<TRNAMT>-1130.00`
		// So I guess I'll use `System.Decimal` for this type.

		Html // R-(n) - Only seen for MSGBODY `R-10000` which is described as "HTML-encoded text - A-1000 or Plain text - A-2000".
    }
}
