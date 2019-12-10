namespace X12.Core.Parsing
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Xml;
    using System.Xml.Xsl;
    
    using X12.Core.Parsing.Properties;
    using X12.Core.Shared.Exceptions;
    using X12.Core.Shared.Models;
    using X12.Core.Specifications.Finders;
    using X12.Core.Specifications.Interfaces;
    using X12.Core.Transformations;

    /// <summary>
    /// Parser for converting X12 documents to Interchanges
    /// </summary>
    public class X12Parser
    {
        private readonly ISpecificationFinder specFinder;
        private readonly bool throwExceptionOnSyntaxErrors;
        private readonly char[] ignoredChars;

        /// <summary>
        /// Initializes a new instance of the <see cref="X12Parser"/> class
        /// </summary>
        /// <param name="specFinder">Specification finder for determining how to process X12</param>
        /// <param name="throwExceptionOnSyntaxErrors">Flag if exceptions should be thrown with invalid syntax</param>
        /// <param name="ignoredChars">Characters to be ignored by the parser</param>
        public X12Parser(ISpecificationFinder specFinder, bool throwExceptionOnSyntaxErrors, char[] ignoredChars)
        {
            this.specFinder = specFinder;
            this.throwExceptionOnSyntaxErrors = throwExceptionOnSyntaxErrors;
            this.ignoredChars = ignoredChars;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="X12Parser"/> class
        /// </summary>
        /// <param name="specFinder">Specification finder for determining how to process X12</param>
        /// <param name="throwExceptionOnSyntaxErrors">Flag if exceptions should be thrown with invalid syntax</param>
        public X12Parser(ISpecificationFinder specFinder, bool throwExceptionOnSyntaxErrors)
            : this(specFinder, throwExceptionOnSyntaxErrors, new char[] { })
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="X12Parser"/> class
        /// </summary>
        /// <param name="specFinder">Specification finder for determining how to process X12</param>
        public X12Parser(ISpecificationFinder specFinder)
            : this(specFinder, true, new char[] { })
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="X12Parser"/> class
        /// </summary>
        /// <param name="throwExceptionOnSyntaxErrors">Flag if exceptions should be thrown with invalid syntax</param>
        public X12Parser(bool throwExceptionOnSyntaxErrors)
            : this(new SpecificationFinder(), throwExceptionOnSyntaxErrors, new char[] { })
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="X12Parser"/> class
        /// </summary>
        public X12Parser()
            : this(new SpecificationFinder(), true, new char[] { })
        {
        }

        /// <summary>
        /// Event handler definition for broadcasting issues with the X12
        /// </summary>
        /// <param name="sender">Object sending the call</param>
        /// <param name="args">Additional event arguments</param>
        public delegate void X12ParserWarningEventHandler(object sender, X12ParserWarningEventArgs args);

        /// <summary>
        /// Event hook to be triggered on parser warning
        /// </summary>
        public event X12ParserWarningEventHandler ParserWarning;

        /// <summary>
        /// Parses a single interchange from an X12 document
        /// </summary>
        /// <param name="x12">X12 data to be parsed</param>
        /// <returns>First interchange parsed from X12</returns>
        [Obsolete("Use ParseMultiple instead.  Parse will only return the first interchange in the file.")]
        public Interchange Parse(string x12)
        {
            byte[] byteArray = Encoding.UTF8.GetBytes(x12);
            using (var mstream = new MemoryStream(byteArray))
            {
                return this.Parse(mstream);
            }
        }

        /// <summary>
        /// Parses a single interchange from an X12 document
        /// </summary>
        /// <param name="stream">Stream pointing to source X12 data</param>
        /// <returns>First interchange parsed from X12</returns>
        [Obsolete("Use ParseMultiple instead.  Parse will only return the first interchange in the file.")]
        public Interchange Parse(Stream stream)
        {
            var interchanges = this.ParseMultiple(stream);
            if (interchanges.Count > 1)
            {
                throw new ApplicationException(Resources.X12ParserParseError);
            }

            return interchanges.FirstOrDefault();
        }

        /// <summary>
        /// Parses all interchanges from an X12 document
        /// </summary>
        /// <param name="x12">X12 data to be parsed</param>
        /// <returns><see cref="Interchange"/> collection parsed from X12</returns>
        public List<Interchange> ParseMultiple(string x12)
        {
            byte[] byteArray = Encoding.UTF8.GetBytes(x12);
            using (MemoryStream mstream = new MemoryStream(byteArray))
            {
                return this.ParseMultiple(mstream);
            }
        }

        /// <summary>
        /// Parses all interchanges from an X12 document
        /// </summary>
        /// <param name="stream">Stream pointing to source X12 data</param>
        /// <returns><see cref="Interchange"/> collection parsed from X12</returns>
        public List<Interchange> ParseMultiple(Stream stream)
        {
            return this.ParseMultiple(stream, Encoding.UTF8);
        }

        /// <summary>
        /// Parses all interchanges from an X12 document
        /// </summary>
        /// <param name="stream">Stream pointing to source X12 data</param>
        /// <param name="encoding">Stream encoding for reading data</param>
        /// <returns><see cref="Interchange"/> collection parsed from X12</returns>
        public List<Interchange> ParseMultiple(Stream stream, Encoding encoding)
        {
            var envelopes = new List<Interchange>();
            var exceptions = new List<Exception>();

            using (var reader = new X12StreamReader(stream, encoding, this.ignoredChars))
            {
                var envelop = new Interchange(this.specFinder, reader.CurrentIsaSegment);
                envelopes.Add(envelop);
                Container currentContainer = envelop;
                FunctionGroup fg = null;
                Transaction tr = null;
                var hloops = new Dictionary<string, HierarchicalLoop>();

                string segmentString = reader.ReadNextSegment();
                string segmentId = reader.ReadSegmentId(segmentString);
                int segmentIndex = 1;
                var containerStack = new Stack<string>();
                while (segmentString.Length > 0)
                {
                    switch (segmentId)
                    {
                        case "ISA":
                            // TODO Break this into its own function that is in a try-catch
                            //      Check inner methods to throw X12Exceptions instead of other types
                            envelop = new Interchange(this.specFinder, segmentString + reader.Delimiters.SegmentTerminator);
                            envelopes.Add(envelop);
                            currentContainer = envelop;
                            fg = null;
                            tr = null;
                            hloops = new Dictionary<string, HierarchicalLoop>();
                            break;
                        case "IEA":
                            if (envelop == null)
                            {
                                exceptions.Add(new X12Exception(string.Format(Resources.X12ParserMismatchSegment, segmentString, "ISA")));
                                continue;
                            }

                            envelop.SetTerminatingTrailerSegment(segmentString);
                            break;
                        case "GS":
                            if (envelop == null)
                            {
                                exceptions.Add(new InvalidOperationException(string.Format(Resources.X12ParserMissingPrecedingSegment, segmentString, "ISA")));
                                continue;
                            }

                            currentContainer = fg = envelop.AddFunctionGroup(segmentString);
                            break;
                        case "GE":
                            if (fg == null)
                            {
                                exceptions.Add(new InvalidOperationException(string.Format(Resources.X12ParserMismatchSegment, segmentString, "GS")));
                                continue;
                            }

                            fg.SetTerminatingTrailerSegment(segmentString);
                            fg = null;
                            break;
                        case "ST":
                            if (fg == null)
                            {
                                exceptions.Add(new InvalidOperationException(string.Format(Resources.X12ParserMissingGsSegment, segmentString)));
                                continue;
                            }

                            segmentIndex = 1;
                            currentContainer = tr = fg.AddTransaction(segmentString);
                            hloops = new Dictionary<string, HierarchicalLoop>();
                            break;
                        case "SE":
                            if (tr == null)
                            {
                                exceptions.Add(new InvalidOperationException(string.Format(Resources.X12ParserMismatchSegment, segmentString, "ST")));
                                continue;
                            }

                            tr.SetTerminatingTrailerSegment(segmentString);
                            tr = null;
                            break;
                        case "HL":
                            var hierarchicalLoopSegment = new Segment(null, reader.Delimiters, segmentString);
                            string id = hierarchicalLoopSegment.GetElement(1);
                            string parentId = hierarchicalLoopSegment.GetElement(2);
                            string levelCode = hierarchicalLoopSegment.GetElement(3);

                            while (!(currentContainer is HierarchicalLoopContainer hlCurrentContainer && hlCurrentContainer.AllowsHierarchicalLoop(levelCode)))
                            {
                                if (currentContainer.Parent != null)
                                {
                                    currentContainer = currentContainer.Parent;
                                }
                                else
                                {
                                    exceptions.Add(new InvalidOperationException(string.Format(
                                        Resources.X12ParserInvalidHLoopSpecification,
                                        segmentString,
                                        tr.ControlNumber)));
                                    continue;
                                }
                            }

                            bool parentFound = false;
                            if (!string.IsNullOrEmpty(parentId))
                            {
                                if (hloops.ContainsKey(parentId))
                                {
                                    parentFound = true;
                                    currentContainer = hloops[parentId].AddHLoop(segmentString);
                                }
                                else
                                {
                                    if (this.throwExceptionOnSyntaxErrors)
                                    {
                                        exceptions.Add(new InvalidOperationException(string.Format(Resources.X12ParserMissingParentIdError, id, parentId)));
                                        continue;
                                    }

                                    this.OnParserWarning(new X12ParserWarningEventArgs
                                    {
                                        FileIsValid = false,
                                        InterchangeControlNumber = envelop.InterchangeControlNumber,
                                        FunctionalGroupControlNumber = fg.ControlNumber,
                                        TransactionControlNumber = tr.ControlNumber,
                                        SegmentPositionInInterchange = segmentIndex,
                                        Segment = segmentString,
                                        SegmentId = segmentId,
                                        Message = string.Format(Resources.X12ParserMissingParentIdWarning, id, parentId)
                                    });
                                }
                            }

                            if (string.IsNullOrEmpty(parentId) || !parentFound)
                            {
                                while (!(currentContainer is HierarchicalLoopContainer hlCurrentContainer && hlCurrentContainer.HasHierachicalSpecs()))
                                {
                                    currentContainer = currentContainer.Parent;
                                }

                                currentContainer = ((HierarchicalLoopContainer)currentContainer).AddHLoop(segmentString);
                            }

                            if (hloops.ContainsKey(id))
                            {
                                exceptions.Add(new InvalidOperationException(string.Format(Resources.X12ParserHLoopIdExists, segmentString, tr.ControlNumber, id)));
                                continue;
                            }

                            hloops.Add(id, (HierarchicalLoop)currentContainer);
                            break;
                        case "TA1":
                            if (envelop == null)
                            {
                                exceptions.Add(new InvalidOperationException(string.Format(Resources.X12ParserMismatchSegment, segmentString, "ISA")));
                                continue;
                            }

                            envelop.AddSegment(segmentString);
                            break;  
                        default:
                            var originalContainer = currentContainer;
                            containerStack.Clear();
                            while (currentContainer != null)
                            {
                                if (currentContainer.AddSegment(segmentString) != null)
                                {
                                    if (segmentId == "LE")
                                    {
                                        currentContainer = currentContainer.Parent;
                                    }

                                    break;
                                }

                                if (currentContainer is LoopContainer loopContainer)
                                {
                                    Loop newLoop = loopContainer.AddLoop(segmentString);
                                    if (newLoop != null)
                                    {
                                        currentContainer = newLoop;
                                        break;
                                    }

                                    if (currentContainer is Transaction tran)
                                    {
                                        if (this.throwExceptionOnSyntaxErrors)
                                        {
                                            exceptions.Add(new TransactionValidationException(
                                                Resources.X12ParserSegmentCannotBeIdentitied,
                                                tran.IdentifierCode,
                                                tran.ControlNumber,
                                                string.Empty,
                                                segmentString,
                                                segmentIndex,
                                                string.Join(",", containerStack)));
                                            continue;
                                        }

                                        currentContainer = originalContainer;
                                        currentContainer.AddSegment(segmentString, true);
                                        this.OnParserWarning(new X12ParserWarningEventArgs
                                        {
                                            FileIsValid = false,
                                            InterchangeControlNumber = envelop.InterchangeControlNumber,
                                            FunctionalGroupControlNumber = fg.ControlNumber,
                                            TransactionControlNumber = tran.ControlNumber,
                                            SegmentPositionInInterchange = segmentIndex,
                                            SegmentId = segmentId,
                                            Segment = segmentString,
                                            Message = string.Format(
                                                Resources.X12ParserSegmentWarning,
                                                tran.IdentifierCode,
                                                tran.ControlNumber,
                                                segmentString,
                                                segmentIndex,
                                                string.Join(",", containerStack),
                                                containerStack.LastOrDefault())
                                        });
                                        break;
                                    }

                                    if (currentContainer is Loop containerLoop)
                                    {
                                        containerStack.Push(containerLoop.Specification.LoopId);
                                    }

                                    if (currentContainer is HierarchicalLoop hloop)
                                    {
                                        containerStack.Push($"{hloop.Specification.LoopId}[{hloop.Id}]");
                                    }

                                    currentContainer = currentContainer.Parent;
                                    continue;
                                }

                                break;
                            }

                            break;
                    }

                    segmentString = reader.ReadNextSegment();
                    segmentId = reader.ReadSegmentId(segmentString);
                    segmentIndex++;
                }
            }

            if (exceptions.Any())
                throw new AggregateException("X12 Exceptions", exceptions);
            return envelopes;
        }

        /// <summary>
        /// Transforms XML data to X12 data and returns the string representation
        /// </summary>
        /// <param name="xml">XML data to be transformed</param>
        /// <returns>String representation of data in X12</returns>
        public string TransformToX12(string xml)
        {
            var transform = new XslCompiledTransform();
            Stream stream = TransformationStreamFactory.GetX12TransformationStream();
            transform.Load(XmlReader.Create(stream));

            using (var writer = new StringWriter())
            {
                transform.Transform(XmlReader.Create(new StringReader(xml)), new XsltArgumentList(), writer);
                return writer.GetStringBuilder().ToString();
            }
        }

        /// <summary>
        /// Separates all <see cref="Segment"/> objects from an <see cref="Interchange"/> and returns the collection
        /// </summary>
        /// <param name="interchange">Object to remove Segments from</param>
        /// <param name="loopId">Identifier of loop to unbundle</param>
        /// <returns>Collection of <see cref="Interchange"/> objects</returns>
        public List<Interchange> UnbundleByLoop(Interchange interchange, string loopId)
        {
            char terminator = interchange.Delimiters.SegmentTerminator;
            var service = new UnbundlingService(terminator);
            string isa = interchange.SegmentString;
            string iea = interchange.TrailerSegments.First().SegmentString;
            var list = new List<string>();
            foreach (FunctionGroup group in interchange.FunctionGroups)
            {
                foreach (Transaction transaction in group.Transactions)
                {
                    service.UnbundleHLoops(list, transaction, loopId);
                }
            }

            var interchanges = new List<Interchange>();
            foreach (string item in list)
            {
                var x12 = new StringBuilder();
                x12.Append($"{isa}{terminator}");
                x12.Append(item);
                x12.Append($"{iea}{terminator}");
                using (var mstream = new MemoryStream(Encoding.ASCII.GetBytes(x12.ToString())))
                {
                    interchanges.AddRange(this.ParseMultiple(mstream));
                }
            }

            return interchanges;
        }

        /// <summary>
        /// Separates all <see cref="Segment"/> objects from an <see cref="Interchange"/>
        /// </summary>
        /// <param name="interchange">Object to remove Segments from</param>
        /// <returns>Collection of <see cref="Interchange"/> objects</returns>
        public List<Interchange> UnbundleByTransaction(Interchange interchange)
        {
            var interchanges = new List<Interchange>();
            char terminator = interchange.Delimiters.SegmentTerminator;
            string isa = interchange.SegmentString;
            string iea = interchange.TrailerSegments.First().SegmentString;
            foreach (FunctionGroup group in interchange.FunctionGroups)
            {
                foreach (Transaction transaction in group.Transactions)
                {
                    var x12 = new StringBuilder();
                    x12.Append($"{isa}{terminator}");
                    x12.Append($"{group.SegmentString}{terminator}");
                    x12.Append(transaction.SerializeToX12(false));
                    x12.Append($"{group.TrailerSegments.First().SegmentString}{terminator}");
                    x12.Append($"{iea}{terminator}");

                    using (var mstream = new MemoryStream(Encoding.ASCII.GetBytes(x12.ToString())))
                    {
                        interchanges.AddRange(this.ParseMultiple(mstream));
                    }
                }
            }

            return interchanges;
        }

        private void OnParserWarning(X12ParserWarningEventArgs args)
        {
            this.ParserWarning?.Invoke(this, args);
        }
    }
}
