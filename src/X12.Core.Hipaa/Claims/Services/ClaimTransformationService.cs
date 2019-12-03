namespace X12.Core.Hipaa.Claims.Services
{
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using System.Xml;
    using System.Xml.Xsl;

    using X12.Core.Parsing;
    using X12.Core.Shared.Models;

    /// <summary>
    /// Provides <see cref="ClaimDocument"/> transformation methods for converting 837 X12 data
    /// </summary>
    public class ClaimTransformationService
    {
        private readonly X12Parser parser;

        private Dictionary<string, string> revenueCodeToDescriptionMapping; 

        /// <summary>
        /// Initializes a new instance of the <see cref="ClaimTransformationService"/> class
        /// </summary>
        /// <param name="parser">X12 document parser</param>
        public ClaimTransformationService(X12Parser parser)
        {
            this.parser = parser;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ClaimTransformationService"/> class
        /// </summary>
        public ClaimTransformationService()
            : this(new X12Parser())
        {
        }
  
        /// <summary>
        /// Reads a claim that has been sent
        /// </summary>
        /// <param name="stream">Data stream used for reading</param>
        /// <returns><see cref="ClaimDocument"/> created from 837 stream</returns>
        public ClaimDocument Transform837ToClaimDocument(Stream stream)
        {
            var interchanges = this.parser.ParseMultiple(stream);
            var doc = new ClaimDocument();
            foreach (var interchange in interchanges)
            {
                var thisDoc = this.Transform837ToClaimDocument(interchange);
                this.AddRevenueCodeDescription(thisDoc);
                doc.Claims.AddRange(thisDoc.Claims);
            }

            return doc;
        }

        /// <summary>
        /// Populates the revenue code description mapping with the provided dictionary
        /// </summary>
        /// <param name="revCodeDictionary">Dictionary to populate into revenue code description mapping</param>
        public void FillRevenueCodeDescriptionMapping(Dictionary<string,string> revCodeDictionary)
        {
            this.revenueCodeToDescriptionMapping = revCodeDictionary;
        }

        /// <summary>
        /// Transforms the provided 837 <see cref="Interchange"/> to its matching <see cref="ClaimDocument"/>
        /// </summary>
        /// <param name="interchange">837 data to be transformed</param>
        /// <returns>Resultant ClaimDocument</returns>
        public ClaimDocument Transform837ToClaimDocument(Interchange interchange)
        {
            var xml = interchange.Serialize();

            var transformStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("X12.Core.Hipaa.Claims.Services.Xsl.X12-837-To-ClaimDocument.xslt");

            var transform = new XslCompiledTransform();
            if (transformStream != null)
            {
                transform.Load(XmlReader.Create(transformStream));
            }

            using (var outputStream = new MemoryStream())
            using (var xmlReader = new StringReader(xml))
            {
                transform.Transform(XmlReader.Create(xmlReader), new XsltArgumentList(), outputStream);
                outputStream.Position = 0;
                using (var reader = new StreamReader(outputStream))
                {
                    xml = reader.ReadToEnd();
                }                
            }

            return ClaimDocument.Deserialize(xml);
        }

        private void AddRevenueCodeDescription(ClaimDocument claimdoc)
        {
            if (this.revenueCodeToDescriptionMapping == null)
            {
                return;
            }
            
             foreach (Claim claim in claimdoc.Claims)
             {
                 foreach (ServiceLine serviceLine in claim.ServiceLines)
                 {
                     if (serviceLine.RevenueCode != null)
                     {
                         if (this.revenueCodeToDescriptionMapping.ContainsKey(serviceLine.RevenueCode))
                         {
                             serviceLine.RevenueCodeDescription = this.revenueCodeToDescriptionMapping[serviceLine.RevenueCode];
                         }
                     }
                 }
             }
        }
    }
}
