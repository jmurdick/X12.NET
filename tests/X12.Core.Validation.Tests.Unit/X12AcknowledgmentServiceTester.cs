namespace X12.Core.Validation.Tests.Unit
{
    using System.IO;
    using System.Linq;
    using System.Reflection;

    using NUnit.Framework;

    using X12.Core.Validation;

    [TestFixture]
    public class X12AcknowledgmentServiceTester
    {
        [Test]
        public void Acknowledge837I_HasCorrectResponseCount()
        {
            var service = new InstitutionalClaimAcknowledgmentService();
            var responses = service.AcknowledgeTransactions(this.GetEdi("837I_4010_Batch1.txt"));

            Assert.That(responses.Count, Is.EqualTo(1));
        }

        [Test]
        public void Acknowledge837I_GroupControlNumberIsCorrect()
        {
            var service = new InstitutionalClaimAcknowledgmentService();
            var responses = service.AcknowledgeTransactions(this.GetEdi("837I_4010_Batch1.txt"));

            var response = responses.First();
            Assert.That(response.GroupControlNumber, Is.EqualTo("612200041"));
        }
        
        [Test]
        public void Acknowledge837I_HasCorrectTransactionSetResponsesCount()
        {
            var service = new InstitutionalClaimAcknowledgmentService();
            var responses = service.AcknowledgeTransactions(this.GetEdi("837I_4010_Batch1.txt"));

            var response = responses.First();
            Assert.That(response.TransactionSetResponses.Count, Is.EqualTo(54));
        }

        private Stream GetEdi(string filename)
        {
            return Assembly.GetExecutingAssembly().GetManifestResourceStream("X12.Core.Validation.Tests.Unit.Data." + filename);
        }
    }
}
