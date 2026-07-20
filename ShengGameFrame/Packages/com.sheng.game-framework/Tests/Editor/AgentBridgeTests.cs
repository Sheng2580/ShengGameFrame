using NUnit.Framework;
using Sheng.GameFramework.Editor.AgentBridge;

namespace Sheng.GameFramework.Tests
{
    public sealed class AgentBridgeTests
    {
        [Test]
        public void CommandCatalog_ContainsCoreAutomationCommands()
        {
            string json = FrameworkAgentCommands.GetCommandCatalog();

            StringAssert.Contains("GetProjectSnapshot", json);
            StringAssert.Contains("StartEditModeTests", json);
            StringAssert.Contains("StartEditorAssetBundleBuild", json);
            StringAssert.Contains("CaptureGameView", json);
        }

        [Test]
        public void ProjectSnapshot_ContainsFrameworkAndBridgeVersions()
        {
            string json = FrameworkAgentCommands.GetProjectSnapshot();

            StringAssert.Contains("ShengGameFrame", json);
            StringAssert.Contains("frameworkVersion", json);
            StringAssert.Contains("bridgeVersion", json);
            StringAssert.Contains("activeBuildTarget", json);
        }

        [Test]
        public void ValidateProject_ReturnsStructuredReport()
        {
            string json = FrameworkAgentCommands.ValidateProject();

            StringAssert.Contains("success", json);
            StringAssert.Contains("errorCount", json);
            StringAssert.Contains("issues", json);
        }

        [Test]
        public void HierarchyCommands_ReturnStructuredCollections()
        {
            string sceneJson = FrameworkAgentCommands.DumpSceneHierarchy();
            string uiJson = FrameworkAgentCommands.DumpUIHierarchy();

            StringAssert.Contains("nodes", sceneJson);
            StringAssert.Contains("elements", uiJson);
        }
    }
}
