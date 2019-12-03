namespace X12.Core.Specifications
{
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Xml.Serialization;

    using X12.Core.Specifications.Enumerations;
    using X12.Core.Specifications.Interfaces;

    /// <summary>
    /// Represents the specificiation for an X12 Transaction
    /// </summary>
    [DebuggerStepThrough]
    [XmlType(AnonymousType = true)]
    [XmlRoot(Namespace = "http://tempuri.org/X12ParserSpecification.xsd")]
    public class TransactionSpecification : IContainerSpecification
    {
        /// <summary>
        /// Gets or sets the ID code for the transaction set
        /// </summary>
        [XmlAttribute]
        public string TransactionSetIdentifierCode { get; set; }

        /// <summary>
        /// Gets or sets the ID code for transaction function group
        /// </summary>
        [XmlAttribute]
        public string FunctionalGroupCode { get; set; }
        
        /// <summary>
        /// Gets or sets the transaction name
        /// </summary>
        [XmlElement]
        public string Name { get; set; }
        
        /// <summary>
        /// Gets or sets the collection of segment specifications
        /// </summary>
        [XmlElement(X12Elements.Segment)]
        public List<SegmentSpecification> SegmentSpecifications { get; set; }

        /// <summary>
        /// Gets or sets the collection of loop specifications
        /// </summary>
        [XmlElement(X12Elements.Loop)]
        public List<LoopSpecification> LoopSpecifications { get; set; }

        /// <summary>
        /// Gets or sets the collection of hierarchical loop specifications
        /// </summary>
        [XmlElement(X12Elements.HierarchicalLoop)]
        public List<HierarchicalLoopSpecification> HierarchicalLoopSpecifications { get; set; }

        /// <summary>
        /// Gets the ID of the container specification (defaults to <c>string.Empty</c>)
        /// </summary>
        string IContainerSpecification.LoopId => string.Empty;

        /// <summary>
        /// Deserializes an XML string to it's transaction equivalent
        /// </summary>
        /// <param name="xml">XML string to deserialized</param>
        /// <returns>Equivalent transaction specification</returns>
        public static TransactionSpecification Deserialize(string xml)
        {
            using (var stringReader = new StringReader(xml))
            using (var xmlTextReader = new System.Xml.XmlTextReader(stringReader))
            {
                var xmlSerializer = new XmlSerializer(typeof(TransactionSpecification));
                return (TransactionSpecification)xmlSerializer.Deserialize(xmlTextReader);
            }
        }

        /// <summary>
        /// Serializes the transaction to its string equivalent
        /// </summary>
        /// <returns>String representation of the transaction</returns>
        public string Serialize()
        {
            var xmlSerializer = new XmlSerializer(typeof(TransactionSpecification));
            using (var mstream = new MemoryStream())
            {
                xmlSerializer.Serialize(mstream, this);
                mstream.Seek(0, SeekOrigin.Begin);
                using (var streamReader = new StreamReader(mstream))
                {
                    return streamReader.ReadToEnd();
                }                    
            }
        }
    }
}
