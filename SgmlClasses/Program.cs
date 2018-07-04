using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Sgml;

namespace SgmlClasses
{
	// TODO: Modify SgmlReader's DtdReader to include entity information in ElementDecl or its ContentModel.
	// e.g.  after reading `<!ELEMENT DTUSER	- o %DTTMTYPE>` from the DTD, the internal object model doesn't mention `DTTMTYPE` anywhere...

	public class RenderContext
	{
		public RenderContext(SgmlDtd dtd, Dictionary<String, ParameterEntityMetadata> parameterEntityMetadatas)
		{
			this.Dtd = dtd;
			this.ParameterEntityMetadatas = parameterEntityMetadatas;
		}

		public SgmlDtd Dtd { get; }
		public Dictionary<String,ParameterEntityMetadata> ParameterEntityMetadatas { get; }
	}

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

			// Read the file again into a string to use metadata regex:
			Dictionary<String,ParameterEntityMetadata> parameterEntityMetadatas;
			ILookup<String,ElementMetadata>            elementMetadatas;
			{
				String dtdFileContents = File.ReadAllText( dtdFileName );

				parameterEntityMetadatas = ParameterEntityMetadata.Regex
					.GetAllMatches( dtdFileContents )
					.Select( match => ParameterEntityMetadata.Create( match ) )
					.ToDictionary( md => md.Name );

				// I don't know what the `#ELEMENT name #Link` metadata means... none of the relationships make sense...
				elementMetadatas = ElementMetadata.Regex
					.GetAllMatches( dtdFileContents )
					.Select( match => ElementMetadata.Create( match ) )
					.ToLookup( md => md.FromElement );
			}

			////////////////////////

			RenderContext ctx = new RenderContext( dtd, parameterEntityMetadatas );

			using( StreamWriter w = new StreamWriter( outputFileName, append: false, encoding: Encoding.UTF8 ) )
			{
				w.WriteLine( "using System;" );
				w.WriteLine( "using System.Collections.Generic;" );
				w.WriteLine( "using System.ComponentModel.DataAnnotations;" );
				w.WriteLine();
				w.WriteLine( "namespace MyNamespace" );
				w.WriteLine( "{" );
				w.WriteLine();

				Dictionary<String,Group> groupsAsClasses = new Dictionary<String,Group>();

				// Render all enum types:
				IEnumerable<ParameterEntityMetadata> pems = parameterEntityMetadatas
					.Values
					.Where( pe => pe.DataType == ParameterEnumDataType.Enum )
					.OrderBy( pe => pe.Name );

				w.WriteLine( "#region Enums" );
				w.WriteLine();

				foreach( ParameterEntityMetadata pe in pems )
				{
					String typeName = pe.Name + "Values";

					w.WriteLine( "\tpublic enum {0}", typeName );
					w.WriteLine( "\t{" );

					if( pe.EnumValues.Max( v => v.Length ) < 5 && pe.EnumValues.Count > 10 )
					{
						w.Write( "\t\t" );
						for( Int32 i = 0; i < pe.EnumValues.Count; i++ )
						{
							if( i > 0 && ( i % 10 == 0 ) )
							{
								w.Write( "\r\n\t\t" );
							}
							
							String value = pe.EnumValues[i];

							if( !Char.IsLetter( value[0] ) )
							{
								value = "_" + value;
							}

							w.Write( value );
							w.Write( ", " );
						}
						w.WriteLine();
					}
					else
					{
						foreach( String name in pe.EnumValues )
						{
							String value = name;
							if( !Char.IsLetter( value[0] ) )
							{
								value = "_" + value;
							}

							w.WriteLine( "\t\t{0},", value );
						}
					}

					w.WriteLine( "\t}" );
					w.WriteLine();
				}

				w.WriteLine( "#endregion" );

				w.WriteLine();
				w.WriteLine( @"
	public abstract class Element
	{
		protected Element(String name)
		{
			this.Name = name;
		}

		public String Name { get; }
	}
" );

				foreach( ElementDecl el in dtd.Elements.Values )
				{
					// if an element has no members and is text-only, then it doesn't need to be its own class.
					if( !ShouldRenderAsClass( el ) ) continue;

					RenderElementClass( ctx, el, w );
				}

				w.WriteLine( "}" );
				w.WriteLine();
			}
		}

		private static Boolean ShouldRenderAsClass( ElementDecl el )
		{
			if( el.Attributes?.Count > 0 ) return true;

			if( el.ContentModel.DeclaredContent != DeclaredContent.Default ) return true;
			// TODO: Should CDATA/RCDATA elements be rendered or just be string properties in their parents? I don't have any examples in the ofx160.dtd file, unfortunately.

			Group g = el.ContentModel.Group;

			if( g.Members.Count > 0 ) return true; // TODO: Could simplify nested single children to a single property.

			switch( g.Occurrence )
			{
			case Occurrence.ZeroOrMore:
			case Occurrence.OneOrMore:
				return true;
			case Occurrence.Optional:
			case Occurrence.Required:
				break; // TODO: If the group is a single value then it could be an inline array.
			default:
				throw new InvalidOperationException( "Unknown occurrence: " + g.Occurrence );
			}

			if( g.TextOnly ) return false;

			return true; // default fallback, let's see what happens!
		}

		private static void RenderElementClass( RenderContext ctx, ElementDecl el, StreamWriter w )
		{
			w.WriteLine( "\tpublic class {0} : Element", el.GetCSharpName() );
			w.WriteLine( "\t{" );
			w.WriteLine( "\t\tinternal const String NameStr = \"{0}\";", el.Name );
			w.WriteLine( "\t\tpublic static String Name => NameStr;" );
			w.WriteLine( "\t\tpublic {0}() : base( NameStr ) {{}}", el.GetCSharpName() );
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

					RenderGroup( ctx, el.ContentModel.Group, w, depth: 0 );
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
			w.WriteLine();
		}

		#region RenderGroup

		private static void RenderGroup( RenderContext ctx, Group g, StreamWriter w, Int32 depth )
		{
			if( g == null ) return;

			w.WriteLine( "\t\t// Begin Group. {0} members. Occurrence: {1}. Type: {2}. TextOnly: {3}.".PrefixTabs( depth ), g.Members.Count, g.Occurrence, g.GroupType, g.TextOnly );

			switch( g.Occurrence )
			{
			case Occurrence.Required:

				switch( g.GroupType )
				{
				case GroupType.None:
					RenderGroup_Required_None( ctx, g, w, depth ); break;
				case GroupType.And:
					RenderGroup_Required_And( ctx, g, w, depth ); break;
				case GroupType.Or:
					RenderGroup_Required_Or( ctx, g, w, depth ); break;
				case GroupType.Sequence:
					RenderGroup_Required_Sequence( ctx, g, w, depth ); break;
				}

				break;
			case Occurrence.Optional:

				switch( g.GroupType )
				{
				case GroupType.None:
					RenderGroup_Optional_None( ctx, g, w, depth ); break;
				case GroupType.And:
					RenderGroup_Optional_And( ctx, g, w, depth ); break;
				case GroupType.Or:
					RenderGroup_Optional_Or( ctx, g, w, depth ); break;
				case GroupType.Sequence:
					RenderGroup_Optional_Sequence( ctx, g, w, depth ); break;
				}

				break;
			case Occurrence.ZeroOrMore:

				switch( g.GroupType )
				{
				case GroupType.None:
					RenderGroup_ZeroOrMore_None( ctx, g, w, depth ); break;
				case GroupType.And:
					RenderGroup_ZeroOrMore_And( ctx, g, w, depth ); break;
				case GroupType.Or:
					RenderGroup_ZeroOrMore_Or( ctx, g, w, depth ); break;
				case GroupType.Sequence:
					RenderGroup_ZeroOrMore_Sequence( ctx, g, w, depth ); break;
				}

				break;
			case Occurrence.OneOrMore:

				switch( g.GroupType )
				{
				case GroupType.None:
					RenderGroup_OneOrMore_None( ctx, g, w, depth ); break;
				case GroupType.And:
					RenderGroup_OneOrMore_And( ctx, g, w, depth ); break;
				case GroupType.Or:
					RenderGroup_OneOrMore_Or( ctx, g, w, depth ); break;
				case GroupType.Sequence:
					RenderGroup_OneOrMore_Sequence( ctx, g, w, depth ); break;
				}

				break;
			}

			/*
			// Handle 0:1 children for now:
			if( g.OccurrenceIsOnce )
			{
				if	 ( g.Occurrence == Occurrence.Optional ) w.WriteLine( "\t\t// Optional" );
				else if( g.Occurrence == Occurrence.Required ) w.WriteLine( "\t\t// Required" );

				foreach( GroupMember m in g.Members )
				{
					if( m.Symbol != null )
					{
						ElementDecl el = dtd.Elements[ m.Symbol ];

						if( !ShouldRenderAsClass( el ) )
						{
							// If the element's SGML type does not have a class in this project then it's a scalar value:
							if( el.ContentModel.Entity != null )
							{
								ParameterEntityMetadata pe = parameterEntityMetadatas[ el.ContentModel.Entity.Name ];
								
								RenderParameterEntityProperty( w, m, pe );
							}
							else
							{
								// Fallback to a string:
								w.WriteLine( "\t\tpublic String {0} {{ get; set; }}", m.GetCSharpSymbol() );
							}
						}
						else
						{
							w.WriteLine( "\t\tpublic {0} {0} {{ get; set; }}", m.GetCSharpSymbol() );
						}
					}
					else if( m.Group != null )
					{
						RenderGroup( dtd, parameterEntityMetadatas, m.Group, w );
					}
				}
				w.WriteLine();
			}
			else if( g.Occurrence == Occurrence.OneOrMore || g.Occurrence == Occurrence.ZeroOrMore )
			{
				if	 ( g.Occurrence == Occurrence.OneOrMore ) w.WriteLine( "\t\t// OneOrMore" );
				else if( g.Occurrence == Occurrence.ZeroOrMore ) w.WriteLine( "\t\t// ZeroOrMore" );

				RenderRepeatingGroup( dtd, parameterEntityMetadatas, g, w );
			}
			*/
			w.WriteLine( "\t\t// End Group.".PrefixTabs( depth ) );
		}

		private static void RenderGroup_Required_None( RenderContext ctx, Group g, StreamWriter w, Int32 depth )
		{
			w.WriteLine( "\t\t// RenderGroup_Required_None".PrefixTabs( depth ) );
			RenderGroup_Required_NoneAndSequence( ctx, g, w, depth );
		}

		private static void RenderGroup_Required_And( RenderContext ctx, Group g, StreamWriter w, Int32 depth )
		{
			w.WriteLine( "\t\t// RenderGroup_Required_And".PrefixTabs( depth ) );
			RenderGroup_Required_NoneAndSequence( ctx, g, w, depth );
		}

		private static void RenderGroup_Required_Or( RenderContext ctx, Group g, StreamWriter w, Int32 depth )
		{
			w.WriteLine( "\t\t// RenderGroup_Required_Or".PrefixTabs( depth ) );

			// A single property for any single element in the membership list.
			// Quick approach: the property is typed `Element`
			// Best approach: the possible element types (C# classes) are annotated with a new interface that the property is typed as.
			// Another approach: a new class is defined for the group that has strongly-typed properties, but only 1 such property is populated.

			// Problem: the group's options are not necessarily elements, but could be entities or other groups (aaaah).

			// Handle the easier case where all members are elements (a subset of the case where all children are symbols)
			Boolean allChildrenAreElementClasses = g.Members.All( m => m.Group == null && ShouldRenderAsClass( ctx.Dtd.Elements[m.Symbol] ) );
			if( allChildrenAreElementClasses )
			{
				String propertyName = String.Join( "_or_", g.Members.Select( m => m.GetCSharpSymbol() ) );
				String comment      = String.Join( " | ", g.Members.Select( m => m.GetCSharpSymbol() ) );

				w.WriteLine( "\t\tpublic Element {0} {{ get; set; }} // {1}", propertyName, comment );
			}
			else
			{
				Boolean allChildrenAreValues = g.Members.All( m => m.Group == null && !ShouldRenderAsClass( ctx.Dtd.Elements[m.Symbol] ) );
				if( allChildrenAreValues )
				{
					String propertyName = String.Join( "_or_", g.Members.Select( m => m.GetCSharpSymbol() ) );
					String comment      = String.Join( " | ", g.Members.Select( m => m.GetCSharpSymbol() ) );

					w.WriteLine( "\t\tpublic String {0} {{ get; set; }} // {1}", propertyName, comment );
				}
				else
				{
					w.WriteLine( "\t\t// RenderGroup_Required_Or - TODO (Non-element children)".PrefixTabs( depth ) );
				}
			}
		}

		private static void RenderGroup_Required_Sequence( RenderContext ctx, Group g, StreamWriter w, Int32 depth )
		{
			w.WriteLine( "\t\t// RenderGroup_Required_Sequence".PrefixTabs( depth ) );
			RenderGroup_Required_NoneAndSequence( ctx, g, w, depth );
		}

		private static void RenderGroup_Required_NoneAndSequence( RenderContext ctx, Group g, StreamWriter w, Int32 depth )
		{
			foreach( GroupMember member in g.Members )
			{
				if( member.Group != null )
				{
					RenderGroup( ctx, member.Group, w, depth + 1 );
				}
				else if( member.Symbol != null )
				{
					RenderGroupMemberSymbolAsScalarProperty( ctx, member, w, depth );
				}
			}
		}



		private static void RenderGroup_Optional_None( RenderContext ctx, Group g, StreamWriter w, Int32 depth )
		{
			if( g.Members.Count == 1 )
			{
				w.WriteLine( "\t\t// RenderGroup_Optional_None_Single".PrefixTabs( depth ) );

				GroupMember member = g.Members[0];
				if( member.Group != null )
				{
					RenderGroup( ctx, member.Group, w, depth + 1 );
				}
				else if( member.Symbol != null )
				{
					RenderGroupMemberSymbolAsScalarProperty( ctx, member, w, depth );
				}
			}
			else
			{
				w.WriteLine( "\t\t// RenderGroup_Optional_None_Multiple - TODO".PrefixTabs( depth ) );
			}
		}

		private static void RenderGroup_Optional_And( RenderContext ctx, Group g, StreamWriter w, Int32 depth )
		{
			w.WriteLine( "\t\t// RenderGroup_Optional_And - TODO".PrefixTabs( depth ) );
		}

		private static void RenderGroup_Optional_Or( RenderContext ctx, Group g, StreamWriter w, Int32 depth )
		{
			w.WriteLine( "\t\t// RenderGroup_Optional_Or - TODO".PrefixTabs( depth ) );
		}

		private static void RenderGroup_Optional_Sequence( RenderContext ctx, Group g, StreamWriter w, Int32 depth )
		{
			w.WriteLine( "\t\t// RenderGroup_Optional_Sequence - TODO".PrefixTabs( depth ) );
		}

		

		private static void RenderGroup_ZeroOrMore_None( RenderContext ctx, Group g, StreamWriter w, Int32 depth )
		{
			w.WriteLine( "\t\t// RenderGroup_ZeroOrMore_None - TODO".PrefixTabs( depth ) );
		}

		private static void RenderGroup_ZeroOrMore_And( RenderContext ctx, Group g, StreamWriter w, Int32 depth )
		{
			w.WriteLine( "\t\t// RenderGroup_ZeroOrMore_And - TODO".PrefixTabs( depth ) );
		}

		private static void RenderGroup_ZeroOrMore_Or( RenderContext ctx, Group g, StreamWriter w, Int32 depth )
		{
			w.WriteLine( "\t\t// RenderGroup_ZeroOrMore_Or - TODO".PrefixTabs( depth ) );
		}

		private static void RenderGroup_ZeroOrMore_Sequence( RenderContext ctx, Group g, StreamWriter w, Int32 depth )
		{
			w.WriteLine( "\t\t// RenderGroup_ZeroOrMore_Sequence - TODO".PrefixTabs( depth ) );
		}



		private static void RenderGroup_OneOrMore_None( RenderContext ctx, Group g, StreamWriter w, Int32 depth )
		{
			w.WriteLine( "\t\t// RenderGroup_OneOrMore_None - TODO".PrefixTabs( depth ) );
		}

		private static void RenderGroup_OneOrMore_And( RenderContext ctx, Group g, StreamWriter w, Int32 depth )
		{
			w.WriteLine( "\t\t// RenderGroup_OneOrMore_And - TODO".PrefixTabs( depth ) );
		}

		private static void RenderGroup_OneOrMore_Or( RenderContext ctx, Group g, StreamWriter w, Int32 depth )
		{
			w.WriteLine( "\t\t// RenderGroup_OneOrMore_Or".PrefixTabs( depth ) );

			Boolean noGroupChildren = g.Members.All( m => m.Group == null );
			if( noGroupChildren )
			{
				RenderOrGroupClassAndListProperty( ctx, g, w, depth );
			}
			else
			{
				w.WriteLine( "\t\t// RenderGroup_OneOrMore_Or - TODO (Non-element children)".PrefixTabs( depth ) );
			}

			w.WriteLine();
		}

		private static void RenderOrGroupClassAndListProperty( RenderContext ctx, Group g, StreamWriter w, Int32 depth )
		{
			Boolean noGroupChildren = g.Members.All( m => m.Group == null );
			if( !noGroupChildren ) throw new ArgumentException( "Group must not have any group members." );

			String propertyName  = String.Join( "_or_", g.Members.Select( m => m.GetCSharpSymbol() ) );
			String comment       = String.Join( " | ", g.Members.Select( m => m.GetCSharpSymbol() ) );
			String groupTypeName = propertyName + "Group";

			///////////////////////

			w.WriteLine( "\t\tpublic class {0}".PrefixTabs( depth ), groupTypeName );
			w.WriteLine( "\t\t{".PrefixTabs( depth ) );

			foreach( GroupMember member in g.Members )
			{
				if( member.Group != null ) throw new InvalidOperationException( "This should never happen." );

				RenderGroupMemberSymbolAsScalarProperty( ctx, member, w, depth + 1 );
			}

			w.WriteLine( "\t\t".PrefixTabs( depth ) );
			w.WriteLine( "\t\t}".PrefixTabs( depth ) );

			///////////////////////

			w.WriteLine( "\t\tpublic List<{0}> {1} {{ get; set; }} = new List<{0}>(); // {2} (All elements)".PrefixTabs( depth ), groupTypeName, propertyName, comment );
		}

		private static void RenderAndGroupClassAndListProperty( RenderContext ctx, Group g, StreamWriter w, Int32 depth )
		{
			Boolean noGroupChildren = g.Members.All( m => m.Group == null );
			if( !noGroupChildren ) throw new ArgumentException( "Group must not have any group members." );

			String propertyName  = String.Join( "_and_", g.Members.Select( m => m.GetCSharpSymbol() ) );
			String comment       = String.Join( " | ", g.Members.Select( m => m.GetCSharpSymbol() ) );
			String groupTypeName = propertyName + "Group";

			///////////////////////

			w.WriteLine( "\t\tpublic class {0}".PrefixTabs( depth ), groupTypeName );
			w.WriteLine( "\t\t{".PrefixTabs( depth ) );

			foreach( GroupMember member in g.Members )
			{
				if( member.Group != null ) throw new InvalidOperationException( "This should never happen." );

				RenderGroupMemberSymbolAsScalarProperty( ctx, member, w, depth + 1 );
			}

			w.WriteLine( "\t\t".PrefixTabs( depth ) );
			w.WriteLine( "\t\t}".PrefixTabs( depth ) );

			///////////////////////

			w.WriteLine( "\t\tpublic List<{0}> {1} {{ get; set; }} = new List<{0}>(); // {2} (All elements)".PrefixTabs( depth ), groupTypeName, propertyName, comment );
		}

		private static void RenderGroup_OneOrMore_Sequence( RenderContext ctx, Group g, StreamWriter w, Int32 depth )
		{
			w.WriteLine( "\t\t// RenderGroup_OneOrMore_Sequence - TODO".PrefixTabs( depth ) );
		}

		private static void RenderGroupMemberSymbolAsScalarProperty( RenderContext ctx, GroupMember member, StreamWriter w, Int32 depth )
		{
			if( member.Symbol == null ) throw new ArgumentException( "Member must be a symbol.", nameof(member) );

			ElementDecl el = ctx.Dtd.Elements[ member.Symbol ];

			if( !ShouldRenderAsClass( el ) )
			{
				// If the element's SGML type does not have a class in this project then it's a scalar value:
				if( el.ContentModel.Entity != null )
				{
					ParameterEntityMetadata pe = ctx.ParameterEntityMetadatas[ el.ContentModel.Entity.Name ];
								
					RenderParameterEntityProperty( w, member, pe, depth );
				}
				else
				{
					// Fallback to a string:
					w.WriteLine( "\t\tpublic String {0} {{ get; set; }} // TODO: Couldn't determine member type".PrefixTabs( depth ), member.GetCSharpSymbol() );
				}
			}
			else
			{
				w.WriteLine( "\t\tpublic {0} {0} {{ get; set; }}".PrefixTabs( depth ), member.GetCSharpSymbol() );
			}
		}

		#endregion

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

		private static void RenderParameterEntityProperty( StreamWriter w, GroupMember m, ParameterEntityMetadata pe, Int32 depth )
		{
			String typeName;
			if( pe.DataType == ParameterEnumDataType.Enum && pe.EnumValues.Count > 0 )
			{
				typeName = pe.Name + "Values";
			}
			else
			{
				typeName = GetCSharpTypeName( pe.DataType );
			}

			if( pe.Length != null )
			{
				w.WriteLine( "\t\t[StringLength( {0} )]".PrefixTabs( depth ), pe.Length.Value );
			}

			w.WriteLine( "\t\tpublic {0} {1} {{ get; set; }}".PrefixTabs( depth ), typeName, m.GetCSharpSymbol() );
		}

		private static void RenderRepeatingGroup( SgmlDtd dtd, Dictionary<String,ParameterEntityMetadata> parameterEntityMetadatas, Group g, StreamWriter w )
		{
			if( g.TextOnly ) throw new InvalidOperationException( "Repeating group that's TextOnly. This should never happen." );

			if( g.Members.Count == 0 )
			{
				throw new InvalidOperationException( "Repeating group with zero members. This should never happen." );
			}
			else if( g.Members.Count == 1 )
			{
				w.WriteLine( "\t\t// g.Members.Count == 1. Symbol: {0}. Group: {1}. Occurrence: {2}.",  g.Members[0].Symbol, g.Members[0].Group == null ? "null" : "Group", g.Occurrence );

				GroupMember member = g.Members[0];
				if( member.Group == null )
				{
					// Symbol member, not a Group member:
					w.WriteLine( "\t\tpublic List<{0}> {1} {{ get; set; }} = new List<{0}>();", member.Symbol, member.Symbol + "Values" );
				}
				else
				{
					// Sub-Group member:
					//RenderGroup( dtd, parameterEntityMetadatas, member.Group, w );
					Group subGroup = member.Group;

					w.WriteLine( "// TODO. Sub-group repeated {0}. TextOnly: {1}. Members.Count: {2}", subGroup.Occurrence, subGroup.TextOnly, subGroup.Members.Count );
				}

				
			}
			else // g.Members.Count > 1
			{
				w.WriteLine( "// TODO. Members.Count == {0}", g.Members.Count );
			}
		}

		//private static void RenderRepeatingGroup

		private static String GetCSharpTypeName( ParameterEnumDataType t )
		{
			switch( t )
			{
			case ParameterEnumDataType.Bool       : return "Boolean";
			case ParameterEnumDataType.Char       : return "Char";
			case ParameterEnumDataType.Date       : return "DateTime";
			case ParameterEnumDataType.Empty      : return "Boolean"; // the property is true if the element was present, false if it's absent.
			case ParameterEnumDataType.Enum       : throw new ArgumentOutOfRangeException( nameof(t),t, "Enum types must have enum values." );
			case ParameterEnumDataType.Html       : return "String";
			case ParameterEnumDataType.Integer    : return "Int32";
			case ParameterEnumDataType.None       : throw new ArgumentOutOfRangeException( nameof(t),t, "A property's type cannot be None." );
			case ParameterEnumDataType.Numeric    : return "Decimal";
			case ParameterEnumDataType.Time       : return "TimeSpan";
			case ParameterEnumDataType.UnicodeText: return "String";
			default                               : throw new ArgumentOutOfRangeException( nameof(t),t, "Unrecognized value." );
			}
		}
	}

	internal static partial class Extensions
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

		public static String PrefixTabs(this String str, Int32 depth)
		{
			String prefix = "".PadLeft( depth, '\t' );
			return prefix + str;
		}
	}

	
}
