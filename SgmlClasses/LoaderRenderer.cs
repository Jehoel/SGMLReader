using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Sgml;

namespace SgmlClasses
{
	public static class LoaderRenderer
	{
		public static void RenderElementLoadMethod( RenderContext ctx, Group g, StreamWriter w, Int32 depth )
		{
			if( g.GroupType == GroupType.Or )
			{
				RenderGroupOrLoadMethod( ctx, g, w, depth );
			}
			else if( g.GroupType == GroupType.And )
			{
				RenderGroupAndLoadMethod( ctx, g, w, depth );
			}
			else if( g.GroupType == GroupType.Sequence )
			{
				RenderGroupSequenceLoadMethod( ctx, g, w, depth );
			}
			else if( g.GroupType == GroupType.None )
			{
				RenderGroupNoneLoadMethod( ctx, g, w, depth );
			}
		}
		/*
		private static void RenderGroupMemberSymbolAsScalarAssignment( RenderContext ctx, GroupMember member, StreamWriter w, Int32 depth )
		{
			if( member.Symbol == null ) throw new ArgumentException( "Member must be a symbol.", nameof(member) );

			ElementDecl el = ctx.Dtd.Elements[ member.Symbol ];

			if( !ElementRenderer.ShouldRenderElementAsClass( el ) )
			{
				// If the element's SGML type does not have a class in this project then it's a scalar value:
				if( el.ContentModel.Entity != null )
				{
					ParameterEntityMetadata pe = ctx.ParameterEntityMetadatas[ el.ContentModel.Entity.Name ];
								
					//RenderParameterEntityProperty( w, member, pe, depth );
				}
				else
				{
					// Fallback to a string:
					w.WriteLine( "this.{0} = ;".PrefixTabs( 3 + depth ), member.GetCSharpSymbol() );
				}
			}
			else
			{
				w.WriteLine( "this.{0} = ;".PrefixTabs( 3 + depth ), member.GetCSharpSymbol() );
			}
		}*/

		public static void RenderGroupOrLoadMethod( RenderContext ctx, Group g, StreamWriter w, Int32 depth )
		{
			w.WriteLine( "public void Load( XmlElement e )".PrefixTabs(depth) );
			w.WriteLine( "{".PrefixTabs(depth) );

			// For the case where all children are elements then the solution is simple:
			// But how to do it for non-trivial cases? e.g. nested groups with optional leading members?

			if( g.Members.All( m => m.Symbol != null ) )
			{
				Boolean first = true;
				foreach( GroupMember member in g.Members )
				{
					w.Write( "\t\t".PrefixTabs(depth) );
					if( !first )
					{
						w.Write( "else " );
					}
					else
					{
						first = false;
					}

					w.WriteLine(       "if( e.Name == {0}.TagName )", member.GetCSharpSymbol() );
					w.WriteLine( "\t\t{".PrefixTabs(depth) );
					w.WriteLine( "\t\t\tthis.{0} = new {0}();".PrefixTabs(depth), member.GetCSharpSymbol() );
					w.WriteLine( "\t\t\tthis.{0}.Load( e );".PrefixTabs(depth), member.GetCSharpSymbol() );
					w.WriteLine( "\t\t}".PrefixTabs(depth) );
				}
			}
			else
			{
				w.WriteLine( "// TODO: Group with OR children.".PrefixTabs(depth) );
			}

			w.WriteLine( "}".PrefixTabs(depth) );
		}

		public static void RenderGroupAndLoadMethod( RenderContext ctx, Group g, StreamWriter w, Int32 depth )
		{

			w.WriteLine( "public void Load( XmlElement e )".PrefixTabs(depth) );
			w.WriteLine( "{".PrefixTabs(depth) );

			if( g.Members.All( m => m.Symbol != null ) )
			{
				foreach( GroupMember member in g.Members )
				{
					// TODO: How to do this for AND? Does order matter?
				}
			}
			else
			{
				w.WriteLine( "// TODO: Group with OR children.".PrefixTabs(depth) );
			}

			w.WriteLine( "}".PrefixTabs(depth) );
		}

		public static void RenderGroupSequenceLoadMethod( RenderContext ctx, Group g, StreamWriter w, Int32 depth )
		{
			w.WriteLine( "public void Load( XmlElement e )".PrefixTabs(depth) );
			w.WriteLine( "{".PrefixTabs(depth) );

			if( g.Members.All( m => m.Symbol != null ) )
			{
				foreach( GroupMember member in g.Members )
				{
					// TODO: Take current 'e' parameter, assign to first in Sequence, then `e = e.NextSibling()` and repeat for next in sequence
				}
			}
			else
			{
				w.WriteLine( "// TODO: Group with OR children.".PrefixTabs(depth) );
			}

			w.WriteLine( "}".PrefixTabs(depth) );
		}

		public static void RenderGroupNoneLoadMethod( RenderContext ctx, Group g, StreamWriter w, Int32 depth )
		{
			w.WriteLine( "public void Load( XmlElement e )".PrefixTabs(depth) );
			w.WriteLine( "{".PrefixTabs(depth) );

			w.WriteLine( "// TODO - Group with NONE children.".PrefixTabs(depth) );

			w.WriteLine( "}".PrefixTabs(depth) );
		}
	}
}
