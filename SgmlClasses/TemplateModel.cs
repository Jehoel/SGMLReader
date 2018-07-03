using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SgmlClasses
{
	public class TemplateModel
	{
		public List<EnumClass> Enums { get; } = new List<EnumClass>();

		public List<Class> Classes { get; } = new List<Class>();
	}

	public class EnumClass
	{
		public String Name { get; set; }

		public List<String> Values { get; } = new List<String>();
	}

	public class Class
	{
		public String Name { get; set; }
		
		public List<String> Constructors { get; } = new List<String>();

		public List<String> Fields { get; } = new List<String>();

		public List<String> Properties { get; } = new List<String>();

		public List<String> Methods { get; } = new List<String>();
	}
}
