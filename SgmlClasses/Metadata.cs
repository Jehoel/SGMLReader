using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace SgmlClasses
{
	public class ParameterEntityMetadata
	{
		// <!--#ENTITY % PHONETYPE          #DataType(A-32)-->
		// <!--#ENTITY % CALLTYPEENUM       #Enum("CALL", "PUT","PREFUND","MATURITY")-->

		public static Regex Regex => _metadataRegex;

		private static readonly Regex _metadataRegex           = new Regex( @"<!--#ENTITY\s+%\s+(\w+)\s+(.+?(?=-->))", RegexOptions.Compiled | RegexOptions.Singleline ); // [1] = Name. [2] = Type expression.
		private static readonly Regex _dataTypeWithLengthRegex = new Regex( @"#DataType\(([AINR])\-(\d+)\)"          , RegexOptions.Compiled | RegexOptions.IgnoreCase ); // [1] = Type name. [2] = Length
		private static readonly Regex _dataTypeElseRegex       = new Regex( @"#DataType\((.+?)\)"                    , RegexOptions.Compiled | RegexOptions.IgnoreCase ); // [1] = Type name
		private static readonly Regex _enumTypeRegex           = new Regex( @"#Enum\((?:""(\w+)""(?:,\s*)?)+\)"      , RegexOptions.Compiled | RegexOptions.IgnoreCase ); // [1] = Enum values (repeating group)

		public static ParameterEntityMetadata Create(Match match)
		{
			String nameStr = match.Groups[1].Value;
			String typeStr = match.Groups[2].Value;

			{
				Match dataTypeWithLengthMatch = _dataTypeWithLengthRegex.Match( typeStr );
				if( dataTypeWithLengthMatch.Success )
				{
					String typeName  = dataTypeWithLengthMatch.Groups[1].Value.ToUpperInvariant();
					String lengthStr = dataTypeWithLengthMatch.Groups[2].Value;
					Int32  length	= Int32.Parse( lengthStr, NumberStyles.Integer, CultureInfo.InvariantCulture );

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
			}

			{
				Match dataTypeElseMatch = _dataTypeElseRegex.Match( typeStr );
				if( dataTypeElseMatch.Success )
				{
					String typeName  = dataTypeElseMatch.Groups[1].Value.ToUpperInvariant();

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
			}

			{
				Match enumMatch = _enumTypeRegex.Match( typeStr );
				if( enumMatch.Success )
				{
					CaptureCollection captures = enumMatch.Groups[1].Captures;
					String[] values = new String[ captures.Count ];
					for( Int32 i = 0; i < values.Length; i++ ) values[i] = captures[i].Value;

					return new ParameterEntityMetadata( nameStr, ParameterEnumDataType.Enum, null, values );
				}
			}

			throw new FormatException( "Unrecognized ENTITY metadata: " + typeStr );
		}

		private ParameterEntityMetadata(String name, ParameterEnumDataType dataType, Int32? length, IReadOnlyList<String> enumValues)
		{
			this.Name = name;
			this.DataType = dataType;
			this.Length = length;
			this.EnumValues = enumValues ?? new String[0];
		}

		public String Name { get; }
		
		public ParameterEnumDataType DataType { get; }

		public Int32? Length { get; }

		public IReadOnlyList<String> EnumValues { get; }
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

	public class ElementMetadata
	{
		// <!--#ELEMENT PROFRS #Link(URLGETREDIRECT,PROFMSGSRSV2)-->
		// <!--#ELEMENT INVTRAN #Link(SRVRTID,(INVSTMTMSGSRQV1|INVSTMTMSGSRSV1))-->
		// <!--#ELEMENT STATUS #Link(MESSAGE2, (SIGNONMSGSRSV2 |  SIGNUPMSGSRSV2 | BANKMSGSRSV2 | CREDITCARDMSGSRSV2 | INVSTMTMSGSRSV2 | INTERXFERMSGSRSV2 | WIREXFERMSGSRSV2 | BILLPAYMSGSRSV2 | EMAILMSGSRSV2 | SECLISTMSGSRSV2 | PROFMSGSRSV2 | PRESDIRMSGSRSV1 | PRESDLVMSGSRSV1))-->

		public static Regex Regex => _metadataRegex;

		private static readonly Regex _metadataRegex = new Regex( @"<!--#ELEMENT\s+(\w+)\s+(.+?(?=-->))"            , RegexOptions.Compiled | RegexOptions.Singleline ); // [1] = Name. [2] = Metadata.
		private static readonly Regex _linkOneRegex  = new Regex( @"#Link\(\s*(\w+)\s*,\s*(\w+)\s*\)"               , RegexOptions.Compiled | RegexOptions.IgnoreCase ); // [1] = First element, [2] = Other elements
		private static readonly Regex _linkManyRegex = new Regex( @"#Link\(\s*(\w+)\s*,\s*\((?:(\w+)(?:\s*\|\s*)?)+", RegexOptions.Compiled | RegexOptions.IgnoreCase ); // [1] = First element, [2] = Other elements

		public static ElementMetadata Create(Match match)
		{
			String nameStr = match.Groups[1].Value;
			String metaStr = match.Groups[2].Value;

			{
				Match linkOneMatch = _linkOneRegex.Match( metaStr );
				if( linkOneMatch.Success )
				{
					String from = linkOneMatch.Groups[1].Value;
					String to   = linkOneMatch.Groups[2].Value;

					return new ElementMetadata( from, new String[] { to } );
				}
			}

			{
				Match linkManyMatch = _linkManyRegex.Match( metaStr );
				if( linkManyMatch.Success )
				{
					String from = linkManyMatch.Groups[1].Value;

					CaptureCollection captures = linkManyMatch.Groups[2].Captures;
					String[] toValues = new String[ captures.Count ];
					for( Int32 i = 0; i < toValues.Length; i++ ) toValues[i] = captures[i].Value;

					return new ElementMetadata( from, toValues );
				}
			}

			throw new FormatException( "Unrecognized ELEMENT metadata: " + metaStr );
		}

		private ElementMetadata(String fromElement, IReadOnlyList<String> toElements)
		{
			this.FromElement = fromElement;
			this.ToElements = toElements;
		}

		public String FromElement { get; }
		public IReadOnlyList<String> ToElements { get; }
	}

	internal static partial class Extensions
	{
		public static IEnumerable<Match> GetAllMatches(this Regex regex, String input)
		{
			return regex
				.Matches( input )
				.Cast<Match>();
		}
	}
}
