using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SgmlClasses
{
	public static class EnumRenderer
	{
		public static void RenderEnums( RenderContext ctx, StreamWriter w )
		{
			// Render all enum types:
			IEnumerable<ParameterEntityMetadata> pems = ctx.ParameterEntityMetadatas
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
		}
	}
}
