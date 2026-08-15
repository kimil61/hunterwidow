using System;
using System.IO;
using HunterWidow.Domain.Content;
using HunterWidow.Unity.Gameplay;
using HunterWidow.Unity.Presentation;
using UnityEngine;

namespace HunterWidow.Unity.Content
{
    /// <summary>
    /// The initial scene never assumes data exists. This makes a bad or empty content
    /// pack a recoverable authoring state instead of a startup crash.
    /// </summary>
    public sealed class HunterWidowBootstrap : MonoBehaviour
    {
        private const string ContentPathVariable = "HUNTERWIDOW_CONTENT_PATH";
        private const float ReportWidth = 900f;
        private const float ReportPadding = 24f;

        private ContentLoadResult result;
        private ContentBootStatus bootStatus;
        private ContentLocalizer fallbackLocalizer;
        private Vector2 scrollPosition;
        private bool canRunMvp;

        public ContentLoadResult CurrentContent => result;

        public bool CanRunMvp => canRunMvp;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateIfNeeded()
        {
            if (FindFirstObjectByType<HunterWidowBootstrap>() != null)
            {
                return;
            }

            var root = new GameObject("HunterWidowBootstrap");
            root.AddComponent<HunterWidowBootstrap>();
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            fallbackLocalizer = new ContentLocalizer(Path.Combine(Application.streamingAssetsPath, "bootstrap_locale.csv"));
            ReloadContent();

            if (canRunMvp && GetComponent<HunterWidowGameController>() == null)
            {
                gameObject.AddComponent<HunterWidowGameController>();
            }
        }

        public void ReloadContent()
        {
            result = ContentLoader.Load(GetContentPath());
            bootStatus = ContentBootStatus.From(result);
            canRunMvp = bootStatus.CanStart && MvpContentRequirements.IsReady(result.Database);
        }

        private static string GetContentPath()
        {
            var overridePath = Environment.GetEnvironmentVariable(ContentPathVariable);
            return string.IsNullOrWhiteSpace(overridePath)
                ? Path.Combine(Application.streamingAssetsPath, "content")
                : overridePath;
        }

        private void OnGUI()
        {
            if (bootStatus == null || canRunMvp)
            {
                return;
            }

            var canaryPack = bootStatus.CanStart;
            var width = Mathf.Min(ReportWidth, Screen.width - (ReportPadding * 2f));
            var height = Mathf.Min(Screen.height - (ReportPadding * 2f), 640f);
            var x = (Screen.width - width) * 0.5f;
            var y = (Screen.height - height) * 0.5f;
            GUI.Box(new Rect(x, y, width, height), BootstrapText(canaryPack ? "bootstrap.canary.title" : "bootstrap.invalid.title"));
            GUI.Label(new Rect(x + ReportPadding, y + 38f, width - (ReportPadding * 2f), 26f), BootstrapText(canaryPack ? "bootstrap.canary.body" : "bootstrap.invalid.body"));
            var reportText = canaryPack ? BootstrapText("bootstrap.canary.detail") : result.Report.ToMultilineText();
            scrollPosition = GUI.BeginScrollView(
                new Rect(x + ReportPadding, y + 72f, width - (ReportPadding * 2f), height - 112f),
                scrollPosition,
                new Rect(0f, 0f, width - (ReportPadding * 3f), Mathf.Max(120f, canaryPack ? 120f : result.Report.Issues.Count * 26f)));
            GUI.TextArea(new Rect(0f, 0f, width - (ReportPadding * 3f), Mathf.Max(120f, canaryPack ? 120f : result.Report.Issues.Count * 26f)), reportText);
            GUI.EndScrollView();

            if (GUI.Button(new Rect(x + width - 136f, y + height - 34f, 112f, 24f), BootstrapText("bootstrap.retry")))
            {
                ReloadContent();
            }
        }

        private string BootstrapText(string key)
        {
            return fallbackLocalizer == null ? key : fallbackLocalizer.Get(key);
        }
    }
}
