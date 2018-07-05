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
				w.WriteLine( "using System.Xml;" );
				w.WriteLine();
				w.WriteLine( "namespace MyNamespace" );
				w.WriteLine( "{" );
				w.WriteLine();

				EnumRenderer.RenderEnums( ctx, w );

				w.WriteLine();

				ElementRenderer.RenderElements( ctx, w );

				w.WriteLine( "}" );
				w.WriteLine();
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
