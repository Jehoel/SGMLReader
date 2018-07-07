using System;
using System.IO;

using Sgml;

namespace SgmlClasses
{
	public static class ElementRenderer
	{
		public static void RenderElements( RenderContext ctx, StreamWriter w )
		{
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

			foreach( ElementDecl el in ctx.Dtd.Elements.Values )
			{
				// if an element has no members and is text-only, then it doesn't need to be its own class.
				if( !ShouldRenderElementAsClass( el ) ) continue;

				RenderElementClass( ctx, el, w );
			}

			w.WriteLine();
			w.WriteLine( @"
	public static class Elements
	{
		public static Element Create( String tagName )
		{
			switch( tagName )
			{");

			foreach( ElementDecl el in ctx.Dtd.Elements.Values )
			{
				if( !ShouldRenderElementAsClass( el ) ) continue;

				w.WriteLine( @"			case ""{0}"": return new {0}();", el.Name );
			}

			w.WriteLine( @"
			default: return null;
			}
		}
	}
" );
		}

		public static Boolean ShouldRenderElementAsClass( ElementDecl el )
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

		public static void RenderElementClass( RenderContext ctx, ElementDecl el, StreamWriter w )
		{
			w.WriteLine( "\tpublic class {0} : Element", el.GetCSharpName() );
			w.WriteLine( "\t{" );
			w.WriteLine( "\t\tinternal const String TagNameStr = \"{0}\";", el.Name );
			w.WriteLine( "\t\tpublic static String TagName => TagNameStr;" );
			w.WriteLine( "\t\tpublic {0}() : base( TagNameStr ) {{}}", el.GetCSharpName() );
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

					GroupRenderer.RenderGroup( ctx, el.ContentModel.Group, w, depth: 0 );

					LoaderRenderer.RenderElementLoadMethod( ctx, el.ContentModel.Group, w, depth: 0 );

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
}
