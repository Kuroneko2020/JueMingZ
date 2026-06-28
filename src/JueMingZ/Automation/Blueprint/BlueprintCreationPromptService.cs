using System;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;

namespace JueMingZ.Automation.Blueprint
{
    internal interface IBlueprintCreationPromptSink
    {
        bool TryShowBlueprintCreationPrompt(BlueprintCreationPromptRequest request, out string failureReason);
    }

    internal sealed class BlueprintCreationPromptRequest
    {
        public string EventKind { get; set; }
        public string Text { get; set; }
        public int DurationFrames { get; set; }
        public int ColorR { get; set; }
        public int ColorG { get; set; }
        public int ColorB { get; set; }
    }

    internal sealed class BlueprintCreationPromptAttempt
    {
        private BlueprintCreationPromptAttempt()
        {
            EventKind = string.Empty;
            Text = string.Empty;
            ResultCode = string.Empty;
            FailureReason = string.Empty;
        }

        public bool Attempted { get; private set; }
        public bool Succeeded { get; private set; }
        public string EventKind { get; private set; }
        public string Text { get; private set; }
        public string ResultCode { get; private set; }
        public string FailureReason { get; private set; }
        public int DurationFrames { get; private set; }
        public int ColorR { get; private set; }
        public int ColorG { get; private set; }
        public int ColorB { get; private set; }

        public static BlueprintCreationPromptAttempt NotAttempted()
        {
            return new BlueprintCreationPromptAttempt
            {
                Attempted = false,
                Succeeded = false,
                ResultCode = "notAttempted"
            };
        }

        public static BlueprintCreationPromptAttempt Create(
            BlueprintCreationPromptRequest request,
            bool succeeded,
            string resultCode,
            string failureReason)
        {
            request = request ?? CreateEmptyRequest();
            return new BlueprintCreationPromptAttempt
            {
                Attempted = true,
                Succeeded = succeeded,
                EventKind = request.EventKind ?? string.Empty,
                Text = request.Text ?? string.Empty,
                DurationFrames = request.DurationFrames,
                ColorR = request.ColorR,
                ColorG = request.ColorG,
                ColorB = request.ColorB,
                ResultCode = resultCode ?? string.Empty,
                FailureReason = failureReason ?? string.Empty
            };
        }

        private static BlueprintCreationPromptRequest CreateEmptyRequest()
        {
            return new BlueprintCreationPromptRequest
            {
                EventKind = string.Empty,
                Text = string.Empty
            };
        }
    }

    internal static class BlueprintCreationPromptService
    {
        private const string StartEventKind = "start";
        private const string ExitEventKind = "exit";
        private const string StartText = "开始创建蓝图选区";
        private const string ExitText = "退出创建蓝图";
        private const int PromptDurationFrames = 90;
        private const int PromptColorR = 98;
        private const int PromptColorG = 185;
        private const int PromptColorB = 255;
        private const string LocalPromptContract = "local-popuptext-no-chat-no-network-no-player-state+head-position+duration-90-frames";

        private static readonly object SyncRoot = new object();
        private static IBlueprintCreationPromptSink _sink = TerrariaBlueprintCreationPromptCompat.Instance;
        private static BlueprintCreationPromptAttempt _lastAttempt = BlueprintCreationPromptAttempt.NotAttempted();

        public static void NotifyCreateStarted(BlueprintEntryCommandResult result)
        {
            if (result == null ||
                !result.Succeeded ||
                !result.Changed ||
                !string.Equals(result.Mode, BlueprintEntryModes.Creating, StringComparison.Ordinal))
            {
                return;
            }

            Show(StartEventKind, StartText);
        }

        public static void NotifyCreateExited(BlueprintEntryCommandResult result, bool wasCreating)
        {
            if (!wasCreating ||
                result == null ||
                !result.Succeeded ||
                !result.Changed ||
                string.Equals(result.Mode, BlueprintEntryModes.Creating, StringComparison.Ordinal))
            {
                return;
            }

            Show(ExitEventKind, ExitText);
        }

        internal static string GetLocalPromptContractForTesting()
        {
            return LocalPromptContract;
        }

        internal static int GetPromptDurationFramesForTesting()
        {
            return PromptDurationFrames;
        }

        internal static int GetPromptColorRForTesting()
        {
            return PromptColorR;
        }

        internal static int GetPromptColorGForTesting()
        {
            return PromptColorG;
        }

        internal static int GetPromptColorBForTesting()
        {
            return PromptColorB;
        }

        internal static void SetSinkForTesting(IBlueprintCreationPromptSink sink)
        {
            lock (SyncRoot)
            {
                _sink = sink ?? TerrariaBlueprintCreationPromptCompat.Instance;
                _lastAttempt = BlueprintCreationPromptAttempt.NotAttempted();
            }
        }

        internal static BlueprintCreationPromptAttempt GetLastAttemptForTesting()
        {
            lock (SyncRoot)
            {
                return _lastAttempt;
            }
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                _lastAttempt = BlueprintCreationPromptAttempt.NotAttempted();
            }
        }

        private static void Show(string eventKind, string text)
        {
            var request = new BlueprintCreationPromptRequest
            {
                EventKind = eventKind,
                Text = text,
                DurationFrames = PromptDurationFrames,
                ColorR = PromptColorR,
                ColorG = PromptColorG,
                ColorB = PromptColorB
            };

            IBlueprintCreationPromptSink sink;
            lock (SyncRoot)
            {
                sink = _sink ?? TerrariaBlueprintCreationPromptCompat.Instance;
            }

            var failureReason = string.Empty;
            var succeeded = false;
            try
            {
                succeeded = sink.TryShowBlueprintCreationPrompt(request, out failureReason);
            }
            catch (Exception error)
            {
                failureReason = error.Message;
                LogThrottle.ErrorThrottled(
                    "blueprint-creation-prompt-exception",
                    TimeSpan.FromSeconds(30),
                    "BlueprintCreationPromptService",
                    "Blueprint creation local prompt failed; exception swallowed.", error);
            }

            lock (SyncRoot)
            {
                _lastAttempt = BlueprintCreationPromptAttempt.Create(
                    request,
                    succeeded,
                    succeeded ? "shown" : "failed",
                    failureReason);
            }

            if (!succeeded)
            {
                LogThrottle.WarnThrottled(
                    "blueprint-creation-prompt-failed",
                    TimeSpan.FromSeconds(30),
                    "BlueprintCreationPromptService",
                    "Blueprint creation local prompt failed: " + (failureReason ?? string.Empty));
            }
        }
    }
}
