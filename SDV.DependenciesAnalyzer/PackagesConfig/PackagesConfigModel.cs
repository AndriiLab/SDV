using System.Xml.Serialization;

namespace SDV.DependenciesAnalyzer.PackagesConfig;

[XmlRoot(ElementName="packages")]
public class PackagesConfigModel { 

    [XmlElement(ElementName="package")] 
    public List<Package>? Package { get; set; } 
}


[XmlRoot(ElementName="package")]
public class Package { 

    [XmlAttribute(AttributeName="id")] 
    public string Id { get; set; } 

    [XmlAttribute(AttributeName="version")] 
    public string Version { get; set; } 

    [XmlAttribute(AttributeName="targetFramework")] 
    public string TargetFramework { get; set; } 
}
