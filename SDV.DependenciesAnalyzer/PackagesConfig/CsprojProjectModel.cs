using System.Xml.Serialization;

namespace SDV.DependenciesAnalyzer.PackagesConfig
{
	[XmlRoot(ElementName="Project", Namespace="http://schemas.microsoft.com/developer/msbuild/2003")]
	public class CsprojProjectModel {
		[XmlElement(ElementName="ItemGroup", Namespace="http://schemas.microsoft.com/developer/msbuild/2003")]
		public List<ItemGroup> ItemGroup { get; set; }
	}

	[XmlRoot(ElementName = "ItemGroup", Namespace = "http://schemas.microsoft.com/developer/msbuild/2003")]
	public class ItemGroup
	{
		[XmlElement(ElementName = "ProjectReference",
			Namespace = "http://schemas.microsoft.com/developer/msbuild/2003")]
		public List<CsProjProjectReference>? ProjectReference { get; set; }

	}

	[XmlRoot(ElementName="ProjectReference", Namespace="http://schemas.microsoft.com/developer/msbuild/2003")]
	public class CsProjProjectReference {
		[XmlElement(ElementName="Project", Namespace="http://schemas.microsoft.com/developer/msbuild/2003")]
		public string Project { get; set; }
		[XmlElement(ElementName="Name", Namespace="http://schemas.microsoft.com/developer/msbuild/2003")]
		public string Name { get; set; }
		[XmlAttribute(AttributeName="Include")]
		public string Include { get; set; }
	}
}
