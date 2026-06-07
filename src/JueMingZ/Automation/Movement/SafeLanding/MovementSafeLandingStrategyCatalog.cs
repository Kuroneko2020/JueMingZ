using System;
using System.Collections.Generic;
using System.Text;
using JueMingZ.Actions;
using JueMingZ.Config;

namespace JueMingZ.Automation.Movement
{
    // Strategy selection ranks rescue plans only; execution, activation, and restore stay in queued actions.
    internal static class MovementSafeLandingStrategyCatalog
    {
        public const string Version = "safe_landing_strategy_framework_phase1";
        private static readonly IMovementSafeLandingStrategy[] Strategies =
        {
            new AlreadySafeStrategy(),
            new EquippedActiveAbilityStrategy(),
            new TemporaryEquipmentStrategy(),
            new TemporaryUmbrellaStrategy(),
            new GrappleStrategy(),
            new TeleportRodStrategy(),
            new NotImplementedStrategy()
        };

        public static MovementSafeLandingStrategySelection Evaluate(MovementSafeLandingStrategyContext context)
        {
            var selection = new MovementSafeLandingStrategySelection();
            for (var index = 0; index < Strategies.Length; index++)
            {
                var results = Strategies[index].Evaluate(context);
                if (results == null)
                {
                    continue;
                }

                foreach (var result in results)
                {
                    if (result != null)
                    {
                        selection.Evaluations.Add(result);
                    }
                }
            }

            SelectFirstCandidate(selection, context);
            BuildSummaries(selection);
            ApplySelectionToAnalysis(context, selection);
            return selection;
        }

        public static MovementSafeLandingStrategySelection EvaluateWithoutTemporaryEquipment(AppSettings settings, MovementSafeLandingAnalysis analysis)
        {
            return Evaluate(MovementSafeLandingStrategyContext.FromAnalysis(settings, analysis));
        }

        private static void SelectFirstCandidate(MovementSafeLandingStrategySelection selection, MovementSafeLandingStrategyContext context)
        {
            if (selection == null || selection.Evaluations == null)
            {
                return;
            }

            var selected = FindFirstCandidate(selection);
            if (selected == null)
            {
                return;
            }

            selection.SelectedEvaluation = selected;
            selection.SelectedPlan = BuildPlan(selected, context);
        }

        private static MovementSafeLandingStrategyEvaluation FindFirstCandidate(MovementSafeLandingStrategySelection selection)
        {
            for (var priority = 0; priority <= 8; priority++)
            {
                for (var index = 0; index < selection.Evaluations.Count; index++)
                {
                    var evaluation = selection.Evaluations[index];
                    if (evaluation == null ||
                        evaluation.Priority != priority ||
                        !evaluation.IsCandidate)
                    {
                        continue;
                    }

                    if (evaluation.IsReady)
                    {
                        return evaluation;
                    }

                    if (evaluation.BlocksLowerPriority)
                    {
                        return evaluation;
                    }
                }
            }

            return null;
        }

        private static MovementSafeLandingRescuePlan BuildPlan(MovementSafeLandingStrategyEvaluation selected, MovementSafeLandingStrategyContext context)
        {
            if (selected == null)
            {
                return null;
            }

            var plan = new MovementSafeLandingRescuePlan
            {
                Priority = selected.Priority,
                StrategyId = selected.StrategyId,
                ActionType = selected.ActionType,
                RequestKind = selected.RequestKind,
                RequiredChannels = selected.RequiredChannels,
                IsReady = selected.IsReady,
                RequiresTemporaryEquipment = selected.RequiresTemporaryEquipment,
                RequiresRestore = selected.RequiresRestore,
                EquipmentPlan = selected.EquipmentPlan,
                ExpiryCondition = "worldSwitch|death|mainMenu|textInput|queueConflict|actionTimeout|alreadySafe",
                MetadataSummary = "SafeLandingStrategy/SafeLandingActionType/SafeLandingPriority/SafeLandingImpact*"
            };

            if (selected.Priority == 0)
            {
                plan.IsNoAction = true;
                plan.RequestKind = InputActionKind.None;
                plan.ExpectedVerification = "already safe: no action submitted";
                plan.RestorePolicy = "none";
                return plan;
            }

            if (selected.Priority == 1)
            {
                plan.RequestPriority = InputActionPriority.High;
                plan.Timeout = TimeSpan.FromMilliseconds(450);
                plan.ExpectedVerification = ResolveActiveVerification(selected.StrategyId, selected.ActionType);
                plan.RestorePolicy = ResolveActiveRestorePolicy(selected.ActionType);
                return plan;
            }

            if (selected.Priority == 2 || selected.Priority == 3)
            {
                plan.RequestPriority = InputActionPriority.High;
                plan.Timeout = TimeSpan.FromMilliseconds(650);
                plan.ExpectedVerification = selected.Priority == 3
                    ? "selected hotbar slot contains umbrella; no activation pulse"
                    : "temporary equipment swap applied; active categories re-read capability before activation";
                plan.RestorePolicy = "stable landing restore; no-space keeps rescue item equipped and records manual required";
                return plan;
            }

            if (selected.Priority == 4)
            {
                plan.RequestPriority = InputActionPriority.High;
                plan.Timeout = TimeSpan.FromMilliseconds(450);
                plan.ExpectedVerification = "grapple input applied with mouse target near projected impact";
                plan.RestorePolicy = "release grapple input after short pulse; no equipment restore";
                return plan;
            }

            if (selected.Priority == 5)
            {
                plan.RequestPriority = InputActionPriority.High;
                plan.Timeout = TimeSpan.FromMilliseconds(650);
                plan.ExpectedVerification = "teleport rod used through vanilla ItemCheck at projected landing target";
                plan.RestorePolicy = "restore original selected slot after vanilla rod use";
                return plan;
            }

            plan.IsNoAction = true;
            plan.RequestKind = InputActionKind.None;
            plan.ExpectedVerification = "not implemented placeholder";
            plan.RestorePolicy = "none";
            return plan;
        }

        private static string ResolveActiveVerification(string strategyId, string actionType)
        {
            if (string.Equals(actionType, MovementSafeLandingActionTypes.GravityFlip, StringComparison.OrdinalIgnoreCase))
            {
                return "gravity direction changes; restore state becomes pending";
            }

            if (string.Equals(actionType, MovementSafeLandingActionTypes.QuickMount, StringComparison.OrdinalIgnoreCase))
            {
                return "quick mount input applied; mount cancel state may become pending";
            }

            if (string.Equals(strategyId, MovementSafeLandingStrategyIds.EquippedRocketBoots, StringComparison.OrdinalIgnoreCase))
            {
                return "jump pulse applied with rocket release";
            }

            return "jump pulse applied before Player.Update";
        }

        private static string ResolveActiveRestorePolicy(string actionType)
        {
            if (string.Equals(actionType, MovementSafeLandingActionTypes.GravityFlip, StringComparison.OrdinalIgnoreCase))
            {
                return "restore original gravity after stable safe probe";
            }

            if (string.Equals(actionType, MovementSafeLandingActionTypes.QuickMount, StringComparison.OrdinalIgnoreCase))
            {
                return "cancel mount after stable landing";
            }

            return "none";
        }

        private static void BuildSummaries(MovementSafeLandingStrategySelection selection)
        {
            if (selection == null)
            {
                return;
            }

            var evaluations = new StringBuilder();
            var candidates = new StringBuilder();
            var rejected = new StringBuilder();
            for (var index = 0; index < selection.Evaluations.Count; index++)
            {
                var evaluation = selection.Evaluations[index];
                if (evaluation == null)
                {
                    continue;
                }

                AppendPart(evaluations, evaluation.ToSummary());
                if (evaluation.IsCandidate)
                {
                    AppendPart(candidates, evaluation.ToSummary());
                }
                else if (!string.IsNullOrWhiteSpace(evaluation.SkipReason))
                {
                    AppendPart(rejected, evaluation.Priority.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                                       ":" + evaluation.StrategyId + ":" + evaluation.SkipReason);
                }
            }

            selection.EvaluationSummary = evaluations.ToString();
            selection.CandidateSummary = candidates.ToString();
            selection.RejectedStrategiesSummary = rejected.ToString();
            selection.SelectedPlanSummary = selection.SelectedPlan == null ? string.Empty : selection.SelectedPlan.ToSummary();
        }

        private static void ApplySelectionToAnalysis(MovementSafeLandingStrategyContext context, MovementSafeLandingStrategySelection selection)
        {
            if (context == null || context.Analysis == null || selection == null)
            {
                return;
            }

            var analysis = context.Analysis;
            analysis.StrategyCatalogVersion = Version;
            analysis.StrategyEvaluationSummary = selection.EvaluationSummary;
            analysis.CandidateSummary = selection.CandidateSummary;
            analysis.RejectedStrategiesSummary = selection.RejectedStrategiesSummary;
            analysis.SelectedPlanSummary = selection.SelectedPlanSummary;

            if (selection.SelectedEvaluation == null)
            {
                analysis.SelectedStrategyId = string.Empty;
                analysis.SelectedPriority = -1;
                analysis.SelectedActionType = string.Empty;
                return;
            }

            analysis.SelectedStrategyId = selection.SelectedEvaluation.StrategyId ?? string.Empty;
            analysis.SelectedPriority = selection.SelectedEvaluation.Priority;
            analysis.SelectedActionType = selection.SelectedEvaluation.ActionType ?? string.Empty;
            analysis.RescueWindow = selection.SelectedEvaluation.IsReady;
            if (!selection.SelectedEvaluation.IsReady && !string.IsNullOrWhiteSpace(selection.SelectedEvaluation.SkipReason))
            {
                analysis.SkipReason = selection.SelectedEvaluation.SkipReason;
            }
        }

        private static void AppendPart(StringBuilder builder, string value)
        {
            if (builder == null || string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            if (builder.Length > 0)
            {
                builder.Append(" | ");
            }

            builder.Append(value);
        }
    }
}
