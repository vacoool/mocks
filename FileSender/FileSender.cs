using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using FakeItEasy;
using FileSender.Dependencies;
using FluentAssertions;
using NUnit.Framework;

namespace FileSender
{
    public class FileSender
    {
        private readonly ICryptographer cryptographer;
        private readonly ISender sender;
        private readonly IRecognizer recognizer;

        public FileSender(
            ICryptographer cryptographer,
            ISender sender,
            IRecognizer recognizer)
        {
            this.cryptographer = cryptographer;
            this.sender = sender;
            this.recognizer = recognizer;
        }

        public Result SendFiles(File[] files, X509Certificate certificate)
        {
            return new Result
            {
                SkippedFiles = files
                    .Where(file => !TrySendFile(file, certificate))
                    .ToArray()
            };
        }

        private bool TrySendFile(File file, X509Certificate certificate)
        {
            Document document;
            if (!recognizer.TryRecognize(file, out document))
                return false;
            if (!CheckFormat(document) || !CheckActual(document))
                return false;
            var signedContent = cryptographer.Sign(document.Content, certificate);
            return sender.TrySend(signedContent);
        }

        private bool CheckFormat(Document document)
        {
            return document.Format == "4.0" ||
                   document.Format == "3.1";
        }

        private bool CheckActual(Document document)
        {
            return document.Created.AddMonths(1) > DateTime.Now;
        }

        public class Result
        {
            public File[] SkippedFiles { get; set; }
        }
    }

    //TODO: реализовать недостающие тесты
    [TestFixture]
    public class FileSender_Should
    {
        private FileSender fileSender;
        private ICryptographer cryptographer;
        private ISender sender;
        private IRecognizer recognizer;

        private readonly X509Certificate certificate = new X509Certificate();
        private File file;
        private byte[] signedContent;

        [SetUp]
        public void SetUp()
        {
            // Постарайтесь вынести в SetUp всё неспецифическое конфигурирование так,
            // чтобы в конкретных тестах осталась только специфика теста,
            // без конфигурирования "обычного" сценария работы

            file = new File("someFile", new byte[] {1, 2, 3});
            signedContent = new byte[] {1, 7};

            cryptographer = A.Fake<ICryptographer>();
            sender = A.Fake<ISender>();
            recognizer = A.Fake<IRecognizer>();
            fileSender = new FileSender(cryptographer, sender, recognizer);
        }

       // [Test]
        private IEnumerable<File> GenericTest(DateTime dateTime, string format, bool[] recRet, bool[] sendRet, int fileCount)
        {
            var document = new Document(file.Name, file.Content, dateTime, format);
            A.CallTo(() => recognizer.TryRecognize(file, out document))
                .ReturnsNextFromSequence(recRet);
            A.CallTo(() => cryptographer.Sign(document.Content, certificate))
                .Returns(signedContent);
            A.CallTo(() => sender.TrySend(signedContent))
                .ReturnsNextFromSequence(sendRet);
            
            var files = Enumerable.Repeat(file,fileCount).ToArray();
            return fileSender.SendFiles(files, certificate).SkippedFiles;
        }

        private IEnumerable<File> GenericTest(DateTime dateTime, string format, bool recRet, bool sendRet)
        {
            return GenericTest(dateTime, format,new []{recRet},new []{sendRet}, 1);
        }
        
        [Test]
        [TestCase("4.0")]
        [TestCase("3.1")]
        public void Send_WhenGoodFormat(string format)
        {
            GenericTest(DateTime.Now, format, true, true).Should().BeEmpty();
        }

        [Test]
        public void Skip_WhenBadFormat()
        {
            GenericTest(DateTime.Now, "2.1", true, true).Should().NotBeEmpty();
        }

        [Test]
       //[TestCase(1)]
        [TestCase(-1)]
        public void Skip_WhenOlderThanAMonth(int delta)
        {
            var dt = DateTime.Now.AddMonths(delta).AddMilliseconds(delta);
            GenericTest(dt, "3.1", true, true).Should().NotBeEmpty();
            /*
            GenericTest(DateTime.Now.Subtract(TimeSpan.FromDays(1000)), "3.1", new []{true}, new []{true}).Should().NotBeEmpty();
            var document = new Document(file.Name, file.Content, DateTime.Now.Subtract(TimeSpan.FromDays(1000)), "3.1");
            A.CallTo(() => recognizer.TryRecognize(file, out document))
                .Returns(true);
            A.CallTo(() => cryptographer.Sign(document.Content, certificate))
                .Returns(signedContent);
            A.CallTo(() => sender.TrySend(signedContent))
                .Returns(true);

            fileSender.SendFiles(new[] {file}, certificate)
                .SkippedFiles.Should().NotBeEmpty();
                */
        }

        [Test]
        [TestCase(1)]
       // [TestCase(-1)]
        public void Send_WhenYoungerThanAMonth(int delta)
        {
            var dt = DateTime.Now.AddMonths(-delta).AddSeconds(delta);
            GenericTest(dt, "3.1", true, true).Should().BeEmpty();
        }

        [Test]
        public void Skip_WhenSendFails()
        {
            GenericTest(DateTime.Now, "3.1", true, false).Should().NotBeEmpty();
        }

        [Test]
        public void Skip_WhenNotRecognized()
        {
            GenericTest(DateTime.Now, "3.1", false, true).Should().NotBeEmpty();
        }

        [Test]
        public void IndependentlySend_WhenSeveralFilesAndSomeAreInvalid()
        {
            GenericTest(DateTime.Now, "3.1", new [] {false, false, true}, new [] {true}, 3)
                .Count().ShouldBeEquivalentTo(2);
        }

        [Test]
        public void IndependentlySend_WhenSeveralFilesAndSomeCouldNotSend()
        {
            GenericTest(DateTime.Now, "3.1", new [] {true}, new [] {false, true, true}, 3)
                .Count().ShouldBeEquivalentTo(2);
        }
    }
}
