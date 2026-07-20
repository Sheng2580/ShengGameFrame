using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sheng.GameFramework.Editor.AgentBridge
{
    [Serializable]
    internal sealed class AgentCommandResult
    {
        public bool success;
        public string command;
        public string status;
        public string message;
        public string path;
    }

    [Serializable]
    internal sealed class AgentProjectSnapshot
    {
        public string productName;
        public string unityVersion;
        public string frameworkVersion;
        public string bridgeVersion;
        public string activeBuildTarget;
        public string activeScene;
        public bool activeSceneDirty;
        public bool isPlaying;
        public bool isCompiling;
        public bool runInBackground;
        public int loadedSceneCount;
        public int assetBundleCount;
        public List<string> enabledScenes = new List<string>();
        public List<string> assetBundles = new List<string>();
    }

    [Serializable]
    internal sealed class AgentIssue
    {
        public string severity;
        public string code;
        public string message;
        public string path;
    }

    [Serializable]
    internal sealed class AgentValidationReport
    {
        public bool success;
        public int errorCount;
        public int warningCount;
        public int scannedPrefabCount;
        public int scannedObjectCount;
        public List<AgentIssue> issues = new List<AgentIssue>();
    }

    [Serializable]
    internal sealed class AgentSceneHierarchy
    {
        public string scene;
        public int nodeCount;
        public bool truncated;
        public List<AgentSceneNode> nodes = new List<AgentSceneNode>();
    }

    [Serializable]
    internal sealed class AgentSceneNode
    {
        public string path;
        public bool activeSelf;
        public bool activeInHierarchy;
        public int layer;
        public string tag;
        public Vector3 position;
        public Vector3 rotation;
        public Vector3 scale;
        public List<string> components = new List<string>();
    }

    [Serializable]
    internal sealed class AgentUIHierarchy
    {
        public string scene;
        public int canvasCount;
        public int elementCount;
        public bool truncated;
        public List<AgentUIElement> elements = new List<AgentUIElement>();
    }

    [Serializable]
    internal sealed class AgentUIElement
    {
        public string path;
        public string canvas;
        public bool active;
        public Vector2 anchorMin;
        public Vector2 anchorMax;
        public Vector2 pivot;
        public Vector2 anchoredPosition;
        public Vector2 sizeDelta;
        public Vector3 localScale;
        public List<string> components = new List<string>();
    }

    [Serializable]
    internal sealed class AgentRuntimeSnapshot
    {
        public bool isPlaying;
        public bool isPaused;
        public int frameCount;
        public float deltaTime;
        public float framesPerSecond;
        public float timeScale;
        public long monoUsedBytes;
        public long totalAllocatedBytes;
        public long totalReservedBytes;
        public int gameObjectCount;
        public int cameraCount;
        public int canvasCount;
    }

    [Serializable]
    internal sealed class AgentCombinedDiagnostics
    {
        public AgentProjectSnapshot project;
        public AgentValidationReport validation;
        public AgentRuntimeSnapshot runtime;
    }

    [Serializable]
    internal sealed class AgentTaskState
    {
        public string id;
        public string name;
        public string status;
        public string message;
        public string outputPath;
        public string startedAtUtc;
        public string completedAtUtc;
    }

    [Serializable]
    internal sealed class AgentTestFailure
    {
        public string name;
        public string message;
        public string stackTrace;
    }

    [Serializable]
    internal sealed class AgentTestState
    {
        public string jobId;
        public string status;
        public int totalCount;
        public int passedCount;
        public int failedCount;
        public int skippedCount;
        public int inconclusiveCount;
        public double durationSeconds;
        public string startedAtUtc;
        public string completedAtUtc;
        public List<AgentTestFailure> failures = new List<AgentTestFailure>();
    }

    [Serializable]
    internal sealed class AgentScreenshotState
    {
        public string status;
        public string path;
        public string message;
        public string requestedAtUtc;
        public string completedAtUtc;
    }

    [Serializable]
    internal sealed class AgentEditorState
    {
        public bool isPlaying;
        public bool isPaused;
        public bool isCompiling;
        public bool isUpdating;
        public string activeScene;
        public string activeBuildTarget;
    }

    [Serializable]
    internal sealed class AgentLubanStatus
    {
        public bool ready;
        public bool installed;
        public bool dotnetAvailable;
        public string lubanVersion;
        public string lubanPath;
        public string dotnetPath;
        public string configRoot;
        public string jsonOutputDirectory;
        public int tableCount;
        public int errorCount;
        public string message;
        public List<string> errors = new List<string>();
    }

    [Serializable]
    internal sealed class AgentLubanValidation
    {
        public bool success;
        public string message;
        public List<string> errors = new List<string>();
    }

    [Serializable]
    internal sealed class AgentCommandCatalog
    {
        public List<AgentCommandDescriptor> commands = new List<AgentCommandDescriptor>();
    }

    [Serializable]
    internal sealed class AgentCommandDescriptor
    {
        public string method;
        public string description;
    }

    internal static class FrameworkAgentJson
    {
        public static string Serialize(object value)
        {
            return JsonUtility.ToJson(value, true);
        }

        public static string UtcNow()
        {
            return DateTime.UtcNow.ToString("O");
        }
    }
}
