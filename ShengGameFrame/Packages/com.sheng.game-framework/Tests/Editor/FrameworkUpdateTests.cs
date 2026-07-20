using System;
using System.IO;
using NUnit.Framework;
using Sheng.GameFramework.Editor.Updater;

namespace Sheng.GameFramework.Tests
{
    public sealed class FrameworkUpdateTests
    {
        private const string CurrentRevision =
            "a534d0999adc2a74a0c3ab8f7b14ba54cf5799ff";
        private const string LatestRevision =
            "1234567890abcdef1234567890abcdef12345678";
        private const string OfficialDependency =
            "https://github.com/Sheng2580/ShengGameFrame.git"
            + "?path=/ShengGameFrame/Packages/com.sheng.game-framework#"
            + CurrentRevision;

        private string _root;
        private string _manifestPath;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(
                Path.GetTempPath(),
                "ShengFrameworkUpdateTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            _manifestPath = Path.Combine(_root, "manifest.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }

        [Test]
        public void Dependency_RecognizesOfficialRepositoryAndRevision()
        {
            Assert.IsTrue(
                FrameworkUpdateUtility.IsOfficialGitDependency(
                    OfficialDependency));
            Assert.AreEqual(
                CurrentRevision,
                FrameworkUpdateUtility.ExtractRevision(OfficialDependency));
            Assert.IsTrue(
                FrameworkUpdateUtility.IsSameRevision(
                    "a534d09",
                    CurrentRevision));
        }

        [Test]
        public void LatestRevision_ParsesFirstAtomEntry()
        {
            string response =
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>"
                + "<feed xmlns=\"http://www.w3.org/2005/Atom\">"
                + $"<entry><id>tag:github.com,2008:Grit::Commit/{LatestRevision}</id></entry>"
                + $"<entry><id>tag:github.com,2008:Grit::Commit/{CurrentRevision}</id></entry>"
                + "</feed>";
            bool success = FrameworkUpdateUtility.TryParseLatestRevision(
                response,
                out string revision,
                out string error);

            Assert.IsTrue(success, error);
            Assert.AreEqual(LatestRevision, revision);
            Assert.AreEqual("1234567", FrameworkUpdateUtility.ShortRevision(revision));
        }

        [Test]
        public void LatestRevision_RejectsAtomFeedWithoutCommit()
        {
            bool success = FrameworkUpdateUtility.TryParseLatestRevision(
                "<feed xmlns=\"http://www.w3.org/2005/Atom\"></feed>",
                out string revision,
                out string error);

            Assert.IsFalse(success);
            Assert.IsEmpty(revision);
            StringAssert.Contains("提交号无效", error);
        }

        [Test]
        public void ApplyRevision_OnlyUpdatesFrameworkDependency()
        {
            WriteManifest(OfficialDependency);

            bool success = FrameworkUpdateUtility.TryApplyRevision(
                _manifestPath,
                LatestRevision,
                out string updatedDependency,
                out string error);

            Assert.IsTrue(success, error);
            Assert.AreEqual(LatestRevision, FrameworkUpdateUtility.ExtractRevision(
                updatedDependency));

            string manifest = File.ReadAllText(_manifestPath);
            StringAssert.Contains(updatedDependency, manifest);
            StringAssert.Contains("com.unity.test-framework", manifest);
            StringAssert.Contains("1.1.33", manifest);
        }

        [Test]
        public void ApplyRevision_RejectsNonOfficialDependency()
        {
            WriteManifest(
                "file:../LocalPackages/com.sheng.game-framework");

            bool success = FrameworkUpdateUtility.TryApplyRevision(
                _manifestPath,
                LatestRevision,
                out string updatedDependency,
                out string error);

            Assert.IsFalse(success);
            Assert.IsEmpty(updatedDependency);
            StringAssert.Contains("官方 Git 仓库", error);
        }

        private void WriteManifest(string frameworkDependency)
        {
            File.WriteAllText(
                _manifestPath,
                "{\n"
                + "  \"dependencies\": {\n"
                + $"    \"{FrameworkUpdateUtility.PackageName}\": \"{frameworkDependency}\",\n"
                + "    \"com.unity.test-framework\": \"1.1.33\"\n"
                + "  }\n"
                + "}\n");
        }
    }
}
