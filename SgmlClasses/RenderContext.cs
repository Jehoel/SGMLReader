using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Sgml;

namespace SgmlClasses
{
	public class RenderContext
	{
		public RenderContext(SgmlDtd dtd, Dictionary<String, ParameterEntityMetadata> parameterEntityMetadatas)
		{
			this.Dtd = dtd;
			this.ParameterEntityMetadatas = parameterEntityMetadatas;
		}

		public SgmlDtd Dtd { get; }
		public Dictionary<String,ParameterEntityMetadata> ParameterEntityMetadatas { get; }

		public Dictionary<String,Group> Macros { get; } = new Dictionary<String,Group>();
	}
}
