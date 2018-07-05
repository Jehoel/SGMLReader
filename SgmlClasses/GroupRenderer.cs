using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Sgml;

namespace SgmlClasses
{
	public static class GroupRenderer
	{
		public static void RenderGroup( RenderContext ctx, Group g, StreamWriter w, Int32 depth )
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
			Boolean allChildrenAreElementClasses = g.Members.All( m => m.Group == null && ElementRenderer.ShouldRenderElementAsClass( ctx.Dtd.Elements[m.Symbol] ) );
			if( allChildrenAreElementClasses )
			{
				RenderGroupClassAndSingleProperty( ctx, g, w, depth );
			}
			else
			{
				Boolean allChildrenAreValues = g.Members.All( m => m.Group == null && !ElementRenderer.ShouldRenderElementAsClass( ctx.Dtd.Elements[m.Symbol] ) );
				if( allChildrenAreValues )
				{
					String propertyName = String.Join( "_or_", g.Members.Select( m => m.GetCSharpSymbol() ) );
					String comment      = String.Join( " | ", g.Members.Select( m => m.GetCSharpSymbol() ) );

					w.WriteLine( "\t\tpublic String {0} {{ get; set; }} // {1}".PrefixTabs(depth), propertyName, comment );
				}
				else
				{
					RenderGroupClassAndSingleProperty( ctx, g, w, depth + 1 );
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
					RenderGroupMemberSymbolAsScalarProperty( ctx, member, w, depth + 1 );
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
					RenderGroupMemberSymbolAsScalarProperty( ctx, member, w, depth + 1 );
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
			w.WriteLine( "\t\t// RenderGroup_Optional_Or".PrefixTabs( depth ) );

			// BEGIN Copy+pasted from RenderGroup_Required_Or:

			// A single property for any single element in the membership list.
			// Quick approach: the property is typed `Element`
			// Best approach: the possible element types (C# classes) are annotated with a new interface that the property is typed as.
			// Another approach: a new class is defined for the group that has strongly-typed properties, but only 1 such property is populated.

			// Problem: the group's options are not necessarily elements, but could be entities or other groups (aaaah).

			// Handle the easier case where all members are elements (a subset of the case where all children are symbols)
			Boolean allChildrenAreElementClasses = g.Members.All( m => m.Group == null && ElementRenderer.ShouldRenderElementAsClass( ctx.Dtd.Elements[m.Symbol] ) );
			if( allChildrenAreElementClasses )
			{
				RenderGroupClassAndSingleProperty( ctx, g, w, depth );
			}
			else
			{
				Boolean allChildrenAreValues = g.Members.All( m => m.Group == null && !ElementRenderer.ShouldRenderElementAsClass( ctx.Dtd.Elements[m.Symbol] ) );
				if( allChildrenAreValues )
				{
					String propertyName = String.Join( "_or_", g.Members.Select( m => m.GetCSharpSymbol() ) );
					String comment      = String.Join( " | ", g.Members.Select( m => m.GetCSharpSymbol() ) );

					w.WriteLine( "\t\tpublic String {0} {{ get; set; }} // {1} (allChildrenAreValues)".PrefixTabs(depth), propertyName, comment );
				}
				else
				{
					//w.WriteLine( "\t\t// RenderGroup_Optional_Or - TODO (Non-element children)".PrefixTabs( depth ) );

					// Render a class for this group that uses a class `Group` to represent any group children:

					RenderGroupClassAndSingleProperty( ctx, g, w, depth + 1 );
				}
			}

			// END Copy+pasted from RenderGroup_Required_Or:
		}

		private static void RenderGroup_Optional_Sequence( RenderContext ctx, Group g, StreamWriter w, Int32 depth )
		{
			w.WriteLine( "\t\t// RenderGroup_Optional_Sequence".PrefixTabs( depth ) );

			// If all members of a group are optional then just render them directly. No need for a separate class.

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

		private static void RenderGroup_ZeroOrMore_None( RenderContext ctx, Group g, StreamWriter w, Int32 depth )
		{
			w.WriteLine( "\t\t// RenderGroup_ZeroOrMore_None".PrefixTabs( depth ) );

			RenderGroupClassAndListProperty( ctx, g, w, depth );
		}

		private static void RenderGroup_ZeroOrMore_And( RenderContext ctx, Group g, StreamWriter w, Int32 depth )
		{
			w.WriteLine( "\t\t// RenderGroup_ZeroOrMore_And".PrefixTabs( depth ) );

			RenderGroupClassAndListProperty( ctx, g, w, depth );
		}

		private static void RenderGroup_ZeroOrMore_Or( RenderContext ctx, Group g, StreamWriter w, Int32 depth )
		{
			w.WriteLine( "\t\t// RenderGroup_ZeroOrMore_Or".PrefixTabs( depth ) );

			RenderGroupClassAndListProperty( ctx, g, w, depth );
		}

		private static void RenderGroup_ZeroOrMore_Sequence( RenderContext ctx, Group g, StreamWriter w, Int32 depth )
		{
			w.WriteLine( "\t\t// RenderGroup_ZeroOrMore_Sequence".PrefixTabs( depth ) );

			RenderGroupClassAndListProperty( ctx, g, w, depth );
		}



		private static void RenderGroup_OneOrMore_None( RenderContext ctx, Group g, StreamWriter w, Int32 depth )
		{
			w.WriteLine( "\t\t// RenderGroup_OneOrMore_None".PrefixTabs( depth ) );

			RenderGroupClassAndListProperty( ctx, g, w, depth );
		}

		private static void RenderGroup_OneOrMore_And( RenderContext ctx, Group g, StreamWriter w, Int32 depth )
		{
			w.WriteLine( "\t\t// RenderGroup_OneOrMore_And".PrefixTabs( depth ) );

			RenderGroupClassAndListProperty( ctx, g, w, depth );
		}

		private static void RenderGroup_OneOrMore_Or( RenderContext ctx, Group g, StreamWriter w, Int32 depth )
		{
			w.WriteLine( "\t\t// RenderGroup_OneOrMore_Or".PrefixTabs( depth ) );

			RenderGroupClassAndListProperty( ctx, g, w, depth );
		}

		private static void RenderGroup_OneOrMore_Sequence( RenderContext ctx, Group g, StreamWriter w, Int32 depth )
		{
			w.WriteLine( "\t\t// RenderGroup_OneOrMore_Sequence".PrefixTabs( depth ) );

			RenderGroupClassAndListProperty( ctx, g, w, depth );
		}

		#region Render

		private static void RenderGroupMemberSymbolAsScalarProperty( RenderContext ctx, GroupMember member, StreamWriter w, Int32 depth )
		{
			if( member.Symbol == null ) throw new ArgumentException( "Member must be a symbol.", nameof(member) );

			ElementDecl el = ctx.Dtd.Elements[ member.Symbol ];

			if( !ElementRenderer.ShouldRenderElementAsClass( el ) )
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

		private static void RenderGroupClassAndListProperty( RenderContext ctx, Group g, StreamWriter w, Int32 depth )
		{
			RenderGroupAsClass( ctx, g, w, depth, out String groupTypeName, out String propertyName, out String comment );

			w.WriteLine( "\t\tpublic List<{0}> {1} {{ get; set; }} = new List<{0}>(); // {2}".PrefixTabs( depth ), groupTypeName, propertyName, comment );
		}

		private static void RenderGroupClassAndSingleProperty( RenderContext ctx, Group g, StreamWriter w, Int32 depth )
		{
			RenderGroupAsClass( ctx, g, w, depth, out String groupTypeName, out String propertyName, out String comment );

			w.WriteLine( "\t\tpublic {0} {1} {{ get; set; }} // {2}".PrefixTabs( depth ), groupTypeName, propertyName, comment );
		}

		private static void RenderGroupAsClass( RenderContext ctx, Group g, StreamWriter w, Int32 depth, out String groupTypeName, out String propertyName, out String comment )
		{
			String sepProperty;
			String sepComment;
			switch( g.GroupType )
			{
			case GroupType.And:
				sepProperty = "_and_";
				sepComment = " & ";
				break;
			case GroupType.None:
				sepProperty = "_none_";
				sepComment = " ";
				break;
			case GroupType.Or:
				sepProperty = "_or_";
				sepComment = " | ";
				break;
			case GroupType.Sequence:
				sepProperty = "_seq_";
				sepComment = ", ";
				break;
			default:
				throw new ArgumentException();
			}

			propertyName  = String.Join( sepProperty, g.Members.Select( m => m.Symbol != null ? m.GetCSharpSymbol() : "Group" ) ); // NOTE: This is incorrect because it only looks at the first-level children of the group to determine the group's name.
			comment       = String.Join( sepComment , g.Members.Select( m => m.Symbol != null ? m.GetCSharpSymbol() : "Group" ) );
			groupTypeName = propertyName + "_Group";

			///////////////////////

			w.WriteLine( "\t\tpublic class {0}".PrefixTabs( depth ), groupTypeName );
			w.WriteLine( "\t\t{".PrefixTabs( depth ) );

			foreach( GroupMember member in g.Members )
			{
				if( member.Group != null )
				{
					RenderGroup( ctx, member.Group, w, depth + 1 );
				}
				else
				{
					RenderGroupMemberSymbolAsScalarProperty( ctx, member, w, depth + 1 );
				}
			}

			w.WriteLine();

			if( g.GroupType == GroupType.Or )
			{
				LoaderRenderer.RenderGroupOrLoadMethod( ctx, g, w, depth );
			}
			else if( g.GroupType == GroupType.And )
			{
				LoaderRenderer.RenderGroupAndLoadMethod( ctx, g, w, depth );
			}
			else if( g.GroupType == GroupType.Sequence )
			{
				LoaderRenderer.RenderGroupSequenceLoadMethod( ctx, g, w, depth );
			}
			else if( g.GroupType == GroupType.None )
			{
				LoaderRenderer.RenderGroupNoneLoadMethod( ctx, g, w, depth );
			}

			w.WriteLine( "\t\t".PrefixTabs( depth ) );
			w.WriteLine( "\t\t}".PrefixTabs( depth ) );
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

		#endregion
	}

}
